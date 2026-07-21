using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UTF.Configuration;
using UTF.Core;
using UTF.UI.Models;
using UTF.UI.Services;

namespace UTF.UI.ViewModels;

/// <summary>
/// 配置中心视图模型。承载统一配置对象的验证 / 保存 / 重新加载命令，
/// 并直接持有日志级别、语言、主题、串口列表、网络主机列表与测试步骤列表等
/// 两端绑定的可观察集合，取代窗口代码后置的 <c>PopulateManualFields</c> /
/// <c>CollectManualFields</c> 手动同步逻辑。
/// </summary>
public partial class ConfigurationCenterViewModel : ObservableObject
{
    private readonly ConfigurationManager _configManager;
    private readonly IConfigurationAdapter _configAdapter;
    private readonly IDialogService _dialogService;
    private readonly IPluginCapabilityService? _pluginCapabilities;

    public ConfigurationCenterViewModel(
        ConfigurationManager configManager,
        IConfigurationAdapter configAdapter,
        IDialogService dialogService,
        IPluginCapabilityService? pluginCapabilities = null)
    {
        _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
        _configAdapter = configAdapter ?? throw new ArgumentNullException(nameof(configAdapter));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _pluginCapabilities = pluginCapabilities;
        _config = new UnifiedConfiguration();
        EnsureNestedObjects(_config);
    }

    /// <summary>当前编辑中的统一配置对象。两端绑定字段直接写入其嵌套节点。</summary>
    [ObservableProperty]
    private UnifiedConfiguration _config;

    // ────────────────── 可选值集合（供 ComboBox/ListBox 绑定） ──────────────────

    /// <summary>日志级别可选值。</summary>
    public ObservableCollection<string> LogLevelOptions { get; } = new() { "Debug", "Info", "Warning", "Error" };

    /// <summary>语言可选值（显示文本，与 <see cref="LanguageCodes"/> 一一对应）。</summary>
    public ObservableCollection<string> LanguageOptions { get; } = new()
    {
        "简体中文 (zh-CN)",
        "English (en-US)",
        "日本語 (ja-JP)"
    };

    /// <summary>语言代码（与 <see cref="LanguageOptions"/> 索引对应）。</summary>
    private static readonly string[] LanguageCodes = { "zh-CN", "en-US", "ja-JP" };

    /// <summary>主题可选值。</summary>
    public ObservableCollection<string> ThemeOptions { get; } = new() { "Light", "Dark" };

    // ────────────────── 当前选中值（两端绑定） ──────────────────

    /// <summary>当前日志级别。</summary>
    [ObservableProperty]
    private string _logLevel = "Info";

    /// <summary>当前语言显示文本。</summary>
    [ObservableProperty]
    private string _language = "简体中文 (zh-CN)";

    /// <summary>当前主题。</summary>
    [ObservableProperty]
    private string _theme = "Light";

    // ────────────────── 端点 / 串口 / 网络主机 / 测试步骤集合 ──────────────────

    /// <summary>通信端点列表（主编辑源）。DataGrid 双向绑定。</summary>
    public ObservableCollection<EndpointDefinition> Endpoints { get; } = new();

    /// <summary>Kind 下拉可选值。</summary>
    public IReadOnlyList<string> EndpointKindOptions { get; } = EndpointMapper.KnownKinds;

    /// <summary>串口列表（Legacy，由 Endpoints 自动同步）。</summary>
    public ObservableCollection<string> SerialPorts { get; } = new();

    /// <summary>网络主机列表（Legacy，由 Endpoints 自动同步）。</summary>
    public ObservableCollection<string> NetworkHosts { get; } = new();

    /// <summary>测试步骤列表。DataGrid 双向绑定。</summary>
    public ObservableCollection<TestStepConfig> TestSteps { get; } = new();

    /// <summary>当前选中的端点（用于删除按钮）。</summary>
    [ObservableProperty]
    private EndpointDefinition? _selectedEndpoint;

    /// <summary>当前选中的串口（用于删除按钮）。</summary>
    [ObservableProperty]
    private string? _selectedSerialPort;

    /// <summary>当前选中的网络主机（用于删除按钮）。</summary>
    [ObservableProperty]
    private string? _selectedNetworkHost;

    /// <summary>是否存在端点（用于禁用 legacy 列表编辑）。</summary>
    public bool HasEndpoints => Endpoints.Count > 0;

