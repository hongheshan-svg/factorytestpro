using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UTF.Reporting;

/// <summary>
/// 报表类型枚举
/// </summary>
public enum ReportType
{
    /// <summary>测试报告</summary>
    Test,
    /// <summary>统计报告</summary>
    Statistics,
    /// <summary>趋势分析报告</summary>
    TrendAnalysis,
    /// <summary>设备状态报告</summary>
    DeviceStatus,
    /// <summary>生产效率报告</summary>
    ProductionEfficiency,
    /// <summary>质量分析报告</summary>
    QualityAnalysis,
    /// <summary>故障分析报告</summary>
    FailureAnalysis,
    /// <summary>性能监控报告</summary>
    PerformanceMonitoring,
    /// <summary>审计报告</summary>
    Audit,
    /// <summary>自定义报告</summary>
    Custom
}

/// <summary>
/// 报表格式枚举
/// </summary>
public enum ReportFormat
{
    /// <summary>PDF格式</summary>
    PDF,
    /// <summary>Excel格式</summary>
    Excel,
    /// <summary>Word格式</summary>
    Word,
    /// <summary>HTML格式</summary>
    HTML,
    /// <summary>CSV格式</summary>
    CSV,
    /// <summary>JSON格式</summary>
    JSON,
    /// <summary>XML格式</summary>
    XML,
    /// <summary>图片格式</summary>
    Image
}

/// <summary>
/// 报表数据项
/// </summary>
public sealed record ReportDataItem
{
    /// <summary>项目名称</summary>
    public string Name { get; init; } = string.Empty;
    
    /// <summary>项目值</summary>
    public object? Value { get; init; }
    
    /// <summary>数据类型</summary>
    public string DataType { get; init; } = string.Empty;
    
    /// <summary>单位</summary>
    public string? Unit { get; init; }
    
    /// <summary>分类</summary>
    public string Category { get; init; } = string.Empty;
    
    /// <summary>标签</summary>
    public List<string> Tags { get; init; } = new();
    
    /// <summary>扩展属性</summary>
    public Dictionary<string, object> Properties { get; init; } = new();
}

/// <summary>
/// 报表数据集
/// </summary>
public sealed record ReportDataSet
{
    /// <summary>数据集名称</summary>
    public string Name { get; init; } = string.Empty;
    
    /// <summary>数据集描述</summary>
    public string Description { get; init; } = string.Empty;
    
    /// <summary>数据项列表</summary>
    public List<ReportDataItem> DataItems { get; init; } = new();
    
    /// <summary>列定义</summary>
    public List<string> Columns { get; init; } = new();
    
    /// <summary>行数据</summary>
    public List<Dictionary<string, object>> Rows { get; init; } = new();
    
    /// <summary>元数据</summary>
    public Dictionary<string, object> Metadata { get; init; } = new();
}

/// <summary>
/// 图表配置
/// </summary>
public sealed record ChartConfiguration
{
    /// <summary>图表类型</summary>
    public string ChartType { get; init; } = string.Empty;
    
    /// <summary>标题</summary>
    public string Title { get; init; } = string.Empty;
    
    /// <summary>宽度</summary>
    public int Width { get; init; } = 800;
    
    /// <summary>高度</summary>
    public int Height { get; init; } = 600;
    
    /// <summary>X轴标签</summary>
    public string XAxisLabel { get; init; } = string.Empty;
    
    /// <summary>Y轴标签</summary>
    public string YAxisLabel { get; init; } = string.Empty;
    
    /// <summary>数据系列</summary>
    public List<Dictionary<string, object>> Series { get; init; } = new();
    
    /// <summary>颜色方案</summary>
    public List<string> ColorScheme { get; init; } = new();
    
    /// <summary>扩展配置</summary>
    public Dictionary<string, object> ExtendedConfiguration { get; init; } = new();
}

/// <summary>
/// 报表模板
/// </summary>
public sealed record ReportTemplate
{
    /// <summary>模板ID</summary>
    public string TemplateId { get; init; } = string.Empty;
    
    /// <summary>模板名称</summary>
    public string Name { get; init; } = string.Empty;
    
    /// <summary>模板描述</summary>
    public string Description { get; init; } = string.Empty;
    
    /// <summary>报表类型</summary>
    public ReportType ReportType { get; init; }
    
    /// <summary>模板内容</summary>
    public string Content { get; init; } = string.Empty;
    
    /// <summary>样式设置</summary>
    public Dictionary<string, object> Styles { get; init; } = new();
    
    /// <summary>页面设置</summary>
    public Dictionary<string, object> PageSettings { get; init; } = new();
    
    /// <summary>数据绑定配置</summary>
    public Dictionary<string, string> DataBindings { get; init; } = new();
    
    /// <summary>图表配置</summary>
    public List<ChartConfiguration> Charts { get; init; } = new();
    
    /// <summary>创建时间</summary>
    public DateTime CreatedTime { get; init; } = DateTime.UtcNow;
    
