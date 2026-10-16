using System;
using System.Linq;
using System.Threading;
using System.Windows;
using GestureDock.Models;
using GestureDock.Services;
using GestureDock.UI.Diagnostic;
using GestureDock.UI.Overlay;
using GestureDock.UI.Settings;
using GestureDock.UI.Tray;

namespace GestureDock
{
    public partial class App : System.Windows.Application
    {
        private static Mutex? _singleInstanceMutex;
        private SettingsManager _settingsManager = null!;
        private TaskViewDetector _detector = null!;
        private OverlayManager _overlayManager = null!;
        private TrayIconManager _trayManager = null!;
        private SettingsWindow? _settingsWindow;
        private DiagnosticWindow? _diagnosticWindow;
        private HotkeyManager _hotkeyManager = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            Log("OnStartup entered.");
            try
            {
                _settingsManager = new SettingsManager();
                Log("SettingsManager loaded.");

                // Check command-line arguments for shortcut actions
                if (e.Args.Length > 0)
                {
                    string arg = string.Join(" ", e.Args).ToLowerInvariant();
                    Log("Command line args: " + arg);
                    if (arg.Contains("--action home"))
                    {
                        DesktopManager.ShowDesktop();
                        Shutdown();
                        return;
                    }
                    else if (arg.Contains("--action clearall"))
                    {
                        ExecuteClearAllAtOnce();
                        System.Threading.Thread.Sleep(1500);
                        Shutdown();
                        return;
                    }
                    else if (arg.Contains("--action closeall"))
                    {
                        ExecuteCloseAll(false);
                        Shutdown();
                        return;
                    }
                    else if (arg.Contains("--action test"))
                    {
                        var overlayWindow = new GestureDock.UI.Overlay.OverlayWindow();
                        overlayWindow.PositionAndShow(IntPtr.Zero, IntPtr.Zero, _settingsManager.Current.Position, _settingsManager.Current.MarginBottom, _settingsManager.Current.MarginSide, false);
                        Log($"Test overlay displayed. Left={overlayWindow.Left}, Top={overlayWindow.Top}");
                        System.Threading.Thread.Sleep(3000);
                        overlayWindow.Close();
                        Shutdown();
                        return;
                    }
                }

                // Single instance check for the tray background app
                const string mutexName = "Local\\GestureDock_SingleInstance_Mutex";
                _singleInstanceMutex = new Mutex(true, mutexName, out bool isNewInstance);
                Log("Mutex acquired: " + isNewInstance);
                if (!isNewInstance)
                {
                    Log("Existing instance running. Shutting down duplicate.");
                    Shutdown();
                    return;
                }

                _overlayManager = new OverlayManager(_settingsManager);
                _overlayManager.Initialize();
                Log("OverlayManager initialized.");

                _overlayManager.HomeTriggered += () =>
                {
                    DesktopManager.ShowDesktop();
                    _overlayManager.HideManually();
                };

                _overlayManager.ClearAllTriggered += () =>
                {
                    ExecuteClearAllAtOnce();
                    _overlayManager.HideManually();
                };

                _overlayManager.CloseAllTriggered += () =>
                {
                    ExecuteCloseAll(true);
                };

                _detector = new TaskViewDetector();
                _detector.TaskViewStateChanged += (s, active) =>
                {
                    _overlayManager.OnTaskViewStateChanged(active, _detector.TaskViewHwnd, _detector.ActiveMonitor);
                };
                _detector.Start();
                Log("TaskViewDetector started.");

                // Initialize Tray
                _trayManager = new TrayIconManager(
                    _settingsManager,
                    openSettings: ShowSettings,
                    openDiagnostic: ShowDiagnostics,
                    testOverlay: () => _overlayManager.ShowManually(),
                    showDesktop: () => DesktopManager.ShowDesktop(),
                    closeAllApps: () => ExecuteCloseAll(true),
                    exitApp: ExitApplication);
                Log("TrayIconManager initialized.");

                // Initialize Hotkeys for Touchpad Custom Shortcuts & Keyboard
                _hotkeyManager = new HotkeyManager();
                _hotkeyManager.Initialize();
                _hotkeyManager.HomePressed += () =>
                {
                    Log("Hotkey HOME triggered.");
                    DesktopManager.ShowDesktop();
                };
                _hotkeyManager.CloseAllPressed += () =>
                {
                    Log("Hotkey CLOSE ALL triggered.");
                    ExecuteCloseAll(false);
                };
                _hotkeyManager.DockTogglePressed += () =>
                {
                    Log("Hotkey DOCK TOGGLE triggered.");
                    if (_overlayManager.IsVisible)
                        _overlayManager.HideManually();
                    else
                        _overlayManager.ShowManually();
                };
                Log("HotkeyManager initialized.");

                // Ensure desktop shortcuts are never recreated automatically
                StartupManager.RemoveDesktopShortcuts();
                Log("Desktop shortcuts checked and cleaned.");
            }
            catch (Exception ex)
            {
                Log("CRITICAL ERROR in OnStartup: " + ex);
            }
        }

