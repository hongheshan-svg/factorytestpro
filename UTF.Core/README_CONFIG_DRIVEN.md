# 配置驱动测试模块使用指南

## 概述

配置驱动测试模块提供了一个灵活的、基于配置文件的测试执行框架，支持：

- **纯配置驱动**：无需修改代码，仅通过编辑 JSON 配置文件即可添加/修改测试步骤
- **插件系统集成**：自动使用插件执行测试步骤，支持热插拔
- **多种验证规则**：支持 `contains:`、`equals:`、`regex:` 等验证前缀
- **灵活的通道支持**：Serial、Cmd、Network 等多种通信通道
- **并行测试**：支持多个 DUT 并行测试
- **重试机制**：步骤级别的自动重试

## 核心组件

### 1. ConfigDrivenTestEngine
测试执行引擎，负责执行单个测试步骤和完整测试项目。

```csharp
var engine = new ConfigDrivenTestEngine(logger, pluginHost);

// 执行单个步骤
var stepResult = await engine.ExecuteStepAsync(stepConfig, dutId, context);

// 执行完整项目
var report = await engine.ExecuteTestProjectAsync(testProject, dutId, context);
```

### 2. ConfigurationLoader
配置加载器，从 `unified-config.json` 加载测试项目和 DUT 配置。

```csharp
var loader = new ConfigurationLoader("config/unified-config.json", logger);

// 加载测试项目
var testProject = await loader.LoadTestProjectAsync();

// 加载 DUT 配置
var dutConfig = await loader.LoadDUTConfigAsync();

// 验证配置文件
var validation = await loader.ValidateConfigAsync();
```

### 3. ConfigDrivenTestOrchestrator
测试编排器，管理测试会话、并行执行、事件通知。

```csharp
var orchestrator = new ConfigDrivenTestOrchestrator(
    "config/unified-config.json",
    logger,
    pluginHost);

// 初始化
await orchestrator.InitializeAsync();

// 创建会话
var session = await orchestrator.CreateSessionAsync(
    dutIds: new List<string> { "DUT-001", "DUT-002" },
    operatorName: "张三"
);

// 启动测试
await orchestrator.StartSessionAsync(session.SessionId);

// 获取统计信息
var stats = orchestrator.GetSessionStatistics(session.SessionId);
```

### 4. ConfigDrivenTestAdapter
适配器，用于在新旧配置模型之间转换。

```csharp
var adapter = new ConfigDrivenTestAdapter(logger);

// 转换测试步骤
var configStep = adapter.ConvertToConfigTestStep(unifiedStepConfig);

// 转换测试项目
var configProject = adapter.ConvertToConfigTestProject(unifiedTestProject);

// 转换测试结果
var testReport = adapter.ConvertToTestReport(configReport);
```

## 配置文件格式

### 测试项目配置

```json
{
  "TestProjectConfiguration": {
    "TestMode": {
      "Id": "production",
      "Name": "生产测试",
      "Icon": "🏭",
      "Description": "完整生产测试流程",
      "DefaultTimeout": 300000,
      "EnableParallel": true,
      "MaxRetries": 2
    },
    "TestProject": {
      "Id": "qq_doll_production_test",
      "Name": "QQ公仔生产测试",
      "Description": "QQ公仔完整生产测试流程",
      "Enabled": true,
      "Steps": [
        {
          "Id": "step_001",
          "Name": "网络连接测试",
          "Description": "通过ping命令测试网络连接",
          "Order": 1,
          "Enabled": true,
          "Target": "dut",
          "Type": "custom",
          "Command": "ping -n 2 www.qq.com",
          "Expected": "contains:来自",
          "Timeout": 10000,
          "Delay": 1000,
          "Channel": "Cmd",
          "ContinueOnFailure": false,
          "Parameters": {
            "MaxRetries": 3
          }
        }
      ]
    }
  }
}
```

