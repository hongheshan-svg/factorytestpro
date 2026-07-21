using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using UTF.Configuration.Models;
using UTF.Core;
using UTF.Core.Caching;
using UTF.Reporting;
using UTF.UI.Models;
using UTF.UI.Services;

namespace UTF.UI.ViewModels;

/// <summary>
/// 主窗口视图模型。承载测试运行状态、DUT 统计、产品信息以及主导航命令。
/// 所有菜单 / 工具栏交互均通过 <see cref="RelayCommand"/> 暴露，对话框与窗口展示
/// 委托给 <see cref="IDialogService"/> / <see cref="IWindowFactory"/>，避免 VM 直接
/// 引用 <c>MessageBox</c> / 具体窗口类型。步骤预览、日志刷新等仍保留在代码后置中，
/// 通过 <see cref="UpdateStatistics"/> / <see cref="UpdateDateTime"/> 由代码后置调用。
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly DUTMonitorManager _dutMonitorManager;
    private readonly ConfigurationManager _configManager;
    private readonly IPermissionManager _permissionManager;
    private readonly IDialogService _dialogService;
    private readonly IWindowFactory _windowFactory;
    private readonly ReportGenerator? _reportGenerator;
    private readonly IServiceProvider _serviceProvider;
    private readonly UTF.Logging.ILogger _logger;
    private readonly IConfigurationAdapter _configAdapter;

    /// <summary>Active UI profile (defaults to full engineer chrome when config omits it).</summary>
    private UiProfile _uiProfile = UiProfile.CreateDefault();

    public MainWindowViewModel(
        DUTMonitorManager dutMonitorManager,
        ConfigurationManager configManager,
        IPermissionManager permissionManager,
        IDialogService dialogService,
        IWindowFactory windowFactory,
        IServiceProvider serviceProvider,
        UTF.Logging.ILogger logger,
        IConfigurationAdapter configAdapter)
    {
        _dutMonitorManager = dutMonitorManager ?? throw new ArgumentNullException(nameof(dutMonitorManager));
        _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
        _permissionManager = permissionManager ?? throw new ArgumentNullException(nameof(permissionManager));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _windowFactory = windowFactory ?? throw new ArgumentNullException(nameof(windowFactory));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configAdapter = configAdapter ?? throw new ArgumentNullException(nameof(configAdapter));
        _reportGenerator = _serviceProvider.GetService<ReportGenerator>();

        DUTItems = _dutMonitorManager.DUTItems;

        // 跟随 DUTMonitorManager 的统计刷新事件，将统计聚合到本 VM 的属性。
        _dutMonitorManager.StatisticsUpdateRequested += OnStatisticsUpdateRequested;
        _dutMonitorManager.AllTestsCompleted += OnAllTestsCompleted;

        // 权限门控：初始计算各 Can* 标志，并订阅权限变更事件以保持同步。
        ApplyUiProfile(null);
        RefreshPermissions();
        _permissionManager.PermissionChanged += OnPermissionChanged;

        // 辅助窗口保存配置后，触发刷新流程（由代码后置订阅以处理 DataGrid 等 UI 资源）。
        _windowFactory.ConfigurationApplied += OnConfigurationApplied;
    }

    /// <summary>
    /// 释放事件订阅（由 MainWindow.OnClosed 调用以避免重复刷新）。
    /// </summary>
    public void DetachManagerEvents()
    {
        _dutMonitorManager.StatisticsUpdateRequested -= OnStatisticsUpdateRequested;
        _dutMonitorManager.AllTestsCompleted -= OnAllTestsCompleted;
        _permissionManager.PermissionChanged -= OnPermissionChanged;
        _windowFactory.ConfigurationApplied -= OnConfigurationApplied;
    }

    // ────────────────── Observable properties ──────────────────

    /// <summary>当前是否有测试在运行。</summary>
    [ObservableProperty]
    private bool _isTestRunning;

    /// <summary>顶部状态栏文本（默认就绪）。</summary>
    [ObservableProperty]
    private string _statusText = "就绪";

    /// <summary>顶部状态栏尾部副本（默认就绪）。</summary>
    [ObservableProperty]
    private string _statusTextFooter = "就绪";

    /// <summary>产品型号显示文本。</summary>
    [ObservableProperty]
    private string _productModelText = "未加载";

    /// <summary>当前日期时间显示文本。</summary>
    [ObservableProperty]
    private string _dateTimeText = string.Empty;

    /// <summary>DUT 总数。</summary>
    [ObservableProperty]
    private int _totalDuts;

    /// <summary>运行中 DUT 数。</summary>
    [ObservableProperty]
    private int _runningDuts;

    /// <summary>等待中 DUT 数。</summary>
    [ObservableProperty]
    private int _waitingDuts;

    /// <summary>通过 DUT 数。</summary>
    [ObservableProperty]
    private int _passedDuts;

    /// <summary>失败 DUT 数。</summary>
    [ObservableProperty]
    private int _failedDuts;

    /// <summary>通过率显示（含百分号）。</summary>
    [ObservableProperty]
    private string _passRateText = "0%";

    /// <summary>已加载插件数（状态栏）。</summary>
    [ObservableProperty]
    private int _loadedPlugins;

    /// <summary>加载失败插件数（状态栏）。</summary>
    [ObservableProperty]
    private int _failedPlugins;

    /// <summary>
    /// 启动测试按钮显示文本。随 <see cref="IsTestRunning"/> 切换“开始测试”/“测试进行中...”。
    /// 取代代码后置对按钮 Content 的直接赋值。
    /// </summary>
    [ObservableProperty]
    private string _startTestButtonText = "▶ 开始测试";

    // ────────────────── Permission-gating flags ──────────────────
    // 这些标志由 RefreshPermissions() 基于 IPermissionManager.HasPermission 计算，
    // 供 XAML 中 IsEnabled="{Binding CanXxx}" 绑定使用，取代 MainWindow.ApplyPermissions
    // 中对控件 IsEnabled 的直接赋值。命令的 CanExecute 也绑定到这些标志。

    /// <summary>当前用户是否有启动测试权限。</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartTestCommand))]
    private bool _canStartTest;

    /// <summary>当前用户是否有停止测试权限。</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StopTestCommand))]
    private bool _canStopTest;

    /// <summary>当前用户是否有导入系统配置权限。</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ImportConfigCommand))]
    private bool _canImportConfig;

    /// <summary>当前用户是否有导出报告权限。</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportReportCommand))]
    private bool _canExportReport;

    /// <summary>当前用户是否有清除日志权限。</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ClearAllLogsCommand))]
    private bool _canClearLogs;

    /// <summary>当前用户是否有重新测试 DUT 权限。</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RetestDutCommand))]
    private bool _canRetestDut;

    /// <summary>当前用户是否有系统配置权限（用于配置中心、插件管理）。</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenConfigurationCenterCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenPluginManagerCommand))]
    private bool _canConfigureSystem;

    /// <summary>当前用户是否有测试计划管理权限（用于快速创建、测试计划编辑器）。</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenQuickTestWizardCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenTestPlanEditorCommand))]
    private bool _canManageTestPlans;

    /// <summary>当前用户是否有测试计划创建权限（用于快速创建向导）。</summary>
    [ObservableProperty]
    private bool _canCreateTestPlan;

    /// <summary>当前用户是否有测试计划编辑权限（用于测试计划编辑器）。</summary>
    [ObservableProperty]
    private bool _canEditTestPlan;

    /// <summary>当前用户是否有设备管理权限。</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenDeviceManagerCommand))]
    private bool _canManageDevices;

    /// <summary>当前用户是否有用户管理权限。</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenUserManagerCommand))]
    private bool _canManageUsers;

    // ────────────────── UiProfile / shell chrome ──────────────────

    /// <summary>
    /// Engineering menus (config center, plugin manager, test plan editor, import config).
    /// Bound to menu Visibility; false = operator-simplified shell.
    /// </summary>
    [ObservableProperty]
    private bool _showEngineeringMenus = true;

    /// <summary>
    /// Operator-oriented chrome (status badge, simplified emphasis). True when engineering menus are hidden.
    /// </summary>
    [ObservableProperty]
    private bool _showOperatorChrome;

    /// <summary>UiProfile.Mode text for the status bar (e.g. MultiDutBoard).</summary>
    [ObservableProperty]
    private string _uiModeDisplayName = "MultiDutBoard";

    /// <summary>Unit terminology from UiProfile (default DUT).</summary>
    [ObservableProperty]
    private string _unitLabel = "DUT";

    /// <summary>Status-bar stats header, e.g. "📊 DUT统计:".</summary>
    [ObservableProperty]
    private string _unitStatsLabel = "📊 DUT统计:";

    /// <summary>Monitor panel title, e.g. "🎛️ DUT监控台".</summary>
    [ObservableProperty]
    private string _monitorTitleText = "🎛️ DUT监控台";

    /// <summary>Whether dynamic step columns should be shown (UiProfile.ShowStepColumns).</summary>
    [ObservableProperty]
    private bool _showStepColumns = true;

    /// <summary>
    /// Snapshot of the active profile (for diagnostics / tests). Never null after construction.
    /// </summary>
    public UiProfile ActiveUiProfile => _uiProfile;

    /// <summary>
    /// Apply a <see cref="UiProfile"/> (null → full engineer defaults) and refresh chrome flags.
    /// Safe to call from unit tests without loading config.
    /// </summary>
    public void ApplyUiProfile(UiProfile? profile)
    {
        _uiProfile = profile ?? UiProfile.CreateDefault();

        UnitLabel = string.IsNullOrWhiteSpace(_uiProfile.UnitLabel) ? "DUT" : _uiProfile.UnitLabel.Trim();
        UnitStatsLabel = $"📊 {UnitLabel}统计:";
        MonitorTitleText = $"🎛️ {UnitLabel}监控台";
        UiModeDisplayName = string.IsNullOrWhiteSpace(_uiProfile.Mode) ? "MultiDutBoard" : _uiProfile.Mode.Trim();
        ShowStepColumns = _uiProfile.ShowStepColumns;

        // Keep grid column generation in sync when a DataGrid is attached.
        _dutMonitorManager.ShowStepColumns = ShowStepColumns;

        RefreshPermissions();
    }

    /// <summary>
    /// Load <see cref="UiProfile"/> from the unified configuration (missing → engineer defaults).
    /// Called on MainWindow load and after configuration refresh.
    /// </summary>
    public async Task LoadUiProfileFromConfigAsync()
    {
        try
        {
            var config = await _configManager.GetUnifiedConfigurationAsync().ConfigureAwait(true);
            ApplyUiProfile(config.UiProfile);
        }
        catch (Exception ex)
        {
            _logger?.Error("加载 UiProfile 失败，使用默认工程师界面", ex);
            ApplyUiProfile(null);
        }
    }

    /// <summary>
    /// 基于 <see cref="IPermissionManager"/> 重新计算所有 Can* 标志，
    /// 再按 UiProfile + 角色收紧工程菜单（安全优先于配置）。
    /// 在构造函数、权限变更事件、登录/登出后调用，由 XAML 绑定消费。
    /// </summary>
    public void RefreshPermissions()
    {
        CanStartTest = _permissionManager.HasPermission(Permission.TestStart);
        CanStopTest = _permissionManager.HasPermission(Permission.TestStop);
        CanImportConfig = _permissionManager.HasPermission(Permission.SystemConfig);
        CanExportReport = _permissionManager.HasPermission(Permission.DataExport)
            || _permissionManager.HasPermission(Permission.DataView)
            || _permissionManager.HasPermission(Permission.ReportGeneration);
        CanClearLogs = _permissionManager.HasPermission(Permission.LogClear);
        CanRetestDut = _permissionManager.HasPermission(Permission.TestStart);
        CanConfigureSystem = _permissionManager.HasPermission(Permission.SystemConfig);
        CanManageTestPlans = _permissionManager.HasPermission(Permission.TestPlanManagement);
        CanCreateTestPlan = _permissionManager.HasPermission(Permission.TestPlanCreate);
        CanEditTestPlan = _permissionManager.HasPermission(Permission.TestPlanEdit);
        CanManageDevices = _permissionManager.HasPermission(Permission.DeviceManagement);
        CanManageUsers = _permissionManager.HasPermission(Permission.UserManagement);

        ApplyUiChromeConstraints();
    }

    /// <summary>
    /// Compute <see cref="ShowEngineeringMenus"/> / <see cref="ShowOperatorChrome"/> and clamp
    /// config-edit Can* flags. Operator/Observer always get simplified chrome (security &gt; profile).
    /// </summary>
    private void ApplyUiChromeConstraints()
    {
        var profile = _uiProfile ?? UiProfile.CreateDefault();
        var role = _permissionManager.CurrentUser?.Role;
        var isRestrictedRole = role is UserRole.Operator or UserRole.Observer;

        // Profile wants full chrome only when both edit + advanced menus are enabled.
        var profileAllowsEngineering = profile.AllowConfigEdit && profile.ShowAdvancedMenus;
        var hasSystemConfig = _permissionManager.HasPermission(Permission.SystemConfig);

        // Hide engineering shell when: profile denies edit, no SystemConfig, or restricted role.
        ShowEngineeringMenus = profileAllowsEngineering && hasSystemConfig && !isRestrictedRole;
        ShowOperatorChrome = !ShowEngineeringMenus;

        if (!ShowEngineeringMenus)
        {
            // Config / plan / plugin entry points — force off for simplified shell.
            CanImportConfig = false;
            CanConfigureSystem = false;
            CanManageTestPlans = false;
            CanCreateTestPlan = false;
            CanEditTestPlan = false;
            // User management stays available only when the user has UserManagement permission
            // (already set above). Restricted operator roles typically lack it.
        }
    }

    private void OnPermissionChanged(object? sender, PermissionChangedEventArgs e)
    {
        RefreshPermissions();
    }

    /// <summary>
    /// DUT 监控列表，直接复用 <see cref="DUTMonitorManager.DUTItems"/>。
    /// </summary>
    public ObservableCollection<DUTMonitorItem> DUTItems { get; }

    // ────────────────── Timers / Statistics ──────────────────

    /// <summary>
    /// 由 MainWindow 的 DispatcherTimer 每秒调用，刷新日期时间显示。
    /// </summary>
    public void UpdateDateTime()
    {
        DateTimeText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }

    /// <summary>
    /// 遍历 <see cref="DUTItems"/> 并将聚合统计写入各计数属性。
    /// 取代 MainWindow.UpdateDUTStatistics 的核心逻辑。
    /// </summary>
    public void UpdateStatistics()
    {
        try
        {
            var items = _dutMonitorManager.DUTItems;
            if (items is null || items.Count == 0)
            {
                TotalDuts = 0;
                RunningDuts = 0;
                WaitingDuts = 0;
                PassedDuts = 0;
                FailedDuts = 0;
                PassRateText = "0%";
                return;
            }

            int total = 0, running = 0, waiting = 0, passed = 0, failed = 0;
            foreach (var dut in items)
            {
                total++;
                switch (dut.OverallStatus)
                {
                    case DUTMonitorStatus.Running:
                        running++;
                        break;
                    case DUTMonitorStatus.Idle:
                        waiting++;
                        break;
                    case DUTMonitorStatus.Passed:
                        passed++;
                        break;
                    case DUTMonitorStatus.Failed:
                    case DUTMonitorStatus.Error:
                    case DUTMonitorStatus.Timeout:
                        failed++;
                        break;
                }
            }

            TotalDuts = total;
            RunningDuts = running;
            WaitingDuts = waiting;
            PassedDuts = passed;
            FailedDuts = failed;
            PassRateText = total > 0 ? $"{(passed * 100.0 / total):F1}%" : "0%";
        }
        catch (Exception ex)
        {
            _logger?.Error("更新DUT统计失败", ex);
        }
    }

    /// <summary>
    /// 读取 DUTMonitorManager 已加载插件信息并更新状态栏属性。
    /// </summary>
    public void UpdatePluginStatus()
    {
        try
        {
            LoadedPlugins = _dutMonitorManager.LoadedPlugins.Count;
            FailedPlugins = _dutMonitorManager.LastLoadReport?.FailedCount ?? 0;
        }
        catch (Exception ex)
        {
            _logger?.Error("更新插件状态失败", ex);
        }
    }

    private void OnStatisticsUpdateRequested()
    {
        // 直接刷新即可 - ObservableCollection 同步发布到 UI 线程，绑定由 Dispatcher 处理。
        UpdateStatistics();
    }

    private void OnAllTestsCompleted()
    {
        IsTestRunning = false;
        StartTestButtonText = "▶ 开始测试";
        StatusText = "所有测试已完成";
        StatusTextFooter = "所有测试已完成";
        UpdateStatistics();
    }

    // ────────────────── Commands ──────────────────

    /// <summary>启动测试。</summary>
    [RelayCommand(CanExecute = nameof(CanStartTest))]
    private async Task StartTestAsync()
    {
        if (IsTestRunning)
        {
            return;
        }

        try
        {
            IsTestRunning = true;
            StartTestButtonText = "▶️ 测试进行中...";
            StatusText = "测试进行中...";
            StatusTextFooter = "测试进行中...";
            await _dutMonitorManager.StartAllTestsAsync();
        }
        catch (Exception ex)
        {
            IsTestRunning = false;
            StartTestButtonText = "▶ 开始测试";
            StatusText = "测试启动失败";
            StatusTextFooter = $"测试启动失败: {ex.Message}";
            _logger?.Error("启动测试失败", ex);
            _dialogService.ShowError($"启动测试失败: {ex.Message}");
        }
    }

    /// <summary>停止测试。</summary>
    [RelayCommand(CanExecute = nameof(CanStopTest))]
    private async Task StopTestAsync()
    {
        if (!IsTestRunning)
        {
            return;
        }

        try
        {
            IsTestRunning = false;
            StartTestButtonText = "▶ 开始测试";
            StatusText = "测试已停止";
            StatusTextFooter = "测试已停止";
            await _dutMonitorManager.StopAllTestsAsync();
        }
        catch (Exception ex)
        {
            _logger?.Error("停止测试失败", ex);
            _dialogService.ShowError($"停止测试失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 导入配置文件（JSON）并应用。<paramref name="filePath"/> 为空时弹出打开文件对话框。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanImportConfig))]
    private async Task ImportConfigAsync(string? filePath = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            filePath = _dialogService.ShowOpenFileDialog("导入系统配置", "配置文件 (*.json)|*.json|所有文件 (*.*)|*.*");
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }
        }

        try
        {
            var jsonContent = await File.ReadAllTextAsync(filePath);
            var config = System.Text.Json.JsonSerializer.Deserialize<UnifiedConfiguration>(jsonContent);
            if (config is null)
            {
                _dialogService.ShowError("配置文件解析失败：未得到有效配置对象。");
                return;
            }

            var errors = _configAdapter.ValidateConfigurationWithErrors(config);
            if (errors.Count > 0)
            {
                StatusText = "配置校验失败";
                StatusTextFooter = string.Join("; ", errors);
                _dialogService.ShowError($"配置校验失败：\n{string.Join("\n", errors)}");
                return;
            }

            await _configManager.SaveUnifiedConfigurationAsync(config);
            await _configManager.RefreshConfiguration();
            await _dutMonitorManager.InitializeAsync();
            StatusText = "配置已刷新，系统已就绪";
            StatusTextFooter = "配置已刷新，系统已就绪";

            await RefreshConfigurationAfterImportAsync();
            _dialogService.ShowInformation("配置导入成功！");
        }
        catch (Exception ex)
        {
            _logger?.Error("导入配置失败", ex);
            StatusText = "导入配置失败";
            StatusTextFooter = $"导入配置失败: {ex.Message}";
            _dialogService.ShowError($"导入配置失败: {ex.Message}");
        }
    }

    /// <summary>刷新 DUT 列表。</summary>
    [RelayCommand]
    private async Task RefreshDUTsAsync()
    {
        try
        {
            await _dutMonitorManager.InitializeAsync();
            UpdateStatistics();
        }
        catch (Exception ex)
        {
            _logger?.Error("刷新DUT列表失败", ex);
        }
    }

    /// <summary>
    /// 导出测试报告（HTML/CSV/JSON）。通过 <see cref="IDialogService"/> 询问保存路径，
    /// 构建 <see cref="ReportDataSet"/> 后委托 <see cref="ReportGenerator"/> 生成。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExportReport))]
    private async Task ExportReportAsync()
    {
        if (_reportGenerator is null)
        {
            _dialogService.ShowWarning("报告生成器未注册，无法导出报告。");
            return;
        }

        var fileName = _dialogService.ShowSaveFileDialog(
            "导出测试报告",
            "HTML 报告 (*.html)|*.html|CSV 报告 (*.csv)|*.csv|JSON 报告 (*.json)|*.json|所有文件 (*.*)|*.*",
            "html",
            $"TestReport_{DateTime.Now:yyyyMMdd_HHmmss}");

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return;
        }

        try
        {
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
                    ["通过数"] = dutItems.Count(d => d.OverallStatus == DUTMonitorStatus.Passed),
                    ["失败数"] = dutItems.Count(d => d.OverallStatus == DUTMonitorStatus.Failed)
                }
            };

            var extension = Path.GetExtension(fileName).ToLowerInvariant();
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

            var result = await _reportGenerator.GenerateReportFromTemplateAsync(template, dataSet, format, fileName);

            if (result.Success)
            {
                _dialogService.ShowInformation($"报告已导出至:\n{fileName}", "导出成功");
            }
            else
            {
                _dialogService.ShowError($"导出报告失败: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            _logger?.Error("导出报告失败", ex);
            _dialogService.ShowError($"导出报告失败: {ex.Message}");
        }
    }

    /// <summary>清除所有 DUT 日志。</summary>
    [RelayCommand(CanExecute = nameof(CanClearLogs))]
    private void ClearAllLogs()
    {
        try
        {
            _dutMonitorManager.ResetAllDUTs();
            UpdateStatistics();
        }
        catch (Exception ex)
        {
            _logger?.Error("清除日志失败", ex);
            _dialogService.ShowError($"清除日志失败: {ex.Message}");
        }
    }

    /// <summary>打开配置管理中心。</summary>
    [RelayCommand(CanExecute = nameof(CanConfigureSystem))]
    private void OpenConfigurationCenter()
        => _windowFactory.ShowConfigurationCenterDialog();

    /// <summary>打开快速创建测试向导。</summary>
    [RelayCommand(CanExecute = nameof(CanManageTestPlans))]
    private void OpenQuickTestWizard()
        => _windowFactory.ShowQuickTestWizardDialog();

    /// <summary>打开测试计划编辑器。</summary>
    [RelayCommand(CanExecute = nameof(CanManageTestPlans))]
    private void OpenTestPlanEditor()
        => _windowFactory.ShowTestPlanEditorDialog();

    /// <summary>打开插件管理。</summary>
    [RelayCommand(CanExecute = nameof(CanConfigureSystem))]
    private void OpenPluginManager()
        => _windowFactory.ShowPluginManagerDialog();

    /// <summary>打开设备管理。</summary>
    [RelayCommand(CanExecute = nameof(CanManageDevices))]
    private void OpenDeviceManager()
        => _windowFactory.ShowDeviceManagerDialog();

    /// <summary>打开用户管理。</summary>
    [RelayCommand(CanExecute = nameof(CanManageUsers))]
    private void OpenUserManager()
        => _windowFactory.ShowUserManagerDialog();

    /// <summary>退出应用程序（带确认）。</summary>
    [RelayCommand]
    private void ExitApp()
    {
        if (_dialogService.ShowConfirmation("确定要退出系统吗？", "确认退出"))
        {
            Application.Current.Shutdown();
        }
    }

    // ────────────────── DUT context-menu commands ──────────────────
    // 3 个命令对应原 MainWindow.xaml.cs 的 ViewDUTLogBtn_Click / ViewDUTDetailBtn_Click /
    // RetestDUTBtn_Click。CommandParameter 为 DataGrid 行的 DUTMonitorItem（由 XAML 经
    // ContextMenu.DataContext = PlacementTarget.DataContext 传入）。

    /// <summary>
    /// 查看指定 DUT 的完整日志。从 <see cref="DUTMonitorItem.Logs"/> 聚合后通过对话框展示。
    /// </summary>
    /// <param name="dut">右键选中的 DUT 行；为空时直接返回。</param>
    [RelayCommand]
    private void ViewDutLog(DUTMonitorItem? dut)
    {
        if (dut is null)
        {
            return;
        }

        var logs = dut.Logs;
        var body = logs.Count > 0
            ? string.Join("\n", logs)
            : $"DUT {dut.DutId}（{dut.DutName}）暂无日志记录。";
        _dialogService.ShowInformation(body, $"DUT {dut.DutId} 日志");
    }

    /// <summary>
    /// 查看指定 DUT 的详细信息（ID/名称/类型/序列号/状态/步骤序列）。
    /// </summary>
    /// <param name="dut">右键选中的 DUT 行；为空时直接返回。</param>
    [RelayCommand]
    private void ViewDutDetail(DUTMonitorItem? dut)
    {
        if (dut is null)
        {
            return;
        }

        var steps = dut.TestSteps.Count > 0
            ? string.Join("\n", dut.TestSteps.Select(s => $"  • {s.StepName}（{s.StepId}）: {s.StatusText}"))
            : "  （无步骤）";
        var body =
            $"ID: {dut.DutId}\n" +
            $"名称: {dut.DutName}\n" +
            $"类型: {dut.DeviceType}\n" +
            $"序列号: {dut.SerialNumber}\n" +
            $"状态: {dut.OverallStatusText}\n" +
            $"当前步骤: {dut.CurrentStepText}\n" +
            $"测试步骤:\n{steps}";
        _dialogService.ShowInformation(body, $"DUT {dut.DutId} 详情");
    }

    /// <summary>
    /// 重新测试指定 DUT。先校验 <see cref="Permission.TestStart"/> 权限，
    /// 通过后调用 <see cref="DUTMonitorManager.StartAllTestsAsync"/>（当前为占位提示，
    /// 因 DUTMonitorManager 仅支持整体重测；保留提示避免误触整批运行）。
    /// </summary>
    /// <param name="dut">右键选中的 DUT 行；为空时直接返回。</param>
    [RelayCommand(CanExecute = nameof(CanRetestDut))]
    private void RetestDut(DUTMonitorItem? dut)
    {
        if (dut is null)
        {
            return;
        }

        if (!_permissionManager.HasPermission(Permission.TestStart))
        {
            _dialogService.ShowWarning("当前账户无权重新测试 DUT。", "权限不足");
            return;
        }

        _dialogService.ShowInformation(
            $"已请求重新测试 DUT {dut.DutId}（{dut.DutName}）。\n" +
            "请在工具栏点击\"开始测试\"以启动整体测试会话。",
            "重新测试 DUT");
    }

    /// <summary>退出当前登录并返回登录窗口。</summary>
    [RelayCommand]
    private async Task LogoutAsync()
    {
        if (IsTestRunning)
        {
            _dialogService.ShowWarning("请先停止当前测试。", "无法退出登录");
            return;
        }

        if (!_dialogService.ShowConfirmation("确定要退出当前登录吗？", "确认登出"))
        {
            return;
        }

        try
        {
            await _permissionManager.LogoutAsync();
            LogoutRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger?.Error("退出登录失败", ex);
            _dialogService.ShowError($"退出登录失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 当前用户请求登出时触发（MainWindow 代码后置订阅以切换到 LoginWindow）。
    /// </summary>
    public event EventHandler? LogoutRequested;

    /// <summary>
    /// 配置已被辅助窗口修改时触发（由 <see cref="IWindowFactory.ConfigurationApplied"/> 转发）。
    /// MainWindow 代码后置订阅以刷新 DataGrid / 产品型号显示等需要 UI 资源的部分。
    /// </summary>
    public event EventHandler? ConfigurationRefreshRequested;

    private void OnConfigurationApplied(object? sender, WindowClosedEventArgs e)
    {
        // 桥接到代码后置：DataGrid 重绑、产品型号刷新等需要 UI 资源的操作仍保留在 code-behind。
        ConfigurationRefreshRequested?.Invoke(this, e);
    }

    /// <summary>
    /// 配置导入 / 辅助窗口保存后，刷新配置、DUT 列表与产品型号显示。
    /// VM 内完成的部分（配置刷新、DUT 重新初始化、统计更新）在此处理；
    /// DataGrid 重绑等需要 UI 控件引用的步骤由代码后置订阅
    /// <see cref="ConfigurationRefreshRequested"/> 完成。
    /// </summary>
    private async Task RefreshConfigurationAfterImportAsync()
    {
        try
        {
            _logger?.Info("开始刷新配置...");

            await _configManager.RefreshConfiguration();
            await LoadUiProfileFromConfigAsync();
            await _dutMonitorManager.InitializeAsync();

            if (!IsTestRunning)
            {
                StatusText = "配置已刷新，系统已就绪";
                StatusTextFooter = "配置已刷新，系统已就绪";
            }

            UpdateStatistics();
            ConfigurationRefreshRequested?.Invoke(this, new WindowClosedEventArgs { Source = "Import" });

            _logger?.Info("配置刷新完成");
        }
        catch (Exception ex)
        {
            _logger?.Error("刷新配置时发生错误", ex);
        }
    }
}
