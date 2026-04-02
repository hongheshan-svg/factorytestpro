using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UTF.Core;
using UTF.HAL;

namespace UTF.Business;

/// <summary>
/// 设备管理器接口
/// </summary>
public interface IDeviceManager
{
    /// <summary>注册的设备列表</summary>
    IReadOnlyList<IDevice> RegisteredDevices { get; }
    
    /// <summary>设备状态变化事件</summary>
    event EventHandler<DeviceEventArgs>? DeviceStatusChanged;
    
    /// <summary>初始化设备管理器</summary>
    Task<bool> InitializeAsync(CancellationToken cancellationToken = default);
    
    /// <summary>关闭设备管理器</summary>
    Task<bool> ShutdownAsync(CancellationToken cancellationToken = default);
    
    /// <summary>注册设备</summary>
    Task<bool> RegisterDeviceAsync(IDevice device, CancellationToken cancellationToken = default);
    
    /// <summary>注销设备</summary>
    Task<bool> UnregisterDeviceAsync(string deviceId, CancellationToken cancellationToken = default);
    
    /// <summary>获取设备</summary>
    Task<IDevice?> GetDeviceAsync(string deviceId, CancellationToken cancellationToken = default);
    
    /// <summary>获取设备by类型</summary>
    Task<List<IDevice>> GetDevicesByTypeAsync(DeviceType deviceType, CancellationToken cancellationToken = default);
    
    /// <summary>连接设备</summary>
    Task<DeviceOperationResult> ConnectDeviceAsync(string deviceId, CancellationToken cancellationToken = default);
    
    /// <summary>断开设备</summary>
    Task<DeviceOperationResult> DisconnectDeviceAsync(string deviceId, CancellationToken cancellationToken = default);
    
    /// <summary>连接所有设备</summary>
    Task<Dictionary<string, DeviceOperationResult>> ConnectAllDevicesAsync(CancellationToken cancellationToken = default);
    
    /// <summary>断开所有设备</summary>
    Task<Dictionary<string, DeviceOperationResult>> DisconnectAllDevicesAsync(CancellationToken cancellationToken = default);
    
    /// <summary>检查设备健康状态</summary>
    Task<Dictionary<string, DeviceOperationResult>> CheckDeviceHealthAsync(CancellationToken cancellationToken = default);
    
    /// <summary>获取可用设备</summary>
    Task<List<IDevice>> GetAvailableDevicesAsync(List<string>? requiredDeviceTypes = null, CancellationToken cancellationToken = default);
    
    /// <summary>分配设备</summary>
    Task<bool> AllocateDeviceAsync(string deviceId, string allocationId, CancellationToken cancellationToken = default);
    
    /// <summary>释放设备</summary>
    Task<bool> ReleaseDeviceAsync(string deviceId, string allocationId, CancellationToken cancellationToken = default);
    
    /// <summary>是否设备可用</summary>
    Task<bool> IsDeviceAvailableAsync(string deviceId, CancellationToken cancellationToken = default);
    
    /// <summary>重置设备</summary>
    Task<DeviceOperationResult> ResetDeviceAsync(string deviceId, CancellationToken cancellationToken = default);
    
    /// <summary>校准设备</summary>
    Task<DeviceOperationResult> CalibrateDeviceAsync(string deviceId, Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default);
    
    /// <summary>更新设备配置</summary>
    Task<bool> UpdateDeviceConfigurationAsync(string deviceId, Dictionary<string, object> configuration, CancellationToken cancellationToken = default);
    
    /// <summary>导入设备配置</summary>
    Task<bool> ImportDeviceConfigurationAsync(string configurationPath, CancellationToken cancellationToken = default);
    
    /// <summary>导出设备配置</summary>
    Task<bool> ExportDeviceConfigurationAsync(string exportPath, CancellationToken cancellationToken = default);
}

