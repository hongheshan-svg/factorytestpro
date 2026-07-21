using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace UTF.Core.Persistence;

/// <summary>
/// 基于文件的测试结果仓储 - 查询时先过滤再分页，目录创建延迟到首次写入
/// </summary>
public class FileTestResultRepository : ITestResultRepository
{
    private readonly string _basePath;
    private readonly string _basePathPrefix;

    public FileTestResultRepository(string basePath = "test-results")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(basePath);
        _basePath = Path.GetFullPath(basePath);
        _basePathPrefix = _basePath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
    }

    public async Task SaveAsync(TestReport result, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        EnsureDirectory();
        var path = GetReportPath(result.ReportId);
        var temporaryPath = Path.Combine(_basePath, $".{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(result), ct).ConfigureAwait(false);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            TryDelete(temporaryPath);
        }
    }

    public async Task<IEnumerable<TestReport>> QueryAsync(TestResultQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!Directory.Exists(_basePath))
        {
            return Array.Empty<TestReport>();
        }

        var files = Directory.GetFiles(_basePath, "*.json");

        // 先加载并过滤全部匹配项，再对过滤后的集合分页
        var matched = new List<TestReport>();
        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
                var report = JsonSerializer.Deserialize<TestReport>(json);
                if (report != null && MatchesQuery(report, query))
                {
                    matched.Add(report);
                }
            }
            catch (JsonException)
            {
                // A corrupt report is isolated instead of breaking the whole query.
            }
        }

        return matched
            .OrderByDescending(report => report.StartTime)
            .Skip(Math.Max(0, query.Skip))
            .Take(Math.Clamp(query.Take, 0, 10_000))
            .ToArray();
    }

    public async Task<TestReport?> GetByIdAsync(string reportId, CancellationToken ct = default)
    {
        var path = GetReportPath(reportId);
        if (!File.Exists(path)) return null;
        var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
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

    private void EnsureDirectory()
    {
        Directory.CreateDirectory(_basePath);
    }

    private string GetReportPath(string reportId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportId);
        if (reportId.Length > 180 || reportId is "." or ".." ||
            reportId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            reportId.Contains(Path.DirectorySeparatorChar) ||
            reportId.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new ArgumentException("Report ID contains invalid path characters.", nameof(reportId));
        }

        var path = Path.GetFullPath(Path.Combine(_basePath, $"{reportId}.json"));
        if (!path.StartsWith(_basePathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Report path escapes the repository root.", nameof(reportId));
        }

        return path;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup must not mask the original write failure.
        }
    }
}
