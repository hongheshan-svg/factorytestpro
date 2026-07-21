using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using UTF.Configuration;
using UTF.Core;
using UTF.Core.Mapping;
using UTF.Plugin.Abstractions;
using UTF.Plugin.Host;
using UTF.UI.Models;

namespace UTF.UI.Services;

/// <summary>
/// Projects configuration-driven test sessions onto the WPF DUT monitor surface.
/// Execution is owned by <see cref="ConfigDrivenTestOrchestrator"/>; this type only maps events to UI.
/// </summary>
public sealed class DUTMonitorManager : IDUTMonitorService, IDisposable
{
    private readonly ConfigurationManager _configManager;
    private readonly IConfigurationAdapter _configAdapter;
    private readonly StepExecutorPluginHost _pluginHost;
    private readonly ConfigDrivenTestOrchestrator _orchestrator;
    private readonly UTF.Logging.ILogger _logger;
    private readonly SemaphoreSlim _pluginInitSemaphore = new(1, 1);
    private readonly Dictionary<string, DUTMonitorItem> _itemsById = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<DataGridColumn> _dynamicColumns = new();
    private readonly object _runGate = new();
    private DataGrid? _dataGrid;
    private CancellationTokenSource? _runCts;
    private Task? _activeRunTask;
    private string? _activeSessionId;
    private bool _pluginsInitialized;
    private bool _disposed;
    private bool _eventsHooked;

    public DUTMonitorManager(
        ConfigurationManager configManager,
        IConfigurationAdapter configAdapter,
        StepExecutorPluginHost pluginHost,
        ConfigDrivenTestOrchestrator orchestrator,
        UTF.Logging.ILogger logger)
    {
        _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
        _configAdapter = configAdapter ?? throw new ArgumentNullException(nameof(configAdapter));
        _pluginHost = pluginHost ?? throw new ArgumentNullException(nameof(pluginHost));
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        HookOrchestratorEvents();
    }

    public event Action? StatisticsUpdateRequested;
    public event Action? AllTestsCompleted;

    public ObservableCollection<DUTMonitorItem> DUTItems { get; } = new();
    public IReadOnlyList<PluginMetadata> LoadedPlugins => _pluginHost.LoadedPlugins;
    public PluginLoadReport? LastLoadReport { get; private set; }

    public ObservableCollection<DUTMonitorItem> GetDUTItems() => DUTItems;

