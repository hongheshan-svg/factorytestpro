using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UTF.Logging;

namespace UTF.Reporting;

/// <summary>
/// 报表生成器实现
/// </summary>
public sealed class ReportGenerator : IReportGenerator, IDisposable
{
    private readonly Dictionary<string, ReportTemplate> _templates = new();
    private readonly UTF.Logging.ILogger? _logger;
    private bool _disposed = false;

    public ReportGenerator(UTF.Logging.ILogger? logger = null)
    {
        _logger = logger;
        InitializeDefaultTemplates();
    }

    public IReadOnlyList<ReportFormat> SupportedFormats => new[]
    {
        ReportFormat.PDF,
        ReportFormat.Excel,
        ReportFormat.HTML,
        ReportFormat.CSV,
        ReportFormat.JSON,
        ReportFormat.XML
    }.ToList().AsReadOnly();

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
        catch (Exception ex)
        {
            var generationTime = DateTime.UtcNow - startTime;
            _logger?.Error($"生成报告失败: {ex.Message}");
            return ReportGenerationResult.CreateFailure(ex.Message, generationTime);
        }
    }

    private async Task<ReportDataSet> GenerateDataSetAsync(ReportConfiguration configuration, CancellationToken cancellationToken)
    {
        await Task.Delay(500, cancellationToken); // 模拟数据获取过程
        
        var dataSet = new ReportDataSet
        {
            Name = configuration.ReportName,
            Description = $"数据集为 {configuration.ReportName}",
            Columns = new List<string> { "SessionId", "DUTId", "TestResult", "ExecutionTime", "Timestamp" },
            Rows = new List<Dictionary<string, object>>(),
            Metadata = new Dictionary<string, object>
            {
                { "GeneratedAt", DateTime.UtcNow },
                { "ReportType", configuration.ReportType },
                { "DataSource", "TestDatabase" }
            }
        };
        
        // 生成模拟数据
        var random = new Random();
        for (int i = 0; i < 100; i++)
        {
            var row = new Dictionary<string, object>
            {
                { "SessionId", $"SESSION_{i / 10 + 1:D3}" },
                { "DUTId", $"DUT_{i % 10 + 1:D3}" },
                { "TestResult", random.Next(100) < 85 ? "PASS" : "FAIL" },
                { "ExecutionTime", TimeSpan.FromSeconds(random.Next(30, 300)) },
                { "Timestamp", DateTime.UtcNow.AddHours(-random.Next(0, 168)) }
            };
            dataSet.Rows.Add(row);
        }
        
        // 添加数据项
        dataSet.DataItems.AddRange(new[]
        {
            new ReportDataItem { Name = "TotalTests", Value = dataSet.Rows.Count, DataType = "int", Category = "Summary" },
            new ReportDataItem { Name = "PassedTests", Value = dataSet.Rows.Count(r => r["TestResult"].ToString() == "PASS"), DataType = "int", Category = "Summary" },
            new ReportDataItem { Name = "FailedTests", Value = dataSet.Rows.Count(r => r["TestResult"].ToString() == "FAIL"), DataType = "int", Category = "Summary" },
            new ReportDataItem { Name = "PassRate", Value = Math.Round((double)dataSet.Rows.Count(r => r["TestResult"].ToString() == "PASS") / dataSet.Rows.Count * 100, 2), DataType = "double", Unit = "%", Category = "Summary" }
        });
        
        return dataSet;
    }

    private async Task<ReportTemplate?> GetTemplateAsync(string? templateId, ReportType reportType)
    {
        await Task.Delay(100); // 模拟模板获取
        
        if (!string.IsNullOrEmpty(templateId) && _templates.TryGetValue(templateId, out var template))
        {
            return template;
        }
        
        // 根据报告类型查找默认模板
        var defaultTemplate = _templates.Values.FirstOrDefault(t => t.ReportType == reportType);
        return defaultTemplate;
    }

    public async Task<ReportGenerationResult> GenerateReportFromTemplateAsync(ReportTemplate template, ReportDataSet dataSet, ReportFormat format, string outputPath, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            _logger?.Info($"使用模板生成报告: {template.Name} -> {format}");
            
            // 根据格式生成报告
            var content = format switch
            {
                ReportFormat.HTML => await GenerateHtmlReportAsync(template, dataSet, cancellationToken),
                ReportFormat.PDF => await GeneratePdfReportAsync(template, dataSet, cancellationToken),
                ReportFormat.Excel => await GenerateExcelReportAsync(template, dataSet, cancellationToken),
                ReportFormat.CSV => await GenerateCsvReportAsync(template, dataSet, cancellationToken),
                ReportFormat.JSON => await GenerateJsonReportAsync(template, dataSet, cancellationToken),
                ReportFormat.XML => await GenerateXmlReportAsync(template, dataSet, cancellationToken),
                _ => throw new NotSupportedException($"不支持的报告格式: {format}")
            };
            
            // 保存报告文件
            await File.WriteAllTextAsync(outputPath, content, Encoding.UTF8, cancellationToken);
            
            var fileInfo = new FileInfo(outputPath);
            var generationTime = DateTime.UtcNow - startTime;
            
            _logger?.Info($"报告文件已保存: {outputPath}, 大小: {fileInfo.Length} 字节");
            
            return ReportGenerationResult.CreateSuccess(outputPath, fileInfo.Length, generationTime);
        }
        catch (Exception ex)
        {
            var generationTime = DateTime.UtcNow - startTime;
            _logger?.Error($"生成报告失败: {ex.Message}");
            return ReportGenerationResult.CreateFailure(ex.Message, generationTime);
        }
    }

    private async Task<string> GenerateHtmlReportAsync(ReportTemplate template, ReportDataSet dataSet, CancellationToken cancellationToken)
    {
        await Task.Delay(200, cancellationToken); // 模拟HTML生成过程
        
        var html = template.Content;
        
        // 替换数据绑定
        foreach (var binding in template.DataBindings)
        {
            var placeholder = $"{{{{{binding.Key}}}}}";
            var value = GetDataValue(dataSet, binding.Value)?.ToString() ?? "";
            html = html.Replace(placeholder, value);
        }
        
        // 替换基本统计信息
        var totalTests = dataSet.DataItems.FirstOrDefault(i => i.Name == "TotalTests")?.Value?.ToString() ?? "0";
        var passedTests = dataSet.DataItems.FirstOrDefault(i => i.Name == "PassedTests")?.Value?.ToString() ?? "0";
        var failedTests = dataSet.DataItems.FirstOrDefault(i => i.Name == "FailedTests")?.Value?.ToString() ?? "0";
        var passRate = dataSet.DataItems.FirstOrDefault(i => i.Name == "PassRate")?.Value?.ToString() ?? "0";
        
        html = html.Replace("{{SessionId}}", "SESSION_001")
                  .Replace("{{TestTime}}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
                  .Replace("{{Operator}}", "System")
                  .Replace("{{TotalTests}}", totalTests)
                  .Replace("{{PassedTests}}", passedTests)
                  .Replace("{{FailedTests}}", failedTests)
                  .Replace("{{PassRate}}", passRate)
                  .Replace("{{ExecutionTime}}", "00:15:30");
        
        // 生成测试结果表格
        var resultsHtml = new StringBuilder();
        foreach (var row in dataSet.Rows.Take(20)) // 只显示前20行
        {
            var resultClass = row["TestResult"].ToString() == "PASS" ? "passed" : "failed";
            resultsHtml.AppendLine($@"
            <tr>
                <td>{row["DUTId"]}</td>
                <td>测试步骤_{row["DUTId"]}</td>
                <td class='{resultClass}'>{row["TestResult"]}</td>
                <td>5.02V</td>
                <td>5.00V</td>
                <td>{row["ExecutionTime"]}</td>
            </tr>");
        }
        
        html = html.Replace("{{#TestResults}}", "").Replace("{{/TestResults}}", "");
        html = html.Replace("<tr>", resultsHtml.ToString());
        
        return html;
    }

    private async Task<string> GeneratePdfReportAsync(ReportTemplate template, ReportDataSet dataSet, CancellationToken cancellationToken)
    {
        await Task.Delay(500, cancellationToken); // 模拟PDF生成过程
        
        // 这里应该使用PDF生成库（如iTextSharp）来生成实际的PDF内容
        // 目前返回PDF的文本表示
        var pdfContent = $@"%PDF-1.4
1 0 obj
<<
/Type /Catalog
/Pages 2 0 R
>>
endobj

2 0 obj
<<
/Type /Pages
/Kids [3 0 R]
/Count 1
>>
endobj

3 0 obj
<<
/Type /Page
/Parent 2 0 R
/MediaBox [0 0 612 792]
/Contents 4 0 R
>>
endobj

4 0 obj
<<
/Length 200
>>
stream
BT
/F1 12 Tf
100 700 Td
(测试报告) Tj
0 -20 Td
(总测试数: {dataSet.DataItems.FirstOrDefault(i => i.Name == "TotalTests")?.Value}) Tj
0 -20 Td
(通过测试: {dataSet.DataItems.FirstOrDefault(i => i.Name == "PassedTests")?.Value}) Tj
0 -20 Td
(失败测试: {dataSet.DataItems.FirstOrDefault(i => i.Name == "FailedTests")?.Value}) Tj
ET
endstream
endobj

xref
0 5
0000000000 65535 f 
0000000009 00000 n 
0000000058 00000 n 
0000000115 00000 n 
0000000206 00000 n 
trailer
<<
/Size 5
/Root 1 0 R
>>
startxref
456
%%EOF";
        
        return pdfContent;
    }

    private async Task<string> GenerateExcelReportAsync(ReportTemplate template, ReportDataSet dataSet, CancellationToken cancellationToken)
    {
        await Task.Delay(300, cancellationToken); // 模拟Excel生成过程
        
        // 这里应该使用Excel生成库（如EPPlus）来生成实际的Excel内容
        // 目前返回CSV格式作为Excel的简化版本
        var csv = new StringBuilder();
        csv.AppendLine(string.Join(",", dataSet.Columns));
        
        foreach (var row in dataSet.Rows)
        {
            var values = dataSet.Columns.Select(col => row.TryGetValue(col, out var value) ? value?.ToString() ?? "" : "");
            csv.AppendLine(string.Join(",", values));
        }
        
        return csv.ToString();
    }

    private async Task<string> GenerateCsvReportAsync(ReportTemplate template, ReportDataSet dataSet, CancellationToken cancellationToken)
    {
        await Task.Delay(100, cancellationToken); // 模拟CSV生成过程
        
        var csv = new StringBuilder();
        csv.AppendLine(string.Join(",", dataSet.Columns));
        
        foreach (var row in dataSet.Rows)
        {
            var values = dataSet.Columns.Select(col => row.TryGetValue(col, out var value) ? value?.ToString() ?? "" : "");
            csv.AppendLine(string.Join(",", values));
        }
        
        return csv.ToString();
    }

    private async Task<string> GenerateJsonReportAsync(ReportTemplate template, ReportDataSet dataSet, CancellationToken cancellationToken)
    {
        await Task.Delay(150, cancellationToken); // 模拟JSON生成过程
        
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
        
        return JsonSerializer.Serialize(reportData, new JsonSerializerOptions 
        { 
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }

    private async Task<string> GenerateXmlReportAsync(ReportTemplate template, ReportDataSet dataSet, CancellationToken cancellationToken)
    {
        await Task.Delay(200, cancellationToken); // 模拟XML生成过程
        
        var xml = new StringBuilder();
        xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        xml.AppendLine("<TestReport>");
        xml.AppendLine($"  <ReportInfo>");
        xml.AppendLine($"    <Name>{template.Name}</Name>");
        xml.AppendLine($"    <GeneratedAt>{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss}</GeneratedAt>");
        xml.AppendLine($"    <Template>{template.TemplateId}</Template>");
        xml.AppendLine($"  </ReportInfo>");
        
        xml.AppendLine("  <Summary>");
        foreach (var item in dataSet.DataItems)
        {
            xml.AppendLine($"    <{item.Name}>{item.Value}</{item.Name}>");
        }
        xml.AppendLine("  </Summary>");
        
        xml.AppendLine("  <TestResults>");
        foreach (var row in dataSet.Rows)
        {
            xml.AppendLine("    <TestResult>");
            foreach (var column in dataSet.Columns)
            {
                var value = row.TryGetValue(column, out var val) ? val?.ToString() ?? "" : "";
                xml.AppendLine($"      <{column}>{value}</{column}>");
            }
            xml.AppendLine("    </TestResult>");
        }
        xml.AppendLine("  </TestResults>");
        
        xml.AppendLine("</TestReport>");
        
        return xml.ToString();
    }

    private object? GetDataValue(ReportDataSet dataSet, string path)
    {
        // 简单的数据路径解析
        return path switch
        {
            "session" => "SESSION_001",
            "results" => dataSet.Rows,
            "statistics" => dataSet.DataItems,
            "devices" => new[] { "DUT_001", "DUT_002", "DUT_003" },
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
        catch (Exception ex)
        {
            _logger?.Error($"生成报告预览失败: {ex.Message}");
            return ReportGenerationResult.CreateFailure(ex.Message);
        }
    }

    public async Task<bool> ValidateConfigurationAsync(ReportConfiguration configuration, CancellationToken cancellationToken = default)
    {
        await Task.Delay(50, cancellationToken); // 模拟验证过程
        
        if (string.IsNullOrWhiteSpace(configuration.ReportName))
        {
            _logger?.Warning("报告名称不能为空");
            return false;
        }
        
        if (string.IsNullOrWhiteSpace(configuration.OutputPath))
        {
            _logger?.Warning("输出路径不能为空");
            return false;
        }
        
        if (!SupportedFormats.Contains(configuration.OutputFormat))
        {
            _logger?.Warning($"不支持的报告格式: {configuration.OutputFormat}");
            return false;
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
                return false;
            }
        }
        
        return true;
    }

    public async Task<List<ReportTemplate>> GetAvailableTemplatesAsync(ReportType? reportType = null, CancellationToken cancellationToken = default)
    {
        await Task.Delay(100, cancellationToken); // 模拟模板查询
        
        var templates = _templates.Values.AsEnumerable();
        
        if (reportType.HasValue)
        {
            templates = templates.Where(t => t.ReportType == reportType.Value);
        }
        
        return templates.ToList();
    }

    public async Task<bool> CreateTemplateAsync(ReportTemplate template, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_templates.ContainsKey(template.TemplateId))
            {
                _logger?.Warning($"模板已存在: {template.TemplateId}");
                return false;
            }
            
            _templates[template.TemplateId] = template;
            
            _logger?.Info($"创建报告模板成功: {template.TemplateId}");
            await Task.Delay(100, cancellationToken);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger?.Error($"创建报告模板失败: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> UpdateTemplateAsync(ReportTemplate template, CancellationToken cancellationToken = default)
    {
        try
        {
            _templates[template.TemplateId] = template;
            
            _logger?.Info($"更新报告模板成功: {template.TemplateId}");
            await Task.Delay(100, cancellationToken);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger?.Error($"更新报告模板失败: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> DeleteTemplateAsync(string templateId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_templates.Remove(templateId))
            {
                _logger?.Info($"删除报告模板成功: {templateId}");
                await Task.Delay(50, cancellationToken);
                return true;
            }
            
            _logger?.Warning($"模板不存在: {templateId}");
            return false;
        }
        catch (Exception ex)
        {
            _logger?.Error($"删除报告模板失败: {ex.Message}");
            return false;
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
            _templates.Clear();
            _disposed = true;
        }
    }
}