### 测试步骤字段说明

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `Id` | string | 是 | 步骤唯一标识 |
| `Name` | string | 是 | 步骤名称 |
| `Description` | string | 否 | 步骤描述 |
| `Order` | int | 是 | 执行顺序 |
| `Enabled` | bool | 否 | 是否启用（默认 true） |
| `Type` | string | 是 | 步骤类型：serial/custom/cmd/network/instrument |
| `Command` | string | 是 | 执行的命令 |
| `Expected` | string | 否 | 期望结果（支持前缀） |
| `Timeout` | int | 否 | 超时时间（毫秒） |
| `Delay` | int | 否 | 执行后延迟（毫秒） |
| `Channel` | string | 否 | 通信通道：Serial/Cmd/Network |
| `ContinueOnFailure` | bool | 否 | 失败后是否继续（默认 false） |
| `Parameters` | object | 否 | 额外参数 |
| `ValidationRules` | object | 否 | 验证规则 |

### 验证规则前缀

配置驱动测试引擎支持以下验证前缀：

1. **contains:** - 包含匹配（不区分大小写）
   ```json
   "Expected": "contains:SW_VERSION:V1.0"
   ```

2. **equals:** - 精确匹配（不区分大小写）
   ```json
   "Expected": "equals:OK"
   ```

3. **regex:** - 正则表达式匹配
   ```json
   "Expected": "regex:^SW_VERSION:V\\d+\\.\\d+$"
   ```

4. **无前缀** - 默认使用包含匹配
   ```json
   "Expected": "test voice pass"
   ```

## 使用示例

### 示例 1：基本使用

```csharp
using UTF.Core;
using UTF.Logging;

// 创建日志记录器
var logger = LoggerFactory.CreateLogger<Program>();

// 创建编排器
var orchestrator = new ConfigDrivenTestOrchestrator(
    "config/unified-config.json",
    logger
);

// 初始化
if (!await orchestrator.InitializeAsync())
{
    logger.Error("初始化失败");
    return;
}

// 创建测试会话
var session = await orchestrator.CreateSessionAsync(
    dutIds: new List<string> { "DUT-001", "DUT-002", "DUT-003" },
    operatorName: "操作员A"
);

if (session == null)
{
    logger.Error("创建会话失败");
    return;
}

// 订阅事件
orchestrator.StepCompleted += (sender, e) =>
{
    logger.Info($"步骤完成: DUT={e.DutId}, 步骤={e.Data}");
};

orchestrator.SessionCompleted += (sender, e) =>
{
    logger.Info($"会话完成: {e.SessionId}");
};

// 启动测试
await orchestrator.StartSessionAsync(session.SessionId);

// 等待完成
while (session.Status == ConfigTestStatus.Running)
{
    await Task.Delay(1000);

    // 获取统计信息
    var stats = orchestrator.GetSessionStatistics(session.SessionId);
    logger.Info($"进度: {stats.CompletedDuts}/{stats.TotalDuts} DUTs, " +
                $"通过率: {stats.PassRate:P2}");
}

// 获取最终结果
var finalSession = orchestrator.GetSession(session.SessionId);
logger.Info($"测试完成，整体结果: {(finalSession.OverallPassed ? "PASS" : "FAIL")}");
```

### 示例 2：集成插件系统

```csharp
using UTF.Core;
using UTF.Plugin.Host;
using UTF.Logging;

var logger = LoggerFactory.CreateLogger<Program>();

// 初始化插件主机
var pluginHost = new StepExecutorPluginHost("plugins", logger);
var pluginReport = await pluginHost.InitializeAsync();

logger.Info($"插件加载: 成功 {pluginReport.LoadedCount}, 失败 {pluginReport.FailedCount}");

// 创建编排器（传入插件主机）
var orchestrator = new ConfigDrivenTestOrchestrator(
    "config/unified-config.json",
    logger,
    pluginHost  // 插件主机会自动处理匹配的步骤类型
);

await orchestrator.InitializeAsync();

// 后续使用与示例 1 相同
```

