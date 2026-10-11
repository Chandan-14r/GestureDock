namespace GestureDock.Models
{
    public enum DockPosition
    {
        BottomRight,
        BottomCenter,
        BottomLeft
    }

    public enum AppTheme
    {
        System,
        Light,
        Dark
    }

    public class AppSettings
    {
        public bool StartWithWindows { get; set; } = false;
        public bool EnableOverlay { get; set; } = true;
        public bool EnableAnimations { get; set; } = true;
        public DockPosition Position { get; set; } = DockPosition.BottomRight;
        public bool ConfirmBeforeCloseAll { get; set; } = true;
        public bool GracefulCloseOnly { get; set; } = true;
        public bool AllowForceCloseAfterTimeout { get; set; } = false;
        public int ForceCloseTimeoutSeconds { get; set; } = 4;
        public AppTheme Theme { get; set; } = AppTheme.System;
        public int MarginBottom { get; set; } = 36;
        public int MarginSide { get; set; } = 40;
    }

    public class WindowAppItem
    {
        public IntPtr Hwnd { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ProcessName { get; set; } = string.Empty;
        public uint ProcessId { get; set; }
        public bool IsSelected { get; set; } = true;
        public string Status { get; set; } = "Ready";
        public bool Succeeded { get; set; }
    }
}