        private static void Log(string message)
        {
            try
            {
                System.IO.File.AppendAllText(@"C:\Users\crs14\.gemini\antigravity\scratch\GestureDock\app_debug.txt", $"[{DateTime.Now:HH:mm:ss.fff}] {message}\r\n");
            }
            catch { }
        }

        private void ExecuteCloseAll(bool fromOverlay)
        {
            Log($"ExecuteCloseAll started (fromOverlay={fromOverlay})");
            try
            {
                var closable = WindowManager.GetClosableWindows();
                Log($"Closable windows count: {closable.Count}");
                foreach (var item in closable)
                {
                    Log($" - Closable: [{item.ProcessName}] {item.Title}");
                }

                if (closable.Count == 0)
                {
                    if (!fromOverlay)
                    {
                        System.Windows.MessageBox.Show("No open user applications found to close.", "GestureDock", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    return;
                }

                if (_settingsManager.Current.ConfirmBeforeCloseAll)
                {
                    Log("Opening CloseConfirmWindow dialog...");
                    var confirmWindow = new CloseConfirmWindow(closable, _settingsManager.Current);
                    confirmWindow.ShowDialog();
                    Log("CloseConfirmWindow closed.");
                }
                else
                {
                    Log("Closing windows directly without confirmation...");
                    _ = WindowManager.CloseWindowsAsync(
                        closable,
                        _settingsManager.Current.AllowForceCloseAfterTimeout,
                        _settingsManager.Current.ForceCloseTimeoutSeconds,
                        null);
                }
            }
            catch (Exception ex)
            {
                Log("Error in ExecuteCloseAll: " + ex);
            }
        }

        private void ExecuteClearAllAtOnce()
        {
            Log("ExecuteClearAllAtOnce started: closing all user windows immediately.");
            try
            {
                var closable = WindowManager.GetClosableWindows();
                Log($"Closable windows count for Clear All: {closable.Count}");
                foreach (var item in closable)
                {
                    Log($" - Clear All target: [{item.ProcessName}] {item.Title}");
                }

                if (closable.Count == 0) return;

                // Close all windows at once immediately
                _ = WindowManager.CloseWindowsAsync(
                    closable,
                    allowForceClose: true,
                    timeoutSeconds: 2,
                    onItemUpdated: null);
            }
            catch (Exception ex)
            {
                Log("Error in ExecuteClearAllAtOnce: " + ex);
            }
        }

        private void ShowSettings()
        {
            if (_settingsWindow == null || !_settingsWindow.IsLoaded)
            {
                _settingsWindow = new SettingsWindow(_settingsManager);
                _settingsWindow.Closed += (s, e) => _settingsWindow = null;
                _settingsWindow.Show();
            }
            else
            {
                _settingsWindow.Activate();
            }
        }

        private void ShowDiagnostics()
        {
            if (_diagnosticWindow == null || !_diagnosticWindow.IsLoaded)
            {
                _diagnosticWindow = new DiagnosticWindow(_detector, _overlayManager);
                _diagnosticWindow.Closed += (s, e) => _diagnosticWindow = null;
                _diagnosticWindow.Show();
            }
            else
            {
                _diagnosticWindow.Activate();
            }
        }

        private void ExitApplication()
        {
            _hotkeyManager?.Dispose();
            _detector?.Dispose();
            _trayManager?.Dispose();
            _singleInstanceMutex?.ReleaseMutex();
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _hotkeyManager?.Dispose();
            _detector?.Dispose();
            _trayManager?.Dispose();
            base.OnExit(e);
        }
    }
}
