using System.Collections.Generic;
using NSubstitute;
using UTF.UI.Models;
using UTF.UI.Services;
using UTF.UI.ViewModels;
using Xunit;

namespace UTF.UI.Tests;

/// <summary>
/// Unit tests for <see cref="QuickTestWizardViewModel.BuildInput"/> - verifies
/// that VM-bound properties are projected into the <see cref="QuickTestWizardInput"/>
/// DTO correctly, including the step collection ordering and field mapping.
/// </summary>
public class QuickTestWizardViewModelTests
{
    private static QuickTestWizardViewModel CreateViewModel()
    {
        var configBuilder = Substitute.For<ITestConfigurationBuilder>();
        var configManager = Substitute.For<ConfigurationManager>(
            Substitute.For<UTF.Core.Caching.ICache>(),
            Substitute.For<IConfigurationAdapter>());
        return new QuickTestWizardViewModel(configBuilder, configManager);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildInput_EmptySteps_ProducesEmptyStepList()
    {
        var vm = CreateViewModel();
        vm.ProductName = "Widget";
        vm.ProductModel = "W-100";

        var input = vm.BuildInput();

        Assert.Equal("Widget", input.ProductName);
        Assert.Equal("W-100", input.ProductModel);
        Assert.Empty(input.Steps);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildInput_WithSteps_ProjectsAllStepFields()
    {
        var vm = CreateViewModel();
        vm.ProductName = "Phone";
        vm.ProductModel = "P-7";
        vm.ProductIcon = "📱";
        vm.ProductCategory = "消费电子产品";
        vm.DutCount = 8;
        vm.UseSerial = true;
        vm.UseNetwork = false;

        vm.Steps.Add(new WizardStepInput
        {
            Id = "step_001",
            Order = 1,
            Name = "Version check",
            StepType = "serial",
            Channel = "Serial",
            Command = "version",
            Expected = "contains:V1",
            Timeout = 5000
        });
        vm.Steps.Add(new WizardStepInput
        {
            Id = "step_002",
            Order = 2,
            Name = "MAC check",
            StepType = "network",
            Channel = "Telnet",
            Command = "ifconfig",
            Expected = "regex:[0-9A-F]{12}",
            Timeout = 10000
        });

        var input = vm.BuildInput();

        Assert.Equal("Phone", input.ProductName);
        Assert.Equal("P-7", input.ProductModel);
        Assert.Equal("📱", input.Icon);
        Assert.Equal("消费电子产品", input.Category);
        Assert.Equal(8, input.DUTCount);
        Assert.True(input.UseSerial);
        Assert.False(input.UseNetwork);

        Assert.Equal(2, input.Steps.Count);
        Assert.Equal("step_001", input.Steps[0].Id);
        Assert.Equal(1, input.Steps[0].Order);
        Assert.Equal("Version check", input.Steps[0].Name);
        Assert.Equal("serial", input.Steps[0].StepType);
        Assert.Equal("Serial", input.Steps[0].Channel);
        Assert.Equal("version", input.Steps[0].Command);
        Assert.Equal("contains:V1", input.Steps[0].Expected);
        Assert.Equal(5000, input.Steps[0].Timeout);

        Assert.Equal("step_002", input.Steps[1].Id);
        Assert.Equal("network", input.Steps[1].StepType);
        Assert.Equal("regex:[0-9A-F]{12}", input.Steps[1].Expected);
        Assert.Equal(10000, input.Steps[1].Timeout);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildInput_PreservesStepsOrdering()
    {
        var vm = CreateViewModel();
        vm.Steps.Add(new WizardStepInput { Id = "a", Order = 1, Name = "A" });
        vm.Steps.Add(new WizardStepInput { Id = "b", Order = 2, Name = "B" });
        vm.Steps.Add(new WizardStepInput { Id = "c", Order = 3, Name = "C" });

        var input = vm.BuildInput();

        Assert.Equal(new List<string> { "a", "b", "c" }, new List<string> { input.Steps[0].Id, input.Steps[1].Id, input.Steps[2].Id });
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ValidateInput_NoSteps_ReturnsFalseWithErrorMessage()
    {
        var vm = CreateViewModel();
        var input = new QuickTestWizardInput
        {
            ProductName = "Widget",
            Steps = new List<WizardStepInput>()
        };

        var ok = vm.ValidateInput(input, out var errors);

        Assert.False(ok);
        Assert.Contains(errors, e => e.Contains("步骤"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ValidateInput_NoProductName_ReturnsFalseWithErrorMessage()
    {
        var vm = CreateViewModel();
        var input = new QuickTestWizardInput
        {
            ProductName = "",
            Steps = new List<WizardStepInput> { new() { Id = "s1", Order = 1, Name = "S1" } }
        };

        var ok = vm.ValidateInput(input, out var errors);

        Assert.False(ok);
        Assert.Contains(errors, e => e.Contains("产品名称"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ValidateInput_ValidInput_ReturnsTrue()
    {
        var vm = CreateViewModel();
        var input = new QuickTestWizardInput
        {
            ProductName = "Widget",
            Steps = new List<WizardStepInput> { new() { Id = "s1", Order = 1, Name = "S1" } }
        };

        var ok = vm.ValidateInput(input, out var errors);

        Assert.True(ok);
        Assert.Empty(errors);
    }
}
