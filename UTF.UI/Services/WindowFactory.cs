using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using UTF.Core;

namespace UTF.UI.Services;

/// <summary>
/// <see cref="IWindowFactory"/> 的默认实现。每个 <c>Show*Dialog</c> 方法：
/// 1) 通过 <see cref="IPermissionManager.HasPermission"/> 校验权限，不足时提示并返回 <c>null</c>；
/// 2) 从 <see cref="IServiceProvider"/> 解析窗口（DI 注册）或直接构造（依赖既有签名）；
/// 3) 将主窗口绑定为 Owner（若可用）；
/// 4) 模态展示；返回值反映保存 / 取消状态，并在保存后触发 <see cref="ConfigurationApplied"/>。
/// </summary>
public sealed class WindowFactory : IWindowFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IDialogService _dialogService;
    private readonly IPermissionManager _permissionManager;
    private readonly DUTMonitorManager _dutMonitorManager;
    private readonly Func<Window?> _ownerResolver;

    /// <summary>
    /// 初始化工厂。所有依赖由 DI 注入；<see cref="DUTMonitorManager"/> 用于
    /// <see cref="PluginManagementWindow"/> 的直接构造。
    /// <paramref name="ownerResolver"/> 在组合根（App/DI）注入，用于解析当前主窗口作为
    /// 辅助窗口的 <c>Owner</c>。生产环境返回 <c>Application.Current.MainWindow</c>；
    /// 测试环境可注入 <c>() => null</c> 或 mock 窗口。
    /// </summary>
    public WindowFactory(
        IServiceProvider serviceProvider,
        IDialogService dialogService,
        IPermissionManager permissionManager,
        DUTMonitorManager dutMonitorManager,
        Func<Window?> ownerResolver)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _permissionManager = permissionManager ?? throw new ArgumentNullException(nameof(permissionManager));
        _dutMonitorManager = dutMonitorManager ?? throw new ArgumentNullException(nameof(dutMonitorManager));
        _ownerResolver = ownerResolver ?? throw new ArgumentNullException(nameof(ownerResolver));
    }

    /// <inheritdoc />
    public event EventHandler<WindowClosedEventArgs>? ConfigurationApplied;

    /// <inheritdoc />
    public bool? ShowConfigurationCenterDialog()
    {
        if (!Require(Permission.SystemConfig, "修改系统配置"))
        {
            return null;
        }

        var win = _serviceProvider.GetRequiredService<ConfigurationCenterWindow>();
        TrySetOwner(win);
        var result = win.ShowDialog();
        if (result == true)
        {
            RaiseApplied("ConfigurationCenter");
        }
        return result;
    }

    /// <inheritdoc />
    public bool? ShowQuickTestWizardDialog()
    {
        if (!Require(Permission.TestPlanCreate, "创建测试计划"))
        {
            return null;
        }

        var win = _serviceProvider.GetRequiredService<QuickTestWizardWindow>();
        TrySetOwner(win);

        // 向导通过 ConfigurationCreated 事件通知配置已落盘；窗口关闭后据此触发刷新。
        bool created = false;
        EventHandler? handler = (_, _) => created = true;
        win.ConfigurationCreated += handler;
        try
        {
            var result = win.ShowDialog();
            if (created)
            {
                RaiseApplied("QuickTestWizard");
            }
            return result;
        }
        finally
        {
            win.ConfigurationCreated -= handler;
        }
    }

    /// <inheritdoc />
    public bool? ShowTestPlanEditorDialog()
    {
        if (!Require(Permission.TestPlanEdit, "编辑测试计划"))
        {
            return null;
        }

        var win = _serviceProvider.GetRequiredService<TestPlanEditorWindow>();
        TrySetOwner(win);
        var result = win.ShowDialog();
        if (result == true)
        {
            RaiseApplied("TestPlanEditor");
        }
        return result;
    }

    /// <inheritdoc />
    public bool? ShowPluginManagerDialog()
    {
        if (!Require(Permission.SystemConfig, "管理插件"))
        {
            return null;
        }

        var win = new PluginManagementWindow(_dutMonitorManager);
        TrySetOwner(win);
        win.ShowDialog();
        // 插件管理窗口无 DialogResult 语义；关闭即视为完成，不触发 ConfigurationApplied。
        return null;
    }

    /// <inheritdoc />
    public bool? ShowDeviceManagerDialog()
    {
        if (!Require(Permission.DeviceManagement, "管理设备"))
        {
            return null;
        }

        var win = new DeviceManagementWindow(_permissionManager);
        TrySetOwner(win);
        win.ShowDialog();
        return null;
    }

    /// <inheritdoc />
    public bool? ShowUserManagerDialog()
    {
        if (!Require(Permission.UserManagement, "管理用户"))
        {
            return null;
        }

        var win = new UserManagementWindow(_permissionManager);
        TrySetOwner(win);
        win.ShowDialog();
        return null;
    }

    /// <inheritdoc />
    public bool? ShowTemplatePackPickerDialog()
    {
        // 与系统配置或测试计划管理任一权限一致即可打开。
        if (!_permissionManager.HasPermission(Permission.SystemConfig) &&
            !_permissionManager.HasPermission(Permission.TestPlanManagement))
        {
            _dialogService.ShowWarning("无权限：选择工艺包/模板");
            return null;
        }

        var win = _serviceProvider.GetRequiredService<TemplatePackPickerWindow>();
        TrySetOwner(win);
        var result = win.ShowDialog();
        if (result == true)
        {
            RaiseApplied("TemplatePackPicker");
        }
        return result;
    }

    private bool Require(Permission permission, string action)
    {
        if (_permissionManager.HasPermission(permission))
        {
            return true;
        }

        _dialogService.ShowWarning($"无权限：{action}");
        return false;
    }

    private void TrySetOwner(Window win)
    {
        var owner = _ownerResolver();
        if (owner is not null && owner != win)
        {
            win.Owner = owner;
        }
    }

    private void RaiseApplied(string source)
        => ConfigurationApplied?.Invoke(this, new WindowClosedEventArgs { Source = source });
}
