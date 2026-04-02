using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using UTF.Core;
using UTF.Core.Caching;
using UTF.Reporting;
using UTF.UI.ViewModels;
using UTF.UI.Localization;
using UTF.UI.Services;

namespace UTF.UI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly DispatcherTimer _timer = new();
    private readonly DispatcherTimer _testCompletionMonitor = new();
    private readonly DispatcherTimer _logUpdateTimer = new();
    private readonly object _testStatsLock = new();
    private bool _isTestRunning = false;
    private UTF.UI.Services.IPermissionManager _permissionManager = null!;
    private DUTTestListViewModel _dutListViewModel = null!;
    private readonly ITestEngine _testEngine;
    private readonly ICache _cache;
    private readonly UTF.Logging.ILogger _logger;
    private readonly DUTMonitorManager _dutMonitorManager;
    private readonly ConfigurationManager _configManager;
    private readonly IConfigurationAdapter _configAdapter;
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, object> _configControls = new();
    private int _timerTickCount = 0;
    private int _cachedTotal = 0;
    private int _cachedRunning = 0;
    private int _cachedWaiting = 0;
    private int _cachedPassed = 0;
    private int _cachedFailed = 0;
    private DateTime _lastStatisticsUpdate = DateTime.MinValue;

    public MainWindow(
        UTF.Logging.ILogger logger,
        ICache cache,
        ITestEngine testEngine,
        ConfigurationManager configManager,
        IConfigurationAdapter configAdapter,
        DUTMonitorManager dutMonitorManager,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _cache = cache;
        _testEngine = testEngine;
        _configManager = configManager;
        _configAdapter = configAdapter;
        _dutMonitorManager = dutMonitorManager;
        _serviceProvider = serviceProvider;

        _logger.Info("MainWindow 正在初始化（通过依赖注入）");

        try
        {
            InitializeComponent();

            _permissionManager = new UTF.UI.Services.PermissionManager();

            var languageManager = LocalizationService.GetLanguageManager();
            LocalizationHelper.Initialize(languageManager);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"启动错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            throw;
        }

        _dutListViewModel = new DUTTestListViewModel();

        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += Timer_Tick;
        _timer.Start();

        _logUpdateTimer.Interval = TimeSpan.FromMilliseconds(500);
        _logUpdateTimer.Tick += LogUpdateTimer_Tick;
        _logUpdateTimer.Start();

        InitializeUI();
        UpdateUserDisplay();

        _ = LoadAndDisplayProductModelAsync();

        InitializeDUTList();

        _dutMonitorManager.StatisticsUpdateRequested += UpdateDUTStatistics;
        _dutMonitorManager.AllTestsCompleted += OnAllTestsCompleted;

        // Subscribe to configuration changes
        _configManager.ConfigurationChanged += OnConfigurationChanged;

        this.Loaded += MainWindow_Loaded;

        UpdateTestButtonStates();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshLanguageBindings();
        _ = RefreshStepPreviewAsync();
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
            await _dutMonitorManager.InitializeAsync(MainDUTListDataGrid);
            System.Diagnostics.Debug.WriteLine("DUT监控台管理器初始化成功");
            var loaded = _dutMonitorManager.LoadedPlugins.Count;
            var failed = _dutMonitorManager.LastLoadReport?.FailedCount ?? 0;
            UpdatePluginStatusBar(loaded, failed);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"初始化DUT列表失败: {ex.Message}");
            _logger?.Error("DUT监控台管理器初始化失败", ex);

            MainDUTListDataGrid.ItemsSource = _dutListViewModel.DUTs;
            LoadSimulatedDUTs();
        }
    }

    private void UpdateTestStepsList()
    {
    }

    private void UpdateStatistics()
    {
        UpdateDUTStatistics();
    }

    private void InitializeLog()
    {
    }

    private void LogUpdateTimer_Tick(object? sender, EventArgs e)
    {
        try
        {
            var dutItems = _dutMonitorManager?.GetDUTItems();
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

    private void UpdatePluginStatusBar(int loaded, int failed)
    {
        Dispatcher.Invoke(() =>
        {
            PluginStatusText.Text = $"插件: {loaded}个";
            PluginStatusBorder.Background = new System.Windows.Media.SolidColorBrush(
                failed > 0
                    ? System.Windows.Media.Color.FromRgb(0xE7, 0x4C, 0x3C)
                    : System.Windows.Media.Color.FromRgb(0x27, 0xAE, 0x60));
            PluginStatusBorder.ToolTip = failed > 0
                ? $"已加载 {loaded} 个插件，{failed} 个加载失败，点击查看详情"
                : $"已加载 {loaded} 个插件，点击查看详情";
        });
    }

    private void UpdateUserDisplay()
    {
        var userDisplay = FindUserDisplayInMenu();
        if (userDisplay != null)
        {
            if (_permissionManager.CurrentUser != null)
            {
                userDisplay.Text = $"用户: {_permissionManager.CurrentUser.DisplayName}";
            }
            else
            {
                userDisplay.Text = "用户: 管理员";
            }
        }
    }

    private void ApplyPermissions()
    {
    }

    private System.Windows.Controls.TextBlock? FindUserDisplayInMenu()
    {
        return null;
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer?.Stop();
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

    private void ExitApp_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show("确定要退出系统吗？", "确认退出",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            Application.Current.Shutdown();
        }
    }

    private void OpenConfigCenter_Click(object sender, RoutedEventArgs e)
    {
        var win = _serviceProvider.GetRequiredService<ConfigurationCenterWindow>();
        win.Owner = this;
        if (win.ShowDialog() == true)
        {
            _ = RefreshConfigurationAfterImportAsync();
        }
    }

    private void OpenQuickTestWizard_Click(object sender, RoutedEventArgs e)
    {
        var win = _serviceProvider.GetRequiredService<QuickTestWizardWindow>();
        win.Owner = this;
        win.ConfigurationCreated += (_, _) => _ = RefreshConfigurationAfterImportAsync();
        win.ShowDialog();
    }

    private void OpenTestPlanEditor_Click(object sender, RoutedEventArgs e)
    {
        var win = _serviceProvider.GetRequiredService<TestPlanEditorWindow>();
        win.Owner = this;
        if (win.ShowDialog() == true)
        {
            _ = RefreshConfigurationAfterImportAsync();
        }
    }

    private void OpenPluginManager_Click(object sender, RoutedEventArgs e)
    {
        var win = new PluginManagementWindow(_dutMonitorManager);
        win.Owner = this;
        win.ShowDialog();
    }

    private void OpenDeviceManager_Click(object sender, RoutedEventArgs e)
    {
        var deviceWindow = new DeviceManagementWindow(_permissionManager)
        {
            Owner = this
        };
        deviceWindow.ShowDialog();
    }

    private void OpenUserManager_Click(object sender, RoutedEventArgs e)
    {
        var userWindow = new UserManagementWindow(_permissionManager)
        {
            Owner = this
        };
        userWindow.ShowDialog();
    }

    private void PluginStatus_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        OpenPluginManager_Click(sender, null!);
    }

    private async void OnConfigurationChanged(object? sender, EventArgs e)
    {
        await RefreshAfterConfigurationChange();
    }

    private async System.Threading.Tasks.Task RefreshAfterConfigurationChange()
    {
        try
        {
            _logger?.Info("配置已更改，刷新界面...");

            await _dutMonitorManager.InitializeAsync(MainDUTListDataGrid);

            await System.Threading.Tasks.Task.Run(() =>
            {
                Dispatcher.Invoke(() =>
                {
                    RefreshProductModelDisplay();
                    UpdateDUTStatistics();
                });
            });

            await RefreshStepPreviewAsync();

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
                    ProductModelText.Text = $"{productModelLabel} {productModel}";
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
        UpdateTestButtonStates();
    }

    private void UpdateTestButtonStates()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => UpdateTestButtonStates());
            return;
        }

        try
        {
            StartTestBtn.IsEnabled = !_isTestRunning;
            StopTestBtn.IsEnabled = _isTestRunning;

            if (_isTestRunning)
            {
                StartTestBtn.Content = "▶️ 测试进行中...";
                StopTestBtn.Content = "⏹️ 停止测试";
            }
            else
            {
                StartTestBtn.Content = "▶️ 开始测试";
                StopTestBtn.Content = "⏹️ 停止测试";
            }
        }
        catch (Exception ex)
        {
            _logger?.Error($"更新测试按钮状态失败: {ex.Message}", ex);
        }
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        _timerTickCount++;
        DateTimeText.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        if (_timerTickCount % 2 == 0)
        {
            UpdateDUTStatistics();
        }
    }

    private void UpdateDUTStatistics()
    {
        try
        {
            var now = DateTime.Now;
            if ((now - _lastStatisticsUpdate).TotalMilliseconds < 500)
            {
                return;
            }
            _lastStatisticsUpdate = now;

            var dutItems = _dutMonitorManager?.GetDUTItems();
            if (dutItems == null || dutItems.Count == 0)
            {
                return;
            }

            int total = 0, running = 0, waiting = 0, passed = 0, failed = 0;
            foreach (var dut in dutItems)
            {
                total++;
                switch (dut.OverallStatus)
                {
                    case UTF.UI.Models.DUTMonitorStatus.Running:
                        running++;
                        break;
                    case UTF.UI.Models.DUTMonitorStatus.Idle:
                        waiting++;
                        break;
                    case UTF.UI.Models.DUTMonitorStatus.Passed:
                        passed++;
                        break;
                    case UTF.UI.Models.DUTMonitorStatus.Failed:
                    case UTF.UI.Models.DUTMonitorStatus.Error:
                    case UTF.UI.Models.DUTMonitorStatus.Timeout:
                        failed++;
                        break;
                }
            }

            _cachedTotal = total;
            _cachedRunning = running;
            _cachedWaiting = waiting;
            _cachedPassed = passed;
            _cachedFailed = failed;

            var passRate = _cachedTotal > 0 ? (_cachedPassed * 100.0 / _cachedTotal) : 0;

            Dispatcher.Invoke(() =>
            {
                TotalDUTsText.Text = _cachedTotal.ToString("N0");
                RunningDUTsText.Text = _cachedRunning.ToString("N0");
                WaitingDUTsText.Text = _cachedWaiting.ToString("N0");
                PassedDUTsText.Text = _cachedPassed.ToString("N0");
                FailedDUTsText.Text = _cachedFailed.ToString("N0");
                PassRateMainText.Text = $"{passRate:F1}%";
            });
        }
        catch (Exception ex)
        {
            _logger?.Error("更新DUT统计失败", ex);
        }
    }

    private void OnAllTestsCompleted()
    {
        _logger?.Info("收到所有测试完成事件，重置按钮状态");

        Application.Current.Dispatcher.Invoke(() =>
        {
            _isTestRunning = false;
            UpdateTestButtonStates();
            StatusText.Text = "所有测试已完成";
            StatusTextFooter.Text = "所有测试已完成";
        });

        UpdateDUTStatistics();
    }

    private void LoadSimulatedDUTs()
    {
        _dutListViewModel.DUTs.Clear();
        UpdateDUTStatistics();
    }

    private async System.Threading.Tasks.Task RefreshConfigurationAfterImportAsync()
    {
        try
        {
            _logger?.Info("开始刷新配置...");

            if (_configManager != null)
            {
                await _configManager.RefreshConfiguration();
            }

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (_dutMonitorManager != null && MainDUTListDataGrid != null)
                {
                    MainDUTListDataGrid.ItemsSource = null;
                    MainDUTListDataGrid.Items.Refresh();
                }
            });

            if (MainDUTListDataGrid != null)
            {
                await _dutMonitorManager.InitializeAsync(MainDUTListDataGrid);
            }

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (!_isTestRunning)
                {
                    StatusText.Text = "配置已刷新，系统已就绪";
                    StatusTextFooter.Text = "配置已刷新，系统已就绪";
                }

                RefreshProductModelDisplay();
                UpdateDUTStatistics();
            });

            _logger?.Info("配置刷新完成");
        }
        catch (Exception ex)
        {
            _logger?.Error("刷新配置时发生错误", ex);
        }
    }

    private async void StartTestBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_isTestRunning) return;

        try
        {
            _isTestRunning = true;
            UpdateTestButtonStates();
            await _dutMonitorManager.StartAllTestsAsync();
            StatusText.Text = "测试进行中...";
            StatusTextFooter.Text = "测试进行中...";
        }
        catch (Exception ex)
        {
            _isTestRunning = false;
            UpdateTestButtonStates();
            MessageBox.Show($"启动测试失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void StopTestBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!_isTestRunning) return;

        try
        {
            _isTestRunning = false;
            UpdateTestButtonStates();
            _dutMonitorManager.StopAllTests();
            StatusText.Text = "测试已停止";
            StatusTextFooter.Text = "测试已停止";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"停止测试失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void ImportConfig_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "导入系统配置",
                Filter = "配置文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                DefaultExt = ".json"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                var jsonContent = await System.IO.File.ReadAllTextAsync(openFileDialog.FileName);
                var config = System.Text.Json.JsonSerializer.Deserialize<UnifiedConfiguration>(jsonContent);
                if (config != null)
                {
                    await _configManager.SaveUnifiedConfigurationAsync(config);
                    await RefreshConfigurationAfterImportAsync();
                    MessageBox.Show("配置导入成功！", "导入成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导入配置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void RefreshDUTsBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _dutMonitorManager.InitializeAsync(MainDUTListDataGrid);
            UpdateDUTStatistics();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"刷新DUT列表失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void ExportReportBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var reportGenerator = _serviceProvider.GetService<ReportGenerator>();
            if (reportGenerator == null)
            {
                MessageBox.Show("报告生成器未注册，无法导出报告。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "导出测试报告",
                Filter = "HTML 报告 (*.html)|*.html|CSV 报告 (*.csv)|*.csv|JSON 报告 (*.json)|*.json|所有文件 (*.*)|*.*",
                DefaultExt = ".html",
                FileName = $"TestReport_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (saveDialog.ShowDialog() != true) return;

            var dutItems = _dutMonitorManager.DUTItems;
            var columns = new List<string> { "DUT ID", "名称", "状态", "当前步骤" };
            var rows = new List<Dictionary<string, object>>();

            foreach (var dut in dutItems)
            {
                rows.Add(new Dictionary<string, object>
                {
                    ["DUT ID"] = dut.DutId,
                    ["名称"] = dut.DutName,
                    ["状态"] = dut.OverallStatus.ToString(),
                    ["当前步骤"] = dut.CurrentStepText ?? ""
                });
            }

            var dataSet = new ReportDataSet
            {
                Name = "UTF 测试报告",
                Description = $"生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                Columns = columns,
                Rows = rows,
                Metadata = new Dictionary<string, object>
                {
                    ["Operator"] = _permissionManager?.CurrentUser?.Username ?? "未知",
                    ["Application"] = "UniversalTestFramework",
                    ["总DUT数"] = dutItems.Count,
                    ["通过数"] = dutItems.Count(d => d.OverallStatus == Models.DUTMonitorStatus.Passed),
                    ["失败数"] = dutItems.Count(d => d.OverallStatus == Models.DUTMonitorStatus.Failed)
                }
            };

            var extension = Path.GetExtension(saveDialog.FileName).ToLowerInvariant();
            var format = extension switch
            {
                ".csv" => ReportFormat.CSV,
                ".json" => ReportFormat.JSON,
                _ => ReportFormat.HTML
            };

            var template = new ReportTemplate
            {
                TemplateId = "default-export",
                Name = "默认导出模板",
                Content = "<html><body><h1>{{Title}}</h1></body></html>"
            };

            var result = await reportGenerator.GenerateReportFromTemplateAsync(
                template, dataSet, format, saveDialog.FileName);

            if (result.Success)
            {
                MessageBox.Show($"报告已导出至:\n{saveDialog.FileName}", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show($"导出报告失败: {result.ErrorMessage}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导出报告失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ClearAllLogsBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _dutMonitorManager.ResetAllDUTs();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"清除日志失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ViewDUTLogBtn_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("查看DUT日志", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ViewDUTDetailBtn_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("查看DUT详情", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void RetestDUTBtn_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("重新测试DUT", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