### 示例 3：自定义上下文和参数

```csharp
// 创建会话时传入自定义上下文
var sessionContext = new Dictionary<string, object>
{
    { "BatchNumber", "BATCH-2024-001" },
    { "ProductionLine", "Line-A" },
    { "QualityLevel", "Premium" }
};

var session = await orchestrator.CreateSessionAsync(
    dutIds: new List<string> { "DUT-001" },
    operatorName: "张三",
    sessionContext: sessionContext
);

// 上下文可在测试步骤中使用（通过 Parameters 传递）
```

### 示例 4：单独使用测试引擎

```csharp
using UTF.Core;
using UTF.Logging;

var logger = LoggerFactory.CreateLogger<Program>();
var engine = new ConfigDrivenTestEngine(logger);

// 手动构建测试步骤
var step = new ConfigTestStep
{
    Id = "test_001",
    Name = "串口版本检查",
    Type = "serial",
    Command = "system_manager version",
    Expected = "contains:SW_VERSION:V1.0",
    Timeout = 5000,
    Channel = "Serial"
};

// 执行步骤
var result = await engine.ExecuteStepAsync(step, "DUT-001");

Console.WriteLine($"步骤结果: {(result.Passed ? "PASS" : "FAIL")}");
Console.WriteLine($"输出: {result.RawOutput}");
Console.WriteLine($"错误: {result.ErrorMessage}");
```

### 示例 5：配置文件验证

```csharp
using UTF.Core;
using UTF.Logging;

var logger = LoggerFactory.CreateLogger<Program>();
var loader = new ConfigurationLoader("config/unified-config.json", logger);

// 验证配置文件
var validation = await loader.ValidateConfigAsync();

if (!validation.IsValid)
{
    logger.Error("配置文件验证失败:");
    foreach (var error in validation.Errors)
    {
        logger.Error($"  - {error}");
    }
    return;
}

logger.Info("配置文件验证通过");

// 加载测试项目
var testProject = await loader.LoadTestProjectAsync();
logger.Info($"加载测试项目: {testProject.Name}, 步骤数: {testProject.Steps.Count}");

// 加载 DUT 配置
var dutConfig = await loader.LoadDUTConfigAsync();
logger.Info($"产品: {dutConfig.ProductName}, 串口数: {dutConfig.SerialPorts.Count}");
```

## 事件系统

配置驱动测试编排器提供以下事件：

### SessionStarted
会话启动时触发。

```csharp
orchestrator.SessionStarted += (sender, e) =>
{
    Console.WriteLine($"会话启动: {e.SessionId}");
};
```

### SessionCompleted
会话完成时触发。

```csharp
orchestrator.SessionCompleted += (sender, e) =>
{
    var session = e.Data as ConfigTestSession;
    Console.WriteLine($"会话完成: {session.SessionId}, 结果: {session.OverallPassed}");
};
```

### StepCompleted
每个测试步骤完成时触发。

```csharp
orchestrator.StepCompleted += (sender, e) =>
{
    var stepResult = e.Data as ConfigDrivenStepResult;
    Console.WriteLine($"步骤完成: DUT={e.DutId}, 步骤={stepResult.StepName}, " +
                      $"结果={stepResult.Passed}");
};
```

### ErrorOccurred
发生错误时触发。

```csharp
orchestrator.ErrorOccurred += (sender, e) =>
{
    Console.WriteLine($"错误: {e.EventType}, 消息={e.Data}");
};
```

## 最佳实践

### 1. 配置文件组织

```
config/
├── unified-config.json          # 主配置文件
├── test-projects/               # 测试项目配置（可选）
│   ├── production-test.json
│   └── debug-test.json
└── dut-configs/                 # DUT 配置（可选）
    ├── qq-doll.json
    └── smart-speaker.json
```

### 2. 步骤命名规范

- 使用有意义的 ID：`step_001_network_test`
- 使用清晰的名称：`网络连接测试`
- 添加详细描述：`通过 ping 命令测试 DUT 的网络连接性`

