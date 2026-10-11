using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;
using System.Windows.Threading;
using GestureDock.Interop;
using GestureDock.Models;

namespace GestureDock.UI.Overlay
{
    public partial class OverlayWindow : Window
    {
        private bool _isShown = false;
        private readonly DispatcherTimer _topmostKeepAliveTimer;
        private IntPtr _hwnd = IntPtr.Zero;

        private IntPtr _mouseHook = IntPtr.Zero;
        private IntPtr _keyboardHook = IntPtr.Zero;
        private NativeMethods.HookProc? _mouseHookProc;
        private NativeMethods.HookProc? _keyboardHookProc;

        private bool _hasCustomPosition = false;
        private double _customLeft = 0;
        private double _customTop = 0;

        private bool _isDragging = false;
        private NativeMethods.POINT _dragStartCursorPt;
        private double _dragStartLeft;
        private double _dragStartTop;

        public event Action? HomeRequested;
        public event Action? ClearAllRequested;
        public event Action? CloseAllRequested;

        public OverlayWindow()
        {
            InitializeComponent();

            _topmostKeepAliveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(80)
            };
            _topmostKeepAliveTimer.Tick += TopmostKeepAliveTimer_Tick;

            GripHandle.LostMouseCapture += (s, e) => EndDrag();
            PreviewMouseLeftButtonUp += (s, e) => EndDrag();
        }

        private void TopmostKeepAliveTimer_Tick(object? sender, EventArgs e)
        {
            if (!_isShown)
            {
                _topmostKeepAliveTimer.Stop();
                return;
            }

            var helper = new WindowInteropHelper(this);
            IntPtr hwnd = helper.Handle;
            if (hwnd != IntPtr.Zero && NativeMethods.IsWindow(hwnd))
            {
                NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                    NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var helper = new WindowInteropHelper(this);
            _hwnd = helper.Handle;
            IntPtr hwnd = _hwnd;

            int exStyle = (int)NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE);
            exStyle |= (int)(NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOPMOST);
            NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(exStyle));

            NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);

            try
            {
                // Attempt to elevate to UIACCESS band (Band 2) or SYSTEM_TOOLS (Band 16)
                if (!NativeMethods.SetWindowBand(hwnd, IntPtr.Zero, NativeMethods.ZBID_UIACCESS))
                {
                    NativeMethods.SetWindowBand(hwnd, IntPtr.Zero, NativeMethods.ZBID_SYSTEM_TOOLS);
                }
            }
            catch { }

            int cornerPreference = NativeMethods.DWMWCP_ROUND;
            NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPreference, sizeof(int));
        }

        public void ApplyTheme(AppTheme theme)
        {
            // Windows 11 Task View is an immersive dark backdrop, so the capsule uses
            // dark frosted acrylic glass to guarantee maximum readability and sharp Fluent aesthetics!
            CapsuleBorder.Background = new SolidColorBrush(MediaColor.FromArgb(0xE8, 0x1A, 0x1B, 0x22));
            CapsuleBorder.BorderBrush = new SolidColorBrush(MediaColor.FromArgb(0x38, 0xFF, 0xFF, 0xFF));
        }

        public void PositionAndShow(IntPtr hMonitor, IntPtr taskViewHwnd, DockPosition position, int marginBottom, int marginSide, bool animate)
        {
            var helper = new WindowInteropHelper(this);
            IntPtr hwnd = helper.EnsureHandle();
            _hwnd = hwnd;

            int exStyle = (int)NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE);
            exStyle |= (int)(NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOPMOST);
            NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(exStyle));

            if (hMonitor == IntPtr.Zero)
            {
                NativeMethods.GetCursorPos(out var pt);
                hMonitor = NativeMethods.MonitorFromPoint(pt, NativeMethods.MONITOR_DEFAULTTONEAREST);
            }

            var mi = new NativeMethods.MONITORINFO();
            mi.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.MONITORINFO));

            if (!NativeMethods.GetMonitorInfo(hMonitor, ref mi))
            {
                mi.rcWork = new NativeMethods.RECT
                {
                    Left = 0,
                    Top = 0,
                    Right = (int)SystemParameters.PrimaryScreenWidth,
                    Bottom = (int)SystemParameters.PrimaryScreenHeight
                };
            }

            uint dpi = NativeMethods.GetDpiForWindow(hwnd);
            double dpiScale = (dpi > 0) ? (dpi / 96.0) : 1.25;

            // Dimensions for the 3-button capsule with drag grip
            double w = 370;
            double h = 72;

            int physWidth = (int)Math.Round(w * dpiScale);
            int physHeight = (int)Math.Round(h * dpiScale);
            int physMarginSide = (int)Math.Round(marginSide * dpiScale);
            int physMarginBottom = (int)Math.Round(marginBottom * dpiScale);

            int workLeft = mi.rcWork.Left;
            int workTop = mi.rcWork.Top;
            int workWidth = mi.rcWork.Right - mi.rcWork.Left;
            int workHeight = mi.rcWork.Bottom - mi.rcWork.Top;

            int physX;
            switch (position)
            {
                case DockPosition.BottomLeft:
                    physX = workLeft + physMarginSide;
                    break;
                case DockPosition.BottomCenter:
                    physX = workLeft + (workWidth - physWidth) / 2;
                    break;
                case DockPosition.BottomRight:
                default:
                    physX = workLeft + workWidth - physWidth - physMarginSide;
                    break;
            }

            int physY = workTop + workHeight - physHeight - physMarginBottom;

            Width = w;
            Height = h;

            if (_hasCustomPosition)
            {
                // Validate and clamp custom position to visible screen boundaries
                double screenW = SystemParameters.PrimaryScreenWidth;
                double screenH = SystemParameters.PrimaryScreenHeight;
                _customLeft = Math.Clamp(_customLeft, 0, Math.Max(0, screenW - w));
                _customTop = Math.Clamp(_customTop, 0, Math.Max(0, screenH - h));

                physX = (int)Math.Round(_customLeft * dpiScale);
                physY = (int)Math.Round(_customTop * dpiScale);
                Left = _customLeft;
                Top = _customTop;
            }
            else
            {
                Left = physX / dpiScale;
                Top = physY / dpiScale;
            }

            Visibility = Visibility.Visible;
            Show();

            NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST,
                physX, physY, physWidth, physHeight,
                NativeMethods.SWP_SHOWWINDOW | NativeMethods.SWP_NOACTIVATE);
            NativeMethods.ShowWindow(hwnd, NativeMethods.SW_SHOWNOACTIVATE);

            _isShown = true;

            // Guarantee 100% opacity and clear any animations
            CapsuleBorder.BeginAnimation(UIElement.OpacityProperty, null);
            CapsuleBorder.Opacity = 1.0;
            if (CapsuleTransform != null)
            {
                CapsuleTransform.BeginAnimation(TranslateTransform.YProperty, null);
                CapsuleTransform.Y = 0;
            }

            // Start topmost re-assertion timer to stay above Task View's asynchronous DirectX composition
            _topmostKeepAliveTimer.Start();

            // Install instant dismiss hooks: the moment the user clicks any app tile or desktop,
            // or presses a dismiss key, the dock disappears AT THE HARDWARE EVENT TIMESTAMP (0ms)!
            InstallHooks();
        }

        public void FastHideNative()
        {
            _isShown = false;
            _isDragging = false;
            UninstallHooks();

            try
            {
                _topmostKeepAliveTimer?.Stop();
            }
            catch { }

            IntPtr h = _hwnd;
            if (h != IntPtr.Zero && NativeMethods.IsWindow(h))
            {
                // Instantly move off-screen and hide native HWND so it physically cannot linger for a single millisecond
                NativeMethods.SetWindowPos(h, IntPtr.Zero, -10000, -10000, 0, 0,
                    NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_HIDEWINDOW | NativeMethods.SWP_NOACTIVATE);
                NativeMethods.ShowWindow(h, NativeMethods.SW_HIDE);
            }

            try
            {
                Dispatcher.BeginInvoke(() =>
                {
                    Visibility = Visibility.Collapsed;
                });
            }
            catch { }
        }

        public void HideWithAnimation(bool animate)
        {
            FastHideNative();

            try
            {
                Visibility = Visibility.Collapsed;
                Hide();
            }
            catch { }
        }

        private void InstallHooks()
        {
            try
            {
                if (_mouseHook == IntPtr.Zero)
                {
                    _mouseHookProc = LowLevelMouseProc;
                    _mouseHook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _mouseHookProc, IntPtr.Zero, 0);
                }

                if (_keyboardHook == IntPtr.Zero)
                {
                    _keyboardHookProc = LowLevelKeyboardProc;
                    _keyboardHook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _keyboardHookProc, IntPtr.Zero, 0);
                }
            }
            catch { }
        }

        private void UninstallHooks()
        {
            try
            {
                if (_mouseHook != IntPtr.Zero)
                {
                    NativeMethods.UnhookWindowsHookEx(_mouseHook);
                    _mouseHook = IntPtr.Zero;
                    _mouseHookProc = null;
                }

                if (_keyboardHook != IntPtr.Zero)
                {
                    NativeMethods.UnhookWindowsHookEx(_keyboardHook);
                    _keyboardHook = IntPtr.Zero;
                    _keyboardHookProc = null;
                }
            }
            catch { }
        }

        private IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _isShown)
            {
                uint msg = (uint)wParam.ToInt64();

                if (msg == NativeMethods.WM_LBUTTONUP || msg == NativeMethods.WM_NCLBUTTONUP)
                {
                    _isDragging = false;
                }
                else if (!_isDragging && (
                    msg == NativeMethods.WM_LBUTTONDOWN ||
                    msg == NativeMethods.WM_RBUTTONDOWN ||
                    msg == NativeMethods.WM_MBUTTONDOWN ||
                    msg == NativeMethods.WM_NCLBUTTONDOWN ||
                    msg == NativeMethods.WM_NCRBUTTONDOWN ||
                    msg == NativeMethods.WM_NCMBUTTONDOWN))
                {
                    var hookStruct = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                    int clickX = hookStruct.pt.X;
                    int clickY = hookStruct.pt.Y;

                    IntPtr h = _hwnd;
                    if (h != IntPtr.Zero && NativeMethods.IsWindow(h))
                    {
                        if (NativeMethods.GetWindowRect(h, out var rect))
                        {
                            bool insideDock = (clickX >= rect.Left && clickX <= rect.Right &&
                                               clickY >= rect.Top && clickY <= rect.Bottom);

                            if (!insideDock)
                            {
                                // User clicked outside dock (selecting an app card or desktop)!
                                // Dismiss dock INSTANTLY at hardware click timestamp before zoom animation starts!
                                FastHideNative();
                            }
                        }
                    }
                }
            }

            return NativeMethods.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }

        private IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _isShown)
            {
                var kbd = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
                // Esc (0x1B), Enter (0x0D), Win keys (0x5B, 0x5C)
                if (kbd.vkCode == 0x1B || kbd.vkCode == 0x0D || kbd.vkCode == 0x5B || kbd.vkCode == 0x5C)
                {
                    FastHideNative();
                }
            }

            return NativeMethods.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
        }

        private void GripHandle_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                // Double-click resets custom position to default!
                _hasCustomPosition = false;
                _isDragging = false;
                try { GripHandle.ReleaseMouseCapture(); } catch { }

                NativeMethods.GetCursorPos(out var curPt);
                IntPtr hMon = NativeMethods.MonitorFromPoint(curPt, NativeMethods.MONITOR_DEFAULTTONEAREST);
                PositionAndShow(hMon, IntPtr.Zero, DockPosition.BottomRight, 32, 32, false);
                e.Handled = true;
                return;
            }

            _isDragging = true;
            NativeMethods.GetCursorPos(out _dragStartCursorPt);
            _dragStartLeft = Left;
            _dragStartTop = Top;
            GripHandle.CaptureMouse();
            e.Handled = true;
        }

        private void GripHandle_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_isDragging) return;

            NativeMethods.GetCursorPos(out var currentPt);
            double deltaXPhys = currentPt.X - _dragStartCursorPt.X;
            double deltaYPhys = currentPt.Y - _dragStartCursorPt.Y;

            var helper = new WindowInteropHelper(this);
            uint dpi = NativeMethods.GetDpiForWindow(helper.Handle);
            double dpiScale = (dpi > 0) ? (dpi / 96.0) : 1.25;

            double newLeft = _dragStartLeft + (deltaXPhys / dpiScale);
            double newTop = _dragStartTop + (deltaYPhys / dpiScale);

            double dockW = ActualWidth > 0 ? ActualWidth : 370;
            double dockH = ActualHeight > 0 ? ActualHeight : 72;

            double screenW = SystemParameters.PrimaryScreenWidth;
            double screenH = SystemParameters.PrimaryScreenHeight;

            newLeft = Math.Clamp(newLeft, 0, Math.Max(0, screenW - dockW));
            newTop = Math.Clamp(newTop, 0, Math.Max(0, screenH - dockH));

            Left = newLeft;
            Top = newTop;
            _customLeft = newLeft;
            _customTop = newTop;
            _hasCustomPosition = true;

            int physX = (int)Math.Round(newLeft * dpiScale);
            int physY = (int)Math.Round(newTop * dpiScale);
            int physW = (int)Math.Round(dockW * dpiScale);
            int physH = (int)Math.Round(dockH * dpiScale);

            NativeMethods.SetWindowPos(helper.Handle, NativeMethods.HWND_TOPMOST,
                physX, physY, physW, physH,
                NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);

            e.Handled = true;
        }

        private void GripHandle_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            EndDrag();
            e.Handled = true;
        }

        private void EndDrag()
        {
            if (_isDragging)
            {
                _isDragging = false;
                try
                {
                    GripHandle.ReleaseMouseCapture();
                }
                catch { }
            }
        }

        private static bool IsChildOfButton(DependencyObject? obj)
        {
            while (obj != null)
            {
                if (obj is System.Windows.Controls.Button) return true;
                obj = System.Windows.Media.VisualTreeHelper.GetParent(obj);
            }
            return false;
        }

        private void BtnHome_Click(object sender, RoutedEventArgs e)
        {
            HomeRequested?.Invoke();
        }

        private void BtnClearAll_Click(object sender, RoutedEventArgs e)
        {
            ClearAllRequested?.Invoke();
        }

        private void BtnCloseAll_Click(object sender, RoutedEventArgs e)
        {
            CloseAllRequested?.Invoke();
        }
    }
}
