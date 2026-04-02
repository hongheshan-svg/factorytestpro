using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using UTF.Core;

namespace UTF.UI.ViewModels;

/// <summary>
/// DUT测试列表主视图模型
/// </summary>
public class DUTTestListViewModel : INotifyPropertyChanged
{
    private DUTTestRowViewModel? _selectedDUT;
    private int _totalDUTs;
    private int _runningDUTs;
    private int _waitingDUTs;
    private int _passedDUTs;
    private int _failedDUTs;
    private double _passRate;

    public DUTTestListViewModel()
    {
        DUTs = new ObservableCollection<DUTTestRowViewModel>();
        SelectedDUTLogs = new ObservableCollection<DUTLogEntry>();
        
        // 监听DUT集合变化
        DUTs.CollectionChanged += (s, e) => UpdateStatistics();
    }

    /// <summary>
    /// DUT列表
    /// </summary>
    public ObservableCollection<DUTTestRowViewModel> DUTs { get; }

    /// <summary>
    /// 选中的DUT
    /// </summary>
    public DUTTestRowViewModel? SelectedDUT
    {
        get => _selectedDUT;
        set
        {
            if (SetProperty(ref _selectedDUT, value))
            {
                UpdateSelectedDUTLogs();
            }
        }
    }

    /// <summary>
    /// 选中DUT的日志
    /// </summary>
    public ObservableCollection<DUTLogEntry> SelectedDUTLogs { get; }

    // 统计属性
    public int TotalDUTs
    {
        get => _totalDUTs;
        set => SetProperty(ref _totalDUTs, value);
    }

    public int RunningDUTs
    {
        get => _runningDUTs;
        set => SetProperty(ref _runningDUTs, value);
    }

    public int WaitingDUTs
    {
        get => _waitingDUTs;
        set => SetProperty(ref _waitingDUTs, value);
    }

    public int PassedDUTs
    {
        get => _passedDUTs;
        set => SetProperty(ref _passedDUTs, value);
    }

    public int FailedDUTs
    {
        get => _failedDUTs;
        set => SetProperty(ref _failedDUTs, value);
    }

    public double PassRate
    {
        get => _passRate;
        set => SetProperty(ref _passRate, value);
    }

    /// <summary>
    /// 更新统计信息
    /// </summary>
    public void UpdateStatistics()
    {
        TotalDUTs = DUTs.Count;
        RunningDUTs = DUTs.Count(d => d.OverallStatus == DUTTestStatus.Running);
        WaitingDUTs = DUTs.Count(d => d.OverallStatus == DUTTestStatus.Idle || d.OverallStatus == DUTTestStatus.Ready);
        PassedDUTs = DUTs.Count(d => d.OverallResult == DUTTestResult.Pass);
        FailedDUTs = DUTs.Count(d => d.OverallResult == DUTTestResult.Fail);
        
        var completedTests = PassedDUTs + FailedDUTs;
        PassRate = completedTests > 0 ? (double)PassedDUTs / completedTests * 100 : 0;
    }

    /// <summary>
    /// 更新选中DUT的日志显示
    /// </summary>
    private void UpdateSelectedDUTLogs()
    {
        SelectedDUTLogs.Clear();
        if (SelectedDUT?.DUTInfo?.Logs != null)
        {
            foreach (var log in SelectedDUT.DUTInfo.Logs.OrderBy(l => l.Timestamp))
            {
                SelectedDUTLogs.Add(log);
            }
        }
    }

    /// <summary>
    /// 添加DUT
    /// </summary>
    public void AddDUT(DUTTestInfo dutInfo)
    {
        var rowViewModel = new DUTTestRowViewModel(dutInfo);
        DUTs.Add(rowViewModel);
        
        // 监听DUT状态变化
        dutInfo.PropertyChanged += (s, e) =>
        {
            rowViewModel.UpdateFromDUTInfo();
            UpdateStatistics();
            
            // 如果是当前选中的DUT，更新日志显示
            if (SelectedDUT == rowViewModel)
            {
                UpdateSelectedDUTLogs();
            }
        };
    }