    /// <summary>更新时间</summary>
    public DateTime UpdatedTime { get; init; } = DateTime.UtcNow;
    
    /// <summary>版本号</summary>
    public string Version { get; init; } = "1.0";
}

/// <summary>
/// 报表配置
/// </summary>
public sealed record ReportConfiguration
{
    /// <summary>报表名称</summary>
    public string ReportName { get; init; } = string.Empty;
    
    /// <summary>报表类型</summary>
    public ReportType ReportType { get; init; }
    
    /// <summary>输出格式</summary>
    public ReportFormat OutputFormat { get; init; }
    
    /// <summary>使用的模板</summary>
    public string? TemplateId { get; init; }
    
    /// <summary>数据源配置</summary>
    public Dictionary<string, object> DataSourceConfiguration { get; init; } = new();
    
    /// <summary>过滤条件</summary>
    public Dictionary<string, object> Filters { get; init; } = new();
    
    /// <summary>排序设置</summary>
    public List<Dictionary<string, object>> SortSettings { get; init; } = new();
    
    /// <summary>分组设置</summary>
    public List<string> GroupByFields { get; init; } = new();
    
    /// <summary>聚合设置</summary>
    public Dictionary<string, string> AggregateSettings { get; init; } = new();
    
    /// <summary>输出路径</summary>
    public string OutputPath { get; init; } = string.Empty;
    
    /// <summary>是否包含图表</summary>
    public bool IncludeCharts { get; init; } = true;
    
    /// <summary>是否包含统计信息</summary>
    public bool IncludeStatistics { get; init; } = true;
    
    /// <summary>日期范围开始</summary>
    public DateTime? DateRangeStart { get; init; }
    
    /// <summary>日期范围结束</summary>
    public DateTime? DateRangeEnd { get; init; }
    
    /// <summary>扩展配置</summary>
    public Dictionary<string, object> ExtendedConfiguration { get; init; } = new();
}

/// <summary>
/// 报表生成结果
/// </summary>
public sealed record ReportGenerationResult
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }
    
    /// <summary>报表文件路径</summary>
    public string? FilePath { get; init; }
    
    /// <summary>报表内容(base64编码)</summary>
    public string? Content { get; init; }
    
    /// <summary>文件大小(字节)</summary>
    public long FileSize { get; init; }
    
    /// <summary>生成时间</summary>
    public TimeSpan GenerationTime { get; init; }
    
    /// <summary>错误信息</summary>
    public string? ErrorMessage { get; init; }
    
    /// <summary>警告信息</summary>
    public List<string> Warnings { get; init; } = new();
    
    /// <summary>统计信息</summary>
    public Dictionary<string, object> Statistics { get; init; } = new();
    
    /// <summary>扩展数据</summary>
    public Dictionary<string, object> ExtendedData { get; init; } = new();
    
    /// <summary>创建成功结果</summary>
    public static ReportGenerationResult CreateSuccess(string filePath, long fileSize, TimeSpan generationTime)
        => new() { Success = true, FilePath = filePath, FileSize = fileSize, GenerationTime = generationTime };
    
    /// <summary>创建失败结果</summary>
    public static ReportGenerationResult CreateFailure(string errorMessage, TimeSpan generationTime = default)
        => new() { Success = false, ErrorMessage = errorMessage, GenerationTime = generationTime };
}

/// <summary>
/// 报表统计信息
/// </summary>
public sealed record ReportStatistics
{
    /// <summary>总记录数</summary>
    public int TotalRecords { get; init; }
    
    /// <summary>通过测试数</summary>
    public int PassedTests { get; init; }
    
    /// <summary>失败测试数</summary>
    public int FailedTests { get; init; }
    
    /// <summary>通过率</summary>
    public double PassRate => TotalRecords > 0 ? (double)PassedTests / TotalRecords : 0.0;
    
    /// <summary>失败率</summary>
    public double FailureRate => TotalRecords > 0 ? (double)FailedTests / TotalRecords : 0.0;
    
    /// <summary>平均执行时间</summary>
    public TimeSpan AverageExecutionTime { get; init; }
    
    /// <summary>最短执行时间</summary>
    public TimeSpan MinExecutionTime { get; init; }
    
    /// <summary>最长执行时间</summary>
    public TimeSpan MaxExecutionTime { get; init; }
    
    /// <summary>首次通过率</summary>
    public double FirstPassYield { get; init; }
    
    /// <summary>设备利用率</summary>
    public double EquipmentUtilization { get; init; }
    
    /// <summary>缺陷密度</summary>
    public double DefectDensity { get; init; }
    
    /// <summary>分类统计</summary>
    public Dictionary<string, int> CategoryStatistics { get; init; } = new();
    
    /// <summary>趋势数据</summary>
    public Dictionary<DateTime, double> TrendData { get; init; } = new();
    
    /// <summary>扩展统计</summary>
    public Dictionary<string, object> ExtendedStatistics { get; init; } = new();
}

