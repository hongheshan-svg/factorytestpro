using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using UTF.Core;

namespace UTF.UI;

/// <summary>
/// DUT详细信息窗口
/// </summary>
public partial class DUTDetailWindow : Window
{
    private readonly DUTTestInfo _dutInfo;
    private readonly DispatcherTimer _refreshTimer;
    private bool _autoScroll = true;
    private List<DUTLogEntry> _filteredLogs = new();

    public DUTDetailWindow(DUTTestInfo dutInfo)
    {
        InitializeComponent();
        _dutInfo = dutInfo;
        
        // 初始化刷新定时器
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _refreshTimer.Tick += RefreshTimer_Tick;
        _refreshTimer.Start();
        
        // 初始化界面
        InitializeUI();
        LoadDUTInfo();
        LoadTestSteps();
        LoadLogs();
        
        // 监听DUT状态变化
        _dutInfo.PropertyChanged += DutInfo_PropertyChanged;
    }
    
    private void InitializeUI()
    {
        // 设置窗口标题
        Title = $"DUT测试详情 - {_dutInfo.DutId}";
        
        // 初始化步骤过滤器
        StepFilter.Items.Clear();
        StepFilter.Items.Add(new ComboBoxItem { Content = "全部步骤", IsSelected = true });
        
        foreach (var step in _dutInfo.TestSteps)
        {
            StepFilter.Items.Add(new ComboBoxItem { Content = $"步骤{step.StepIndex + 1}: {step.StepName}" });
        }
    }
    
    private void LoadDUTInfo()
    {
        // 加载DUT基本信息
        DutTitleText.Text = $"DUT测试详情 - {_dutInfo.DutName}";
        DutIdText.Text = _dutInfo.DutId;
        DutNameText.Text = _dutInfo.DutName;
        StartTimeText.Text = _dutInfo.StartTimeDisplay;
        DurationText.Text = _dutInfo.DurationDisplay;
        ProgressText.Text = _dutInfo.Progress;
        
        // 设置状态显示
        UpdateStatusDisplay();
    }
    
    private void UpdateStatusDisplay()
    {
        StatusText.Text = _dutInfo.Status.ToString();
        
        // 根据状态设置颜色
        StatusBorder.Background = _dutInfo.Status switch
        {
            DUTTestStatus.Idle => new SolidColorBrush(Color.FromRgb(149, 165, 166)),      // 灰色
            DUTTestStatus.Ready => new SolidColorBrush(Color.FromRgb(52, 152, 219)),      // 蓝色
            DUTTestStatus.Running => new SolidColorBrush(Color.FromRgb(243, 156, 18)),    // 橙色
            DUTTestStatus.Paused => new SolidColorBrush(Color.FromRgb(241, 196, 15)),     // 黄色
            DUTTestStatus.Completed => new SolidColorBrush(Color.FromRgb(39, 174, 96)),   // 绿色
            DUTTestStatus.Failed => new SolidColorBrush(Color.FromRgb(231, 76, 60)),      // 红色
            DUTTestStatus.Error => new SolidColorBrush(Color.FromRgb(192, 57, 43)),       // 深红色
            DUTTestStatus.Disconnected => new SolidColorBrush(Color.FromRgb(127, 140, 141)), // 深灰色
            _ => new SolidColorBrush(Color.FromRgb(149, 165, 166))
        };
    }
    
    private void LoadTestSteps()
    {
        TestStepsDataGrid.ItemsSource = _dutInfo.TestSteps;
        UpdateStepStatistics();
    }
    
    private void UpdateStepStatistics()
    {
        var steps = _dutInfo.TestSteps;
        
        TotalStepsText.Text = steps.Count.ToString();
        PendingStepsText.Text = steps.Count(s => s.Status == TestStepStatus.Pending).ToString();
        RunningStepsText.Text = steps.Count(s => s.Status == TestStepStatus.Running).ToString();
        PassedStepsText.Text = steps.Count(s => s.Result == TestStepResult.Pass).ToString();
        FailedStepsText.Text = steps.Count(s => s.Result == TestStepResult.Fail).ToString();
    }
    
