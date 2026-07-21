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
/// 快速测试向导视图模型。承载产品信息、工位设置、测试步骤列表与保存命令。
/// 所有 UI 字段（含产品图标/类别、步骤输入与步骤集合）均通过双向绑定直接读写，
/// 取代窗口代码后置的 <c>BuildQuickTestWizardInput</c> 手动收集逻辑。
/// </summary>
public partial class QuickTestWizardViewModel : ObservableObject
{
    private readonly ITestConfigurationBuilder _configBuilder;
    private readonly ConfigurationManager _configManager;

    /// <summary>配置已创建事件，MainWindow / WindowFactory 可监听以刷新 UI。</summary>
    public event EventHandler? ConfigurationCreated;

    public QuickTestWizardViewModel(
        ITestConfigurationBuilder configBuilder,
        ConfigurationManager configManager)
    {
        _configBuilder = configBuilder ?? throw new ArgumentNullException(nameof(configBuilder));
        _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
        Steps.CollectionChanged += (_, _) =>
        {
            HasSteps = Steps.Count > 0;
            HasNoSteps = Steps.Count == 0;
        };
    }

    /// <summary>是否已添加至少一个步骤（驱动空提示可见性）。</summary>
    [ObservableProperty]
    private bool _hasSteps;

    /// <summary>是否没有任何步骤（驱动空提示可见性，与 <see cref="HasSteps"/> 互补）。</summary>
    [ObservableProperty]
    private bool _hasNoSteps = true;

    /// <summary>保存命令的可用性状态（基于是否已收集到足够输入）。</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _canSave;

    // ────────────────── Step 1: 产品信息 / 工位设置 ──────────────────

    /// <summary>产品名称。</summary>
    [ObservableProperty]
    private string _productName = string.Empty;

    /// <summary>产品型号。</summary>
    [ObservableProperty]
    private string _productModel = string.Empty;

    /// <summary>产品图标（emoji 字符串）。</summary>
    [ObservableProperty]
    private string _productIcon = "📱";

    /// <summary>产品类别。</summary>
    [ObservableProperty]
    private string _productCategory = string.Empty;

    /// <summary>DUT 工位数量。</summary>
    [ObservableProperty]
    private int _dutCount = 4;

    /// <summary>是否使用串口通信。</summary>
    [ObservableProperty]
    private bool _useSerial = true;

    /// <summary>是否使用网络通信。</summary>
    [ObservableProperty]
    private bool _useNetwork;

    /// <summary>是否设为默认配置。</summary>
    [ObservableProperty]
    private bool _saveAsDefault = true;

    /// <summary>是否同时导出副本。</summary>
    [ObservableProperty]
    private bool _exportCopy;

    /// <summary>产品图标可选值（ComboBox 数据源）。</summary>
    public ObservableCollection<string> AvailableIcons { get; } = new()
    {
        "📱", "🖥️", "🔧", "🎮", "🔌", "💡", "🚗", "🏭"
    };

    /// <summary>产品类别可选值（可编辑 ComboBox 数据源）。</summary>
    public ObservableCollection<string> AvailableCategories { get; } = new()
    {
        "消费电子产品", "汽车电子", "工业设备", "通信设备", "智能家居", "医疗设备", "其他"
    };

    // ────────────────── Step 2: 测试步骤 ──────────────────

    /// <summary>已添加的测试步骤列表（双向绑定到 ItemsControl）。</summary>
    public ObservableCollection<WizardStepInput> Steps { get; } = new();

    /// <summary>当前选中的步骤（用于上移/下移/删除）。</summary>
    [ObservableProperty]
    private WizardStepInput? _selectedStep;

    /// <summary>步骤类别可选值（由代码后置从插件能力发现后填充）。</summary>
    public ObservableCollection<WizardStepCategory> AvailableStepCategories { get; } = new();

    /// <summary>当前选中的步骤类别。</summary>
    [ObservableProperty]
    private WizardStepCategory? _selectedStepCategory;

    /// <summary>步骤类别命令提示文本（随 <see cref="SelectedStepCategory"/> 变化）。</summary>
    [ObservableProperty]
    private string _stepCommandHint = string.Empty;

    /// <summary>新增步骤的名称输入。</summary>
    [ObservableProperty]
    private string _newStepName = string.Empty;

    /// <summary>新增步骤的命令输入。</summary>
    [ObservableProperty]
    private string _newStepCommand = string.Empty;

