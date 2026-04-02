using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UTF.HAL;

/// <summary>
/// 设备状态枚举
/// </summary>
public enum DeviceStatus
{
    /// <summary>未连接</summary>
    Disconnected,
    /// <summary>连接中</summary>
    Connecting,
    /// <summary>已连接</summary>
    Connected,
    /// <summary>工作中</summary>
    Working,
    /// <summary>错误</summary>
    Error,
    /// <summary>维护中</summary>
    Maintenance
}

/// <summary>
/// 设备类型枚举
/// </summary>
public enum DeviceType
{
    /// <summary>测试仪器</summary>
    Instrument,
    /// <summary>待测设备</summary>
    DUT,
    /// <summary>夹具</summary>
    Fixture,
    /// <summary>电源</summary>
    PowerSupply,
    /// <summary>负载</summary>
    Load,
    /// <summary>其他</summary>
    Other
}

/// <summary>
/// 设备能力描述
/// </summary>
public sealed record DeviceCapability
{
    /// <summary>能力名称</summary>
    public string Name { get; init; } = string.Empty;
    
    /// <summary>能力描述</summary>
    public string Description { get; init; } = string.Empty;
    
    /// <summary>参数列表</summary>
    public List<string> Parameters { get; init; } = new();
    
    /// <summary>返回值类型</summary>
    public string ReturnType { get; init; } = string.Empty;
}

/// <summary>
/// 设备信息
/// </summary>
public sealed record DeviceInfo
{
    /// <summary>设备ID</summary>
    public string DeviceId { get; init; } = string.Empty;
    
    /// <summary>设备名称</summary>
    public string Name { get; init; } = string.Empty;
    
    /// <summary>设备类型</summary>
    public DeviceType Type { get; init; }
    
    /// <summary>制造商</summary>
    public string Manufacturer { get; init; } = string.Empty;
    
    /// <summary>型号</summary>
    public string Model { get; init; } = string.Empty;
    
    /// <summary>序列号</summary>
    public string SerialNumber { get; init; } = string.Empty;
    
    /// <summary>固件版本</summary>
    public string FirmwareVersion { get; init; } = string.Empty;
    
    /// <summary>硬件版本</summary>
    public string HardwareVersion { get; init; } = string.Empty;
    
    /// <summary>当前状态</summary>
    public DeviceStatus Status { get; init; }
    
    /// <summary>最后错误信息</summary>
    public string? LastError { get; init; }
    
    /// <summary>设备能力列表</summary>
    public List<DeviceCapability> Capabilities { get; init; } = new();
    
    /// <summary>扩展属性</summary>
    public Dictionary<string, object> Properties { get; init; } = new();
}

/// <summary>
/// 设备操作结果
/// </summary>
public sealed record DeviceOperationResult
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }
    
    /// <summary>返回值</summary>
    public object? Value { get; init; }
    
    /// <summary>错误信息</summary>
    public string? ErrorMessage { get; init; }
    
    /// <summary>执行时间</summary>
    public TimeSpan ExecutionTime { get; init; }
    
    /// <summary>扩展数据</summary>
    public Dictionary<string, object> Data { get; init; } = new();
    
    /// <summary>创建成功结果</summary>
    public static DeviceOperationResult CreateSuccess(object? value = null, TimeSpan executionTime = default)
        => new() { Success = true, Value = value, ExecutionTime = executionTime };
    
    /// <summary>创建失败结果</summary>
    public static DeviceOperationResult CreateFailure(string errorMessage, TimeSpan executionTime = default)
        => new() { Success = false, ErrorMessage = errorMessage, ExecutionTime = executionTime };
}

/// <summary>
/// 设备事件参数
/// </summary>
public sealed class DeviceEventArgs : EventArgs
{
    public string DeviceId { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public object? Data { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// 设备连接状态
/// </summary>
public enum DeviceConnectionStatus
{
    /// <summary>未连接</summary>
    Disconnected,
    /// <summary>连接中</summary>
    Connecting,
    /// <summary>已连接</summary>
    Connected,
    /// <summary>断开中</summary>
    Disconnecting,
    /// <summary>错误</summary>
    Error
}

/// <summary>
/// 设备基础接口
/// </summary>
public interface IDevice : IDisposable
{
    /// <summary>设备信息</summary>
    DeviceInfo Info { get; }
    
    /// <summary>是否已连接</summary>
    bool IsConnected { get; }
    
    /// <summary>设备ID (便捷属性)</summary>
    string DeviceId => Info.DeviceId;
    
    /// <summary>设备名称 (便捷属性)</summary>
    string Name => Info.Name;
    
    /// <summary>设备类型 (便捷属性)</summary>
    DeviceType DeviceType => Info.Type;
    
    /// <summary>连接状态 (便捷属性)</summary>
    DeviceConnectionStatus ConnectionStatus => IsConnected ? DeviceConnectionStatus.Connected : DeviceConnectionStatus.Disconnected;
    
    /// <summary>设备状态变化事件</summary>
    event EventHandler<DeviceEventArgs>? StatusChanged;
    
    /// <summary>设备错误事件</summary>
    event EventHandler<DeviceEventArgs>? ErrorOccurred;
    
    /// <summary>设备数据事件</summary>
    event EventHandler<DeviceEventArgs>? DataReceived;
    
    /// <summary>连接设备</summary>
    Task<DeviceOperationResult> ConnectAsync(CancellationToken cancellationToken = default);
    
    /// <summary>断开连接</summary>
    Task<DeviceOperationResult> DisconnectAsync(CancellationToken cancellationToken = default);
    
    /// <summary>初始化设备</summary>
    Task<DeviceOperationResult> InitializeAsync(Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default);
    
    /// <summary>重置设备</summary>
    Task<DeviceOperationResult> ResetAsync(CancellationToken cancellationToken = default);
    
    /// <summary>执行命令</summary>
    Task<DeviceOperationResult> ExecuteCommandAsync(string command, Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default);
    
    /// <summary>读取数据</summary>
    Task<DeviceOperationResult> ReadDataAsync(string dataType, Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default);
    
    /// <summary>写入数据</summary>
    Task<DeviceOperationResult> WriteDataAsync(string dataType, object data, Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default);
    
    /// <summary>获取设备状态</summary>
    Task<DeviceOperationResult> GetStatusAsync(CancellationToken cancellationToken = default);
    
    /// <summary>配置设备</summary>
    Task<DeviceOperationResult> ConfigureAsync(Dictionary<string, object> configuration, CancellationToken cancellationToken = default);
    
    /// <summary>校准设备</summary>
    Task<DeviceOperationResult> CalibrateAsync(Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default);
    
    /// <summary>自检</summary>
    Task<DeviceOperationResult> SelfTestAsync(CancellationToken cancellationToken = default);
    
    /// <summary>执行自检 (别名方法)</summary>
    Task<DeviceOperationResult> PerformSelfTestAsync(CancellationToken cancellationToken = default) => SelfTestAsync(cancellationToken);
}