    /// <summary>Legacy 列表是否可直接编辑（无 Endpoints 时）。</summary>
    public bool LegacyListsEditable => Endpoints.Count == 0;

    /// <summary>当前选中的测试步骤（用于编辑/删除/移动）。</summary>
    [ObservableProperty]
    private TestStepConfig? _selectedStep;

    /// <summary>
    /// 步骤详情面板中"最大重试"输入框的绑定源。
    /// 由 <see cref="OnSelectedStepChanged"/> 在选中步骤切换时从
    /// <c>SelectedStep.Parameters["MaxRetries"]</c> 同步；用户编辑后由
    /// <see cref="ApplyStepMaxRetries"/> 回写。独立 ObservableProperty 避免
    /// 将 int? 直接双向绑定到 <c>Dictionary</c> 项带来的双向同步复杂度。
    /// </summary>
    [ObservableProperty]
    private int? _selectedStepMaxRetries;

    /// <summary>
    /// Dynamic plugin parameter fields for the selected step (from <c>parameterSchema</c>).
    /// Empty when no matching plugin or schema is absent.
    /// </summary>
    public ObservableCollection<StepParameterFieldViewModel> DynamicParameterFields { get; } = new();

    /// <summary>
    /// Whether the dynamic parameter panel should be shown (schema fields present).
    /// </summary>
    public bool HasDynamicParameterFields => DynamicParameterFields.Count > 0;

    /// <summary>
    /// CommunityToolkit 生成的 <c>SelectedStep</c> setter 的 partial 钩子。
    /// 在选中步骤变化时同步 <see cref="SelectedStepMaxRetries"/> 与动态参数面板。
    /// </summary>
    partial void OnSelectedStepChanged(TestStepConfig? value)
    {
        if (value?.Parameters is { } parameters
            && parameters.TryGetValue("MaxRetries", out var raw)
            && raw is int maxRetries)
        {
            SelectedStepMaxRetries = maxRetries;
        }
        else if (value?.Parameters is { } dict
            && dict.TryGetValue("MaxRetries", out var strRaw)
            && int.TryParse(strRaw?.ToString(), out var parsed))
        {
            SelectedStepMaxRetries = parsed;
        }
        else
        {
            SelectedStepMaxRetries = null;
        }

        RebuildDynamicParameterFields();
    }

    /// <summary>
    /// 将 <see cref="SelectedStepMaxRetries"/> 回写到
    /// <c>SelectedStep.Parameters["MaxRetries"]</c>。由详情面板"应用"按钮调用，
    /// 也在 TwoWay 绑定 UpdateSourceTrigger=PropertyChanged 时由绑定引擎触发。
    /// 由于 <see cref="TestStepConfig"/> 不实现 INPC，DataGrid 刷新由
    /// <see cref="RefreshStepsCommand"/> 显式触发。
    /// </summary>
    public void ApplyStepMaxRetries()
    {
        if (SelectedStep is null)
        {
            return;
        }

        SelectedStep.Parameters ??= new Dictionary<string, object>();
        if (SelectedStepMaxRetries is int value && value > 0)
        {
            SelectedStep.Parameters["MaxRetries"] = value;
        }
        else
        {
            SelectedStep.Parameters.Remove("MaxRetries");
        }
    }

    /// <summary>
    /// Commit dynamic parameter editors and MaxRetries into <see cref="SelectedStep"/>.Parameters.
    /// Unknown keys not in the schema are left untouched.
    /// </summary>
    public void ApplyDynamicParameters()
    {
        ApplyStepMaxRetries();
        foreach (var field in DynamicParameterFields)
        {
            field.Commit();
        }
    }

    /// <summary>
    /// Rebuild <see cref="DynamicParameterFields"/> from the matching plugin
    /// <c>parameterSchema</c> for the selected step's Type/Channel.
    /// Call after Type or Channel changes (window code-behind hooks ComboBox).
    /// </summary>
    public void RebuildDynamicParameterFields()
    {
        // Persist current field values before rebuilding so Type/Channel edits don't drop edits.
        foreach (var field in DynamicParameterFields)
        {
            field.Commit();
        }

        DynamicParameterFields.Clear();

        var step = SelectedStep;
        if (step is null || _pluginCapabilities is null)
        {
            OnPropertyChanged(nameof(HasDynamicParameterFields));
            return;
        }

        step.Parameters ??= new Dictionary<string, object>();
        var schema = _pluginCapabilities.GetParameterSchema(step.Type, step.Channel);
        foreach (var item in schema)
        {
            if (string.IsNullOrWhiteSpace(item.Name))
            {
                continue;
            }

            DynamicParameterFields.Add(new StepParameterFieldViewModel(item, step.Parameters));
        }

        OnPropertyChanged(nameof(HasDynamicParameterFields));
    }

