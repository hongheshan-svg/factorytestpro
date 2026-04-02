using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace UTF.Configuration;

/// <summary>
/// JSON配置提供者实现
/// </summary>
public sealed class JsonConfigurationProvider : IConfigurationProvider, IDisposable
{
    private readonly JsonSerializerOptions _jsonOptions;
    private string _configurationDirectory = string.Empty;
    private FileSystemWatcher? _fileWatcher;
    private bool _disposed;
    
    public IReadOnlyList<ConfigurationFormat> SupportedFormats { get; } = new[] { ConfigurationFormat.Json };
    
    public event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;
    
    public JsonConfigurationProvider()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };
    }
    
    public async Task<bool> InitializeAsync(string configurationDirectory, CancellationToken cancellationToken = default)
    {
        try
        {
            _configurationDirectory = configurationDirectory;
            
            // 确保配置目录存在
            if (!Directory.Exists(configurationDirectory))
            {
                Directory.CreateDirectory(configurationDirectory);
            }
            
            // 创建默认配置文件（如果不存在）
            await CreateDefaultConfigurationsIfNotExistAsync(cancellationToken);
            
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    public async Task<SystemConfigurationInfo> LoadSystemConfigurationAsync(string? filePath = null, CancellationToken cancellationToken = default)
    {
        filePath ??= GetConfigurationFilePath("system", ConfigurationFormat.Json);
        
        if (!File.Exists(filePath))
        {
            return CreateDefaultSystemConfiguration();
        }
        
        try
        {
            await using var fileStream = File.OpenRead(filePath);
            var config = await JsonSerializer.DeserializeAsync<SystemConfigurationDto>(fileStream, _jsonOptions, cancellationToken);
            return MapToSystemConfiguration(config ?? new SystemConfigurationDto());
        }
        catch
        {
            return CreateDefaultSystemConfiguration();
        }
    }
    
    public async Task<bool> SaveSystemConfigurationAsync(SystemConfigurationInfo configuration, string? filePath = null, CancellationToken cancellationToken = default)
    {
        try
        {
            filePath ??= GetConfigurationFilePath("system", ConfigurationFormat.Json);
            var dto = MapFromSystemConfiguration(configuration);
            
            await using var fileStream = File.Create(filePath);
            await JsonSerializer.SerializeAsync(fileStream, dto, _jsonOptions, cancellationToken);
            
            OnConfigurationChanged("system", "Updated", null, configuration);
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    public async Task<List<DeviceConfigurationInfo>> LoadDeviceConfigurationAsync(string? filePath = null, CancellationToken cancellationToken = default)
    {
        filePath ??= GetConfigurationFilePath("devices", ConfigurationFormat.Json);
        
        if (!File.Exists(filePath))
        {
            return new List<DeviceConfigurationInfo>();
        }
        
        try
        {
            await using var fileStream = File.OpenRead(filePath);
            var config = await JsonSerializer.DeserializeAsync<DeviceConfigurationListDto>(fileStream, _jsonOptions, cancellationToken);
            return config?.Devices?.Select(MapToDeviceConfiguration).ToList() ?? new List<DeviceConfigurationInfo>();
        }
        catch
        {
            return new List<DeviceConfigurationInfo>();
        }
    }
    
    public async Task<bool> SaveDeviceConfigurationAsync(List<DeviceConfigurationInfo> configurations, string? filePath = null, CancellationToken cancellationToken = default)
    {
        try
        {
            filePath ??= GetConfigurationFilePath("devices", ConfigurationFormat.Json);
            var dto = new DeviceConfigurationListDto
            {
                Devices = configurations.Select(MapFromDeviceConfiguration).ToList()
            };
            
            await using var fileStream = File.Create(filePath);
            await JsonSerializer.SerializeAsync(fileStream, dto, _jsonOptions, cancellationToken);
            
            OnConfigurationChanged("devices", "Updated", null, configurations);
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    public async Task<List<TestSequenceConfigurationInfo>> LoadTestSequenceConfigurationAsync(string? filePath = null, CancellationToken cancellationToken = default)
    {
        filePath ??= GetConfigurationFilePath("test-sequences", ConfigurationFormat.Json);
        
        if (!File.Exists(filePath))
        {
            return new List<TestSequenceConfigurationInfo>();
        }
        
        try
        {
            await using var fileStream = File.OpenRead(filePath);
            var config = await JsonSerializer.DeserializeAsync<TestSequenceConfigurationListDto>(fileStream, _jsonOptions, cancellationToken);
            return config?.TestSequences?.Select(MapToTestSequenceConfiguration).ToList() ?? new List<TestSequenceConfigurationInfo>();
        }
        catch
        {
            return new List<TestSequenceConfigurationInfo>();
        }
    }
    
    public async Task<bool> SaveTestSequenceConfigurationAsync(List<TestSequenceConfigurationInfo> configurations, string? filePath = null, CancellationToken cancellationToken = default)
    {
        try
        {
            filePath ??= GetConfigurationFilePath("test-sequences", ConfigurationFormat.Json);
            var dto = new TestSequenceConfigurationListDto
            {
                TestSequences = configurations.Select(MapFromTestSequenceConfiguration).ToList()
            };
            
            await using var fileStream = File.Create(filePath);
            await JsonSerializer.SerializeAsync(fileStream, dto, _jsonOptions, cancellationToken);
            
            OnConfigurationChanged("test-sequences", "Updated", null, configurations);
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    public async Task<ConfigurationValidationResult> ValidateConfigurationAsync(string filePath, ConfigurationFormat format, CancellationToken cancellationToken = default)
    {
        var result = new ConfigurationValidationResult
        {
            ConfigurationPath = filePath,
            ValidationTime = DateTime.UtcNow
        };
        
        if (format != ConfigurationFormat.Json)
        {
            return result with { 
                IsValid = false, 
                Errors = new List<string> { "不支持的配置格式" } 
            };
        }
        
        if (!File.Exists(filePath))
        {
            return result with { 
                IsValid = false, 
                Errors = new List<string> { "配置文件不存在" } 
            };
        }
        
        try
        {
            await using var fileStream = File.OpenRead(filePath);
            using var document = await JsonDocument.ParseAsync(fileStream, cancellationToken: cancellationToken);
            
            // 基本JSON格式验证
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return result with { 
                    IsValid = false, 
                    Errors = new List<string> { "配置文件根元素必须是JSON对象" } 
                };
            }
            
            return result with { IsValid = true };
        }
        catch (JsonException ex)
        {
            return result with { 
                IsValid = false, 
                Errors = new List<string> { $"JSON格式错误: {ex.Message}" } 
            };
        }
        catch (Exception ex)
        {
            return result with { 
                IsValid = false, 
                Errors = new List<string> { $"验证失败: {ex.Message}" } 
            };
        }
    }
    
    public Task<bool> StartMonitoringAsync(string configurationPath, CancellationToken cancellationToken = default)
    {
        try
        {
            StopWatcherInternal();

            var directory = Path.GetDirectoryName(configurationPath);
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                return Task.FromResult(false);
            }

            _fileWatcher = new FileSystemWatcher(directory)
            {
                Filter = "*.json",
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                IncludeSubdirectories = false,
                EnableRaisingEvents = true
            };

            _fileWatcher.Changed += OnConfigFileChanged;
            _fileWatcher.Created += OnConfigFileChanged;
            _fileWatcher.Renamed += (s, e) => OnConfigFileChanged(s, e);

            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }
    
    public Task<bool> StopMonitoringAsync(CancellationToken cancellationToken = default)
    {
        StopWatcherInternal();
        return Task.FromResult(true);
    }

    private void StopWatcherInternal()
    {
        if (_fileWatcher != null)
        {
            _fileWatcher.EnableRaisingEvents = false;
            _fileWatcher.Changed -= OnConfigFileChanged;
            _fileWatcher.Created -= OnConfigFileChanged;
            _fileWatcher.Dispose();
            _fileWatcher = null;
        }
    }

    private void OnConfigFileChanged(object sender, FileSystemEventArgs e)
    {
        ConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs
        {
            ConfigurationPath = e.FullPath,
            ChangeType = e.ChangeType.ToString(),
            Timestamp = DateTime.UtcNow
        });
    }
    
    public string GetConfigurationFilePath(string configurationName, ConfigurationFormat format)
    {
        var extension = format switch
        {
            ConfigurationFormat.Json => ".json",
            ConfigurationFormat.Xml => ".xml",
            ConfigurationFormat.Yaml => ".yaml",
            ConfigurationFormat.Ini => ".ini",
            ConfigurationFormat.Properties => ".properties",
            _ => ".json"
        };
        
        return Path.Combine(_configurationDirectory, $"{configurationName}{extension}");
    }
    
    public async Task<bool> BackupConfigurationAsync(string sourcePath, string backupPath, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var source = File.OpenRead(sourcePath);
            await using var backup = File.Create(backupPath);
            await source.CopyToAsync(backup, cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    public async Task<bool> RestoreConfigurationAsync(string backupPath, string targetPath, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var backup = File.OpenRead(backupPath);
            await using var target = File.Create(targetPath);
            await backup.CopyToAsync(target, cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    public async Task<bool> ExportConfigurationAsync(string sourcePath, string exportPath, ConfigurationFormat exportFormat, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(sourcePath))
            {
                return false;
            }

            var json = await File.ReadAllTextAsync(sourcePath, cancellationToken);
            var doc = JsonSerializer.Deserialize<JsonElement>(json, _jsonOptions);

            string output;
            switch (exportFormat)
            {
                case ConfigurationFormat.Json:
                    output = JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
                    break;

                case ConfigurationFormat.Xml:
                    output = ConvertJsonElementToXml(doc, "Configuration");
                    break;

                case ConfigurationFormat.Yaml:
                    output = ConvertJsonElementToYaml(doc, indent: 0);
                    break;

                default:
                    return false;
            }

            var dir = Path.GetDirectoryName(exportPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            await File.WriteAllTextAsync(exportPath, output, System.Text.Encoding.UTF8, cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    public async Task<bool> ImportConfigurationAsync(string importPath, string targetPath, ConfigurationFormat importFormat, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(importPath))
            {
                return false;
            }

            string json;
            switch (importFormat)
            {
                case ConfigurationFormat.Json:
                    // JSON → JSON: 直接复制并格式化
                    var raw = await File.ReadAllTextAsync(importPath, cancellationToken);
                    var doc = JsonSerializer.Deserialize<JsonElement>(raw, _jsonOptions);
                    json = JsonSerializer.Serialize(doc, _jsonOptions);
                    break;

                case ConfigurationFormat.Xml:
                case ConfigurationFormat.Yaml:
                case ConfigurationFormat.Ini:
                case ConfigurationFormat.Properties:
                    // 不支持从非 JSON 格式逆向导入为 JSON（需要专用解析器）
                    return false;

                default:
                    return false;
            }

            var dir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            await File.WriteAllTextAsync(targetPath, json, System.Text.Encoding.UTF8, cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    public async Task<bool> MergeConfigurationAsync(string baseConfigPath, string overrideConfigPath, string outputPath, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(baseConfigPath) || !File.Exists(overrideConfigPath))
            {
                return false;
            }

            var baseJson = await File.ReadAllTextAsync(baseConfigPath, cancellationToken);
            var overrideJson = await File.ReadAllTextAsync(overrideConfigPath, cancellationToken);

            using var baseDoc = JsonDocument.Parse(baseJson);
            using var overrideDoc = JsonDocument.Parse(overrideJson);

            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                MergeJsonElements(writer, baseDoc.RootElement, overrideDoc.RootElement);
            }

            var merged = System.Text.Encoding.UTF8.GetString(stream.ToArray());

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            await File.WriteAllTextAsync(outputPath, merged, System.Text.Encoding.UTF8, cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void MergeJsonElements(Utf8JsonWriter writer, JsonElement baseElement, JsonElement overrideElement)
    {
        if (baseElement.ValueKind == JsonValueKind.Object && overrideElement.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            var visited = new HashSet<string>();

            foreach (var prop in baseElement.EnumerateObject())
            {
                visited.Add(prop.Name);
                if (overrideElement.TryGetProperty(prop.Name, out var overrideValue))
                {
                    writer.WritePropertyName(prop.Name);
                    MergeJsonElements(writer, prop.Value, overrideValue);
                }
                else
                {
                    prop.WriteTo(writer);
                }
            }

            foreach (var prop in overrideElement.EnumerateObject())
            {
                if (!visited.Contains(prop.Name))
                {
                    prop.WriteTo(writer);
                }
            }

            writer.WriteEndObject();
        }
        else
        {
            // 非对象元素：override 覆盖 base
            overrideElement.WriteTo(writer);
        }
    }

    private static string ConvertJsonElementToXml(JsonElement element, string rootName)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        WriteXmlElement(sb, element, rootName, indent: 0);
        return sb.ToString();
    }

    private static void WriteXmlElement(System.Text.StringBuilder sb, JsonElement element, string name, int indent)
    {
        var prefix = new string(' ', indent * 2);
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                sb.AppendLine($"{prefix}<{name}>");
                foreach (var prop in element.EnumerateObject())
                {
                    WriteXmlElement(sb, prop.Value, prop.Name, indent + 1);
                }
                sb.AppendLine($"{prefix}</{name}>");
                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    WriteXmlElement(sb, item, name, indent);
                }
                break;

            default:
                sb.AppendLine($"{prefix}<{name}>{element}</{name}>");
                break;
        }
    }

    private static string ConvertJsonElementToYaml(JsonElement element, int indent)
    {
        var sb = new System.Text.StringBuilder();
        WriteYamlElement(sb, element, indent, isRoot: true);
        return sb.ToString();
    }

    private static void WriteYamlElement(System.Text.StringBuilder sb, JsonElement element, int indent, bool isRoot)
    {
        var prefix = new string(' ', indent * 2);
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.Object)
                    {
                        sb.AppendLine($"{prefix}{prop.Name}:");
                        WriteYamlElement(sb, prop.Value, indent + 1, isRoot: false);
                    }
                    else if (prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        sb.AppendLine($"{prefix}{prop.Name}:");
                        WriteYamlElement(sb, prop.Value, indent + 1, isRoot: false);
                    }
                    else
                    {
                        sb.AppendLine($"{prefix}{prop.Name}: {FormatYamlScalar(prop.Value)}");
                    }
                }
                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object || item.ValueKind == JsonValueKind.Array)
                    {
                        sb.AppendLine($"{prefix}-");
                        WriteYamlElement(sb, item, indent + 1, isRoot: false);
                    }
                    else
                    {
                        sb.AppendLine($"{prefix}- {FormatYamlScalar(item)}");
                    }
                }
                break;

            default:
                sb.AppendLine($"{prefix}{FormatYamlScalar(element)}");
                break;
        }
    }

    private static string FormatYamlScalar(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => $"\"{element.GetString()}\"",
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "null",
            JsonValueKind.Number => element.GetRawText(),
            _ => element.GetRawText()
        };
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            StopWatcherInternal();
            _disposed = true;
        }
    }
    
    #region Private Methods
    
    private async Task CreateDefaultConfigurationsIfNotExistAsync(CancellationToken cancellationToken)
    {
        // 创建默认系统配置
        var systemConfigPath = GetConfigurationFilePath("system", ConfigurationFormat.Json);
        if (!File.Exists(systemConfigPath))
        {
            var defaultSystemConfig = CreateDefaultSystemConfiguration();
            await SaveSystemConfigurationAsync(defaultSystemConfig, systemConfigPath, cancellationToken);
        }
        
        // 创建默认设备配置
        var deviceConfigPath = GetConfigurationFilePath("devices", ConfigurationFormat.Json);
        if (!File.Exists(deviceConfigPath))
        {
            var defaultDeviceConfig = CreateDefaultDeviceConfigurations();
            await SaveDeviceConfigurationAsync(defaultDeviceConfig, deviceConfigPath, cancellationToken);
        }
        
        // 创建默认测试序列配置
        var testSequenceConfigPath = GetConfigurationFilePath("test-sequences", ConfigurationFormat.Json);
        if (!File.Exists(testSequenceConfigPath))
        {
            var defaultTestSequences = CreateDefaultTestSequenceConfigurations();
            await SaveTestSequenceConfigurationAsync(defaultTestSequences, testSequenceConfigPath, cancellationToken);
        }
    }
    
    private SystemConfigurationInfo CreateDefaultSystemConfiguration()
    {
        return new SystemConfigurationInfo
        {
            SystemName = "通用自动化测试平台",
            Version = "1.0.0",
            WorkingDirectory = Environment.CurrentDirectory,
            DataDirectory = Path.Combine(Environment.CurrentDirectory, "Data"),
            Logging = new LoggingConfigurationInfo
            {
                LogLevel = "Info",
                LogFilePath = Path.Combine(Environment.CurrentDirectory, "Logs", "system.log"),
                MaxFileSizeMB = 100,
                RetentionDays = 30,
                EnableConsole = true,
                EnableFile = true,
                EnableDatabase = false
            },
            MaxParallelTasks = Environment.ProcessorCount,
            TaskTimeoutSeconds = 3600,
            AutoSaveIntervalSeconds = 300,
            Language = "zh-CN",
            Security = new SecurityConfigurationInfo
            {
                EnableAuthentication = false,
                EnableAuthorization = false,
                SessionTimeoutMinutes = 60,
                PasswordPolicy = new PasswordPolicyInfo
                {
                    MinLength = 8,
                    RequireUppercase = true,
                    RequireLowercase = true,
                    RequireDigits = true,
                    RequireSpecialCharacters = false
                }
            }
        };
    }
    
    private List<DeviceConfigurationInfo> CreateDefaultDeviceConfigurations()
    {
        return new List<DeviceConfigurationInfo>
        {
            new DeviceConfigurationInfo
            {
                DeviceId = "DMM_001",
                Name = "数字万用表",
                DeviceType = "DigitalMultimeter",
                Category = "Instrument",
                Manufacturer = "Keysight",
                Model = "34470A",
                ConnectionType = "VISA",
                ConnectionParameters = new Dictionary<string, object>
                {
                    {"ResourceString", "USB0::0x2A8D::0x0101::MY12345678::INSTR"}
                },
                AutoConnect = true,
                TimeoutMs = 5000,
                Enabled = true
            },
            new DeviceConfigurationInfo
            {
                DeviceId = "PSU_001",
                Name = "可编程电源",
                DeviceType = "PowerSupply",
                Category = "Instrument",
                Manufacturer = "Keysight",
                Model = "E36313A",
                ConnectionType = "VISA",
                ConnectionParameters = new Dictionary<string, object>
                {
                    {"ResourceString", "USB0::0x2A8D::0x1909::MY12345679::INSTR"}
                },
                AutoConnect = true,
                TimeoutMs = 5000,
                Enabled = true
            }
        };
    }
    
    private List<TestSequenceConfigurationInfo> CreateDefaultTestSequenceConfigurations()
    {
        return new List<TestSequenceConfigurationInfo>
        {
            new TestSequenceConfigurationInfo
            {
                SequenceId = "SEQ_001",
                Name = "基础电气测试",
                Description = "基础的电压、电流测试序列",
                Version = "1.0",
                RequiredInstruments = new List<string> { "DMM_001", "PSU_001" },
                RequiredDUTs = new List<string> { "DUT_001" },
                Steps = new List<TestStepConfigurationInfo>
                {
                    new TestStepConfigurationInfo
                    {
                        StepId = "STEP_001",
                        Name = "电源初始化",
                        StepType = "Instrument",
                        TargetDeviceId = "PSU_001",
                        Command = "Initialize",
                        TimeoutMs = 5000
                    },
                    new TestStepConfigurationInfo
                    {
                        StepId = "STEP_002",
                        Name = "设置输出电压",
                        StepType = "Instrument",
                        TargetDeviceId = "PSU_001",
                        Command = "SetVoltage",
                        Parameters = new Dictionary<string, object>
                        {
                            {"Channel", 1},
                            {"Voltage", 5.0},
                            {"CurrentLimit", 1.0}
                        },
                        TimeoutMs = 3000
                    },
                    new TestStepConfigurationInfo
                    {
                        StepId = "STEP_003",
                        Name = "测量电压",
                        StepType = "Instrument",
                        TargetDeviceId = "DMM_001",
                        Command = "Measure",
                        Parameters = new Dictionary<string, object>
                        {
                            {"Function", "VoltageDC"},
                            {"Range", 10.0}
                        },
                        ExpectedRange = new ExpectedRangeInfo
                        {
                            Min = 4.9,
                            Max = 5.1,
                            Unit = "V"
                        },
                        TimeoutMs = 5000,
                        StoreResultAs = "measured_voltage"
                    }
                }
            }
        };
    }
    
    private void OnConfigurationChanged(string configurationPath, string changeType, object? oldValue, object? newValue)
    {
        ConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs
        {
            ConfigurationPath = configurationPath,
            ChangeType = changeType,
            OldValue = oldValue,
            NewValue = newValue,
            Timestamp = DateTime.UtcNow
        });
    }
    
    #endregion
    
    #region Mapping Methods
    
    private SystemConfigurationInfo MapToSystemConfiguration(SystemConfigurationDto dto)
    {
        return new SystemConfigurationInfo
        {
            SystemName = dto.SystemName ?? string.Empty,
            Version = dto.Version ?? string.Empty,
            WorkingDirectory = dto.WorkingDirectory ?? string.Empty,
            DataDirectory = dto.DataDirectory ?? string.Empty,
            Logging = MapToLoggingConfiguration(dto.Logging ?? new LoggingConfigurationDto()),
            Database = dto.Database != null ? MapToDatabaseConfiguration(dto.Database) : null,
            MaxParallelTasks = dto.MaxParallelTasks,
            TaskTimeoutSeconds = dto.TaskTimeoutSeconds,
            AutoSaveIntervalSeconds = dto.AutoSaveIntervalSeconds,
            Language = dto.Language ?? "zh-CN",
            TimeZone = dto.TimeZone ?? string.Empty,
            Security = MapToSecurityConfiguration(dto.Security ?? new SecurityConfigurationDto()),
            ExtendedSettings = dto.ExtendedSettings ?? new Dictionary<string, object>()
        };
    }
    
    private SystemConfigurationDto MapFromSystemConfiguration(SystemConfigurationInfo info)
    {
        return new SystemConfigurationDto
        {
            SystemName = info.SystemName,
            Version = info.Version,
            WorkingDirectory = info.WorkingDirectory,
            DataDirectory = info.DataDirectory,
            Logging = MapFromLoggingConfiguration(info.Logging),
            Database = info.Database != null ? MapFromDatabaseConfiguration(info.Database) : null,
            MaxParallelTasks = info.MaxParallelTasks,
            TaskTimeoutSeconds = info.TaskTimeoutSeconds,
            AutoSaveIntervalSeconds = info.AutoSaveIntervalSeconds,
            Language = info.Language,
            TimeZone = info.TimeZone,
            Security = MapFromSecurityConfiguration(info.Security),
            ExtendedSettings = info.ExtendedSettings
        };
    }
    
    private LoggingConfigurationInfo MapToLoggingConfiguration(LoggingConfigurationDto dto)
    {
        return new LoggingConfigurationInfo
        {
            LogLevel = dto.LogLevel ?? "Info",
            LogFilePath = dto.LogFilePath ?? string.Empty,
            MaxFileSizeMB = dto.MaxFileSizeMB,
            RetentionDays = dto.RetentionDays,
            EnableConsole = dto.EnableConsole,
            EnableFile = dto.EnableFile,
            EnableDatabase = dto.EnableDatabase,
            ExtendedConfiguration = dto.ExtendedConfiguration ?? new Dictionary<string, object>()
        };
    }
    
    private LoggingConfigurationDto MapFromLoggingConfiguration(LoggingConfigurationInfo info)
    {
        return new LoggingConfigurationDto
        {
            LogLevel = info.LogLevel,
            LogFilePath = info.LogFilePath,
            MaxFileSizeMB = info.MaxFileSizeMB,
            RetentionDays = info.RetentionDays,
            EnableConsole = info.EnableConsole,
            EnableFile = info.EnableFile,
            EnableDatabase = info.EnableDatabase,
            ExtendedConfiguration = info.ExtendedConfiguration
        };
    }
    
    private DatabaseConfigurationInfo MapToDatabaseConfiguration(DatabaseConfigurationDto dto)
    {
        return new DatabaseConfigurationInfo
        {
            DatabaseType = dto.DatabaseType ?? string.Empty,
            ConnectionString = dto.ConnectionString ?? string.Empty,
            CommandTimeoutSeconds = dto.CommandTimeoutSeconds,
            ConnectionPoolSize = dto.ConnectionPoolSize,
            EnableMigrations = dto.EnableMigrations,
            ExtendedConfiguration = dto.ExtendedConfiguration ?? new Dictionary<string, object>()
        };
    }
    
    private DatabaseConfigurationDto MapFromDatabaseConfiguration(DatabaseConfigurationInfo info)
    {
        return new DatabaseConfigurationDto
        {
            DatabaseType = info.DatabaseType,
            ConnectionString = info.ConnectionString,
            CommandTimeoutSeconds = info.CommandTimeoutSeconds,
            ConnectionPoolSize = info.ConnectionPoolSize,
            EnableMigrations = info.EnableMigrations,
            ExtendedConfiguration = info.ExtendedConfiguration
        };
    }
    
    private SecurityConfigurationInfo MapToSecurityConfiguration(SecurityConfigurationDto dto)
    {
        return new SecurityConfigurationInfo
        {
            EnableAuthentication = dto.EnableAuthentication,
            EnableAuthorization = dto.EnableAuthorization,
            SessionTimeoutMinutes = dto.SessionTimeoutMinutes,
            PasswordPolicy = MapToPasswordPolicy(dto.PasswordPolicy ?? new PasswordPolicyDto()),
            ExtendedConfiguration = dto.ExtendedConfiguration ?? new Dictionary<string, object>()
        };
    }
    
    private SecurityConfigurationDto MapFromSecurityConfiguration(SecurityConfigurationInfo info)
    {
        return new SecurityConfigurationDto
        {
            EnableAuthentication = info.EnableAuthentication,
            EnableAuthorization = info.EnableAuthorization,
            SessionTimeoutMinutes = info.SessionTimeoutMinutes,
            PasswordPolicy = MapFromPasswordPolicy(info.PasswordPolicy),
            ExtendedConfiguration = info.ExtendedConfiguration
        };
    }
    
    private PasswordPolicyInfo MapToPasswordPolicy(PasswordPolicyDto dto)
    {
        return new PasswordPolicyInfo
        {
            MinLength = dto.MinLength,
            RequireUppercase = dto.RequireUppercase,
            RequireLowercase = dto.RequireLowercase,
            RequireDigits = dto.RequireDigits,
            RequireSpecialCharacters = dto.RequireSpecialCharacters
        };
    }
    
    private PasswordPolicyDto MapFromPasswordPolicy(PasswordPolicyInfo info)
    {
        return new PasswordPolicyDto
        {
            MinLength = info.MinLength,
            RequireUppercase = info.RequireUppercase,
            RequireLowercase = info.RequireLowercase,
            RequireDigits = info.RequireDigits,
            RequireSpecialCharacters = info.RequireSpecialCharacters
        };
    }
    
    private DeviceConfigurationInfo MapToDeviceConfiguration(DeviceConfigurationDto dto)
    {
        return new DeviceConfigurationInfo
        {
            DeviceId = dto.DeviceId ?? string.Empty,
            Name = dto.Name ?? string.Empty,
            DeviceType = dto.DeviceType ?? string.Empty,
            Category = dto.Category ?? string.Empty,
            Manufacturer = dto.Manufacturer ?? string.Empty,
            Model = dto.Model ?? string.Empty,
            ConnectionType = dto.ConnectionType ?? string.Empty,
            ConnectionParameters = dto.ConnectionParameters ?? new Dictionary<string, object>(),
            InitializationParameters = dto.InitializationParameters ?? new Dictionary<string, object>(),
            CalibrationParameters = dto.CalibrationParameters ?? new Dictionary<string, object>(),
            AutoConnect = dto.AutoConnect,
            TimeoutMs = dto.TimeoutMs,
            RetryCount = dto.RetryCount,
            Enabled = dto.Enabled,
            Priority = dto.Priority,
            Tags = dto.Tags ?? new List<string>(),
            ExtendedConfiguration = dto.ExtendedConfiguration ?? new Dictionary<string, object>()
        };
    }
    
    private DeviceConfigurationDto MapFromDeviceConfiguration(DeviceConfigurationInfo info)
    {
        return new DeviceConfigurationDto
        {
            DeviceId = info.DeviceId,
            Name = info.Name,
            DeviceType = info.DeviceType,
            Category = info.Category,
            Manufacturer = info.Manufacturer,
            Model = info.Model,
            ConnectionType = info.ConnectionType,
            ConnectionParameters = info.ConnectionParameters,
            InitializationParameters = info.InitializationParameters,
            CalibrationParameters = info.CalibrationParameters,
            AutoConnect = info.AutoConnect,
            TimeoutMs = info.TimeoutMs,
            RetryCount = info.RetryCount,
            Enabled = info.Enabled,
            Priority = info.Priority,
            Tags = info.Tags,
            ExtendedConfiguration = info.ExtendedConfiguration
        };
    }
    
    private TestSequenceConfigurationInfo MapToTestSequenceConfiguration(TestSequenceConfigurationDto dto)
    {
        return new TestSequenceConfigurationInfo
        {
            SequenceId = dto.SequenceId ?? string.Empty,
            Name = dto.Name ?? string.Empty,
            Description = dto.Description ?? string.Empty,
            Version = dto.Version ?? "1.0",
            Steps = dto.Steps?.Select(MapToTestStepConfiguration).ToList() ?? new List<TestStepConfigurationInfo>(),
            RequiredInstruments = dto.RequiredInstruments ?? new List<string>(),
            RequiredDUTs = dto.RequiredDUTs ?? new List<string>(),
            EstimatedDuration = TimeSpan.FromSeconds(dto.EstimatedDurationSeconds),
            AllowParallelExecution = dto.AllowParallelExecution,
            StopOnFailure = dto.StopOnFailure,
            Tags = dto.Tags ?? new List<string>(),
            GlobalVariables = dto.GlobalVariables ?? new Dictionary<string, object>(),
            Properties = dto.Properties ?? new Dictionary<string, object>()
        };
    }
    
    private TestSequenceConfigurationDto MapFromTestSequenceConfiguration(TestSequenceConfigurationInfo info)
    {
        return new TestSequenceConfigurationDto
        {
            SequenceId = info.SequenceId,
            Name = info.Name,
            Description = info.Description,
            Version = info.Version,
            Steps = info.Steps.Select(MapFromTestStepConfiguration).ToList(),
            RequiredInstruments = info.RequiredInstruments,
            RequiredDUTs = info.RequiredDUTs,
            EstimatedDurationSeconds = (int)info.EstimatedDuration.TotalSeconds,
            AllowParallelExecution = info.AllowParallelExecution,
            StopOnFailure = info.StopOnFailure,
            Tags = info.Tags,
            GlobalVariables = info.GlobalVariables,
            Properties = info.Properties
        };
    }
    
    private TestStepConfigurationInfo MapToTestStepConfiguration(TestStepConfigurationDto dto)
    {
        return new TestStepConfigurationInfo
        {
            StepId = dto.StepId ?? string.Empty,
            Name = dto.Name ?? string.Empty,
            Description = dto.Description ?? string.Empty,
            StepType = dto.StepType ?? string.Empty,
            TargetDeviceId = dto.TargetDeviceId,
            Command = dto.Command ?? string.Empty,
            Parameters = dto.Parameters ?? new Dictionary<string, object>(),
            ExpectedResult = dto.ExpectedResult,
            ExpectedRange = dto.ExpectedRange != null ? new ExpectedRangeInfo
            {
                Min = dto.ExpectedRange.Min,
                Max = dto.ExpectedRange.Max,
                Unit = dto.ExpectedRange.Unit
            } : null,
            Unit = dto.Unit ?? string.Empty,
            TimeoutMs = dto.TimeoutMs,
            RetryCount = dto.RetryCount,
            DelayAfterMs = dto.DelayAfterMs,
            IsCritical = dto.IsCritical,
            Prerequisites = dto.Prerequisites ?? new List<string>(),
            CleanupActions = dto.CleanupActions ?? new List<string>(),
            StoreResultAs = dto.StoreResultAs,
            ConditionExpression = dto.ConditionExpression
        };
    }
    
    private TestStepConfigurationDto MapFromTestStepConfiguration(TestStepConfigurationInfo info)
    {
        return new TestStepConfigurationDto
        {
            StepId = info.StepId,
            Name = info.Name,
            Description = info.Description,
            StepType = info.StepType,
            TargetDeviceId = info.TargetDeviceId,
            Command = info.Command,
            Parameters = info.Parameters,
            ExpectedResult = info.ExpectedResult,
            ExpectedRange = info.ExpectedRange != null ? new ExpectedRangeDto
            {
                Min = info.ExpectedRange.Min,
                Max = info.ExpectedRange.Max,
                Unit = info.ExpectedRange.Unit
            } : null,
            Unit = info.Unit,
            TimeoutMs = info.TimeoutMs,
            RetryCount = info.RetryCount,
            DelayAfterMs = info.DelayAfterMs,
            IsCritical = info.IsCritical,
            Prerequisites = info.Prerequisites,
            CleanupActions = info.CleanupActions,
            StoreResultAs = info.StoreResultAs,
            ConditionExpression = info.ConditionExpression
        };
    }
    
    #endregion
}

#region DTO Classes

public sealed class SystemConfigurationDto
{
    public string? SystemName { get; set; }
    public string? Version { get; set; }
    public string? WorkingDirectory { get; set; }
    public string? DataDirectory { get; set; }
    public LoggingConfigurationDto? Logging { get; set; }
    public DatabaseConfigurationDto? Database { get; set; }
    public int MaxParallelTasks { get; set; } = 4;
    public int TaskTimeoutSeconds { get; set; } = 3600;
    public int AutoSaveIntervalSeconds { get; set; } = 300;
    public string? Language { get; set; }
    public string? TimeZone { get; set; }
    public SecurityConfigurationDto? Security { get; set; }
    public Dictionary<string, object>? ExtendedSettings { get; set; }
}

public sealed class LoggingConfigurationDto
{
    public string? LogLevel { get; set; }
    public string? LogFilePath { get; set; }
    public int MaxFileSizeMB { get; set; } = 100;
    public int RetentionDays { get; set; } = 30;
    public bool EnableConsole { get; set; } = true;
    public bool EnableFile { get; set; } = true;
    public bool EnableDatabase { get; set; } = false;
    public Dictionary<string, object>? ExtendedConfiguration { get; set; }
}

public sealed class DatabaseConfigurationDto
{
    public string? DatabaseType { get; set; }
    public string? ConnectionString { get; set; }
    public int CommandTimeoutSeconds { get; set; } = 30;
    public int ConnectionPoolSize { get; set; } = 10;
    public bool EnableMigrations { get; set; } = true;
    public Dictionary<string, object>? ExtendedConfiguration { get; set; }
}

public sealed class SecurityConfigurationDto
{
    public bool EnableAuthentication { get; set; } = false;
    public bool EnableAuthorization { get; set; } = false;
    public int SessionTimeoutMinutes { get; set; } = 60;
    public PasswordPolicyDto? PasswordPolicy { get; set; }
    public Dictionary<string, object>? ExtendedConfiguration { get; set; }
}

public sealed class PasswordPolicyDto
{
    public int MinLength { get; set; } = 8;
    public bool RequireUppercase { get; set; } = true;
    public bool RequireLowercase { get; set; } = true;
    public bool RequireDigits { get; set; } = true;
    public bool RequireSpecialCharacters { get; set; } = false;
}

public sealed class DeviceConfigurationListDto
{
    public List<DeviceConfigurationDto>? Devices { get; set; }
}

public sealed class DeviceConfigurationDto
{
    public string? DeviceId { get; set; }
    public string? Name { get; set; }
    public string? DeviceType { get; set; }
    public string? Category { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? ConnectionType { get; set; }
    public Dictionary<string, object>? ConnectionParameters { get; set; }
    public Dictionary<string, object>? InitializationParameters { get; set; }
    public Dictionary<string, object>? CalibrationParameters { get; set; }
    public bool AutoConnect { get; set; } = true;
    public int TimeoutMs { get; set; } = 30000;
    public int RetryCount { get; set; } = 3;
    public bool Enabled { get; set; } = true;
    public int Priority { get; set; } = 0;
    public List<string>? Tags { get; set; }
    public Dictionary<string, object>? ExtendedConfiguration { get; set; }
}

public sealed class TestSequenceConfigurationListDto
{
    public List<TestSequenceConfigurationDto>? TestSequences { get; set; }
}

public sealed class TestSequenceConfigurationDto
{
    public string? SequenceId { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Version { get; set; }
    public List<TestStepConfigurationDto>? Steps { get; set; }
    public List<string>? RequiredInstruments { get; set; }
    public List<string>? RequiredDUTs { get; set; }
    public int EstimatedDurationSeconds { get; set; }
    public bool AllowParallelExecution { get; set; } = false;
    public bool StopOnFailure { get; set; } = true;
    public List<string>? Tags { get; set; }
    public Dictionary<string, object>? GlobalVariables { get; set; }
    public Dictionary<string, object>? Properties { get; set; }
}

public sealed class TestStepConfigurationDto
{
    public string? StepId { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? StepType { get; set; }
    public string? TargetDeviceId { get; set; }
    public string? Command { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }
    public object? ExpectedResult { get; set; }
    public ExpectedRangeDto? ExpectedRange { get; set; }
    public string? Unit { get; set; }
    public int TimeoutMs { get; set; } = 30000;
    public int RetryCount { get; set; } = 0;
    public int DelayAfterMs { get; set; } = 0;
    public bool IsCritical { get; set; } = true;
    public List<string>? Prerequisites { get; set; }
    public List<string>? CleanupActions { get; set; }
    public string? StoreResultAs { get; set; }
    public string? ConditionExpression { get; set; }
}

public sealed class ExpectedRangeDto
{
    public double Min { get; set; }
    public double Max { get; set; }
    public string? Unit { get; set; }
}

#endregion