### 3. 超时设置

- 串口命令：5-10 秒
- 网络命令：10-15 秒
- 复杂测试：20-30 秒
- 全局超时：300 秒（5 分钟）

### 4. 重试策略

```json
{
  "Parameters": {
    "MaxRetries": 3,
    "RetryDelay": 1000
  }
}
```

### 5. 错误处理

```csharp
try
{
    var session = await orchestrator.CreateSessionAsync(dutIds);
    await orchestrator.StartSessionAsync(session.SessionId);
}
catch (Exception ex)
{
    logger.Error("测试执行失败", ex);
    // 清理资源
    await orchestrator.CleanupSessionAsync(session.SessionId);
}
```

## 性能优化

### 1. 并行测试

配置驱动测试编排器自动并行执行多个 DUT 的测试：

```csharp
// 16 个 DUT 会并行测试
var dutIds = Enumerable.Range(1, 16).Select(i => $"DUT-{i:D3}").ToList();
var session = await orchestrator.CreateSessionAsync(dutIds);
```

### 2. 缓存配置

配置加载器会缓存解析后的配置，避免重复解析。

### 3. 插件优先

当插件系统可用时，优先使用插件执行步骤，性能更好。

## 故障排查

### 问题 1：配置文件加载失败

**症状**：`LoadTestProjectAsync` 返回 null

**解决方案**：
1. 检查配置文件路径是否正确
2. 验证 JSON 格式是否有效
3. 确认 `TestProjectConfiguration.TestProject` 节点存在
4. 查看日志输出的详细错误信息

### 问题 2：步骤执行失败

**症状**：步骤结果 `Passed = false`

**解决方案**：
1. 检查 `ErrorMessage` 字段
2. 验证 `Command` 是否正确
3. 确认 `Expected` 验证规则是否合理
4. 检查 `Timeout` 是否足够
5. 查看 `RawOutput` 了解实际输出

### 问题 3：插件未被使用

**症状**：即使有插件，仍使用内置执行逻辑

**解决方案**：
1. 确认插件已正确加载（检查 `PluginLoadReport`）
2. 验证插件的 `SupportedStepTypes` 和 `SupportedChannels` 匹配
3. 检查插件的 `CanHandle` 方法实现
4. 确认插件优先级设置正确

## 扩展开发

### 自定义验证规则

在 `ConfigDrivenTestEngine.ValidateResult` 方法中添加新的验证前缀：

```csharp
else if (expectedPattern.StartsWith("json:", StringComparison.OrdinalIgnoreCase))
{
    var jsonPath = expectedPattern.Substring("json:".Length);
    // 实现 JSON 路径验证
}
```

### 自定义步骤类型

创建插件实现 `IStepExecutorPlugin` 接口：

```csharp
public class MyCustomPlugin : IStepExecutorPlugin
{
    public PluginMetadata Metadata => new()
    {
        PluginId = "my-custom-plugin",
        SupportedStepTypes = new[] { "custom_type" },
        SupportedChannels = new[] { "CustomChannel" }
    };

    public bool CanHandle(string stepType, string channel)
    {
        return stepType == "custom_type" && channel == "CustomChannel";
    }

    public async Task<StepExecutionResult> ExecuteAsync(
        StepExecutionRequest request,
        CancellationToken ct)
    {
        // 实现自定义执行逻辑
    }
}
```

## 总结

配置驱动测试模块提供了一个强大而灵活的测试框架，主要优势：

✅ **零代码修改** - 仅通过配置文件即可添加/修改测试
✅ **插件集成** - 自动使用插件系统，支持扩展
✅ **并行执行** - 多 DUT 并行测试，提高效率
✅ **灵活验证** - 支持多种验证规则和前缀
✅ **事件驱动** - 实时监控测试进度和结果
✅ **易于维护** - 配置与代码分离，便于管理

开始使用配置驱动测试模块，让测试配置更简单、更灵活！