    /// <summary>
    /// 提示 DataGrid 刷新行显示。由于 <see cref="TestStepConfig"/> 不实现 INPC，
    /// TwoWay 绑定直接回写属性时 DataGrid 不会自动重绘当前行文本；
    /// 此命令由详情面板"刷新"按钮触发，调用方（ConfigurationCenterWindow.xaml.cs）
    /// 监听并在 UI 线程调用 <c>TestStepsGrid.Items.Refresh()</c>。
    /// </summary>
    [RelayCommand]
    private void RefreshSteps()
    {
        // 实际刷新由 ConfigurationCenterWindow 代码后置订阅本命令的 CanExecuteChanged/调用执行；
        // 此处仅触发一次属性变更以驱动绑定更新。
        ApplyDynamicParameters();
        RebuildDynamicParameterFields();
        OnPropertyChanged(nameof(TestSteps));
    }

    /// <summary>新增串口输入文本。</summary>
    [ObservableProperty]
    private string _newSerialPort = "COM";

    /// <summary>新增网络主机输入文本。</summary>
    [ObservableProperty]
    private string _newNetworkHost = "192.168.1.";

    /// <summary>新增端点默认 Kind。</summary>
    [ObservableProperty]
    private string _newEndpointKind = "serial";

    /// <summary>
    /// 从 <see cref="ConfigurationManager"/> 异步加载配置到 <see cref="Config"/>，
    /// 并同步填充所有集合与选中值。
    /// </summary>
    public async Task LoadConfigAsync()
    {
        var loaded = await _configManager.GetUnifiedConfigurationAsync();
        EnsureNestedObjects(loaded);
        Config = loaded;
        SyncFromConfig();
    }

    /// <summary>将 <see cref="Config"/> 中的列表/枚举字段同步到各可观察集合与选中值。</summary>
    private void SyncFromConfig()
    {
        var sys = Config.SystemSettings;
        LogLevel = string.IsNullOrEmpty(sys.LogLevel) ? "Info" : sys.LogLevel;

        // 语言代码 -> 显示文本
        Language = sys.DefaultLanguage switch
        {
            "zh-CN" => "简体中文 (zh-CN)",
            "en-US" => "English (en-US)",
            "ja-JP" => "日本語 (ja-JP)",
            _ => "简体中文 (zh-CN)"
        };

        Theme = string.IsNullOrEmpty(sys.Theme) ? "Light" : sys.Theme;

        // Prefer Endpoints; synthesize from legacy SerialPorts/NetworkHosts when empty.
        EndpointMapper.NormalizeEndpoints(Config);
        Endpoints.Clear();
        if (Config.DUTConfiguration?.Endpoints != null)
        {
            foreach (var ep in Config.DUTConfiguration.Endpoints)
            {
                Endpoints.Add(CloneEndpoint(ep));
            }
        }

        RefreshLegacyListsFromEndpoints();

        TestSteps.Clear();
        var steps = Config.TestProjectConfiguration?.TestProject?.Steps;
        if (steps != null)
        {
            foreach (var step in steps)
            {
                TestSteps.Add(step);
            }
        }

        SelectedStep = null;
        SelectedEndpoint = null;
        SelectedSerialPort = null;
        SelectedNetworkHost = null;
        NotifyEndpointCollectionChanged();
    }

    /// <summary>将各可观察集合与选中值写回 <see cref="Config"/>。</summary>
    private void SyncToConfig()
    {
        var sys = Config.SystemSettings;
        sys.LogLevel = LogLevel ?? "Info";
        sys.DefaultLanguage = LanguageCodeFromDisplay(Language);
        sys.Theme = Theme ?? "Light";

        Config.DUTConfiguration ??= new DUTConfiguration();
        Config.DUTConfiguration.Endpoints = Endpoints
            .Select(CloneEndpoint)
            .ToList();

        // Endpoints are source of truth → mirror into legacy SerialPorts/NetworkHosts.
        EndpointMapper.MirrorEndpointsToLegacy(Config);

        // If Endpoints empty and user still edited legacy lists, keep those.
        if (Endpoints.Count == 0)
        {
            Config.DUTConfiguration.CommunicationEndpoints ??= new CommunicationEndpoints();
            Config.DUTConfiguration.CommunicationEndpoints.SerialPorts = SerialPorts.ToList();
            Config.DUTConfiguration.CommunicationEndpoints.NetworkHosts = NetworkHosts.ToList();
        }
        else
        {
            RefreshLegacyListsFromEndpoints();
        }

        Config.TestProjectConfiguration ??= new TestProjectConfiguration();
        Config.TestProjectConfiguration.TestProject ??= new TestProject();
        Config.TestProjectConfiguration.TestProject.Steps = TestSteps.ToList();
    }

