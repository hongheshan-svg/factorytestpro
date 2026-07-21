using System.Threading;
using System.Threading.Tasks;

namespace UTF.Core;

/// <summary>
/// 步骤执行服务接口。作为 UI / 业务层调用单步执行的契约，
/// 由 <see cref="ConfigDrivenTestEngine"/> 直接实现（适配 <see cref="CoreStepExecutionRequest"/>
/// 到内部 <see cref="ConfigTestStep"/> 路径）。原 <c>UTF.Business.StepExecutionService</c>
/// 适配层已移除，消除一层不必要的间接调用。
/// </summary>
public interface IStepExecutionService
{
    /// <summary>
    /// 异步执行单个测试步骤。
    /// </summary>
    /// <param name="request">步骤执行请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>步骤执行结果。</returns>
    Task<CoreStepExecutionResult> ExecuteStepAsync(CoreStepExecutionRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// 步骤执行请求。聚合 <see cref="ConfigTestStep"/> 与 DUT 上下文信息。
/// 位于 <see cref="UTF.Core"/> 命名空间（与 <c>UTF.Plugin.Abstractions.StepExecutionRequest</c>
/// 区分：后者是插件契约，前者是引擎 / 业务层调用契约）。
/// </summary>
public sealed class CoreStepExecutionRequest
{
    /// <summary>测试步骤配置。</summary>
    public ConfigTestStep Step { get; init; } = new();

    /// <summary>DUT 标识。</summary>
    public string DutId { get; init; } = string.Empty;

    /// <summary>上下文变量（如 SerialPort、Host 等）。</summary>
    public Dictionary<string, object>? Context { get; init; }
}

/// <summary>
/// 步骤执行结果。镜像 <see cref="ConfigDrivenStepResult"/> 的关键信息。
/// </summary>
public sealed class CoreStepExecutionResult
{
    /// <summary>步骤标识。</summary>
    public string StepId { get; init; } = string.Empty;

    /// <summary>步骤名称。</summary>
    public string StepName { get; init; } = string.Empty;

    /// <summary>是否通过。</summary>
    public bool Passed { get; init; }

    /// <summary>是否跳过（步骤禁用或条件不满足）。</summary>
    public bool Skipped { get; init; }

    /// <summary>原始输出。</summary>
    public string RawOutput { get; init; } = string.Empty;

    /// <summary>测量值。</summary>
    public string MeasuredValue { get; init; } = string.Empty;

    /// <summary>期望值。</summary>
    public string ExpectedValue { get; init; } = string.Empty;

    /// <summary>错误信息。</summary>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>开始时间（UTC）。</summary>
    public DateTime StartTime { get; init; }

    /// <summary>结束时间（UTC）。</summary>
    public DateTime EndTime { get; init; }

    /// <summary>重试次数。</summary>
    public int RetryCount { get; init; }
}
