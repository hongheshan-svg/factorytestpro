using System.Collections.Generic;
using NSubstitute;
using UTF.Core;
using UTF.Plugin.Abstractions;
using UTF.UI.Services;
using UTF.UI.ViewModels;
using Xunit;

namespace UTF.UI.Tests;

/// <summary>
/// Unit tests for <see cref="ConfigurationCenterViewModel"/> step-selection
/// synchronization behavior (MaxRetries retrieval from int and string storage).
/// </summary>
public class ConfigurationCenterViewModelTests
{
    private static ConfigurationCenterViewModel CreateViewModel(IPluginCapabilityService? capabilities = null)
    {
        var configManager = Substitute.For<ConfigurationManager>(
            Substitute.For<UTF.Core.Caching.ICache>(),
            Substitute.For<IConfigurationAdapter>());
        var configAdapter = Substitute.For<IConfigurationAdapter>();
        var dialogService = Substitute.For<IDialogService>();
        return new ConfigurationCenterViewModel(configManager, configAdapter, dialogService, capabilities);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void OnSelectedStepChanged_WithIntMaxRetries_SyncsSelectedStepMaxRetries()
    {
        var vm = CreateViewModel();
        var step = new TestStepConfig
        {
            Parameters = new Dictionary<string, object> { ["MaxRetries"] = 3 }
        };

        vm.SelectedStep = step;

        Assert.Equal(3, vm.SelectedStepMaxRetries);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void OnSelectedStepChanged_WithStringMaxRetries_SyncsSelectedStepMaxRetries()
    {
        var vm = CreateViewModel();
        var step = new TestStepConfig
        {
            Parameters = new Dictionary<string, object> { ["MaxRetries"] = "5" }
        };

        vm.SelectedStep = step;

        Assert.Equal(5, vm.SelectedStepMaxRetries);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void OnSelectedStepChanged_WithoutMaxRetries_SetsSelectedStepMaxRetriesNull()
    {
        var vm = CreateViewModel();
        var step = new TestStepConfig
        {
            Parameters = new Dictionary<string, object>()
        };

        vm.SelectedStep = step;

        Assert.Null(vm.SelectedStepMaxRetries);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void OnSelectedStepChanged_NullStep_SetsSelectedStepMaxRetriesNull()
    {
        var vm = CreateViewModel();

        vm.SelectedStep = null;

        Assert.Null(vm.SelectedStepMaxRetries);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ApplyStepMaxRetries_PositiveValue_WritesBackToParameters()
    {
        var vm = CreateViewModel();
        var step = new TestStepConfig
        {
            Parameters = new Dictionary<string, object>()
        };
        vm.SelectedStep = step;
        vm.SelectedStepMaxRetries = 4;

        vm.ApplyStepMaxRetries();

        Assert.Equal(4, step.Parameters!["MaxRetries"]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ApplyStepMaxRetries_NullValue_RemovesFromParameters()
    {
        var vm = CreateViewModel();
        var step = new TestStepConfig
        {
            Parameters = new Dictionary<string, object> { ["MaxRetries"] = 2 }
        };
        vm.SelectedStep = step;
        vm.SelectedStepMaxRetries = null;

        vm.ApplyStepMaxRetries();

        Assert.False(step.Parameters!.ContainsKey("MaxRetries"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void RebuildDynamicParameterFields_WithSchema_BuildsFieldsAndPreservesUnknownKeys()
    {
        var capabilities = Substitute.For<IPluginCapabilityService>();
        capabilities.GetParameterSchema("serial", "serial")
            .Returns(new List<PluginParameterSchemaItem>
            {
                new() { Name = "BaudRate", Type = "int", Label = "Baud rate", Default = "115200" },
                new() { Name = "SerialPort", Type = "string", Label = "Port", Required = true }
            });

        var vm = CreateViewModel(capabilities);
        var step = new TestStepConfig
        {
            Type = "serial",
            Channel = "serial",
            Parameters = new Dictionary<string, object>
            {
                ["BaudRate"] = 9600,
                ["CustomFlag"] = "keep-me"
            }
        };

        vm.SelectedStep = step;

        Assert.True(vm.HasDynamicParameterFields);
        Assert.Equal(2, vm.DynamicParameterFields.Count);
        Assert.Equal("9600", vm.DynamicParameterFields[0].StringValue);
        Assert.Equal("SerialPort", vm.DynamicParameterFields[1].Name);
        Assert.Equal("keep-me", step.Parameters["CustomFlag"]);

        vm.DynamicParameterFields[0].StringValue = "57600";
        vm.ApplyDynamicParameters();

        Assert.Equal(57600, step.Parameters["BaudRate"]);
        Assert.Equal("keep-me", step.Parameters["CustomFlag"]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void RebuildDynamicParameterFields_NoCapabilityService_LeavesFieldsEmpty()
    {
        var vm = CreateViewModel(capabilities: null);
        vm.SelectedStep = new TestStepConfig { Type = "serial", Channel = "serial" };

        Assert.False(vm.HasDynamicParameterFields);
        Assert.Empty(vm.DynamicParameterFields);
    }
}