    /// <summary>
    /// 清除所有DUT
    /// </summary>
    public void ClearDUTs()
    {
        DUTs.Clear();
        SelectedDUT = null;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

/// <summary>
/// DUT测试行视图模型
/// </summary>
public class DUTTestRowViewModel : INotifyPropertyChanged
{
    private bool _isExpanded;
    private string _currentStepText = "";
    private string _durationText = "";

    public DUTTestRowViewModel(DUTTestInfo dutInfo)
    {
        DUTInfo = dutInfo;
        Steps = new ObservableCollection<TestStepViewModel>();
        RecentLogs = new ObservableCollection<DUTLogEntry>();
        
        UpdateFromDUTInfo();
    }

    /// <summary>
    /// 关联的DUT测试信息
    /// </summary>
    public DUTTestInfo DUTInfo { get; }

    /// <summary>
    /// DUT ID
    /// </summary>
    public string DutId => DUTInfo.DutId;

    /// <summary>
    /// DUT名称
    /// </summary>
    public string DutName => DUTInfo.DutName;

    /// <summary>
    /// 整体状态
    /// </summary>
    public DUTTestStatus OverallStatus => DUTInfo.Status;

    /// <summary>
    /// 整体结果
    /// </summary>
    public DUTTestResult OverallResult => DUTInfo.Result;

    /// <summary>
    /// 当前步骤文本
    /// </summary>
    public string CurrentStepText
    {
        get => _currentStepText;
        set => SetProperty(ref _currentStepText, value);
    }

    /// <summary>
    /// 持续时间文本
    /// </summary>
    public string DurationText
    {
        get => _durationText;
        set => SetProperty(ref _durationText, value);
    }

    /// <summary>
    /// 整体状态文本
    /// </summary>
    public string OverallStatusText => OverallStatus switch
    {
        DUTTestStatus.Idle => "⏳等待",
        DUTTestStatus.Ready => "🟢就绪",
        DUTTestStatus.Running => "🔄测试中",
        DUTTestStatus.Paused => "⏸️暂停",
        DUTTestStatus.Completed => "✅完成",
        DUTTestStatus.Failed => "❌失败",
        DUTTestStatus.Error => "🚨错误",
        DUTTestStatus.Disconnected => "🔌断开",
        _ => "❓未知"
    };

    /// <summary>
    /// 整体状态颜色
    /// </summary>
    public SolidColorBrush OverallStatusBrush => OverallStatus switch
    {
        DUTTestStatus.Idle => new SolidColorBrush(Color.FromRgb(149, 165, 166)),      // 灰色
        DUTTestStatus.Ready => new SolidColorBrush(Color.FromRgb(46, 204, 113)),      // 绿色
        DUTTestStatus.Running => new SolidColorBrush(Color.FromRgb(243, 156, 18)),    // 橙色
        DUTTestStatus.Paused => new SolidColorBrush(Color.FromRgb(241, 196, 15)),     // 黄色
        DUTTestStatus.Completed => new SolidColorBrush(Color.FromRgb(39, 174, 96)),   // 深绿色
        DUTTestStatus.Failed => new SolidColorBrush(Color.FromRgb(231, 76, 60)),      // 红色
        DUTTestStatus.Error => new SolidColorBrush(Color.FromRgb(192, 57, 43)),       // 深红色
        DUTTestStatus.Disconnected => new SolidColorBrush(Color.FromRgb(127, 140, 141)), // 深灰色
        _ => new SolidColorBrush(Color.FromRgb(149, 165, 166))
    };

    /// <summary>
    /// 测试步骤列表
    /// </summary>
    public ObservableCollection<TestStepViewModel> Steps { get; }

    /// <summary>
    /// 最近的日志 (最多3条)
    /// </summary>
    public ObservableCollection<DUTLogEntry> RecentLogs { get; }

    /// <summary>
    /// 是否展开显示日志
    /// </summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    /// <summary>
    /// 从DUT信息更新视图模型
    /// </summary>
    public void UpdateFromDUTInfo()
    {
        // 更新当前步骤信息
        CurrentStepText = $"{DUTInfo.CurrentStepIndex + 1}/{DUTInfo.TestSteps.Count}";
        if (DUTInfo.CurrentStepIndex < DUTInfo.TestSteps.Count)
        {
            CurrentStepText += $" - {DUTInfo.TestSteps[DUTInfo.CurrentStepIndex].StepName}";
        }

        // 更新持续时间
        DurationText = DUTInfo.DurationDisplay;

        // 更新测试步骤
        Steps.Clear();
        foreach (var step in DUTInfo.TestSteps)
        {
            Steps.Add(new TestStepViewModel(step));
        }

        // 更新最近日志 (最多3条)
        RecentLogs.Clear();
        var recentLogs = DUTInfo.Logs.OrderByDescending(l => l.Timestamp).Take(3);
        foreach (var log in recentLogs.Reverse()) // 倒序显示，最新的在下面
        {
            RecentLogs.Add(log);
        }

        // 通知属性变化
        OnPropertyChanged(nameof(DutId));
        OnPropertyChanged(nameof(DutName));
        OnPropertyChanged(nameof(OverallStatus));
        OnPropertyChanged(nameof(OverallResult));
        OnPropertyChanged(nameof(OverallStatusText));
        OnPropertyChanged(nameof(OverallStatusBrush));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

/// <summary>
/// 测试步骤视图模型
/// </summary>
public class TestStepViewModel : INotifyPropertyChanged
{
    public TestStepViewModel(TestStepInfo stepInfo)
    {
        StepInfo = stepInfo;
        UpdateFromStepInfo();
    }

    /// <summary>
    /// 关联的步骤信息
    /// </summary>
    public TestStepInfo StepInfo { get; }

    /// <summary>
    /// 步骤名称
    /// </summary>
    public string StepName => StepInfo.StepName;

    /// <summary>
    /// 步骤状态
    /// </summary>
    public TestStepStatus Status => StepInfo.Status;

    /// <summary>
    /// 步骤结果
    /// </summary>
    public TestStepResult Result => StepInfo.Result;

    /// <summary>
    /// 持续时间
    /// </summary>
    public string Duration => StepInfo.DurationDisplay;

    /// <summary>
    /// 状态显示文本
    /// </summary>
    public string StatusText => Status switch
    {
        TestStepStatus.Pending => "⏳等待",
        TestStepStatus.Running => "🔄执行中",
        TestStepStatus.Completed => Result switch
        {
            TestStepResult.Pass => "✅PASS",
            TestStepResult.Fail => "❌FAIL",
            TestStepResult.Skip => "⏭️SKIP",
            _ => "✅完成"
        },
        TestStepStatus.Skipped => "⏭️跳过",
        TestStepStatus.Failed => "❌失败",
        _ => "❓未知"
    };

    /// <summary>
    /// 状态颜色
    /// </summary>
    public SolidColorBrush StatusBrush => Status switch
    {
        TestStepStatus.Pending => new SolidColorBrush(Color.FromRgb(149, 165, 166)),    // 灰色
        TestStepStatus.Running => new SolidColorBrush(Color.FromRgb(243, 156, 18)),     // 橙色
        TestStepStatus.Completed => Result switch
        {
            TestStepResult.Pass => new SolidColorBrush(Color.FromRgb(39, 174, 96)),     // 绿色
            TestStepResult.Fail => new SolidColorBrush(Color.FromRgb(231, 76, 60)),     // 红色
            TestStepResult.Skip => new SolidColorBrush(Color.FromRgb(52, 152, 219)),    // 蓝色
            _ => new SolidColorBrush(Color.FromRgb(39, 174, 96))
        },
        TestStepStatus.Skipped => new SolidColorBrush(Color.FromRgb(52, 152, 219)),     // 蓝色
        TestStepStatus.Failed => new SolidColorBrush(Color.FromRgb(231, 76, 60)),       // 红色
        _ => new SolidColorBrush(Color.FromRgb(149, 165, 166))
    };

    /// <summary>
    /// 从步骤信息更新视图模型
    /// </summary>
    public void UpdateFromStepInfo()
    {
        OnPropertyChanged(nameof(StepName));
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(Result));
        OnPropertyChanged(nameof(Duration));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusBrush));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
