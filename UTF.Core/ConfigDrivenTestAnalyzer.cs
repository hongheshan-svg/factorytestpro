using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UTF.Logging;

namespace UTF.Core;

/// <summary>
/// 测试数据分析器 - 提供测试结果的统计分析功能
/// </summary>
public sealed class ConfigDrivenTestAnalyzer
{
    private readonly ILogger _logger;

    public ConfigDrivenTestAnalyzer(ILogger? logger = null)
    {
        _logger = logger ?? LoggerFactory.CreateLogger<ConfigDrivenTestAnalyzer>();
    }

    /// <summary>
    /// 分析测试会话
    /// </summary>
    public TestSessionAnalysis AnalyzeSession(ConfigTestSession session)
    {
        try
        {
            _logger.Info($"分析测试会话: {session.SessionId}");

            var analysis = new TestSessionAnalysis
            {
                SessionId = session.SessionId,
                TestProjectName = session.TestProject.Name,
                Operator = session.Operator,
                StartTime = session.StartTime ?? DateTime.UtcNow,
                EndTime = session.EndTime ?? DateTime.UtcNow
            };

            // 基本统计
            analysis.TotalDuts = session.DutIds.Count;
            analysis.CompletedDuts = session.DutResults.Count;
            analysis.PassedDuts = session.DutResults.Values.Count(r => r.Passed);
            analysis.FailedDuts = analysis.CompletedDuts - analysis.PassedDuts;
            analysis.DutPassRate = analysis.CompletedDuts > 0
                ? (double)analysis.PassedDuts / analysis.CompletedDuts
                : 0;

            // 步骤统计
            analysis.TotalSteps = session.DutResults.Values.Sum(r => r.StepResults.Count);
            analysis.PassedSteps = session.DutResults.Values.Sum(r => r.StepResults.Count(s => s.Passed));
            analysis.FailedSteps = session.DutResults.Values.Sum(r => r.StepResults.Count(s => !s.Passed && !s.Skipped));
            analysis.SkippedSteps = session.DutResults.Values.Sum(r => r.StepResults.Count(s => s.Skipped));
            analysis.StepPassRate = analysis.TotalSteps > 0
                ? (double)analysis.PassedSteps / analysis.TotalSteps
                : 0;

            // 时间统计
            analysis.TotalDuration = analysis.EndTime - analysis.StartTime;
            analysis.AverageDutDuration = session.DutResults.Values.Any()
                ? TimeSpan.FromSeconds(session.DutResults.Values.Average(r => (r.EndTime - r.StartTime).TotalSeconds))
                : TimeSpan.Zero;
            analysis.MinDutDuration = session.DutResults.Values.Any()
                ? TimeSpan.FromSeconds(session.DutResults.Values.Min(r => (r.EndTime - r.StartTime).TotalSeconds))
                : TimeSpan.Zero;
            analysis.MaxDutDuration = session.DutResults.Values.Any()
                ? TimeSpan.FromSeconds(session.DutResults.Values.Max(r => (r.EndTime - r.StartTime).TotalSeconds))
                : TimeSpan.Zero;

            // 重试统计
            analysis.TotalRetries = session.DutResults.Values.Sum(r => r.StepResults.Sum(s => s.RetryCount));
            analysis.AverageRetriesPerStep = analysis.TotalSteps > 0
                ? (double)analysis.TotalRetries / analysis.TotalSteps
                : 0;

            // 步骤性能分析
            analysis.StepPerformance = AnalyzeStepPerformance(session);

            // 失败原因分析
            analysis.FailureReasons = AnalyzeFailureReasons(session);

            // DUT 性能排名
            analysis.DutPerformanceRanking = AnalyzeDutPerformance(session);

            _logger.Info($"会话分析完成: {session.SessionId}");
            return analysis;
        }
        catch (Exception ex)
        {
            _logger.Error($"分析测试会话失败: {session.SessionId}", ex);
            throw;
        }
    }

