using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UTF.Core;
using UTF.Logging;

namespace UTF.Reporting;

/// <summary>
/// 将 ConfigDrivenTestReport 映射为 ReportDataSet，驱动 ReportGenerator 生成真实报告
/// </summary>
public sealed class ConfigDrivenReportBridge
{
    private readonly ReportGenerator _reportGenerator;
    private readonly ILogger? _logger;

    public ConfigDrivenReportBridge(ReportGenerator reportGenerator, ILogger? logger = null)
    {
        _reportGenerator = reportGenerator ?? throw new ArgumentNullException(nameof(reportGenerator));
        _logger = logger;
    }

    /// <summary>
    /// 从测试报告生成指定格式的文件
    /// </summary>
    public async Task<ReportGenerationResult> GenerateFromTestReportAsync(
        ConfigDrivenTestReport testReport,
        ReportFormat format,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(testReport);

        _logger?.Info($"从测试报告生成 {format} 文件: 项目={testReport.ProjectId}, 步骤数={testReport.StepResults.Count}");

        var dataSet = ConvertToDataSet(testReport);
        var template = BuildTestReportTemplate(testReport);

        return await _reportGenerator.GenerateReportFromTemplateAsync(
            template, dataSet, format, outputPath, cancellationToken);
    }

    /// <summary>
    /// 批量报告：接收多个 DUT 测试结果，合并为一份汇总报告
    /// </summary>
    public async Task<ReportGenerationResult> GenerateBatchReportAsync(
        IReadOnlyList<ConfigDrivenTestReport> reports,
        ReportFormat format,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reports);

        _logger?.Info($"生成批量报告: {reports.Count} 个测试报告, 格式={format}");

        var mergedDataSet = MergeReportsToDataSet(reports);
        var template = BuildBatchReportTemplate(reports);

