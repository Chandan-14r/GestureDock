using System;
using System.Windows.Interop;
using GestureDock.Interop;

namespace GestureDock.Services
{
    public class HotkeyManager : IDisposable
    {
        private HwndSource? _source;
        private IntPtr _hwnd = IntPtr.Zero;

        public const int HOTKEY_ID_HOME = 9001;
        public const int HOTKEY_ID_CLOSEALL = 9002;
        public const int HOTKEY_ID_DOCK = 9003;
        public const int HOTKEY_ID_HOME_SHIFT = 9004;
        public const int HOTKEY_ID_CLOSEALL_SHIFT = 9005;
        public const int HOTKEY_ID_DOCK_SHIFT = 9006;

        public event Action? HomePressed;
        public event Action? CloseAllPressed;
        public event Action? DockTogglePressed;

        public void Initialize()
        {
            var parameters = new HwndSourceParameters("GestureDock_HotkeyListener")
            {
                Width = 0,
                Height = 0,
                WindowStyle = 0x800000 // WS_BORDER invisible message window
            };

            _source = new HwndSource(parameters);
            _source.AddHook(HwndHook);
            _hwnd = _source.Handle;

            // Register Ctrl + Alt combinations
            NativeMethods.RegisterHotKey(_hwnd, HOTKEY_ID_HOME, NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT, 0x48 /* 'H' */);
            NativeMethods.RegisterHotKey(_hwnd, HOTKEY_ID_CLOSEALL, NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT, 0x58 /* 'X' */);
            NativeMethods.RegisterHotKey(_hwnd, HOTKEY_ID_DOCK, NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT, 0x44 /* 'D' */);

            // Register Ctrl + Shift combinations
            NativeMethods.RegisterHotKey(_hwnd, HOTKEY_ID_HOME_SHIFT, NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT | NativeMethods.MOD_NOREPEAT, 0x48 /* 'H' */);
            NativeMethods.RegisterHotKey(_hwnd, HOTKEY_ID_CLOSEALL_SHIFT, NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT | NativeMethods.MOD_NOREPEAT, 0x58 /* 'X' */);
            NativeMethods.RegisterHotKey(_hwnd, HOTKEY_ID_DOCK_SHIFT, NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT | NativeMethods.MOD_NOREPEAT, 0x44 /* 'D' */);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == (int)NativeMethods.WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                switch (id)
                {
                    case HOTKEY_ID_HOME:
                    case HOTKEY_ID_HOME_SHIFT:
                        HomePressed?.Invoke();
                        handled = true;
                        break;
                    case HOTKEY_ID_CLOSEALL:
                    case HOTKEY_ID_CLOSEALL_SHIFT:
                        CloseAllPressed?.Invoke();
                        handled = true;
                        break;
                    case HOTKEY_ID_DOCK:
                    case HOTKEY_ID_DOCK_SHIFT:
                        DockTogglePressed?.Invoke();
                        handled = true;
                        break;
                }
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            if (_hwnd != IntPtr.Zero)
            {
                NativeMethods.UnregisterHotKey(_hwnd, HOTKEY_ID_HOME);
                NativeMethods.UnregisterHotKey(_hwnd, HOTKEY_ID_CLOSEALL);
                NativeMethods.UnregisterHotKey(_hwnd, HOTKEY_ID_DOCK);
                NativeMethods.UnregisterHotKey(_hwnd, HOTKEY_ID_HOME_SHIFT);
                NativeMethods.UnregisterHotKey(_hwnd, HOTKEY_ID_CLOSEALL_SHIFT);
                NativeMethods.UnregisterHotKey(_hwnd, HOTKEY_ID_DOCK_SHIFT);
            }
            _source?.RemoveHook(HwndHook);
            _source?.Dispose();
            _source = null;
            _hwnd = IntPtr.Zero;
        }
    }
}
