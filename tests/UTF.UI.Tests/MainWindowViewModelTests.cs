using System;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using UTF.Configuration.Models;
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
    public void ApplyUiProfile_Null_UsesFullEngineerDefaults()
    {
        _permissionManager.HasPermission(Permission.SystemConfig).Returns(true);
        _permissionManager.CurrentUser.Returns(new UserInfo
        {
            Username = "admin",
            DisplayName = "Admin",
            Role = UserRole.Admin
        });

        _viewModel.ApplyUiProfile(null);

        Assert.True(_viewModel.ShowEngineeringMenus);
        Assert.False(_viewModel.ShowOperatorChrome);
        Assert.Equal("MultiDutBoard", _viewModel.UiModeDisplayName);
        Assert.Equal("DUT", _viewModel.UnitLabel);
        Assert.True(_viewModel.ShowStepColumns);
        Assert.True(_viewModel.CanConfigureSystem);
        Assert.True(_viewModel.CanImportConfig);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ApplyUiProfile_AllowConfigEditFalse_HidesEngineeringMenus()
    {
        _permissionManager.HasPermission(Permission.SystemConfig).Returns(true);
        _permissionManager.CurrentUser.Returns(new UserInfo
        {
            Username = "eng",
            Role = UserRole.Engineer
        });

        _viewModel.ApplyUiProfile(new UiProfile
        {
            Mode = "SingleStation",
            AllowConfigEdit = false,
            ShowAdvancedMenus = true
        });

        Assert.False(_viewModel.ShowEngineeringMenus);
        Assert.True(_viewModel.ShowOperatorChrome);
        Assert.False(_viewModel.CanConfigureSystem);
        Assert.False(_viewModel.CanImportConfig);
        Assert.False(_viewModel.CanManageTestPlans);
        Assert.Equal("SingleStation", _viewModel.UiModeDisplayName);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ApplyUiProfile_OperatorRole_ForcesSimplifiedChromeEvenWhenProfileAllowsEdit()
    {
        // Security > profile: Operator must not see engineering menus even with full profile.
        _permissionManager.HasPermission(Permission.SystemConfig).Returns(true);
        _permissionManager.HasPermission(Permission.TestPlanManagement).Returns(true);
        _permissionManager.CurrentUser.Returns(new UserInfo
        {
            Username = "op1",
            DisplayName = "Operator",
            Role = UserRole.Operator
        });

        _viewModel.ApplyUiProfile(new UiProfile
        {
            Mode = "MultiDutBoard",
            AllowConfigEdit = true,
            ShowAdvancedMenus = true
        });

        Assert.False(_viewModel.ShowEngineeringMenus);
        Assert.True(_viewModel.ShowOperatorChrome);
        Assert.False(_viewModel.CanConfigureSystem);
        Assert.False(_viewModel.CanImportConfig);
        Assert.False(_viewModel.CanManageTestPlans);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ApplyUiProfile_ObserverRole_ForcesSimplifiedChrome()
    {
        _permissionManager.HasPermission(Permission.SystemConfig).Returns(true);
        _permissionManager.CurrentUser.Returns(new UserInfo
        {
            Username = "obs",
            Role = UserRole.Observer
        });

        _viewModel.ApplyUiProfile(UiProfile.CreateDefault());

        Assert.False(_viewModel.ShowEngineeringMenus);
        Assert.True(_viewModel.ShowOperatorChrome);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ApplyUiProfile_UnitLabel_UpdatesTerminologyLabels()
    {
        _viewModel.ApplyUiProfile(new UiProfile
        {
            UnitLabel = "Station",
            Mode = "SingleStation",
            ShowStepColumns = false
        });

        Assert.Equal("Station", _viewModel.UnitLabel);
        Assert.Equal("📊 Station统计:", _viewModel.UnitStatsLabel);
        Assert.Equal("🎛️ Station监控台", _viewModel.MonitorTitleText);
        Assert.False(_viewModel.ShowStepColumns);
        Assert.False(_dutMonitorManager.ShowStepColumns);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ApplyUiProfile_LacksSystemConfig_HidesEngineeringMenus()
    {
        _permissionManager.HasPermission(Permission.SystemConfig).Returns(false);
        _permissionManager.CurrentUser.Returns(new UserInfo
        {
            Username = "tech",
            Role = UserRole.Technician
        });

        _viewModel.ApplyUiProfile(UiProfile.CreateDefault());

        Assert.False(_viewModel.ShowEngineeringMenus);
        Assert.True(_viewModel.ShowOperatorChrome);
        Assert.False(_viewModel.CanConfigureSystem);
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

    public void Dispose()
    {
        _viewModel?.DetachManagerEvents();
        (_dutMonitorManager as IDisposable)?.Dispose();
        (_serviceProvider as IDisposable)?.Dispose();
    }
}
