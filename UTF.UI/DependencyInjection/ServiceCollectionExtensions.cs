using Microsoft.Extensions.DependencyInjection;
using UTF.Core;
using UTF.Reporting;
using UTF.UI.Services;

namespace UTF.UI.DependencyInjection;

/// <summary>
/// UTF UI 和业务层服务注册扩展
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册 UTF 业务服务
    /// </summary>
    public static IServiceCollection AddUtfBusiness(this IServiceCollection services)
    {
        // 业务层服务
        services.AddSingleton<UTF.Business.IDeviceManager, UTF.Business.DeviceManager>();

        return services;
    }

    /// <summary>
    /// 注册 UTF UI 服务
    /// </summary>
    public static IServiceCollection AddUtfUI(this IServiceCollection services)
    {
        // 配置：模型/IO 在 UTF.Configuration；UI 注册兼容包装
        services.AddSingleton<UTF.Configuration.IUnifiedConfigurationAdapter, UTF.UI.Services.ConfigurationAdapter>();
        services.AddSingleton<UTF.UI.Services.IConfigurationAdapter>(sp =>
            (UTF.UI.Services.IConfigurationAdapter)sp.GetRequiredService<UTF.Configuration.IUnifiedConfigurationAdapter>());

        // 插件主机
        services.AddSingleton<UTF.Plugin.Host.StepExecutorPluginHost>(sp =>
        {
            var pluginRoot = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");
            return new UTF.Plugin.Host.StepExecutorPluginHost(pluginRoot);
        });

        // 插件服务
        services.AddSingleton<IPluginService>(sp =>
        {
            var pluginHost = sp.GetRequiredService<UTF.Plugin.Host.StepExecutorPluginHost>();
            return new UTF.Plugin.Host.PluginServiceAdapter(pluginHost);
        });

        // Plugin capability queries (step types / channels / parameterSchema) for Configuration Center
        services.AddSingleton<IPluginCapabilityService>(sp =>
        {
            var pluginHost = sp.GetRequiredService<UTF.Plugin.Host.StepExecutorPluginHost>();
            return new UTF.Plugin.Host.PluginCapabilityService(pluginHost);
        });

        services.AddSingleton<UTF.UI.Services.ConfigurationManager>(sp =>
            new UTF.UI.Services.ConfigurationManager(
                sp.GetRequiredService<UTF.Configuration.IUnifiedConfigurationAdapter>()));
        services.AddSingleton<IConfigurationService>(sp => sp.GetRequiredService<UTF.UI.Services.ConfigurationManager>());

        // DUTMonitorManager projects onto ConfigDrivenTestOrchestrator (single session entry).
        services.AddSingleton<UTF.UI.Services.DUTMonitorManager>();
        services.AddSingleton<IDUTMonitorService>(sp => sp.GetRequiredService<UTF.UI.Services.DUTMonitorManager>());

        // 权限管理器（单例 - 持有用户状态）
        services.AddSingleton<UTF.UI.Services.IPermissionManager, UTF.UI.Services.PermissionManager>();

        // 报告生成器
        services.AddSingleton<ReportGenerator>();

        // 配置驱动报告桥接
        services.AddTransient<ConfigDrivenReportBridge>();

        // 步骤执行服务（ConfigDrivenTestEngine 直接实现 IStepExecutionService）
        services.AddSingleton<UTF.Core.IStepExecutionService>(sp => sp.GetRequiredService<UTF.Core.ConfigDrivenTestEngine>());

        // 对话框与窗口工厂抽象
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IWindowFactory>(sp => new WindowFactory(
            sp.GetRequiredService<IServiceProvider>(),
            sp.GetRequiredService<IDialogService>(),
            sp.GetRequiredService<IPermissionManager>(),
            sp.GetRequiredService<DUTMonitorManager>(),
            () => System.Windows.Application.Current?.MainWindow as System.Windows.Window));

        // 测试配置构建器（快速创建向导使用）
        services.AddSingleton<ITestConfigurationBuilder, TestConfigurationBuilder>();

        // 工艺包/模板目录服务（扫描 config/templates；不注入 path 覆盖参数）
        services.AddSingleton<ITemplatePackService>(sp =>
            new TemplatePackService(
                sp.GetRequiredService<ConfigurationManager>(),
                sp.GetService<UTF.Logging.ILogger>()));

        // 视图模型（瞬态 - 每次打开窗口获得新实例）
        services.AddTransient<UTF.UI.ViewModels.MainWindowViewModel>();
        services.AddTransient<UTF.UI.ViewModels.ConfigurationCenterViewModel>();
        services.AddTransient<UTF.UI.ViewModels.QuickTestWizardViewModel>();
        services.AddTransient<UTF.UI.ViewModels.TemplatePackPickerViewModel>();

        return services;
    }
}
