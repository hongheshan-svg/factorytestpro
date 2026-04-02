using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UTF.HAL;

/// <summary>
/// DUT状态枚举
/// </summary>
public enum DUTStatus
{
    /// <summary>未知</summary>
    Unknown,
    /// <summary>未上电</summary>
    PowerOff,
    /// <summary>上电中</summary>
    PoweringOn,
    /// <summary>启动中</summary>
    Booting,
    /// <summary>就绪</summary>
    Ready,
    /// <summary>测试中</summary>
    Testing,
    /// <summary>错误</summary>
    Error,
    /// <summary>维护模式</summary>
    Maintenance,
    /// <summary>固件升级中</summary>
    Upgrading,
    /// <summary>校准中</summary>
    Calibrating
}

/// <summary>
/// DUT类型枚举
/// </summary>
public enum DUTCategory
{
    /// <summary>手机/平板</summary>
    MobileDevice,
    /// <summary>笔记本电脑</summary>
    Laptop,
    /// <summary>台式机主板</summary>
    Motherboard,
    /// <summary>显卡</summary>
    GraphicsCard,
    /// <summary>电源模块</summary>
    PowerModule,
    /// <summary>汽车ECU</summary>
    AutomotiveECU,
    /// <summary>汽车传感器</summary>
    AutomotiveSensor,
    /// <summary>汽车执行器</summary>
    AutomotiveActuator,
    /// <summary>网络设备</summary>
    NetworkDevice,
    /// <summary>存储设备</summary>
    StorageDevice,
    /// <summary>显示设备</summary>
    DisplayDevice,
    /// <summary>音频设备</summary>
    AudioDevice,
    /// <summary>通用电子设备</summary>
    GenericDevice
}

/// <summary>
/// 测试项类型
/// </summary>
public enum TestItemType
{
    /// <summary>功能测试</summary>
    Functional,
    /// <summary>性能测试</summary>
    Performance,
    /// <summary>稳定性测试</summary>
    Stability,
    /// <summary>电气特性测试</summary>
    Electrical,
    /// <summary>环境测试</summary>
    Environmental,
    /// <summary>兼容性测试</summary>
    Compatibility,
    /// <summary>安全测试</summary>
    Safety,
    /// <summary>校准测试</summary>
    Calibration,
    /// <summary>老化测试</summary>
    BurnIn,
    /// <summary>质量测试</summary>
    Quality
}

/// <summary>
/// 测试结果
/// </summary>
public sealed record TestResult
{
    /// <summary>测试项名称</summary>
    public string TestName { get; init; } = string.Empty;
    
    /// <summary>测试类型</summary>
    public TestItemType Type { get; init; }
    
    /// <summary>是否通过</summary>
    public bool Passed { get; init; }
    
    /// <summary>测试值</summary>
    public object? MeasuredValue { get; init; }
    
    /// <summary>期望值</summary>
    public object? ExpectedValue { get; init; }
    
    /// <summary>最小值</summary>
    public object? MinValue { get; init; }
    
    /// <summary>最大值</summary>
    public object? MaxValue { get; init; }
    
    /// <summary>单位</summary>
    public string Unit { get; init; } = string.Empty;
    
    /// <summary>错误信息</summary>
    public string? ErrorMessage { get; init; }
    
    /// <summary>开始时间</summary>
    public DateTime StartTime { get; init; }
    
    /// <summary>结束时间</summary>
    public DateTime EndTime { get; init; }
    
    /// <summary>执行时间</summary>
    public TimeSpan Duration => EndTime - StartTime;
    
    /// <summary>操作员</summary>
    public string Operator { get; init; } = string.Empty;
    
    /// <summary>测试站台</summary>
    public string TestStation { get; init; } = string.Empty;
    
    /// <summary>扩展数据</summary>
    public Dictionary<string, object> ExtendedData { get; init; } = new();
}

/// <summary>
/// DUT信息
/// </summary>
public sealed record DUTInfo
{
    /// <summary>设备ID (便捷属性)</summary>
    public string DeviceId { get; init; } = string.Empty;
    
    /// <summary>设备名称 (便捷属性)</summary>
    public string Name { get; init; } = string.Empty;
    
    /// <summary>产品序列号</summary>
    public string SerialNumber { get; init; } = string.Empty;
    
    /// <summary>产品型号</summary>
    public string Model { get; init; } = string.Empty;
    
    /// <summary>产品分类</summary>
    public DUTCategory Category { get; init; }
    
    /// <summary>硬件版本</summary>
    public string HardwareVersion { get; init; } = string.Empty;
    
    /// <summary>固件版本</summary>
    public string FirmwareVersion { get; init; } = string.Empty;
    
    /// <summary>软件版本</summary>
    public string SoftwareVersion { get; init; } = string.Empty;
    
