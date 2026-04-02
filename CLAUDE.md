# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 项目概述

通用测试框架（UTF）— 面向 3C/汽车电子的自动化测试平台。基于 .NET 10.0 WPF，支持 10-64 个 DUT 并行测试。

## 编译与运行

```bash
# 编译（自动打包插件）
dotnet build UniversalTestFramework.sln

# Release 版本
dotnet build -c Release UniversalTestFramework.sln

# 运行
cd UTF.UI/bin/Debug/net10.0-windows && ./UTF.UI.exe

# 清理重建
dotnet clean && dotnet build
```

编译时自动执行 `scripts/pack-plugins.ps1`，将插件复制到 `UTF.UI/bin/<Config>/net10.0-windows/plugins/`。

## 层级依赖关系（从底层到顶层）

```
UTF.HAL                  → IDevice, IDUT, IInstrument, DUTCommunicationHelper
UTF.Logging              → ILogger, AdvancedLogger
UTF.Plugin.Abstractions  → IStepExecutorPlugin, PluginMetadata, StepExecutionResult
UTF.Core                 → DI容器, 缓存, 调度器, 测试引擎, 会话管理
UTF.Configuration        → JSON/XML/YAML 配置解析
UTF.Plugin.Host          → StepExecutorPluginHost（AssemblyLoadContext 隔离加载）
UTF.Business             → TestOrchestrator, DeviceManager
UTF.Reporting            → ReportGenerator, DataAnalyzer
UTF.Vision               → VisionManager, 视觉算法
UTF.UI                   → WPF 主程序（net10.0-windows）
UTF.Plugins.Example      → 示例插件（CMD 执行器）
```

## 配置驱动

主配置：`config/unified-config.json`，结构：
- `SystemSettings` — 日志、语言、主题
- `DUTConfiguration` — 产品信息、串口列表（COM3-COM16）、网络主机、MAC 范围
- `TestProjectConfiguration.TestProject.Steps[]` — 测试步骤序列

**测试步骤字段：** `Id, Name, Order, Type(serial/custom/instrument), Command, Expected, Timeout, Channel`

**结果验证前缀：** `contains:` / `equals:` / `regex:`

添加新测试步骤只需编辑配置，无需改动代码。

## 插件系统

**目录结构：**
```
plugins/<pluginId>/<version>/plugin.manifest.json
                             <entryAssembly>.dll
```

**Manifest 必填字段：** `pluginId, version, pluginApiVersion, entryAssembly, entryType, supportedStepTypes, supportedChannels, priority`

`priority` 数值越小优先级越高。插件在独立 `AssemblyLoadContext` 中加载，支持热卸载。

**实现接口：**
```csharp
public class MyPlugin : IStepExecutorPlugin {
    public PluginMetadata Metadata { get; }
    public Task InitializeAsync(PluginInitContext context, CancellationToken ct);
    public bool CanHandle(string stepType, string channel);
    public Task<StepExecutionResult> ExecuteAsync(StepExecutionRequest request, CancellationToken ct);
    public Task ShutdownAsync(CancellationToken ct);
}
```

## 依赖注入

项目使用 **Microsoft.Extensions.DependencyInjection** 进行依赖注入管理。

### 应用启动配置

```csharp
// App.xaml.cs
protected override void OnStartup(StartupEventArgs e)
{
    var services = new ServiceCollection();

    // 注册核心服务
    services.AddUtfCore();

    // 注册业务服务
    services.AddUtfBusiness();

    // 注册 UI 服务
    services.AddUtfUI();

    // 注册窗口
    services.AddTransient<MainWindow>();

    var serviceProvider = services.BuildServiceProvider();
    var mainWindow = serviceProvider.GetRequiredService<MainWindow>();
    mainWindow.Show();
}
```

### 构造函数注入

```csharp
public class MainWindow : Window
{
    private readonly ILogger _logger;
    private readonly ITestEngine _testEngine;
    private readonly ICache _cache;

    public MainWindow(ILogger logger, ITestEngine testEngine, ICache cache)
    {
        _logger = logger;
        _testEngine = testEngine;
        _cache = cache;
        InitializeComponent();
    }
}
```

### 预注册服务

- **UTF.Core.DependencyInjection.ServiceCollectionExtensions.AddUtfCore()**
  - `ICache` (Singleton) - 内存缓存
  - `ILogger` (Singleton) - 日志服务
  - `IResourcePool` (Singleton) - 资源池
  - `ITestEngine` (Transient) - 测试引擎
  - `ITestSessionManager` (Singleton) - 会话管理器
  - `IDUTScheduler` (Singleton) - DUT 调度器

- **UTF.UI.DependencyInjection.ServiceCollectionExtensions.AddUtfBusiness()**
  - `DeviceManager` (Singleton)
  - `TestOrchestrator` (Singleton)

- **UTF.UI.DependencyInjection.ServiceCollectionExtensions.AddUtfUI()**
  - `DUTMonitorManager` (Singleton)
  - `ConfigurationManager` (Singleton)
  - `IPermissionManager` (Transient)


## 通信通道

- **Serial：** 115200/8N1，用于 DUT 控制命令
- **Cmd/Network：** Telnet 连接 DUT IP，用于网络测试

`DUTCommunicationHelper`（UTF.HAL）提供统一的命令发送和响应解析。

## 多语言

语言文件：`UTF.UI/Languages/{zh-CN,en-US,ja-JP}.json`，通过 `SystemSettings.DefaultLanguage` 切换，由 `ILanguageManager` 管理。

## 日志

```csharp
var logger = LoggerFactory.CreateLogger<YourClass>();
logger.Info("msg"); logger.Error("msg", exception);
```

日志输出：`UTF.UI/bin/Debug/net10.0-windows/logs/utf-app.log`

## 注意事项

- 仅支持 Windows（WPF / net10.0-windows）
- 串口访问可能需要管理员权限
- DUT 命名模板：`DUTConfiguration.NamingConfig.Template`（如 `{TypeName}测试工位{Index}`）
- MAC 地址必须在 `DUTConfiguration.MacRanges` 范围内
- 单步超时默认 5-25 秒，全局超时在 `GlobalSettings.TestTimeout`