    /// <summary>
    /// 分析步骤性能
    /// </summary>
    private List<StepPerformanceMetrics> AnalyzeStepPerformance(ConfigTestSession session)
    {
        var stepMetrics = new Dictionary<string, List<double>>();

        // 收集每个步骤的执行时间
        foreach (var dutResult in session.DutResults.Values)
        {
            foreach (var stepResult in dutResult.StepResults)
            {
                if (!stepMetrics.ContainsKey(stepResult.StepName))
                {
                    stepMetrics[stepResult.StepName] = new List<double>();
                }

                var duration = (stepResult.EndTime - stepResult.StartTime).TotalMilliseconds;
                stepMetrics[stepResult.StepName].Add(duration);
            }
        }

        // 计算统计指标
        var performance = new List<StepPerformanceMetrics>();

        foreach (var kvp in stepMetrics)
        {
            var durations = kvp.Value;
            var metrics = new StepPerformanceMetrics
            {
                StepName = kvp.Key,
                ExecutionCount = durations.Count,
                AverageDuration = durations.Average(),
                MinDuration = durations.Min(),
                MaxDuration = durations.Max(),
                MedianDuration = CalculateMedian(durations),
                StandardDeviation = CalculateStandardDeviation(durations)
            };

            performance.Add(metrics);
        }

        return performance.OrderByDescending(p => p.AverageDuration).ToList();
    }

    /// <summary>
    /// 分析失败原因
    /// </summary>
    private List<FailureReasonMetrics> AnalyzeFailureReasons(ConfigTestSession session)
    {
        var failureReasons = new Dictionary<string, int>();

        foreach (var dutResult in session.DutResults.Values)
        {
            foreach (var stepResult in dutResult.StepResults.Where(s => !s.Passed && !s.Skipped))
            {
                var reason = string.IsNullOrEmpty(stepResult.ErrorMessage)
                    ? "未知错误"
                    : stepResult.ErrorMessage;

                if (!failureReasons.ContainsKey(reason))
                {
                    failureReasons[reason] = 0;
                }

                failureReasons[reason]++;
            }
        }

        var totalFailures = failureReasons.Values.Sum();

        return failureReasons
            .Select(kvp => new FailureReasonMetrics
            {
                Reason = kvp.Key,
                Count = kvp.Value,
                Percentage = totalFailures > 0 ? (double)kvp.Value / totalFailures : 0
            })
            .OrderByDescending(f => f.Count)
            .ToList();
    }

    /// <summary>
    /// 分析 DUT 性能
    /// </summary>
    private List<DutPerformanceMetrics> AnalyzeDutPerformance(ConfigTestSession session)
    {
        var performance = new List<DutPerformanceMetrics>();

        foreach (var dutResult in session.DutResults)
        {
            var duration = (dutResult.Value.EndTime - dutResult.Value.StartTime).TotalSeconds;
            var passedSteps = dutResult.Value.StepResults.Count(s => s.Passed);
            var totalSteps = dutResult.Value.StepResults.Count;

            var metrics = new DutPerformanceMetrics
            {
                DutId = dutResult.Key,
                Passed = dutResult.Value.Passed,
                Duration = duration,
                PassedSteps = passedSteps,
                TotalSteps = totalSteps,
                PassRate = totalSteps > 0 ? (double)passedSteps / totalSteps : 0,
                TotalRetries = dutResult.Value.StepResults.Sum(s => s.RetryCount)
            };

            performance.Add(metrics);
        }

        return performance.OrderBy(p => p.Duration).ToList();
    }

