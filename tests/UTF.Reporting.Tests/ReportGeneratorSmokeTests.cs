using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UTF.Reporting;
using Xunit;

namespace UTF.Reporting.Tests;

/// <summary>
/// Smoke tests for <see cref="ReportGenerator"/>. Verifies the advertised
/// supported formats and that an HTML report can be produced from a minimal
/// data set. Deeper scenarios (PDF/PDF-not-implemented, charts, statistics)
/// belong in follow-up tests.
/// </summary>
public class ReportGeneratorSmokeTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void SupportedFormats_ContainsHtmlAndPdf()
    {
        // Arrange
        using var generator = new ReportGenerator();

        // Act
        var formats = generator.SupportedFormats;

        // Assert
        Assert.Contains(ReportFormat.HTML, formats);
        Assert.Contains(ReportFormat.PDF, formats);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GeneratePdfReportAsync_EmptyDataSet_WritesPdfFile()
    {
        using var generator = new ReportGenerator();
        var templates = await generator.GetAvailableTemplatesAsync(ReportType.Test);
        var template = Assert.Single(templates);
        var dataSet = new ReportDataSet
        {
            Name = "smoke-pdf",
            Description = "pdf smoke",
            Columns = new List<string> { "DUTId", "StepName", "TestResult" },
            Rows =
            {
                new Dictionary<string, object>
                {
                    ["DUTId"] = "DUT-1",
                    ["StepName"] = "StepA",
                    ["TestResult"] = "PASS"
                }
            },
            DataItems =
            {
                new ReportDataItem { Name = "TotalTests", Value = 1 },
                new ReportDataItem { Name = "PassedTests", Value = 1 },
                new ReportDataItem { Name = "FailedTests", Value = 0 },
                new ReportDataItem { Name = "PassRate", Value = "100%" }
            }
        };

        var outputPath = Path.Combine(Path.GetTempPath(), $"utf-report-smoke-{Guid.NewGuid():N}.pdf");
        try
        {
            var result = await generator.GenerateReportFromTemplateAsync(
                template, dataSet, ReportFormat.PDF, outputPath, CancellationToken.None);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.True(File.Exists(outputPath));
            Assert.True(new FileInfo(outputPath).Length > 100);
            // PDF magic header
            var header = new byte[4];
            await using (var fs = File.OpenRead(outputPath))
            {
                _ = await fs.ReadAsync(header);
            }

            Assert.Equal((byte)'%', header[0]);
            Assert.Equal((byte)'P', header[1]);
            Assert.Equal((byte)'D', header[2]);
            Assert.Equal((byte)'F', header[3]);
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GenerateHtmlReportAsync_EmptyDataSet_ReturnsNonEmptyFile()
    {
        // Arrange: The public entry point is GenerateReportFromTemplateAsync,
        // which writes the rendered HTML to disk and returns a result whose
        // FilePath points to the produced file. The private GenerateHtmlReportAsync
        // is not directly callable.
        using var generator = new ReportGenerator();
        var templates = await generator.GetAvailableTemplatesAsync(ReportType.Test);
        var template = Assert.Single(templates);
        var dataSet = new ReportDataSet
        {
            Name = "smoke",
            Description = "smoke test data set"
        };
        var outputPath = Path.Combine(Path.GetTempPath(), $"utf-report-smoke-{System.Guid.NewGuid():N}.html");

        try
        {
            // Act
            var result = await generator.GenerateReportFromTemplateAsync(
                template, dataSet, ReportFormat.HTML, outputPath, CancellationToken.None);

            // Assert
            Assert.True(result.Success, $"Expected success but got: {result.ErrorMessage}");
            Assert.True(File.Exists(outputPath), "Report file was not written to disk");

            var content = await File.ReadAllTextAsync(outputPath);
            Assert.False(string.IsNullOrEmpty(content));
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }
}
