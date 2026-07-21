using System.Threading;
using System.Threading.Tasks;
using UTF.Core;
using UTF.Logging;
using Xunit;
using NSubstitute;

namespace UTF.Business.Tests;

/// <summary>
/// 融合后的步骤执行服务冒烟测试。原 <c>UTF.Business.StepExecutionService</c> 适配层已删除，
/// <see cref="ConfigDrivenTestEngine"/> 直接实现 <see cref="IStepExecutionService"/>。
/// 本测试验证引擎作为步骤执行服务入口的空参数保护与单步执行行为。
/// </summary>
public class StepExecutionServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExecuteStepAsync_NullRequest_ThrowsArgumentNullException()
    {
        // Arrange
        using var engine = new ConfigDrivenTestEngine();
        IStepExecutionService service = engine;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.ExecuteStepAsync(null!, CancellationToken.None));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExecuteStepAsync_DisabledStep_SkipsAndPasses()
    {
        // Arrange - 步骤禁用时引擎应跳过并返回 Passed=true, Skipped=true。
        using var engine = new ConfigDrivenTestEngine();
        IStepExecutionService service = engine;
        var request = new CoreStepExecutionRequest
        {
            Step = new ConfigTestStep
            {
                Id = "s1",
                Name = "disabled-step",
                Enabled = false
            },
            DutId = "dut-1"
        };

        // Act
        var result = await service.ExecuteStepAsync(request, CancellationToken.None);

        // Assert
        Assert.True(result.Passed);
        Assert.True(result.Skipped);
        Assert.Equal("s1", result.StepId);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExecuteStepAsync_NoPlugin_FailsWithErrorMessage()
    {
        // Arrange - 无插件且无 MockOutput 时，内置执行应返回失败并附带错误信息。
        using var engine = new ConfigDrivenTestEngine();
        IStepExecutionService service = engine;
        var request = new CoreStepExecutionRequest
        {
            Step = new ConfigTestStep
            {
                Id = "s2",
                Name = "no-plugin-step",
                Enabled = true,
                Type = "serial",
                Channel = "Serial",
                Command = "AT"
            },
            DutId = "dut-2"
        };

        // Act
        var result = await service.ExecuteStepAsync(request, CancellationToken.None);

        // Assert
        Assert.False(result.Passed);
        Assert.Contains("未找到可处理步骤类型", result.ErrorMessage);
    }
}