    /// <summary>
    /// 比较两个测试会话
    /// </summary>
    public SessionComparisonResult CompareSession(
        TestSessionAnalysis baseline,
        TestSessionAnalysis current)
    {
        try
        {
            _logger.Info($"比较测试会话: {baseline.SessionId} vs {current.SessionId}");

            var comparison = new SessionComparisonResult
            {
                BaselineSessionId = baseline.SessionId,
                CurrentSessionId = current.SessionId,
                BaselineAnalysis = baseline,
                CurrentAnalysis = current
            };

            // 通过率变化
            comparison.PassRateChange = current.DutPassRate - baseline.DutPassRate;
            comparison.PassRateChangePercentage = baseline.DutPassRate > 0
                ? (comparison.PassRateChange / baseline.DutPassRate) * 100
                : 0;

            // 耗时变化
            comparison.DurationChange = current.TotalDuration - baseline.TotalDuration;
            comparison.DurationChangePercentage = baseline.TotalDuration.TotalSeconds > 0
                ? (comparison.DurationChange.TotalSeconds / baseline.TotalDuration.TotalSeconds) * 100
                : 0;

            // 重试次数变化
            comparison.RetryCountChange = current.TotalRetries - baseline.TotalRetries;
            comparison.RetryCountChangePercentage = baseline.TotalRetries > 0
                ? ((double)comparison.RetryCountChange / baseline.TotalRetries) * 100
                : 0;

            // 步骤性能变化
            comparison.StepPerformanceChanges = CompareStepPerformance(
                baseline.StepPerformance,
                current.StepPerformance);

            // 失败原因变化
            comparison.FailureReasonChanges = CompareFailureReasons(
                baseline.FailureReasons,
                current.FailureReasons);

            _logger.Info($"会话比较完成");
            return comparison;
        }
        catch (Exception ex)
        {
            _logger.Error($"比较测试会话失败", ex);
            throw;
        }
    }

    /// <summary>
    /// 比较步骤性能
    /// </summary>
    private List<StepPerformanceChange> CompareStepPerformance(
        List<StepPerformanceMetrics> baseline,
        List<StepPerformanceMetrics> current)
    {
        var changes = new List<StepPerformanceChange>();

        var baselineDict = baseline.ToDictionary(s => s.StepName);
        var currentDict = current.ToDictionary(s => s.StepName);

        foreach (var stepName in baselineDict.Keys.Union(currentDict.Keys))
        {
            var hasBaseline = baselineDict.TryGetValue(stepName, out var baselineMetrics);
            var hasCurrent = currentDict.TryGetValue(stepName, out var currentMetrics);

            if (hasBaseline && hasCurrent)
            {
                var change = new StepPerformanceChange
                {
                    StepName = stepName,
                    BaselineDuration = baselineMetrics!.AverageDuration,
                    CurrentDuration = currentMetrics!.AverageDuration,
                    DurationChange = currentMetrics.AverageDuration - baselineMetrics.AverageDuration,
                    DurationChangePercentage = baselineMetrics.AverageDuration > 0
                        ? ((currentMetrics.AverageDuration - baselineMetrics.AverageDuration) / baselineMetrics.AverageDuration) * 100
                        : 0
                };

                changes.Add(change);
            }
        }

        return changes.OrderByDescending(c => Math.Abs(c.DurationChangePercentage)).ToList();
    }

    /// <summary>
    /// 比较失败原因
    /// </summary>
    private List<FailureReasonChange> CompareFailureReasons(
        List<FailureReasonMetrics> baseline,
        List<FailureReasonMetrics> current)
    {
        var changes = new List<FailureReasonChange>();

        var baselineDict = baseline.ToDictionary(f => f.Reason);
        var currentDict = current.ToDictionary(f => f.Reason);

        foreach (var reason in baselineDict.Keys.Union(currentDict.Keys))
        {
            var hasBaseline = baselineDict.TryGetValue(reason, out var baselineMetrics);
            var hasCurrent = currentDict.TryGetValue(reason, out var currentMetrics);

            var change = new FailureReasonChange
            {
                Reason = reason,
                BaselineCount = hasBaseline ? baselineMetrics!.Count : 0,
                CurrentCount = hasCurrent ? currentMetrics!.Count : 0
            };

            change.CountChange = change.CurrentCount - change.BaselineCount;

            changes.Add(change);
        }

        return changes.OrderByDescending(c => Math.Abs(c.CountChange)).ToList();
    }