        return await _reportGenerator.GenerateReportFromTemplateAsync(
            template, mergedDataSet, format, outputPath, cancellationToken);
    }

    /// <summary>
    /// 将单个 ConfigDrivenTestReport 转换为 ReportDataSet
    /// </summary>
    public static ReportDataSet ConvertToDataSet(ConfigDrivenTestReport testReport)
    {
        var rows = new List<Dictionary<string, object>>();
        foreach (var step in testReport.StepResults)
        {
            var row = new Dictionary<string, object>
            {
                { "SessionId", testReport.ProjectId },
                { "DUTId", testReport.DutId },
                { "StepId", step.StepId },
                { "StepName", step.StepName },
                { "TestResult", step.Skipped ? "SKIP" : (step.Passed ? "PASS" : "FAIL") },
                { "MeasuredValue", step.MeasuredValue },
                { "ExpectedValue", step.ExpectedValue },
                { "ErrorMessage", step.ErrorMessage },
                { "ExecutionTime", step.EndTime - step.StartTime },
                { "Timestamp", step.StartTime },
                { "RetryCount", step.RetryCount }
            };
            rows.Add(row);
        }

        int total = testReport.StepResults.Count;
        int passed = testReport.StepResults.Count(s => s.Passed && !s.Skipped);
        int failed = testReport.StepResults.Count(s => !s.Passed && !s.Skipped);
        int skipped = testReport.StepResults.Count(s => s.Skipped);
        double passRate = total > 0 ? Math.Round((double)passed / total * 100, 2) : 0;

        var dataSet = new ReportDataSet
        {
            Name = testReport.ProjectName,
            Description = $"测试报告: {testReport.ProjectName} (DUT: {testReport.DutId})",
            Columns = new List<string>
            {
                "SessionId", "DUTId", "StepId", "StepName", "TestResult",
                "MeasuredValue", "ExpectedValue", "ErrorMessage", "ExecutionTime", "Timestamp", "RetryCount"
            },
            Rows = rows,
            Metadata = new Dictionary<string, object>
            {
                { "GeneratedAt", DateTime.UtcNow },
                { "ReportType", ReportType.Test },
                { "ProjectId", testReport.ProjectId },
                { "DutId", testReport.DutId },
                { "OverallResult", testReport.Passed ? "PASS" : "FAIL" }
            }
        };

        dataSet.DataItems.AddRange(new[]
        {
            new ReportDataItem { Name = "TotalTests", Value = total, DataType = "int", Category = "Summary" },
            new ReportDataItem { Name = "PassedTests", Value = passed, DataType = "int", Category = "Summary" },
            new ReportDataItem { Name = "FailedTests", Value = failed, DataType = "int", Category = "Summary" },
            new ReportDataItem { Name = "SkippedTests", Value = skipped, DataType = "int", Category = "Summary" },
            new ReportDataItem { Name = "PassRate", Value = passRate, DataType = "double", Unit = "%", Category = "Summary" }
        });

        return dataSet;
    }

    /// <summary>
    /// 合并多个测试报告的数据集
    /// </summary>
    public static ReportDataSet MergeReportsToDataSet(IReadOnlyList<ConfigDrivenTestReport> reports)
    {
        var allRows = new List<Dictionary<string, object>>();
        int totalSteps = 0;
        int totalPassed = 0;
        int totalFailed = 0;
        int totalSkipped = 0;

        foreach (var report in reports)
        {
            var single = ConvertToDataSet(report);
            allRows.AddRange(single.Rows);

            totalSteps += report.StepResults.Count;
            totalPassed += report.StepResults.Count(s => s.Passed && !s.Skipped);
            totalFailed += report.StepResults.Count(s => !s.Passed && !s.Skipped);
            totalSkipped += report.StepResults.Count(s => s.Skipped);
        }

        double passRate = totalSteps > 0 ? Math.Round((double)totalPassed / totalSteps * 100, 2) : 0;
        int dutPassed = reports.Count(r => r.Passed);
        int dutFailed = reports.Count(r => !r.Passed);

        var merged = new ReportDataSet
        {
            Name = "批量测试报告",
            Description = $"包含 {reports.Count} 个 DUT 的汇总报告",
            Columns = new List<string>
            {
                "SessionId", "DUTId", "StepId", "StepName", "TestResult",
                "MeasuredValue", "ExpectedValue", "ErrorMessage", "ExecutionTime", "Timestamp", "RetryCount"
            },
            Rows = allRows,
            Metadata = new Dictionary<string, object>
            {
                { "GeneratedAt", DateTime.UtcNow },
                { "ReportType", ReportType.Test },
                { "DutCount", reports.Count },
                { "DutPassed", dutPassed },
                { "DutFailed", dutFailed }
            }
        };

        merged.DataItems.AddRange(new[]
        {
            new ReportDataItem { Name = "TotalTests", Value = totalSteps, DataType = "int", Category = "Summary" },
            new ReportDataItem { Name = "PassedTests", Value = totalPassed, DataType = "int", Category = "Summary" },
            new ReportDataItem { Name = "FailedTests", Value = totalFailed, DataType = "int", Category = "Summary" },
            new ReportDataItem { Name = "SkippedTests", Value = totalSkipped, DataType = "int", Category = "Summary" },
            new ReportDataItem { Name = "PassRate", Value = passRate, DataType = "double", Unit = "%", Category = "Summary" },
            new ReportDataItem { Name = "DutCount", Value = reports.Count, DataType = "int", Category = "Summary" },
            new ReportDataItem { Name = "DutPassed", Value = dutPassed, DataType = "int", Category = "Summary" },
            new ReportDataItem { Name = "DutFailed", Value = dutFailed, DataType = "int", Category = "Summary" }
        });

        return merged;
    }

    private static ReportTemplate BuildTestReportTemplate(ConfigDrivenTestReport report)
    {
        int total = report.StepResults.Count;
        int passed = report.StepResults.Count(s => s.Passed && !s.Skipped);
        int failed = report.StepResults.Count(s => !s.Passed && !s.Skipped);
        int skipped = report.StepResults.Count(s => s.Skipped);
        double passRate = total > 0 ? Math.Round((double)passed / total * 100, 2) : 0;
        TimeSpan elapsed = report.EndTime - report.StartTime;

        var rowsHtml = new StringBuilder();
        foreach (var step in report.StepResults)
        {
            string resultText = step.Skipped ? "跳过" : (step.Passed ? "通过" : "失败");
            string cssClass = step.Skipped ? "skipped" : (step.Passed ? "passed" : "failed");
            TimeSpan stepTime = step.EndTime - step.StartTime;

            rowsHtml.AppendLine($"<tr><td>{step.StepId}</td><td>{step.StepName}</td>" +
                $"<td class='{cssClass}'>{resultText}</td>" +
                $"<td>{step.MeasuredValue}</td><td>{step.ExpectedValue}</td>" +
                $"<td>{stepTime.TotalSeconds:F2}s</td>" +
                $"<td>{step.ErrorMessage}</td></tr>");
        }

        string content = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>测试报告 - {report.ProjectName}</title>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 20px; }}
        .header {{ border-bottom: 2px solid #333; padding-bottom: 10px; margin-bottom: 20px; }}
        .summary {{ background-color: #f5f5f5; padding: 15px; margin-bottom: 20px; border-radius: 5px; }}
        .passed {{ color: #28a745; font-weight: bold; }}
        .failed {{ color: #dc3545; font-weight: bold; }}
        .skipped {{ color: #6c757d; }}
        table {{ border-collapse: collapse; width: 100%; }}
        th, td {{ border: 1px solid #ddd; padding: 8px; text-align: left; }}
        th {{ background-color: #f2f2f2; }}
        .overall-pass {{ background-color: #d4edda; padding: 5px 10px; border-radius: 3px; }}
        .overall-fail {{ background-color: #f8d7da; padding: 5px 10px; border-radius: 3px; }}
    </style>
</head>
<body>
    <div class='header'>
        <h1>自动化测试报告</h1>
        <p>项目: {report.ProjectName} ({report.ProjectId})</p>
        <p>DUT: {report.DutId}</p>
        <p>测试时间: {report.StartTime:yyyy-MM-dd HH:mm:ss} ~ {report.EndTime:yyyy-MM-dd HH:mm:ss}</p>
        <p>整体结果: <span class='{(report.Passed ? "overall-pass" : "overall-fail")}'>{(report.Passed ? "通过" : "失败")}</span></p>
    </div>

    <div class='summary'>
        <h2>测试摘要</h2>
        <p>总步骤数: {total}</p>
        <p class='passed'>通过: {passed}</p>
        <p class='failed'>失败: {failed}</p>
        <p class='skipped'>跳过: {skipped}</p>
        <p>通过率: {passRate}%</p>
        <p>执行时间: {elapsed.TotalSeconds:F2} 秒</p>
    </div>

    <div>
        <h2>详细步骤结果</h2>
        <table>
            <tr>
                <th>步骤ID</th>
                <th>步骤名称</th>
                <th>结果</th>
                <th>测量值</th>
                <th>期望值</th>
                <th>执行时间</th>
                <th>错误信息</th>
            </tr>
            {rowsHtml}
        </table>
    </div>
</body>
</html>";

        return new ReportTemplate
        {
            TemplateId = "CONFIG_DRIVEN_TEST_REPORT",
            Name = $"测试报告 - {report.ProjectName}",
            Description = "由 ConfigDrivenReportBridge 生成的测试报告",
            ReportType = ReportType.Test,
            Content = content,
            Version = "1.0"
        };
    }

    private static ReportTemplate BuildBatchReportTemplate(IReadOnlyList<ConfigDrivenTestReport> reports)
    {
        int dutCount = reports.Count;
        int dutPassed = reports.Count(r => r.Passed);
        int dutFailed = reports.Count(r => !r.Passed);
        int totalSteps = reports.Sum(r => r.StepResults.Count);
        int totalPassed = reports.Sum(r => r.StepResults.Count(s => s.Passed && !s.Skipped));
        int totalFailed = reports.Sum(r => r.StepResults.Count(s => !s.Passed && !s.Skipped));
        double passRate = totalSteps > 0 ? Math.Round((double)totalPassed / totalSteps * 100, 2) : 0;

        var dutRowsHtml = new StringBuilder();
        foreach (var report in reports)
        {
            int p = report.StepResults.Count(s => s.Passed && !s.Skipped);
            int f = report.StepResults.Count(s => !s.Passed && !s.Skipped);
            TimeSpan el = report.EndTime - report.StartTime;
            string css = report.Passed ? "passed" : "failed";

            dutRowsHtml.AppendLine($"<tr><td>{report.DutId}</td><td>{report.ProjectName}</td>" +
                $"<td class='{css}'>{(report.Passed ? "通过" : "失败")}</td>" +
                $"<td>{p}/{report.StepResults.Count}</td><td>{f}</td>" +
                $"<td>{el.TotalSeconds:F2}s</td></tr>");
        }

        string content = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>批量测试报告</title>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 20px; }}
        .header {{ border-bottom: 2px solid #333; padding-bottom: 10px; margin-bottom: 20px; }}
        .summary {{ background-color: #f5f5f5; padding: 15px; margin-bottom: 20px; border-radius: 5px; display: flex; gap: 20px; flex-wrap: wrap; }}
        .stat-card {{ background: white; padding: 15px; border-radius: 5px; box-shadow: 0 1px 3px rgba(0,0,0,.12); text-align: center; min-width: 120px; }}
        .passed {{ color: #28a745; font-weight: bold; }}
        .failed {{ color: #dc3545; font-weight: bold; }}
        table {{ border-collapse: collapse; width: 100%; }}
        th, td {{ border: 1px solid #ddd; padding: 8px; text-align: left; }}
        th {{ background-color: #f2f2f2; }}
    </style>
</head>
<body>
    <div class='header'>
        <h1>批量自动化测试报告</h1>
        <p>生成时间: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</p>
    </div>

    <div class='summary'>
        <div class='stat-card'><h3>DUT 总数</h3><p>{dutCount}</p></div>
        <div class='stat-card'><h3>DUT 通过</h3><p class='passed'>{dutPassed}</p></div>
        <div class='stat-card'><h3>DUT 失败</h3><p class='failed'>{dutFailed}</p></div>
        <div class='stat-card'><h3>总步骤数</h3><p>{totalSteps}</p></div>
        <div class='stat-card'><h3>步骤通过率</h3><p>{passRate}%</p></div>
    </div>

    <div>
        <h2>DUT 测试汇总</h2>
        <table>
            <tr>
                <th>DUT ID</th>
                <th>项目名称</th>
                <th>结果</th>
                <th>通过/总数</th>
                <th>失败数</th>
                <th>执行时间</th>
            </tr>
            {dutRowsHtml}
        </table>
    </div>
</body>
</html>";

        return new ReportTemplate
        {
            TemplateId = "CONFIG_DRIVEN_BATCH_REPORT",
            Name = "批量测试报告",
            Description = "由 ConfigDrivenReportBridge 生成的批量报告",
            ReportType = ReportType.Test,
            Content = content,
            Version = "1.0"
        };
    }
}
