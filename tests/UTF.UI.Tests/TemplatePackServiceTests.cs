using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NSubstitute;
using UTF.Configuration;
using UTF.UI.Services;
using Xunit;

namespace UTF.UI.Tests;

/// <summary>
/// Unit tests for <see cref="TemplatePackService"/> catalog scan, load, and apply-with-backup.
/// </summary>
public sealed class TemplatePackServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _templatesDir;
    private readonly string _configDir;
    private readonly ConfigurationManager _configManager;
    private readonly TemplatePackService _service;

    public TemplatePackServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "utf-template-pack-tests-" + Guid.NewGuid().ToString("N"));
        _templatesDir = Path.Combine(_tempRoot, "templates");
        _configDir = Path.Combine(_tempRoot, "config");
        Directory.CreateDirectory(_templatesDir);
        Directory.CreateDirectory(_configDir);

        File.WriteAllText(Path.Combine(_templatesDir, "factory-quick-start-minimal.json"), MinimalPackJson(
            name: "工厂快速上手最小配置",
            description: "最小可运行样例",
            productName: "示例产品",
            productModel: "Demo-1.0",
            stepCount: 2));

        File.WriteAllText(Path.Combine(_templatesDir, "consumer-electronics-android.json"), MinimalPackJson(
            name: "Android 终端测试",
            description: "ADB 产线模板",
            productName: "Android Phone",
            productModel: "Phone-X",
            stepCount: 3));

        File.WriteAllText(Path.Combine(_templatesDir, "not-a-pack.txt"), "ignore me");

        var adapter = Substitute.For<IUnifiedConfigurationAdapter>();
        adapter.ValidateConfigurationWithErrors(Arg.Any<UTF.Configuration.Models.UnifiedConfiguration>())
            .Returns(new System.Collections.Generic.List<string>());
        adapter.ValidateConfiguration(Arg.Any<UTF.Configuration.Models.UnifiedConfiguration>())
            .Returns(true);

        _configManager = new ConfigurationManager(adapter, _configDir);
        _service = new TemplatePackService(_configManager, logger: null, templatesDirectoryOverride: _templatesDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
        }
        catch
        {
            // best-effort cleanup
        }

        _configManager.Dispose();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetAvailablePacks_ScansJsonAndInfersMetadata()
    {
        var packs = _service.GetAvailablePacks();

        Assert.Equal(2, packs.Count);
        var factory = packs.Single(p => p.FileName == "factory-quick-start-minimal.json");
        Assert.Equal("工厂快速上手最小配置", factory.DisplayName);
        Assert.Equal("最小可运行样例", factory.Description);
        Assert.Equal("示例产品", factory.ProductName);
        Assert.Equal(2, factory.StepCount);
        Assert.Equal("factory", factory.Industry);

        var android = packs.Single(p => p.FileName == "consumer-electronics-android.json");
        Assert.Equal("consumer-electronics", android.Industry);
        Assert.Equal(3, android.StepCount);
        Assert.Contains("android", android.Tags);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task LoadPackAsync_DeserializesUnifiedConfiguration()
    {
        var path = Path.Combine(_templatesDir, "factory-quick-start-minimal.json");
        var config = await _service.LoadPackAsync(path);

        Assert.Equal("工厂快速上手最小配置", config.ConfigurationInfo.Name);
        Assert.Equal("示例产品", config.DUTConfiguration.ProductInfo?.Name);
        Assert.Equal(2, config.TestProjectConfiguration?.TestProject?.Steps.Count);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ApplyPackAsync_BacksUpAndSavesUnifiedConfig()
    {
        // Seed an existing unified-config so backup is created.
        var existing = Path.Combine(_configDir, "unified-config.json");
        await File.WriteAllTextAsync(existing, MinimalPackJson(
            name: "旧配置",
            description: "to backup",
            productName: "Old",
            productModel: "O-1",
            stepCount: 1));

        var packPath = Path.Combine(_templatesDir, "consumer-electronics-android.json");
        var backup = await _service.ApplyPackAsync(packPath, backupCurrent: true);

        Assert.NotNull(backup);
        Assert.True(File.Exists(backup));
        Assert.Contains("unified-config.backup.", Path.GetFileName(backup), StringComparison.OrdinalIgnoreCase);

        var applied = await _configManager.GetUnifiedConfigurationAsync();
        Assert.Equal("Android 终端测试", applied.ConfigurationInfo.Name);
        Assert.Equal("Android Phone", applied.DUTConfiguration.ProductInfo?.Name);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void InferIndustryAndTags_KnownPrefixes()
    {
        var (industry, tags) = TemplatePackService.InferIndustryAndTags("automotive-ecu-eol.json");
        Assert.Equal("automotive", industry);
        Assert.Contains("ecu", tags);

        var (ind2, _) = TemplatePackService.InferIndustryAndTags("instrument-integration-pcba.json");
        Assert.Equal("instrument-integration", ind2);
    }

    private static string MinimalPackJson(
        string name,
        string description,
        string productName,
        string productModel,
        int stepCount)
    {
        var steps = string.Join(",\n", Enumerable.Range(1, stepCount).Select(i =>
            $$"""
                  {
                    "Id": "step-{{i}}",
                    "Name": "Step {{i}}",
                    "Order": {{i}},
                    "Type": "cmd",
                    "Command": "echo ok",
                    "Expected": "contains:ok",
                    "Timeout": 5000,
                    "Channel": "local"
                  }
            """));

        return $$"""
        {
          "ConfigurationInfo": {
            "Name": "{{name}}",
            "Version": "1.0.0",
            "Description": "{{description}}",
            "Author": "tests"
          },
          "SystemSettings": {
            "LogLevel": "Info",
            "ResultsPath": "./test-results",
            "DefaultLanguage": "zh-CN",
            "Theme": "Light"
          },
          "DUTConfiguration": {
            "ProductInfo": {
              "Name": "{{productName}}",
              "Model": "{{productModel}}",
              "Category": "test"
            },
            "GlobalSettings": {
              "DefaultMaxConcurrent": 2
            },
            "CommunicationEndpoints": {
              "SerialPorts": [ "COM3" ]
            },
            "NamingConfig": {
              "Template": "{TypeName}{Index}",
              "IdTemplate": "DUT-{Index}"
            }
          },
          "TestProjectConfiguration": {
            "TestMode": {
              "Id": "production",
              "Name": "量产"
            },
            "TestProject": {
              "Id": "proj-1",
              "Name": "{{name}}",
              "Enabled": true,
              "Steps": [
            {{steps}}
              ]
            }
          }
        }
        """;
    }
}
