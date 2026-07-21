using System;

namespace UTF.UI.Services;

/// <summary>
/// 窗口工厂抽象。集中负责辅助窗口（配置中心、快速向导、测试计划编辑器、
/// 插件 / 设备 / 用户管理）的权限校验、Owner 绑定与模态展示，
/// 使视图模型无需直接引用具体 <see cref="System.Windows.Window"/> 子类。
/// </summary>
public interface IWindowFactory
{
    /// <summary>
    /// 任意辅助窗口关闭（且可能已修改配置）时触发。
    /// <see cref="MainWindowViewModel"/> 订阅以触发配置刷新。
    /// </summary>
    event EventHandler<WindowClosedEventArgs>? ConfigurationApplied;

    /// <summary>展示配置管理中心对话框。</summary>
    /// <returns>用户保存则返回 <c>true</c>，取消 <c>false</c>，权限不足等提前退出 <c>null</c>。</returns>
    bool? ShowConfigurationCenterDialog();

    /// <summary>展示快速创建测试向导对话框。</summary>
    /// <returns>用户保存则返回 <c>true</c>，取消 <c>false</c>，权限不足等提前退出 <c>null</c>。</returns>
    bool? ShowQuickTestWizardDialog();

    /// <summary>展示测试计划编辑器对话框。</summary>
    /// <returns>用户保存则返回 <c>true</c>，取消 <c>false</c>，权限不足等提前退出 <c>null</c>。</returns>
    bool? ShowTestPlanEditorDialog();

    /// <summary>展示插件管理对话框。</summary>
    /// <returns>对话框关闭结果；权限不足则 <c>null</c>。</returns>
    bool? ShowPluginManagerDialog();

    /// <summary>展示设备管理对话框。</summary>
    /// <returns>对话框关闭结果；权限不足则 <c>null</c>。</returns>
    bool? ShowDeviceManagerDialog();

    /// <summary>展示用户管理对话框。</summary>
    /// <returns>对话框关闭结果；权限不足则 <c>null</c>。</returns>
    bool? ShowUserManagerDialog();
}

/// <summary>
/// 辅助窗口关闭事件参数。携带 <see cref="Source"/> 标识触发来源，
/// 便于订阅方按需选择刷新策略。
/// </summary>
public sealed class WindowClosedEventArgs : EventArgs
{
    /// <summary>触发来源（如 "ConfigurationCenter" / "QuickTestWizard" / "TestPlanEditor"）。</summary>
    public string? Source { get; init; }
}
