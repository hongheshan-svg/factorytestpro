using System.Xml.Linq;
using UTF.Core;
using UTF.Core.Caching;
using UTF.Core.Persistence;
using UTF.Reporting;
using Xunit;

namespace UTF.Core.Tests;

public sealed class CriticalRegressionTests
{
    [Fact][Trait("Category","Unit")]
    public async Task MemoryCache_ConcurrentFactory_ExecutesOnce()
    {
        using var cache = new MemoryCache();
        var calls = 0;
        var tasks = Enumerable.Range(0, 20).Select(_ => cache.GetOrCreateAsync("shared", async () =>
        {
            Interlocked.Increment(ref calls);
            await Task.Delay(25);
            return "value";
        }));

        var values = await Task.WhenAll(tasks);

        Assert.Equal(1, calls);
        Assert.All(values, value => Assert.Equal("value", value));
    }

    [Fact][Trait("Category","Unit")]
    public async Task FileRepository_TraversalId_IsRejected()
    {
        var directory = CreateTempDirectory();
        try
        {
            var repository = new FileTestResultRepository(directory);
            await Assert.ThrowsAsync<ArgumentException>(() => repository.SaveAsync(new TestReport
            {
                ReportId = "../outside",
                DUTId = "DUT-1"
            }));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact][Trait("Category","Unit")]
    public async Task FileRepository_SaveAndQuery_IsAtomicAndReadable()
    {
        var directory = CreateTempDirectory();
        try
        {
            var repository = new FileTestResultRepository(directory);
            var report = new TestReport
            {
                ReportId = "report-1",
                DUTId = "DUT-1",
                OverallResult = true,
                StartTime = DateTime.UtcNow.AddSeconds(-1),
                EndTime = DateTime.UtcNow
            };

            await repository.SaveAsync(report);
            var loaded = await repository.GetByIdAsync("report-1");
            var queried = await repository.QueryAsync(new TestResultQuery(DutId: "DUT-1"));

            Assert.NotNull(loaded);
            Assert.Equal(report.ReportId, loaded.ReportId);
            Assert.Equal(report.DUTId, loaded.DUTId);
            Assert.Equal(report.OverallResult, loaded.OverallResult);
            Assert.Single(queried);
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact][Trait("Category","Unit")]
    public async Task ReportGenerator_UnsafeValues_AreEscapedInAllTextFormats()
    {
        var directory = CreateTempDirectory();
        try
        {
            using var generator = new ReportGenerator();
            var data = new ReportDataSet
            {
                Columns = new List<string> { "DUTId", "StepName", "TestResult", "MeasuredValue", "ExpectedValue", "ExecutionTime" },
                Rows = new List<Dictionary<string, object>>
                {
                    new()
                    {
                        ["DUTId"] = "<script>alert(1)</script>", ["StepName"] = "=1+1", ["TestResult"] = "PASS",
                        ["MeasuredValue"] = "a,\"b\"", ["ExpectedValue"] = "<ok>", ["ExecutionTime"] = "1"
                    }
                }
            };
            var template = new ReportTemplate
            {
                TemplateId = "security",
                Name = "Security",
                Content = "{{#TestResults}}<p>{{DUTId}}|{{StepName}}|{{MeasuredValue}}</p>{{/TestResults}}"
            };

            var htmlPath = Path.Combine(directory, "report.html");
            var csvPath = Path.Combine(directory, "report.csv");
            var xmlPath = Path.Combine(directory, "report.xml");
            Assert.True((await generator.GenerateReportFromTemplateAsync(template, data, ReportFormat.HTML, htmlPath)).Success);
            Assert.True((await generator.GenerateReportFromTemplateAsync(template, data, ReportFormat.CSV, csvPath)).Success);
            Assert.True((await generator.GenerateReportFromTemplateAsync(template, data, ReportFormat.XML, xmlPath)).Success);

            var html = await File.ReadAllTextAsync(htmlPath);
            var csv = await File.ReadAllTextAsync(csvPath);
            Assert.DoesNotContain("<script>", html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("&lt;script&gt;", html);
            Assert.Contains("\"'=1+1\"", csv);
            Assert.Contains("\"a,\"\"b\"\"\"", csv);
            _ = XDocument.Load(xmlPath);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact][Trait("Category","Unit")]
    public void ReportGenerator_SupportedFormats_ExcludeFakeBinaryFormats()
    {
        using var generator = new ReportGenerator();

        Assert.DoesNotContain(ReportFormat.Excel, generator.SupportedFormats);
        Assert.DoesNotContain(ReportFormat.PDF, generator.SupportedFormats);
    }

    [Fact][Trait("Category","Unit")]
    public async Task ReportGenerator_GenerateReportAsync_UsesRepositoryResults()
    {
        var directory = CreateTempDirectory();
        try
        {
            var repository = new FileTestResultRepository(Path.Combine(directory, "results"));
            await repository.SaveAsync(new TestReport
            {
                ReportId = "real-result",
                TaskId = "session-1",
                DUTId = "DUT-REAL",
                OverallResult = true,
                StartTime = DateTime.UtcNow.AddSeconds(-2),
                EndTime = DateTime.UtcNow
            });
            using var generator = new ReportGenerator(resultRepository: repository);
            var outputPath = Path.Combine(directory, "real-report.html");

            var result = await generator.GenerateReportAsync(new ReportConfiguration
            {
                ReportName = "Real Results",
                ReportType = ReportType.Test,
                OutputFormat = ReportFormat.HTML,
                OutputPath = outputPath
            });

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Contains("DUT-REAL", await File.ReadAllTextAsync(outputPath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "utf-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
