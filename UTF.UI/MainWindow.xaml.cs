using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using UTF.Core;
using UTF.Core.Caching;
using UTF.UI.Localization;
using UTF.UI.Services;
using UTF.UI.ViewModels;

namespace UTF.UI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly DispatcherTimer _timer = new();
    private readonly DispatcherTimer _logUpdateTimer = new();
    private readonly MainWindowViewModel _viewModel;
    private UTF.UI.Services.IPermissionManager _permissionManager = null!;
    private readonly ICache _cache;
    private readonly UTF.Logging.ILogger _logger;
    private readonly DUTMonitorManager _dutMonitorManager;
    private readonly ConfigurationManager _configManager;
    private readonly IConfigurationAdapter _configAdapter;
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, object> _configControls = new();
    private int _timerTickCount = 0;
    private DateTime _lastStatisticsUpdate = DateTime.MinValue;

    public MainWindow(
        UTF.Logging.ILogger logger,
        ICache cache,
        ConfigurationManager configManager,
        IConfigurationAdapter configAdapter,
        DUTMonitorManager dutMonitorManager,
        UTF.UI.Services.IPermissionManager permissionManager,
        MainWindowViewModel viewModel,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _cache = cache;
        _configManager = configManager;
        _configAdapter = configAdapter;
        _dutMonitorManager = dutMonitorManager;
        _permissionManager = permissionManager;
        _viewModel = viewModel;
        _serviceProvider = serviceProvider;

        _logger.Info("MainWindow 正在初始化（通过依赖注入）");

        try
        {
            InitializeComponent();

            // MVVM 绑定：将 ViewModel 设为窗口根 DataContext。XAML 中通过 {Binding ...} 绑定各属性。
            DataContext = _viewModel;

            var languageManager = LocalizationService.GetLanguageManager();
            LocalizationHelper.Initialize(languageManager);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"启动错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            throw;
        }

        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += Timer_Tick;
        _timer.Start();

        _logUpdateTimer.Interval = TimeSpan.FromMilliseconds(500);
        _logUpdateTimer.Tick += LogUpdateTimer_Tick;
        _logUpdateTimer.Start();

        InitializeUI();
        UpdateUserDisplay();
        ApplyPermissions();
        _permissionManager.PermissionChanged += OnPermissionChanged;

        _ = LoadAndDisplayProductModelAsync();

        InitializeDUTList();

        _dutMonitorManager.StatisticsUpdateRequested += OnStatisticsUpdateRequested;
        _dutMonitorManager.AllTestsCompleted += OnAllTestsCompleted;

        // 订阅 VM 事件：配置刷新请求（来自辅助窗口保存）与登出请求。
        _viewModel.ConfigurationRefreshRequested += OnConfigurationRefreshRequested;
        _viewModel.LogoutRequested += OnLogoutRequested;

        // Subscribe to configuration changes
        _configManager.ConfigurationChanged += OnConfigurationChanged;

        this.Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshLanguageBindings();
        _ = RefreshStepPreviewAsync();
        _ = LoadUiProfileAsync();
        _ = _viewModel.RefreshConfigValidationAsync();
    }

    /// <summary>
    /// Load UiProfile from unified config and apply operator/engineer shell chrome.
    /// </summary>
    private async System.Threading.Tasks.Task LoadUiProfileAsync()
    {
        try
        {
            await _viewModel.LoadUiProfileFromConfigAsync();
            // Re-attach grid so ShowStepColumns takes effect on dynamic columns.
            var dutGrid = MultiDutBoardPanel?.DutDataGrid;
            if (dutGrid != null)
            {
                _dutMonitorManager.ShowStepColumns = _viewModel.ShowStepColumns;
                _dutMonitorManager.AttachToDataGrid(dutGrid);
            }
        }
        catch (Exception ex)
        {
            _logger?.Error("加载 UiProfile 失败", ex);
        }
    }

    private void InitializeUI()
    {
        UpdateTestStepsList();
        InitializeLog();
    }

    private async void InitializeDUTList()
    {
        try
        {
            // MVVM: InitializeAsync 不再需要 DataGrid 参数；ItemsSource 由 XAML 绑定 DUTItems。
            // 动态列生成通过 AttachToDataGrid 显式调用，确保列结构正确。
            await _dutMonitorManager.InitializeAsync();
            var dutGrid = MultiDutBoardPanel?.DutDataGrid;
            if (dutGrid != null)
            {
                _dutMonitorManager.AttachToDataGrid(dutGrid);
            }

            await _viewModel.LoadWorkbenchProfileFromConfigAsync(forceFromConfig: true);
            System.Diagnostics.Debug.WriteLine("DUT监控台管理器初始化成功");
            _viewModel.UpdatePluginStatus();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"初始化DUT列表失败: {ex.Message}");
            _logger?.Error("DUT监控台管理器初始化失败", ex);

            var dutGrid = MultiDutBoardPanel?.DutDataGrid;
            if (dutGrid != null)
            {
                dutGrid.ItemsSource = _dutMonitorManager.DUTItems;
            }

            LoadSimulatedDUTs();
        }
    }

    private void UpdateTestStepsList()
    {
    }

    private void InitializeLog()
    {
    }

    private void LogUpdateTimer_Tick(object? sender, EventArgs e)
    {
        try
        {
            // P4-30: iterate DUTItems directly (no per-tick list allocation).
            var dutItems = _dutMonitorManager?.DUTItems;
            if (dutItems != null)
            {
                foreach (var dut in dutItems)
                {
                    dut.FlushPendingLogs();
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.Error($"批量刷新日志失败: {ex.Message}", ex);
        }
    }

    private void UpdateUserDisplay()
    {
        UserInfoText.Text = _permissionManager.CurrentUser is { } user
            ? $"用户: {user.DisplayName}"
            : "用户: 未登录";
    }

    private void ApplyPermissions()
    {
        // MVVM: 权限门控已迁移到 MainWindowViewModel.RefreshPermissions()，
        // XAML 中各菜单/按钮通过 IsEnabled="{Binding CanXxx}" 绑定 VM 属性。
        _viewModel.RefreshPermissions();
    }

    private void OnPermissionChanged(object? sender, PermissionChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            UpdateUserDisplay();
            ApplyPermissions();
        });
    }

    protected override void OnClosed(EventArgs e)
    {
        // P1-21: detach event subscriptions to avoid leaks across window recreations.
        _logUpdateTimer?.Stop();
        _timer?.Stop();
        if (_dutMonitorManager != null)
        {
            _dutMonitorManager.StatisticsUpdateRequested -= OnStatisticsUpdateRequested;
            _dutMonitorManager.AllTestsCompleted -= OnAllTestsCompleted;
        }
        // MVVM: VM 也订阅了 DUTMonitorManager / IWindowFactory 事件，需要解绑。
        _viewModel?.DetachManagerEvents();
        if (_viewModel != null)
        {
            _viewModel.ConfigurationRefreshRequested -= OnConfigurationRefreshRequested;
            _viewModel.LogoutRequested -= OnLogoutRequested;
        }
        if (_configManager != null)
        {
            _configManager.ConfigurationChanged -= OnConfigurationChanged;
        }
        Loaded -= MainWindow_Loaded;
        _permissionManager.PermissionChanged -= OnPermissionChanged;
        base.OnClosed(e);
    }

    private void InitializeLanguageMenu() { }
    private void NewSessionBtn_Click(object sender, RoutedEventArgs e) { LoadSimulatedDUTs(); }
    private void LoadPlanBtn_Click(object sender, RoutedEventArgs e) { LoadSimulatedDUTs(); }
    private void NewMultiDutSessionBtn_Click(object sender, RoutedEventArgs e) { }
    private void LoadSessionBtn_Click(object sender, RoutedEventArgs e) { }
    private void PauseSessionBtn_Click(object sender, RoutedEventArgs e) { }
    private void ResumeSessionBtn_Click(object sender, RoutedEventArgs e) { }
    private void StopSessionBtn_Click(object sender, RoutedEventArgs e) { }

    /// <summary>
    /// 状态栏插件徽标点击：委托给 VM 的 <c>OpenPluginManagerCommand</c>。
    /// 保留为代码后置是因为 MouseLeftButtonUp 不支持直接的 Command 绑定。
    /// </summary>
    private void PluginStatus_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_viewModel.OpenPluginManagerCommand.CanExecute(null))
        {
            _viewModel.OpenPluginManagerCommand.Execute(null);
        }
    }

    // ────────────────── VM event bridges ──────────────────

    /// <summary>
    /// VM 请求刷新配置（来自辅助窗口保存 / 配置导入）。处理需要 UI 资源的步骤：
    /// DataGrid 重绑、产品型号显示、步骤预览刷新。
    /// </summary>
    private async void OnConfigurationRefreshRequested(object? sender, EventArgs e)
    {
        try
        {
            var dutGrid = MultiDutBoardPanel?.DutDataGrid;
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (_dutMonitorManager != null && dutGrid != null)
                {
                    dutGrid.ItemsSource = null;
                    dutGrid.Items.Refresh();
                }
            });

            if (_dutMonitorManager != null && dutGrid != null)
            {
                _dutMonitorManager.AttachToDataGrid(dutGrid);
            }

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                RefreshProductModelDisplay();
                _viewModel.UpdateStatistics();
            });

            await _viewModel.RefreshConfigValidationAsync();
            await _viewModel.LoadWorkbenchProfileFromConfigAsync(forceFromConfig: true);
        }
        catch (Exception ex)
        {
            _logger?.Error("刷新配置时发生错误", ex);
        }
    }

    /// <summary>
    /// VM 请求登出：隐藏主窗口，展示登录窗口，成功则恢复，否则关闭应用。
    /// </summary>
    private void OnLogoutRequested(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            Hide();
            var loginWindow = _serviceProvider.GetRequiredService<LoginWindow>();
            if (loginWindow.ShowDialog() == true)
            {
                UpdateUserDisplay();
                ApplyPermissions();
                Show();
                Activate();
                return;
            }

            Application.Current.Shutdown();
        });
    }

    // ────────────────── Configuration change handling ──────────────────

    private async void OnConfigurationChanged(object? sender, EventArgs e)
    {
        await RefreshAfterConfigurationChange();
    }

    private async System.Threading.Tasks.Task RefreshAfterConfigurationChange()
    {
        try
        {
            _logger?.Info("配置已更改，刷新界面...");

            await _viewModel.LoadUiProfileFromConfigAsync();
            await _dutMonitorManager.InitializeAsync();

            // P1-9: these already marshal to the UI thread internally - call directly instead of
            // round-tripping through a ThreadPool task + Dispatcher.Invoke.
            RefreshProductModelDisplay();
            _viewModel.UpdateStatistics();
            await _viewModel.LoadWorkbenchProfileFromConfigAsync(forceFromConfig: true);

            var dutGrid = MultiDutBoardPanel?.DutDataGrid;
            if (dutGrid != null)
            {
                _dutMonitorManager.ShowStepColumns = _viewModel.ShowStepColumns;
                _dutMonitorManager.AttachToDataGrid(dutGrid);
            }

            await RefreshStepPreviewAsync();
            await _viewModel.RefreshConfigValidationAsync();

            _logger?.Info("配置刷新完成");
        }
        catch (Exception ex)
        {
            _logger?.Error("刷新配置失败", ex);
        }
    }

    private async System.Threading.Tasks.Task RefreshStepPreviewAsync()
    {
        try
        {
            var config = await _configManager.GetUnifiedConfigurationAsync();
            var steps = _configAdapter.GetTestSteps(config);
            Dispatcher.Invoke(() =>
            {
                StepCountText.Text = $"{steps.Count}步";
                StepPreviewList.ItemsSource = steps
                    .Where(s => s.Enabled)
                    .Select(s => $"{s.Order}. [{s.Type ?? "?"}] {s.Name}")
                    .ToList();
            });
        }
        catch (Exception ex)
        {
            _logger?.Error("刷新步骤预览失败", ex);
        }
    }

    private void ToggleStepPreview_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (StepPreviewBorder.ToolTip is System.Windows.Controls.ToolTip tt)
        {
            tt.PlacementTarget = StepPreviewBorder;
            tt.IsOpen = !tt.IsOpen;
        }
    }

    private async System.Threading.Tasks.Task LoadAndDisplayProductModelAsync()
    {
        try
        {
            var unifiedConfig = await _configManager.GetUnifiedConfigurationAsync();
            string productModel = _configAdapter.GetProductModel(unifiedConfig);

            Dispatcher.Invoke(() =>
            {
                if (ProductModelText != null)
                {
                    string productModelLabel = LocalizationHelper.GetString("Main.ProductModelLabel", "产品型号:");
                    _viewModel.ProductModelText = $"{productModelLabel} {productModel}";
                }
            });
        }
        catch (Exception ex)
        {
            _logger?.Error("加载产品型号信息时发生错误", ex);
        }
    }

    public async void RefreshProductModelDisplay()
    {
        await LoadAndDisplayProductModelAsync();
    }

    private void RefreshLanguageBindings()
    {
        this.UpdateLayout();
        this.Title = LocalizationHelper.GetString("Main.Title", "通用自动化测试平台 - Universal Test Framework");
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        _timerTickCount++;
        _viewModel.UpdateDateTime();

        if (_timerTickCount % 2 == 0)
        {
            OnStatisticsUpdateRequested();
        }
    }

    private void OnStatisticsUpdateRequested()
    {
        // 委托给 ViewModel 的统计聚合逻辑（DUTMonitorManager.StatisticsUpdateRequested 也调用本方法）。
        _viewModel.UpdateStatistics();
    }

    private void OnAllTestsCompleted()
    {
        _logger?.Info("收到所有测试完成事件，重置按钮状态");

        Application.Current.Dispatcher.Invoke(() =>
        {
            // MVVM: 按钮状态由 IsTestRunning + StartTestButtonText 绑定驱动，无需手动赋值。
        });

        _viewModel.UpdateStatistics();
    }

    private void LoadSimulatedDUTs()
    {
        _dutMonitorManager.DUTItems.Clear();
        _viewModel.UpdateStatistics();
    }
}