    private void LoadLogs()
    {
        ApplyLogFilter();
    }
    
    private void ApplyLogFilter()
    {
        var logs = _dutInfo.Logs;
        
        // 应用日志级别过滤
        var selectedLevel = (LogLevelFilter.SelectedItem as ComboBoxItem)?.Content.ToString();
        if (!string.IsNullOrEmpty(selectedLevel) && selectedLevel != "全部")
        {
            var filterLevel = selectedLevel switch
            {
                "调试" => UTF.Logging.LogLevel.Debug,
                "信息" => UTF.Logging.LogLevel.Info,
                "警告" => UTF.Logging.LogLevel.Warning,
                "错误" => UTF.Logging.LogLevel.Error,
                "严重" => UTF.Logging.LogLevel.Critical,
                _ => UTF.Logging.LogLevel.Debug
            };
            logs = logs.Where(l => l.Level >= filterLevel).ToList();
        }
        
        // 应用步骤过滤
        var selectedStep = (StepFilter.SelectedItem as ComboBoxItem)?.Content.ToString();
        if (!string.IsNullOrEmpty(selectedStep) && selectedStep != "全部步骤")
        {
            // 提取步骤索引
            if (selectedStep.StartsWith("步骤") && int.TryParse(selectedStep.Substring(2, 1), out int stepIndex))
            {
                logs = logs.Where(l => l.StepIndex == stepIndex - 1).ToList();
            }
        }
        
        _filteredLogs = logs;
        LogDataGrid.ItemsSource = _filteredLogs;
        
        // 自动滚动到底部
        if (_autoScroll && LogDataGrid.Items.Count > 0)
        {
            LogDataGrid.ScrollIntoView(LogDataGrid.Items[LogDataGrid.Items.Count - 1]);
        }
    }
    
