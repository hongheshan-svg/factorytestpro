using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UTF.Configuration.Models;

namespace UTF.Configuration;

/// <summary>
/// 统一配置服务：缓存 + 文件持久化。不依赖 UI / Core，可供 WPF 与 headless CLI 共用。
/// </summary>
public sealed class UnifiedConfigurationManager : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly string _configDirectory;
    private readonly string _configFilePath;
    private readonly IUnifiedConfigurationAdapter _configAdapter;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly object _cacheLock = new();
    private UnifiedConfiguration? _cached;
    private DateTime _cacheUntilUtc = DateTime.MinValue;
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(15);
    private bool _disposed;

    public event EventHandler? ConfigurationChanged;

    /// <param name="configDirectoryOrFile">
    /// 配置目录（内含 unified-config.json），或直接指向该文件的路径。
    /// 为 null 时使用 <c>AppDomain.BaseDirectory/config</c>。
    /// </param>
    public UnifiedConfigurationManager(
        IUnifiedConfigurationAdapter? configAdapter = null,
        string? configDirectoryOrFile = null)
    {
        _configAdapter = configAdapter ?? new UnifiedConfigurationAdapter();
        (_configDirectory, _configFilePath) = ResolvePaths(configDirectoryOrFile);

        if (!Directory.Exists(_configDirectory))
        {
            Directory.CreateDirectory(_configDirectory);
        }
    }

    public string ConfigDirectory => _configDirectory;
    public string ConfigFilePath => _configFilePath;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _fileLock.Dispose();
        GC.SuppressFinalize(this);
    }

    public async Task<UnifiedConfiguration> GetUnifiedConfigurationAsync()
    {
        lock (_cacheLock)
        {
            if (_cached != null && DateTime.UtcNow < _cacheUntilUtc)
            {
                return _cached;
            }
        }

        var config = await LoadUnifiedConfigurationInternalAsync().ConfigureAwait(false);
        lock (_cacheLock)
        {
            _cached = config;
            _cacheUntilUtc = DateTime.UtcNow.Add(CacheExpiration);
        }

        return config;
    }

    public async Task<DUTConfiguration> GetDUTConfigurationAsync()
    {
        var unified = await GetUnifiedConfigurationAsync().ConfigureAwait(false);
        return unified.DUTConfiguration;
    }

    public async Task<TestProjectConfiguration> GetTestProjectConfigurationAsync()
    {
        var unified = await GetUnifiedConfigurationAsync().ConfigureAwait(false);
        return unified.TestProjectConfiguration ?? new TestProjectConfiguration();
    }

    public async Task SaveUnifiedConfigurationAsync(UnifiedConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var errors = _configAdapter.ValidateConfigurationWithErrors(config);
        if (errors.Count > 0)
        {
            throw new InvalidDataException($"配置校验失败: {string.Join("; ", errors)}");
        }

        await _fileLock.WaitAsync().ConfigureAwait(false);
        var temporaryPath = string.Empty;
        try
        {
            temporaryPath = _configFilePath + ".tmp";
            var jsonContent = JsonSerializer.Serialize(config, JsonOptions);
            await File.WriteAllTextAsync(temporaryPath, jsonContent).ConfigureAwait(false);
            File.Move(temporaryPath, _configFilePath, overwrite: true);

            lock (_cacheLock)
            {
                _cached = config;
                _cacheUntilUtc = DateTime.UtcNow.Add(CacheExpiration);
            }

            ConfigurationChanged?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            if (!string.IsNullOrEmpty(temporaryPath) && File.Exists(temporaryPath))
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch
                {
                    // ignore cleanup failure
                }
            }

            _fileLock.Release();
        }
    }

    public Task RefreshAsync()
    {
        lock (_cacheLock)
        {
            _cached = null;
            _cacheUntilUtc = DateTime.MinValue;
        }

        return Task.CompletedTask;
    }

    public string GetConfigurationSummary(UnifiedConfiguration config) =>
        _configAdapter.GetConfigurationSummary(config);

    public IUnifiedConfigurationAdapter Adapter => _configAdapter;

    private async Task<UnifiedConfiguration> LoadUnifiedConfigurationInternalAsync()
    {
        try
        {
            if (File.Exists(_configFilePath))
            {
                var jsonContent = await File.ReadAllTextAsync(_configFilePath).ConfigureAwait(false);
                var config = JsonSerializer.Deserialize<UnifiedConfiguration>(jsonContent, JsonOptions);
                if (config != null)
                {
                    if (!_configAdapter.ValidateConfiguration(config))
                    {
                        var errors = _configAdapter.ValidateConfigurationWithErrors(config);
                        throw new InvalidDataException($"配置文件验证失败: {string.Join("; ", errors)}");
                    }

                    return config;
                }
            }
            else
            {
                var config = CreateDefaultConfiguration();
                await SaveUnifiedConfigurationAsync(config).ConfigureAwait(false);
                return config;
            }
        }
        catch (Exception ex) when (ex is not InvalidDataException)
        {
            System.Diagnostics.Debug.WriteLine($"加载配置失败: {ex.Message}");
            return CreateDefaultConfiguration();
        }

        return new UnifiedConfiguration();
    }

    private static UnifiedConfiguration CreateDefaultConfiguration()
    {
        return new UnifiedConfiguration
        {
            ConfigurationInfo = new ConfigurationInfo
            {
                Name = "默认配置",
                Version = "1.0.0",
                Description = "系统默认配置",
                Author = "UTF System",
                CreatedDate = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssZ")
            },
            SystemSettings = new SystemSettings(),
            DUTConfiguration = new DUTConfiguration
            {
                ProductInfo = new ProductInfo
                {
                    Name = "通用设备",
                    Model = "Generic",
                    Category = "通用设备"
                },
                GlobalSettings = new GlobalSettings
                {
                    DefaultMaxConcurrent = 16
                },
                CommunicationEndpoints = new CommunicationEndpoints
                {
                    SerialPorts = new List<string> { "COM3", "COM4", "COM5", "COM6" },
                    NetworkHosts = new List<string> { "192.168.1.10", "192.168.1.11" }
                },
                NamingConfig = new NamingConfig()
            },
            TestProjectConfiguration = new TestProjectConfiguration
            {
                TestMode = new TestMode
                {
                    Id = "production",
                    Name = "生产测试",
                    Icon = "🏭"
                },
                TestProject = new TestProject
                {
                    Id = "default_test",
                    Name = "默认测试项目",
                    Description = "默认测试流程",
                    Enabled = true,
                    Steps = new List<UnifiedTestStepConfig>()
                }
            }
        };
    }

    private static (string Directory, string FilePath) ResolvePaths(string? configDirectoryOrFile)
    {
        if (string.IsNullOrWhiteSpace(configDirectoryOrFile))
        {
            var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config");
            return (dir, Path.Combine(dir, "unified-config.json"));
        }

        var full = Path.GetFullPath(configDirectoryOrFile);
        if (File.Exists(full) || full.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            var dir = Path.GetDirectoryName(full) ?? AppDomain.CurrentDomain.BaseDirectory;
            return (dir, full);
        }

        return (full, Path.Combine(full, "unified-config.json"));
    }
}