    private void RefreshLegacyListsFromEndpoints()
    {
        var endpoints = Config.DUTConfiguration?.CommunicationEndpoints;
        // After mirror, CommunicationEndpoints holds derived lists; if not mirrored yet, derive live.
        var serial = Endpoints.Count > 0
            ? Endpoints.Where(e => EndpointMapper.IsSerialLike(e.Kind) && !string.IsNullOrWhiteSpace(e.Address))
                .Select(e => e.Address.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
            : endpoints?.SerialPorts?.ToList() ?? new List<string>();

        var hosts = Endpoints.Count > 0
            ? Endpoints.Where(e => EndpointMapper.IsNetworkLike(e.Kind) && !string.IsNullOrWhiteSpace(e.Address))
                .Select(e => e.Address.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
            : endpoints?.NetworkHosts?.ToList() ?? new List<string>();

        SerialPorts.Clear();
        foreach (var port in serial)
        {
            SerialPorts.Add(port);
        }

        NetworkHosts.Clear();
        foreach (var host in hosts)
        {
            NetworkHosts.Add(host);
        }
    }

    private void NotifyEndpointCollectionChanged()
    {
        OnPropertyChanged(nameof(HasEndpoints));
        OnPropertyChanged(nameof(LegacyListsEditable));
    }

    private static EndpointDefinition CloneEndpoint(EndpointDefinition source)
    {
        return new EndpointDefinition
        {
            Id = source.Id ?? string.Empty,
            Kind = string.IsNullOrWhiteSpace(source.Kind) ? "serial" : source.Kind,
            Address = source.Address ?? string.Empty,
            DisplayName = source.DisplayName,
            Settings = source.Settings == null
                ? null
                : new Dictionary<string, object>(source.Settings)
        };
    }

    /// <summary>由显示文本反查语言代码。</summary>
    private static string LanguageCodeFromDisplay(string? display)
        => display switch
        {
            "English (en-US)" => "en-US",
            "日本語 (ja-JP)" => "ja-JP",
            _ => "zh-CN"
        };

    /// <summary>
    /// 将 <paramref name="config"/> 的可空嵌套对象初始化为非空默认实例。
    /// TwoWay 绑定路径如 <c>DUTConfiguration.ProductInfo.Name</c> 需要中间节点非空才能写入。
    /// </summary>
    private static void EnsureNestedObjects(UnifiedConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.SystemSettings ??= new SystemSettings();
        config.DUTConfiguration ??= new DUTConfiguration();
        config.DUTConfiguration.ProductInfo ??= new ProductInfo();
        config.DUTConfiguration.GlobalSettings ??= new GlobalSettings();
        config.DUTConfiguration.CommunicationEndpoints ??= new CommunicationEndpoints();
        config.DUTConfiguration.Endpoints ??= new List<EndpointDefinition>();
        config.DUTConfiguration.NamingConfig ??= new NamingConfig();
        config.TestProjectConfiguration ??= new TestProjectConfiguration();
        config.TestProjectConfiguration.TestProject ??= new TestProject();
        config.TestProjectConfiguration.TestMode ??= new TestMode();
    }

    /// <summary>校验状态文本。</summary>
    [ObservableProperty]
    private string _validationStatus = "✅ 配置有效";

    /// <summary>校验状态颜色（绿色 / 红色）。由窗口按状态设置。</summary>
    [ObservableProperty]
    private string _validationStatusColor = "#28A745";

    /// <summary>
    /// 从 <paramref name="config"/> 中收集校验结果并写入 <see cref="ValidationStatus"/>。
    /// 返回错误列表，便于调用方进一步展示。
    /// </summary>
    public IReadOnlyList<string> Validate(UnifiedConfiguration? config = null)
    {
        // 先将 UI 集合写回 Config，确保校验基于最新输入。
        ApplyDynamicParameters();
        SyncToConfig();
        var target = config ?? Config;
        var errors = _configAdapter.ValidateConfigurationWithErrors(target);
        if (errors is null || errors.Count == 0)
        {
            ValidationStatus = "✅ 配置有效";
            ValidationStatusColor = "#28A745";
            return Array.Empty<string>();
        }

        ValidationStatus = $"❌ {errors[0]}";
        ValidationStatusColor = "#DC3545";
        return errors;
    }

    /// <summary>校验当前 <see cref="Config"/>；失败则抛出包含全部错误的异常。</summary>
    [RelayCommand]
    private void Validate()
    {
        Validate(Config);
    }

    /// <summary>持久化 <see cref="Config"/> 到磁盘并触发配置刷新。</summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        ApplyDynamicParameters();
        SyncToConfig();
        var errors = _configAdapter.ValidateConfigurationWithErrors(Config);
        if (errors is null || errors.Count > 0)
        {
            ValidationStatus = errors is { Count: > 0 } ? $"❌ {errors[0]}" : "❌ 配置无效";
            ValidationStatusColor = "#DC3545";
            throw new InvalidOperationException(string.Join("; ", errors ?? new List<string>()));
        }

        await _configManager.SaveUnifiedConfigurationAsync(Config);
        await _configManager.RefreshConfiguration();
        ValidationStatus = "✅ 配置已保存";
        ValidationStatusColor = "#28A745";
    }

    /// <summary>从 <see cref="ConfigurationManager"/> 重新加载配置到 <see cref="Config"/>。</summary>
    [RelayCommand]
    private async Task ReloadAsync()
    {
        await LoadConfigAsync();
        ValidationStatus = "🔄 已重新加载";
        ValidationStatusColor = "#007ACC";
    }

    // ────────────────── 端点管理命令 ──────────────────

    /// <summary>新增通信端点。</summary>
    [RelayCommand]
    private void AddEndpoint()
    {
        var kind = string.IsNullOrWhiteSpace(NewEndpointKind) ? "serial" : NewEndpointKind.Trim().ToLowerInvariant();
        var index = Endpoints.Count(e => string.Equals(e.Kind, kind, StringComparison.OrdinalIgnoreCase)) + 1;
        var idBase = kind;
        var id = $"{idBase}-{index}";
        while (Endpoints.Any(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase)))
        {
            index++;
            id = $"{idBase}-{index}";
        }

        var address = kind switch
        {
            "serial" => $"COM{2 + Endpoints.Count(e => EndpointMapper.IsSerialLike(e.Kind))}",
            "network" or "telnet" => $"192.168.1.{10 + Endpoints.Count(e => EndpointMapper.IsNetworkLike(e.Kind))}",
            "adb" => "emulator-5554",
            "scpi" => "TCPIP0::192.168.1.100::INSTR",
            _ => string.Empty
        };

        var ep = new EndpointDefinition
        {
            Id = id,
            Kind = kind,
            Address = address,
            DisplayName = address
        };
        Endpoints.Add(ep);
        SelectedEndpoint = ep;
        RefreshLegacyListsFromEndpoints();
        NotifyEndpointCollectionChanged();
    }

