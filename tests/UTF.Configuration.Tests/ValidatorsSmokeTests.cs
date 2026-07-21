using UTF.Configuration.Models;
using UTF.Configuration.Validators;
using Xunit;

namespace UTF.Configuration.Tests;

/// <summary>
/// Smoke tests for the Stack B configuration validators.
/// </summary>
public class ValidatorsSmokeTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void SystemConfigValidator_ValidConfig_ReturnsNoErrors()
    {
        var validator = new SystemConfigValidator();
        var config = new SystemConfig
        {
            LogLevel = "Info",
            ResultsPath = "./test-results",
            DefaultLanguage = "zh-CN"
        };

        var result = validator.Validate(config);

        Assert.True(result.IsValid);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void DUTConfigValidator_InvalidMaxConcurrent_ReturnsError()
    {
        var validator = new DUTConfigValidator();
        var config = new DUTConfig
        {
            ProductName = "Test Device",
            MaxConcurrent = 0
        };

        var result = validator.Validate(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("并发数"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void DUTConfigValidator_ValidConfig_ReturnsNoErrors()
    {
        var validator = new DUTConfigValidator();
        var config = new DUTConfig
        {
            ProductName = "Test Device",
            MaxConcurrent = 16
        };

        var result = validator.Validate(config);

        Assert.True(result.IsValid);
    }
}