    /// <summary>
    /// 计算中位数
    /// </summary>
    private double CalculateMedian(List<double> values)
    {
        if (values.Count == 0) return 0;

        var sorted = values.OrderBy(v => v).ToList();
        var mid = sorted.Count / 2;

        if (sorted.Count % 2 == 0)
        {
            return (sorted[mid - 1] + sorted[mid]) / 2.0;
        }
        else
        {
            return sorted[mid];
        }
    }

    /// <summary>
    /// 计算标准差
    /// </summary>
    private double CalculateStandardDeviation(List<double> values)
    {
        if (values.Count == 0) return 0;

        var average = values.Average();
        var sumOfSquares = values.Sum(v => Math.Pow(v - average, 2));
        return Math.Sqrt(sumOfSquares / values.Count);
    }

    /// <summary>
    /// 生成趋势分析
    /// </summary>
    public TrendAnalysisResult AnalyzeTrend(List<TestSessionAnalysis> sessions)
    {
        try
        {
            _logger.Info($"分析测试趋势，会话数: {sessions.Count}");

            if (sessions.Count < 2)
            {
                _logger.Warning("会话数量不足，无法进行趋势分析");
                return new TrendAnalysisResult
                {
                    SessionCount = sessions.Count,
                    HasSufficientData = false
                };
            }

            var orderedSessions = sessions.OrderBy(s => s.StartTime).ToList();

            var trend = new TrendAnalysisResult
            {
                SessionCount = sessions.Count,
                HasSufficientData = true,
                StartDate = orderedSessions.First().StartTime,
                EndDate = orderedSessions.Last().EndTime
            };

            // 通过率趋势
            trend.PassRateTrend = CalculateTrend(orderedSessions.Select(s => s.DutPassRate).ToList());

            // 耗时趋势
            trend.DurationTrend = CalculateTrend(orderedSessions.Select(s => s.TotalDuration.TotalSeconds).ToList());

            // 重试次数趋势
            trend.RetryCountTrend = CalculateTrend(orderedSessions.Select(s => (double)s.TotalRetries).ToList());

            // 平均通过率
            trend.AveragePassRate = orderedSessions.Average(s => s.DutPassRate);

            // 平均耗时
            trend.AverageDuration = TimeSpan.FromSeconds(orderedSessions.Average(s => s.TotalDuration.TotalSeconds));

            // 最佳/最差会话
            trend.BestSession = orderedSessions.OrderByDescending(s => s.DutPassRate).First().SessionId;
            trend.WorstSession = orderedSessions.OrderBy(s => s.DutPassRate).First().SessionId;

            _logger.Info($"趋势分析完成");
            return trend;
        }
        catch (Exception ex)
        {
            _logger.Error($"分析测试趋势失败", ex);
            throw;
        }
    }

    /// <summary>
    /// 计算趋势（简单线性回归）
    /// </summary>
    private TrendDirection CalculateTrend(List<double> values)
    {
        if (values.Count < 2)
        {
            return TrendDirection.Stable;
        }

        // 计算斜率
        var n = values.Count;
        var sumX = 0.0;
        var sumY = 0.0;
        var sumXY = 0.0;
        var sumX2 = 0.0;

        for (int i = 0; i < n; i++)
        {
            sumX += i;
            sumY += values[i];
            sumXY += i * values[i];
            sumX2 += i * i;
        }

        var slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);

        // 根据斜率判断趋势
        if (Math.Abs(slope) < 0.01)
        {
            return TrendDirection.Stable;
        }
        else if (slope > 0)
        {
            return TrendDirection.Improving;
        }
        else
        {
            return TrendDirection.Declining;
        }
    }
}

/// <summary>
/// 测试会话分析结果
/// </summary>
public class TestSessionAnalysis
{
    public string SessionId { get; set; } = "";
    public string TestProjectName { get; set; } = "";
    public string Operator { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }

    // 基本统计
    public int TotalDuts { get; set; }
    public int CompletedDuts { get; set; }
    public int PassedDuts { get; set; }
    public int FailedDuts { get; set; }
    public double DutPassRate { get; set; }

