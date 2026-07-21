using System;
using System.Threading.Tasks;
using UTF.Configuration;
using UTF.Configuration.Models;
using UTF.Core;

namespace UTF.UI.Services;

/// <summary>
/// UI 侧配置入口：薄包装 <see cref="UnifiedConfigurationManager"/>，并实现 <see cref="IConfigurationService"/>。
/// 配置模型与 IO 真相位于 <c>UTF.Configuration</c>。
/// </summary>
/// <remarks>
/// Not sealed so UI unit tests can substitute with NSubstitute class proxies.
/// </remarks>
public class ConfigurationManager : IConfigurationService, IDisposable
{
    private readonly UnifiedConfigurationManager _manager;

    public event EventHandler? ConfigurationChanged
    {
        add => _manager.ConfigurationChanged += value;
        remove => _manager.ConfigurationChanged -= value;
    }

    public ConfigurationManager(IUnifiedConfigurationAdapter configAdapter, string? configDirectory = null)
    {
        _manager = new UnifiedConfigurationManager(configAdapter, configDirectory);
    }

    /// <summary>兼容旧 DI 签名（忽略 cache，内部自带缓存）。</summary>
    public ConfigurationManager(UTF.Core.Caching.ICache cache, IConfigurationAdapter configAdapter)
        : this((IUnifiedConfigurationAdapter)configAdapter, configDirectory: null)
    {
        _ = cache;
    }

    public UnifiedConfigurationManager Inner => _manager;

    public Task<UnifiedConfiguration> GetUnifiedConfigurationAsync() =>
        _manager.GetUnifiedConfigurationAsync();

    public Task<DUTConfiguration> GetDUTConfigurationAsync() =>
        _manager.GetDUTConfigurationAsync();

    public Task<TestProjectConfiguration> GetTestProjectConfigurationAsync() =>
        _manager.GetTestProjectConfigurationAsync();

    public Task SaveUnifiedConfigurationAsync(UnifiedConfiguration config) =>
        _manager.SaveUnifiedConfigurationAsync(config);

    public Task RefreshConfiguration() => _manager.RefreshAsync();

    public async Task<T?> GetConfigurationSectionAsync<T>(string section) where T : class
    {
        var unified = await _manager.GetUnifiedConfigurationAsync().ConfigureAwait(false);
        object? value = section switch
        {
            "DUTConfiguration" => unified.DUTConfiguration,
            "TestProjectConfiguration" => unified.TestProjectConfiguration,
            "UnifiedConfiguration" => unified,
            "SystemSettings" => unified.SystemSettings,
            "ConfigurationInfo" => unified.ConfigurationInfo,
            "UiProfile" => unified.UiProfile ?? UiProfile.CreateDefault(),
            _ => null
        };
        return value as T;
    }

    async Task IConfigurationService.SaveConfigurationAsync(object config)
    {
        if (config is UnifiedConfiguration unifiedConfig)
        {
            await SaveUnifiedConfigurationAsync(unifiedConfig).ConfigureAwait(false);
        }
    }

    Task IConfigurationService.RefreshAsync() => RefreshConfiguration();

    public void Dispose() => _manager.Dispose();
}
