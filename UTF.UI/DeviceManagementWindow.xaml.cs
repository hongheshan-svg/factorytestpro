using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using UTF.UI.Services;

namespace UTF.UI;

/// <summary>
/// 设备管理窗口
/// </summary>
public partial class DeviceManagementWindow : Window
{
    private readonly IPermissionManager _permissionManager;
    private List<InstrumentDeviceInfo> _instruments = new();
    private List<DutDeviceInfo> _duts = new();
    
    public DeviceManagementWindow(IPermissionManager permissionManager)
    {
        InitializeComponent();
        _permissionManager = permissionManager;
        
        InitializeWindow();
        LoadDevices();
    }
    
    private void InitializeWindow()
    {
        // 根据权限控制功能可用性
        CheckPermissions();
        
        // 初始化状态
        StatusTextBlock.Text = "设备管理系统已就绪";
    }
    
    private void CheckPermissions()
    {
        var hasDeviceManagement = _permissionManager.HasPermission(Permission.DeviceManagement);
        var hasDeviceConfig = _permissionManager.HasPermission(Permission.DeviceConfig);
        
        // 根据权限禁用相关功能
        AddDeviceBtn.IsEnabled = hasDeviceManagement;
        ImportConfigBtn.IsEnabled = hasDeviceConfig;
        ExportConfigBtn.IsEnabled = hasDeviceConfig;
        
        if (!hasDeviceManagement)
        {
            StatusTextBlock.Text = "当前用户没有设备管理权限，部分功能受限";
        }
    }
    
    private void LoadDevices()
    {
        LoadInstruments();
        LoadDuts();
        UpdateDeviceCount();
    }
    
    private void LoadInstruments()
    {
        _instruments = new List<InstrumentDeviceInfo>();
        InstrumentDataGrid.ItemsSource = _instruments;
    }

    private void LoadDuts()
    {
        _duts = new List<DutDeviceInfo>();
        DutDataGrid.ItemsSource = _duts;
    }
    
    private void UpdateDeviceCount()
    {
        var totalDevices = _instruments.Count + _duts.Count;
        DeviceCountText.Text = $"设备总数: {totalDevices} (仪器:{_instruments.Count}, DUT:{_duts.Count})";
    }
    
    // 工具栏事件处理
    private void RefreshBtn_Click(object sender, RoutedEventArgs e)
    {
        StatusTextBlock.Text = "正在刷新设备列表...";
        LoadDevices();
        StatusTextBlock.Text = "设备列表刷新完成";
    }
    
