using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UTF.Logging;

namespace UTF.Core;

/// <summary>
/// 测试编排器 - 负责测试流程编排和重试逻辑
/// </summary>
public class TestOrchestrator
{
    private readonly ITestExecutor _executor;
    private readonly ITestValidator _validator;
    private readonly IRetryPolicy _retryPolicy;
    private readonly ILogger _logger;

    public TestOrchestrator(ITestExecutor executor, ITestValidator validator, IRetryPolicy retryPolicy, ILogger logger)
    {
        _executor = executor;
        _validator = validator;
        _retryPolicy = retryPolicy;
        _logger = logger;
    }

    public async Task<TestStepExecutionResult> ExecuteStepWithRetryAsync(
        TestStep step,
        string dutId,
        CancellationToken ct = default)
    {
        var attemptCount = 0;
        TestStepExecutionResult? lastResult = null;

        while (attemptCount <= step.RetryCount)
        {
            try
            {
                lastResult = await _executor.ExecuteAsync(step, dutId, ct);

                if (lastResult.MeasuredValue != null && step.ExpectedResult != null)
                {
                    var validation = _validator.Validate(
                        lastResult.MeasuredValue.ToString() ?? "",
                        step.ExpectedResult.ToString() ?? "");

                    lastResult = lastResult with
                    {
                        Passed = validation.IsValid,
                        ErrorMessage = validation.ErrorMessage ?? lastResult.ErrorMessage,
                        RetryCount = attemptCount
                    };
                }

                if (lastResult.Passed || !_retryPolicy.ShouldRetry(attemptCount, null))
                    break;

                var delay = _retryPolicy.GetNextDelay(attemptCount);
                _logger.Warning($"步骤失败，{delay.TotalSeconds}秒后重试 ({attemptCount + 1}/{step.RetryCount})");
                await Task.Delay(delay, ct);
            }
            catch (Exception ex)
            {
                _logger.Error($"执行异常: {step.Name}", ex);
                if (!_retryPolicy.ShouldRetry(attemptCount, ex))
                    throw;
            }

            attemptCount++;
        }

        return lastResult ?? new TestStepExecutionResult
        {
            StepId = step.StepId,
            StepName = step.Name,
            Passed = false,
            ErrorMessage = "执行失败"
        };
    }

    public async Task<List<TestStepExecutionResult>> ExecuteSequenceAsync(
        TestSequence sequence,
        string dutId,
        CancellationToken ct = default)
    {
        var results = new List<TestStepExecutionResult>();

        foreach (var step in sequence.Steps.OrderBy(s => s.StepId))
        {
            var result = await ExecuteStepWithRetryAsync(step, dutId, ct);
            results.Add(result);

            if (!result.Passed && sequence.StopOnFailure)
            {
                _logger.Warning($"关键步骤失败，停止序列: {step.Name}");
                break;
            }
        }

        return results;
    }
}
