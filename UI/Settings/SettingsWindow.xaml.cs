using System.Windows;
using GestureDock.Models;
using GestureDock.Services;

namespace GestureDock.UI.Settings
{
    public partial class SettingsWindow : Window
    {
        private readonly SettingsManager _settingsManager;

        public SettingsWindow(SettingsManager settingsManager)
        {
            InitializeComponent();
            _settingsManager = settingsManager;
            LoadCurrentSettings();
        }

        private void LoadCurrentSettings()
        {
            var s = _settingsManager.Current;
            ChkStartWithWindows.IsChecked = StartupManager.IsStartupEnabled();
            ChkEnableOverlay.IsChecked = s.EnableOverlay;
            ChkEnableAnimations.IsChecked = s.EnableAnimations;

            switch (s.Position)
            {
                case DockPosition.BottomLeft:
                    RbBottomLeft.IsChecked = true;
                    break;
                case DockPosition.BottomCenter:
                    RbBottomCenter.IsChecked = true;
                    break;
                case DockPosition.BottomRight:
                default:
                    RbBottomRight.IsChecked = true;
                    break;
            }

            ChkConfirmClose.IsChecked = s.ConfirmBeforeCloseAll;
            ChkGracefulOnly.IsChecked = s.GracefulCloseOnly;
            ChkForceClose.IsChecked = s.AllowForceCloseAfterTimeout;

            CmbTheme.SelectedIndex = (int)s.Theme;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var s = _settingsManager.Current;
            s.StartWithWindows = ChkStartWithWindows.IsChecked ?? false;
            s.EnableOverlay = ChkEnableOverlay.IsChecked ?? true;
            s.EnableAnimations = ChkEnableAnimations.IsChecked ?? true;

            if (RbBottomLeft.IsChecked == true) s.Position = DockPosition.BottomLeft;
            else if (RbBottomCenter.IsChecked == true) s.Position = DockPosition.BottomCenter;
            else s.Position = DockPosition.BottomRight;

            s.ConfirmBeforeCloseAll = ChkConfirmClose.IsChecked ?? true;
            s.GracefulCloseOnly = ChkGracefulOnly.IsChecked ?? true;
            s.AllowForceCloseAfterTimeout = ChkForceClose.IsChecked ?? false;

            s.Theme = (AppTheme)(CmbTheme.SelectedIndex >= 0 ? CmbTheme.SelectedIndex : 0);

            // Apply startup setting
            StartupManager.SetStartup(s.StartWithWindows);

            // Save JSON settings
            _settingsManager.Save();

            Close();
        }

        private void BtnCreateShortcuts_Click(object sender, RoutedEventArgs e)
        {
            StartupManager.CreateDesktopShortcuts();
            TxtShortcutsStatus.Text = "Shortcuts created on Desktop!";
        }

        private void BtnRemoveShortcuts_Click(object sender, RoutedEventArgs e)
        {
            StartupManager.RemoveDesktopShortcuts();
            TxtShortcutsStatus.Text = "Shortcuts removed from Desktop!";
        }
    }
}