    /// <summary>删除当前选中的通信端点。</summary>
    [RelayCommand]
    private void RemoveEndpoint()
    {
        if (SelectedEndpoint is { } ep)
        {
            Endpoints.Remove(ep);
            SelectedEndpoint = null;
            RefreshLegacyListsFromEndpoints();
            NotifyEndpointCollectionChanged();
        }
    }

    /// <summary>端点编辑后刷新 legacy 列表显示。</summary>
    [RelayCommand]
    private void RefreshEndpoints()
    {
        RefreshLegacyListsFromEndpoints();
        OnPropertyChanged(nameof(Endpoints));
        NotifyEndpointCollectionChanged();
    }

    // ────────────────── 串口管理命令（Legacy） ──────────────────

    /// <summary>新增串口。读取 <see cref="NewSerialPort"/>，校验后加入集合。</summary>
    [RelayCommand]
    private void AddSerialPort()
    {
        if (!LegacyListsEditable)
        {
            _dialogService.ShowInformation("请在「通信端点」中编辑；Legacy 列表由端点自动同步。");
            return;
        }

        var port = (NewSerialPort ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(port) || !port.StartsWith("COM", StringComparison.OrdinalIgnoreCase))
        {
            _dialogService.ShowWarning("请输入有效的串口名（如 COM3）");
            return;
        }
        if (SerialPorts.Contains(port))
        {
            _dialogService.ShowInformation($"{port} 已存在");
            return;
        }
        SerialPorts.Add(port);
        NewSerialPort = "COM";
    }

