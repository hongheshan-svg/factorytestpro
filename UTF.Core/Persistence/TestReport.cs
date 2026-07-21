using System;
using System.Collections.Generic;

namespace UTF.Core.Persistence;

/// <summary>
/// 测试步骤执行结果 - 持久化契约的一部分。
/// 由 ConfigDrivenTestEngine.ConvertToTestReport 产出，并经 ITestResultRepository 序列化。
/// </summary>
public sealed record TestStepExecutionResult
{
    /// <summary>步骤ID</summary>
    public string StepId { get; init; } = string.Empty;

    /// <summary>步骤名称</summary>
    public string StepName { get; init; } = string.Empty;

    /// <summary>是否通过</summary>
    public bool Passed { get; init; }

    /// <summary>测量值</summary>
    public object? MeasuredValue { get; init; }

    /// <summary>期望值</summary>
    public object? ExpectedValue { get; init; }

    /// <summary>错误信息</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>开始时间</summary>
    public DateTime StartTime { get; init; }

    /// <summary>结束时间</summary>
    public DateTime EndTime { get; init; }

    /// <summary>执行时间</summary>
    public TimeSpan ExecutionTime => EndTime - StartTime;

    /// <summary>重试次数</summary>
    public int RetryCount { get; init; }

    /// <summary>扩展数据</summary>
    public Dictionary<string, object> ExtendedData { get; init; } = new();
}

/// <summary>
/// 测试结果报告 - ITestResultRepository 的持久化契约。
/// FileTestResultRepository 以 JSON 形式序列化此类。
/// </summary>
public sealed record TestReport
{
    /// <summary>报告ID</summary>
    public string ReportId { get; init; } = string.Empty;

    /// <summary>任务ID</summary>
    public string TaskId { get; init; } = string.Empty;

    /// <summary>DUT ID</summary>
    public string DUTId { get; init; } = string.Empty;

    /// <summary>总体结果</summary>
    public bool OverallResult { get; init; }

    /// <summary>步骤结果列表</summary>
    public List<TestStepExecutionResult> StepResults { get; init; } = new();

    /// <summary>开始时间</summary>
    public DateTime StartTime { get; init; }

    /// <summary>结束时间</summary>
    public DateTime EndTime { get; init; }

    /// <summary>总执行时间</summary>
    public TimeSpan TotalExecutionTime => EndTime - StartTime;

    /// <summary>操作员</summary>
    public string Operator { get; init; } = string.Empty;

    /// <summary>测试站台</summary>
    public string TestStation { get; init; } = string.Empty;

    /// <summary>通过的步骤数</summary>
    public int PassedSteps => StepResults.Count(r => r.Passed);

    /// <summary>失败的步骤数</summary>
    public int FailedSteps => StepResults.Count(r => !r.Passed);

    /// <summary>总步骤数</summary>
    public int TotalSteps => StepResults.Count;

    /// <summary>通过率</summary>
    public double PassRate => TotalSteps > 0 ? (double)PassedSteps / TotalSteps : 0.0;

    /// <summary>扩展数据</summary>
    public Dictionary<string, object> ExtendedData { get; init; } = new();
}
