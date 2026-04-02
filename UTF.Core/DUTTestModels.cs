using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using UTF.Logging;

namespace UTF.Core;

/// <summary>
/// DUT测试信息 - 支持详细的测试步骤跟踪
/// </summary>
public class DUTTestInfo : INotifyPropertyChanged
{
    private string _dutId = "";
    private string _dutName = "";
    private string _currentStep = "";
    private string _progress = "";
    private DUTTestStatus _status = DUTTestStatus.Idle;
    private DUTTestResult _result = DUTTestResult.Pending;
    private DateTime _startTime = DateTime.MinValue;
    private TimeSpan _duration = TimeSpan.Zero;
    private int _currentStepIndex = 0;

    public string DutId
    {
        get => _dutId;
        set => SetProperty(ref _dutId, value);
    }

    public string DutName
    {
        get => _dutName;
        set => SetProperty(ref _dutName, value);
    }

    public string CurrentStep
    {
        get => _currentStep;
        set => SetProperty(ref _currentStep, value);
    }

    public string Progress
    {
        get => _progress;
        set => SetProperty(ref _progress, value);
    }

    public DUTTestStatus Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public DUTTestResult Result
    {
        get => _result;
        set => SetProperty(ref _result, value);
    }

    public DateTime StartTime
    {
        get => _startTime;
        set => SetProperty(ref _startTime, value);
    }

    public TimeSpan Duration
    {
        get => _duration;
        set => SetProperty(ref _duration, value);
    }

    public string StartTimeDisplay => StartTime == DateTime.MinValue ? "-" : StartTime.ToString("HH:mm:ss");
    public string DurationDisplay => Duration == TimeSpan.Zero ? "-" : Duration.ToString(@"mm\:ss");

    public int CurrentStepIndex
    {
        get => _currentStepIndex;
        set
        {
            SetProperty(ref _currentStepIndex, value);
            UpdateProgress();
        }
    }

    /// <summary>
    /// 测试步骤列表
    /// </summary>
    public List<TestStepInfo> TestSteps { get; set; } = new();

    /// <summary>
    /// DUT专属日志列表
    /// </summary>
    public List<DUTLogEntry> Logs { get; set; } = new();

    /// <summary>
    /// 更新进度显示
    /// </summary>
    private void UpdateProgress()
    {
        if (TestSteps.Count > 0)
        {
            Progress = $"{CurrentStepIndex}/{TestSteps.Count}";
            if (CurrentStepIndex < TestSteps.Count)
            {
                CurrentStep = TestSteps[CurrentStepIndex].StepName;
            }
        }
    }

    /// <summary>
    /// 添加日志条目
    /// </summary>
    public void AddLog(LogLevel level, string message, string? details = null)
    {
        var logEntry = new DUTLogEntry
        {
            Timestamp = DateTime.Now,
            Level = level,
            Message = message,
            Details = details,
            StepIndex = CurrentStepIndex,
            StepName = CurrentStep
        };
        Logs.Add(logEntry);
        OnPropertyChanged(nameof(Logs));
    }

    /// <summary>
    /// 开始测试步骤
    /// </summary>
    public void StartStep(int stepIndex)
    {
        if (stepIndex < TestSteps.Count)
        {
            CurrentStepIndex = stepIndex;
            TestSteps[stepIndex].Status = TestStepStatus.Running;
            TestSteps[stepIndex].StartTime = DateTime.Now;
            
            AddLog(LogLevel.Info, $"开始执行步骤: {TestSteps[stepIndex].StepName}");
            OnPropertyChanged(nameof(TestSteps));
        }
    }

