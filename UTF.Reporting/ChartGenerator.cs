using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UTF.Logging;

namespace UTF.Reporting;

/// <summary>
/// 图表生成器实现
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ChartGenerator : IChartGenerator, IDisposable
{
    private readonly UTF.Logging.ILogger? _logger;
    private bool _disposed = false;

    public ChartGenerator(UTF.Logging.ILogger? logger = null)
    {
        _logger = logger;
    }

    public IReadOnlyList<string> SupportedChartTypes => new[]
    {
        "LineChart",
        "BarChart", 
        "PieChart",
        "ScatterChart",
        "AreaChart",
        "ColumnChart",
        "Dashboard"
    }.ToList().AsReadOnly();

    [SupportedOSPlatform("windows")]
    public async Task<byte[]> GenerateChartAsync(ChartConfiguration configuration, ReportDataSet dataSet, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.Info($"生成图表: {configuration.ChartType} - {configuration.Title}");
            
            await Task.Delay(300, cancellationToken); // 模拟图表生成过程
            
            var chartBytes = configuration.ChartType switch
            {
                "LineChart" => await GenerateLineChartAsync(configuration, dataSet, cancellationToken),
                "BarChart" => await GenerateBarChartAsync(configuration, dataSet, cancellationToken),
                "PieChart" => await GeneratePieChartAsync(configuration, dataSet, cancellationToken),
                "ScatterChart" => await GenerateScatterChartAsync(configuration, dataSet, cancellationToken),
                "AreaChart" => await GenerateAreaChartAsync(configuration, dataSet, cancellationToken),
                "ColumnChart" => await GenerateColumnChartAsync(configuration, dataSet, cancellationToken),
                _ => throw new NotSupportedException($"不支持的图表类型: {configuration.ChartType}")
            };
            
            _logger?.Info($"图表生成完成: {configuration.Title}, 大小: {chartBytes.Length} 字节");
            
            return chartBytes;
        }
        catch (Exception ex)
        {
            _logger?.Error($"生成图表失败: {ex.Message}");
            throw;
        }
    }

    [SupportedOSPlatform("windows")]
    private async Task<byte[]> GenerateLineChartAsync(ChartConfiguration configuration, ReportDataSet dataSet, CancellationToken cancellationToken)
    {
        await Task.Delay(200, cancellationToken);
        
        using var bitmap = new Bitmap(configuration.Width, configuration.Height);
        using var graphics = Graphics.FromImage(bitmap);
        
        // 设置背景色
        graphics.Clear(Color.White);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        
        // 绘制标题
        using var titleFont = new Font("Arial", 16, FontStyle.Bold);
        var titleSize = graphics.MeasureString(configuration.Title, titleFont);
        var titleX = (configuration.Width - titleSize.Width) / 2;
        graphics.DrawString(configuration.Title, titleFont, Brushes.Black, titleX, 10);
        
        // 计算绘图区域
        var chartArea = new Rectangle(60, 50, configuration.Width - 120, configuration.Height - 120);
        graphics.DrawRectangle(Pens.Black, chartArea);
        
        // 生成示例数据点
        var dataPoints = GenerateTimeSeriesData(dataSet, 20);
        if (dataPoints.Any())
        {
            // 绘制折线
            var points = new List<PointF>();
            for (int i = 0; i < dataPoints.Count; i++)
            {
                var x = chartArea.X + (float)i / (dataPoints.Count - 1) * chartArea.Width;
                var y = chartArea.Y + chartArea.Height - (float)dataPoints[i] / 100 * chartArea.Height;
                points.Add(new PointF(x, y));
            }
            
            if (points.Count > 1)
            {
                using var pen = new Pen(Color.Blue, 2);
                graphics.DrawLines(pen, points.ToArray());
                
                // 绘制数据点
                foreach (var point in points)
                {
                    graphics.FillEllipse(Brushes.Blue, point.X - 3, point.Y - 3, 6, 6);
                }
            }
        }
        
        // 绘制坐标轴标签
        using var axisFont = new Font("Arial", 10);
        graphics.DrawString(configuration.XAxisLabel, axisFont, Brushes.Black, chartArea.X, chartArea.Bottom + 5);
        
        // 保存为字节数组
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }

    [SupportedOSPlatform("windows")]
    private async Task<byte[]> GenerateBarChartAsync(ChartConfiguration configuration, ReportDataSet dataSet, CancellationToken cancellationToken)
    {
        await Task.Delay(200, cancellationToken);
        
        using var bitmap = new Bitmap(configuration.Width, configuration.Height);
        using var graphics = Graphics.FromImage(bitmap);
        
        graphics.Clear(Color.White);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        
        // 绘制标题
        using var titleFont = new Font("Arial", 16, FontStyle.Bold);
        var titleSize = graphics.MeasureString(configuration.Title, titleFont);
        var titleX = (configuration.Width - titleSize.Width) / 2;
        graphics.DrawString(configuration.Title, titleFont, Brushes.Black, titleX, 10);
        
        // 计算绘图区域
        var chartArea = new Rectangle(80, 50, configuration.Width - 160, configuration.Height - 120);
        graphics.DrawRectangle(Pens.Black, chartArea);
        
        // 生成示例数据
        var categories = new[] { "通过", "失败", "跳过", "错误", "超时" };
        var values = new[] { 85, 10, 3, 1, 1 };
        var colors = new[] { Color.Green, Color.Red, Color.Orange, Color.Purple, Color.Gray };
        
        // 绘制条形图
        var barWidth = chartArea.Width / categories.Length * 0.8f;
        var spacing = chartArea.Width / categories.Length * 0.2f;
        var maxValue = values.Max();
        
        for (int i = 0; i < categories.Length; i++)
        {
            var barHeight = (float)values[i] / maxValue * chartArea.Height;
            var x = chartArea.X + i * (barWidth + spacing) + spacing / 2;
            var y = chartArea.Bottom - barHeight;
            
            var barRect = new RectangleF(x, y, barWidth, barHeight);
            using var brush = new SolidBrush(colors[i]);
            graphics.FillRectangle(brush, barRect);
            graphics.DrawRectangle(Pens.Black, Rectangle.Round(barRect));
            
            // 绘制数值标签
            using var valueFont = new Font("Arial", 9);
            var valueText = values[i].ToString();
            var valueSize = graphics.MeasureString(valueText, valueFont);
            graphics.DrawString(valueText, valueFont, Brushes.Black, 
                x + (barWidth - valueSize.Width) / 2, y - valueSize.Height - 2);
            
            // 绘制类别标签
            using var categoryFont = new Font("Arial", 8);
            var categorySize = graphics.MeasureString(categories[i], categoryFont);
            graphics.DrawString(categories[i], categoryFont, Brushes.Black,
                x + (barWidth - categorySize.Width) / 2, chartArea.Bottom + 5);
        }
        
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }

    [SupportedOSPlatform("windows")]
    private async Task<byte[]> GeneratePieChartAsync(ChartConfiguration configuration, ReportDataSet dataSet, CancellationToken cancellationToken)
    {
        await Task.Delay(200, cancellationToken);
        
        using var bitmap = new Bitmap(configuration.Width, configuration.Height);
        using var graphics = Graphics.FromImage(bitmap);
        
        graphics.Clear(Color.White);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        
        // 绘制标题
        using var titleFont = new Font("Arial", 16, FontStyle.Bold);
        var titleSize = graphics.MeasureString(configuration.Title, titleFont);
        var titleX = (configuration.Width - titleSize.Width) / 2;
        graphics.DrawString(configuration.Title, titleFont, Brushes.Black, titleX, 10);
        
        // 计算饼图区域
        var pieSize = Math.Min(configuration.Width - 200, configuration.Height - 150);
        var pieRect = new Rectangle(
            (configuration.Width - pieSize) / 2,
            50 + (configuration.Height - 150 - pieSize) / 2,
            pieSize,
            pieSize
        );
        
        // 示例数据
        var data = new[] { 
            new { Label = "通过", Value = 85f, Color = Color.Green },
            new { Label = "失败", Value = 10f, Color = Color.Red },
            new { Label = "跳过", Value = 3f, Color = Color.Orange },
            new { Label = "错误", Value = 2f, Color = Color.Purple }
        };
        
        var total = data.Sum(d => d.Value);
        float startAngle = 0;
        
        // 绘制饼图扇形
        foreach (var item in data)
        {
            var sweepAngle = item.Value / total * 360;
            
            using var brush = new SolidBrush(item.Color);
            graphics.FillPie(brush, pieRect, startAngle, sweepAngle);
            graphics.DrawPie(Pens.Black, pieRect, startAngle, sweepAngle);
            
            startAngle += sweepAngle;
        }
        
        // 绘制图例
        var legendX = pieRect.Right + 20;
        var legendY = pieRect.Y + 20;
        using var legendFont = new Font("Arial", 10);
        
        for (int i = 0; i < data.Length; i++)
        {
            var item = data[i];
            var y = legendY + i * 25;
            
            // 颜色方块
            using var brush = new SolidBrush(item.Color);
            graphics.FillRectangle(brush, legendX, y, 15, 15);
            graphics.DrawRectangle(Pens.Black, legendX, y, 15, 15);
            
            // 标签和百分比
            var percentage = item.Value / total * 100;
            var text = $"{item.Label} ({percentage:F1}%)";
            graphics.DrawString(text, legendFont, Brushes.Black, legendX + 20, y);
        }
        
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }

    [SupportedOSPlatform("windows")]
    private async Task<byte[]> GenerateScatterChartAsync(ChartConfiguration configuration, ReportDataSet dataSet, CancellationToken cancellationToken)
    {
        await Task.Delay(200, cancellationToken);
        
        using var bitmap = new Bitmap(configuration.Width, configuration.Height);
        using var graphics = Graphics.FromImage(bitmap);
        
        graphics.Clear(Color.White);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        
        // 绘制标题
        using var titleFont = new Font("Arial", 16, FontStyle.Bold);
        var titleSize = graphics.MeasureString(configuration.Title, titleFont);
        var titleX = (configuration.Width - titleSize.Width) / 2;
        graphics.DrawString(configuration.Title, titleFont, Brushes.Black, titleX, 10);
        
        // 计算绘图区域
        var chartArea = new Rectangle(60, 50, configuration.Width - 120, configuration.Height - 120);
        graphics.DrawRectangle(Pens.Black, chartArea);
        
        // 生成随机散点数据
        var random = new Random();
        var points = new List<PointF>();
        
        for (int i = 0; i < 50; i++)
        {
            var x = chartArea.X + random.NextSingle() * chartArea.Width;
            var y = chartArea.Y + random.NextSingle() * chartArea.Height;
            points.Add(new PointF(x, y));
        }
        
        // 绘制散点
        foreach (var point in points)
        {
            var color = Color.FromArgb(150, Color.Blue);
            using var brush = new SolidBrush(color);
            graphics.FillEllipse(brush, point.X - 4, point.Y - 4, 8, 8);
            graphics.DrawEllipse(Pens.DarkBlue, point.X - 4, point.Y - 4, 8, 8);
        }
        
        // 绘制坐标轴标签
        using var axisFont = new Font("Arial", 10);
        graphics.DrawString(configuration.XAxisLabel, axisFont, Brushes.Black, 
            chartArea.X + (chartArea.Width - graphics.MeasureString(configuration.XAxisLabel, axisFont).Width) / 2, 
            chartArea.Bottom + 5);
        
        // Y轴标签（垂直文本）
        var yLabelSize = graphics.MeasureString(configuration.YAxisLabel, axisFont);
        graphics.TranslateTransform(15, chartArea.Y + (chartArea.Height + yLabelSize.Width) / 2);
        graphics.RotateTransform(-90);
        graphics.DrawString(configuration.YAxisLabel, axisFont, Brushes.Black, 0, 0);
        graphics.ResetTransform();
        
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }

    [SupportedOSPlatform("windows")]
    private async Task<byte[]> GenerateAreaChartAsync(ChartConfiguration configuration, ReportDataSet dataSet, CancellationToken cancellationToken)
    {
        await Task.Delay(200, cancellationToken);
        
        using var bitmap = new Bitmap(configuration.Width, configuration.Height);
        using var graphics = Graphics.FromImage(bitmap);
        
        graphics.Clear(Color.White);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        
        // 绘制标题
        using var titleFont = new Font("Arial", 16, FontStyle.Bold);
        var titleSize = graphics.MeasureString(configuration.Title, titleFont);
        var titleX = (configuration.Width - titleSize.Width) / 2;
        graphics.DrawString(configuration.Title, titleFont, Brushes.Black, titleX, 10);
        
        // 计算绘图区域
        var chartArea = new Rectangle(60, 50, configuration.Width - 120, configuration.Height - 120);
        graphics.DrawRectangle(Pens.Black, chartArea);
        
        // 生成面积图数据
        var dataPoints = GenerateTimeSeriesData(dataSet, 15);
        if (dataPoints.Any())
        {
            var points = new List<PointF>();
            
            // 添加起始点（底部左侧）
            points.Add(new PointF(chartArea.X, chartArea.Bottom));
            
            // 添加数据点
            for (int i = 0; i < dataPoints.Count; i++)
            {
                var x = chartArea.X + (float)i / (dataPoints.Count - 1) * chartArea.Width;
                var y = chartArea.Y + chartArea.Height - (float)dataPoints[i] / 100 * chartArea.Height;
                points.Add(new PointF(x, y));
            }
            
            // 添加结束点（底部右侧）
            points.Add(new PointF(chartArea.Right, chartArea.Bottom));
            
            // 填充面积
            using var brush = new SolidBrush(Color.FromArgb(100, Color.Blue));
            graphics.FillPolygon(brush, points.ToArray());
            
            // 绘制边界线
            using var pen = new Pen(Color.Blue, 2);
            var topPoints = points.Skip(1).Take(points.Count - 2).ToArray();
            if (topPoints.Length > 1)
            {
                graphics.DrawLines(pen, topPoints);
            }
        }
        
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }

    [SupportedOSPlatform("windows")]
    private async Task<byte[]> GenerateColumnChartAsync(ChartConfiguration configuration, ReportDataSet dataSet, CancellationToken cancellationToken)
    {
        await Task.Delay(200, cancellationToken);
        
        // 柱状图与条形图类似，但是垂直方向
        using var bitmap = new Bitmap(configuration.Width, configuration.Height);
        using var graphics = Graphics.FromImage(bitmap);
        
        graphics.Clear(Color.White);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        
        // 绘制标题
        using var titleFont = new Font("Arial", 16, FontStyle.Bold);
        var titleSize = graphics.MeasureString(configuration.Title, titleFont);
        var titleX = (configuration.Width - titleSize.Width) / 2;
        graphics.DrawString(configuration.Title, titleFont, Brushes.Black, titleX, 10);
        
        // 计算绘图区域
        var chartArea = new Rectangle(80, 50, configuration.Width - 160, configuration.Height - 120);
        graphics.DrawRectangle(Pens.Black, chartArea);
        
        // 示例数据
        var months = new[] { "1月", "2月", "3月", "4月", "5月", "6月" };
        var values = new[] { 75, 82, 78, 85, 90, 88 };
        var colors = new[] { Color.CornflowerBlue, Color.LightSeaGreen, Color.Orange, Color.MediumPurple, Color.IndianRed, Color.Gold };
        
        // 绘制柱状图
        var columnWidth = chartArea.Width / months.Length * 0.7f;
        var spacing = chartArea.Width / months.Length * 0.3f;
        var maxValue = values.Max();
        
        for (int i = 0; i < months.Length; i++)
        {
            var columnHeight = (float)values[i] / maxValue * chartArea.Height;
            var x = chartArea.X + i * (columnWidth + spacing) + spacing / 2;
            var y = chartArea.Bottom - columnHeight;
            
            var columnRect = new RectangleF(x, y, columnWidth, columnHeight);
            using var brush = new SolidBrush(colors[i % colors.Length]);
            graphics.FillRectangle(brush, columnRect);
            graphics.DrawRectangle(Pens.Black, Rectangle.Round(columnRect));
            
            // 绘制数值标签
            using var valueFont = new Font("Arial", 9);
            var valueText = $"{values[i]}%";
            var valueSize = graphics.MeasureString(valueText, valueFont);
            graphics.DrawString(valueText, valueFont, Brushes.Black, 
                x + (columnWidth - valueSize.Width) / 2, y - valueSize.Height - 2);
            
            // 绘制月份标签
            using var monthFont = new Font("Arial", 8);
            var monthSize = graphics.MeasureString(months[i], monthFont);
            graphics.DrawString(months[i], monthFont, Brushes.Black,
                x + (columnWidth - monthSize.Width) / 2, chartArea.Bottom + 5);
        }
        
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }

    private List<double> GenerateTimeSeriesData(ReportDataSet dataSet, int pointCount)
    {
        var random = new Random();
        var data = new List<double>();
        
        // 如果数据集中有实际数据，尝试使用
        if (dataSet.Rows.Any())
        {
            var passRate = dataSet.DataItems.FirstOrDefault(i => i.Name == "PassRate")?.Value;
            if (passRate is double rate)
            {
                // 基于实际通过率生成模拟趋势数据
                var baseValue = rate;
                for (int i = 0; i < pointCount; i++)
                {
                    var variation = (random.NextDouble() - 0.5) * 10; // ±5% 变化
                    data.Add(Math.Max(0, Math.Min(100, baseValue + variation)));
                }
            }
        }
        
        // 如果没有实际数据，生成模拟数据
        if (!data.Any())
        {
            var baseValue = 80.0;
            for (int i = 0; i < pointCount; i++)
            {
                var trend = Math.Sin(i * 0.3) * 5; // 添加一些趋势
                var noise = (random.NextDouble() - 0.5) * 8; // 随机噪声
                data.Add(Math.Max(60, Math.Min(95, baseValue + trend + noise)));
            }
        }
        
        return data;
    }

    [SupportedOSPlatform("windows")]
    public async Task<byte[]> GenerateTrendChartAsync(ReportDataSet dataSet, string dateField, List<string> valueFields, ChartConfiguration? configuration = null, CancellationToken cancellationToken = default)
    {
        var config = configuration ?? new ChartConfiguration
        {
            ChartType = "LineChart",
            Title = "趋势分析图",
            Width = 800,
            Height = 600,
            XAxisLabel = "时间",
            YAxisLabel = "数值"
        };
        
        _logger?.Info($"生成趋势图: {string.Join(", ", valueFields)}");
        
        return await GenerateLineChartAsync(config, dataSet, cancellationToken);
    }

    [SupportedOSPlatform("windows")]
    public async Task<byte[]> GenerateBarChartAsync(ReportDataSet dataSet, string categoryField, string valueField, ChartConfiguration? configuration = null, CancellationToken cancellationToken = default)
    {
        var config = configuration ?? new ChartConfiguration
        {
            ChartType = "BarChart",
            Title = "条形图",
            Width = 600,
            Height = 400,
            XAxisLabel = categoryField,
            YAxisLabel = valueField
        };
        
        _logger?.Info($"生成条形图: {categoryField} vs {valueField}");
        
        return await GenerateBarChartAsync(config, dataSet, cancellationToken);
    }

    [SupportedOSPlatform("windows")]
    public async Task<byte[]> GeneratePieChartAsync(ReportDataSet dataSet, string categoryField, string valueField, ChartConfiguration? configuration = null, CancellationToken cancellationToken = default)
    {
        var config = configuration ?? new ChartConfiguration
        {
            ChartType = "PieChart",
            Title = "饼图",
            Width = 500,
            Height = 400
        };
        
        _logger?.Info($"生成饼图: {categoryField} vs {valueField}");
        
        return await GeneratePieChartAsync(config, dataSet, cancellationToken);
    }

    [SupportedOSPlatform("windows")]
    public async Task<byte[]> GenerateScatterChartAsync(ReportDataSet dataSet, string xField, string yField, ChartConfiguration? configuration = null, CancellationToken cancellationToken = default)
    {
        var config = configuration ?? new ChartConfiguration
        {
            ChartType = "ScatterChart",
            Title = "散点图",
            Width = 600,
            Height = 400,
            XAxisLabel = xField,
            YAxisLabel = yField
        };
        
        _logger?.Info($"生成散点图: {xField} vs {yField}");
        
        return await GenerateScatterChartAsync(config, dataSet, cancellationToken);
    }

    [SupportedOSPlatform("windows")]
    public async Task<byte[]> GenerateDashboardAsync(List<ChartConfiguration> charts, ReportDataSet dataSet, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.Info($"生成仪表盘: {charts.Count} 个图表");
            
            await Task.Delay(500, cancellationToken); // 模拟仪表盘生成过程
            
            // 计算仪表盘布局
            var dashboardWidth = 1200;
            var dashboardHeight = 800;
            var cols = Math.Min(2, charts.Count);
            var rows = (int)Math.Ceiling((double)charts.Count / cols);
            
            var chartWidth = dashboardWidth / cols - 20;
            var chartHeight = dashboardHeight / rows - 20;
            
            using var bitmap = new Bitmap(dashboardWidth, dashboardHeight);
            using var graphics = Graphics.FromImage(bitmap);
            
            graphics.Clear(Color.WhiteSmoke);
            
            // 绘制仪表盘标题
            using var titleFont = new Font("Arial", 20, FontStyle.Bold);
            var title = "测试数据仪表盘";
            var titleSize = graphics.MeasureString(title, titleFont);
            var titleX = (dashboardWidth - titleSize.Width) / 2;
            graphics.DrawString(title, titleFont, Brushes.Black, titleX, 10);
            
            // 生成每个图表并放置到仪表盘中
            for (int i = 0; i < charts.Count; i++)
            {
                var row = i / cols;
                var col = i % cols;
                
                var x = col * (chartWidth + 10) + 10;
                var y = row * (chartHeight + 10) + 60;
                
                // 调整图表配置以适应仪表盘
                var chartConfig = charts[i] with 
                { 
                    Width = chartWidth, 
                    Height = chartHeight 
                };
                
                // 生成单个图表
                var chartBytes = await GenerateChartAsync(chartConfig, dataSet, cancellationToken);
                
                // 将图表绘制到仪表盘上
                using var chartStream = new MemoryStream(chartBytes);
                using var chartImage = Image.FromStream(chartStream);
                graphics.DrawImage(chartImage, x, y, chartWidth, chartHeight);
                
                // 绘制边框
                graphics.DrawRectangle(Pens.Gray, x, y, chartWidth, chartHeight);
            }
            
            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            
            _logger?.Info($"仪表盘生成完成: {charts.Count} 个图表, 大小: {stream.Length} 字节");
            
            return stream.ToArray();
        }
        catch (Exception ex)
        {
            _logger?.Error($"生成仪表盘失败: {ex.Message}");
            throw;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}