    // 步骤统计
    public int TotalSteps { get; set; }
    public int PassedSteps { get; set; }
    public int FailedSteps { get; set; }
    public int SkippedSteps { get; set; }
    public double StepPassRate { get; set; }

    // 时间统计
    public TimeSpan TotalDuration { get; set; }
    public TimeSpan AverageDutDuration { get; set; }
    public TimeSpan MinDutDuration { get; set; }
    public TimeSpan MaxDutDuration { get; set; }

    // 重试统计
    public int TotalRetries { get; set; }
    public double AverageRetriesPerStep { get; set; }

    // 详细分析
    public List<StepPerformanceMetrics> StepPerformance { get; set; } = new();
    public List<FailureReasonMetrics> FailureReasons { get; set; } = new();
    public List<DutPerformanceMetrics> DutPerformanceRanking { get; set; } = new();
}

/// <summary>
/// 步骤性能指标
/// </summary>
public class StepPerformanceMetrics
{
    public string StepName { get; set; } = "";
    public int ExecutionCount { get; set; }
    public double AverageDuration { get; set; }
    public double MinDuration { get; set; }
    public double MaxDuration { get; set; }
    public double MedianDuration { get; set; }
    public double StandardDeviation { get; set; }
}

/// <summary>
/// 失败原因指标
/// </summary>
public class FailureReasonMetrics
{
    public string Reason { get; set; } = "";
    public int Count { get; set; }
    public double Percentage { get; set; }
}

/// <summary>
/// DUT 性能指标
/// </summary>
public class DutPerformanceMetrics
{
    public string DutId { get; set; } = "";
    public bool Passed { get; set; }
    public double Duration { get; set; }
    public int PassedSteps { get; set; }
    public int TotalSteps { get; set; }
    public double PassRate { get; set; }
    public int TotalRetries { get; set; }
}

/// <summary>
/// 会话比较结果
/// </summary>
public class SessionComparisonResult
{
    public string BaselineSessionId { get; set; } = "";
    public string CurrentSessionId { get; set; } = "";
    public TestSessionAnalysis BaselineAnalysis { get; set; } = new();
    public TestSessionAnalysis CurrentAnalysis { get; set; } = new();

    // 通过率变化
    public double PassRateChange { get; set; }
    public double PassRateChangePercentage { get; set; }

    // 耗时变化
    public TimeSpan DurationChange { get; set; }
    public double DurationChangePercentage { get; set; }

    // 重试次数变化
    public int RetryCountChange { get; set; }
    public double RetryCountChangePercentage { get; set; }

    // 详细变化
    public List<StepPerformanceChange> StepPerformanceChanges { get; set; } = new();
    public List<FailureReasonChange> FailureReasonChanges { get; set; } = new();
}

/// <summary>
/// 步骤性能变化
/// </summary>
public class StepPerformanceChange
{
    public string StepName { get; set; } = "";
    public double BaselineDuration { get; set; }
    public double CurrentDuration { get; set; }
    public double DurationChange { get; set; }
    public double DurationChangePercentage { get; set; }
}

/// <summary>
/// 失败原因变化
/// </summary>
public class FailureReasonChange
{
    public string Reason { get; set; } = "";
    public int BaselineCount { get; set; }
    public int CurrentCount { get; set; }
    public int CountChange { get; set; }
}

/// <summary>
/// 趋势分析结果
/// </summary>
public class TrendAnalysisResult
{
    public int SessionCount { get; set; }
    public bool HasSufficientData { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    // 趋势方向
    public TrendDirection PassRateTrend { get; set; }
    public TrendDirection DurationTrend { get; set; }
    public TrendDirection RetryCountTrend { get; set; }

    // 平均值
    public double AveragePassRate { get; set; }
    public TimeSpan AverageDuration { get; set; }

    // 最佳/最差
    public string BestSession { get; set; } = "";
    public string WorstSession { get; set; } = "";
}

/// <summary>
/// 趋势方向
/// </summary>
public enum TrendDirection
{
    Improving,   // 改善
    Stable,      // 稳定
    Declining    // 下降
}
