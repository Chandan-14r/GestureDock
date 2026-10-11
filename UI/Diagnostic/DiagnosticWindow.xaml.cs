using System;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using GestureDock.Interop;
using GestureDock.Services;

namespace GestureDock.UI.Diagnostic
{
    public partial class DiagnosticWindow : Window
    {
        private readonly TaskViewDetector _detector;
        private readonly OverlayManager _overlayManager;
        private readonly DispatcherTimer _refreshTimer;

        public DiagnosticWindow(TaskViewDetector detector, OverlayManager overlayManager)
        {
            InitializeComponent();
            _detector = detector;
            _overlayManager = overlayManager;

            _detector.DiagnosticLog += (s, msg) => Dispatcher.Invoke(() => AppendLog(msg));

            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _refreshTimer.Tick += (s, e) => RefreshStatus();
            _refreshTimer.Start();
        }

        protected override void OnClosed(EventArgs e)
        {
            _refreshTimer.Stop();
            base.OnClosed(e);
        }

        private void RefreshStatus()
        {
            TxtTvActive.Text = _detector.IsTaskViewActive.ToString();
            TxtTvActive.Foreground = _detector.IsTaskViewActive
                ? System.Windows.Media.Brushes.LightGreen
                : System.Windows.Media.Brushes.Tomato;

            TxtOverlayVisible.Text = _overlayManager.IsVisible.ToString();

            IntPtr fg = NativeMethods.GetForegroundWindow();
            if (fg != IntPtr.Zero)
            {
                var sbTitle = new StringBuilder(256);
                var sbClass = new StringBuilder(256);
                NativeMethods.GetWindowText(fg, sbTitle, 256);
                NativeMethods.GetClassName(fg, sbClass, 256);

                TxtFgWindow.Text = $"{sbTitle} (0x{fg.ToInt64():X})";
                TxtFgClass.Text = sbClass.ToString();
            }
            else
            {
                TxtFgWindow.Text = "None";
                TxtFgClass.Text = "None";
            }
        }

        private void AppendLog(string text)
        {
            TxtLogs.AppendText(text + Environment.NewLine);
            TxtLogs.ScrollToEnd();
        }

        private void BtnTestShow_Click(object sender, RoutedEventArgs e)
        {
            _overlayManager.ShowManually();
        }

        private void BtnTestHide_Click(object sender, RoutedEventArgs e)
        {
            _overlayManager.HideManually();
        }

        private void BtnClearLog_Click(object sender, RoutedEventArgs e)
        {
            TxtLogs.Clear();
        }
    }
}