    /// <summary>删除当前选中的串口。</summary>
    [RelayCommand]
    private void RemoveSerialPort()
    {
        if (!LegacyListsEditable)
        {
            _dialogService.ShowInformation("请在「通信端点」中编辑；Legacy 列表由端点自动同步。");
            return;
        }

        if (SelectedSerialPort is { } port)
        {
            SerialPorts.Remove(port);
            SelectedSerialPort = null;
        }
    }

    // ────────────────── 网络主机管理命令（Legacy） ──────────────────

    /// <summary>新增网络主机。</summary>
    [RelayCommand]
    private void AddNetworkHost()
    {
        if (!LegacyListsEditable)
        {
            _dialogService.ShowInformation("请在「通信端点」中编辑；Legacy 列表由端点自动同步。");
            return;
        }

        var host = (NewNetworkHost ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(host))
        {
            _dialogService.ShowWarning("请输入有效的IP地址");
            return;
        }
        if (NetworkHosts.Contains(host))
        {
            _dialogService.ShowInformation($"{host} 已存在");
            return;
        }
        NetworkHosts.Add(host);
        NewNetworkHost = "192.168.1.";
    }

    /// <summary>删除当前选中的网络主机。</summary>
    [RelayCommand]
    private void RemoveNetworkHost()
    {
        if (!LegacyListsEditable)
        {
            _dialogService.ShowInformation("请在「通信端点」中编辑；Legacy 列表由端点自动同步。");
            return;
        }

        if (SelectedNetworkHost is { } host)
        {
            NetworkHosts.Remove(host);
            SelectedNetworkHost = null;
        }
    }

    // ────────────────── 测试步骤管理命令 ──────────────────

    /// <summary>新增测试步骤。</summary>
    [RelayCommand]
    private void AddStep()
    {
        var step = new TestStepConfig
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Name = "新步骤",
            Order = TestSteps.Count + 1,
            Enabled = true,
            Type = string.Empty,
            Channel = string.Empty,
            Target = "dut",
            Timeout = 5000
        };
        TestSteps.Add(step);
        SelectedStep = step;
    }

    /// <summary>删除当前选中的测试步骤。</summary>
    [RelayCommand]
    private void RemoveStep()
    {
        if (SelectedStep is { } step)
        {
            TestSteps.Remove(step);
            RenumberSteps();
            SelectedStep = null;
        }
    }

    /// <summary>上移当前选中的测试步骤。</summary>
    [RelayCommand]
    private void MoveStepUp()
    {
        if (SelectedStep is { } step)
        {
            int idx = TestSteps.IndexOf(step);
            if (idx > 0)
            {
                TestSteps.Move(idx, idx - 1);
                RenumberSteps();
            }
        }
    }

    /// <summary>下移当前选中的测试步骤。</summary>
    [RelayCommand]
    private void MoveStepDown()
    {
        if (SelectedStep is { } step)
        {
            int idx = TestSteps.IndexOf(step);
            if (idx >= 0 && idx < TestSteps.Count - 1)
            {
                TestSteps.Move(idx, idx + 1);
                RenumberSteps();
            }
        }
    }

    /// <summary>复制当前选中的测试步骤。</summary>
    [RelayCommand]
    private void CopyStep()
    {
        if (SelectedStep is { } step)
        {
            var copy = new TestStepConfig
            {
                Id = Guid.NewGuid().ToString("N")[..8],
                Name = step.Name + " (副本)",
                Order = TestSteps.Count + 1,
                Enabled = step.Enabled,
                Type = step.Type,
                Channel = step.Channel,
                Target = step.Target,
                Description = step.Description,
                Command = step.Command,
                Expected = step.Expected,
                Timeout = step.Timeout,
                Delay = step.Delay,
                ContinueOnFailure = step.ContinueOnFailure,
                Parameters = step.Parameters is null
                    ? null
                    : new Dictionary<string, object>(step.Parameters)
            };
            TestSteps.Add(copy);
            SelectedStep = copy;
        }
    }

    private void RenumberSteps()
    {
        for (int i = 0; i < TestSteps.Count; i++)
        {
            TestSteps[i].Order = i + 1;
        }
    }
}