    private void AddDeviceBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!_permissionManager.HasPermission(Permission.DeviceManagement))
        {
            MessageBox.Show("您没有设备管理权限", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        MessageBox.Show("设备管理功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ScanDevicesBtn_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("设备扫描功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ImportConfigBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!_permissionManager.HasPermission(Permission.DeviceConfig))
        {
            MessageBox.Show("您没有设备配置权限", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        MessageBox.Show("导入配置功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ExportConfigBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!_permissionManager.HasPermission(Permission.DeviceConfig))
        {
            MessageBox.Show("您没有设备配置权限", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        MessageBox.Show("导出配置功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    
    // 筛选事件处理
    private void ApplyInstrumentFilter_Click(object sender, RoutedEventArgs e)
    {
        var typeFilter = (InstrumentTypeCombo.SelectedItem as ComboBoxItem)?.Content.ToString();
        var statusFilter = (InstrumentStatusCombo.SelectedItem as ComboBoxItem)?.Content.ToString();
        
        var filteredInstruments = _instruments.AsEnumerable();
        
        if (typeFilter != "全部类型")
            filteredInstruments = filteredInstruments.Where(i => i.DeviceType == typeFilter);
        
        if (statusFilter != "全部状态")
            filteredInstruments = filteredInstruments.Where(i => i.Status == statusFilter);
        
        InstrumentDataGrid.ItemsSource = filteredInstruments.ToList();
        StatusTextBlock.Text = $"仪器筛选完成，找到 {filteredInstruments.Count()} 个设备";
    }
    
    private void ApplyDutFilter_Click(object sender, RoutedEventArgs e)
    {
        var typeFilter = (DutTypeCombo.SelectedItem as ComboBoxItem)?.Content.ToString();
        var statusFilter = (DutStatusCombo.SelectedItem as ComboBoxItem)?.Content.ToString();
        
        var filteredDuts = _duts.AsEnumerable();
        
        if (typeFilter != "全部类型")
            filteredDuts = filteredDuts.Where(d => d.DutType == typeFilter);
        
        if (statusFilter != "全部状态")
            filteredDuts = filteredDuts.Where(d => d.Status == statusFilter);
        
        DutDataGrid.ItemsSource = filteredDuts.ToList();
        StatusTextBlock.Text = $"DUT筛选完成，找到 {filteredDuts.Count()} 个设备";
    }
    
    // 仪器设备操作
    private void ConnectDevice_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("设备连接功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ConfigDevice_Click(object sender, RoutedEventArgs e)
    {
        if (!_permissionManager.HasPermission(Permission.DeviceConfig))
        {
            MessageBox.Show("您没有设备配置权限", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        MessageBox.Show("设备配置功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void CalibrateDevice_Click(object sender, RoutedEventArgs e)
    {
        if (!_permissionManager.HasPermission(Permission.DeviceCalibration))
        {
            MessageBox.Show("您没有设备校准权限", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        MessageBox.Show("设备校准功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void DeleteDevice_Click(object sender, RoutedEventArgs e)
    {
        if (!_permissionManager.HasPermission(Permission.DeviceManagement))
        {
            MessageBox.Show("您没有设备管理权限", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        MessageBox.Show("删除设备功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // DUT设备操作
    private void ConnectDut_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("DUT连接功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ConfigDut_Click(object sender, RoutedEventArgs e)
    {
        if (!_permissionManager.HasPermission(Permission.DutConfig))
        {
            MessageBox.Show("您没有DUT配置权限", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        MessageBox.Show("DUT配置功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void TestDut_Click(object sender, RoutedEventArgs e)
    {
        if (!_permissionManager.HasPermission(Permission.TestExecution))
        {
            MessageBox.Show("您没有测试执行权限", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        MessageBox.Show("DUT测试功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void DeleteDut_Click(object sender, RoutedEventArgs e)
    {
        if (!_permissionManager.HasPermission(Permission.DutManagement))
        {
            MessageBox.Show("您没有DUT管理权限", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        MessageBox.Show("删除DUT功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    
    // 配置保存
    private void SaveConfig_Click(object sender, RoutedEventArgs e)
    {
        if (!_permissionManager.HasPermission(Permission.SystemConfig))
        {
            MessageBox.Show("您没有系统配置权限", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        MessageBox.Show("配置保存功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ResetConfig_Click(object sender, RoutedEventArgs e)
    {
        if (!_permissionManager.HasPermission(Permission.SystemConfig))
        {
            MessageBox.Show("您没有系统配置权限", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = MessageBox.Show("确定要重置为默认配置吗？", "确认重置", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            ConnectionTimeoutTextBox.Text = "30";
            RetryCountTextBox.Text = "3";
            AutoReconnectCheckBox.IsChecked = true;
            LogLevelCombo.SelectedIndex = 1;
            EnableDeviceLogCheckBox.IsChecked = true;

            StatusTextBlock.Text = "已重置为默认配置";
        }
    }
    
    private void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

/// <summary>
/// 仪器设备信息
/// </summary>
public class InstrumentDeviceInfo
{
    public string DeviceId { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public string DeviceType { get; set; } = "";
    public string Manufacturer { get; set; } = "";
    public string Model { get; set; } = "";
    public string ConnectionType { get; set; } = "";
    public string ConnectionAddress { get; set; } = "";
    public string Status { get; set; } = "";
}

/// <summary>
/// DUT设备信息
/// </summary>
public class DutDeviceInfo
{
    public string DutId { get; set; } = "";
    public string DutName { get; set; } = "";
    public string DutType { get; set; } = "";
    public string Manufacturer { get; set; } = "";
    public string Model { get; set; } = "";
    public string CommunicationType { get; set; } = "";
    public string ConnectionParams { get; set; } = "";
    public string Status { get; set; } = "";
}
