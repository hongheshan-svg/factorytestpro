using System.Reflection;
using System.Runtime.Loader;

namespace UTF.Plugin.Host;

internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    /// <summary>
    /// 跟踪卸载后的弱引用，便于宿主在卸载后探测上下文是否已实际回收（Unload 是异步的）。
    /// 调用方不应在卸载后继续复用本上下文。
    /// </summary>
    public WeakReference<PluginLoadContext> SelfReference { get; }

    public PluginLoadContext(string pluginMainAssemblyPath)
        : base($"Plugin::{Path.GetFileNameWithoutExtension(pluginMainAssemblyPath)}::{Guid.NewGuid()}", isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginMainAssemblyPath);
        SelfReference = new WeakReference<PluginLoadContext>(this);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var name = assemblyName.Name ?? string.Empty;

        // 共享框架程序集与 UTF 共享程序集一律由宿主统一加载（返回 null 走默认解析），
        // 避免插件私加载重复副本造成类型不一致。只有插件私有依赖才从插件目录加载。
        if (IsSharedAssembly(name))
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

    /// <summary>
    /// 判断程序集名是否属于宿主共享集（应跳过插件私加载）：
    /// - System.* / Microsoft.* 运行时与框架程序集
    /// - UTF.* 共享 UTF 程序集（Abstractions、Core、Logging 等，由宿主统一提供）
    /// </summary>
    private static bool IsSharedAssembly(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        return name.StartsWith("System.", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("UTF.", StringComparison.OrdinalIgnoreCase);
    }
}