    /// <summary>MAC地址列表</summary>
    public List<string> MacAddresses { get; init; } = new();
    
    /// <summary>IMEI号</summary>
    public string? IMEI { get; init; }
    
    /// <summary>制造商</summary>
    public string Manufacturer { get; init; } = string.Empty;
    
    /// <summary>生产日期</summary>
    public DateTime? ManufactureDate { get; init; }
    
    /// <summary>当前状态</summary>
    public DUTStatus Status { get; init; }
    
    /// <summary>测试计数</summary>
    public int TestCount { get; init; }
    
    /// <summary>扩展属性</summary>
    public Dictionary<string, object> Properties { get; init; } = new();
}

/// <summary>
/// 电源控制结果
/// </summary>
public sealed record PowerControlResult
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }
    
    /// <summary>当前电压</summary>
    public double Voltage { get; init; }
    
    /// <summary>当前电流</summary>
    public double Current { get; init; }
    
    /// <summary>功率状态</summary>
    public bool PowerEnabled { get; init; }
    
    /// <summary>错误信息</summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// 待测设备(DUT)接口
/// </summary>
public interface IDUT : IDevice
{
    /// <summary>DUT信息</summary>
    DUTInfo DUTInfo { get; }
    
    /// <summary>测试结果事件</summary>
    event EventHandler<TestResult>? TestCompleted;
    
    /// <summary>DUT状态变化事件</summary>
    new event EventHandler<DUTStatus>? StatusChanged;
    
    /// <summary>上电</summary>
    Task<DeviceOperationResult> PowerOnAsync(double voltage = 12.0, double currentLimit = 5.0, CancellationToken cancellationToken = default);
    
    /// <summary>下电</summary>
    Task<DeviceOperationResult> PowerOffAsync(CancellationToken cancellationToken = default);
    
    /// <summary>软重启</summary>
    Task<DeviceOperationResult> SoftResetAsync(CancellationToken cancellationToken = default);
    
    /// <summary>硬重启</summary>
    Task<DeviceOperationResult> HardResetAsync(CancellationToken cancellationToken = default);
    
    /// <summary>进入测试模式</summary>
    Task<DeviceOperationResult> EnterTestModeAsync(string? testMode = null, CancellationToken cancellationToken = default);
    
    /// <summary>退出测试模式</summary>
    Task<DeviceOperationResult> ExitTestModeAsync(CancellationToken cancellationToken = default);
    
    /// <summary>执行测试项</summary>
    Task<DeviceOperationResult> ExecuteTestAsync(string testName, Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default);
    
    /// <summary>执行命令</summary>
    Task<DeviceOperationResult> ExecuteCommandAsync(string command, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
    
    /// <summary>读取寄存器</summary>
    Task<DeviceOperationResult> ReadRegisterAsync(uint address, int wordSize = 4, CancellationToken cancellationToken = default);
    
    /// <summary>写入寄存器</summary>
    Task<DeviceOperationResult> WriteRegisterAsync(uint address, uint value, int wordSize = 4, CancellationToken cancellationToken = default);
    
    /// <summary>读取内存</summary>
    Task<DeviceOperationResult> ReadMemoryAsync(uint address, int length, CancellationToken cancellationToken = default);
    
    /// <summary>写入内存</summary>
    Task<DeviceOperationResult> WriteMemoryAsync(uint address, byte[] data, CancellationToken cancellationToken = default);
    
    /// <summary>固件升级</summary>
    Task<DeviceOperationResult> FirmwareUpdateAsync(string firmwarePath, Action<int>? progressCallback = null, CancellationToken cancellationToken = default);
    
    /// <summary>获取DUT信息</summary>
    Task<DeviceOperationResult> GetDUTInfoAsync(CancellationToken cancellationToken = default);
    
    /// <summary>设置DUT参数</summary>
    Task<DeviceOperationResult> SetParameterAsync(string parameter, object value, CancellationToken cancellationToken = default);
    
    /// <summary>获取DUT参数</summary>
    Task<DeviceOperationResult> GetParameterAsync(string parameter, CancellationToken cancellationToken = default);
    
    /// <summary>校准DUT</summary>
    Task<DeviceOperationResult> CalibrateAsync(string calibrationType, Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default);
    
    /// <summary>启动老化测试</summary>
    Task<DeviceOperationResult> StartBurnInTestAsync(TimeSpan duration, Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default);
    
    /// <summary>停止老化测试</summary>
    Task<DeviceOperationResult> StopBurnInTestAsync(CancellationToken cancellationToken = default);
    
    /// <summary>获取温度</summary>
    Task<DeviceOperationResult> GetTemperatureAsync(string? sensor = null, CancellationToken cancellationToken = default);
    
    /// <summary>获取电源状态</summary>
    Task<DeviceOperationResult> GetPowerStatusAsync(CancellationToken cancellationToken = default);
}

