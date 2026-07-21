using System;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using UTF.Core;
using UTF.Core.Caching;
using UTF.Logging;
using UTF.Reporting;
using UTF.UI.Models;
using UTF.UI.Services;
using UTF.UI.ViewModels;
using Xunit;

namespace UTF.UI.Tests;

/// <summary>
/// Unit tests for <see cref="MainWindowViewModel"/> permission gating and
/// DUT statistics aggregation. Uses NSubstitute for service-level seams and
/// a real <see cref="DUTMonitorManager"/> (sealed) with mocked dependencies
/// to exercise <see cref="MainWindowViewModel.UpdateStatistics"/>.
/// </summary>
public sealed class MainWindowViewModelTests : IDisposable
{
    private readonly DUTMonitorManager _dutMonitorManager;
    private readonly ConfigurationManager _configManager;
    private readonly IPermissionManager _permissionManager;
    private readonly IDialogService _dialogService;
    private readonly IWindowFactory _windowFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;
    private readonly IConfigurationAdapter _configAdapter;
    private readonly MainWindowViewModel _viewModel;

    public MainWindowViewModelTests()
    {
        var logger = Substitute.For<ILogger>();
        var configAdapter = Substitute.For<IConfigurationAdapter>();
        var pluginHost = new UTF.Plugin.Host.StepExecutorPluginHost(
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "utf-ui-tests-" + Guid.NewGuid().ToString("N")));
        var engine = new ConfigDrivenTestEngine(logger: logger);
        var cache = Substitute.For<ICache>();

        _logger = logger;
        _configAdapter = configAdapter;
        _configManager = new ConfigurationManager(cache, configAdapter);
        var orchestrator = new ConfigDrivenTestOrchestrator(_configManager, engine, logger);
        _dutMonitorManager = new DUTMonitorManager(
            _configManager, configAdapter, pluginHost, orchestrator, logger);
        _permissionManager = Substitute.For<IPermissionManager>();
        _dialogService = Substitute.For<IDialogService>();
        _windowFactory = Substitute.For<IWindowFactory>();
        var services = new ServiceCollection();
        services.AddSingleton<ReportGenerator>();
        _serviceProvider = services.BuildServiceProvider();

