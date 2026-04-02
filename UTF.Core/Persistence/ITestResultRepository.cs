using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UTF.Core;

namespace UTF.Core.Persistence;

public interface ITestResultRepository
{
    Task SaveAsync(TestReport result, CancellationToken ct = default);
    Task<IEnumerable<TestReport>> QueryAsync(TestResultQuery query, CancellationToken ct = default);
    Task<TestReport?> GetByIdAsync(string reportId, CancellationToken ct = default);
}

public record TestResultQuery(
    string? DutId = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    bool? Passed = null,
    int Skip = 0,
    int Take = 100
);
