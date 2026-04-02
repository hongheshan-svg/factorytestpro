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

    public async Task<bool> HealthCheckAsync(string pluginId, CancellationToken ct = default)
    {
        if (!_plugins.TryGetValue(pluginId, out var plugin))
            return false;

        try
        {
            if (plugin is IStepExecutorPlugin executor)
                return executor.Metadata != null;
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"插件健康检查失败: {pluginId}", ex);
            return false;
        }
    }

    public IReadOnlyList<PluginMetadata> GetAllPlugins()
    {
        return _plugins.Values.Select(p => p.Metadata).ToList();
    }
}
