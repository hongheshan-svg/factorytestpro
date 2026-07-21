using System.Collections.Generic;

namespace UTF.UI.Models;

/// <summary>
/// 快速测试向导的输入数据传输对象。
/// 由 <c>QuickTestWizardWindow</c> 的代码后置从 UI 控件收集后传给 <c>ITestConfigurationBuilder</c>。
/// </summary>
public sealed class QuickTestWizardInput
{
    /// <summary>产品名称。</summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>产品型号。</summary>
    public string ProductModel { get; set; } = string.Empty;

    /// <summary>产品图标（emoji 字符串）。</summary>
    public string Icon { get; set; } = "📱";

    /// <summary>产品类别。</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>并行 DUT 数量。</summary>
    public int DUTCount { get; set; } = 1;

    /// <summary>是否启用串口通信。</summary>
    public bool UseSerial { get; set; }

    /// <summary>是否启用网络通信。</summary>
    public bool UseNetwork { get; set; }

    /// <summary>测试步骤输入项。</summary>
    public List<WizardStepInput> Steps { get; set; } = new();
}

/// <summary>
/// 测试步骤输入项（向导 -> builder）。
/// 与 <c>QuickTestWizardWindow.WizardStepItem</c> 字段一一对应，避免 ViewModel 引用 WPF 类型。
/// </summary>
public sealed class WizardStepInput
{
    public string Id { get; set; } = string.Empty;
    public int Order { get; set; }
    public string Name { get; set; } = string.Empty;
    public string StepType { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string? Command { get; set; }
    public string? Expected { get; set; }
    public int Timeout { get; set; } = 5000;
}
