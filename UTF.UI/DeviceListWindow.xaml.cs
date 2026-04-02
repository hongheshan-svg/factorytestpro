using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using UTF.Core;

namespace UTF.UI
{
    /// <summary>
    /// 设备列表窗口
    /// </summary>
    public partial class DeviceListWindow : Window
    {
        private List<DeviceInfo> _devices;

        public DeviceListWindow(List<DeviceInfo> devices)
        {
            InitializeComponent();
            _devices = devices;
            InitializeDeviceList();
            
            // 监听选择变化
            DevicesDataGrid.SelectionChanged += DevicesDataGrid_SelectionChanged;
        }

        private void InitializeDeviceList()
        {
            DevicesDataGrid.ItemsSource = _devices;
            DeviceCountTextBlock.Text = UTF.UI.Localization.LocalizationHelper.GetStringFormatted("Device.DevicesFound", _devices.Count);
            
            // 默认选择第一个设备
            if (_devices.Count > 0)
            {
                DevicesDataGrid.SelectedIndex = 0;
            }
        }

        private void DevicesDataGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            ConnectButton.IsEnabled = DevicesDataGrid.SelectedItem != null;
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (DevicesDataGrid.SelectedItem is DeviceInfo selectedDevice)
            {
                try
                {
                    ConnectToDevice(selectedDevice);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(UTF.UI.Localization.LocalizationHelper.GetString("Error.ConnectDevice") + ": " + ex.Message, 
                                  UTF.UI.Localization.LocalizationHelper.GetString("Device.ConnectFailed"), 
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ConnectToDevice(DeviceInfo device)
        {
            // 模拟设备连接过程
            var result = MessageBox.Show(UTF.UI.Localization.LocalizationHelper.GetStringFormatted("Device.ConfirmConnect", device.Name) + "\n" +
                                       $"{UTF.UI.Localization.LocalizationHelper.GetString("Device.Type", "类型")}: {device.Type}\n" +
                                       $"{UTF.UI.Localization.LocalizationHelper.GetString("Device.Port", "端口")}: {device.Port}", 
                                       UTF.UI.Localization.LocalizationHelper.GetString("Device.ConfirmConnect", "确认连接"), 
                                       MessageBoxButton.YesNo, 
                                       MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // 模拟连接延迟
                var progressWindow = new DeviceScanProgressWindow();
                progressWindow.Title = UTF.UI.Localization.LocalizationHelper.GetString("Device.Connecting");
                progressWindow.Owner = this;
                progressWindow.Show();

                // 模拟连接过程
                var timer = new System.Windows.Threading.DispatcherTimer();
                var progress = 0;
                timer.Interval = TimeSpan.FromMilliseconds(200);
                timer.Tick += (s, e) =>
                {
                    progress += 10;
                    progressWindow.UpdateProgress(UTF.UI.Localization.LocalizationHelper.GetStringFormatted("Device.Connecting") + " " + device.Name + "...", progress);
                    
                    if (progress >= 100)
                    {
                        timer.Stop();
                        progressWindow.Close();
                        
                        device.IsConnected = true;
                        device.Status = UTF.UI.Localization.LocalizationHelper.GetString("Device.Connected");
                        
                        MessageBox.Show(UTF.UI.Localization.LocalizationHelper.GetStringFormatted("Device.ConnectSuccess") + " '" + device.Name + "'", 
                                      UTF.UI.Localization.LocalizationHelper.GetString("Device.ConnectSuccess"), 
                                      MessageBoxButton.OK, MessageBoxImage.Information);
                        
                        // 刷新显示
                        DevicesDataGrid.Items.Refresh();
                    }
                };
                timer.Start();
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                RefreshButton.IsEnabled = false;
                RefreshButton.Content = "🔄 " + UTF.UI.Localization.LocalizationHelper.GetString("Device.Scanning");

                // 重新扫描设备
                var progressWindow = new DeviceScanProgressWindow();
                progressWindow.Owner = this;
                progressWindow.Show();

                // 模拟扫描过程
                await System.Threading.Tasks.Task.Delay(2000);
                progressWindow.UpdateProgress(UTF.UI.Localization.LocalizationHelper.GetString("Scan.Complete"), 100);
                await System.Threading.Tasks.Task.Delay(500);
                progressWindow.Close();

                // 模拟发现新设备
                var newDevice = new DeviceInfo
                {
                    Name = $"{UTF.UI.Localization.LocalizationHelper.GetString("Device.NewDevice", "新设备")}-{DateTime.Now:HHmmss}",
                    Type = UTF.UI.Localization.LocalizationHelper.GetString("Device.AutoDiscovered", "自动发现"),
                    Port = "AUTO",
                    Status = UTF.UI.Localization.LocalizationHelper.GetString("Device.Online"),
                    Description = UTF.UI.Localization.LocalizationHelper.GetString("Device.RescanDiscovered", "重新扫描发现的设备")
                };

                _devices.Add(newDevice);
                InitializeDeviceList();

                MessageBox.Show(UTF.UI.Localization.LocalizationHelper.GetStringFormatted("Device.RescanComplete", _devices.Count), 
                              UTF.UI.Localization.LocalizationHelper.GetString("Device.ScanComplete"), 
                              MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(UTF.UI.Localization.LocalizationHelper.GetString("Error.RescanDevices") + ": " + ex.Message, 
                              UTF.UI.Localization.LocalizationHelper.GetString("Device.ScanFailed"), 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                RefreshButton.IsEnabled = true;
                RefreshButton.Content = "🔄 " + UTF.UI.Localization.LocalizationHelper.GetString("Device.Rescan", "重新扫描");
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
