using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UTF.Configuration.Abstractions;

namespace UTF.Configuration;

public class JsonConfigurationSerializer : IConfigurationSerializer
{
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };

    public async Task<T?> DeserializeAsync<T>(string content, CancellationToken ct = default) where T : class
    {
        return JsonSerializer.Deserialize<T>(content, _options);
    }

    public async Task<string> SerializeAsync<T>(T config, CancellationToken ct = default) where T : class
    {
        return JsonSerializer.Serialize(config, _options);
    }
}