    /// <summary>期望结果判定方式（contains/equals/regex/none）。</summary>
    [ObservableProperty]
    private string _newStepExpectedMode = "contains";

    /// <summary>期望结果判定方式可选值。</summary>
    public ObservableCollection<string> ExpectedModeOptions { get; } = new()
    {
        "包含文本（推荐）", "完全匹配", "正则表达式", "不判定（仅执行）"
    };

    /// <summary>期望结果判定方式对应的内部 Tag（与 <see cref="ExpectedModeOptions"/> 索引对应）。</summary>
    private static readonly string[] ExpectedModeTags = { "contains", "equals", "regex", "none" };

    /// <summary>当前选中的期望结果判定方式显示文本。</summary>
    [ObservableProperty]
    private string _selectedExpectedMode = "包含文本（推荐）";

    /// <summary>期望的结果内容输入。</summary>
    [ObservableProperty]
    private string _newStepExpectedValue = string.Empty;

    /// <summary>超时时间可选值（毫秒）。</summary>
    public ObservableCollection<string> TimeoutOptions { get; } = new()
    {
        "5 秒（快速命令）", "10 秒（普通命令）", "30 秒（耗时操作）", "60 秒（长时测试）", "120 秒（超长测试）"
    };

    /// <summary>超时选项对应的毫秒数（与 <see cref="TimeoutOptions"/> 索引对应）。</summary>
    private static readonly int[] TimeoutValues = { 5000, 10000, 30000, 60000, 120000 };

    /// <summary>当前选中的超时选项显示文本。</summary>
    [ObservableProperty]
    private string _selectedTimeout = "5 秒（快速命令）";

    /// <summary>下一个步骤序号。</summary>
    private int _nextStepId = 1;

    // ────────────────── 步骤编辑命令 ──────────────────

    /// <summary>添加当前输入的步骤到 <see cref="Steps"/>。</summary>
    [RelayCommand]
    private void AddStep()
    {
        if (string.IsNullOrWhiteSpace(NewStepName))
        {
            return;
        }

        if (SelectedStepCategory is null)
        {
            return;
        }

        var expected = BuildExpectedExpression();
        var timeout = ResolveTimeout();

        var step = new WizardStepInput
        {
            Id = $"step_{_nextStepId:D3}",
            Order = Steps.Count + 1,
            Name = NewStepName.Trim(),
            StepType = SelectedStepCategory.StepType,
            Channel = SelectedStepCategory.Channel,
            Command = NewStepCommand.Trim(),
            Expected = expected,
            Timeout = timeout
        };

        Steps.Add(step);
        _nextStepId++;

        // 清空输入
        NewStepName = string.Empty;
        NewStepCommand = string.Empty;
        NewStepExpectedValue = string.Empty;
        SelectedExpectedMode = "包含文本（推荐）";
        SelectedTimeout = "5 秒（快速命令）";
    }

    /// <summary>删除指定步骤。</summary>
    [RelayCommand]
    private void RemoveStep(WizardStepInput? step)
    {
        if (step is null)
        {
            return;
        }
        Steps.Remove(step);
        RenumberSteps();
    }

    /// <summary>上移指定步骤。</summary>
    [RelayCommand]
    private void MoveStepUp(WizardStepInput? step)
    {
        if (step is null)
        {
            return;
        }
        int idx = Steps.IndexOf(step);
        if (idx > 0)
        {
            Steps.Move(idx, idx - 1);
            RenumberSteps();
        }
    }

    /// <summary>下移指定步骤。</summary>
    [RelayCommand]
    private void MoveStepDown(WizardStepInput? step)
    {
        if (step is null)
        {
            return;
        }
        int idx = Steps.IndexOf(step);
        if (idx >= 0 && idx < Steps.Count - 1)
        {
            Steps.Move(idx, idx + 1);
            RenumberSteps();
        }
    }

    private void RenumberSteps()
    {
        for (int i = 0; i < Steps.Count; i++)
        {
            Steps[i].Order = i + 1;
        }
    }

    private string? BuildExpectedExpression()
    {
        var mode = ResolveExpectedModeTag();
        var value = (NewStepExpectedValue ?? string.Empty).Trim();

        if (mode == "none" || string.IsNullOrEmpty(value))
        {
            return null;
        }

        return mode switch
        {
            "contains" => $"contains:{value}",
            "equals" => $"equals:{value}",
            "regex" => $"regex:{value}",
            _ => null
        };
    }

