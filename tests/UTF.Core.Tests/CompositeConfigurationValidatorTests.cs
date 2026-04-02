using System.Collections.Generic;
using Xunit;
using UTF.Configuration.Abstractions;
using UTF.Configuration.Models;
using UTF.Configuration.Validators;

namespace UTF.Core.Tests;

public class CompositeConfigurationValidatorTests
{
    private readonly CompositeConfigurationValidator _validator;

    public CompositeConfigurationValidatorTests()
    {
        _validator = new CompositeConfigurationValidator(
            new SystemConfigValidator(),
            new DUTConfigValidator(),
            new TestConfigValidator());
    }

    [Fact]
    public void ValidateAll_AllValidConfigs_ReturnsValid()
    {
        // Arrange
        var system = new SystemConfig
        {
            LogLevel = "Info",
            ResultsPath = "./results",
            DefaultLanguage = "zh-CN"
        };
        var dut = new DUTConfig
        {
            ProductName = "TestProduct",
            MaxConcurrent = 16
        };
        var test = new TestConfig
        {
            ProjectName = "TestProject",
            Steps = new List<TestStepConfig>
            {
                new() { Id = "step1", Name = "Step 1" }
            }
        };

        // Act
        var result = _validator.ValidateAll(system, dut, test);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.AllErrors);
        Assert.Equal(3, result.SectionResults.Count);
    }

    [Fact]
    public void ValidateAll_AllNull_ReturnsThreeErrors()
    {
        // Act
        var result = _validator.ValidateAll(null, null, null);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(3, result.AllErrors.Length);
        Assert.Contains(result.AllErrors, e => e.Contains("SystemSettings"));
        Assert.Contains(result.AllErrors, e => e.Contains("DUTConfiguration"));
        Assert.Contains(result.AllErrors, e => e.Contains("TestProjectConfiguration"));
    }

    [Fact]
    public void ValidateAll_OnlySystemNull_ReturnsSystemError()
    {
        // Arrange
        var dut = new DUTConfig { ProductName = "P", MaxConcurrent = 4 };
        var test = new TestConfig { ProjectName = "T" };

        // Act
        var result = _validator.ValidateAll(null, dut, test);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.AllErrors);
        Assert.Contains("SystemSettings", result.AllErrors[0]);
    }

    [Fact]
    public void ValidateAll_InvalidSystemConfig_ReturnsValidationErrors()
    {
        // Arrange
        var system = new SystemConfig
        {
            LogLevel = "",
            ResultsPath = "",
            DefaultLanguage = ""
        };
        var dut = new DUTConfig { ProductName = "P", MaxConcurrent = 4 };
        var test = new TestConfig { ProjectName = "T" };

        // Act
        var result = _validator.ValidateAll(system, dut, test);

        // Assert
        Assert.False(result.IsValid);
        Assert.All(result.AllErrors, e => Assert.StartsWith("[SystemSettings]", e));
    }

    [Fact]
    public void ValidateAll_InvalidDUTConfig_ReturnsValidationErrors()
    {
        // Arrange
        var system = new SystemConfig();
        var dut = new DUTConfig { ProductName = "", MaxConcurrent = 0 };
        var test = new TestConfig { ProjectName = "T" };

        // Act
        var result = _validator.ValidateAll(system, dut, test);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.AllErrors, e => e.Contains("[DUTConfiguration]"));
    }

    [Fact]
    public void ValidateAll_DuplicateStepIds_ReturnsTestConfigError()
    {
        // Arrange
        var system = new SystemConfig();
        var dut = new DUTConfig { ProductName = "P", MaxConcurrent = 4 };
        var test = new TestConfig
        {
            ProjectName = "T",
            Steps = new List<TestStepConfig>
            {
                new() { Id = "s1", Name = "Step A" },
                new() { Id = "s1", Name = "Step B" }
            }
        };

        // Act
        var result = _validator.ValidateAll(system, dut, test);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.AllErrors, e => e.Contains("步骤ID存在重复"));
    }

    [Fact]
    public void ValidateAll_MixedErrors_AggregatesAllSections()
    {
        // Arrange
        var system = new SystemConfig { LogLevel = "" };
        var dut = new DUTConfig { ProductName = "", MaxConcurrent = 100 };
        var test = new TestConfig { ProjectName = "" };

        // Act
        var result = _validator.ValidateAll(system, dut, test);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.AllErrors, e => e.Contains("[SystemSettings]"));
        Assert.Contains(result.AllErrors, e => e.Contains("[DUTConfiguration]"));
        Assert.Contains(result.AllErrors, e => e.Contains("[TestProjectConfiguration]"));
    }

    [Fact]
    public void SectionResults_ContainsAllThreeSections()
    {
        // Act
        var result = _validator.ValidateAll(new SystemConfig(), new DUTConfig { ProductName = "P" }, new TestConfig { ProjectName = "T" });

        // Assert
        Assert.Equal(3, result.SectionResults.Count);
        Assert.Equal("SystemSettings", result.SectionResults[0].SectionName);
        Assert.Equal("DUTConfiguration", result.SectionResults[1].SectionName);
        Assert.Equal("TestProjectConfiguration", result.SectionResults[2].SectionName);
    }
}