    private void DutInfo_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // 在UI线程上更新界面
        Dispatcher.Invoke(() =>
        {
            LoadDUTInfo();
            
            if (e.PropertyName == nameof(DUTTestInfo.TestSteps))
            {
                LoadTestSteps();
            }
            
            if (e.PropertyName == nameof(DUTTestInfo.Logs))
            {
                LoadLogs();
            }
        });
    }
    
    private void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        // 更新持续时间
        if (_dutInfo.StartTime != DateTime.MinValue)
        {
            _dutInfo.Duration = DateTime.Now - _dutInfo.StartTime;
            DurationText.Text = _dutInfo.DurationDisplay;
        }
        
        // 更新最后更新时间
        LastUpdateText.Text = DateTime.Now.ToString("HH:mm:ss");
        
        // 更新实时监控数据（模拟）
        UpdateRealtimeMonitoring();
    }
    
    private void UpdateRealtimeMonitoring()
    {
        // 模拟实时监控数据
        var random = new Random();
        CpuUsageText.Text = $"{random.Next(10, 80)}%";
        MemoryUsageText.Text = $"{random.Next(100, 512)}MB";
        
        // 根据DUT状态更新通信状态
        CommStatusText.Text = _dutInfo.Status == DUTTestStatus.Disconnected ? "断开" : "正常";
        CommStatusText.Foreground = _dutInfo.Status == DUTTestStatus.Disconnected 
            ? new SolidColorBrush(Color.FromRgb(220, 53, 69)) 
            : new SolidColorBrush(Color.FromRgb(40, 167, 69));
        
        // 计算错误日志数量
        var errorCount = _dutInfo.Logs.Count(l => l.Level >= UTF.Logging.LogLevel.Error);
        ErrorCountText.Text = errorCount.ToString();
    }
    
    // 事件处理方法
    private void RefreshBtn_Click(object sender, RoutedEventArgs e)
    {
        LoadDUTInfo();
        LoadTestSteps();
        LoadLogs();
        StatusBarText.Text = "数据已刷新";
    }
    
    private void ExportLogBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var fileName = $"DUT_{_dutInfo.DutId}_Log_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), fileName);
            
            var logContent = new StringBuilder();
            logContent.AppendLine($"DUT测试日志导出");
            logContent.AppendLine($"DUT ID: {_dutInfo.DutId}");
            logContent.AppendLine($"DUT名称: {_dutInfo.DutName}");
            logContent.AppendLine($"导出时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            logContent.AppendLine(new string('=', 80));
            logContent.AppendLine();
            
            foreach (var log in _dutInfo.Logs)
            {
                logContent.AppendLine($"[{log.TimeDisplay}] [{log.LevelDisplay}] [{log.StepName}] {log.FullMessage}");
                if (!string.IsNullOrEmpty(log.Details))
                {
                    logContent.AppendLine($"    详情: {log.Details}");
                }
            }
            
            File.WriteAllText(filePath, logContent.ToString(), Encoding.UTF8);
            
            MessageBox.Show($"日志已导出到: {filePath}", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
            StatusBarText.Text = $"日志已导出: {fileName}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导出日志失败: {ex.Message}", "导出失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
    
    private void LogLevelFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyLogFilter();
    }
    
    private void StepFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyLogFilter();
    }
    
    private void ClearLogBtn_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show("确定要清除所有日志吗？此操作不可撤销。", "确认清除", 
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        
        if (result == MessageBoxResult.Yes)
        {
            _dutInfo.Logs.Clear();
            LoadLogs();
            StatusBarText.Text = "日志已清除";
        }
    }
    
    private void AutoScrollBtn_Click(object sender, RoutedEventArgs e)
    {
        _autoScroll = !_autoScroll;
        AutoScrollBtn.Content = _autoScroll ? "📜 自动滚动" : "📜 手动滚动";
        AutoScrollBtn.Background = _autoScroll 
            ? new SolidColorBrush(Color.FromRgb(40, 167, 69))   // 绿色
            : new SolidColorBrush(Color.FromRgb(108, 117, 125)); // 灰色
        
        StatusBarText.Text = _autoScroll ? "已启用自动滚动" : "已禁用自动滚动";
    }
    
    private void TestStepsDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (TestStepsDataGrid.SelectedItem is TestStepInfo selectedStep)
        {
            ShowStepDetails(selectedStep);
        }
    }
    
    private void ShowStepDetails(TestStepInfo step)
    {
        var details = $"步骤详情\n\n" +
                     $"步骤索引: {step.StepIndex + 1}\n" +
                     $"步骤名称: {step.StepName}\n" +
                     $"描述: {step.Description}\n" +
                     $"状态: {step.StatusDisplay}\n" +
                     $"结果: {step.ResultDisplay}\n" +
                     $"开始时间: {(step.StartTime == DateTime.MinValue ? "-" : step.StartTime.ToString("HH:mm:ss.fff"))}\n" +
                     $"结束时间: {(step.EndTime == DateTime.MinValue ? "-" : step.EndTime.ToString("HH:mm:ss.fff"))}\n" +
                     $"执行时长: {step.DurationDisplay}\n" +
                     $"结果信息: {step.ResultMessage ?? "-"}\n\n";
        
        if (step.Parameters.Count > 0)
        {
            details += "参数:\n";
            foreach (var param in step.Parameters)
            {
                details += $"  {param.Key}: {param.Value}\n";
            }
            details += "\n";
        }
        
        if (step.Results.Count > 0)
        {
            details += "结果数据:\n";
            foreach (var result in step.Results)
            {
                details += $"  {result.Key}: {result.Value}\n";
            }
        }
        
        MessageBox.Show(details, $"步骤详情 - {step.StepName}", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    
    protected override void OnClosed(EventArgs e)
    {
        _refreshTimer?.Stop();
        _dutInfo.PropertyChanged -= DutInfo_PropertyChanged;
        base.OnClosed(e);
    }
}
