using System;

namespace UTF.Core;

/// <summary>
/// 测试序列状态
/// </summary>
public enum SimpleTestSequenceStatus
{
    Running,
    Passed,
    Failed,
    Stopped
}

/// <summary>
/// 测试序列结果
/// </summary>
public class SimpleTestSequenceResult
{
    public SimpleTestSequenceStatus Status { get; set; }
    public int TotalSteps { get; set; }
    public int PassedSteps { get; set; }
    public int FailedSteps { get; set; }
    public TimeSpan TotalDuration { get; set; }
}

/// <summary>
/// 测试进度变化事件参数
/// </summary>
public class SimpleTestProgressEventArgs : EventArgs
{
    public string Message { get; set; } = string.Empty;
    public int CurrentStep { get; set; }
    public int TotalSteps { get; set; }
    public double ProgressPercentage { get; set; }
    public double Progress { get; set; }  // 进度百分比 (0-100)
    public string StepName { get; set; } = string.Empty;
}

/// <summary>
/// 测试步骤完成事件参数
/// </summary>
public class SimpleTestStepCompletedEventArgs : EventArgs
{
    public string StepName { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public string Message { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// 测试序列完成事件参数
/// </summary>
public class SimpleTestSequenceCompletedEventArgs : EventArgs
{
    public string SequenceName { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public int TotalSteps { get; set; }
    public int PassedSteps { get; set; }
    public int FailedSteps { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public SimpleTestSequenceResult SequenceResult { get; set; } = new();
}

/// <summary>
/// 测试错误事件参数
/// </summary>
public class SimpleTestErrorEventArgs : EventArgs
{
    public string ErrorMessage { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;  // 错误信息
    public Exception? Exception { get; set; }
    public string Source { get; set; } = string.Empty;
}

