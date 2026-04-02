using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UTF.Configuration.Abstractions;

namespace UTF.Configuration;

public class FileConfigurationProvider<T> : IConfigurationProvider<T> where T : class
{
    private readonly string _filePath;
    private readonly IConfigurationSerializer _serializer;

    public FileConfigurationProvider(string filePath, IConfigurationSerializer serializer)
    {
        _filePath = filePath;
        _serializer = serializer;
    }

    public async Task<T> LoadAsync(CancellationToken ct = default)
    {
        var content = await File.ReadAllTextAsync(_filePath, ct);
        return await _serializer.DeserializeAsync<T>(content, ct)
            ?? throw new InvalidOperationException($"配置文件解析失败: {_filePath}");
    }

    public async Task SaveAsync(T config, CancellationToken ct = default)
    {
        var content = await _serializer.SerializeAsync(config, ct);
        await File.WriteAllTextAsync(_filePath, content, ct);
    }

    public Task<bool> ExistsAsync(CancellationToken ct = default)
    {
        return Task.FromResult(File.Exists(_filePath));
    }
}
