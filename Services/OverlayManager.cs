using System;
using System.Windows;
using WpfApp = System.Windows.Application;
using GestureDock.Models;
using GestureDock.Services;
using GestureDock.UI.Overlay;

namespace GestureDock.Services
{
    public class OverlayManager
    {
        private readonly SettingsManager _settingsManager;
        private OverlayWindow? _overlayWindow;
        private bool _isVisible;

        public bool IsVisible => _isVisible;

        public event Action? HomeTriggered;
        public event Action? ClearAllTriggered;
        public event Action? CloseAllTriggered;

        public OverlayManager(SettingsManager settingsManager)
        {
            _settingsManager = settingsManager;
            _settingsManager.SettingsChanged += OnSettingsChanged;
        }

        public void Initialize()
        {
            WpfApp.Current.Dispatcher.Invoke(() =>
            {
                _overlayWindow = new OverlayWindow();
                _overlayWindow.HomeRequested += () => HomeTriggered?.Invoke();
                _overlayWindow.ClearAllRequested += () => ClearAllTriggered?.Invoke();
                _overlayWindow.CloseAllRequested += () => CloseAllTriggered?.Invoke();

                var settings = _settingsManager.Current;
                _overlayWindow.ApplyTheme(settings.Theme);

                // PRE-WARM: Force WPF and DirectX device initialization on startup!
                _overlayWindow.Left = -5000;
                _overlayWindow.Top = -5000;
                _overlayWindow.Width = 345;
                _overlayWindow.Height = 72;
                _overlayWindow.Show();
                _overlayWindow.Hide();
            });
        }

        public void OnTaskViewStateChanged(bool isTaskViewActive, IntPtr taskViewHwnd, IntPtr activeMonitor)
        {
            if (!_settingsManager.Current.EnableOverlay)
            {
                HideManually();
                return;
            }

            if (isTaskViewActive)
            {
                ShowOnMonitor(activeMonitor, taskViewHwnd);
            }
            else
            {
                // Fast Win32 atomic offscreen hide directly on background thread (<0.1ms latency)
                try
                {
                    _overlayWindow?.FastHideNative();
                }
                catch { }

                HideManually();
            }
        }

        public void ShowOnMonitor(IntPtr hMonitor, IntPtr taskViewHwnd)
        {
            WpfApp.Current.Dispatcher.Invoke(() =>
            {
                if (_overlayWindow == null) Initialize();
                if (_overlayWindow == null) return;

                var settings = _settingsManager.Current;
                _overlayWindow.ApplyTheme(settings.Theme);
                _overlayWindow.PositionAndShow(hMonitor, taskViewHwnd, settings.Position, settings.MarginBottom, settings.MarginSide, settings.EnableAnimations);
                _isVisible = true;

                try
                {
                    System.IO.File.AppendAllText(@"C:\Users\crs14\.gemini\antigravity\scratch\GestureDock\app_debug.txt", $"[{DateTime.Now:HH:mm:ss.fff}] Overlay displayed. Left={_overlayWindow.Left}, Top={_overlayWindow.Top}, TaskViewHwnd=0x{taskViewHwnd.ToInt64():X}\r\n");
                }
                catch { }
            });
        }

        public void ShowManually()
        {
            ShowOnMonitor(IntPtr.Zero, IntPtr.Zero);
        }

        public void HideManually()
        {
            WpfApp.Current.Dispatcher.Invoke(() =>
            {
                if (_overlayWindow == null) return;
                var settings = _settingsManager.Current;
                _overlayWindow.HideWithAnimation(settings.EnableAnimations);
                _isVisible = false;
            });
        }

        private void OnSettingsChanged()
        {
            WpfApp.Current.Dispatcher.Invoke(() =>
            {
                if (_overlayWindow == null) return;
                var settings = _settingsManager.Current;
                _overlayWindow.ApplyTheme(settings.Theme);
            });
        }
    }
}
