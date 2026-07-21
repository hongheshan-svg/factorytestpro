using System.Text.Json;
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
    public void UnifiedConfiguration_MissingUiProfile_DeserializesAsNull_DefaultIsFullEngineer()
    {
        const string json = """{"ConfigurationInfo":{"Name":"t"},"SystemSettings":{},"DUTConfiguration":{}}""";
        var config = JsonSerializer.Deserialize<UnifiedConfiguration>(json);

        Assert.NotNull(config);
        Assert.Null(config!.UiProfile);

        var defaults = UiProfile.CreateDefault();
        Assert.Equal("MultiDutBoard", defaults.Mode);
        Assert.True(defaults.ShowStepColumns);
        Assert.True(defaults.ShowAdvancedMenus);
        Assert.True(defaults.AllowConfigEdit);
        Assert.Equal("DUT", defaults.UnitLabel);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void UnifiedConfiguration_UiProfile_RoundTripsJson()
    {
        var original = new UnifiedConfiguration
        {
            UiProfile = new UiProfile
            {
                Mode = "ScanToTest",
                ShowStepColumns = false,
                ShowAdvancedMenus = false,
                AllowConfigEdit = false,
                UnitLabel = "Unit",
                PrimaryActions = new() { "StartAll", "StopAll" }
            }
        };

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<UnifiedConfiguration>(json);

        Assert.NotNull(restored?.UiProfile);
        Assert.Equal("ScanToTest", restored!.UiProfile!.Mode);
        Assert.False(restored.UiProfile.ShowStepColumns);
        Assert.False(restored.UiProfile.AllowConfigEdit);
        Assert.Equal("Unit", restored.UiProfile.UnitLabel);
        Assert.Equal(2, restored.UiProfile.PrimaryActions.Count);
    }

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
