using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UTF.Logging;

namespace UTF.Core;

/// <summary>
/// 测试报告生成器 - 支持多种格式的测试报告生成
/// </summary>
public sealed class ConfigDrivenReportGenerator
{
    private readonly ILogger _logger;

    public ConfigDrivenReportGenerator(ILogger? logger = null)
    {
        _logger = logger ?? LoggerFactory.CreateLogger<ConfigDrivenReportGenerator>();
    }

    /// <summary>
    /// 生成 JSON 格式报告
    /// </summary>
    public async Task<bool> GenerateJsonReportAsync(
        ConfigTestSession session,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.Info($"生成 JSON 报告: {outputPath}");

            var report = new
            {
                SessionId = session.SessionId,
                TestProject = new
                {
                    session.TestProject.Id,
                    session.TestProject.Name,
                    session.TestProject.Description
                },
                Operator = session.Operator,
                Status = session.Status.ToString(),
                OverallPassed = session.OverallPassed,
                CreatedTime = session.CreatedTime,
                StartTime = session.StartTime,
                EndTime = session.EndTime,
                Duration = session.EndTime.HasValue
                    ? (session.EndTime.Value - session.StartTime.GetValueOrDefault()).TotalSeconds
                    : 0,
                DutCount = session.DutIds.Count,
                PassedDutCount = session.DutResults.Values.Count(r => r.Passed),
                FailedDutCount = session.DutResults.Values.Count(r => !r.Passed),
                DutResults = session.DutResults.Select(kvp => new
                {
                    DutId = kvp.Key,
                    Passed = kvp.Value.Passed,
                    StartTime = kvp.Value.StartTime,
                    EndTime = kvp.Value.EndTime,
                    Duration = (kvp.Value.EndTime - kvp.Value.StartTime).TotalSeconds,
                    ErrorMessage = kvp.Value.ErrorMessage,
                    StepResults = kvp.Value.StepResults.Select(s => new
                    {
                        s.StepId,
                        s.StepName,
                        s.Passed,
                        s.Skipped,
                        s.MeasuredValue,
                        s.ExpectedValue,
                        s.ErrorMessage,
                        s.StartTime,
                        s.EndTime,
                        Duration = (s.EndTime - s.StartTime).TotalMilliseconds,
                        s.RetryCount
                    }).ToList()
                }).ToList()
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            var jsonString = JsonSerializer.Serialize(report, options);

            // 确保目录存在
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(outputPath, jsonString, Encoding.UTF8, cancellationToken);

            _logger.Info($"JSON 报告生成成功: {outputPath}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"生成 JSON 报告失败: {outputPath}", ex);
            return false;
        }
    }

    /// <summary>
    /// 生成 CSV 格式报告
    /// </summary>
    public async Task<bool> GenerateCsvReportAsync(
        ConfigTestSession session,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.Info($"生成 CSV 报告: {outputPath}");

            var csv = new StringBuilder();

            // 添加标题行
            csv.AppendLine("DUT ID,步骤ID,步骤名称,结果,跳过,测量值,期望值,错误信息,开始时间,结束时间,耗时(ms),重试次数");

            // 添加数据行
            foreach (var dutResult in session.DutResults)
            {
                foreach (var stepResult in dutResult.Value.StepResults)
                {
                    var duration = (stepResult.EndTime - stepResult.StartTime).TotalMilliseconds;
                    csv.AppendLine($"{EscapeCsv(dutResult.Key)}," +
                                   $"{EscapeCsv(stepResult.StepId)}," +
                                   $"{EscapeCsv(stepResult.StepName)}," +
                                   $"{(stepResult.Passed ? "PASS" : "FAIL")}," +
                                   $"{stepResult.Skipped}," +
                                   $"{EscapeCsv(stepResult.MeasuredValue)}," +
                                   $"{EscapeCsv(stepResult.ExpectedValue)}," +
                                   $"{EscapeCsv(stepResult.ErrorMessage)}," +
                                   $"{stepResult.StartTime:yyyy-MM-dd HH:mm:ss}," +
                                   $"{stepResult.EndTime:yyyy-MM-dd HH:mm:ss}," +
                                   $"{duration:F2}," +
                                   $"{stepResult.RetryCount}");
                }
            }

            // 确保目录存在
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(outputPath, csv.ToString(), Encoding.UTF8, cancellationToken);

            _logger.Info($"CSV 报告生成成功: {outputPath}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"生成 CSV 报告失败: {outputPath}", ex);
            return false;
        }
    }

    /// <summary>
    /// 生成 HTML 格式报告
    /// </summary>
    public async Task<bool> GenerateHtmlReportAsync(
        ConfigTestSession session,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.Info($"生成 HTML 报告: {outputPath}");

            var html = new StringBuilder();

            // HTML 头部
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html lang='zh-CN'>");
            html.AppendLine("<head>");
            html.AppendLine("    <meta charset='UTF-8'>");
            html.AppendLine("    <meta name='viewport' content='width=device-width, initial-scale=1.0'>");
            html.AppendLine($"    <title>测试报告 - {session.TestProject.Name}</title>");
            html.AppendLine("    <style>");
            html.AppendLine("        body { font-family: Arial, sans-serif; margin: 20px; background-color: #f5f5f5; }");
            html.AppendLine("        .container { max-width: 1200px; margin: 0 auto; background-color: white; padding: 20px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }");
            html.AppendLine("        h1 { color: #333; border-bottom: 2px solid #4CAF50; padding-bottom: 10px; }");
            html.AppendLine("        h2 { color: #555; margin-top: 30px; }");
            html.AppendLine("        .summary { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 15px; margin: 20px 0; }");
            html.AppendLine("        .summary-card { background: #f9f9f9; padding: 15px; border-radius: 5px; border-left: 4px solid #4CAF50; }");
            html.AppendLine("        .summary-card.fail { border-left-color: #f44336; }");
            html.AppendLine("        .summary-card h3 { margin: 0 0 10px 0; font-size: 14px; color: #666; }");
            html.AppendLine("        .summary-card .value { font-size: 24px; font-weight: bold; color: #333; }");
            html.AppendLine("        table { width: 100%; border-collapse: collapse; margin: 20px 0; }");
            html.AppendLine("        th, td { padding: 12px; text-align: left; border-bottom: 1px solid #ddd; }");
            html.AppendLine("        th { background-color: #4CAF50; color: white; font-weight: bold; }");
            html.AppendLine("        tr:hover { background-color: #f5f5f5; }");
            html.AppendLine("        .pass { color: #4CAF50; font-weight: bold; }");
            html.AppendLine("        .fail { color: #f44336; font-weight: bold; }");
            html.AppendLine("        .skip { color: #FF9800; font-weight: bold; }");
            html.AppendLine("        .dut-section { margin: 30px 0; padding: 20px; background: #fafafa; border-radius: 5px; }");
            html.AppendLine("        .error-message { color: #f44336; font-size: 12px; }");
            html.AppendLine("        .timestamp { color: #666; font-size: 12px; }");
            html.AppendLine("    </style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            html.AppendLine("    <div class='container'>");

            // 标题
            html.AppendLine($"        <h1>测试报告 - {session.TestProject.Name}</h1>");

            // 摘要信息
            var passedDuts = session.DutResults.Values.Count(r => r.Passed);
            var failedDuts = session.DutResults.Values.Count(r => !r.Passed);
            var totalSteps = session.DutResults.Values.Sum(r => r.StepResults.Count);
            var passedSteps = session.DutResults.Values.Sum(r => r.StepResults.Count(s => s.Passed));
            var duration = session.EndTime.HasValue
                ? (session.EndTime.Value - session.StartTime.GetValueOrDefault()).TotalSeconds
                : 0;

            html.AppendLine("        <div class='summary'>");
            html.AppendLine($"            <div class='summary-card {(session.OverallPassed ? "" : "fail")}'>");
            html.AppendLine("                <h3>整体结果</h3>");
            html.AppendLine($"                <div class='value'>{(session.OverallPassed ? "✅ PASS" : "❌ FAIL")}</div>");
            html.AppendLine("            </div>");
            html.AppendLine($"            <div class='summary-card'>");
            html.AppendLine("                <h3>DUT 数量</h3>");
            html.AppendLine($"                <div class='value'>{session.DutIds.Count}</div>");
            html.AppendLine("            </div>");
            html.AppendLine($"            <div class='summary-card'>");
            html.AppendLine("                <h3>通过 DUT</h3>");
            html.AppendLine($"                <div class='value'>{passedDuts}</div>");
            html.AppendLine("            </div>");
            html.AppendLine($"            <div class='summary-card {(failedDuts > 0 ? "fail" : "")}'>");
            html.AppendLine("                <h3>失败 DUT</h3>");
            html.AppendLine($"                <div class='value'>{failedDuts}</div>");
            html.AppendLine("            </div>");
            html.AppendLine($"            <div class='summary-card'>");
            html.AppendLine("                <h3>总步骤数</h3>");
            html.AppendLine($"                <div class='value'>{totalSteps}</div>");
            html.AppendLine("            </div>");
            html.AppendLine($"            <div class='summary-card'>");
            html.AppendLine("                <h3>通过率</h3>");
            html.AppendLine($"                <div class='value'>{(totalSteps > 0 ? (double)passedSteps / totalSteps : 0):P2}</div>");
            html.AppendLine("            </div>");
            html.AppendLine($"            <div class='summary-card'>");
            html.AppendLine("                <h3>总耗时</h3>");
            html.AppendLine($"                <div class='value'>{duration:F2}s</div>");
            html.AppendLine("            </div>");
            html.AppendLine($"            <div class='summary-card'>");
            html.AppendLine("                <h3>操作员</h3>");
            html.AppendLine($"                <div class='value' style='font-size: 18px;'>{session.Operator}</div>");
            html.AppendLine("            </div>");
            html.AppendLine("        </div>");

            // 测试信息
            html.AppendLine("        <h2>测试信息</h2>");
            html.AppendLine("        <table>");
            html.AppendLine("            <tr><th>项目</th><th>值</th></tr>");
            html.AppendLine($"            <tr><td>会话 ID</td><td>{session.SessionId}</td></tr>");
            html.AppendLine($"            <tr><td>测试项目</td><td>{session.TestProject.Name}</td></tr>");
            html.AppendLine($"            <tr><td>项目描述</td><td>{session.TestProject.Description}</td></tr>");
            html.AppendLine($"            <tr><td>操作员</td><td>{session.Operator}</td></tr>");
            html.AppendLine($"            <tr><td>创建时间</td><td>{session.CreatedTime:yyyy-MM-dd HH:mm:ss}</td></tr>");
            html.AppendLine($"            <tr><td>开始时间</td><td>{session.StartTime:yyyy-MM-dd HH:mm:ss}</td></tr>");
            html.AppendLine($"            <tr><td>结束时间</td><td>{session.EndTime:yyyy-MM-dd HH:mm:ss}</td></tr>");
            html.AppendLine($"            <tr><td>总耗时</td><td>{duration:F2} 秒</td></tr>");
            html.AppendLine("        </table>");

            // 各 DUT 测试结果
            html.AppendLine("        <h2>DUT 测试结果</h2>");

            foreach (var dutResult in session.DutResults.OrderBy(kvp => kvp.Key))
            {
                var dutDuration = (dutResult.Value.EndTime - dutResult.Value.StartTime).TotalSeconds;

                html.AppendLine("        <div class='dut-section'>");
                html.AppendLine($"            <h3>{dutResult.Key} - {(dutResult.Value.Passed ? "<span class='pass'>✅ PASS</span>" : "<span class='fail'>❌ FAIL</span>")} ({dutDuration:F2}s)</h3>");

                if (!string.IsNullOrEmpty(dutResult.Value.ErrorMessage))
                {
                    html.AppendLine($"            <p class='error-message'>错误: {dutResult.Value.ErrorMessage}</p>");
                }

                html.AppendLine("            <table>");
                html.AppendLine("                <tr>");
                html.AppendLine("                    <th>步骤</th>");
                html.AppendLine("                    <th>结果</th>");
                html.AppendLine("                    <th>测量值</th>");
                html.AppendLine("                    <th>期望值</th>");
                html.AppendLine("                    <th>耗时</th>");
                html.AppendLine("                    <th>重试</th>");
                html.AppendLine("                    <th>错误信息</th>");
                html.AppendLine("                </tr>");

                foreach (var stepResult in dutResult.Value.StepResults)
                {
                    var stepDuration = (stepResult.EndTime - stepResult.StartTime).TotalMilliseconds;
                    var resultClass = stepResult.Passed ? "pass" : (stepResult.Skipped ? "skip" : "fail");
                    var resultText = stepResult.Passed ? "✅ PASS" : (stepResult.Skipped ? "⏭️ SKIP" : "❌ FAIL");

                    html.AppendLine("                <tr>");
                    html.AppendLine($"                    <td>{stepResult.StepName}</td>");
                    html.AppendLine($"                    <td class='{resultClass}'>{resultText}</td>");
                    html.AppendLine($"                    <td>{stepResult.MeasuredValue}</td>");
                    html.AppendLine($"                    <td>{stepResult.ExpectedValue}</td>");
                    html.AppendLine($"                    <td>{stepDuration:F0}ms</td>");
                    html.AppendLine($"                    <td>{stepResult.RetryCount}</td>");
                    html.AppendLine($"                    <td class='error-message'>{stepResult.ErrorMessage}</td>");
                    html.AppendLine("                </tr>");
                }

                html.AppendLine("            </table>");
                html.AppendLine("        </div>");
            }

            // HTML 尾部
            html.AppendLine("    </div>");
            html.AppendLine("</body>");
            html.AppendLine("</html>");

            // 确保目录存在
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(outputPath, html.ToString(), Encoding.UTF8, cancellationToken);

            _logger.Info($"HTML 报告生成成功: {outputPath}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"生成 HTML 报告失败: {outputPath}", ex);
            return false;
        }
    }

    /// <summary>
    /// 生成所有格式的报告
    /// </summary>
    public async Task<bool> GenerateAllReportsAsync(
        ConfigTestSession session,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.Info($"生成所有格式报告: {outputDirectory}");

            // 确保目录存在
            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var baseFileName = $"test_report_{session.SessionId}_{timestamp}";

            var tasks = new[]
            {
                GenerateJsonReportAsync(session, Path.Combine(outputDirectory, $"{baseFileName}.json"), cancellationToken),
                GenerateCsvReportAsync(session, Path.Combine(outputDirectory, $"{baseFileName}.csv"), cancellationToken),
                GenerateHtmlReportAsync(session, Path.Combine(outputDirectory, $"{baseFileName}.html"), cancellationToken)
            };

            var results = await Task.WhenAll(tasks);

            var success = results.All(r => r);
            _logger.Info($"所有格式报告生成{(success ? "成功" : "部分失败")}");

            return success;
        }
        catch (Exception ex)
        {
            _logger.Error($"生成所有格式报告失败: {outputDirectory}", ex);
            return false;
        }
    }

    /// <summary>
    /// CSV 字段转义
    /// </summary>
    private string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