    /// <summary>
    /// 初始化 DUT 监控：加载插件、加载 DUT 配置、生成集合。
    /// </summary>
    public async Task InitializeAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await EnsurePluginsInitializedAsync().ConfigureAwait(true);
        await LoadDutConfigurationAsync().ConfigureAwait(true);
        await GenerateDynamicColumnsAsync().ConfigureAwait(true);
        StatisticsUpdateRequested?.Invoke();
    }

    /// <summary>
    /// When false, dynamic per-step result columns are not generated (UiProfile.ShowStepColumns).
    /// Default true preserves existing engineer grid behavior.
    /// </summary>
    public bool ShowStepColumns { get; set; } = true;

    /// <summary>
    /// 将 DUT 监控列表绑定到指定 <see cref="DataGrid"/>，并刷新动态列。
    /// </summary>
    public void AttachToDataGrid(DataGrid dataGrid)
    {
        ArgumentNullException.ThrowIfNull(dataGrid);
        _dataGrid = dataGrid;
        dataGrid.ItemsSource = DUTItems;
        _ = GenerateDynamicColumnsAsync();
    }

    /// <summary>
    /// 兼容旧 API：将初始化与 DataGrid 绑定合并执行。
    /// </summary>
    public async Task InitializeAsync(DataGrid dataGrid)
    {
        await InitializeAsync().ConfigureAwait(true);
        AttachToDataGrid(dataGrid);
    }

    public async Task StartAllTestsAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var configuration = await _configManager.GetUnifiedConfigurationAsync().ConfigureAwait(true);
        var errors = _configAdapter.ValidateConfigurationWithErrors(configuration);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException($"测试配置无效: {string.Join("; ", errors)}");
        }

        var project = MapProject(configuration);
        if (!project.Enabled)
        {
            throw new InvalidOperationException("测试项目已禁用。");
        }

        var candidates = DUTItems.Where(item => item.OverallStatus != DUTMonitorStatus.Running).ToList();
        if (candidates.Count == 0)
        {
            throw new InvalidOperationException("没有可执行测试的 DUT。");
        }

        await EnsurePluginsInitializedAsync().ConfigureAwait(true);

        var maxConcurrency = Math.Clamp(_configAdapter.GetMaxConcurrent(configuration), 1, 256);
        var dutConfig = TestProjectMapper.BuildDutConfig(
            productName: configuration.DUTConfiguration.ProductInfo?.Name,
            productModel: configuration.DUTConfiguration.ProductInfo?.Model,
            expectedSoftwareVersion: configuration.DUTConfiguration.ProductInfo?.ExpectedSoftwareVersion,
            defaultMaxConcurrent: maxConcurrency,
            testTimeout: configuration.DUTConfiguration.GlobalSettings?.TestTimeout ?? 300,
            retryCount: configuration.DUTConfiguration.GlobalSettings?.RetryCount ?? 3,
            serialPorts: _configAdapter.GetSerialPorts(configuration),
            networkHosts: _configAdapter.GetNetworkHosts(configuration));

        var perDutContexts = new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in candidates)
        {
            perDutContexts[item.DutId] = BuildDutContext(item, configuration);
        }

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task runTask;
        lock (_runGate)
        {
            if (_activeRunTask is { IsCompleted: false })
            {
                cts.Dispose();
                throw new InvalidOperationException("已有测试会话正在运行。");
            }

            _runCts?.Dispose();
            _runCts = cts;
            runTask = RunSessionViaOrchestratorAsync(
                candidates,
                project,
                dutConfig,
                perDutContexts,
                cts.Token);
            _activeRunTask = runTask;
        }

        _ = ObserveRunAsync(runTask, cts);
    }

    public async Task StopAllTestsAsync()
    {
        string? sessionId;
        Task? active;
        CancellationTokenSource? cts;
        lock (_runGate)
        {
            sessionId = _activeSessionId;
            active = _activeRunTask;
            cts = _runCts;
        }

        cts?.Cancel();

        if (!string.IsNullOrEmpty(sessionId))
        {
            try
            {
                await _orchestrator.StopSessionAsync(sessionId).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Warning($"停止编排会话失败: {ex.Message}");
            }
        }

        if (active != null)
        {
            try
            {
                await active.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        await OnUiAsync(() =>
        {
            foreach (var item in DUTItems.Where(item => item.OverallStatus == DUTMonitorStatus.Running))
            {
                item.OverallStatus = DUTMonitorStatus.Idle;
                item.CurrentStepText = "测试已停止";
                item.EndTime = DateTime.Now;
                AddDutLogCore(item, "测试已停止", UTF.Logging.LogLevel.Warning);
            }

            StatisticsUpdateRequested?.Invoke();
        }).ConfigureAwait(false);
    }

    public void StopAllTests()
    {
        string? sessionId;
        lock (_runGate)
        {
            _runCts?.Cancel();
            sessionId = _activeSessionId;
        }

        if (!string.IsNullOrEmpty(sessionId))
        {
            _ = _orchestrator.StopSessionAsync(sessionId);
        }
    }

    public void ResetAllDUTs()
    {
        EnsureUiThread();
        foreach (var item in DUTItems)
        {
            item.OverallStatus = DUTMonitorStatus.Idle;
            item.CurrentStepText = "待机中";
            item.StartTime = null;
            item.EndTime = null;
            item.Logs.Clear();
            item.RecentLogs.Clear();
            foreach (var step in item.TestSteps)
            {
                step.Status = DUTMonitorStepStatus.Pending;
                step.StartTime = null;
                step.EndTime = null;
                step.ErrorMessage = string.Empty;
            }
        }

        StatisticsUpdateRequested?.Invoke();
    }

    public void AddDUTLog(string dutId, string message)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            _ = dispatcher.BeginInvoke(() => AddDUTLog(dutId, message));
            return;
        }

        if (_itemsById.TryGetValue(dutId, out var item))
        {
            AddDutLogCore(item, message, UTF.Logging.LogLevel.Info);
        }
    }

    private async Task RunSessionViaOrchestratorAsync(
        IReadOnlyList<DUTMonitorItem> candidates,
        ConfigTestProject project,
        DUTConfigInfo dutConfig,
        IReadOnlyDictionary<string, Dictionary<string, object>> perDutContexts,
        CancellationToken cancellationToken)
    {
        var dutIds = candidates.Select(c => c.DutId).ToList();
        var session = await _orchestrator.CreateSessionAsync(
            project,
            dutIds,
            operatorName: Environment.UserName,
            sessionContext: null,
            dutConfig: dutConfig,
            perDutContexts: perDutContexts,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (session == null)
        {
            throw new InvalidOperationException("无法创建测试会话（项目校验失败或 DUT 列表无效）。");
        }

        lock (_runGate)
        {
            _activeSessionId = session.SessionId;
        }

        var started = await _orchestrator.StartSessionAsync(session.SessionId, cancellationToken).ConfigureAwait(false);
        if (!started)
        {
            // Cleanup must not be cancelled by the run token (session already failed to start).
            await _orchestrator.CleanupSessionAsync(session.SessionId, CancellationToken.None).ConfigureAwait(false);
            lock (_runGate)
            {
                _activeSessionId = null;
            }

            throw new InvalidOperationException("无法启动测试会话。");
        }

        try
        {
            await _orchestrator.WaitForSessionAsync(session.SessionId).ConfigureAwait(false);
        }
        finally
        {
            // Always clean up even if the run token is cancelled.
            await _orchestrator.CleanupSessionAsync(session.SessionId, CancellationToken.None).ConfigureAwait(false);
            lock (_runGate)
            {
                if (string.Equals(_activeSessionId, session.SessionId, StringComparison.Ordinal))
                {
                    _activeSessionId = null;
                }
            }
        }
    }

    private async Task ObserveRunAsync(Task runTask, CancellationTokenSource owner)
    {
        var completed = false;
        try
        {
            await runTask.ConfigureAwait(false);
            completed = true;
        }
        catch (OperationCanceledException)
        {
            _logger.Info("测试会话已取消。");
        }
        catch (Exception ex)
        {
            _logger.Error("测试会话失败", ex);
        }
        finally
        {
            lock (_runGate)
            {
                if (ReferenceEquals(_runCts, owner))
                {
                    _runCts = null;
                    _activeRunTask = null;
                }
            }

            owner.Dispose();
            await OnUiAsync(() =>
            {
                StatisticsUpdateRequested?.Invoke();
                if (completed)
                {
                    AllTestsCompleted?.Invoke();
                }
            }).ConfigureAwait(false);
        }
    }

    private void HookOrchestratorEvents()
    {
        if (_eventsHooked)
        {
            return;
        }

        _orchestrator.DutStarted += OnDutStarted;
        _orchestrator.DutCompleted += OnDutCompleted;
        _orchestrator.StepCompleted += OnStepCompleted;
        _orchestrator.ErrorOccurred += OnErrorOccurred;
        _eventsHooked = true;
    }

    private void UnhookOrchestratorEvents()
    {
        if (!_eventsHooked)
        {
            return;
        }

        _orchestrator.DutStarted -= OnDutStarted;
        _orchestrator.DutCompleted -= OnDutCompleted;
        _orchestrator.StepCompleted -= OnStepCompleted;
        _orchestrator.ErrorOccurred -= OnErrorOccurred;
        _eventsHooked = false;
    }

    private void OnDutStarted(object? sender, ConfigTestEventArgs e)
    {
        if (string.IsNullOrEmpty(e.DutId) || !_itemsById.TryGetValue(e.DutId, out var item))
        {
            return;
        }

        _ = OnUiAsync(() =>
        {
            item.OverallStatus = DUTMonitorStatus.Running;
            item.CurrentStepText = "正在执行测试";
            item.StartTime = DateTime.Now;
            item.EndTime = null;
            AddDutLogCore(item, "开始执行测试", UTF.Logging.LogLevel.Info);
            StatisticsUpdateRequested?.Invoke();
        });
    }

    private void OnStepCompleted(object? sender, ConfigTestEventArgs e)
    {
        if (string.IsNullOrEmpty(e.DutId) ||
            e.Data is not ConfigDrivenStepResult result ||
            !_itemsById.TryGetValue(e.DutId, out var item))
        {
            return;
        }

        _ = OnUiAsync(() =>
        {
            var step = item.TestSteps.FirstOrDefault(s =>
                string.Equals(s.StepId, result.StepId, StringComparison.OrdinalIgnoreCase));
            if (step != null)
            {
                step.Status = result.Skipped
                    ? DUTMonitorStepStatus.Skipped
                    : result.Passed ? DUTMonitorStepStatus.Passed : DUTMonitorStepStatus.Failed;
                step.StartTime = result.StartTime.ToLocalTime();
                step.EndTime = result.EndTime.ToLocalTime();
                step.ErrorMessage = result.ErrorMessage;
            }

            AddDutLogCore(
                item,
                $"步骤 {result.StepName}: {(result.Skipped ? "SKIP" : result.Passed ? "PASS" : "FAIL")}" +
                (string.IsNullOrWhiteSpace(result.ErrorMessage) ? string.Empty : $" - {result.ErrorMessage}"),
                result.Passed || result.Skipped ? UTF.Logging.LogLevel.Info : UTF.Logging.LogLevel.Error);
            item.CurrentStepText = $"步骤 {result.StepName}: {(result.Skipped ? "SKIP" : result.Passed ? "PASS" : "FAIL")}";
            StatisticsUpdateRequested?.Invoke();
        });
    }

    private void OnDutCompleted(object? sender, ConfigTestEventArgs e)
    {
        if (string.IsNullOrEmpty(e.DutId) || !_itemsById.TryGetValue(e.DutId, out var item))
        {
            return;
        }

        if (e.Data is ConfigDrivenTestReport report)
        {
            _ = OnUiAsync(() => ApplyReport(item, report));
            return;
        }

        _ = OnUiAsync(() =>
        {
            if (item.OverallStatus == DUTMonitorStatus.Running)
            {
                item.OverallStatus = DUTMonitorStatus.Failed;
                item.CurrentStepText = "测试完成（无报告）";
                item.EndTime = DateTime.Now;
            }

            StatisticsUpdateRequested?.Invoke();
        });
    }

    private void OnErrorOccurred(object? sender, ConfigTestEventArgs e)
    {
        if (string.IsNullOrEmpty(e.DutId) || !_itemsById.TryGetValue(e.DutId, out var item))
        {
            return;
        }

        var message = e.Data?.ToString() ?? "未知错误";
        _ = OnUiAsync(() =>
        {
            item.OverallStatus = DUTMonitorStatus.Error;
            item.CurrentStepText = $"测试异常: {message}";
            item.EndTime = DateTime.Now;
            AddDutLogCore(item, $"测试异常: {message}", UTF.Logging.LogLevel.Error);
            StatisticsUpdateRequested?.Invoke();
        });
    }

    private void ApplyReport(DUTMonitorItem item, ConfigDrivenTestReport report)
    {
        var byId = item.TestSteps.ToDictionary(step => step.StepId, StringComparer.OrdinalIgnoreCase);
        foreach (var result in report.StepResults)
        {
            if (!byId.TryGetValue(result.StepId, out var step))
            {
                continue;
            }

            step.Status = result.Skipped
                ? DUTMonitorStepStatus.Skipped
                : result.Passed ? DUTMonitorStepStatus.Passed : DUTMonitorStepStatus.Failed;
            step.StartTime = result.StartTime.ToLocalTime();
            step.EndTime = result.EndTime.ToLocalTime();
            step.ErrorMessage = result.ErrorMessage;
        }

        foreach (var pending in item.TestSteps.Where(step => step.Status == DUTMonitorStepStatus.Pending))
        {
            pending.Status = DUTMonitorStepStatus.Skipped;
        }

        item.OverallStatus = report.Passed ? DUTMonitorStatus.Passed : DUTMonitorStatus.Failed;
        item.CurrentStepText = report.Passed ? "测试通过" : $"测试失败: {report.ErrorMessage}";
        item.EndTime = DateTime.Now;
        AddDutLogCore(item, report.Passed ? "测试完成: PASS" : "测试完成: FAIL", report.Passed
            ? UTF.Logging.LogLevel.Info
            : UTF.Logging.LogLevel.Error);
        StatisticsUpdateRequested?.Invoke();
    }

    private static Dictionary<string, object> BuildDutContext(
        DUTMonitorItem item,
        UnifiedConfiguration configuration)
    {
        var endpoints = EndpointMapper.NormalizeEndpoints(configuration);
        var serialPorts = EndpointMapper.GetSerialAddresses(configuration);
        var hosts = EndpointMapper.GetNetworkAddresses(configuration);

        var index = 0;
        if (serialPorts.Count > 0 && !string.IsNullOrWhiteSpace(item.SerialNumber))
        {
            var found = serialPorts.FindIndex(port =>
                string.Equals(port, item.SerialNumber, StringComparison.OrdinalIgnoreCase));
            if (found >= 0)
            {
                index = found;
            }
        }

        var serialPort = !string.IsNullOrWhiteSpace(item.SerialNumber)
            ? item.SerialNumber
            : serialPorts.Count > 0
                ? serialPorts[index % serialPorts.Count]
                : string.Empty;
        var host = hosts.Count > 0 ? hosts[index % hosts.Count] : string.Empty;

        return TestProjectMapper.BuildDutContext(
            item.DutId,
            item.DutName,
            item.DeviceType,
            serialPort,
            host,
            endpoints);
    }

    private static ConfigTestProject MapProject(UnifiedConfiguration configuration)
    {
        var source = configuration.TestProjectConfiguration?.TestProject
            ?? throw new InvalidOperationException("缺少测试项目配置。");
        var defaultRetryCount = configuration.DUTConfiguration.GlobalSettings?.RetryCount ?? 0;
        var steps = source.Steps.Select(step => new ConfigTestStep
        {
            Id = step.Id,
            Name = step.Name,
            Description = step.Description,
            Order = step.Order,
            Enabled = step.Enabled,
            Type = step.Type,
            TargetDeviceId = step.TargetDeviceId ?? step.Target,
            Command = step.Command,
            Expected = step.Expected,
            Timeout = step.Timeout,
            Delay = step.Delay,
            RetryCount = step.RetryCount,
            Channel = step.Channel,
            StoreResultAs = step.StoreResultAs,
            ConditionExpression = step.ConditionExpression,
            ContinueOnFailure = step.ContinueOnFailure,
            ValidationRules = step.ValidationRules == null
                ? null
                : new Dictionary<string, object>(step.ValidationRules),
            Parameters = step.Parameters == null
                ? null
                : new Dictionary<string, object>(step.Parameters)
        });

        return TestProjectMapper.BuildProject(
            source.Id,
            source.Name,
            source.Description,
            source.Enabled,
            steps,
            defaultRetryCount);
    }

    private async Task LoadDutConfigurationAsync(int? requestedCount = null)
    {
        var configuration = await _configManager.GetUnifiedConfigurationAsync().ConfigureAwait(true);
        var count = Math.Clamp(requestedCount ?? _configAdapter.GetMaxConcurrent(configuration), 1, 256);
        var naming = _configAdapter.GetNamingTemplate(configuration);
        var idTemplate = _configAdapter.GetIdTemplate(configuration);
        var product = configuration.DUTConfiguration.ProductInfo?.Name ?? _configAdapter.GetProductModel(configuration);
        var type = configuration.DUTConfiguration.ProductInfo?.Category ?? "通用DUT";
        var serialPorts = _configAdapter.GetSerialPorts(configuration);

        DUTItems.Clear();
        _itemsById.Clear();
        for (var index = 1; index <= count; index++)
        {
            var id = idTemplate.Replace("{Index}", index.ToString(), StringComparison.Ordinal);
            if (_itemsById.ContainsKey(id))
            {
                throw new InvalidOperationException($"DUT ID 模板产生重复值: {id}");
            }

            var item = new DUTMonitorItem
            {
                DutId = id,
                DutName = naming.Replace("{TypeName}", product, StringComparison.Ordinal)
                    .Replace("{Index}", index.ToString(), StringComparison.Ordinal),
                DeviceType = type,
                SerialNumber = serialPorts.Count > 0 ? serialPorts[(index - 1) % serialPorts.Count] : string.Empty,
                OverallStatus = DUTMonitorStatus.Idle,
                CurrentStepText = "待机中"
            };

            foreach (var step in _configAdapter.GetTestSteps(configuration).Where(step => step.Enabled).OrderBy(step => step.Order))
            {
                item.TestSteps.Add(new DUTTestStep
                {
                    StepId = step.Id,
                    StepName = step.Name,
                    Order = step.Order,
                    Status = DUTMonitorStepStatus.Pending
                });
            }

            DUTItems.Add(item);
            _itemsById.Add(id, item);
        }
    }

    private Task GenerateDynamicColumnsAsync()
    {
        if (_dataGrid == null)
        {
            return Task.CompletedTask;
        }

        foreach (var column in _dynamicColumns)
        {
            _dataGrid.Columns.Remove(column);
        }
        _dynamicColumns.Clear();

        if (!ShowStepColumns)
        {
            return Task.CompletedTask;
        }

        var steps = DUTItems.FirstOrDefault()?.TestSteps ?? new ObservableCollection<DUTTestStep>();
        var insertionIndex = Math.Max(0, _dataGrid.Columns.Count - 1);
        for (var index = 0; index < steps.Count; index++)
        {
            var column = CreateStepColumn(steps[index].StepName, index);
            _dataGrid.Columns.Insert(insertionIndex + index, column);
            _dynamicColumns.Add(column);
        }

        return Task.CompletedTask;
    }

    private static DataGridTemplateColumn CreateStepColumn(string name, int index)
    {
        var template = new DataTemplate();
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(2));
        border.SetValue(Border.PaddingProperty, new Thickness(3, 1, 3, 1));
        border.SetBinding(Border.BackgroundProperty, new Binding($"TestSteps[{index}].StatusBrush"));
        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetBinding(TextBlock.TextProperty, new Binding($"TestSteps[{index}].StatusText"));
        text.SetValue(TextBlock.ForegroundProperty, System.Windows.Media.Brushes.White);
        text.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center);
        border.AppendChild(text);
        template.VisualTree = border;
        return new DataGridTemplateColumn
        {
            Header = name,
            Width = new DataGridLength(80),
            MinWidth = 60,
            CellTemplate = template
        };
    }

    private async Task EnsurePluginsInitializedAsync()
    {
        if (_pluginsInitialized)
        {
            return;
        }

        await _pluginInitSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_pluginsInitialized)
            {
                return;
            }

            LastLoadReport = await _pluginHost.InitializeAsync().ConfigureAwait(false);
            _pluginsInitialized = true;
            if (LastLoadReport.FailedCount > 0)
            {
                _logger.Warning($"{LastLoadReport.FailedCount} 个插件加载失败；匹配步骤将按失败处理。");
            }
        }
        finally
        {
            _pluginInitSemaphore.Release();
        }
    }

    private static void AddDutLogCore(DUTMonitorItem item, string message, UTF.Logging.LogLevel level)
    {
        var line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
        item.Logs.Add(line);
        while (item.Logs.Count > 200)
        {
            item.Logs.RemoveAt(0);
        }
        item.LatestLog = line;
        item.AddLog(message, level);
    }

    private static Task OnUiAsync(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return dispatcher.InvokeAsync(action).Task;
    }

    private static void EnsureUiThread()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            throw new InvalidOperationException("此操作必须在 UI 线程执行。");
        }
    }

    async Task IDUTMonitorService.InitializeAsync(int dutCount)
    {
        await EnsurePluginsInitializedAsync().ConfigureAwait(false);
        await OnUiAsync(() => { }).ConfigureAwait(false);
        await LoadDutConfigurationOnUiAsync(dutCount).ConfigureAwait(false);
        await OnUiAsync(() => StatisticsUpdateRequested?.Invoke()).ConfigureAwait(false);
    }

    private async Task LoadDutConfigurationOnUiAsync(int count)
    {
        var configuration = await _configManager.GetUnifiedConfigurationAsync().ConfigureAwait(false);
        await OnUiAsync(() => LoadDutConfigurationFromSnapshot(configuration, count)).ConfigureAwait(false);
    }

    private void LoadDutConfigurationFromSnapshot(UnifiedConfiguration configuration, int requestedCount)
    {
        var count = Math.Clamp(requestedCount > 0 ? requestedCount : _configAdapter.GetMaxConcurrent(configuration), 1, 256);
        var naming = _configAdapter.GetNamingTemplate(configuration);
        var idTemplate = _configAdapter.GetIdTemplate(configuration);
        var product = configuration.DUTConfiguration.ProductInfo?.Name ?? _configAdapter.GetProductModel(configuration);
        var serialPorts = _configAdapter.GetSerialPorts(configuration);
        DUTItems.Clear();
        _itemsById.Clear();
        for (var index = 1; index <= count; index++)
        {
            var id = idTemplate.Replace("{Index}", index.ToString(), StringComparison.Ordinal);
            var item = new DUTMonitorItem
            {
                DutId = id,
                DutName = naming.Replace("{TypeName}", product, StringComparison.Ordinal)
                    .Replace("{Index}", index.ToString(), StringComparison.Ordinal),
                DeviceType = configuration.DUTConfiguration.ProductInfo?.Category ?? "通用DUT",
                SerialNumber = serialPorts.Count > 0 ? serialPorts[(index - 1) % serialPorts.Count] : string.Empty,
                OverallStatus = DUTMonitorStatus.Idle,
                CurrentStepText = "待机中"
            };
            foreach (var step in _configAdapter.GetTestSteps(configuration).Where(step => step.Enabled).OrderBy(step => step.Order))
            {
                item.TestSteps.Add(new DUTTestStep { StepId = step.Id, StepName = step.Name, Order = step.Order });
            }
            DUTItems.Add(item);
            _itemsById[id] = item;
        }
    }

    Task IDUTMonitorService.StartAllTestsAsync(CancellationToken ct) => StartAllTestsAsync(ct);
    Task IDUTMonitorService.StopAllTestsAsync() => StopAllTestsAsync();
    IReadOnlyList<PluginMetadata> IDUTMonitorService.GetLoadedPlugins() => LoadedPlugins;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        StopAllTests();
        UnhookOrchestratorEvents();
        _pluginInitSemaphore.Dispose();
        _disposed = true;
    }
}
