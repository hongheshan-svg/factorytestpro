using System;
using System.Threading;
using System.Threading.Tasks;
using UTF.Logging;

namespace UTF.Core;

public class TestExecutor : ITestExecutor
{
    private readonly ILogger _logger;
    private readonly IPluginService? _pluginService;

    public TestExecutor(ILogger logger, IPluginService? pluginService = null)
    {
        _logger = logger;
        _pluginService = pluginService;
    }

    public async Task<TestStepExecutionResult> ExecuteAsync(TestStep step, string dutId, CancellationToken ct = default)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            if (_pluginService == null)
                throw new InvalidOperationException("插件服务未初始化");

            var request = new Plugin.Abstractions.StepExecutionRequest
            {
                StepId = step.StepId,
                StepName = step.Name,
                StepType = step.StepType,
                Command = step.Command,
                TimeoutMs = (int)step.Timeout.TotalMilliseconds,
                DutId = dutId,
                Parameters = new System.Collections.ObjectModel.ReadOnlyDictionary<string, object?>(
                    step.Parameters.ToDictionary(
                        pair => pair.Key,
                        pair => (object?)pair.Value))
            };

            var result = await _pluginService.ExecuteAsync(request, ct);

            return new TestStepExecutionResult
            {
                StepId = step.StepId,
                StepName = step.Name,
                Passed = result.Status == Plugin.Abstractions.StepExecutionStatus.Passed,
                MeasuredValue = result.RawOutput,
                StartTime = result.StartTimeUtc,
                EndTime = result.EndTimeUtc,
                ErrorMessage = result.ErrorMessage
            };
        }
        catch (Exception ex)
        {
            _logger.Error($"执行步骤失败: {step.Name}", ex);
            return new TestStepExecutionResult
            {
                StepId = step.StepId,
                StepName = step.Name,
                Passed = false,
                StartTime = startTime,
                EndTime = DateTime.UtcNow,
                ErrorMessage = ex.Message
            };
        }
    }
}