        _viewModel = new MainWindowViewModel(
            _dutMonitorManager,
            _configManager,
            _permissionManager,
            _dialogService,
            _windowFactory,
            _serviceProvider,
            _logger,
            _configAdapter);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void RefreshPermissions_HasTestStartPermission_SetsCanStartTestTrue()
    {
        _permissionManager.HasPermission(Permission.TestStart).Returns(true);
        _permissionManager.HasPermission(Permission.TestStop).Returns(false);

        _viewModel.RefreshPermissions();

        Assert.True(_viewModel.CanStartTest);
        Assert.False(_viewModel.CanStopTest);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void RefreshPermissions_HasTestStopPermission_SetsCanStopTestTrue()
    {
        _permissionManager.HasPermission(Permission.TestStart).Returns(false);
        _permissionManager.HasPermission(Permission.TestStop).Returns(true);

        _viewModel.RefreshPermissions();

        Assert.False(_viewModel.CanStartTest);
        Assert.True(_viewModel.CanStopTest);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void RefreshPermissions_NoPermissions_SetsAllFlagsFalse()
    {
        _permissionManager.HasPermission(default(Permission)).Returns(false);

        _viewModel.RefreshPermissions();

        Assert.False(_viewModel.CanStartTest);
        Assert.False(_viewModel.CanStopTest);
        Assert.False(_viewModel.CanImportConfig);
        Assert.False(_viewModel.CanExportReport);
        Assert.False(_viewModel.CanClearLogs);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void UpdateStatistics_EmptyDutItems_ZeroCounts()
    {
        _viewModel.UpdateStatistics();

        Assert.Equal(0, _viewModel.TotalDuts);
        Assert.Equal(0, _viewModel.RunningDuts);
        Assert.Equal(0, _viewModel.WaitingDuts);
        Assert.Equal(0, _viewModel.PassedDuts);
        Assert.Equal(0, _viewModel.FailedDuts);
        Assert.Equal("0%", _viewModel.PassRateText);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void CanOpenTemplatePackPicker_TrueWhenSystemConfigOrTestPlanManagement()
    {
        _permissionManager.HasPermission(Permission.SystemConfig).Returns(true);
        _permissionManager.HasPermission(Permission.TestPlanManagement).Returns(false);
        _viewModel.RefreshPermissions();
        Assert.True(_viewModel.CanOpenTemplatePackPicker);
        Assert.True(_viewModel.OpenTemplatePackPickerCommand.CanExecute(null));

        _permissionManager.HasPermission(Permission.SystemConfig).Returns(false);
        _permissionManager.HasPermission(Permission.TestPlanManagement).Returns(true);
        _viewModel.RefreshPermissions();
        Assert.True(_viewModel.CanOpenTemplatePackPicker);
        Assert.True(_viewModel.OpenTemplatePackPickerCommand.CanExecute(null));

        _permissionManager.HasPermission(Permission.SystemConfig).Returns(false);
        _permissionManager.HasPermission(Permission.TestPlanManagement).Returns(false);
        _viewModel.RefreshPermissions();
        Assert.False(_viewModel.CanOpenTemplatePackPicker);
        Assert.False(_viewModel.OpenTemplatePackPickerCommand.CanExecute(null));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ApplyWorkbenchMode_SingleStation_SetsModeFlags()
    {
        Assert.True(_viewModel.IsMultiDutBoardMode);

        _viewModel.ApplyWorkbenchMode("SingleStation", isSessionOverride: true);

        Assert.Equal(WorkbenchModes.SingleStation, _viewModel.WorkbenchMode);
        Assert.True(_viewModel.IsSingleStationMode);
        Assert.False(_viewModel.IsMultiDutBoardMode);
        Assert.False(_viewModel.IsScanToTestMode);
        Assert.False(_viewModel.IsInstrumentBenchMode);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void OpenTemplatePackPickerCommand_InvokesWindowFactory()
    {
        _permissionManager.HasPermission(Permission.SystemConfig).Returns(true);
        _viewModel.RefreshPermissions();

        _viewModel.OpenTemplatePackPickerCommand.Execute(null);

        _windowFactory.Received(1).ShowTemplatePackPickerDialog();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ApplyWorkbenchMode_UnknownMode_FallsBackToMultiDutBoard()
    {
        _viewModel.ApplyWorkbenchMode("NotARealMode", isSessionOverride: true);

        Assert.Equal(WorkbenchModes.MultiDutBoard, _viewModel.WorkbenchMode);
        Assert.True(_viewModel.IsMultiDutBoardMode);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void UpdateStatistics_MixedStatusDutItems_AggregatesCorrectly()
    {
        _dutMonitorManager.DUTItems.Add(new DUTMonitorItem
        {
            DutId = "DUT-1",
            OverallStatus = DUTMonitorStatus.Running
        });
        _dutMonitorManager.DUTItems.Add(new DUTMonitorItem
        {
            DutId = "DUT-2",
            OverallStatus = DUTMonitorStatus.Idle
        });
        _dutMonitorManager.DUTItems.Add(new DUTMonitorItem
        {
            DutId = "DUT-3",
            OverallStatus = DUTMonitorStatus.Passed
        });
        _dutMonitorManager.DUTItems.Add(new DUTMonitorItem
        {
            DutId = "DUT-4",
            OverallStatus = DUTMonitorStatus.Failed
        });
        _dutMonitorManager.DUTItems.Add(new DUTMonitorItem
        {
            DutId = "DUT-5",
            OverallStatus = DUTMonitorStatus.Error
        });
        _dutMonitorManager.DUTItems.Add(new DUTMonitorItem
        {
            DutId = "DUT-6",
            OverallStatus = DUTMonitorStatus.Timeout
        });

        _viewModel.UpdateStatistics();

        Assert.Equal(6, _viewModel.TotalDuts);
        Assert.Equal(1, _viewModel.RunningDuts);
        Assert.Equal(1, _viewModel.WaitingDuts);
        Assert.Equal(1, _viewModel.PassedDuts);
        Assert.Equal(3, _viewModel.FailedDuts);
        // 1 passed out of 6 total -> ~16.7%
        Assert.Equal("16.7%", _viewModel.PassRateText);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ApplyConfigValidation_WithErrors_SetsBannerAndDisablesStart()
    {
        _permissionManager.HasPermission(Permission.TestStart).Returns(true);
        _viewModel.RefreshPermissions();

        _viewModel.ApplyConfigValidation(new[]
        {
            "缺少产品型号",
            "步骤 step1 缺少 Command",
            "步骤 step2 超时无效",
            "多余的第四条"
        });

        Assert.True(_viewModel.HasConfigErrors);
        Assert.Contains("4 个问题", _viewModel.ConfigValidationSummary);
        Assert.Contains("缺少产品型号", _viewModel.ConfigValidationSummary);
        Assert.Contains("另有 1 项", _viewModel.ConfigValidationSummary);
        Assert.False(_viewModel.IsStartTestEnabled);
        Assert.False(_viewModel.StartTestCommand.CanExecute(null));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ApplyConfigValidation_NoErrors_ClearsBannerAndAllowsStartWhenPermitted()
    {
        _permissionManager.HasPermission(Permission.TestStart).Returns(true);
        _viewModel.RefreshPermissions();
        _viewModel.ApplyConfigValidation(new[] { "temp error" });

        _viewModel.ApplyConfigValidation(System.Array.Empty<string>());

        Assert.False(_viewModel.HasConfigErrors);
        Assert.Equal(string.Empty, _viewModel.ConfigValidationSummary);
        Assert.True(_viewModel.IsStartTestEnabled);
        Assert.True(_viewModel.StartTestCommand.CanExecute(null));
    }

    public void Dispose()
    {
        _viewModel?.DetachManagerEvents();
        (_dutMonitorManager as IDisposable)?.Dispose();
        (_serviceProvider as IDisposable)?.Dispose();
    }
}
