using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using GestureDock.Models;
using GestureDock.Services;

namespace GestureDock.UI.Overlay
{
    public partial class CloseConfirmWindow : Window
    {
        private readonly List<WindowAppItem> _items;
        private readonly AppSettings _settings;
        private CancellationTokenSource? _cts;

        public CloseConfirmWindow(List<WindowAppItem> items, AppSettings settings)
        {
            InitializeComponent();
            _items = items;
            _settings = settings;
            AppsItemsControl.ItemsSource = _items;
        }

        private void ChkSelectAll_Checked(object sender, RoutedEventArgs e)
        {
            if (_items == null || AppsItemsControl == null) return;
            foreach (var item in _items) item.IsSelected = true;
            AppsItemsControl.Items.Refresh();
        }

        private void ChkSelectAll_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_items == null || AppsItemsControl == null) return;
            foreach (var item in _items) item.IsSelected = false;
            AppsItemsControl.Items.Refresh();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
            Close();
        }

        private async void BtnCloseAll_Click(object sender, RoutedEventArgs e)
        {
            BtnCloseAll.IsEnabled = false;
            BtnCancel.Content = "Done";

            _cts = new CancellationTokenSource();

            await WindowManager.CloseWindowsAsync(
                _items,
                _settings.AllowForceCloseAfterTimeout,
                _settings.ForceCloseTimeoutSeconds,
                item => Dispatcher.Invoke(() => AppsItemsControl.Items.Refresh()),
                _cts.Token);

            BtnCloseAll.Content = "Finished";
            await Task.Delay(1200);
            Close();
        }

        private void Window_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
                DragMove();
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                _cts?.Cancel();
                Close();
            }
        }
    }
}
