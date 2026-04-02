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

        // 测试引擎（瞬态）
        services.AddTransient<ITestEngine, OptimizedTestEngine>();

        // 配置驱动测试引擎（瞬态）
        services.AddTransient<ConfigDrivenTestEngine>();

        // 配置驱动测试验证器（瞬态）
        services.AddTransient<ConfigDrivenTestValidator>();

        // 配置驱动测试编排器（瞬态）
        services.AddTransient<ConfigDrivenTestOrchestrator>();

        // 测试执行器（瞬态）
        services.AddTransient<ITestExecutor, TestExecutor>();

        // 测试验证器（瞬态）
        services.AddTransient<ITestValidator, TestValidator>();

        // 重试策略（瞬态）
        services.AddTransient<IRetryPolicy, ExponentialBackoffRetryPolicy>();

        // 测试编排器（瞬态）
        services.AddTransient<TestOrchestrator>();

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
