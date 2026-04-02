using System.Threading;
using System.Threading.Tasks;

namespace UTF.Configuration.Abstractions;

/// <summary>
/// 配置提供者接口
/// </summary>
public interface IConfigurationProvider<T> where T : class
{
    Task<T> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(T config, CancellationToken ct = default);
    Task<bool> ExistsAsync(CancellationToken ct = default);
}
