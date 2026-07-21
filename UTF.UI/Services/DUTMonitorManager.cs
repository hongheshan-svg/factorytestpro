using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using UTF.Core;
using UTF.Plugin.Abstractions;
using UTF.Plugin.Host;
using UTF.UI.Models;

namespace UTF.UI.Services;

/// <summary>
/// Current production test-run entry for the desktop app (UTF.UI).
/// Projects configuration-driven test sessions onto the WPF DUT monitor surface.
/// Execution and result validation remain in <see cref="ConfigDrivenTestEngine"/>;
/// session orchestration via <see cref="ConfigDrivenTestOrchestrator"/> is Phase B.
/// </summary>
public sealed class DUTMonitorManager : IDUTMonitorService, IDisposable
{
    private readonly ConfigurationManager _configManager;
    private readonly IConfigurationAdapter _configAdapter;
    private readonly StepExecutorPluginHost _pluginHost;
    private readonly ConfigDrivenTestEngine _testEngine;
    private readonly UTF.Logging.ILogger _logger;
    private readonly SemaphoreSlim _pluginInitSemaphore = new(1, 1);
    private readonly Dictionary<string, DUTMonitorItem> _itemsById = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<DataGridColumn> _dynamicColumns = new();
    private readonly object _runGate = new();
    private DataGrid? _dataGrid;
    private CancellationTokenSource? _runCts;
    private Task? _activeRunTask;
    private bool _pluginsInitialized;
    private bool _disposed;

    public DUTMonitorManager(
        ConfigurationManager configManager,
        IConfigurationAdapter configAdapter,
        StepExecutorPluginHost pluginHost,
        ConfigDrivenTestEngine testEngine,
        UTF.Logging.ILogger logger)
    {
        _configManager = configManager;
        _configAdapter = configAdapter;
        _pluginHost = pluginHost;
        _testEngine = testEngine;
        _logger = logger;
    }

    public event Action? StatisticsUpdateRequested;
    public event Action? AllTestsCompleted;

    public ObservableCollection<DUTMonitorItem> DUTItems { get; } = new();
    public IReadOnlyList<PluginMetadata> LoadedPlugins => _pluginHost.LoadedPlugins;
    public PluginLoadReport? LastLoadReport { get; private set; }

    public ObservableCollection<DUTMonitorItem> GetDUTItems() => DUTItems;

    /// <summary>
    /// 初始化 DUT 监控：加载插件、加载 DUT 配置、生成集合。
    /// 不再接受 DataGrid 参数 - 动态列生成请通过 <see cref="AttachToDataGrid(DataGrid)"/> 显式调用，
    /// 或直接通过 XAML 绑定 <see cref="DUTItems"/>。
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
    /// 将 DUT 监控列表绑定到指定 <see cref="DataGrid"/>，并刷新动态列。
    /// 兼容旧代码路径：旧 <c>InitializeAsync(DataGrid)</c> 已被拆分为本方法 + <see cref="InitializeAsync"/>。
    /// </summary>
    /// <param name="dataGrid">要绑定 DUTItems 的 DataGrid。</param>
    public void AttachToDataGrid(DataGrid dataGrid)
    {
        ArgumentNullException.ThrowIfNull(dataGrid);
        _dataGrid = dataGrid;
        dataGrid.ItemsSource = DUTItems;
        // 列已绑定 ItemsSource；GenerateDynamicColumnsAsync 会按 _dataGrid.Columns 修改动态列。
        _ = GenerateDynamicColumnsAsync();
    }

    /// <summary>
    /// 兼容旧 API：将初始化与 DataGrid 绑定合并执行。
    /// 推荐改为先 <see cref="InitializeAsync"/> 再 <see cref="AttachToDataGrid"/>。
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

