using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UTF.UI.Models;

namespace UTF.UI.Services;

/// <summary>
/// 默认 <see cref="ITestConfigurationBuilder"/> 实现。从快速向导输入构建一份完整的统一配置。
/// 该实现从 <c>QuickTestWizardWindow.BuildUnifiedConfiguration</c> 提取而来。
/// </summary>
public sealed class TestConfigurationBuilder : ITestConfigurationBuilder
{
    /// <inheritdoc />
    public UnifiedConfiguration Build(QuickTestWizardInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var productName = input.ProductName ?? string.Empty;
        var productModel = input.ProductModel ?? string.Empty;
        var icon = string.IsNullOrWhiteSpace(input.Icon) ? "📱" : input.Icon;
        var category = input.Category ?? string.Empty;
        var dutCount = Math.Max(1, input.DUTCount);

        var serialPorts = new List<string>();
        var networkHosts = new List<string>();
        if (input.UseSerial)
        {
            for (var i = 0; i < dutCount; i++)
            {
                serialPorts.Add($"COM{3 + i}");
            }
        }
        if (input.UseNetwork)
        {
            for (var i = 0; i < dutCount; i++)
            {
                networkHosts.Add($"192.168.1.{10 + i}");
            }
        }

        var steps = (input.Steps ?? new List<WizardStepInput>())
            .Select(s => new TestStepConfig
            {
                Id = s.Id,
                Name = s.Name,
                Order = s.Order,
                Enabled = true,
                Type = s.StepType,
                Channel = s.Channel,
                Target = "dut",
                Command = s.Command,
                Expected = s.Expected,
                Timeout = s.Timeout,
                Delay = 500
            })
            .ToList();

        var projectId = productName.Length == 0
            ? "test"
            : productName.ToLowerInvariant().Replace(" ", "_");

        return new UnifiedConfiguration
        {
            ConfigurationInfo = new ConfigurationInfo
            {
                Name = $"{productName}测试配置",
                Version = "1.0.0",
                CreatedDate = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                Description = $"由快速向导创建的 {productName} 测试配置",
                Author = "UTF Quick Wizard"
            },
            SystemSettings = new SystemSettings
            {
                LogLevel = "Info",
                AutoSaveResults = true,
                ResultsPath = "./test-results",
                DefaultLanguage = "zh-CN",
                Theme = "Light"
            },
            DUTConfiguration = new DUTConfiguration
            {
                ProductInfo = new ProductInfo
                {
                    Name = productName,
                    Model = productModel,
                    Icon = icon,
                    Category = category
                },
                GlobalSettings = new GlobalSettings
                {
                    DefaultMaxConcurrent = dutCount,
                    TestTimeout = 300,
                    RetryCount = 2,
                    RetryDelay = 2000
                },
                CommunicationEndpoints = new CommunicationEndpoints
                {
                    SerialPorts = serialPorts,
                    NetworkHosts = networkHosts
                },
                NamingConfig = new NamingConfig
                {
                    Template = "{TypeName}测试工位{Index}",
                    IdTemplate = "DUT-{Index}"
                },
                Connections = new DUTConnections
                {
                    Primary = input.UseSerial
                        ? new ConnectionConfig { Type = "Serial", BaudRate = 115200, DataBits = 8, StopBits = 1, Parity = "None" }
                        : input.UseNetwork
                            ? new ConnectionConfig { Type = "Network", TelnetPort = 23 }
                            : null
                }
            },
            TestProjectConfiguration = new TestProjectConfiguration
            {
                TestMode = new TestMode
                {
                    Id = "production",
                    Name = "生产测试",
                    Description = $"{productName}生产测试流程",
                    DefaultTimeout = 300000,
                    EnableParallel = true,
                    MaxRetries = 2
                },
                TestProject = new TestProject
                {
                    Id = $"{projectId}_test",
                    Name = $"{productName}生产测试",
                    Enabled = true,
                    Steps = steps
                }
            }
        };
    }
}
