using System.Collections.Generic;
using NSubstitute;
using UTF.UI.Models;
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
    private static ConfigurationCenterViewModel CreateViewModel()
    {
        var configManager = Substitute.For<ConfigurationManager>(
            Substitute.For<UTF.Core.Caching.ICache>(),
            Substitute.For<IConfigurationAdapter>());
        var configAdapter = Substitute.For<IConfigurationAdapter>();
        var dialogService = Substitute.For<IDialogService>();
        return new ConfigurationCenterViewModel(configManager, configAdapter, dialogService);
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
}