    private string ResolveExpectedModeTag()
    {
        int idx = ExpectedModeOptions.IndexOf(SelectedExpectedMode ?? string.Empty);
        return idx >= 0 ? ExpectedModeTags[idx] : "none";
    }

    private int ResolveTimeout()
    {
        int idx = TimeoutOptions.IndexOf(SelectedTimeout ?? string.Empty);
        return idx >= 0 ? TimeoutValues[idx] : 5000;
    }

    /// <summary>
    /// 由代码后置在 <see cref="SelectedStepCategory"/> 变更后调用，
    /// 刷新 <see cref="StepCommandHint"/>。
    /// </summary>
    partial void OnSelectedStepCategoryChanged(WizardStepCategory? value)
    {
        StepCommandHint = value is null
            ? string.Empty
            : "💡 " + value.CommandHint;
    }

    /// <summary>从 VM 已绑定属性构建 <see cref="QuickTestWizardInput"/>。</summary>
    public QuickTestWizardInput BuildInput()
    {
        return new QuickTestWizardInput
        {
            ProductName = ProductName?.Trim() ?? string.Empty,
            ProductModel = ProductModel?.Trim() ?? string.Empty,
            Icon = ProductIcon ?? "📱",
            Category = ProductCategory ?? string.Empty,
            DUTCount = DutCount,
            UseSerial = UseSerial,
            UseNetwork = UseNetwork,
            Steps = Steps.Select(s => new WizardStepInput
            {
                Id = s.Id,
                Order = s.Order,
                Name = s.Name,
                StepType = s.StepType,
                Channel = s.Channel,
                Command = s.Command,
                Expected = s.Expected,
                Timeout = s.Timeout
            }).ToList()
        };
    }

    /// <summary>
    /// 基于输入构建一份 <see cref="UnifiedConfiguration"/> 并保存为默认配置（可选）。
    /// 触发 <see cref="ConfigurationCreated"/> 事件。
    /// </summary>
    /// <param name="saveAsDefault">是否覆盖默认配置文件。</param>
    /// <param name="exportPath">可选的导出路径（JSON）。</param>
    /// <returns>构建好的配置对象。</returns>
    public async Task<UnifiedConfiguration> SaveAsync(bool saveAsDefault, string? exportPath)
    {
        var input = BuildInput();

        var config = _configBuilder.Build(input);

        if (saveAsDefault)
        {
            await _configManager.SaveUnifiedConfigurationAsync(config);
            await _configManager.RefreshConfiguration();
        }

        if (!string.IsNullOrWhiteSpace(exportPath))
        {
            var json = System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            await System.IO.File.WriteAllTextAsync(exportPath, json);
        }

        ConfigurationCreated?.Invoke(this, EventArgs.Empty);
        return config;
    }

    /// <summary>保存命令。窗口代码后置收集导出路径后调用 <see cref="SaveAsync(bool, string?)"/>。</summary>
    [RelayCommand(CanExecute = nameof(CanSave))]
    private Task SaveAsync()
    {
        // 占位实现：实际保存逻辑通过 <see cref="SaveAsync(bool, string?)"/> 完成。
        return Task.CompletedTask;
    }

    /// <summary>校验输入是否合法（非空、有步骤）。</summary>
    public bool ValidateInput(QuickTestWizardInput input, out List<string> errors)
    {
        errors = new List<string>();
        if (string.IsNullOrWhiteSpace(input.ProductName))
        {
            errors.Add("产品名称未填写");
        }
        if (input.Steps is null || input.Steps.Count == 0)
        {
            errors.Add("没有测试步骤");
        }
        return errors.Count == 0;
    }
}

/// <summary>
/// 向导步骤类别（映射插件能力到用户友好的类别）。
/// 由窗口代码后置从 <c>StepExecutorPluginHost</c> 已加载插件构建后填充到 VM。
/// </summary>
public sealed class WizardStepCategory
{
    /// <summary>显示标签（如“🔧 SerialDriver · serial/Serial”）。</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>步骤类型。</summary>
    public string StepType { get; set; } = string.Empty;

    /// <summary>通信通道。</summary>
    public string Channel { get; set; } = string.Empty;

    /// <summary>命令提示。</summary>
    public string CommandHint { get; set; } = string.Empty;

    /// <summary>插件标识。</summary>
    public string PluginId { get; set; } = string.Empty;

    /// <summary>插件名称。</summary>
    public string PluginName { get; set; } = string.Empty;
}
