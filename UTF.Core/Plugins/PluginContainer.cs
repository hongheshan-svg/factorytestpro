using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UTF.Logging;
using UTF.Plugin.Abstractions;

namespace UTF.Core;

public class PluginContainer : IPluginContainer
{
    private readonly Dictionary<string, IPlugin> _plugins = new();
    private readonly ILogger _logger;

    public PluginContainer(ILogger logger)
    {
        _logger = logger;
    }

    public void Register(IPlugin plugin)
    {
        _plugins[plugin.Metadata.PluginId] = plugin;
        _logger.Info($"插件已注册: {plugin.Metadata.Name} v{plugin.Metadata.Version}");
    }

    public T? GetPlugin<T>(string pluginId) where T : class, IPlugin
    {
        return _plugins.TryGetValue(pluginId, out var plugin) ? plugin as T : null;
    }

    public IEnumerable<T> GetPlugins<T>() where T : class, IPlugin
    {
        return _plugins.Values.OfType<T>();
    }

    /// <summary>
    /// 健康检查 - 基于已注册的插件元数据判断。
    /// 真正的活性探测应由插件自身实现（如 ping/握手）；此处仅校验插件已注册。
    /// </summary>
    public Task<bool> HealthCheckAsync(string pluginId, CancellationToken ct = default)
    {
        if (!_plugins.TryGetValue(pluginId, out var plugin))
            return Task.FromResult(false);

        // 插件已注册即视为通过元数据层检查；真实健康检查由插件侧负责
        return Task.FromResult(plugin.Metadata != null);
    }

    public IReadOnlyList<PluginMetadata> GetAllPlugins()
    {
        return _plugins.Values.Select(p => p.Metadata).ToList();
    }
}
