using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GestureDock.Interop;
using GestureDock.Models;

namespace GestureDock.Services
{
    public class WindowManager
    {
        private static readonly HashSet<string> ExcludedProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "dwm",
            "winlogon",
            "csrss",
            "services",
            "svchost",
            "lsass",
            "smss",
            "system",
            "gesturedock",
            "startmenuexperiencehost",
            "shellexperiencehost",
            "searchhost",
            "searchapp",
            "textinputhost",
            "lockapp",
            "fontdrvhost",
            "sihost"
        };

        private static readonly HashSet<string> ExcludedClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Progman",
            "WorkerW",
            "Shell_TrayWnd",
            "Shell_SecondaryTrayWnd",
            "XamlExplorerHostIslandWindow",
            "Windows.UI.Core.CoreWindow",
            "ThumbnailDeviceHelperWnd",
            "DummyDWMListenerWindow",
            "Internet Explorer_Hidden",
            "Winit Thread Event Target"
        };

        /// <summary>
        /// Enumerates top-level interactive user windows that can safely be closed.
        /// </summary>
        public static List<WindowAppItem> GetClosableWindows()
        {
            var results = new List<WindowAppItem>();
            uint currentPid = (uint)Process.GetCurrentProcess().Id;

            IntPtr hDesk = NativeMethods.OpenInputDesktop(0, false, 0x01FF);
            try
            {
                NativeMethods.EnumDesktopWindows(hDesk, (hwnd, lParam) =>
                {
                    if (!NativeMethods.IsWindowVisible(hwnd)) return true;

                    // Exclude owned windows (e.g. child dialogs owned by another window)
                    IntPtr owner = NativeMethods.GetWindow(hwnd, NativeMethods.GW_OWNER);
                    int exStyle = (int)NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE);

                    // Skip ToolWindows unless they have no owner and are an AppWindow
                    if ((exStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0 && owner != IntPtr.Zero)
                    {
                        return true;
                    }

                    var sbTitle = new StringBuilder(512);
                    var sbClass = new StringBuilder(256);
                    NativeMethods.GetWindowText(hwnd, sbTitle, 512);
                    NativeMethods.GetClassName(hwnd, sbClass, 256);

                    string title = sbTitle.ToString().Trim();
                    string className = sbClass.ToString().Trim();

                    if (string.IsNullOrEmpty(title)) return true;
                    if (ExcludedClasses.Contains(className)) return true;

                    NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
                    if (pid == currentPid || pid == 0) return true;

                    string procName = "";
                    try
                    {
                        using var proc = Process.GetProcessById((int)pid);
                        procName = proc.ProcessName;
                    }
                    catch
                    {
                        return true;
                    }

                    // Special Explorer Handling: Only include real File Explorer folder windows (CabinetWClass)
                    if (string.Equals(procName, "explorer", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.Equals(className, "CabinetWClass", StringComparison.OrdinalIgnoreCase))
                        {
                            return true; // Skip Taskbar, desktop background, start menu
                        }
                        procName = "File Explorer";
                    }

                    // Friendly names for shells and UWP apps
                    if (string.Equals(procName, "ApplicationFrameHost", StringComparison.OrdinalIgnoreCase))
                    {
                        procName = !string.IsNullOrEmpty(title) ? title : "Windows App";
                    }
                    else if (string.Equals(procName, "SystemSettings", StringComparison.OrdinalIgnoreCase))
                    {
                        procName = "Settings";
                    }
                    else if (string.Equals(procName, "powershell", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(procName, "pwsh", StringComparison.OrdinalIgnoreCase))
                    {
                        procName = "PowerShell";
                    }
                    else if (string.Equals(procName, "cmd", StringComparison.OrdinalIgnoreCase))
                    {
                        procName = "Command Prompt";
                    }
                    else if (string.Equals(procName, "WindowsTerminal", StringComparison.OrdinalIgnoreCase))
                    {
                        procName = "Terminal";
                    }

                    if (ExcludedProcesses.Contains(procName)) return true;

                    // Ensure window has physical dimensions (not collapsed 0x0 helper window)
                    if (NativeMethods.GetWindowRect(hwnd, out var rect))
                    {
                        if (rect.Width <= 20 || rect.Height <= 20) return true;
                    }

                    results.Add(new WindowAppItem
                    {
                        Hwnd = hwnd,
                        Title = title,
                        ProcessName = procName,
                        ProcessId = pid,
                        IsSelected = true,
                        Status = "Ready"
                    });

                    return true;
                }, IntPtr.Zero);
            }
            finally
            {
                if (hDesk != IntPtr.Zero)
                {
                    NativeMethods.CloseDesktop(hDesk);
                }
            }

            return results;
        }

        /// <summary>
        /// Gracefully requests close for target windows via WM_CLOSE.
        /// Preserves user save dialogs and reports progress.
        /// </summary>
        public static async Task CloseWindowsAsync(
            List<WindowAppItem> items,
            bool allowForceClose,
            int timeoutSeconds,
            Action<WindowAppItem>? onItemUpdated,
            CancellationToken ct = default)
        {
            foreach (var item in items)
            {
                if (!item.IsSelected) continue;
                if (ct.IsCancellationRequested) break;

                item.Status = "Closing...";
                onItemUpdated?.Invoke(item);

                // Send WM_CLOSE and SC_CLOSE gracefully for maximum Win32 & UWP compatibility
                NativeMethods.PostMessage(item.Hwnd, NativeMethods.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                NativeMethods.PostMessage(item.Hwnd, NativeMethods.WM_SYSCOMMAND, (IntPtr)NativeMethods.SC_CLOSE, IntPtr.Zero);
            }

            // Wait gracefully for apps to process WM_CLOSE (up to timeout)
            int checkIntervalMs = 250;
            int maxChecks = (timeoutSeconds * 1000) / checkIntervalMs;

            for (int i = 0; i < maxChecks; i++)
            {
                if (ct.IsCancellationRequested) break;
                await Task.Delay(checkIntervalMs, ct);

                bool anyStillPending = false;
                foreach (var item in items)
                {
                    if (!item.IsSelected || item.Succeeded) continue;

                    if (!NativeMethods.IsWindow(item.Hwnd) || !NativeMethods.IsWindowVisible(item.Hwnd))
                    {
                        item.Succeeded = true;
                        item.Status = "Closed ✓";
                        onItemUpdated?.Invoke(item);
                    }
                    else
                    {
                        anyStillPending = true;
                    }
                }

                if (!anyStillPending) break;
            }

            // Final status check
            foreach (var item in items)
            {
                if (!item.IsSelected) continue;

                if (NativeMethods.IsWindow(item.Hwnd) && NativeMethods.IsWindowVisible(item.Hwnd))
                {
                    if (allowForceClose && !string.Equals(item.ProcessName, "File Explorer", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            var proc = Process.GetProcessById((int)item.ProcessId);
                            if (!string.Equals(proc.ProcessName, "explorer", StringComparison.OrdinalIgnoreCase))
                            {
                                proc.Kill();
                                item.Succeeded = true;
                                item.Status = "Terminated";
                            }
                        }
                        catch
                        {
                            item.Succeeded = false;
                            item.Status = "Failed to terminate";
                        }
                    }
                    else
                    {
                        item.Succeeded = false;
                        item.Status = "Prompt open / Busy ⚠";
                    }
                }
                else
                {
                    item.Succeeded = true;
                    item.Status = "Closed ✓";
                }

                onItemUpdated?.Invoke(item);
            }
        }
    }
}