        var project = BuildProject(configuration);
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
            runTask = RunAllAsync(candidates, project, configuration, maxConcurrency, cts.Token);
            _activeRunTask = runTask;
        }

        _ = ObserveRunAsync(runTask, cts);
    }

    public async Task StopAllTestsAsync()
    {
        Task? active;
        CancellationTokenSource? cts;
        lock (_runGate)
        {
            active = _activeRunTask;
            cts = _runCts;
        }

        cts?.Cancel();
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
        lock (_runGate)
        {
            _runCts?.Cancel();
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

    private async Task RunAllAsync(
        IReadOnlyList<DUTMonitorItem> candidates,
        ConfigTestProject project,
        UnifiedConfiguration configuration,
        int maxConcurrency,
        CancellationToken cancellationToken)
    {
        using var concurrency = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        var tasks = candidates.Select(async item =>
        {
            await concurrency.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await RunDutAsync(item, project, configuration, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                concurrency.Release();
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private async Task RunDutAsync(
        DUTMonitorItem item,
        ConfigTestProject project,
        UnifiedConfiguration configuration,
        CancellationToken cancellationToken)
    {
        await OnUiAsync(() =>
        {
            item.OverallStatus = DUTMonitorStatus.Running;
            item.CurrentStepText = "正在执行测试";
            item.StartTime = DateTime.Now;
            item.EndTime = null;
            AddDutLogCore(item, "开始执行测试", UTF.Logging.LogLevel.Info);
        }).ConfigureAwait(false);

        var context = BuildDutContext(item, configuration);
        try
        {
            var report = await _testEngine.ExecuteTestProjectAsync(
                project,
                item.DutId,
                context,
                cancellationToken).ConfigureAwait(false);

            await OnUiAsync(() => ApplyReport(item, report)).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await OnUiAsync(() =>
            {
                item.OverallStatus = DUTMonitorStatus.Idle;
                item.CurrentStepText = "测试已停止";
                item.EndTime = DateTime.Now;
                AddDutLogCore(item, "测试已取消", UTF.Logging.LogLevel.Warning);
            }).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex)
        {
            await OnUiAsync(() =>
            {
                item.OverallStatus = DUTMonitorStatus.Error;
                item.CurrentStepText = $"测试异常: {ex.Message}";
                item.EndTime = DateTime.Now;
                AddDutLogCore(item, $"测试异常: {ex.Message}", UTF.Logging.LogLevel.Error);
            }).ConfigureAwait(false);
            _logger.Error($"DUT {item.DutId} 测试失败", ex);
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
            AddDutLogCore(
                item,
                $"步骤 {result.StepName}: {(result.Skipped ? "SKIP" : result.Passed ? "PASS" : "FAIL")}" +
                (string.IsNullOrWhiteSpace(result.ErrorMessage) ? string.Empty : $" - {result.ErrorMessage}"),
                result.Passed ? UTF.Logging.LogLevel.Info : UTF.Logging.LogLevel.Error);
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
        var index = Math.Max(0, configuration.DUTConfiguration.CommunicationEndpoints?.SerialPorts
            .FindIndex(port => string.Equals(port, item.SerialNumber, StringComparison.OrdinalIgnoreCase)) ?? 0);
        var hosts = configuration.DUTConfiguration.CommunicationEndpoints?.NetworkHosts;
        var host = hosts is { Count: > 0 } ? hosts[index % hosts.Count] : string.Empty;
        return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["SerialPort"] = item.SerialNumber,
            ["Host"] = host,
            ["DutName"] = item.DutName,
            ["DutType"] = item.DeviceType
        };
    }

    private static ConfigTestProject BuildProject(UnifiedConfiguration configuration)
    {
        var source = configuration.TestProjectConfiguration?.TestProject
            ?? throw new InvalidOperationException("缺少测试项目配置。");
        var defaultRetryCount = configuration.DUTConfiguration.GlobalSettings?.RetryCount ?? 0;
        return new ConfigTestProject
        {
            Id = source.Id,
            Name = source.Name,
            Description = source.Description,
            Enabled = source.Enabled,
            Steps = source.Steps.Select(step => new ConfigTestStep
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
                RetryCount = step.RetryCount ?? defaultRetryCount,
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
            }).ToList()
        };
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
        _pluginInitSemaphore.Dispose();
        _disposed = true;
    }
}
