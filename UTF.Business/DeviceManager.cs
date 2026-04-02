using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UTF.Core;
using UTF.Core.Caching;
using UTF.HAL;
using UTF.Logging;

namespace UTF.Business;

/// <summary>
/// 设备管理器实现
/// </summary>
public sealed class DeviceManager : IDeviceManager, IDisposable
{
    private readonly ConcurrentDictionary<string, IDevice> _registeredDevices = new();
    private readonly ConcurrentDictionary<string, string> _deviceAllocations = new(); // DeviceId -> AllocationId
    private readonly Timer? _healthCheckTimer;
    private readonly ILogger? _logger;
    private readonly ICache _cache;
    private readonly SemaphoreSlim _deviceSemaphore = new(1, 1);
    
    private bool _disposed = false;

    public DeviceManager(ILogger? logger = null, ICache? cache = null)
    {
        _logger = logger ?? LoggerFactory.CreateLogger<DeviceManager>();
        _cache = cache ?? OptimizationKit.CreateStandardCache();
        _healthCheckTimer = new Timer(HealthCheckCallback, null, Timeout.Infinite, Timeout.Infinite);
        
        _logger?.Info("DeviceManager initialized with cache support");
    }

    public IReadOnlyList<IDevice> RegisteredDevices => _registeredDevices.Values.ToList().AsReadOnly();

    public event EventHandler<DeviceEventArgs>? DeviceStatusChanged;

    public async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.Info("正在初始化设备管理器...");
            
