using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace UTF.Core.Persistence;

public class FileTestResultRepository : ITestResultRepository
{
    private readonly string _basePath;

    public FileTestResultRepository(string basePath = "test-results")
    {
        _basePath = basePath;
        Directory.CreateDirectory(_basePath);
    }

    public async Task SaveAsync(TestReport result, CancellationToken ct = default)
    {
        var path = Path.Combine(_basePath, $"{result.ReportId}.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(result), ct);
    }

    public async Task<IEnumerable<TestReport>> QueryAsync(TestResultQuery query, CancellationToken ct = default)
    {
        var files = Directory.GetFiles(_basePath, "*.json");
        var results = new List<TestReport>();

        foreach (var file in files.Skip(query.Skip).Take(query.Take))
        {
            var json = await File.ReadAllTextAsync(file, ct);
            var report = JsonSerializer.Deserialize<TestReport>(json);
            if (report != null && MatchesQuery(report, query))
                results.Add(report);
        }

        return results;
    }

    public async Task<TestReport?> GetByIdAsync(string reportId, CancellationToken ct = default)
    {
        var path = Path.Combine(_basePath, $"{reportId}.json");
        if (!File.Exists(path)) return null;
        var json = await File.ReadAllTextAsync(path, ct);
        return JsonSerializer.Deserialize<TestReport>(json);
    }

    private bool MatchesQuery(TestReport report, TestResultQuery query)
    {
        if (query.DutId != null && report.DUTId != query.DutId) return false;
        if (query.Passed.HasValue && report.OverallResult != query.Passed.Value) return false;
        if (query.StartDate.HasValue && report.StartTime < query.StartDate.Value) return false;
        if (query.EndDate.HasValue && report.EndTime > query.EndDate.Value) return false;
        return true;
    }
}
