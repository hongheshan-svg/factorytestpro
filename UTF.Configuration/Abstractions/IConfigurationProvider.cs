using System.Threading;
using System.Threading.Tasks;

namespace UTF.Configuration.Abstractions;

/// <summary>
/// 文件配置提供者接口。
/// 重命名为 <see cref="IFileConfigurationProvider{TConfig}"/> 以避免与已移除的
/// Stack-A 非泛型 <c>IConfigurationProvider</c> 接口命名冲突。
/// </summary>
public interface IFileConfigurationProvider<TConfig> where TConfig : class
{
    /// <summary>从配置源加载配置实例。</summary>
    Task<TConfig> LoadAsync(CancellationToken ct = default);

    /// <summary>持久化配置实例到配置源。</summary>
    Task SaveAsync(TConfig config, CancellationToken ct = default);

    /// <summary>判断配置源是否存在。</summary>
    Task<bool> ExistsAsync(CancellationToken ct = default);
}
