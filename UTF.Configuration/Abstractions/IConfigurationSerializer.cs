using System.Threading;
using System.Threading.Tasks;

namespace UTF.Configuration.Abstractions;

/// <summary>
/// 配置序列化器接口
/// </summary>
public interface IConfigurationSerializer
{
    Task<T?> DeserializeAsync<T>(string content, CancellationToken ct = default) where T : class;
    Task<string> SerializeAsync<T>(T config, CancellationToken ct = default) where T : class;
}
