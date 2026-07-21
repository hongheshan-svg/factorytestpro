using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Xml;
using UTF.Core.Persistence;
using System.Threading;
using System.Threading.Tasks;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using UTF.Logging;

namespace UTF.Reporting;

/// <summary>
/// 报表生成器实现
/// </summary>
public sealed class ReportGenerator : IReportGenerator, IDisposable
{
    private readonly ConcurrentDictionary<string, ReportTemplate> _templates = new();
    private readonly UTF.Logging.ILogger? _logger;
    private readonly ITestResultRepository? _resultRepository;
    private bool _disposed = false;

    public ReportGenerator(UTF.Logging.ILogger? logger = null, ITestResultRepository? resultRepository = null)
    {
        _logger = logger;
        _resultRepository = resultRepository;
        InitializeDefaultTemplates();
    }

    public IReadOnlyList<ReportFormat> SupportedFormats => new[]
    {
        ReportFormat.HTML,
        ReportFormat.PDF,
        ReportFormat.CSV,
        ReportFormat.JSON,
        ReportFormat.XML
    }.ToList().AsReadOnly();

    static ReportGenerator()
    {
        // Community license for open-source / internal factory tooling.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private void InitializeDefaultTemplates()
    {
        // 创建默认的测试报告模板
        var testReportTemplate = new ReportTemplate
        {
            TemplateId = "DEFAULT_TEST_REPORT",
            Name = "默认测试报告",
            Description = "标准的测试结果报告模板",
            ReportType = ReportType.Test,
            Content = CreateDefaultTestReportTemplate(),
            DataBindings = new Dictionary<string, string>
            {
                { "TestSession", "session" },
                { "TestResults", "results" },
                { "Statistics", "statistics" },
                { "DeviceInfo", "devices" }
            },
            Charts = new List<ChartConfiguration>
            {
                new ChartConfiguration
                {
                    ChartType = "PieChart",
                    Title = "测试通过率",
                    Width = 400,
                    Height = 300,
                    Series = new List<Dictionary<string, object>>
                    {
                        new() { { "name", "通过" }, { "field", "PassedTests" } },
                        new() { { "name", "失败" }, { "field", "FailedTests" } }
                    }
                }
            },
            Version = "1.0"
        };
        
        _templates[testReportTemplate.TemplateId] = testReportTemplate;
        
        // 创建统计报告模板
        var statisticsTemplate = new ReportTemplate
        {
            TemplateId = "STATISTICS_REPORT",
            Name = "统计分析报告",
            Description = "测试统计和趋势分析报告",
            ReportType = ReportType.Statistics,
            Content = CreateStatisticsReportTemplate(),
            DataBindings = new Dictionary<string, string>
            {
                { "GlobalStatistics", "global_stats" },
                { "TrendData", "trends" },
                { "DeviceStatistics", "device_stats" }
            },
            Charts = new List<ChartConfiguration>
            {
                new ChartConfiguration
                {
                    ChartType = "LineChart",
                    Title = "测试趋势",
                    Width = 600,
                    Height = 400,
                    XAxisLabel = "时间",
                    YAxisLabel = "通过率",
                    Series = new List<Dictionary<string, object>>
                    {
                        new() { { "name", "通过率" }, { "field", "PassRate" } }
                    }
                }
            },
            Version = "1.0"
        };
        
        _templates[statisticsTemplate.TemplateId] = statisticsTemplate;
    }

    private string CreateDefaultTestReportTemplate()
    {
        return @"
<!DOCTYPE html>
<html>
<head>
    <title>测试报告</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 20px; }
        .header { border-bottom: 2px solid #333; padding-bottom: 10px; margin-bottom: 20px; }
        .summary { background-color: #f5f5f5; padding: 15px; margin-bottom: 20px; }
        .test-results { margin-bottom: 20px; }
        .passed { color: green; }
        .failed { color: red; }
        table { border-collapse: collapse; width: 100%; }
        th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }
        th { background-color: #f2f2f2; }
    </style>
</head>
<body>
    <div class='header'>
        <h1>自动化测试报告</h1>
        <p>会话ID: {{SessionId}}</p>
        <p>测试时间: {{TestTime}}</p>
        <p>操作员: {{Operator}}</p>
    </div>
    
    <div class='summary'>
        <h2>测试摘要</h2>
        <p>总测试数: {{TotalTests}}</p>
        <p class='passed'>通过测试: {{PassedTests}}</p>
        <p class='failed'>失败测试: {{FailedTests}}</p>
        <p>通过率: {{PassRate}}%</p>
        <p>执行时间: {{ExecutionTime}}</p>
    </div>
    
    <div class='test-results'>
        <h2>详细测试结果</h2>
        <table>
            <tr>
                <th>DUT ID</th>
                <th>测试步骤</th>
                <th>结果</th>
                <th>测量值</th>
                <th>期望值</th>
                <th>执行时间</th>
            </tr>
            {{#TestResults}}
            <tr>
                <td>{{DUTId}}</td>
                <td>{{StepName}}</td>
                <td class='{{ResultClass}}'>{{Result}}</td>
                <td>{{MeasuredValue}}</td>
                <td>{{ExpectedValue}}</td>
                <td>{{ExecutionTime}}</td>
            </tr>
            {{/TestResults}}
        </table>
    </div>
</body>
</html>";
    }

    private string CreateStatisticsReportTemplate()
    {
        return @"
<!DOCTYPE html>
<html>
<head>
    <title>统计分析报告</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 20px; }
        .header { border-bottom: 2px solid #333; padding-bottom: 10px; margin-bottom: 20px; }
        .statistics { display: flex; justify-content: space-between; margin-bottom: 20px; }
        .stat-card { background-color: #f5f5f5; padding: 15px; border-radius: 5px; text-align: center; }
        .chart-container { margin: 20px 0; }
    </style>
</head>
<body>
    <div class='header'>
        <h1>测试统计分析报告</h1>
        <p>报告生成时间: {{ReportTime}}</p>
        <p>统计周期: {{StatisticsPeriod}}</p>
    </div>
    
    <div class='statistics'>
        <div class='stat-card'>
            <h3>总会话数</h3>
            <p>{{TotalSessions}}</p>
        </div>
        <div class='stat-card'>
            <h3>总测试数</h3>
            <p>{{TotalTests}}</p>
        </div>
        <div class='stat-card'>
            <h3>平均通过率</h3>
            <p>{{AveragePassRate}}%</p>
        </div>
        <div class='stat-card'>
            <h3>平均执行时间</h3>
            <p>{{AverageExecutionTime}}</p>
        </div>
    </div>
    
    <div class='chart-container'>
        <h2>趋势分析</h2>
        <!-- 图表将在这里插入 -->
        {{TrendChart}}
    </div>
</body>
</html>";
    }

    public async Task<ReportGenerationResult> GenerateReportAsync(ReportConfiguration configuration, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            _logger?.Info($"开始生成报告: {configuration.ReportName} ({configuration.OutputFormat})");
            
            // 验证配置
            if (!await ValidateConfigurationAsync(configuration, cancellationToken))
            {
                return ReportGenerationResult.CreateFailure("报告配置验证失败");
            }
            
            // 获取数据
            var dataSet = await GenerateDataSetAsync(configuration, cancellationToken);
            
            // 选择模板
            var template = await GetTemplateAsync(configuration.TemplateId, configuration.ReportType);
            if (template == null)
            {
                return ReportGenerationResult.CreateFailure("未找到合适的报告模板");
            }
            
            // 生成报告
            var result = await GenerateReportFromTemplateAsync(template, dataSet, configuration.OutputFormat, configuration.OutputPath, cancellationToken);
            
            var generationTime = DateTime.UtcNow - startTime;
            _logger?.Info($"报告生成完成: {configuration.ReportName}, 耗时: {generationTime.TotalSeconds:F2}秒");
            
            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var generationTime = DateTime.UtcNow - startTime;
            _logger?.Error($"生成报告失败: {ex.Message}");
            return ReportGenerationResult.CreateFailure(ex.Message, generationTime);
        }
    }

    private async Task<ReportDataSet> GenerateDataSetAsync(ReportConfiguration configuration, CancellationToken cancellationToken)
    {
        if (_resultRepository == null)
        {
            throw new InvalidOperationException(
                "No test-result repository is configured. Use GenerateReportFromTemplateAsync with an explicit data set.");
        }

        var dutId = configuration.Filters.TryGetValue("DutId", out var dutFilter)
            ? dutFilter?.ToString()
            : null;
        bool? passedFilterValue = null;
        if (configuration.Filters.TryGetValue("Passed", out var passedFilter) &&
            bool.TryParse(passedFilter?.ToString(), out var parsedPassed))
        {
            passedFilterValue = parsedPassed;
        }

        var reports = (await _resultRepository.QueryAsync(new TestResultQuery(
            DutId: dutId,
            StartDate: configuration.DateRangeStart,
            EndDate: configuration.DateRangeEnd,
            Passed: passedFilterValue,
            Take: 10_000), cancellationToken).ConfigureAwait(false)).ToList();

        var rows = reports.Select(report => new Dictionary<string, object>
        {
            ["SessionId"] = report.TaskId,
            ["DUTId"] = report.DUTId,
            ["TestResult"] = report.OverallResult ? "PASS" : "FAIL",
            ["ExecutionTime"] = report.TotalExecutionTime,
            ["Timestamp"] = report.StartTime
        }).ToList();

        var dataSet = new ReportDataSet
        {
            Name = configuration.ReportName,
            Description = $"数据集为 {configuration.ReportName}",
            Columns = new List<string> { "SessionId", "DUTId", "TestResult", "ExecutionTime", "Timestamp" },
            Rows = rows,
            Metadata = new Dictionary<string, object>
            {
                { "GeneratedAt", DateTime.UtcNow },
                { "ReportType", configuration.ReportType },
                { "DataSource", nameof(ITestResultRepository) }
            }
        };

        // 基于真实行计算汇总项；无数据时为 0
        var total = rows.Count;
        var passed = rows.Count(r => r.TryGetValue("TestResult", out var v) && v?.ToString() == "PASS");
        var failed = rows.Count(r => r.TryGetValue("TestResult", out var v) && v?.ToString() == "FAIL");
        var passRate = total > 0 ? Math.Round((double)passed / total * 100, 2) : 0;

        dataSet.DataItems.AddRange(new[]
        {
            new ReportDataItem { Name = "TotalTests", Value = total, DataType = "int", Category = "Summary" },
            new ReportDataItem { Name = "PassedTests", Value = passed, DataType = "int", Category = "Summary" },
            new ReportDataItem { Name = "FailedTests", Value = failed, DataType = "int", Category = "Summary" },
            new ReportDataItem { Name = "PassRate", Value = passRate, DataType = "double", Unit = "%", Category = "Summary" }
        });

        return dataSet;
    }

    private Task<ReportTemplate?> GetTemplateAsync(string? templateId, ReportType reportType)
    {
        if (!string.IsNullOrEmpty(templateId) && _templates.TryGetValue(templateId, out var template))
        {
            return Task.FromResult<ReportTemplate?>(template);
        }

        // 根据报告类型查找默认模板
        var defaultTemplate = _templates.Values.FirstOrDefault(t => t.ReportType == reportType);
        return Task.FromResult<ReportTemplate?>(defaultTemplate);
    }

    public async Task<ReportGenerationResult> GenerateReportFromTemplateAsync(ReportTemplate template, ReportDataSet dataSet, ReportFormat format, string outputPath, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            _logger?.Info($"使用模板生成报告: {template.Name} -> {format}");
            
            var fullOutputPath = Path.GetFullPath(outputPath);
            var outputDirectory = Path.GetDirectoryName(fullOutputPath)!;
            Directory.CreateDirectory(outputDirectory);
            var temporaryPath = Path.Combine(outputDirectory, $".{Path.GetFileName(fullOutputPath)}.{Guid.NewGuid():N}.tmp");
            try
            {
                if (format == ReportFormat.PDF)
                {
                    await GeneratePdfReportToFileAsync(template, dataSet, temporaryPath, cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    var content = format switch
                    {
                        ReportFormat.HTML => await GenerateHtmlReportAsync(template, dataSet, cancellationToken),
                        ReportFormat.Excel => await GenerateExcelReportAsync(template, dataSet, cancellationToken),
                        ReportFormat.CSV => await GenerateCsvReportAsync(template, dataSet, cancellationToken),
                        ReportFormat.JSON => await GenerateJsonReportAsync(template, dataSet, cancellationToken),
                        ReportFormat.XML => await GenerateXmlReportAsync(template, dataSet, cancellationToken),
                        _ => throw new NotSupportedException($"不支持的报告格式: {format}")
                    };
                    await File.WriteAllTextAsync(temporaryPath, content, Encoding.UTF8, cancellationToken)
                        .ConfigureAwait(false);
                }

                File.Move(temporaryPath, fullOutputPath, overwrite: true);
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                }
                catch
                {
                    // Do not mask a report generation failure.
                }
            }
            
            var fileInfo = new FileInfo(outputPath);
            var generationTime = DateTime.UtcNow - startTime;
            
            _logger?.Info($"报告文件已保存: {outputPath}, 大小: {fileInfo.Length} 字节");
            
            return ReportGenerationResult.CreateSuccess(outputPath, fileInfo.Length, generationTime);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var generationTime = DateTime.UtcNow - startTime;
            _logger?.Error($"生成报告失败: {ex.Message}");
            return ReportGenerationResult.CreateFailure(ex.Message, generationTime);
        }
    }

    private Task<string> GenerateHtmlReportAsync(ReportTemplate template, ReportDataSet dataSet, CancellationToken cancellationToken)
    {
        var html = template.Content;

        // 替换数据绑定
        foreach (var binding in template.DataBindings)
        {
            var placeholder = $"{{{{{binding.Key}}}}}";
            var value = HtmlEncode(GetDataValue(dataSet, binding.Value));
            html = html.Replace(placeholder, value);
        }

        // 替换基本统计信息
        var totalTests = dataSet.DataItems.FirstOrDefault(i => i.Name == "TotalTests")?.Value?.ToString() ?? "0";
        var passedTests = dataSet.DataItems.FirstOrDefault(i => i.Name == "PassedTests")?.Value?.ToString() ?? "0";
        var failedTests = dataSet.DataItems.FirstOrDefault(i => i.Name == "FailedTests")?.Value?.ToString() ?? "0";
        var passRate = dataSet.DataItems.FirstOrDefault(i => i.Name == "PassRate")?.Value?.ToString() ?? "0";

        // 从数据集元数据绑定会话级占位符；缺省时省略（替换为空）以避免遗留占位符
        var sessionId = dataSet.Metadata.TryGetValue("SessionId", out var sid) ? sid?.ToString() ?? string.Empty : string.Empty;
        var executionTime = dataSet.Metadata.TryGetValue("ExecutionTime", out var et) ? et?.ToString() ?? string.Empty : string.Empty;

        html = html.Replace("{{SessionId}}", HtmlEncode(sessionId))
                  .Replace("{{TestTime}}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
                  .Replace("{{Operator}}", "System")
                  .Replace("{{TotalTests}}", totalTests)
                  .Replace("{{PassedTests}}", passedTests)
                  .Replace("{{FailedTests}}", failedTests)
                  .Replace("{{PassRate}}", passRate)
                  .Replace("{{ExecutionTime}}", HtmlEncode(executionTime));

        // 在 {{#TestResults}}...{{/TestResults}} 标记处定向插入结果行；
        // 不再使用 html.Replace("<tr>", ...)（会误替换表头与所有 <tr>）。
        html = ReplaceTestResultsBlock(html, dataSet);

        return Task.FromResult(html);
    }

    /// <summary>
    /// 将模板中 <c>{{#TestResults}}...{{/TestResults}}</c> 包围的行模板替换为
    /// 数据集中每行展开后的 HTML，并移除标记本身。行模板内的占位符
    /// （<c>{{DUTId}}</c>、<c>{{StepName}}</c>、<c>{{Result}}</c>、
    /// <c>{{MeasuredValue}}</c>、<c>{{ExpectedValue}}</c>、<c>{{ExecutionTime}}</c>）
    /// 会按行数据填充；行中缺失的字段以空串填充。
    /// </summary>
    private string ReplaceTestResultsBlock(string html, ReportDataSet dataSet)
    {
        const string startMarker = "{{#TestResults}}";
        const string endMarker = "{{/TestResults}}";

        var startIdx = html.IndexOf(startMarker, StringComparison.Ordinal);
        var endIdx = html.IndexOf(endMarker, StringComparison.Ordinal);
        if (startIdx < 0 || endIdx < 0 || endIdx < startIdx)
        {
            // 模板未包含标记：直接返回
            return html;
        }

        // 行模板位于标记之间（不含标记本身）
        var rowTemplate = html.Substring(startIdx + startMarker.Length, endIdx - (startIdx + startMarker.Length));

        var rowsHtml = new StringBuilder();
        foreach (var row in dataSet.Rows)
        {
            var resultClass = row.TryGetValue("TestResult", out var r) && r?.ToString() == "PASS" ? "passed" : "failed";
            var rowHtml = rowTemplate
                .Replace("{{DUTId}}", HtmlEncode(row.TryGetValue("DUTId", out var dutId) ? dutId : null))
                .Replace("{{StepName}}", HtmlEncode(row.TryGetValue("StepName", out var stepName) ? stepName : null))
                .Replace("{{Result}}", HtmlEncode(row.TryGetValue("TestResult", out var result) ? result : null))
                .Replace("{{ResultClass}}", resultClass)
                .Replace("{{MeasuredValue}}", HtmlEncode(row.TryGetValue("MeasuredValue", out var mv) ? mv : null))
                .Replace("{{ExpectedValue}}", HtmlEncode(row.TryGetValue("ExpectedValue", out var ev) ? ev : null))
                .Replace("{{ExecutionTime}}", HtmlEncode(row.TryGetValue("ExecutionTime", out var execTime) ? execTime : null));
            rowsHtml.Append(rowHtml);
        }

        // 用展开后的行替换整个标记块（含起止标记）
        return html.Substring(0, startIdx) + rowsHtml + html.Substring(endIdx + endMarker.Length);
    }

    /// <summary>
    /// 使用 QuestPDF 将数据集渲染为 PDF 文件（二进制写入，非文本）。
    /// </summary>
    private Task GeneratePdfReportToFileAsync(
        ReportTemplate template,
        ReportDataSet dataSet,
        string outputPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var totalTests = dataSet.DataItems.FirstOrDefault(i => i.Name == "TotalTests")?.Value?.ToString() ?? "0";
        var passedTests = dataSet.DataItems.FirstOrDefault(i => i.Name == "PassedTests")?.Value?.ToString() ?? "0";
        var failedTests = dataSet.DataItems.FirstOrDefault(i => i.Name == "FailedTests")?.Value?.ToString() ?? "0";
        var passRate = dataSet.DataItems.FirstOrDefault(i => i.Name == "PassRate")?.Value?.ToString() ?? "0";
        var sessionId = dataSet.Metadata.TryGetValue("SessionId", out var sid) ? sid?.ToString() ?? "" : "";
        var title = string.IsNullOrWhiteSpace(template.Name) ? "UTF Test Report" : template.Name;

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Text(title).FontSize(18).SemiBold();
                    col.Item().Text($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}").FontSize(9).FontColor(Colors.Grey.Darken1);
                    if (!string.IsNullOrEmpty(sessionId))
                    {
                        col.Item().Text($"Session: {sessionId}").FontSize(9);
                    }
                });

                page.Content().PaddingVertical(12).Column(col =>
                {
                    col.Spacing(8);
                    col.Item().Text("Summary").FontSize(14).SemiBold();
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn();
                            c.RelativeColumn();
                        });
                        void Row(string k, string v)
                        {
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(k);
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(v);
                        }

                        Row("Total tests", totalTests);
                        Row("Passed", passedTests);
                        Row("Failed", failedTests);
                        Row("Pass rate", passRate);
                    });

                    col.Item().PaddingTop(12).Text("Results").FontSize(14).SemiBold();
                    if (dataSet.Rows.Count == 0)
                    {
                        col.Item().Text("No result rows.").Italic().FontColor(Colors.Grey.Medium);
                    }
                    else
                    {
                        col.Item().Table(table =>
                        {
                            var columns = dataSet.Columns.Count > 0
                                ? dataSet.Columns
                                : new List<string> { "DUTId", "StepName", "TestResult", "MeasuredValue", "ExpectedValue" };

                            table.ColumnsDefinition(c =>
                            {
                                foreach (var _ in columns)
                                {
                                    c.RelativeColumn();
                                }
                            });

                            table.Header(h =>
                            {
                                foreach (var name in columns)
                                {
                                    h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(name).SemiBold().FontSize(8);
                                }
                            });

                            foreach (var row in dataSet.Rows)
                            {
                                foreach (var name in columns)
                                {
                                    row.TryGetValue(name, out var value);
                                    table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                                        .Padding(3).Text(value?.ToString() ?? "").FontSize(8);
                                }
                            }
                        });
                    }
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Page ");
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        }).GeneratePdf(outputPath);

        return Task.CompletedTask;
    }

    private Task<string> GenerateExcelReportAsync(ReportTemplate template, ReportDataSet dataSet, CancellationToken cancellationToken)
    {
        throw new NotSupportedException("Excel generation is not implemented; use CSV or HTML.");
    }

    private Task<string> GenerateCsvReportAsync(ReportTemplate template, ReportDataSet dataSet, CancellationToken cancellationToken)
    {
        var csv = new StringBuilder();
        csv.AppendLine(string.Join(",", dataSet.Columns.Select(EscapeCsvValue)));

        foreach (var row in dataSet.Rows)
        {
            var values = dataSet.Columns.Select(col =>
                EscapeCsvValue(row.TryGetValue(col, out var value) ? value : null));
            csv.AppendLine(string.Join(",", values));
        }

        return Task.FromResult(csv.ToString());
    }

    private Task<string> GenerateJsonReportAsync(ReportTemplate template, ReportDataSet dataSet, CancellationToken cancellationToken)
    {
        var reportData = new
        {
            ReportInfo = new
            {
                Name = template.Name,
                GeneratedAt = DateTime.UtcNow,
                Template = template.TemplateId,
                Format = "JSON"
            },
            Summary = dataSet.DataItems.ToDictionary(item => item.Name, item => item.Value),
            Data = dataSet.Rows,
            Metadata = dataSet.Metadata
        };

        return Task.FromResult(JsonSerializer.Serialize(reportData, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }));
    }

    private Task<string> GenerateXmlReportAsync(ReportTemplate template, ReportDataSet dataSet, CancellationToken cancellationToken)
    {
        var xml = new StringBuilder();
        using var writer = XmlWriter.Create(xml, new XmlWriterSettings { Indent = true, OmitXmlDeclaration = true });
        writer.WriteStartElement("TestReport");
        writer.WriteStartElement("ReportInfo");
        writer.WriteElementString("Name", template.Name);
        writer.WriteElementString("GeneratedAt", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        writer.WriteElementString("Template", template.TemplateId);
        writer.WriteEndElement();
        writer.WriteStartElement("Summary");
        foreach (var item in dataSet.DataItems)
        {
            writer.WriteStartElement("Item");
            writer.WriteAttributeString("name", item.Name);
            writer.WriteString(FormatValue(item.Value));
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
        writer.WriteStartElement("TestResults");
        foreach (var row in dataSet.Rows)
        {
            writer.WriteStartElement("TestResult");
            foreach (var column in dataSet.Columns)
            {
                writer.WriteStartElement("Field");
                writer.WriteAttributeString("name", column);
                writer.WriteString(FormatValue(row.TryGetValue(column, out var value) ? value : null));
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.Flush();
        return Task.FromResult(xml.ToString());
    }

    private static string HtmlEncode(object? value) => WebUtility.HtmlEncode(FormatValue(value));

    private static string FormatValue(object? value) => value switch
    {
        null => string.Empty,
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
        _ => value.ToString() ?? string.Empty
    };

    private static string EscapeCsvValue(object? value)
    {
        var text = FormatValue(value);
        if (text.Length > 0 && text[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            text = "'" + text;
        }

        return $"\"{text.Replace("\"", "\"\"")}\"";
    }

    private object? GetDataValue(ReportDataSet dataSet, string path)
    {
        // 数据路径解析：优先从数据集元数据与既有结构中取值，避免硬编码伪造值
        return path switch
        {
            "session" => dataSet.Metadata.TryGetValue("SessionId", out var sid) ? sid : dataSet.Name,
            "results" => dataSet.Rows,
            "statistics" => dataSet.DataItems,
            "devices" => dataSet.Metadata.TryGetValue("Devices", out var devices) ? devices : null,
            _ => null
        };
    }

    public async Task<ReportGenerationResult> PreviewReportAsync(ReportConfiguration configuration, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.Info($"生成报告预览: {configuration.ReportName}");
            
            // 生成预览版本（通常是HTML格式）
            var previewConfig = configuration with { OutputFormat = ReportFormat.HTML };
            var result = await GenerateReportAsync(previewConfig, cancellationToken);
            
            if (result.Success && !string.IsNullOrEmpty(result.FilePath))
            {
                var content = await File.ReadAllTextAsync(result.FilePath, cancellationToken);
                return result with { Content = Convert.ToBase64String(Encoding.UTF8.GetBytes(content)) };
            }
            
            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.Error($"生成报告预览失败: {ex.Message}");
            return ReportGenerationResult.CreateFailure(ex.Message);
        }
    }

    public Task<bool> ValidateConfigurationAsync(ReportConfiguration configuration, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(configuration.ReportName))
        {
            _logger?.Warning("报告名称不能为空");
            return Task.FromResult(false);
        }

        if (string.IsNullOrWhiteSpace(configuration.OutputPath))
        {
            _logger?.Warning("输出路径不能为空");
            return Task.FromResult(false);
        }

        if (!SupportedFormats.Contains(configuration.OutputFormat))
        {
            _logger?.Warning($"不支持的报告格式: {configuration.OutputFormat}");
            return Task.FromResult(false);
        }

        // 验证输出目录是否存在
        var directory = Path.GetDirectoryName(configuration.OutputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            try
            {
                Directory.CreateDirectory(directory);
            }
            catch (Exception ex)
            {
                _logger?.Error($"创建输出目录失败: {ex.Message}");
                return Task.FromResult(false);
            }
        }

        return Task.FromResult(true);
    }

    public Task<List<ReportTemplate>> GetAvailableTemplatesAsync(ReportType? reportType = null, CancellationToken cancellationToken = default)
    {
        var templates = _templates.Values.AsEnumerable();

        if (reportType.HasValue)
        {
            templates = templates.Where(t => t.ReportType == reportType.Value);
        }

        return Task.FromResult(templates.ToList());
    }

    public Task<bool> CreateTemplateAsync(ReportTemplate template, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_templates.ContainsKey(template.TemplateId))
            {
                _logger?.Warning($"模板已存在: {template.TemplateId}");
                return Task.FromResult(false);
            }

            _templates[template.TemplateId] = template;

            _logger?.Info($"创建报告模板成功: {template.TemplateId}");
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger?.Error($"创建报告模板失败: {ex.Message}");
            return Task.FromResult(false);
        }
    }

    public Task<bool> UpdateTemplateAsync(ReportTemplate template, CancellationToken cancellationToken = default)
    {
        try
        {
            _templates[template.TemplateId] = template;

            _logger?.Info($"更新报告模板成功: {template.TemplateId}");
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger?.Error($"更新报告模板失败: {ex.Message}");
            return Task.FromResult(false);
        }
    }

    public Task<bool> DeleteTemplateAsync(string templateId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_templates.TryRemove(templateId, out _))
            {
                _logger?.Info($"删除报告模板成功: {templateId}");
                return Task.FromResult(true);
            }

            _logger?.Warning($"模板不存在: {templateId}");
            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            _logger?.Error($"删除报告模板失败: {ex.Message}");
            return Task.FromResult(false);
        }
    }

    public async Task<bool> ExportTemplateAsync(string templateId, string exportPath, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_templates.TryGetValue(templateId, out var template))
            {
                _logger?.Warning($"模板不存在: {templateId}");
                return false;
            }
            
            var templateJson = JsonSerializer.Serialize(template, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            
            await File.WriteAllTextAsync(exportPath, templateJson, Encoding.UTF8, cancellationToken);
            
            _logger?.Info($"导出报告模板成功: {templateId} -> {exportPath}");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.Error($"导出报告模板失败: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> ImportTemplateAsync(string importPath, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(importPath))
            {
                _logger?.Warning($"模板文件不存在: {importPath}");
                return false;
            }
            
            var templateJson = await File.ReadAllTextAsync(importPath, cancellationToken);
            var template = JsonSerializer.Deserialize<ReportTemplate>(templateJson);
            
            if (template == null)
            {
                _logger?.Error("模板文件格式无效");
                return false;
            }
            
            _templates[template.TemplateId] = template;
            
            _logger?.Info($"导入报告模板成功: {template.TemplateId} <- {importPath}");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.Error($"导入报告模板失败: {ex.Message}");
            return false;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            // 仅持有托管模板字典，无未托管资源；清空后通知终结器跳过
            _templates.Clear();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
