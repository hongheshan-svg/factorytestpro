using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using UTF.Core;
using UTF.UI.ViewModels;

namespace UTF.UI;

/// <summary>
/// DUT测试列表监控窗口
/// </summary>
public partial class DUTTestListWindow : Window
{
    private readonly DUTTestListViewModel _viewModel;
    private readonly DispatcherTimer _refreshTimer;

    public DUTTestListWindow()
    {
        InitializeComponent();
        
        _viewModel = new DUTTestListViewModel();
        DataContext = _viewModel;
        
        // 初始化刷新定时器
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2) // 每2秒刷新一次
        };
        _refreshTimer.Tick += RefreshTimer_Tick;
        _refreshTimer.Start();
        
        // 加载模拟数据
        LoadSimulatedData();
        
        Loaded += DUTTestListWindow_Loaded;
    }

    private void DUTTestListWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // 窗口加载完成后的初始化
        Title = $"多DUT测试监控台 - {_viewModel.TotalDUTs}个设备";
    }

    /// <summary>
    /// 加载模拟的DUT测试数据
    /// </summary>
    private void LoadSimulatedData()
    {
        _viewModel.ClearDUTs();
        
        var random = new Random();
        
        // 模拟设备列表
        var deviceTypes = new[]
        {
            "iPhone 15 Pro", "Galaxy S24 Ultra", "MacBook Pro M3", "ECU Engine Control",
            "iPhone 15", "Radar Sensor", "Xiaomi 14 Pro", "Dell XPS 13",
            "Huawei Mate 60", "ThinkPad X1", "iPad Pro", "Surface Pro",
            "OnePlus 12", "ASUS ROG Phone", "Tesla Model Y ECU", "BMW i4 HUD"
        };
        
        for (int i = 0; i < deviceTypes.Length; i++)
        {
            var dut = new DUTTestInfo
            {
                DutId = $"DUT_{i + 1:D2}",
                DutName = deviceTypes[i],
                TestSteps = DUTTestManager.CreateStandardTestSteps(),
                StartTime = DateTime.Now.AddMinutes(-random.Next(1, 180))
            };
            
            // 模拟不同的测试状态
            switch (i % 8)
            {
                case 0: // 正在执行第1步
                    SimulateDUTState(dut, DUTTestStatus.Running, DUTTestResult.Running, 0);
                    break;
                    
                case 1: // 正在执行第2步
                    SimulateDUTState(dut, DUTTestStatus.Running, DUTTestResult.Running, 1);
                    break;
                    
                case 2: // 正在执行第3步
                    SimulateDUTState(dut, DUTTestStatus.Running, DUTTestResult.Running, 2);
                    break;
                    
                case 3: // 测试完成 - PASS
                    SimulateDUTState(dut, DUTTestStatus.Completed, DUTTestResult.Pass, 6, true);
                    break;
                    
                case 4: // 测试失败 - 在第2步失败
                    SimulateDUTState(dut, DUTTestStatus.Failed, DUTTestResult.Fail, 1, false, true);
                    break;
                    
                case 5: // 测试失败 - 在第3步失败
                    SimulateDUTState(dut, DUTTestStatus.Failed, DUTTestResult.Fail, 2, false, true);
                    break;
                    
                case 6: // 等待测试
                    SimulateDUTState(dut, DUTTestStatus.Ready, DUTTestResult.Pending, 0);
                    break;
                    
                case 7: // 暂停状态
                    SimulateDUTState(dut, DUTTestStatus.Paused, DUTTestResult.Running, random.Next(1, 3));
                    break;
            }
            
            _viewModel.AddDUT(dut);
        }
        
        // 默认选中第一个DUT
        if (_viewModel.DUTs.Count > 0)
        {
            _viewModel.SelectedDUT = _viewModel.DUTs[0];
        }
    }

    /// <summary>
    /// 模拟DUT状态
    /// </summary>
    private void SimulateDUTState(DUTTestInfo dut, DUTTestStatus status, DUTTestResult result, 
        int currentStep, bool allComplete = false, bool hasFailed = false)
    {
        dut.Status = status;
        dut.Result = result;
        dut.CurrentStepIndex = currentStep;
        
        var random = new Random();
        
        // 添加初始化日志
        dut.AddLog(UTF.Logging.LogLevel.Info, $"DUT {dut.DutId} 测试开始", $"设备类型: {dut.DutName}");
        
        // 模拟已完成的步骤
        for (int i = 0; i < currentStep && i < dut.TestSteps.Count; i++)
        {
            var step = dut.TestSteps[i];
            step.Status = TestStepStatus.Completed;
            step.StartTime = dut.StartTime.AddMinutes(i * 2);
            step.EndTime = step.StartTime.AddSeconds(random.Next(30, 120));
            step.Duration = step.EndTime - step.StartTime;
            
            if (hasFailed && i == currentStep - 1)
            {
                // 最后一步失败
                step.Result = TestStepResult.Fail;
                step.ResultMessage = "测试参数超出预期范围";
                dut.AddLog(UTF.Logging.LogLevel.Error, $"步骤{i + 1}执行失败", step.ResultMessage);
                dut.AddLog(UTF.Logging.LogLevel.Critical, "测试中止", "由于关键步骤失败，终止后续测试");
            }
            else
            {
                step.Result = TestStepResult.Pass;
                step.ResultMessage = "测试通过";
                dut.AddLog(UTF.Logging.LogLevel.Info, $"步骤{i + 1}执行成功", $"耗时: {step.DurationDisplay}");
                
                // 添加一些模拟的测试数据
                step.Results["测试值"] = random.NextDouble() * 100;
                step.Results["标准值"] = 85.0;
            }
        }
        
        // 模拟当前正在执行的步骤
        if (!allComplete && currentStep < dut.TestSteps.Count && !hasFailed)
        {
            var currentStepInfo = dut.TestSteps[currentStep];
            currentStepInfo.Status = TestStepStatus.Running;
            currentStepInfo.StartTime = DateTime.Now.AddMinutes(-random.Next(1, 5));
            
            dut.AddLog(UTF.Logging.LogLevel.Info, $"开始执行步骤{currentStep + 1}: {currentStepInfo.StepName}");
            dut.AddLog(UTF.Logging.LogLevel.Debug, "设备通信检查", "串口连接正常，波特率115200");
            
            // 添加一些执行过程日志
            var messages = new[]
            {
                "正在初始化测试环境...",
                "发送测试指令...",
                "等待设备响应...",
                "读取测试数据...",
                "验证测试结果..."
            };
            
            for (int j = 0; j < Math.Min(3, messages.Length); j++)
            {
                dut.AddLog(UTF.Logging.LogLevel.Debug, messages[j]);
            }
        }
        
        // 如果全部完成，完成所有剩余步骤
        if (allComplete)
        {
            for (int i = currentStep; i < dut.TestSteps.Count; i++)
            {
                var step = dut.TestSteps[i];
                step.Status = TestStepStatus.Completed;
                step.Result = TestStepResult.Pass;
                step.StartTime = dut.StartTime.AddMinutes(i * 2);
                step.EndTime = step.StartTime.AddSeconds(random.Next(30, 120));
                step.Duration = step.EndTime - step.StartTime;
                step.ResultMessage = "测试通过";
            }
            
            dut.AddLog(UTF.Logging.LogLevel.Info, "所有测试步骤已完成");
            dut.AddLog(UTF.Logging.LogLevel.Info, "测试结果", "PASS - 设备功能正常");
        }
        
        // 更新持续时间
        dut.Duration = DateTime.Now - dut.StartTime;
    }

    private void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        // 模拟实时数据更新
        foreach (var dutRow in _viewModel.DUTs)
        {
            var dut = dutRow.DUTInfo;
            
            // 更新持续时间
            if (dut.StartTime != DateTime.MinValue)
            {
                dut.Duration = DateTime.Now - dut.StartTime;
            }
            
            // 模拟正在运行的DUT添加新日志
            if (dut.Status == DUTTestStatus.Running)
            {
                var random = new Random();
                if (random.Next(0, 10) < 2) // 20%概率添加新日志
                {
                    var messages = new[]
                    {
                        "数据采集中...",
                        "验证测试参数...",
                        "检查设备响应...",
                        "更新测试进度...",
                        "记录测试数据..."
                    };
                    
                    var message = messages[random.Next(messages.Length)];
                    dut.AddLog(UTF.Logging.LogLevel.Debug, message);
                }
            }
            
            // 通知UI更新
            dutRow.UpdateFromDUTInfo();
        }
        
        // 更新统计信息
        _viewModel.UpdateStatistics();
        
        // 更新窗口标题
        Title = $"多DUT测试监控台 - {_viewModel.TotalDUTs}个设备 (运行:{_viewModel.RunningDUTs}, 通过:{_viewModel.PassedDUTs}, 失败:{_viewModel.FailedDUTs})";
    }

    // 事件处理方法
    private void StartAllBtn_Click(object sender, RoutedEventArgs e)
    {
        foreach (var dutRow in _viewModel.DUTs.Where(d => d.OverallStatus == DUTTestStatus.Ready))
        {
            dutRow.DUTInfo.Status = DUTTestStatus.Running;
            dutRow.DUTInfo.AddLog(UTF.Logging.LogLevel.Info, "开始批量测试");
            dutRow.UpdateFromDUTInfo();
        }
        
        _viewModel.UpdateStatistics();
        MessageBox.Show($"已启动 {_viewModel.DUTs.Count(d => d.OverallStatus == DUTTestStatus.Running)} 个DUT的测试", 
            "批量启动", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void PauseAllBtn_Click(object sender, RoutedEventArgs e)
    {
        foreach (var dutRow in _viewModel.DUTs.Where(d => d.OverallStatus == DUTTestStatus.Running))
        {
            dutRow.DUTInfo.Status = DUTTestStatus.Paused;
            dutRow.DUTInfo.AddLog(UTF.Logging.LogLevel.Warning, "测试已暂停");
            dutRow.UpdateFromDUTInfo();
        }
        
        _viewModel.UpdateStatistics();
        MessageBox.Show("所有运行中的测试已暂停", "批量暂停", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void StopAllBtn_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show("确定要停止所有正在运行的测试吗？", "确认停止", 
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        
        if (result == MessageBoxResult.Yes)
        {
            foreach (var dutRow in _viewModel.DUTs.Where(d => d.OverallStatus == DUTTestStatus.Running || d.OverallStatus == DUTTestStatus.Paused))
            {
                dutRow.DUTInfo.Status = DUTTestStatus.Idle;
                dutRow.DUTInfo.Result = DUTTestResult.Cancelled;
                dutRow.DUTInfo.AddLog(UTF.Logging.LogLevel.Warning, "测试已停止");
                dutRow.UpdateFromDUTInfo();
            }
            
            _viewModel.UpdateStatistics();
            MessageBox.Show("所有测试已停止", "批量停止", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void RefreshBtn_Click(object sender, RoutedEventArgs e)
    {
        LoadSimulatedData();
        MessageBox.Show("数据已刷新", "刷新完成", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ExportReportBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var fileName = $"DUT_Test_Report_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), fileName);
            
            var reportContent = new StringBuilder();
            reportContent.AppendLine("DUT测试监控报告");
            reportContent.AppendLine($"生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            reportContent.AppendLine($"总DUT数量: {_viewModel.TotalDUTs}");
            reportContent.AppendLine($"运行中: {_viewModel.RunningDUTs}");
            reportContent.AppendLine($"等待中: {_viewModel.WaitingDUTs}");
            reportContent.AppendLine($"通过: {_viewModel.PassedDUTs}");
            reportContent.AppendLine($"失败: {_viewModel.FailedDUTs}");
            reportContent.AppendLine($"通过率: {_viewModel.PassRate:F1}%");
            reportContent.AppendLine(new string('=', 80));
            reportContent.AppendLine();
            
            foreach (var dutRow in _viewModel.DUTs)
            {
                var dut = dutRow.DUTInfo;
                reportContent.AppendLine($"DUT ID: {dut.DutId}");
                reportContent.AppendLine($"DUT名称: {dut.DutName}");
                reportContent.AppendLine($"状态: {dutRow.OverallStatusText}");
                reportContent.AppendLine($"结果: {dut.Result}");
                reportContent.AppendLine($"开始时间: {dut.StartTimeDisplay}");
                reportContent.AppendLine($"持续时间: {dut.DurationDisplay}");
                reportContent.AppendLine("测试步骤:");
                
                foreach (var step in dut.TestSteps)
                {
                    reportContent.AppendLine($"  {step.StepIndex + 1}. {step.StepName}: {step.StatusDisplay} - {step.ResultDisplay} ({step.DurationDisplay})");
                }
                
                reportContent.AppendLine();
            }
            
            File.WriteAllText(filePath, reportContent.ToString(), Encoding.UTF8);
            
            MessageBox.Show($"报告已导出到: {filePath}", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导出报告失败: {ex.Message}", "导出失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ClearLogsBtn_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show("确定要清除所有DUT的日志吗？此操作不可撤销。", "确认清除", 
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        
        if (result == MessageBoxResult.Yes)
        {
            foreach (var dutRow in _viewModel.DUTs)
            {
                dutRow.DUTInfo.Logs.Clear();
                dutRow.DUTInfo.AddLog(UTF.Logging.LogLevel.Info, "日志已清除");
                dutRow.UpdateFromDUTInfo();
            }
            
            MessageBox.Show("所有日志已清除", "清除完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void ExpandBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is DUTTestRowViewModel dutRow)
        {
            dutRow.IsExpanded = !dutRow.IsExpanded;
            
            // 更新DataGrid行详细信息显示
            var row = DUTListDataGrid.ItemContainerGenerator.ContainerFromItem(dutRow) as DataGridRow;
            if (row != null)
            {
                row.DetailsVisibility = dutRow.IsExpanded ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }

    private void ViewLogBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is DUTTestRowViewModel dutRow)
        {
            var detailWindow = new DUTDetailWindow(dutRow.DUTInfo);
            detailWindow.Show();
        }
    }

    private void ViewDetailBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is DUTTestRowViewModel dutRow)
        {
            var detailWindow = new DUTDetailWindow(dutRow.DUTInfo);
            detailWindow.Show();
        }
    }

    private void PauseBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is DUTTestRowViewModel dutRow)
        {
            if (dutRow.OverallStatus == DUTTestStatus.Running)
            {
                dutRow.DUTInfo.Status = DUTTestStatus.Paused;
                dutRow.DUTInfo.AddLog(UTF.Logging.LogLevel.Warning, "测试已暂停");
            }
            else if (dutRow.OverallStatus == DUTTestStatus.Paused)
            {
                dutRow.DUTInfo.Status = DUTTestStatus.Running;
                dutRow.DUTInfo.AddLog(UTF.Logging.LogLevel.Info, "测试已恢复");
            }
            
            dutRow.UpdateFromDUTInfo();
            _viewModel.UpdateStatistics();
        }
    }

    private void RetestBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is DUTTestRowViewModel dutRow)
        {
            var result = MessageBox.Show($"确定要重新测试 {dutRow.DutId} 吗？", "确认重测", 
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                // 重置DUT状态
                dutRow.DUTInfo.Status = DUTTestStatus.Ready;
                dutRow.DUTInfo.Result = DUTTestResult.Pending;
                dutRow.DUTInfo.CurrentStepIndex = 0;
                dutRow.DUTInfo.StartTime = DateTime.MinValue;
                dutRow.DUTInfo.Duration = TimeSpan.Zero;
                
                // 重置所有步骤
                foreach (var step in dutRow.DUTInfo.TestSteps)
                {
                    step.Status = TestStepStatus.Pending;
                    step.Result = TestStepResult.NotRun;
                    step.StartTime = DateTime.MinValue;
                    step.EndTime = DateTime.MinValue;
                    step.Duration = TimeSpan.Zero;
                    step.ResultMessage = null;
                }
                
                dutRow.DUTInfo.AddLog(UTF.Logging.LogLevel.Info, "重新开始测试");
                dutRow.UpdateFromDUTInfo();
                _viewModel.UpdateStatistics();
            }
        }
    }

    private void ViewFullLogBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is DUTTestRowViewModel dutRow)
        {
            var detailWindow = new DUTDetailWindow(dutRow.DUTInfo);
            detailWindow.Show();
        }
    }

    private void ClearSelectedLogBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedDUT != null)
        {
            var result = MessageBox.Show($"确定要清除 {_viewModel.SelectedDUT.DutId} 的日志吗？", "确认清除", 
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            
            if (result == MessageBoxResult.Yes)
            {
                _viewModel.SelectedDUT.DUTInfo.Logs.Clear();
                _viewModel.SelectedDUT.DUTInfo.AddLog(UTF.Logging.LogLevel.Info, "日志已清除");
                _viewModel.SelectedDUT.UpdateFromDUTInfo();
            }
        }
    }

    private void ExportSelectedLogBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedDUT != null)
        {
            try
            {
                var dut = _viewModel.SelectedDUT.DUTInfo;
                var fileName = $"DUT_{dut.DutId}_Log_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), fileName);
                
                var logContent = new StringBuilder();
                logContent.AppendLine($"DUT测试日志导出");
                logContent.AppendLine($"DUT ID: {dut.DutId}");
                logContent.AppendLine($"DUT名称: {dut.DutName}");
                logContent.AppendLine($"导出时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                logContent.AppendLine(new string('=', 80));
                logContent.AppendLine();
                
                foreach (var log in dut.Logs)
                {
                    logContent.AppendLine($"[{log.TimeDisplay}] [{log.LevelDisplay}] [{log.StepName}] {log.FullMessage}");
                    if (!string.IsNullOrEmpty(log.Details))
                    {
                        logContent.AppendLine($"    详情: {log.Details}");
                    }
                }
                
                File.WriteAllText(filePath, logContent.ToString(), Encoding.UTF8);
                
                MessageBox.Show($"日志已导出到: {filePath}", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出日志失败: {ex.Message}", "导出失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _refreshTimer?.Stop();
        base.OnClosed(e);
    }
}