    /// <summary>
    /// 完成测试步骤
    /// </summary>
    public void CompleteStep(int stepIndex, TestStepResult result, string? message = null)
    {
        if (stepIndex < TestSteps.Count)
        {
            var step = TestSteps[stepIndex];
            step.Status = TestStepStatus.Completed;
            step.Result = result;
            step.EndTime = DateTime.Now;
            step.Duration = step.EndTime - step.StartTime;
            step.ResultMessage = message;

            var logLevel = result == TestStepResult.Pass ? LogLevel.Info : LogLevel.Error;
            var logMessage = $"步骤完成: {step.StepName} - {result}";
            if (!string.IsNullOrEmpty(message))
            {
                logMessage += $" ({message})";
            }
            
            AddLog(logLevel, logMessage);
            OnPropertyChanged(nameof(TestSteps));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

/// <summary>
/// 测试步骤信息。
/// 当前仍被 DUT 监控与旧 UI 展示链路使用。
/// </summary>
public class TestStepInfo
{
    public int StepIndex { get; set; }
    public string StepName { get; set; } = "";
    public string Description { get; set; } = "";
    public TestStepStatus Status { get; set; } = TestStepStatus.Pending;
    public TestStepResult Result { get; set; } = TestStepResult.NotRun;
    public DateTime StartTime { get; set; } = DateTime.MinValue;
    public DateTime EndTime { get; set; } = DateTime.MinValue;
    public TimeSpan Duration { get; set; } = TimeSpan.Zero;
    public string? ResultMessage { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
    public Dictionary<string, object> Results { get; set; } = new();

    public string StatusDisplay => Status switch
    {
        TestStepStatus.Pending => "等待",
        TestStepStatus.Running => "执行中",
        TestStepStatus.Completed => "完成",
        TestStepStatus.Skipped => "跳过",
        TestStepStatus.Failed => "失败",
        _ => "未知"
    };

    public string ResultDisplay => Result switch
    {
        TestStepResult.NotRun => "-",
        TestStepResult.Pass => "PASS",
        TestStepResult.Fail => "FAIL",
        TestStepResult.Skip => "SKIP",
        _ => "?"
    };

    public string DurationDisplay => Duration == TimeSpan.Zero ? "-" : Duration.ToString(@"mm\:ss\.fff");
}

/// <summary>
/// DUT日志条目
/// </summary>
public class DUTLogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public LogLevel Level { get; set; } = LogLevel.Info;
    public string Message { get; set; } = "";
    public string? Details { get; set; }
    public int StepIndex { get; set; } = -1;
    public string StepName { get; set; } = "";
    public string? SourceComponent { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
    
    /// <summary>
    /// 用于显示的格式化文本
    /// </summary>
    public string DisplayText => 
        $"[{Timestamp:HH:mm:ss.fff}] [{Level}] {Message}" +
        (string.IsNullOrEmpty(Details) ? "" : $" - {Details}") +
        (string.IsNullOrEmpty(StepName) ? "" : $" ({StepName})");

    public string TimeDisplay => Timestamp.ToString("HH:mm:ss.fff");
    public string LevelDisplay => Level switch
    {
        LogLevel.Debug => "DEBUG",
        LogLevel.Info => "INFO",
        LogLevel.Warning => "WARN",
        LogLevel.Error => "ERROR",
        LogLevel.Critical => "CRIT",
        _ => "UNKN"
    };

    public string FullMessage => StepIndex >= 0 
        ? $"[步骤{StepIndex + 1}] {Message}" 
        : Message;
}

/// <summary>
/// DUT测试状态枚举
/// </summary>
public enum DUTTestStatus
{
    Idle,           // 空闲
    Ready,          // 就绪
    Running,        // 测试中
    Paused,         // 暂停
    Completed,      // 完成
    Failed,         // 失败
    Error,          // 错误
    Disconnected    // 断开连接
}

/// <summary>
/// DUT测试结果枚举
/// </summary>
public enum DUTTestResult
{
    Pending,        // 待测试
    Running,        // 执行中
    Pass,           // 通过
    Fail,           // 失败
    Error,          // 错误
    Timeout,        // 超时
    Cancelled       // 取消
}

/// <summary>
/// 测试步骤状态枚举
/// </summary>
public enum TestStepStatus
{
    Pending,        // 等待执行
    Running,        // 执行中
    Completed,      // 完成
    Skipped,        // 跳过
    Failed          // 失败
}

/// <summary>
/// 测试步骤结果枚举
/// </summary>
public enum TestStepResult
{
    NotRun,         // 未运行
    Pass,           // 通过
    Fail,           // 失败
    Skip            // 跳过
}

/// <summary>
/// DUT日志级别枚举
/// </summary>

/// <summary>
/// DUT测试管理器 - 负责管理多个DUT的测试状态
/// </summary>
public class DUTTestManager
{
    private readonly Dictionary<string, DUTTestInfo> _duts = new();
    private readonly object _lockObject = new();

    public event EventHandler<DUTTestInfo>? DUTStatusChanged;
    public event EventHandler<DUTLogEntry>? DUTLogAdded;

    /// <summary>
    /// 注册DUT
    /// </summary>
    public void RegisterDUT(DUTTestInfo dutInfo)
    {
        lock (_lockObject)
        {
            _duts[dutInfo.DutId] = dutInfo;
            dutInfo.PropertyChanged += (s, e) => DUTStatusChanged?.Invoke(this, dutInfo);
        }
    }

    /// <summary>
    /// 获取DUT信息
    /// </summary>
    public DUTTestInfo? GetDUT(string dutId)
    {
        lock (_lockObject)
        {
            return _duts.TryGetValue(dutId, out var dut) ? dut : null;
        }
    }

    /// <summary>
    /// 获取所有DUT
    /// </summary>
    public List<DUTTestInfo> GetAllDUTs()
    {
        lock (_lockObject)
        {
            return _duts.Values.ToList();
        }
    }

    /// <summary>
    /// 添加DUT日志
    /// </summary>
    public void AddDUTLog(string dutId, LogLevel level, string message, string? details = null)
    {
        var dut = GetDUT(dutId);
        if (dut != null)
        {
            dut.AddLog(level, message, details);
            
            // 触发日志添加事件
            var logEntry = dut.Logs.LastOrDefault();
            if (logEntry != null)
            {
                DUTLogAdded?.Invoke(this, logEntry);
            }
        }
    }

    /// <summary>
    /// 开始DUT测试步骤
    /// </summary>
    public void StartDUTStep(string dutId, int stepIndex)
    {
        var dut = GetDUT(dutId);
        dut?.StartStep(stepIndex);
    }

    /// <summary>
    /// 完成DUT测试步骤
    /// </summary>
    public void CompleteDUTStep(string dutId, int stepIndex, TestStepResult result, string? message = null)
    {
        var dut = GetDUT(dutId);
        dut?.CompleteStep(stepIndex, result, message);
    }

    /// <summary>
    /// 创建标准测试步骤
    /// </summary>
    public static List<TestStepInfo> CreateStandardTestSteps()
    {
        return new List<TestStepInfo>
        {
            new() { StepIndex = 0, StepName = "设备初始化", Description = "初始化DUT设备和测试环境" },
            new() { StepIndex = 1, StepName = "连接测试", Description = "测试DUT通信连接是否正常" },
            new() { StepIndex = 2, StepName = "功能测试", Description = "测试DUT核心功能" },
            new() { StepIndex = 3, StepName = "性能测试", Description = "测试DUT性能指标" },
            new() { StepIndex = 4, StepName = "稳定性测试", Description = "长时间稳定性测试" },
            new() { StepIndex = 5, StepName = "生成报告", Description = "生成测试结果报告" }
        };
    }
}
