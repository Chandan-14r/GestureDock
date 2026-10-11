using System;
using System.Drawing;
using System.Windows.Forms;
using GestureDock.Services;

namespace GestureDock.UI.Tray
{
    public class TrayIconManager : IDisposable
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly Action _openSettings;
        private readonly Action _openDiagnostic;
        private readonly Action _testOverlay;
        private readonly Action _showDesktop;
        private readonly Action _closeAllApps;
        private readonly Action _exitApp;
        private readonly SettingsManager _settingsManager;

        public TrayIconManager(
            SettingsManager settingsManager,
            Action openSettings,
            Action openDiagnostic,
            Action testOverlay,
            Action showDesktop,
            Action closeAllApps,
            Action exitApp)
        {
            _settingsManager = settingsManager;
            _openSettings = openSettings;
            _openDiagnostic = openDiagnostic;
            _testOverlay = testOverlay;
            _showDesktop = showDesktop;
            _closeAllApps = closeAllApps;
            _exitApp = exitApp;

            _notifyIcon = new NotifyIcon
            {
                Text = "GestureDock - Task View Companion",
                Icon = CreateAppIcon(),
                Visible = true
            };

            BuildContextMenu();
            _notifyIcon.DoubleClick += (s, e) => _openSettings();
            _notifyIcon.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    _openSettings();
                }
            };
            try
            {
                _notifyIcon.ShowBalloonTip(2000, "GestureDock Active", "Swipe up with 3 fingers or press Win+Tab to see your dock overlay.", ToolTipIcon.Info);
            }
            catch { }
        }

        private void BuildContextMenu()
        {
            var menu = new ContextMenuStrip();

            var titleItem = new ToolStripMenuItem("GestureDock (Active)") { Enabled = false };
            menu.Items.Add(titleItem);
            menu.Items.Add(new ToolStripSeparator());

            var enableItem = new ToolStripMenuItem("Enable Overlay")
            {
                Checked = _settingsManager.Current.EnableOverlay,
                CheckOnClick = true
            };
            enableItem.CheckedChanged += (s, e) =>
            {
                _settingsManager.Current.EnableOverlay = enableItem.Checked;
                _settingsManager.Save();
            };
            menu.Items.Add(enableItem);

            var startupItem = new ToolStripMenuItem("Start with Windows")
            {
                Checked = StartupManager.IsStartupEnabled(),
                CheckOnClick = true
            };
            startupItem.CheckedChanged += (s, e) =>
            {
                StartupManager.SetStartup(startupItem.Checked);
                _settingsManager.Current.StartWithWindows = startupItem.Checked;
                _settingsManager.Save();
            };
            menu.Items.Add(startupItem);

            menu.Items.Add(new ToolStripSeparator());

            menu.Items.Add(new ToolStripMenuItem("Test Overlay", null, (s, e) => _testOverlay()));
            menu.Items.Add(new ToolStripMenuItem("Show Desktop", null, (s, e) => _showDesktop()));
            menu.Items.Add(new ToolStripMenuItem("Close All Apps", null, (s, e) => _closeAllApps()));

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Diagnostics / Monitor", null, (s, e) => _openDiagnostic()));
            menu.Items.Add(new ToolStripMenuItem("Settings...", null, (s, e) => _openSettings()));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Exit", null, (s, e) => _exitApp()));

            _notifyIcon.ContextMenuStrip = menu;
        }

        private static Icon CreateAppIcon()
        {
            try
            {
                using var bmp = new Bitmap(32, 32);
                using var g = Graphics.FromImage(bmp);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                using var brush = new SolidBrush(Color.FromArgb(240, 30, 30, 30));
                using var pen = new Pen(Color.FromArgb(220, 76, 194, 255), 2);
                g.FillEllipse(brush, 2, 2, 28, 28);
                g.DrawEllipse(pen, 2, 2, 28, 28);

                using var blueBrush = new SolidBrush(Color.FromArgb(255, 76, 194, 255));
                using var redBrush = new SolidBrush(Color.FromArgb(255, 255, 107, 107));
                g.FillEllipse(blueBrush, 8, 12, 6, 6);
                g.FillEllipse(redBrush, 18, 12, 6, 6);

                IntPtr hIcon = bmp.GetHicon();
                return Icon.FromHandle(hIcon);
            }
            catch
            {
                return SystemIcons.Application;
            }
        }

        public void Dispose()
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
    }
}