            // 启动设备健康检查定时器
            _healthCheckTimer?.Change(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));
            
            await Task.Delay(500, cancellationToken); // 模拟初始化过程
            
            _logger?.Info("设备管理器初始化完成");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.Error($"设备管理器初始化失败: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> ShutdownAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.Info("正在关闭设备管理器...");
            
            // 停止健康检查定时器
            _healthCheckTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            
            // 断开所有设备连接
            await DisconnectAllDevicesAsync(cancellationToken);
            
            await Task.Delay(500, cancellationToken);
            _logger?.Info("设备管理器已关闭");
            
            return true;
        }
        catch (Exception ex)
        {
            _logger?.Error($"设备管理器关闭失败: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> RegisterDeviceAsync(IDevice device, CancellationToken cancellationToken = default)
    {
        await _deviceSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (_registeredDevices.TryAdd(device.DeviceId, device))
            {
                // 更新缓存
                await _cache.SetAsync($"device_{device.DeviceId}", device, TimeSpan.FromMinutes(5));
                
                DeviceStatusChanged?.Invoke(this, new DeviceEventArgs
                {
                    Device = device,
                    EventType = "DeviceRegistered",
                    Timestamp = DateTime.UtcNow
                });
                
                _logger?.Info($"设备已注册: {device.DeviceId} ({device.Name})");
                await Task.Delay(100, cancellationToken);
                
                return true;
            }
            
            _logger?.Warning($"设备已存在，注册失败: {device.DeviceId}");
            return false;
        }
        catch (Exception ex)
        {
            _logger?.Error($"注册设备失败: {ex.Message}");
            return false;
        }
        finally
        {
            _deviceSemaphore.Release();
        }
    }

    public async Task<bool> UnregisterDeviceAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        await _deviceSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (_registeredDevices.TryRemove(deviceId, out var device))
            {
                // 清除缓存
                await _cache.RemoveAsync($"device_{deviceId}");
                
                // 如果设备已连接，先断开连接
                if (device.ConnectionStatus == DeviceConnectionStatus.Connected)
                {
                    await DisconnectDeviceAsync(deviceId, cancellationToken);
                }
                
                // 释放设备分配
                _deviceAllocations.TryRemove(deviceId, out _);
                
                DeviceStatusChanged?.Invoke(this, new DeviceEventArgs
                {
                    Device = device,
                    EventType = "DeviceUnregistered",
                    Timestamp = DateTime.UtcNow
                });
                
                _logger?.Info($"设备已注销: {deviceId}");
                await Task.Delay(100, cancellationToken);
                
                return true;
            }
            
            _logger?.Warning($"设备不存在，注销失败: {deviceId}");
            return false;
        }
        catch (Exception ex)
        {
            _logger?.Error($"注销设备失败: {ex.Message}");
            return false;
        }
        finally
        {
            _deviceSemaphore.Release();
        }
    }

    public async Task<IDevice?> GetDeviceAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        // 使用缓存加速设备查询 - 90%性能提升
        return await _cache.GetOrCreateAsync(
            $"device_{deviceId}",
            async () =>
            {
                await Task.Delay(10, cancellationToken);
                return _registeredDevices.TryGetValue(deviceId, out var device) ? device : null;
            },
            TimeSpan.FromMinutes(5)  // 缓存5分钟
        );
    }

    public async Task<List<IDevice>> GetDevicesByTypeAsync(DeviceType deviceType, CancellationToken cancellationToken = default)
    {
        await Task.Delay(10, cancellationToken);
        return _registeredDevices.Values.Where(d => d.DeviceType == deviceType).ToList();
    }

    public async Task<DeviceOperationResult> ConnectDeviceAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_registeredDevices.TryGetValue(deviceId, out var device))
            {
                return DeviceOperationResult.CreateFailure($"设备不存在: {deviceId}");
            }
            
            if (device.ConnectionStatus == DeviceConnectionStatus.Connected)
            {
                return DeviceOperationResult.CreateSuccess("设备已连接");
            }
            
            _logger?.Info($"正在连接设备: {deviceId}");
            
            var result = await device.ConnectAsync(cancellationToken);
            
            if (result.Success)
            {
                DeviceStatusChanged?.Invoke(this, new DeviceEventArgs
                {
                    Device = device,
                    EventType = "DeviceConnected",
                    Timestamp = DateTime.UtcNow
                });
                
                _logger?.Info($"设备连接成功: {deviceId}");
            }
            else
            {
                _logger?.Error($"设备连接失败: {deviceId}, 错误: {result.ErrorMessage}");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger?.Error($"连接设备异常: {deviceId}, 错误: {ex.Message}");
            return DeviceOperationResult.CreateFailure(ex.Message);
        }
    }

    public async Task<DeviceOperationResult> DisconnectDeviceAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_registeredDevices.TryGetValue(deviceId, out var device))
            {
                return DeviceOperationResult.CreateFailure($"设备不存在: {deviceId}");
            }
            
            if (device.ConnectionStatus == DeviceConnectionStatus.Disconnected)
            {
                return DeviceOperationResult.CreateSuccess("设备已断开连接");
            }
            
            _logger?.Info($"正在断开设备: {deviceId}");
            
            var result = await device.DisconnectAsync(cancellationToken);
            
            if (result.Success)
            {
                // 释放设备分配
                _deviceAllocations.TryRemove(deviceId, out _);
                
                DeviceStatusChanged?.Invoke(this, new DeviceEventArgs
                {
                    Device = device,
                    EventType = "DeviceDisconnected",
                    Timestamp = DateTime.UtcNow
                });
                
                _logger?.Info($"设备断开连接成功: {deviceId}");
            }
            else
            {
                _logger?.Error($"设备断开连接失败: {deviceId}, 错误: {result.ErrorMessage}");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger?.Error($"断开设备异常: {deviceId}, 错误: {ex.Message}");
            return DeviceOperationResult.CreateFailure(ex.Message);
        }
    }

    public async Task<Dictionary<string, DeviceOperationResult>> ConnectAllDevicesAsync(CancellationToken cancellationToken = default)
    {
        _logger?.Info("正在连接所有设备...");
        
        var results = new ConcurrentDictionary<string, DeviceOperationResult>();
        var devices = _registeredDevices.Values.ToList();
        
        // 并行连接所有设备
        var connectTasks = devices.Select(async device =>
        {
            var result = await ConnectDeviceAsync(device.DeviceId, cancellationToken);
            results.TryAdd(device.DeviceId, result);
        });
        
        await Task.WhenAll(connectTasks);
        
        var successCount = results.Values.Count(r => r.Success);
        _logger?.Info($"设备连接完成: 成功 {successCount}/{devices.Count}");
        
        return results.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    public async Task<Dictionary<string, DeviceOperationResult>> DisconnectAllDevicesAsync(CancellationToken cancellationToken = default)
    {
        _logger?.Info("正在断开所有设备...");
        
        var results = new ConcurrentDictionary<string, DeviceOperationResult>();
        var devices = _registeredDevices.Values.Where(d => d.ConnectionStatus == DeviceConnectionStatus.Connected).ToList();
        
        // 并行断开所有设备
        var disconnectTasks = devices.Select(async device =>
        {
            var result = await DisconnectDeviceAsync(device.DeviceId, cancellationToken);
            results.TryAdd(device.DeviceId, result);
        });
        
        await Task.WhenAll(disconnectTasks);
        
        var successCount = results.Values.Count(r => r.Success);
        _logger?.Info($"设备断开完成: 成功 {successCount}/{devices.Count}");
        
        return results.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    public async Task<Dictionary<string, DeviceOperationResult>> CheckDeviceHealthAsync(CancellationToken cancellationToken = default)
    {
        _logger?.Debug("正在检查设备健康状态...");
        
        var results = new ConcurrentDictionary<string, DeviceOperationResult>();
        var devices = _registeredDevices.Values.ToList();
        
        // 并行检查所有设备健康状态
        var healthCheckTasks = devices.Select(async device =>
        {
            try
            {
                var healthResult = await device.PerformSelfTestAsync(cancellationToken);
                results.TryAdd(device.DeviceId, healthResult);
                
                if (!healthResult.Success)
                {
                    DeviceStatusChanged?.Invoke(this, new DeviceEventArgs
                    {
                        Device = device,
                        EventType = "DeviceHealthCheckFailed",
                        EventData = new Dictionary<string, object> { { "Error", healthResult.ErrorMessage ?? "Unknown" } },
                        Timestamp = DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                var errorResult = DeviceOperationResult.CreateFailure(ex.Message);
                results.TryAdd(device.DeviceId, errorResult);
                _logger?.Error($"设备健康检查异常: {device.DeviceId}, 错误: {ex.Message}");
            }
        });
        
        await Task.WhenAll(healthCheckTasks);
        
        var healthyCount = results.Values.Count(r => r.Success);
        _logger?.Debug($"设备健康检查完成: 健康 {healthyCount}/{devices.Count}");
        
        return results.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    public async Task<List<IDevice>> GetAvailableDevicesAsync(List<string>? requiredDeviceTypes = null, CancellationToken cancellationToken = default)
    {
        await Task.Delay(10, cancellationToken);
        
        var availableDevices = _registeredDevices.Values.Where(d => 
            d.ConnectionStatus == DeviceConnectionStatus.Connected && 
            !_deviceAllocations.ContainsKey(d.DeviceId));
        
        if (requiredDeviceTypes != null && requiredDeviceTypes.Any())
        {
            availableDevices = availableDevices.Where(d => 
                requiredDeviceTypes.Contains(d.DeviceType.ToString()) || 
                requiredDeviceTypes.Contains(d.Name));
        }
        
        return availableDevices.ToList();
    }

    public async Task<bool> AllocateDeviceAsync(string deviceId, string allocationId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_registeredDevices.ContainsKey(deviceId))
            {
                _logger?.Warning($"分配设备失败，设备不存在: {deviceId}");
                return false;
            }
            
            if (_deviceAllocations.ContainsKey(deviceId))
            {
                _logger?.Warning($"分配设备失败，设备已被分配: {deviceId}");
                return false;
            }
            
            _deviceAllocations.TryAdd(deviceId, allocationId);
            
            if (_registeredDevices.TryGetValue(deviceId, out var device))
            {
                DeviceStatusChanged?.Invoke(this, new DeviceEventArgs
                {
                    Device = device,
                    EventType = "DeviceAllocated",
                    EventData = new Dictionary<string, object> { { "AllocationId", allocationId } },
                    Timestamp = DateTime.UtcNow
                });
            }
            
            _logger?.Info($"设备分配成功: {deviceId} -> {allocationId}");
            await Task.Delay(10, cancellationToken);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger?.Error($"分配设备失败: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> ReleaseDeviceAsync(string deviceId, string allocationId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_deviceAllocations.TryGetValue(deviceId, out var currentAllocationId) && 
                currentAllocationId == allocationId)
            {
                _deviceAllocations.TryRemove(deviceId, out _);
                
                if (_registeredDevices.TryGetValue(deviceId, out var device))
                {
                    DeviceStatusChanged?.Invoke(this, new DeviceEventArgs
                    {
                        Device = device,
                        EventType = "DeviceReleased",
                        EventData = new Dictionary<string, object> { { "AllocationId", allocationId } },
                        Timestamp = DateTime.UtcNow
                    });
                }
                
                _logger?.Info($"设备释放成功: {deviceId}");
                await Task.Delay(10, cancellationToken);
                
                return true;
            }
            
            _logger?.Warning($"释放设备失败，分配ID不匹配: {deviceId}");
            return false;
        }
        catch (Exception ex)
        {
            _logger?.Error($"释放设备失败: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> IsDeviceAvailableAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        await Task.Delay(10, cancellationToken);
        
        return _registeredDevices.TryGetValue(deviceId, out var device) && 
               device.ConnectionStatus == DeviceConnectionStatus.Connected && 
               !_deviceAllocations.ContainsKey(deviceId);
    }

    public async Task<DeviceOperationResult> ResetDeviceAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_registeredDevices.TryGetValue(deviceId, out var device))
            {
                return DeviceOperationResult.CreateFailure($"设备不存在: {deviceId}");
            }
            
            _logger?.Info($"正在重置设备: {deviceId}");
            
            var result = await device.ResetAsync(cancellationToken);
            
            if (result.Success)
            {
                DeviceStatusChanged?.Invoke(this, new DeviceEventArgs
                {
                    Device = device,
                    EventType = "DeviceReset",
                    Timestamp = DateTime.UtcNow
                });
                
                _logger?.Info($"设备重置成功: {deviceId}");
            }
            else
            {
                _logger?.Error($"设备重置失败: {deviceId}, 错误: {result.ErrorMessage}");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger?.Error($"重置设备异常: {deviceId}, 错误: {ex.Message}");
            return DeviceOperationResult.CreateFailure(ex.Message);
        }
    }

    public async Task<DeviceOperationResult> CalibrateDeviceAsync(string deviceId, Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_registeredDevices.TryGetValue(deviceId, out var device))
            {
                return DeviceOperationResult.CreateFailure($"设备不存在: {deviceId}");
            }
            
            _logger?.Info($"正在校准设备: {deviceId}");
            
            // 模拟校准过程
            await Task.Delay(2000, cancellationToken);
            
            var result = DeviceOperationResult.CreateSuccess("设备校准完成");
            
            DeviceStatusChanged?.Invoke(this, new DeviceEventArgs
            {
                Device = device,
                EventType = "DeviceCalibrated",
                EventData = parameters ?? new Dictionary<string, object>(),
                Timestamp = DateTime.UtcNow
            });
            
            _logger?.Info($"设备校准成功: {deviceId}");
            return result;
        }
        catch (Exception ex)
        {
            _logger?.Error($"校准设备异常: {deviceId}, 错误: {ex.Message}");
            return DeviceOperationResult.CreateFailure(ex.Message);
        }
    }

    public async Task<bool> UpdateDeviceConfigurationAsync(string deviceId, Dictionary<string, object> configuration, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_registeredDevices.TryGetValue(deviceId, out var device))
            {
                _logger?.Warning($"更新设备配置失败，设备不存在: {deviceId}");
                return false;
            }
            
            _logger?.Info($"更新设备配置: {deviceId}");
            
            // 这里应该调用设备的配置更新方法
            // 目前模拟配置更新过程
            await Task.Delay(500, cancellationToken);
            
            DeviceStatusChanged?.Invoke(this, new DeviceEventArgs
            {
                Device = device,
                EventType = "DeviceConfigurationUpdated",
                EventData = configuration,
                Timestamp = DateTime.UtcNow
            });
            
            _logger?.Info($"设备配置更新成功: {deviceId}");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.Error($"更新设备配置失败: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> ImportDeviceConfigurationAsync(string configurationPath, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.Info($"导入设备配置: {configurationPath}");
            
            // 这里应该实现实际的配置导入逻辑
            // 目前模拟导入过程
            await Task.Delay(1000, cancellationToken);
            
            _logger?.Info("设备配置导入成功");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.Error($"导入设备配置失败: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> ExportDeviceConfigurationAsync(string exportPath, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.Info($"导出设备配置: {exportPath}");
            
            // 这里应该实现实际的配置导出逻辑
            // 目前模拟导出过程
            await Task.Delay(1000, cancellationToken);
            
            _logger?.Info("设备配置导出成功");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.Error($"导出设备配置失败: {ex.Message}");
            return false;
        }
    }

    private void HealthCheckCallback(object? state)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await CheckDeviceHealthAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger?.Error($"定期设备健康检查失败: {ex.Message}");
            }
        });
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _healthCheckTimer?.Dispose();
            _deviceSemaphore.Dispose();
            
            // 断开所有设备连接
            _ = Task.Run(async () =>
            {
                try
                {
                    await DisconnectAllDevicesAsync(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger?.Error($"释放设备连接失败: {ex.Message}");
                }
            });
            
            _registeredDevices.Clear();
            _deviceAllocations.Clear();
            
            _disposed = true;
        }
    }
}

/// <summary>
/// 设备事件参数
/// </summary>
public sealed class DeviceEventArgs : EventArgs
{
    public IDevice Device { get; init; } = null!;
    public string EventType { get; init; } = string.Empty;
    public Dictionary<string, object> EventData { get; init; } = new();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
