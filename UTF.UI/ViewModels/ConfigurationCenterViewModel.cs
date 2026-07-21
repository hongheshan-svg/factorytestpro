using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    public ConfigurationCenterViewModel(
        ConfigurationManager configManager,
        IConfigurationAdapter configAdapter,
        IDialogService dialogService)
    {
        _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
        _configAdapter = configAdapter ?? throw new ArgumentNullException(nameof(configAdapter));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
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

    // ────────────────── 串口 / 网络主机 / 测试步骤集合 ──────────────────

    /// <summary>串口列表。ListBox 双向绑定。</summary>
    public ObservableCollection<string> SerialPorts { get; } = new();

    /// <summary>网络主机列表。ListBox 双向绑定。</summary>
    public ObservableCollection<string> NetworkHosts { get; } = new();

    /// <summary>测试步骤列表。DataGrid 双向绑定。</summary>
    public ObservableCollection<TestStepConfig> TestSteps { get; } = new();

    /// <summary>当前选中的串口（用于删除按钮）。</summary>
    [ObservableProperty]
    private string? _selectedSerialPort;

    /// <summary>当前选中的网络主机（用于删除按钮）。</summary>
    [ObservableProperty]
    private string? _selectedNetworkHost;

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
    /// CommunityToolkit 生成的 <c>SelectedStep</c> setter 的 partial 钩子。
    /// 在选中步骤变化时同步 <see cref="SelectedStepMaxRetries"/> 与详情面板可见性。
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
        ApplyStepMaxRetries();
        OnPropertyChanged(nameof(TestSteps));
    }

    /// <summary>新增串口输入文本。</summary>
    [ObservableProperty]
    private string _newSerialPort = "COM";

    /// <summary>新增网络主机输入文本。</summary>
    [ObservableProperty]
    private string _newNetworkHost = "192.168.1.";

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

        var endpoints = Config.DUTConfiguration?.CommunicationEndpoints;
        SerialPorts.Clear();
        if (endpoints?.SerialPorts != null)
        {
            foreach (var port in endpoints.SerialPorts)
            {
                SerialPorts.Add(port);
            }
        }

        NetworkHosts.Clear();
        if (endpoints?.NetworkHosts != null)
        {
            foreach (var host in endpoints.NetworkHosts)
            {
                NetworkHosts.Add(host);
            }
        }

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
        SelectedSerialPort = null;
        SelectedNetworkHost = null;
    }

    /// <summary>将各可观察集合与选中值写回 <see cref="Config"/>。</summary>
    private void SyncToConfig()
    {
        var sys = Config.SystemSettings;
        sys.LogLevel = LogLevel ?? "Info";
        sys.DefaultLanguage = LanguageCodeFromDisplay(Language);
        sys.Theme = Theme ?? "Light";

        Config.DUTConfiguration ??= new DUTConfiguration();
        Config.DUTConfiguration.CommunicationEndpoints ??= new CommunicationEndpoints();
        Config.DUTConfiguration.CommunicationEndpoints.SerialPorts = SerialPorts.ToList();
        Config.DUTConfiguration.CommunicationEndpoints.NetworkHosts = NetworkHosts.ToList();

        Config.TestProjectConfiguration ??= new TestProjectConfiguration();
        Config.TestProjectConfiguration.TestProject ??= new TestProject();
        Config.TestProjectConfiguration.TestProject.Steps = TestSteps.ToList();
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

    // ────────────────── 串口管理命令 ──────────────────

    /// <summary>新增串口。读取 <see cref="NewSerialPort"/>，校验后加入集合。</summary>
    [RelayCommand]
    private void AddSerialPort()
    {
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
        if (SelectedSerialPort is { } port)
        {
            SerialPorts.Remove(port);
            SelectedSerialPort = null;
        }
    }

    // ────────────────── 网络主机管理命令 ──────────────────

    /// <summary>新增网络主机。</summary>
    [RelayCommand]
    private void AddNetworkHost()
    {
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
                ContinueOnFailure = step.ContinueOnFailure
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
