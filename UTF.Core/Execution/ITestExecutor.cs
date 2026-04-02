using System.Threading;
using System.Threading.Tasks;

namespace UTF.Core;

/// <summary>
/// 测试执行器接口 - 负责单步执行
/// </summary>
public interface ITestExecutor
{
    Task<TestStepExecutionResult> ExecuteAsync(TestStep step, string dutId, CancellationToken ct = default);
}