/// <summary>
/// 报表生成器接口
/// </summary>
public interface IReportGenerator
{
    /// <summary>支持的报表格式</summary>
    IReadOnlyList<ReportFormat> SupportedFormats { get; }
    
    /// <summary>生成报表</summary>
    Task<ReportGenerationResult> GenerateReportAsync(ReportConfiguration configuration, CancellationToken cancellationToken = default);
    
    /// <summary>使用模板生成报表</summary>
    Task<ReportGenerationResult> GenerateReportFromTemplateAsync(ReportTemplate template, ReportDataSet dataSet, ReportFormat format, string outputPath, CancellationToken cancellationToken = default);
    
    /// <summary>预览报表</summary>
    Task<ReportGenerationResult> PreviewReportAsync(ReportConfiguration configuration, CancellationToken cancellationToken = default);
    
    /// <summary>验证报表配置</summary>
    Task<bool> ValidateConfigurationAsync(ReportConfiguration configuration, CancellationToken cancellationToken = default);
    
    /// <summary>获取可用模板列表</summary>
    Task<List<ReportTemplate>> GetAvailableTemplatesAsync(ReportType? reportType = null, CancellationToken cancellationToken = default);
    
    /// <summary>创建报表模板</summary>
    Task<bool> CreateTemplateAsync(ReportTemplate template, CancellationToken cancellationToken = default);
    
    /// <summary>更新报表模板</summary>
    Task<bool> UpdateTemplateAsync(ReportTemplate template, CancellationToken cancellationToken = default);
    
    /// <summary>删除报表模板</summary>
    Task<bool> DeleteTemplateAsync(string templateId, CancellationToken cancellationToken = default);
    
    /// <summary>导出报表模板</summary>
    Task<bool> ExportTemplateAsync(string templateId, string exportPath, CancellationToken cancellationToken = default);
    
    /// <summary>导入报表模板</summary>
    Task<bool> ImportTemplateAsync(string importPath, CancellationToken cancellationToken = default);
}

/// <summary>
/// 数据分析器接口
/// </summary>
public interface IDataAnalyzer
{
    /// <summary>计算统计信息</summary>
    Task<ReportStatistics> CalculateStatisticsAsync(ReportDataSet dataSet, CancellationToken cancellationToken = default);
    
    /// <summary>趋势分析</summary>
    Task<Dictionary<string, object>> AnalyzeTrendsAsync(ReportDataSet dataSet, string dateField, List<string> valueFields, CancellationToken cancellationToken = default);
    
    /// <summary>故障分析</summary>
    Task<Dictionary<string, object>> AnalyzeFailuresAsync(ReportDataSet dataSet, CancellationToken cancellationToken = default);
    
    /// <summary>性能分析</summary>
    Task<Dictionary<string, object>> AnalyzePerformanceAsync(ReportDataSet dataSet, CancellationToken cancellationToken = default);
    
    /// <summary>质量分析</summary>
    Task<Dictionary<string, object>> AnalyzeQualityAsync(ReportDataSet dataSet, CancellationToken cancellationToken = default);
    
    /// <summary>异常检测</summary>
    Task<List<Dictionary<string, object>>> DetectAnomaliesAsync(ReportDataSet dataSet, string valueField, double threshold = 2.0, CancellationToken cancellationToken = default);
    
    /// <summary>相关性分析</summary>
    Task<Dictionary<string, double>> AnalyzeCorrelationsAsync(ReportDataSet dataSet, List<string> fields, CancellationToken cancellationToken = default);
}

/// <summary>
/// 图表生成器接口
/// </summary>
public interface IChartGenerator
{
    /// <summary>支持的图表类型</summary>
    IReadOnlyList<string> SupportedChartTypes { get; }
    
    /// <summary>生成图表</summary>
    Task<byte[]> GenerateChartAsync(ChartConfiguration configuration, ReportDataSet dataSet, CancellationToken cancellationToken = default);
    
    /// <summary>生成趋势图</summary>
    Task<byte[]> GenerateTrendChartAsync(ReportDataSet dataSet, string dateField, List<string> valueFields, ChartConfiguration? configuration = null, CancellationToken cancellationToken = default);
    
    /// <summary>生成柱状图</summary>
    Task<byte[]> GenerateBarChartAsync(ReportDataSet dataSet, string categoryField, string valueField, ChartConfiguration? configuration = null, CancellationToken cancellationToken = default);
    
    /// <summary>生成饼图</summary>
    Task<byte[]> GeneratePieChartAsync(ReportDataSet dataSet, string categoryField, string valueField, ChartConfiguration? configuration = null, CancellationToken cancellationToken = default);
    
    /// <summary>生成散点图</summary>
    Task<byte[]> GenerateScatterChartAsync(ReportDataSet dataSet, string xField, string yField, ChartConfiguration? configuration = null, CancellationToken cancellationToken = default);
    
    /// <summary>生成仪表盘</summary>
    Task<byte[]> GenerateDashboardAsync(List<ChartConfiguration> charts, ReportDataSet dataSet, CancellationToken cancellationToken = default);
}
