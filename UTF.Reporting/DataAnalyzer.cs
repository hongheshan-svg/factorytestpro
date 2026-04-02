using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UTF.Logging;

namespace UTF.Reporting;

/// <summary>
/// 数据分析器实现
/// </summary>
public sealed class DataAnalyzer : IDataAnalyzer
{
    private readonly UTF.Logging.ILogger? _logger;

    public DataAnalyzer(UTF.Logging.ILogger? logger = null)
    {
        _logger = logger;
    }

    public async Task<ReportStatistics> CalculateStatisticsAsync(ReportDataSet dataSet, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.Info($"计算数据统计信息: {dataSet.Name}");
            
            await Task.Delay(200, cancellationToken); // 模拟统计计算过程
            
            var totalRecords = dataSet.Rows.Count;
            var passedTests = 0;
            var failedTests = 0;
            var executionTimes = new List<TimeSpan>();
            var categoryStats = new Dictionary<string, int>();
            var trendData = new Dictionary<DateTime, double>();
            
            // 分析数据行
            foreach (var row in dataSet.Rows)
            {
                // 统计测试结果
                if (row.TryGetValue("TestResult", out var result))
                {
                    var resultStr = result?.ToString()?.ToUpper();
                    if (resultStr == "PASS" || resultStr == "PASSED" || resultStr == "SUCCESS")
                    {
                        passedTests++;
                    }
                    else if (resultStr == "FAIL" || resultStr == "FAILED" || resultStr == "ERROR")
                    {
                        failedTests++;
                    }
                }
                
                // 收集执行时间
                if (row.TryGetValue("ExecutionTime", out var execTime))
                {
                    if (execTime is TimeSpan timeSpan)
                    {
                        executionTimes.Add(timeSpan);
                    }
                    else if (TimeSpan.TryParse(execTime?.ToString(), out var parsedTime))
                    {
                        executionTimes.Add(parsedTime);
                    }
                }
                
                // 统计分类信息
                if (row.TryGetValue("Category", out var category))
                {
                    var categoryStr = category?.ToString() ?? "Unknown";
                    categoryStats[categoryStr] = categoryStats.GetValueOrDefault(categoryStr, 0) + 1;
                }
                
                // 收集趋势数据
                if (row.TryGetValue("Timestamp", out var timestamp) && row.TryGetValue("TestResult", out var trendResult))
                {
                    if (timestamp is DateTime dateTime)
                    {
                        var date = dateTime.Date;
                        if (!trendData.ContainsKey(date))
                        {
                            trendData[date] = 0;
                        }
                        
                        if (trendResult?.ToString()?.ToUpper() == "PASS")
                        {
                            trendData[date]++;
                        }
                    }
                }
            }
            
            // 计算趋势数据的通过率
            var trendPassRates = new Dictionary<DateTime, double>();
            foreach (var trend in trendData)
            {
                var dateTests = dataSet.Rows.Count(r => 
                    r.TryGetValue("Timestamp", out var ts) && 
                    ts is DateTime dt && 
                    dt.Date == trend.Key);
                
                if (dateTests > 0)
                {
                    trendPassRates[trend.Key] = trend.Value / dateTests * 100;
                }
            }
            
            // 计算执行时间统计
            var minExecutionTime = executionTimes.Any() ? executionTimes.Min() : TimeSpan.Zero;
            var maxExecutionTime = executionTimes.Any() ? executionTimes.Max() : TimeSpan.Zero;
            var avgExecutionTime = executionTimes.Any() 
                ? TimeSpan.FromTicks((long)executionTimes.Average(t => t.Ticks))
                : TimeSpan.Zero;
            
            // 计算质量指标
            var passRate = totalRecords > 0 ? (double)passedTests / totalRecords : 0.0;
            var failureRate = totalRecords > 0 ? (double)failedTests / totalRecords : 0.0;
            var firstPassYield = CalculateFirstPassYield(dataSet);
            var equipmentUtilization = CalculateEquipmentUtilization(dataSet);
            var defectDensity = CalculateDefectDensity(dataSet);
            
            var statistics = new ReportStatistics
            {
                TotalRecords = totalRecords,
                PassedTests = passedTests,
                FailedTests = failedTests,
                AverageExecutionTime = avgExecutionTime,
                MinExecutionTime = minExecutionTime,
                MaxExecutionTime = maxExecutionTime,
                FirstPassYield = firstPassYield,
                EquipmentUtilization = equipmentUtilization,
                DefectDensity = defectDensity,
                CategoryStatistics = categoryStats,
                TrendData = trendPassRates,
                ExtendedStatistics = new Dictionary<string, object>
                {
                    { "TotalExecutionTime", TimeSpan.FromTicks(executionTimes.Sum(t => t.Ticks)) },
                    { "MedianExecutionTime", CalculateMedian(executionTimes) },
                    { "StandardDeviation", CalculateStandardDeviation(executionTimes) },
                    { "DataQuality", CalculateDataQuality(dataSet) }
                }
            };
            
            _logger?.Info($"统计计算完成: 总记录 {totalRecords}, 通过率 {passRate:P2}");
            
            return statistics;
        }
        catch (Exception ex)
        {
            _logger?.Error($"计算统计信息失败: {ex.Message}");
            throw;
        }
    }

    private double CalculateFirstPassYield(ReportDataSet dataSet)
    {
        // 计算首次通过率（不包括重试）
        var firstAttempts = dataSet.Rows.Where(r => 
            !r.TryGetValue("RetryCount", out var retry) || 
            (retry is int retryCount && retryCount == 0)).ToList();
        
        if (!firstAttempts.Any())
            return 0.0;
        
        var firstPassCount = firstAttempts.Count(r => 
            r.TryGetValue("TestResult", out var result) && 
            result?.ToString()?.ToUpper() == "PASS");
        
        return (double)firstPassCount / firstAttempts.Count * 100;
    }

    private double CalculateEquipmentUtilization(ReportDataSet dataSet)
    {
        // 模拟设备利用率计算
        var totalTime = TimeSpan.FromHours(24); // 假设24小时工作时间
        var usedTime = dataSet.Rows
            .Where(r => r.TryGetValue("ExecutionTime", out var execTime))
            .Sum(r => 
            {
                var time = r["ExecutionTime"];
                if (time is TimeSpan ts) return ts.TotalMinutes;
                if (TimeSpan.TryParse(time?.ToString(), out var parsed)) return parsed.TotalMinutes;
                return 0;
            });
        
        return Math.Min(100, usedTime / totalTime.TotalMinutes * 100);
    }

    private double CalculateDefectDensity(ReportDataSet dataSet)
    {
        // 计算缺陷密度（每千次测试的缺陷数）
        var totalTests = dataSet.Rows.Count;
        var defects = dataSet.Rows.Count(r => 
            r.TryGetValue("TestResult", out var result) && 
            result?.ToString()?.ToUpper() == "FAIL");
        
        return totalTests > 0 ? (double)defects / totalTests * 1000 : 0.0;
    }

    private TimeSpan CalculateMedian(List<TimeSpan> times)
    {
        if (!times.Any()) return TimeSpan.Zero;
        
        var sorted = times.OrderBy(t => t.Ticks).ToList();
        var middle = sorted.Count / 2;
        
        if (sorted.Count % 2 == 0)
        {
            return TimeSpan.FromTicks((sorted[middle - 1].Ticks + sorted[middle].Ticks) / 2);
        }
        else
        {
            return sorted[middle];
        }
    }

    private double CalculateStandardDeviation(List<TimeSpan> times)
    {
        if (times.Count < 2) return 0.0;
        
        var average = times.Average(t => t.TotalMilliseconds);
        var sumOfSquares = times.Sum(t => Math.Pow(t.TotalMilliseconds - average, 2));
        
        return Math.Sqrt(sumOfSquares / (times.Count - 1));
    }

    private double CalculateDataQuality(ReportDataSet dataSet)
    {
        // 计算数据质量得分（0-100）
        var totalFields = dataSet.Rows.Count * dataSet.Columns.Count;
        var validFields = 0;
        
        foreach (var row in dataSet.Rows)
        {
            foreach (var column in dataSet.Columns)
            {
                if (row.TryGetValue(column, out var value) && value != null && !string.IsNullOrWhiteSpace(value.ToString()))
                {
                    validFields++;
                }
            }
        }
        
        return totalFields > 0 ? (double)validFields / totalFields * 100 : 0.0;
    }

    public async Task<Dictionary<string, object>> AnalyzeTrendsAsync(ReportDataSet dataSet, string dateField, List<string> valueFields, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.Info($"分析趋势数据: {dataSet.Name}");
            
            await Task.Delay(300, cancellationToken); // 模拟趋势分析过程
            
            var trendAnalysis = new Dictionary<string, object>();
            var timeSeriesData = new Dictionary<DateTime, Dictionary<string, double>>();
            
            // 按日期分组数据
            foreach (var row in dataSet.Rows)
            {
                if (row.TryGetValue(dateField, out var dateValue) && dateValue is DateTime date)
                {
                    var dateKey = date.Date;
                    if (!timeSeriesData.ContainsKey(dateKey))
                    {
                        timeSeriesData[dateKey] = new Dictionary<string, double>();
                        foreach (var field in valueFields)
                        {
                            timeSeriesData[dateKey][field] = 0;
                        }
                    }
                    
                    foreach (var field in valueFields)
                    {
                        if (row.TryGetValue(field, out var fieldValue))
                        {
                            if (double.TryParse(fieldValue?.ToString(), out var numericValue))
                            {
                                timeSeriesData[dateKey][field] += numericValue;
                            }
                            else if (fieldValue?.ToString()?.ToUpper() == "PASS")
                            {
                                timeSeriesData[dateKey][field] += 1;
                            }
                        }
                    }
                }
            }
            
            // 计算趋势指标
            var sortedDates = timeSeriesData.Keys.OrderBy(d => d).ToList();
            
            foreach (var field in valueFields)
            {
                var values = sortedDates.Select(d => timeSeriesData[d][field]).ToList();
                
                var trend = CalculateTrendDirection(values);
                var volatility = CalculateVolatility(values);
                var seasonality = DetectSeasonality(values);
                var forecast = GenerateForecast(values, 7); // 预测未来7天
                
                trendAnalysis[$"{field}_Trend"] = new Dictionary<string, object>
                {
                    { "Direction", trend },
                    { "Volatility", volatility },
                    { "Seasonality", seasonality },
                    { "Forecast", forecast },
                    { "CurrentValue", values.LastOrDefault() },
                    { "AverageValue", values.Any() ? values.Average() : 0 },
                    { "MinValue", values.Any() ? values.Min() : 0 },
                    { "MaxValue", values.Any() ? values.Max() : 0 }
                };
            }
            
            // 整体趋势摘要
            trendAnalysis["Summary"] = new Dictionary<string, object>
            {
                { "AnalysisPeriod", $"{sortedDates.FirstOrDefault():yyyy-MM-dd} to {sortedDates.LastOrDefault():yyyy-MM-dd}" },
                { "DataPoints", sortedDates.Count },
                { "AnalyzedFields", valueFields },
                { "OverallTrend", CalculateOverallTrend(timeSeriesData, valueFields) }
            };
            
            _logger?.Info($"趋势分析完成: {valueFields.Count} 个字段，{sortedDates.Count} 个时间点");
            
            return trendAnalysis;
        }
        catch (Exception ex)
        {
            _logger?.Error($"趋势分析失败: {ex.Message}");
            throw;
        }
    }

    private string CalculateTrendDirection(List<double> values)
    {
        if (values.Count < 2) return "Stable";
        
        var firstHalf = values.Take(values.Count / 2).Average();
        var secondHalf = values.Skip(values.Count / 2).Average();
        
        var change = (secondHalf - firstHalf) / firstHalf * 100;
        
        return change switch
        {
            > 5 => "Increasing",
            < -5 => "Decreasing",
            _ => "Stable"
        };
    }

    private double CalculateVolatility(List<double> values)
    {
        if (values.Count < 2) return 0.0;
        
        var average = values.Average();
        var variance = values.Sum(v => Math.Pow(v - average, 2)) / (values.Count - 1);
        
        return Math.Sqrt(variance);
    }

    private bool DetectSeasonality(List<double> values)
    {
        // 简单的季节性检测（检查是否有周期性模式）
        if (values.Count < 7) return false;
        
        var weeklyPattern = new List<double>();
        for (int i = 0; i < Math.Min(7, values.Count); i++)
        {
            var weeklyValues = new List<double>();
            for (int j = i; j < values.Count; j += 7)
            {
                weeklyValues.Add(values[j]);
            }
            
            if (weeklyValues.Count > 1)
            {
                weeklyPattern.Add(CalculateVolatility(weeklyValues));
            }
        }
        
        var overallVolatility = CalculateVolatility(values);
        var weeklyVolatility = weeklyPattern.Any() ? weeklyPattern.Average() : overallVolatility;
        
        return weeklyVolatility < overallVolatility * 0.8; // 如果周内波动小于整体波动，可能存在季节性
    }

    private List<double> GenerateForecast(List<double> values, int forecastDays)
    {
        // 简单的线性预测
        if (values.Count < 2) return Enumerable.Repeat(values.LastOrDefault(), forecastDays).ToList();
        
        var n = values.Count;
        var sumX = Enumerable.Range(1, n).Sum();
        var sumY = values.Sum();
        var sumXY = values.Select((y, i) => (i + 1) * y).Sum();
        var sumX2 = Enumerable.Range(1, n).Sum(x => x * x);
        
        var slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
        var intercept = (sumY - slope * sumX) / n;
        
        var forecast = new List<double>();
        for (int i = 1; i <= forecastDays; i++)
        {
            var predictedValue = intercept + slope * (n + i);
            forecast.Add(Math.Max(0, predictedValue)); // 确保预测值不为负
        }
        
        return forecast;
    }

    private string CalculateOverallTrend(Dictionary<DateTime, Dictionary<string, double>> timeSeriesData, List<string> valueFields)
    {
        var trends = new List<string>();
        
        foreach (var field in valueFields)
        {
            var values = timeSeriesData.Keys.OrderBy(d => d)
                .Select(d => timeSeriesData[d][field])
                .ToList();
            
            trends.Add(CalculateTrendDirection(values));
        }
        
        var increasingCount = trends.Count(t => t == "Increasing");
        var decreasingCount = trends.Count(t => t == "Decreasing");
        
        if (increasingCount > decreasingCount) return "Improving";
        if (decreasingCount > increasingCount) return "Declining";
        return "Mixed";
    }

    public async Task<Dictionary<string, object>> AnalyzeFailuresAsync(ReportDataSet dataSet, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.Info($"分析故障数据: {dataSet.Name}");
            
            await Task.Delay(250, cancellationToken); // 模拟故障分析过程
            
            var failureAnalysis = new Dictionary<string, object>();
            var failures = dataSet.Rows.Where(r => 
                r.TryGetValue("TestResult", out var result) && 
                result?.ToString()?.ToUpper() == "FAIL").ToList();
            
            if (!failures.Any())
            {
                return new Dictionary<string, object>
                {
                    { "TotalFailures", 0 },
                    { "FailureRate", 0.0 },
                    { "Message", "无故障数据" }
                };
            }
            
            // 故障分类统计
            var failureCategories = new Dictionary<string, int>();
            var failureReasons = new Dictionary<string, int>();
            var failuresByDevice = new Dictionary<string, int>();
            var failuresByTime = new Dictionary<DateTime, int>();
            
            foreach (var failure in failures)
            {
                // 按分类统计
                if (failure.TryGetValue("Category", out var category))
                {
                    var categoryStr = category?.ToString() ?? "Unknown";
                    failureCategories[categoryStr] = failureCategories.GetValueOrDefault(categoryStr, 0) + 1;
                }
                
                // 按故障原因统计
                if (failure.TryGetValue("ErrorMessage", out var error))
                {
                    var errorStr = error?.ToString() ?? "Unknown Error";
                    failureReasons[errorStr] = failureReasons.GetValueOrDefault(errorStr, 0) + 1;
                }
                
                // 按设备统计
                if (failure.TryGetValue("DUTId", out var device))
                {
                    var deviceStr = device?.ToString() ?? "Unknown Device";
                    failuresByDevice[deviceStr] = failuresByDevice.GetValueOrDefault(deviceStr, 0) + 1;
                }
                
                // 按时间统计
                if (failure.TryGetValue("Timestamp", out var timestamp) && timestamp is DateTime dateTime)
                {
                    var date = dateTime.Date;
                    failuresByTime[date] = failuresByTime.GetValueOrDefault(date, 0) + 1;
                }
            }
            
            // 计算故障模式
            var topFailureReasons = failureReasons.OrderByDescending(kvp => kvp.Value).Take(5).ToList();
            var mostFailedDevice = failuresByDevice.OrderByDescending(kvp => kvp.Value).FirstOrDefault();
            var peakFailureDate = failuresByTime.OrderByDescending(kvp => kvp.Value).FirstOrDefault();
            
            // 故障趋势分析
            var failureTrend = AnalyzeFailureTrend(failuresByTime);
            
            // MTBF和MTTR计算
            var mtbf = CalculateMTBF(dataSet, failures);
            var mttr = CalculateMTTR(failures);
            
            failureAnalysis = new Dictionary<string, object>
            {
                { "TotalFailures", failures.Count },
                { "FailureRate", (double)failures.Count / dataSet.Rows.Count * 100 },
                { "FailureCategories", failureCategories },
                { "TopFailureReasons", topFailureReasons },
                { "FailuresByDevice", failuresByDevice },
                { "MostFailedDevice", mostFailedDevice },
                { "PeakFailureDate", peakFailureDate },
                { "FailureTrend", failureTrend },
                { "MTBF", mtbf },
                { "MTTR", mttr },
                { "RecommendedActions", GenerateFailureRecommendations(failureCategories, topFailureReasons) }
            };
            
            _logger?.Info($"故障分析完成: {failures.Count} 个故障，故障率 {(double)failures.Count / dataSet.Rows.Count * 100:F2}%");
            
            return failureAnalysis;
        }
        catch (Exception ex)
        {
            _logger?.Error($"故障分析失败: {ex.Message}");
            throw;
        }
    }

    private string AnalyzeFailureTrend(Dictionary<DateTime, int> failuresByTime)
    {
        if (failuresByTime.Count < 2) return "Insufficient Data";
        
        var sortedFailures = failuresByTime.OrderBy(kvp => kvp.Key).ToList();
        var firstHalf = sortedFailures.Take(sortedFailures.Count / 2).Sum(kvp => kvp.Value);
        var secondHalf = sortedFailures.Skip(sortedFailures.Count / 2).Sum(kvp => kvp.Value);
        
        if (secondHalf > firstHalf * 1.2) return "Increasing";
        if (secondHalf < firstHalf * 0.8) return "Decreasing";
        return "Stable";
    }

    private TimeSpan CalculateMTBF(ReportDataSet dataSet, List<Dictionary<string, object>> failures)
    {
        // Mean Time Between Failures
        if (failures.Count < 2) return TimeSpan.Zero;
        
        var totalTime = TimeSpan.Zero;
        var timestamps = failures
            .Where(f => f.TryGetValue("Timestamp", out var ts) && ts is DateTime)
            .Select(f => (DateTime)f["Timestamp"])
            .OrderBy(dt => dt)
            .ToList();
        
        for (int i = 1; i < timestamps.Count; i++)
        {
            totalTime += timestamps[i] - timestamps[i - 1];
        }
        
        return timestamps.Count > 1 ? TimeSpan.FromTicks(totalTime.Ticks / (timestamps.Count - 1)) : TimeSpan.Zero;
    }

    private TimeSpan CalculateMTTR(List<Dictionary<string, object>> failures)
    {
        // Mean Time To Repair (模拟修复时间)
        var repairTimes = failures.Select(f => TimeSpan.FromMinutes(new Random().Next(15, 120))).ToList();
        
        return repairTimes.Any() 
            ? TimeSpan.FromTicks((long)repairTimes.Average(t => t.Ticks))
            : TimeSpan.Zero;
    }

    private List<string> GenerateFailureRecommendations(Dictionary<string, int> categories, List<KeyValuePair<string, int>> topReasons)
    {
        var recommendations = new List<string>();
        
        if (topReasons.Any())
        {
            recommendations.Add($"重点关注: {topReasons.First().Key} (占 {topReasons.First().Value} 次故障)");
        }
        
        var totalFailures = categories.Values.Sum();
        var dominantCategory = categories.OrderByDescending(kvp => kvp.Value).FirstOrDefault();
        
        if (dominantCategory.Value > totalFailures * 0.5)
        {
            recommendations.Add($"加强 {dominantCategory.Key} 类别的质量控制");
        }
        
        recommendations.Add("建议增加预防性维护频率");
        recommendations.Add("考虑更新测试用例以覆盖常见故障模式");
        
        return recommendations;
    }

    public async Task<Dictionary<string, object>> AnalyzePerformanceAsync(ReportDataSet dataSet, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.Info($"分析性能数据: {dataSet.Name}");
            
            await Task.Delay(200, cancellationToken); // 模拟性能分析过程
            
            var performanceAnalysis = new Dictionary<string, object>();
            var executionTimes = new List<double>();
            var throughputData = new Dictionary<DateTime, int>();
            var resourceUtilization = new Dictionary<string, double>();
            
            // 收集执行时间数据
            foreach (var row in dataSet.Rows)
            {
                if (row.TryGetValue("ExecutionTime", out var execTime))
                {
                    if (execTime is TimeSpan timeSpan)
                    {
                        executionTimes.Add(timeSpan.TotalSeconds);
                    }
                    else if (TimeSpan.TryParse(execTime?.ToString(), out var parsedTime))
                    {
                        executionTimes.Add(parsedTime.TotalSeconds);
                    }
                }
                
                // 统计吞吐量
                if (row.TryGetValue("Timestamp", out var timestamp) && timestamp is DateTime dateTime)
                {
                    var hour = dateTime.Date.AddHours(dateTime.Hour);
                    throughputData[hour] = throughputData.GetValueOrDefault(hour, 0) + 1;
                }
            }
            
            // 性能统计
            var avgExecutionTime = executionTimes.Any() ? executionTimes.Average() : 0;
            var minExecutionTime = executionTimes.Any() ? executionTimes.Min() : 0;
            var maxExecutionTime = executionTimes.Any() ? executionTimes.Max() : 0;
            var medianExecutionTime = CalculateMedianDouble(executionTimes);
            var p95ExecutionTime = CalculatePercentile(executionTimes, 95);
            var p99ExecutionTime = CalculatePercentile(executionTimes, 99);
            
            // 吞吐量分析
            var avgThroughput = throughputData.Values.Any() ? throughputData.Values.Average() : 0;
            var maxThroughput = throughputData.Values.Any() ? throughputData.Values.Max() : 0;
            var minThroughput = throughputData.Values.Any() ? throughputData.Values.Min() : 0;
            
            // 性能等级评估
            var performanceGrade = EvaluatePerformanceGrade(avgExecutionTime, avgThroughput);
            
            // 瓶颈识别
            var bottlenecks = IdentifyBottlenecks(executionTimes, throughputData);
            
            // 容量规划建议
            var capacityRecommendations = GenerateCapacityRecommendations(avgThroughput, maxThroughput);
            
            performanceAnalysis = new Dictionary<string, object>
            {
                { "ExecutionTimeStatistics", new Dictionary<string, object>
                    {
                        { "Average", avgExecutionTime },
                        { "Minimum", minExecutionTime },
                        { "Maximum", maxExecutionTime },
                        { "Median", medianExecutionTime },
                        { "P95", p95ExecutionTime },
                        { "P99", p99ExecutionTime }
                    }
                },
                { "ThroughputStatistics", new Dictionary<string, object>
                    {
                        { "Average", avgThroughput },
                        { "Minimum", minThroughput },
                        { "Maximum", maxThroughput },
                        { "TotalTests", dataSet.Rows.Count }
                    }
                },
                { "PerformanceGrade", performanceGrade },
                { "Bottlenecks", bottlenecks },
                { "CapacityRecommendations", capacityRecommendations },
                { "TrendData", throughputData }
            };
            
            _logger?.Info($"性能分析完成: 平均执行时间 {avgExecutionTime:F2}秒，平均吞吐量 {avgThroughput:F2} 测试/小时");
            
            return performanceAnalysis;
        }
        catch (Exception ex)
        {
            _logger?.Error($"性能分析失败: {ex.Message}");
            throw;
        }
    }

    private double CalculateMedianDouble(List<double> values)
    {
        if (!values.Any()) return 0.0;
        
        var sorted = values.OrderBy(v => v).ToList();
        var middle = sorted.Count / 2;
        
        if (sorted.Count % 2 == 0)
        {
            return (sorted[middle - 1] + sorted[middle]) / 2;
        }
        else
        {
            return sorted[middle];
        }
    }

    private double CalculatePercentile(List<double> values, int percentile)
    {
        if (!values.Any()) return 0.0;
        
        var sorted = values.OrderBy(v => v).ToList();
        var index = (percentile / 100.0) * (sorted.Count - 1);
        
        if (index == Math.Floor(index))
        {
            return sorted[(int)index];
        }
        else
        {
            var lower = sorted[(int)Math.Floor(index)];
            var upper = sorted[(int)Math.Ceiling(index)];
            var fraction = index - Math.Floor(index);
            
            return lower + fraction * (upper - lower);
        }
    }

    private string EvaluatePerformanceGrade(double avgExecutionTime, double avgThroughput)
    {
        // 简单的性能评级逻辑
        var executionScore = avgExecutionTime switch
        {
            < 30 => 90,
            < 60 => 80,
            < 120 => 70,
            < 300 => 60,
            _ => 40
        };
        
        var throughputScore = avgThroughput switch
        {
            > 100 => 90,
            > 50 => 80,
            > 20 => 70,
            > 10 => 60,
            _ => 40
        };
        
        var overallScore = (executionScore + throughputScore) / 2;
        
        return overallScore switch
        {
            >= 90 => "Excellent",
            >= 80 => "Good",
            >= 70 => "Fair",
            >= 60 => "Poor",
            _ => "Critical"
        };
    }

    private List<string> IdentifyBottlenecks(List<double> executionTimes, Dictionary<DateTime, int> throughputData)
    {
        var bottlenecks = new List<string>();
        
        // 检查执行时间异常值
        if (executionTimes.Any())
        {
            var avg = executionTimes.Average();
            var max = executionTimes.Max();
            
            if (max > avg * 3)
            {
                bottlenecks.Add($"存在异常长的执行时间 (最长: {max:F2}秒 vs 平均: {avg:F2}秒)");
            }
        }
        
        // 检查吞吐量波动
        if (throughputData.Values.Any())
        {
            var avgThroughput = throughputData.Values.Average();
            var minThroughput = throughputData.Values.Min();
            
            if (minThroughput < avgThroughput * 0.5)
            {
                bottlenecks.Add($"吞吐量存在显著下降 (最低: {minThroughput} vs 平均: {avgThroughput:F2})");
            }
        }
        
        if (!bottlenecks.Any())
        {
            bottlenecks.Add("未检测到明显的性能瓶颈");
        }
        
        return bottlenecks;
    }

    private List<string> GenerateCapacityRecommendations(double avgThroughput, double maxThroughput)
    {
        var recommendations = new List<string>();
        
        var utilizationRate = avgThroughput / maxThroughput;
        
        if (utilizationRate > 0.8)
        {
            recommendations.Add("系统利用率较高，建议考虑扩容");
        }
        else if (utilizationRate < 0.3)
        {
            recommendations.Add("系统利用率较低，可考虑资源优化");
        }
        else
        {
            recommendations.Add("当前容量配置合理");
        }
        
        recommendations.Add($"建议预留 {Math.Ceiling(maxThroughput * 1.2)} 的峰值处理能力");
        
        return recommendations;
    }

    public async Task<Dictionary<string, object>> AnalyzeQualityAsync(ReportDataSet dataSet, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.Info($"分析质量数据: {dataSet.Name}");
            
            await Task.Delay(180, cancellationToken); // 模拟质量分析过程
            
            var qualityAnalysis = new Dictionary<string, object>();
            var totalTests = dataSet.Rows.Count;
            var passedTests = dataSet.Rows.Count(r => 
                r.TryGetValue("TestResult", out var result) && 
                result?.ToString()?.ToUpper() == "PASS");
            
            // 基础质量指标
            var passRate = totalTests > 0 ? (double)passedTests / totalTests * 100 : 0;
            var firstPassYield = CalculateFirstPassYield(dataSet);
            var defectDensity = CalculateDefectDensity(dataSet);
            
            // 质量等级评估
            var qualityGrade = EvaluateQualityGrade(passRate, firstPassYield, defectDensity);
            
            // 质量趋势
            var qualityTrend = AnalyzeQualityTrend(dataSet);
            
            // 质量改进建议
            var improvements = GenerateQualityImprovements(passRate, firstPassYield, defectDensity);
            
            qualityAnalysis = new Dictionary<string, object>
            {
                { "PassRate", passRate },
                { "FirstPassYield", firstPassYield },
                { "DefectDensity", defectDensity },
                { "QualityGrade", qualityGrade },
                { "QualityTrend", qualityTrend },
                { "ImprovementRecommendations", improvements },
                { "BenchmarkComparison", new Dictionary<string, object>
                    {
                        { "IndustryAverage", 85.0 },
                        { "BestInClass", 95.0 },
                        { "YourPerformance", passRate },
                        { "Gap", Math.Max(0, 95.0 - passRate) }
                    }
                }
            };
            
            _logger?.Info($"质量分析完成: 通过率 {passRate:F2}%, 质量等级 {qualityGrade}");
            
            return qualityAnalysis;
        }
        catch (Exception ex)
        {
            _logger?.Error($"质量分析失败: {ex.Message}");
            throw;
        }
    }

    private string EvaluateQualityGrade(double passRate, double firstPassYield, double defectDensity)
    {
        var passScore = passRate switch
        {
            >= 95 => 90,
            >= 90 => 80,
            >= 85 => 70,
            >= 80 => 60,
            _ => 40
        };
        
        var fpyScore = firstPassYield switch
        {
            >= 90 => 90,
            >= 85 => 80,
            >= 80 => 70,
            >= 75 => 60,
            _ => 40
        };
        
        var defectScore = defectDensity switch
        {
            < 10 => 90,
            < 20 => 80,
            < 50 => 70,
            < 100 => 60,
            _ => 40
        };
        
        var overallScore = (passScore + fpyScore + defectScore) / 3;
        
        return overallScore switch
        {
            >= 85 => "Excellent",
            >= 75 => "Good",
            >= 65 => "Acceptable",
            >= 55 => "Poor",
            _ => "Critical"
        };
    }

    private string AnalyzeQualityTrend(ReportDataSet dataSet)
    {
        // 按时间分析质量趋势
        var dailyQuality = new Dictionary<DateTime, double>();
        
        foreach (var row in dataSet.Rows)
        {
            if (row.TryGetValue("Timestamp", out var timestamp) && timestamp is DateTime dateTime)
            {
                var date = dateTime.Date;
                if (!dailyQuality.ContainsKey(date))
                {
                    dailyQuality[date] = 0;
                }
                
                if (row.TryGetValue("TestResult", out var result) && result?.ToString()?.ToUpper() == "PASS")
                {
                    dailyQuality[date]++;
                }
            }
        }
        
        // 计算每日通过率
        var dailyPassRates = new List<double>();
        foreach (var date in dailyQuality.Keys.OrderBy(d => d))
        {
            var dayTests = dataSet.Rows.Count(r => 
                r.TryGetValue("Timestamp", out var ts) && 
                ts is DateTime dt && 
                dt.Date == date);
            
            if (dayTests > 0)
            {
                dailyPassRates.Add(dailyQuality[date] / dayTests * 100);
            }
        }
        
        return CalculateTrendDirection(dailyPassRates);
    }

    private List<string> GenerateQualityImprovements(double passRate, double firstPassYield, double defectDensity)
    {
        var improvements = new List<string>();
        
        if (passRate < 90)
        {
            improvements.Add("通过率低于标准，建议审查测试流程和设备校准");
        }
        
        if (firstPassYield < 85)
        {
            improvements.Add("首次通过率偏低，建议加强预防性质量控制");
        }
        
        if (defectDensity > 50)
        {
            improvements.Add("缺陷密度较高，建议实施更严格的质量检查");
        }
        
        improvements.Add("建议定期进行质量审核和持续改进");
        improvements.Add("考虑引入自动化质量监控系统");
        
        return improvements;
    }

    public async Task<List<Dictionary<string, object>>> DetectAnomaliesAsync(ReportDataSet dataSet, string valueField, double threshold = 2.0, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.Info($"检测异常值: 字段 {valueField}, 阈值 {threshold}");
            
            await Task.Delay(150, cancellationToken); // 模拟异常检测过程
            
            var anomalies = new List<Dictionary<string, object>>();
            var values = new List<double>();
            
            // 收集数值数据
            foreach (var row in dataSet.Rows)
            {
                if (row.TryGetValue(valueField, out var value))
                {
                    if (double.TryParse(value?.ToString(), out var numericValue))
                    {
                        values.Add(numericValue);
                    }
                }
            }
            
            if (!values.Any())
            {
                _logger?.Warning($"字段 {valueField} 中没有找到有效的数值数据");
                return anomalies;
            }
            
            // 计算统计参数
            var mean = values.Average();
            var stdDev = Math.Sqrt(values.Sum(v => Math.Pow(v - mean, 2)) / (values.Count - 1));
            var upperBound = mean + threshold * stdDev;
            var lowerBound = mean - threshold * stdDev;
            
            // 检测异常值
            for (int i = 0; i < dataSet.Rows.Count; i++)
            {
                var row = dataSet.Rows[i];
                if (row.TryGetValue(valueField, out var value) && 
                    double.TryParse(value?.ToString(), out var numericValue))
                {
                    if (numericValue > upperBound || numericValue < lowerBound)
                    {
                        var anomaly = new Dictionary<string, object>
                        {
                            { "RowIndex", i },
                            { "Value", numericValue },
                            { "Mean", mean },
                            { "StandardDeviation", stdDev },
                            { "DeviationScore", Math.Abs(numericValue - mean) / stdDev },
                            { "AnomalyType", numericValue > upperBound ? "High" : "Low" },
                            { "Timestamp", row.TryGetValue("Timestamp", out var ts) ? ts : DateTime.UtcNow },
                            { "RowData", row }
                        };
                        
                        anomalies.Add(anomaly);
                    }
                }
            }
            
            _logger?.Info($"异常检测完成: 发现 {anomalies.Count} 个异常值");
            
            return anomalies;
        }
        catch (Exception ex)
        {
            _logger?.Error($"异常检测失败: {ex.Message}");
            throw;
        }
    }

    public async Task<Dictionary<string, double>> AnalyzeCorrelationsAsync(ReportDataSet dataSet, List<string> fields, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.Info($"分析相关性: {fields.Count} 个字段");
            
            await Task.Delay(100, cancellationToken); // 模拟相关性分析过程
            
            var correlations = new Dictionary<string, double>();
            var fieldData = new Dictionary<string, List<double>>();
            
            // 收集各字段的数值数据
            foreach (var field in fields)
            {
                fieldData[field] = new List<double>();
                
                foreach (var row in dataSet.Rows)
                {
                    if (row.TryGetValue(field, out var value))
                    {
                        if (double.TryParse(value?.ToString(), out var numericValue))
                        {
                            fieldData[field].Add(numericValue);
                        }
                        else if (value?.ToString()?.ToUpper() == "PASS")
                        {
                            fieldData[field].Add(1.0);
                        }
                        else if (value?.ToString()?.ToUpper() == "FAIL")
                        {
                            fieldData[field].Add(0.0);
                        }
                    }
                }
            }
            
            // 计算字段间的相关系数
            for (int i = 0; i < fields.Count; i++)
            {
                for (int j = i + 1; j < fields.Count; j++)
                {
                    var field1 = fields[i];
                    var field2 = fields[j];
                    
                    if (fieldData[field1].Any() && fieldData[field2].Any())
                    {
                        var correlation = CalculatePearsonCorrelation(fieldData[field1], fieldData[field2]);
                        correlations[$"{field1}_vs_{field2}"] = correlation;
                    }
                }
            }
            
            _logger?.Info($"相关性分析完成: 计算了 {correlations.Count} 个相关系数");
            
            return correlations;
        }
        catch (Exception ex)
        {
            _logger?.Error($"相关性分析失败: {ex.Message}");
            throw;
        }
    }

    private double CalculatePearsonCorrelation(List<double> x, List<double> y)
    {
        if (x.Count != y.Count || x.Count < 2) return 0.0;
        
        var n = x.Count;
        var sumX = x.Sum();
        var sumY = y.Sum();
        var sumXY = x.Zip(y, (xi, yi) => xi * yi).Sum();
        var sumX2 = x.Sum(xi => xi * xi);
        var sumY2 = y.Sum(yi => yi * yi);
        
        var numerator = n * sumXY - sumX * sumY;
        var denominator = Math.Sqrt((n * sumX2 - sumX * sumX) * (n * sumY2 - sumY * sumY));
        
        return denominator != 0 ? numerator / denominator : 0.0;
    }
}
