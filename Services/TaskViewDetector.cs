using System;
using System.Text;
using System.Threading;
using GestureDock.Interop;

namespace GestureDock.Services
{
    public class TaskViewDetector : IDisposable
    {
        private Thread? _monitorThread;
        private volatile bool _isRunning = false;
        private IntPtr _lastTaskViewHwnd = IntPtr.Zero;

        public bool IsTaskViewActive { get; private set; }
        public IntPtr TaskViewHwnd { get; private set; }
        public IntPtr ActiveMonitor { get; private set; }

        public event EventHandler<bool>? TaskViewStateChanged;
        public event EventHandler<string>? DiagnosticLog;

        public TaskViewDetector()
        {
        }

        public void Start()
        {
            if (_isRunning) return;
            _isRunning = true;

            _monitorThread = new Thread(MonitorWorkerLoop)
            {
                IsBackground = true,
                Priority = ThreadPriority.Normal,
                Name = "GestureDock_TaskViewDetector"
            };
            _monitorThread.Start();
            Log("TaskViewDetector dedicated thread started.");
        }

        public void Stop()
        {
            _isRunning = false;
            try
            {
                _monitorThread?.Join(500);
            }
            catch { }
            Log("TaskViewDetector stopped.");
        }

        private void MonitorWorkerLoop()
        {
            // CRITICAL ON WINDOWS 11:
            // Background threads MUST explicitly attach to the active interactive input desktop
            // to observe GetForegroundWindow() transitions across modern Xaml islands.
            IntPtr hDesk = NativeMethods.OpenInputDesktop(0, false, 0x01FF);
            if (hDesk != IntPtr.Zero)
            {
                NativeMethods.SetThreadDesktop(hDesk);
            }

            IntPtr lastFg = IntPtr.Zero;

            while (_isRunning)
            {
                try
                {
                    IntPtr fg = NativeMethods.GetForegroundWindow();

                    if (fg != IntPtr.Zero)
                    {
                        NativeMethods.GetWindowThreadProcessId(fg, out uint fgPid);
                        if (fgPid != (uint)Environment.ProcessId)
                        {
                            if (fg != lastFg)
                            {
                                lastFg = fg;
                                EvaluateForeground(fg);
                            }
                        }
                    }

                    if (IsTaskViewActive)
                    {
                        // While Task View is active, verify it hasn't closed silently or been cloaked by DWM
                        bool tvVisible = _lastTaskViewHwnd != IntPtr.Zero &&
                                         NativeMethods.IsWindow(_lastTaskViewHwnd) &&
                                         NativeMethods.IsWindowVisible(_lastTaskViewHwnd);

                        bool isCloaked = false;
                        if (tvVisible && _lastTaskViewHwnd != IntPtr.Zero)
                        {
                            if (NativeMethods.DwmGetWindowAttribute(_lastTaskViewHwnd, NativeMethods.DWMWA_CLOAKED, out int cloakedVal, sizeof(int)) == 0)
                            {
                                isCloaked = cloakedVal != 0;
                            }
                        }

                        if (!tvVisible || isCloaked)
                        {
                            lastFg = IntPtr.Zero;
                            SetTaskViewState(false, IntPtr.Zero);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log($"Exception in detector loop: {ex.Message}");
                }

                // 15ms polling interval: matches display refresh rate (<1 frame latency), 0% CPU
                Thread.Sleep(15);
            }

            if (hDesk != IntPtr.Zero)
            {
                NativeMethods.CloseDesktop(hDesk);
            }
        }

        private void EvaluateForeground(IntPtr fg)
        {
            if (fg == IntPtr.Zero) return;

            NativeMethods.GetWindowThreadProcessId(fg, out uint fgPid);
            if (fgPid == (uint)Environment.ProcessId)
            {
                // Ignore focus changes to our own process (e.g. clicking overlay buttons)
                return;
            }

            var sbClass = new StringBuilder(256);
            var sbTitle = new StringBuilder(256);
            NativeMethods.GetClassName(fg, sbClass, 256);
            NativeMethods.GetWindowText(fg, sbTitle, 256);

            string cls = sbClass.ToString();
            string ttl = sbTitle.ToString();

            Log($"FG: 0x{fg.ToInt64():X} | '{cls}' | '{ttl}'");

            bool isTaskView = IsWindowTaskView(cls, ttl, fg);

            if (isTaskView)
            {
                _lastTaskViewHwnd = fg;
                SetTaskViewState(true, fg);
            }
            else if (IsTaskViewActive)
            {
                // The moment foreground moves away from Task View to ANY window
                // (including ForegroundStaging, desktop, taskbar, or an app),
                // dismiss the overlay INSTANTLY without waiting for dismiss animations!
                SetTaskViewState(false, IntPtr.Zero);
            }
            else if (cls.Equals("ForegroundStaging", StringComparison.OrdinalIgnoreCase))
            {
                // Foreground staging transition before Task View arrives while inactive
                return;
            }
        }

        private void SetTaskViewState(bool active, IntPtr hwnd)
        {
            if (active == IsTaskViewActive) return;

            IsTaskViewActive = active;
            TaskViewHwnd = hwnd;

            if (!active)
            {
                _lastTaskViewHwnd = IntPtr.Zero;
            }

            if (active && hwnd != IntPtr.Zero)
            {
                ActiveMonitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
            }
            else
            {
                NativeMethods.GetCursorPos(out var pt);
                ActiveMonitor = NativeMethods.MonitorFromPoint(pt, NativeMethods.MONITOR_DEFAULTTONEAREST);
            }

            Log($"TaskView state changed -> Active: {IsTaskViewActive}, HWND: 0x{hwnd.ToInt64():X}");
            TaskViewStateChanged?.Invoke(this, IsTaskViewActive);
        }

        public static bool IsWindowTaskView(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return false;
            var sbClass = new StringBuilder(256);
            var sbTitle = new StringBuilder(256);
            NativeMethods.GetClassName(hwnd, sbClass, 256);
            NativeMethods.GetWindowText(hwnd, sbTitle, 256);
            return IsWindowTaskView(sbClass.ToString(), sbTitle.ToString(), hwnd);
        }

        public static bool IsWindowTaskView(string cls, string ttl, IntPtr hwnd)
        {
            // Windows 11 Task View Modern Host
            if (cls.Equals("XamlExplorerHostIslandWindow", StringComparison.OrdinalIgnoreCase))
            {
                if (ttl.IndexOf("Task View", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    ttl.IndexOf("Task Switching", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            // Fallback for legacy Windows 10/11 CharmView
            if (cls.Equals("Shell_CharmView", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private void Log(string message)
        {
            string line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
            try
            {
                System.IO.File.AppendAllText(@"C:\Users\crs14\.gemini\antigravity\scratch\GestureDock\app_debug.txt", line + "\r\n");
            }
            catch { }
            DiagnosticLog?.Invoke(this, line);
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
