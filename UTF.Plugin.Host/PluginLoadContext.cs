using System.Reflection;
using System.Runtime.Loader;

namespace UTF.Plugin.Host;

internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginMainAssemblyPath)
        : base($"Plugin::{Path.GetFileNameWithoutExtension(pluginMainAssemblyPath)}::{Guid.NewGuid()}", isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginMainAssemblyPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // 与宿主共享 Abstractions，避免类型不一致
        if (string.Equals(assemblyName.Name, "UTF.Plugin.Abstractions", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath == null)
        {
            return null;
        }

        return LoadFromAssemblyPath(assemblyPath);
    }
}
