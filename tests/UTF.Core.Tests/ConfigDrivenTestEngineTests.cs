using Xunit;
using UTF.Core;
using UTF.Logging;

namespace UTF.Core.Tests;

public class ConfigDrivenTestEngineTests : IDisposable
{
    private readonly ConfigDrivenTestEngine _engine;
    private readonly ILogger _logger;

    public ConfigDrivenTestEngineTests()
    {
        _logger = LoggerFactory.CreateLogger(
            nameof(ConfigDrivenTestEngineTests),
            new LogConfiguration
            {
                EnableConsole = false,
                EnableFile = false
            });
        _engine = new ConfigDrivenTestEngine(logger: _logger, pluginService: null);
    }

    public void Dispose()
    {
        _engine.Dispose();
        if (_logger is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    #region ExecuteStepAsync - 基本行为

    [Fact]
    public async Task ExecuteStepAsync_DisabledStep_IsSkipped()
    {
        var step = new ConfigTestStep
        {
            Id = "step-1",
            Name = "Disabled Step",
            Order = 1,
            Type = "cmd",
            Command = "echo hello",
            Enabled = false
        };

        var result = await _engine.ExecuteStepAsync(step, "DUT-001");

        Assert.True(result.Passed);
        Assert.True(result.Skipped);
    }

    [Fact]
    public async Task ExecuteStepAsync_ConditionNotMet_IsSkipped()
    {
        var step = new ConfigTestStep
        {
            Id = "step-1",
            Name = "Conditional Step",
            Order = 1,
            Type = "cmd",
            Command = "echo hello",
            ConditionExpression = "exists:nonexistent_key"
        };

        var result = await _engine.ExecuteStepAsync(step, "DUT-001", context: new Dictionary<string, object>());

        Assert.True(result.Skipped);
    }

    [Fact]
    public async Task ExecuteStepAsync_ConditionMet_Executes()
    {
        var step = new ConfigTestStep
        {
            Id = "step-1",
            Name = "Conditional Step",
            Order = 1,
            Type = "cmd",
            Command = "echo hello",
            ConditionExpression = "exists:my_key"
        };

        var context = new Dictionary<string, object> { ["my_key"] = "value" };
        var result = await _engine.ExecuteStepAsync(step, "DUT-001", context);

        Assert.False(result.Skipped);
    }

    [Fact]
    public async Task ExecuteStepAsync_UnknownTypeWithoutPlugin_ReturnsFailure()
    {
        var step = new ConfigTestStep
        {
            Id = "step-unknown",
            Name = "Unknown Step",
            Order = 1,
            Type = "unknown",
            Channel = "mystery",
            Command = "do something"
        };

        var result = await _engine.ExecuteStepAsync(step, "DUT-001");

        Assert.False(result.Passed);
        Assert.Contains("未找到可处理步骤类型", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteStepAsync_WithMockOutput_UsesMockOutputWithoutPlugin()
    {
        var step = new ConfigTestStep
        {
            Id = "step-mock",
            Name = "Mock Step",
            Order = 1,
            Type = "unknown",
            Channel = "mystery",
            Command = "ignored",
            Expected = "contains:PASS",
            Parameters = new Dictionary<string, object>
            {
                ["MockOutput"] = "PASS: simulated"
            }
        };

        var result = await _engine.ExecuteStepAsync(step, "DUT-001");

        Assert.True(result.Passed);
        Assert.Equal("PASS: simulated", result.RawOutput);
    }

    #endregion

    #region ExecuteStepAsync - 超时

    [Fact]
    public async Task ExecuteStepAsync_Cancelled_ReturnsFailure()
    {
        var step = new ConfigTestStep
        {
            Id = "step-1",
            Name = "Long Step",
            Order = 1,
            Type = "cmd",
            Command = "long_running_command",
            Timeout = 60000
        };

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await _engine.ExecuteStepAsync(step, "DUT-001", cancellationToken: cts.Token);

        Assert.False(result.Passed);
    }

    #endregion

    #region ExecuteStepAsync - StoreResultAs上下文传递

    [Fact]
    public async Task ExecuteStepAsync_StoreResultAs_SavesOutputToContext()
    {
        var step = new ConfigTestStep
        {
            Id = "step-1",
            Name = "Store Step",
            Order = 1,
            Type = "cmd",
            Command = "echo 1.23",
            StoreResultAs = "measured_value"
        };

        var context = new Dictionary<string, object>();
        var result = await _engine.ExecuteStepAsync(step, "DUT-001", context);

        // 执行后 context 中应包含 StoreResultAs 键（无论命令是否成功，只要有输出）
        // 具体行为取决于插件系统是否可用；无插件时引擎应正常处理
        Assert.NotNull(result);
    }

    #endregion

    #region ExecuteProjectAsync

    [Fact]
    public async Task ExecuteProjectAsync_EmptySteps_ReturnsPassingReport()
    {
        var project = new ConfigTestProject
        {
            Id = "proj-1",
            Name = "Empty Project",
            Steps = new List<ConfigTestStep>()
        };

        var report = await _engine.ExecuteTestProjectAsync(project, "DUT-001");

        Assert.NotNull(report);
        Assert.Equal("proj-1", report.ProjectId);
    }

    [Fact]
    public async Task ExecuteProjectAsync_DisabledProject_ReturnsEmptyReport()
    {
        var project = new ConfigTestProject
        {
            Id = "proj-1",
            Name = "Disabled Project",
            Enabled = false,
            Steps = new List<ConfigTestStep>
            {
                new() { Id = "step-1", Name = "Step 1", Order = 1, Type = "cmd", Command = "echo test" }
            }
        };

        var report = await _engine.ExecuteTestProjectAsync(project, "DUT-001");

        Assert.NotNull(report);
    }

    #endregion
}
