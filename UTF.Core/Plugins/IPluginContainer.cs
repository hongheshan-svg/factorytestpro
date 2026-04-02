using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UTF.Plugin.Abstractions;

namespace UTF.Core;

/// <summary>
/// 插件容器接口
/// </summary>
public interface IPluginContainer
{
    T? GetPlugin<T>(string pluginId) where T : class, IPlugin;
    IEnumerable<T> GetPlugins<T>() where T : class, IPlugin;
    Task<bool> HealthCheckAsync(string pluginId, CancellationToken ct = default);
    IReadOnlyList<PluginMetadata> GetAllPlugins();
}
