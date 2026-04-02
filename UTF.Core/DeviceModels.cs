using System;
using System.Collections.Generic;

namespace UTF.Core
{
    /// <summary>
    /// 设备信息
    /// </summary>
    public class DeviceInfo
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public string Port { get; set; } = "";
        public string Status { get; set; } = "";
        public string Description { get; set; } = "";
        public string Manufacturer { get; set; } = "";
        public string Model { get; set; } = "";
        public string SerialNumber { get; set; } = "";
        public string FirmwareVersion { get; set; } = "";
        public DateTime LastSeen { get; set; } = DateTime.Now;
        public bool IsConnected { get; set; } = false;
        public Dictionary<string, object> Properties { get; set; } = new();
        public List<string> Capabilities { get; set; } = new();
        public DeviceCategory Category { get; set; } = DeviceCategory.Unknown;
    }

    /// <summary>
    /// 设备类别
    /// </summary>
    public enum DeviceCategory
    {
        Unknown,        // 未知
        DUT,           // 被测设备
        Instrument,    // 测试仪器
        PowerSupply,   // 电源
        Multimeter,    // 万用表
        Oscilloscope,  // 示波器
        SignalGenerator, // 信号发生器
        Network,       // 网络设备
        Communication  // 通信设备
    }

    /// <summary>
    /// 设备扫描结果
    /// </summary>
    public class DeviceScanResult
    {
        public List<DeviceInfo> FoundDevices { get; set; } = new();
        public TimeSpan ScanDuration { get; set; }
        public DateTime ScanTime { get; set; } = DateTime.Now;
        public string ScanMethod { get; set; } = "";
        public Dictionary<string, int> DeviceCountByType { get; set; } = new();
        public List<string> ScanErrors { get; set; } = new();
        public bool IsSuccessful { get; set; } = true;
    }

    /// <summary>
    /// 设备连接配置
    /// </summary>
    public class DeviceConnectionConfig
    {
        public string DeviceName { get; set; } = "";
        public string ConnectionType { get; set; } = "";
        public string ConnectionString { get; set; } = "";
        public int Timeout { get; set; } = 5000;
        public int RetryCount { get; set; } = 3;
        public bool AutoReconnect { get; set; } = true;
        public Dictionary<string, object> Settings { get; set; } = new();
    }

    /// <summary>
    /// 设备校准信息
    /// </summary>
    public class DeviceCalibrationInfo
    {
        public string DeviceName { get; set; } = "";
        public DateTime LastCalibrationDate { get; set; }
        public DateTime NextCalibrationDate { get; set; }
        public string CalibrationCertificate { get; set; } = "";
        public string CalibratedBy { get; set; } = "";
        public CalibrationStatus Status { get; set; } = CalibrationStatus.Unknown;
        public Dictionary<string, object> CalibrationData { get; set; } = new();
        public List<string> CalibrationNotes { get; set; } = new();
    }

    /// <summary>
    /// 校准状态
    /// </summary>
    public enum CalibrationStatus
    {
        Unknown,        // 未知
        Valid,          // 有效
        Expired,        // 过期
        DueSoon,        // 即将到期
        InProgress,     // 校准中
        Failed          // 校准失败
    }
}
