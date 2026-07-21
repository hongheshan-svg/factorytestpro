using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using UTF.Logging;

namespace UTF.Reporting;

/// <summary>
/// 图表生成器实现。基于 GDI+ 绘制 PNG 图表。
/// 数据均取自传入的 <see cref="ReportDataSet"/>；无数据时绘制"无数据"占位图。
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

            // 仅在取消时让出；不再以 Task.Delay 模拟生成耗时
            cancellationToken.ThrowIfCancellationRequested();

            var chartBytes = configuration.ChartType switch
            {
                "LineChart" => await GenerateLineChartAsync(configuration, dataSet, cancellationToken).ConfigureAwait(false),
                "BarChart" => await GenerateBarChartAsync(configuration, dataSet, cancellationToken).ConfigureAwait(false),
                "PieChart" => await GeneratePieChartAsync(configuration, dataSet, cancellationToken).ConfigureAwait(false),
                "ScatterChart" => await GenerateScatterChartAsync(configuration, dataSet, cancellationToken).ConfigureAwait(false),
                "AreaChart" => await GenerateAreaChartAsync(configuration, dataSet, cancellationToken).ConfigureAwait(false),
                "ColumnChart" => await GenerateColumnChartAsync(configuration, dataSet, cancellationToken).ConfigureAwait(false),
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
    private Task<byte[]> GenerateLineChartAsync(ChartConfiguration configuration, ReportDataSet dataSet, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var bitmap = new Bitmap(configuration.Width, configuration.Height);
        using var graphics = Graphics.FromImage(bitmap);

        graphics.Clear(Color.White);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        using var titleFont = new Font("Arial", 16, FontStyle.Bold);
        var titleSize = graphics.MeasureString(configuration.Title, titleFont);
        var titleX = (configuration.Width - titleSize.Width) / 2;
        graphics.DrawString(configuration.Title, titleFont, Brushes.Black, titleX, 10);

        var chartArea = new Rectangle(60, 50, configuration.Width - 120, configuration.Height - 120);
        graphics.DrawRectangle(Pens.Black, chartArea);

        // 从数据集中抽取数值序列；无数据时绘制占位
        var dataPoints = ExtractNumericSeries(dataSet);
        if (dataPoints.Count > 0)
        {
            var points = new List<PointF>();
            var max = dataPoints.Max();
            var min = dataPoints.Min();
            var range = max - min;
            if (range <= 0) range = 1;

            for (int i = 0; i < dataPoints.Count; i++)
            {
                var x = dataPoints.Count == 1
                    ? chartArea.X + chartArea.Width / 2f
                    : chartArea.X + (float)i / (dataPoints.Count - 1) * chartArea.Width;
                var y = (float)(chartArea.Y + chartArea.Height - (dataPoints[i] - min) / range * chartArea.Height);
                points.Add(new PointF(x, y));
            }

            if (points.Count > 1)
            {
                using var pen = new Pen(Color.Blue, 2);
                graphics.DrawLines(pen, points.ToArray());

                foreach (var point in points)
                {
                    graphics.FillEllipse(Brushes.Blue, point.X - 3, point.Y - 3, 6, 6);
                }
            }
        }
        else
        {
            DrawNoData(graphics, chartArea);
        }

        using var axisFont = new Font("Arial", 10);
        graphics.DrawString(configuration.XAxisLabel, axisFont, Brushes.Black, chartArea.X, chartArea.Bottom + 5);

        return Task.FromResult(SaveAsPng(bitmap));
    }

    [SupportedOSPlatform("windows")]
    private Task<byte[]> GenerateBarChartAsync(ChartConfiguration configuration, ReportDataSet dataSet, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var bitmap = new Bitmap(configuration.Width, configuration.Height);
        using var graphics = Graphics.FromImage(bitmap);

        graphics.Clear(Color.White);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        using var titleFont = new Font("Arial", 16, FontStyle.Bold);
        var titleSize = graphics.MeasureString(configuration.Title, titleFont);
        var titleX = (configuration.Width - titleSize.Width) / 2;
        graphics.DrawString(configuration.Title, titleFont, Brushes.Black, titleX, 10);

        var chartArea = new Rectangle(80, 50, configuration.Width - 160, configuration.Height - 120);
        graphics.DrawRectangle(Pens.Black, chartArea);

        // 从数据集中按类别聚合（默认按 TestResult 分组计数）
        var categoryData = ExtractCategoryCounts(dataSet);
        if (categoryData.Count > 0)
        {
            var categories = categoryData.Select(c => c.Label).ToArray();
            var values = categoryData.Select(c => (double)c.Value).ToArray();
            var colors = BuildCategoryColors(categories.Length);

            var barWidth = chartArea.Width / categories.Length * 0.8f;
            var spacing = chartArea.Width / categories.Length * 0.2f;
            var maxValue = values.Max();
            if (maxValue <= 0) maxValue = 1;

            for (int i = 0; i < categories.Length; i++)
            {
                var barHeight = (float)((float)values[i] / maxValue * chartArea.Height);
                var x = (float)(chartArea.X + i * (barWidth + spacing) + spacing / 2);
                var y = (float)(chartArea.Bottom - barHeight);

                var barRect = new RectangleF(x, y, barWidth, barHeight);
                using var brush = new SolidBrush(colors[i]);
                graphics.FillRectangle(brush, barRect);
                graphics.DrawRectangle(Pens.Black, Rectangle.Round(barRect));

                using var valueFont = new Font("Arial", 9);
                var valueText = values[i].ToString("0.#");
                var valueSize = graphics.MeasureString(valueText, valueFont);
                graphics.DrawString(valueText, valueFont, Brushes.Black,
                    (float)(x + (barWidth - valueSize.Width) / 2), (float)(y - valueSize.Height - 2));

                using var categoryFont = new Font("Arial", 8);
                var categorySize = graphics.MeasureString(categories[i], categoryFont);
                graphics.DrawString(categories[i], categoryFont, Brushes.Black,
                    x + (barWidth - categorySize.Width) / 2, chartArea.Bottom + 5);
            }
        }
        else
        {
            DrawNoData(graphics, chartArea);
        }

        return Task.FromResult(SaveAsPng(bitmap));
    }

    [SupportedOSPlatform("windows")]
    private Task<byte[]> GeneratePieChartAsync(ChartConfiguration configuration, ReportDataSet dataSet, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var bitmap = new Bitmap(configuration.Width, configuration.Height);
        using var graphics = Graphics.FromImage(bitmap);

        graphics.Clear(Color.White);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        using var titleFont = new Font("Arial", 16, FontStyle.Bold);
        var titleSize = graphics.MeasureString(configuration.Title, titleFont);
        var titleX = (configuration.Width - titleSize.Width) / 2;
        graphics.DrawString(configuration.Title, titleFont, Brushes.Black, titleX, 10);

        var pieSize = Math.Min(configuration.Width - 200, configuration.Height - 150);
        var pieRect = new Rectangle(
            (configuration.Width - pieSize) / 2,
            50 + (configuration.Height - 150 - pieSize) / 2,
            pieSize,
            pieSize
        );

        var categoryData = ExtractCategoryCounts(dataSet);
        if (categoryData.Count > 0)
        {
            var colors = BuildCategoryColors(categoryData.Count);
            var total = categoryData.Sum(c => c.Value);
            if (total <= 0) total = 1;
            float startAngle = 0;

            for (int i = 0; i < categoryData.Count; i++)
            {
                var item = categoryData[i];
                var sweepAngle = item.Value / total * 360;

                using var brush = new SolidBrush(colors[i]);
                graphics.FillPie(brush, pieRect, (float)startAngle, (float)sweepAngle);
                graphics.DrawPie(Pens.Black, pieRect, (float)startAngle, (float)sweepAngle);

                startAngle += (float)sweepAngle;
            }

            // 绘制图例
            var legendX = pieRect.Right + 20;
            var legendY = pieRect.Y + 20;
            using var legendFont = new Font("Arial", 10);

            for (int i = 0; i < categoryData.Count; i++)
            {
                var item = categoryData[i];
                var y = legendY + i * 25;

                using var brush = new SolidBrush(colors[i]);
                graphics.FillRectangle(brush, legendX, y, 15, 15);
                graphics.DrawRectangle(Pens.Black, legendX, y, 15, 15);

                var percentage = item.Value / total * 100;
                var text = $"{item.Label} ({percentage:F1}%)";
                graphics.DrawString(text, legendFont, Brushes.Black, legendX + 20, y);
            }
        }
        else
        {
            DrawNoData(graphics, pieRect);
        }

        return Task.FromResult(SaveAsPng(bitmap));
    }

    [SupportedOSPlatform("windows")]
    private Task<byte[]> GenerateScatterChartAsync(ChartConfiguration configuration, ReportDataSet dataSet, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var bitmap = new Bitmap(configuration.Width, configuration.Height);
        using var graphics = Graphics.FromImage(bitmap);

        graphics.Clear(Color.White);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        using var titleFont = new Font("Arial", 16, FontStyle.Bold);
        var titleSize = graphics.MeasureString(configuration.Title, titleFont);
        var titleX = (configuration.Width - titleSize.Width) / 2;
        graphics.DrawString(configuration.Title, titleFont, Brushes.Black, titleX, 10);

        var chartArea = new Rectangle(60, 50, configuration.Width - 120, configuration.Height - 120);
        graphics.DrawRectangle(Pens.Black, chartArea);

        // 从数据集中抽取两列数值作为 (x, y)；无数据时绘制占位
        var points = ExtractScatterPoints(dataSet, chartArea);
        if (points.Count > 0)
        {
            foreach (var point in points)
            {
                var color = Color.FromArgb(150, Color.Blue);
                using var brush = new SolidBrush(color);
                graphics.FillEllipse(brush, point.X - 4, point.Y - 4, 8, 8);
                graphics.DrawEllipse(Pens.DarkBlue, point.X - 4, point.Y - 4, 8, 8);
            }
        }
        else
        {
            DrawNoData(graphics, chartArea);
        }

        using var axisFont = new Font("Arial", 10);
        graphics.DrawString(configuration.XAxisLabel, axisFont, Brushes.Black,
            chartArea.X + (chartArea.Width - graphics.MeasureString(configuration.XAxisLabel, axisFont).Width) / 2,
            chartArea.Bottom + 5);

        var yLabelSize = graphics.MeasureString(configuration.YAxisLabel, axisFont);
        graphics.TranslateTransform(15, chartArea.Y + (chartArea.Height + yLabelSize.Width) / 2);
        graphics.RotateTransform(-90);
        graphics.DrawString(configuration.YAxisLabel, axisFont, Brushes.Black, 0, 0);
        graphics.ResetTransform();

        return Task.FromResult(SaveAsPng(bitmap));
    }

    [SupportedOSPlatform("windows")]
    private Task<byte[]> GenerateAreaChartAsync(ChartConfiguration configuration, ReportDataSet dataSet, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var bitmap = new Bitmap(configuration.Width, configuration.Height);
        using var graphics = Graphics.FromImage(bitmap);

        graphics.Clear(Color.White);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        using var titleFont = new Font("Arial", 16, FontStyle.Bold);
        var titleSize = graphics.MeasureString(configuration.Title, titleFont);
        var titleX = (configuration.Width - titleSize.Width) / 2;
        graphics.DrawString(configuration.Title, titleFont, Brushes.Black, titleX, 10);

        var chartArea = new Rectangle(60, 50, configuration.Width - 120, configuration.Height - 120);
        graphics.DrawRectangle(Pens.Black, chartArea);

        var dataPoints = ExtractNumericSeries(dataSet);
        if (dataPoints.Count > 0)
        {
            var points = new List<PointF>();
            points.Add(new PointF(chartArea.X, chartArea.Bottom));

            var max = dataPoints.Max();
            var min = dataPoints.Min();
            var range = max - min;
            if (range <= 0) range = 1;

            for (int i = 0; i < dataPoints.Count; i++)
            {
                var x = dataPoints.Count == 1
                    ? chartArea.X + chartArea.Width / 2f
                    : chartArea.X + (float)i / (dataPoints.Count - 1) * chartArea.Width;
                var y = (float)(chartArea.Y + chartArea.Height - (dataPoints[i] - min) / range * chartArea.Height);
                points.Add(new PointF(x, y));
            }

            points.Add(new PointF(chartArea.Right, chartArea.Bottom));

            using var brush = new SolidBrush(Color.FromArgb(100, Color.Blue));
            graphics.FillPolygon(brush, points.ToArray());

            using var pen = new Pen(Color.Blue, 2);
            var topPoints = points.Skip(1).Take(points.Count - 2).ToArray();
            if (topPoints.Length > 1)
            {
                graphics.DrawLines(pen, topPoints);
            }
        }
        else
        {
            DrawNoData(graphics, chartArea);
        }

        return Task.FromResult(SaveAsPng(bitmap));
    }

    [SupportedOSPlatform("windows")]
    private Task<byte[]> GenerateColumnChartAsync(ChartConfiguration configuration, ReportDataSet dataSet, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var bitmap = new Bitmap(configuration.Width, configuration.Height);
        using var graphics = Graphics.FromImage(bitmap);

        graphics.Clear(Color.White);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        using var titleFont = new Font("Arial", 16, FontStyle.Bold);
        var titleSize = graphics.MeasureString(configuration.Title, titleFont);
        var titleX = (configuration.Width - titleSize.Width) / 2;
        graphics.DrawString(configuration.Title, titleFont, Brushes.Black, titleX, 10);

        var chartArea = new Rectangle(80, 50, configuration.Width - 160, configuration.Height - 120);
        graphics.DrawRectangle(Pens.Black, chartArea);

        // 柱状图按类别聚合（与条形图同源），垂直方向绘制
        var categoryData = ExtractCategoryCounts(dataSet);
        if (categoryData.Count > 0)
        {
            var categories = categoryData.Select(c => c.Label).ToArray();
            var values = categoryData.Select(c => (double)c.Value).ToArray();
            var colors = BuildCategoryColors(categories.Length);

            var columnWidth = chartArea.Width / categories.Length * 0.7f;
            var spacing = chartArea.Width / categories.Length * 0.3f;
            var maxValue = values.Max();
            if (maxValue <= 0) maxValue = 1;

            for (int i = 0; i < categories.Length; i++)
            {
                var columnHeight = (float)((float)values[i] / maxValue * chartArea.Height);
                var x = (float)(chartArea.X + i * (columnWidth + spacing) + spacing / 2);
                var y = (float)(chartArea.Bottom - columnHeight);

                var columnRect = new RectangleF(x, y, columnWidth, columnHeight);
                using var brush = new SolidBrush(colors[i]);
                graphics.FillRectangle(brush, columnRect);
                graphics.DrawRectangle(Pens.Black, Rectangle.Round(columnRect));

                using var valueFont = new Font("Arial", 9);
                var valueText = $"{values[i]:0.#}";
                var valueSize = graphics.MeasureString(valueText, valueFont);
                graphics.DrawString(valueText, valueFont, Brushes.Black,
                    (float)(x + (columnWidth - valueSize.Width) / 2), (float)(y - valueSize.Height - 2));

                using var categoryFont = new Font("Arial", 8);
                var categorySize = graphics.MeasureString(categories[i], categoryFont);
                graphics.DrawString(categories[i], categoryFont, Brushes.Black,
                    x + (columnWidth - categorySize.Width) / 2, chartArea.Bottom + 5);
            }
        }
        else
        {
            DrawNoData(graphics, chartArea);
        }

        return Task.FromResult(SaveAsPng(bitmap));
    }

    /// <summary>
    /// 从数据集中抽取一列数值序列用于折线/面积图。
    /// 优先取首个全为数值的列；若 DataItems 中存在 PassRate 等单值，则作为单点返回。
    /// 无可用数值时返回空列表。
    /// </summary>
    private List<double> ExtractNumericSeries(ReportDataSet dataSet)
    {
        var result = new List<double>();
        if (dataSet is null) return result;

        foreach (var column in dataSet.Columns)
        {
            var series = new List<double>();
            var allNumeric = true;
            foreach (var row in dataSet.Rows)
            {
                if (row.TryGetValue(column, out var v) && TryToDouble(v, out var d))
                {
                    series.Add(d);
                }
                else
                {
                    allNumeric = false;
                    break;
                }
            }

            if (allNumeric && series.Count > 0)
            {
                return series;
            }
        }

        return result;
    }

    /// <summary>
    /// 从数据集中按类别聚合计数（默认按 TestResult 列分组）。
    /// 无 TestResult 列或无数据时返回空。
    /// </summary>
    private List<CategoryDatum> ExtractCategoryCounts(ReportDataSet dataSet)
    {
        var result = new List<CategoryDatum>();
        if (dataSet is null || dataSet.Rows.Count == 0) return result;

        // 优先按 TestResult 分组
        const string categoryField = "TestResult";
        if (!dataSet.Columns.Contains(categoryField))
        {
            return result;
        }

        var groups = dataSet.Rows
            .Select(r => r.TryGetValue(categoryField, out var v) ? v?.ToString() ?? string.Empty : string.Empty)
            .GroupBy(v => v)
            .OrderByDescending(g => g.Count());

        foreach (var g in groups)
        {
            result.Add(new CategoryDatum(g.Key, g.Count()));
        }

        return result;
    }

    /// <summary>
    /// 从数据集中抽取两列数值作为散点 (x, y) 并映射到图表区域。
    /// 取前两个全为数值的列；无则返回空。
    /// </summary>
    private List<PointF> ExtractScatterPoints(ReportDataSet dataSet, Rectangle chartArea)
    {
        var result = new List<PointF>();
        if (dataSet is null || dataSet.Rows.Count == 0) return result;

        var numericCols = new List<string>();
        foreach (var column in dataSet.Columns)
        {
            var allNumeric = dataSet.Rows.All(r => r.TryGetValue(column, out var v) && TryToDouble(v, out _));
            if (allNumeric) numericCols.Add(column);
        }

        if (numericCols.Count < 2) return result;

        var xCol = numericCols[0];
        var yCol = numericCols[1];

        var xs = dataSet.Rows.Select(r => TryToDouble(r[xCol], out var d) ? d : 0).ToList();
        var ys = dataSet.Rows.Select(r => TryToDouble(r[yCol], out var d) ? d : 0).ToList();

        var xMin = xs.Min(); var xMax = xs.Max();
        var yMin = ys.Min(); var yMax = ys.Max();
        var xRange = xMax - xMin; if (xRange <= 0) xRange = 1;
        var yRange = yMax - yMin; if (yRange <= 0) yRange = 1;

        for (int i = 0; i < xs.Count; i++)
        {
            var px = chartArea.X + (float)((xs[i] - xMin) / xRange) * chartArea.Width;
            var py = chartArea.Y + chartArea.Height - (float)((ys[i] - yMin) / yRange) * chartArea.Height;
            result.Add(new PointF(px, py));
        }

        return result;
    }

    private static Color[] BuildCategoryColors(int count)
    {
        var palette = new[]
        {
            Color.Green, Color.Red, Color.Orange, Color.Purple, Color.Gray,
            Color.CornflowerBlue, Color.LightSeaGreen, Color.IndianRed, Color.Gold, Color.MediumSeaGreen
        };
        var result = new Color[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = palette[i % palette.Length];
        }
        return result;
    }

    private static bool TryToDouble(object? value, out double result)
    {
        result = 0;
        if (value is null) return false;
        return value switch
        {
            double d => Assign(d, out result),
            float f => Assign((double)f, out result),
            int i => Assign((double)i, out result),
            long l => Assign((double)l, out result),
            decimal dec => Assign((double)dec, out result),
            _ when double.TryParse(value.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed) => Assign(parsed, out result),
            _ => false
        };

        static bool Assign(double d, out double r) { r = d; return true; }
    }

    /// <summary>
    /// 在指定区域居中绘制"无数据"占位文本。
    /// </summary>
    private static void DrawNoData(Graphics graphics, Rectangle area)
    {
        using var font = new Font("Arial", 12, FontStyle.Italic);
        var text = "无数据 / No data";
        var size = graphics.MeasureString(text, font);
        var x = area.X + (area.Width - size.Width) / 2;
        var y = area.Y + (area.Height - size.Height) / 2;
        graphics.DrawString(text, font, Brushes.Gray, x, y);
    }

    private static byte[] SaveAsPng(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return stream.ToArray();
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

        return await GenerateLineChartAsync(config, dataSet, cancellationToken).ConfigureAwait(false);
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

        return await GenerateBarChartAsync(config, dataSet, cancellationToken).ConfigureAwait(false);
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

        return await GeneratePieChartAsync(config, dataSet, cancellationToken).ConfigureAwait(false);
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

        return await GenerateScatterChartAsync(config, dataSet, cancellationToken).ConfigureAwait(false);
    }

    [SupportedOSPlatform("windows")]
    public async Task<byte[]> GenerateDashboardAsync(List<ChartConfiguration> charts, ReportDataSet dataSet, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.Info($"生成仪表盘: {charts.Count} 个图表");

            cancellationToken.ThrowIfCancellationRequested();

            var dashboardWidth = 1200;
            var dashboardHeight = 800;
            var cols = Math.Min(2, charts.Count);
            var rows = (int)Math.Ceiling((double)charts.Count / cols);

            var chartWidth = dashboardWidth / cols - 20;
            var chartHeight = dashboardHeight / rows - 20;

            using var bitmap = new Bitmap(dashboardWidth, dashboardHeight);
            using var graphics = Graphics.FromImage(bitmap);

            graphics.Clear(Color.WhiteSmoke);

            using var titleFont = new Font("Arial", 20, FontStyle.Bold);
            var title = "测试数据仪表盘";
            var titleSize = graphics.MeasureString(title, titleFont);
            var titleX = (dashboardWidth - titleSize.Width) / 2;
            graphics.DrawString(title, titleFont, Brushes.Black, titleX, 10);

            for (int i = 0; i < charts.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var row = i / cols;
                var col = i % cols;

                var x = col * (chartWidth + 10) + 10;
                var y = row * (chartHeight + 10) + 60;

                var chartConfig = charts[i] with
                {
                    Width = chartWidth,
                    Height = chartHeight
                };

                var chartBytes = await GenerateChartAsync(chartConfig, dataSet, cancellationToken).ConfigureAwait(false);

                using var chartStream = new MemoryStream(chartBytes);
                using var chartImage = Image.FromStream(chartStream);
                graphics.DrawImage(chartImage, x, y, chartWidth, chartHeight);

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

    /// <summary>
    /// 类别数据项（标签 + 计数）。
    /// </summary>
    private sealed record CategoryDatum(string Label, double Value);

    public void Dispose()
    {
        // 无未托管资源需要释放；保留 IDisposable 以兼容既有 DI 释放链
        if (!_disposed)
        {
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
