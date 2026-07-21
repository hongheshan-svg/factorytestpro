using Microsoft.Extensions.DependencyInjection;
using UTF.Core.Caching;
using UTF.Logging;

namespace UTF.Core.DependencyInjection;

/// <summary>
/// UTF 核心服务注册扩展
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册所有 UTF 核心服务
    /// </summary>
    public static IServiceCollection AddUtfCore(this IServiceCollection services)
    {
        // 缓存服务（单例）
        services.AddSingleton<ICache>(sp =>
            OptimizationKit.CreateStandardCache(maxItems: 10000, expiration: TimeSpan.FromMinutes(30)));

        // 日志服务（单例）
        services.AddSingleton<ILogger>(sp =>
            LoggerFactory.CreateLogger("GlobalLogger", new LogConfiguration
            {
                MinLevel = LogLevel.Info,
                EnableConsole = true,
                EnableFile = true,
                LogFilePath = "logs/utf-global.log"
            }));

        // 配置驱动测试引擎（单例 - 无状态且线程安全；被 Singleton 编排器与 DUTMonitorManager 复用，
        // 避免被 Singleton 捕获的 Transient 实例无法被 dispose 的 captive dependency 反模式）
        services.AddSingleton<ConfigDrivenTestEngine>();

        // 配置驱动测试验证器（瞬态）
        services.AddTransient<ConfigDrivenTestValidator>();

        // 配置驱动测试编排器（单例 - 持有共享会话状态）
        services.AddSingleton<ConfigDrivenTestOrchestrator>();

        // 重试策略（单例 - 被引擎复用）
        services.AddSingleton<IRetryPolicy, ExponentialBackoffRetryPolicy>();

        // 插件容器（单例）
        services.AddSingleton<IPluginContainer, PluginContainer>();

        // 事件总线（单例）
        services.AddSingleton<Events.IEventBus, Events.EventBus>();

        // 持久化层（单例）
        services.AddSingleton<Persistence.ITestResultRepository, Persistence.FileTestResultRepository>();
        services.AddSingleton<Persistence.IConfigurationAuditLog, Persistence.FileAuditLog>();

        return services;
    }
}
