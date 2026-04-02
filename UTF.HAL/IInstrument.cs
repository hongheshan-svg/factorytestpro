using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UTF.HAL;

/// <summary>
/// 测量类型枚举
/// </summary>
public enum MeasurementType
{
    /// <summary>电压</summary>
    Voltage,
    /// <summary>电流</summary>
    Current,
    /// <summary>电阻</summary>
    Resistance,
    /// <summary>电容</summary>
    Capacitance,
    /// <summary>电感</summary>
    Inductance,
    /// <summary>频率</summary>
    Frequency,
    /// <summary>功率</summary>
    Power,
    /// <summary>温度</summary>
    Temperature,
    /// <summary>时间</summary>
    Time,
    /// <summary>波形</summary>
    Waveform,
    /// <summary>自定义</summary>
    Custom
}

/// <summary>
/// 测量结果
/// </summary>
public sealed record MeasurementResult
{
    /// <summary>测量类型</summary>
    public MeasurementType Type { get; init; }
    
    /// <summary>测量值</summary>
    public double Value { get; init; }
    
    /// <summary>单位</summary>
    public string Unit { get; init; } = string.Empty;
    
    /// <summary>精度</summary>
    public double Accuracy { get; init; }
    
    /// <summary>时间戳</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    
    /// <summary>测量通道</summary>
    public int Channel { get; init; }
    
    /// <summary>状态信息</summary>
    public string Status { get; init; } = string.Empty;
    
    /// <summary>扩展数据</summary>
    public Dictionary<string, object> ExtendedData { get; init; } = new();
}

/// <summary>
/// 波形数据
/// </summary>
public sealed record WaveformData
{
    /// <summary>采样点数据</summary>
    public double[] Samples { get; init; } = Array.Empty<double>();
    
    /// <summary>采样率 (Hz)</summary>
    public double SampleRate { get; init; }
    
    /// <summary>时间轴数据</summary>
    public double[] TimeAxis { get; init; } = Array.Empty<double>();
    
    /// <summary>触发位置</summary>
    public int TriggerPosition { get; init; }
    
    /// <summary>通道号</summary>
    public int Channel { get; init; }
    
    /// <summary>垂直比例 (V/div)</summary>
    public double VerticalScale { get; init; }
    
    /// <summary>水平比例 (s/div)</summary>
    public double HorizontalScale { get; init; }
    
    /// <summary>偏移量</summary>
    public double Offset { get; init; }
}

/// <summary>
/// 仪器配置参数
/// </summary>
public sealed record InstrumentConfig
{
    /// <summary>测量范围</summary>
    public double Range { get; init; }
    
    /// <summary>分辨率</summary>
    public double Resolution { get; init; }
    
    /// <summary>采样率</summary>
    public double SampleRate { get; init; }
    
    /// <summary>积分时间</summary>
    public double IntegrationTime { get; init; }
    
    /// <summary>触发模式</summary>
    public string TriggerMode { get; init; } = string.Empty;
    
    /// <summary>触发电平</summary>
    public double TriggerLevel { get; init; }
    
    /// <summary>自动量程</summary>
    public bool AutoRange { get; init; }
    
    /// <summary>扩展配置</summary>
    public Dictionary<string, object> ExtendedConfig { get; init; } = new();
}

/// <summary>
/// 测试仪器接口
/// </summary>
public interface IInstrument : IDevice
{
    /// <summary>支持的测量类型</summary>
    IReadOnlyList<MeasurementType> SupportedMeasurements { get; }
    
    /// <summary>通道数量</summary>
    int ChannelCount { get; }
    
    /// <summary>测量数据事件</summary>
    event EventHandler<MeasurementResult>? MeasurementCompleted;
    
    /// <summary>配置仪器</summary>
    Task<DeviceOperationResult> ConfigureInstrumentAsync(InstrumentConfig config, int channel = 1, CancellationToken cancellationToken = default);
    
    /// <summary>执行测量</summary>
    Task<DeviceOperationResult> MeasureAsync(MeasurementType type, int channel = 1, CancellationToken cancellationToken = default);
    
    /// <summary>开始连续测量</summary>
    Task<DeviceOperationResult> StartContinuousMeasurementAsync(MeasurementType type, int channel = 1, CancellationToken cancellationToken = default);
    
    /// <summary>停止连续测量</summary>
    Task<DeviceOperationResult> StopContinuousMeasurementAsync(int channel = 1, CancellationToken cancellationToken = default);
    
    /// <summary>设置输出</summary>
    Task<DeviceOperationResult> SetOutputAsync(int channel, double value, string unit = "V", CancellationToken cancellationToken = default);
    
    /// <summary>启用/禁用输出</summary>
    Task<DeviceOperationResult> EnableOutputAsync(int channel, bool enable, CancellationToken cancellationToken = default);
    
    /// <summary>获取测量范围</summary>
    Task<DeviceOperationResult> GetMeasurementRangeAsync(MeasurementType type, int channel = 1, CancellationToken cancellationToken = default);
    
    /// <summary>设置测量范围</summary>
    Task<DeviceOperationResult> SetMeasurementRangeAsync(MeasurementType type, double range, int channel = 1, CancellationToken cancellationToken = default);
}

