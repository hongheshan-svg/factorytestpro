using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace UTF.Configuration;

/// <summary>
/// 动态配置提供者（支持热重载）
/// </summary>
public sealed class DynamicConfigurationProvider : IDisposable
{
    private readonly ConcurrentDictionary<string, object> _configurations = new();
    private readonly ConcurrentDictionary<string, FileSystemWatcher> _watchers = new();
    private readonly ConcurrentDictionary<string, DateTime> _lastModified = new();
    private readonly SemaphoreSlim _reloadSemaphore = new(1, 1);
    private bool _disposed = false;

    public event EventHandler<ConfigurationReloadedEventArgs>? ConfigurationReloaded;

    /// <summary>加载配置并启用监控</summary>
    public async Task<TConfig?> LoadConfigurationAsync<TConfig>(
        string filePath,
        Func<string, Task<TConfig>> deserializer,
        bool enableHotReload = true,
        CancellationToken cancellationToken = default) where TConfig : class
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentNullException(nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"配置文件不存在: {filePath}");

        try
        {
            var config = await deserializer(filePath);
            _configurations[filePath] = config;
            _lastModified[filePath] = File.GetLastWriteTimeUtc(filePath);

            if (enableHotReload)
            {
                StartMonitoring(filePath, deserializer);
            }

            return config;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"加载配置文件失败: {filePath}", ex);
        }
    }

    /// <summary>获取配置</summary>
    public TConfig? GetConfiguration<TConfig>(string filePath) where TConfig : class
    {
        if (_configurations.TryGetValue(filePath, out var config))
        {
            return config as TConfig;
        }
        return null;
    }

    /// <summary>更新配置</summary>
    public async Task<bool> UpdateConfigurationAsync<TConfig>(
        string filePath,
        TConfig configuration,
        Func<string, TConfig, Task> serializer,
        CancellationToken cancellationToken = default) where TConfig : class
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentNullException(nameof(filePath));

        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));

        try
        {
            // 停止监控
            StopMonitoring(filePath);

            // 保存配置
            await serializer(filePath, configuration);

            // 更新内存中的配置
            _configurations[filePath] = configuration;
            _lastModified[filePath] = DateTime.UtcNow;

            // 重新启动监控
            await Task.Delay(100, cancellationToken); // 等待文件系统稳定
            StartMonitoring<TConfig>(filePath, async path => await Task.FromResult(configuration));

            return true;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"更新配置文件失败: {filePath}", ex);
        }
    }

    /// <summary>刷新配置</summary>
    public async Task<bool> RefreshConfigurationAsync<TConfig>(
        string filePath,
        Func<string, Task<TConfig>> deserializer,
        CancellationToken cancellationToken = default) where TConfig : class
    {
        if (!File.Exists(filePath))
            return false;

        var lastModifiedTime = File.GetLastWriteTimeUtc(filePath);
        if (_lastModified.TryGetValue(filePath, out var cachedTime) && lastModifiedTime <= cachedTime)
        {
            return false; // 文件未修改
        }

        await _reloadSemaphore.WaitAsync(cancellationToken);
        try
        {
            var config = await deserializer(filePath);
            _configurations[filePath] = config;
            _lastModified[filePath] = lastModifiedTime;

            ConfigurationReloaded?.Invoke(this, new ConfigurationReloadedEventArgs
            {
                FilePath = filePath,
                ReloadTime = DateTime.UtcNow
            });

            return true;
        }
        catch (Exception ex)
        {
            // 记录错误但不抛出异常
            Console.WriteLine($"刷新配置失败: {filePath}, 错误: {ex.Message}");
            return false;
        }
        finally
        {
            _reloadSemaphore.Release();
        }
    }

    private void StartMonitoring<TConfig>(string filePath, Func<string, Task<TConfig>> deserializer) where TConfig : class
    {
        if (_watchers.ContainsKey(filePath))
            return;

        var directory = Path.GetDirectoryName(filePath) ?? throw new InvalidOperationException("无法获取目录路径");
        var fileName = Path.GetFileName(filePath);

        var watcher = new FileSystemWatcher(directory, fileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };

        watcher.Changed += async (sender, e) =>
        {
            // 延迟处理，避免多次触发
            await Task.Delay(500);
            await RefreshConfigurationAsync(filePath, deserializer);
        };

        _watchers[filePath] = watcher;
    }

    private void StopMonitoring(string filePath)
    {
        if (_watchers.TryRemove(filePath, out var watcher))
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }
    }

    /// <summary>停止所有监控</summary>
    public void StopAllMonitoring()
    {
        foreach (var watcher in _watchers.Values)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }
        _watchers.Clear();
    }

    /// <summary>获取所有配置文件路径</summary>
    public IEnumerable<string> GetAllConfigurationPaths()
    {
        return _configurations.Keys.ToList();
    }

    /// <summary>清除所有配置</summary>
    public void ClearAll()
    {
        StopAllMonitoring();
        _configurations.Clear();
        _lastModified.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;

        StopAllMonitoring();
        _reloadSemaphore?.Dispose();
        _configurations.Clear();
        _lastModified.Clear();

        _disposed = true;
    }
}

/// <summary>
/// 配置重载事件参数
/// </summary>
public sealed class ConfigurationReloadedEventArgs : EventArgs
{
    /// <summary>配置文件路径</summary>
    public string FilePath { get; init; } = string.Empty;
    
    /// <summary>重载时间</summary>
    public DateTime ReloadTime { get; init; }
}

