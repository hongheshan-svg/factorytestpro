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
        // 配置适配器
        services.AddSingleton<UTF.UI.Services.IConfigurationAdapter, UTF.UI.Services.ConfigurationAdapter>();

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

        // UI 管理器和服务
        services.AddSingleton<UTF.UI.Services.ConfigurationManager>();
        services.AddSingleton<IConfigurationService>(sp => sp.GetRequiredService<UTF.UI.Services.ConfigurationManager>());

        services.AddSingleton<UTF.UI.Services.DUTMonitorManager>();
        services.AddSingleton<IDUTMonitorService>(sp => sp.GetRequiredService<UTF.UI.Services.DUTMonitorManager>());

        // 权限管理器（来自 UTF.UI.Services）
        services.AddTransient<UTF.UI.Services.IPermissionManager, UTF.UI.Services.PermissionManager>();

        // 报告生成器
        services.AddSingleton<ReportGenerator>();

        // 配置驱动报告桥接
        services.AddTransient<ConfigDrivenReportBridge>();

        return services;
    }
}
