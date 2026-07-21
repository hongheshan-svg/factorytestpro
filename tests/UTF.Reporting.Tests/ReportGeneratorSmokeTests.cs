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
    public void SupportedFormats_ContainsHtml()
    {
        // Arrange
        using var generator = new ReportGenerator();

        // Act
        var formats = generator.SupportedFormats;

        // Assert
        Assert.Contains(ReportFormat.HTML, formats);
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
