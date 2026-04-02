using System;
using System.Windows;

namespace UTF.UI
{
    /// <summary>
    /// 设备扫描进度窗口
    /// </summary>
    public partial class DeviceScanProgressWindow : Window
    {
        public bool IsCancelled { get; private set; } = false;

        public DeviceScanProgressWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 更新进度
        /// </summary>
        /// <param name="status">状态文本</param>
        /// <param name="progress">进度值 (0-100)</param>
        public void UpdateProgress(string status, int progress)
        {
            Dispatcher.Invoke(() =>
            {
                StatusTextBlock.Text = status;
                ProgressBar.Value = Math.Max(0, Math.Min(100, progress));
                ProgressTextBlock.Text = $"{progress}%";
            });
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            IsCancelled = true;
            Close();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!IsCancelled)
            {
                var result = MessageBox.Show(UTF.UI.Localization.LocalizationHelper.GetString("Scan.CancelConfirm"), 
                                           UTF.UI.Localization.LocalizationHelper.GetString("Scan.ConfirmCancel"), 
                                           MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }
                IsCancelled = true;
            }
            
            base.OnClosing(e);
        }
    }
}
