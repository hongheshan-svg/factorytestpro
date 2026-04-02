using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UTF.UI.Services
{
    /// <summary>
    /// 通用统一配置模型 - 支持所有DUT产品
    /// </summary>
    public class UnifiedConfiguration
    {
        public ConfigurationInfo ConfigurationInfo { get; set; } = new();
        public SystemSettings SystemSettings { get; set; } = new();
        public DUTConfiguration DUTConfiguration { get; set; } = new();
        public TestProjectConfiguration? TestProjectConfiguration { get; set; }
    }

    /// <summary>
    /// 配置文件基本信息
    /// </summary>
    public class ConfigurationInfo
    {
        public string Name { get; set; } = "";
        public string Version { get; set; } = "";
        public string CreatedDate { get; set; } = "";
        public string Description { get; set; } = "";
        public string Author { get; set; } = "";
    }

    /// <summary>
    /// 系统设置
    /// </summary>
    public class SystemSettings
    {
        public string LogLevel { get; set; } = "Info";
        public bool AutoSaveResults { get; set; } = true;
        public string ResultsPath { get; set; } = "./test-results";
        public bool BackupEnabled { get; set; } = true;
        public int MaxLogFiles { get; set; } = 10;
        public int LogRotationSizeMB { get; set; } = 50;
        public string DefaultLanguage { get; set; } = "zh-CN";
        public string Theme { get; set; } = "Light";
        public bool AutoRefresh { get; set; } = true;
        public int RefreshInterval { get; set; } = 2000;
        public string DefaultUser { get; set; } = "Administrator";
        public string DefaultUserRole { get; set; } = "管理员";
    }

    /// <summary>
    /// DUT配置（通用，支持所有产品）
    /// </summary>
    public class DUTConfiguration
    {
        // 产品信息（新结构）
        public ProductInfo? ProductInfo { get; set; }
        
        // 全局设置（新结构）
        public GlobalSettings? GlobalSettings { get; set; }
        
        // MAC地址范围
        public List<MacRange>? MacRanges { get; set; }
        
        // 连接配置（新结构）
        public DUTConnections? Connections { get; set; }
        
        // 通信端点
        public CommunicationEndpoints? CommunicationEndpoints { get; set; }
        
        // 命名配置（新结构）
        public NamingConfig? NamingConfig { get; set; }
        
    }

    /// <summary>
    /// 产品信息（新结构）
    /// </summary>
    public class ProductInfo
    {
        public string Name { get; set; } = "";
        public string Model { get; set; } = "";
        public string Icon { get; set; } = "";
        public string Category { get; set; } = "";
        public string ExpectedSoftwareVersion { get; set; } = "";
    }

    /// <summary>
    /// 全局设置（新结构，统一命名）
    /// </summary>
    public class GlobalSettings
    {
        public int? DefaultMaxConcurrent { get; set; } = 16;
        public bool? EnablePreTestCheck { get; set; } = true;
        public double? DefaultVoltage { get; set; } = 3.8;
        public double? DefaultCurrent { get; set; } = 0.5;
        public int? TestTimeout { get; set; } = 300;
        public int? RetryCount { get; set; } = 3;
        public int? RetryDelay { get; set; } = 2000;
    }

    /// <summary>
    /// MAC地址范围
    /// </summary>
    public class MacRange
    {
        [JsonPropertyName("start")]
        public string Start { get; set; } = "";
        
        [JsonPropertyName("end")]
        public string End { get; set; } = "";
    }

    /// <summary>
    /// DUT连接配置（新结构）
    /// </summary>
    public class DUTConnections
    {
        public ConnectionConfig? Primary { get; set; }
        public ConnectionConfig? Secondary { get; set; }
    }

    /// <summary>
    /// 连接配置（通用）
    /// </summary>
    public class ConnectionConfig
    {
        public string Type { get; set; } = "";
        
        // 串口相关
        public int? BaudRate { get; set; }
        public int? DataBits { get; set; }
        public int? StopBits { get; set; }
        public string? Parity { get; set; }
        
        // 网络相关
        public int? Port { get; set; }
        public int? TelnetPort { get; set; }
        public string? Host { get; set; }
        public string? IPRange { get; set; }
        public string? Protocol { get; set; }
        
        // 通用
        public int? Timeout { get; set; }
    }

    /// <summary>
    /// 命名配置（新结构）
    /// </summary>
    public class NamingConfig
    {
        public string Template { get; set; } = "{TypeName}测试工位{Index}";
        public string IdTemplate { get; set; } = "DUT-{Index}";
    }

    /// <summary>
    /// 通信端点
    /// </summary>
    public class CommunicationEndpoints
    {
        public List<string> SerialPorts { get; set; } = new();
        public List<string> NetworkHosts { get; set; } = new();
    }

    /// <summary>
    /// 测试项目配置（新结构，简化）
    /// </summary>
    public class TestProjectConfiguration
    {
        public TestMode? TestMode { get; set; }
        public TestProject? TestProject { get; set; }
        
    }

    /// <summary>
    /// 测试模式
    /// </summary>
    public class TestMode
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Icon { get; set; } = "";
        public string Description { get; set; } = "";
        public int? DefaultTimeout { get; set; }
        public bool? EnableParallel { get; set; }
        public int? MaxRetries { get; set; }
    }

    /// <summary>
    /// 测试项目（新结构）
    /// </summary>
    public class TestProject
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public bool Enabled { get; set; } = true;
        public List<TestStepConfig> Steps { get; set; } = new();
    }

    /// <summary>
    /// 测试步骤配置
    /// </summary>
    public class TestStepConfig
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public int Order { get; set; } = 1;
        public bool Enabled { get; set; } = true;

        public string? Target { get; set; }
        public string? Type { get; set; }
        public string? Command { get; set; }
        public string? Expected { get; set; }
        public int? Timeout { get; set; }
        public int? Delay { get; set; }
        public string? Channel { get; set; }

        public bool ContinueOnFailure { get; set; } = false;
        public Dictionary<string, object>? ValidationRules { get; set; }
        public Dictionary<string, object>? Parameters { get; set; }
    }
}
