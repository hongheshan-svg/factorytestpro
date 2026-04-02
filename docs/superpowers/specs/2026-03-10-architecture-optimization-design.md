# UTF 架构通用化优化设计文档

日期: 2026-03-10
状态: 已批准

## 1. 背景与目标

UTF（通用测试框架）当前存在以下架构问题：

- 双引擎并行（`OptimizedTestEngine` + `ConfigDrivenTestEngine`），模型重复，内部全是模拟代码
- 真实执行逻辑（串口/网络/ADB）泄漏在 UI 层 `DUTMonitorManager`（~700 行）
- 大量 `[Obsolete]` 死代码未清理（`IDUTScheduler`, `ITestSessionManager`, `IResourcePool`, `TestOrchestrator`）
- DI 注册不完整（仅 3 个服务）
- `ITestEngine` 接口过于庞大（15 个方法混合多个职责）
- 响应验证逻辑在 3 处重复
- Reporting 使用模拟数据，Vision 未集成

**目标：** 通过破坏性重构，建立清晰的分层架构，使核心执行逻辑可独立测试、可扩展、可无头运行。

## 2. 架构总览（重构后）

```
UTF.UI (纯展示层)
  ↓ 依赖抽象
UTF.Core (接口定义 + 模型 + 验证)
  ↑ 实现注入
UTF.HAL (硬件通信执行器)
UTF.Business (测试执行编排, 设备管理)
UTF.Plugin.Host (插件命令执行器)
UTF.Reporting (真实数据报告)
UTF.Vision (视觉命令执行器)
```

### 依赖方向

- UTF.UI → UTF.Core（仅抽象）
- UTF.Business → UTF.Core, UTF.HAL
- UTF.HAL → UTF.Core
- UTF.Plugin.Host → UTF.Core, UTF.Plugin.Abstractions
- UTF.Reporting → UTF.Core
- UTF.Vision → UTF.Core

UI 不再直接依赖 Business/Plugin.Host/Vision 的具体实现，通过 DI 注入。

## 3. 核心执行引擎重构

### 3.1 拆分 ITestEngine 为 3 个聚焦接口

```csharp
// UTF.Core/Execution/ITestExecutor.cs
public interface ITestExecutor
{
    Task<StepExecutionResult> ExecuteStepAsync(
        TestStepConfig step, string dutId,
        IDictionary<string, object>? context = null,
        CancellationToken ct = default);

    Task<TestSessionResult> ExecuteAllStepsAsync(
        IReadOnlyList<TestStepConfig> steps, string dutId,
        IDictionary<string, object>? context = null,
        IProgress<StepProgressInfo>? progress = null,
        CancellationToken ct = default);

    event EventHandler<StepCompletedEventArgs>? StepCompleted;
}

// UTF.Core/Session/ITestSessionManager.cs（新版，替代旧 Obsolete 版本）
public interface ITestSessionManager
{
    Task<string> CreateSessionAsync(IReadOnlyList<string> dutIds, CancellationToken ct = default);
    Task PauseSessionAsync(string sessionId, CancellationToken ct = default);
    Task ResumeSessionAsync(string sessionId, CancellationToken ct = default);
    Task StopSessionAsync(string sessionId, CancellationToken ct = default);
    TestSessionStatus GetSessionStatus(string sessionId);
}

// UTF.Core/Reporting/ITestResultStore.cs
public interface ITestResultStore
{
    Task StoreResultAsync(string sessionId, string dutId, TestSessionResult result, CancellationToken ct = default);
    Task<TestSessionResult?> GetResultAsync(string sessionId, string dutId, CancellationToken ct = default);
    Task<IReadOnlyList<TestSessionResult>> GetAllResultsAsync(string sessionId, CancellationToken ct = default);
}
```

### 3.2 统一模型

删除 `TestStep` record（过度设计），删除 `ConfigTestStep`，统一为 `TestStepConfig`：

```csharp
// UTF.Core/Models/TestStepConfig.cs
public sealed class TestStepConfig
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int Order { get; set; } = 1;
    public bool Enabled { get; set; } = true;
    public string? Target { get; set; }
    public string Type { get; set; } = "serial";   // serial, network, cmd, adb, scpi, vision, custom
    public string? Command { get; set; }
    public string? Expected { get; set; }           // contains:/equals:/regex:/notcontains: 前缀
    public int Timeout { get; set; } = 5000;
    public int Delay { get; set; } = 0;
    public string Channel { get; set; } = "Serial";
    public int MaxRetries { get; set; } = 1;
    public bool ContinueOnFailure { get; set; } = false;
    public Dictionary<string, object>? ValidationRules { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }
}
```

删除 `ConfigDrivenTestReport` / `ConfigDrivenStepResult`，统一为：

```csharp
// UTF.Core/Models/StepExecutionResult.cs
public sealed class StepExecutionResult
{
    public string StepId { get; set; } = "";
    public string StepName { get; set; } = "";
    public bool Passed { get; set; }
    public bool Skipped { get; set; }
    public string RawOutput { get; set; } = "";
    public string ExpectedValue { get; set; } = "";
    public string ErrorMessage { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration => EndTime - StartTime;
    public int RetryCount { get; set; }
}

// UTF.Core/Models/TestSessionResult.cs
public sealed class TestSessionResult
{
    public string SessionId { get; set; } = "";
    public string DutId { get; set; } = "";
    public bool Passed { get; set; }
    public string ErrorMessage { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public List<StepExecutionResult> StepResults { get; set; } = new();
}
```

### 3.3 删除的类型

| 删除 | 替代 |
|------|------|
| `ConfigDrivenTestEngine` | `ITestExecutor` 实现 |
| `ConfigTestStep` | `TestStepConfig` |
| `ConfigTestProject` | 直接用 `List<TestStepConfig>` |
| `ConfigDrivenStepResult` | `StepExecutionResult` |
| `ConfigDrivenTestReport` | `TestSessionResult` |
| `CommandExecutionResult`(internal) | `CommandResult`(public) |
| `StepValidationResult`(internal) | `IResponseValidator` 返回值 |
| `TestStep` record | `TestStepConfig` |
| `TestSequence` record | 不需要，步骤按 Order 排序即可 |
| `TestPlan` record | 配置驱动，不需要代码模型 |
| `TestTask` record | `ITestSessionManager` 管理 |
| `TestReport` record | `TestSessionResult` |
| `TestStepExecutionResult` record | `StepExecutionResult` |

## 4. 命令执行策略 — ICommandExecutor

### 4.1 接口定义

```csharp
// UTF.Core/Commands/ICommandExecutor.cs
public interface ICommandExecutor
{
    bool CanHandle(string stepType, string channel);
    Task<CommandResult> ExecuteAsync(CommandRequest request, CancellationToken ct = default);
}

// UTF.Core/Commands/CommandRequest.cs
public sealed class CommandRequest
{
    public string StepId { get; set; } = "";
    public string DutId { get; set; } = "";
    public string StepType { get; set; } = "";
    public string Channel { get; set; } = "";
    public string Command { get; set; } = "";
    public int TimeoutMs { get; set; } = 5000;
    public string? Target { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }
}

// UTF.Core/Commands/CommandResult.cs
public sealed class CommandResult
{
    public bool Success { get; set; }
    public string Output { get; set; } = "";
    public string ErrorMessage { get; set; } = "";
}

// UTF.Core/Commands/ICommandExecutorFactory.cs
public interface ICommandExecutorFactory
{
    ICommandExecutor GetExecutor(string stepType, string channel);
}
```

### 4.2 实现分布

| 执行器 | 项目 | 处理的 stepType |
|--------|------|----------------|
| `SerialCommandExecutor` | UTF.HAL | serial |
| `NetworkCommandExecutor` | UTF.HAL | network |
| `ProcessCommandExecutor` | UTF.HAL | cmd, adb, scpi |
| `PluginCommandExecutor` | UTF.Plugin.Host | custom（委托给插件） |
| `VisionCommandExecutor` | UTF.Vision | vision |

### 4.3 路由逻辑

`CommandExecutorFactory` 持有所有 `ICommandExecutor` 实例（通过 DI 注入 `IEnumerable<ICommandExecutor>`），按 `CanHandle` 匹配。插件系统作为最后的 fallback。

## 5. 响应验证 — IResponseValidator

```csharp
// UTF.Core/Validation/IResponseValidator.cs
public interface IResponseValidator
{
    bool CanValidate(string expectedPattern);
    ValidationResult Validate(string actualOutput, string expectedPattern);
}

// UTF.Core/Validation/ValidationResult.cs
public sealed class ResponseValidationResult
{
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; } = "";
}

// UTF.Core/Validation/IResponseValidatorFactory.cs
public interface IResponseValidatorFactory
{
    ResponseValidationResult Validate(string actualOutput, string expectedPattern);
}
```

### 实现

| 验证器 | 前缀 | 逻辑 |
|--------|------|------|
| `ContainsValidator` | `contains:` 或无前缀 | `output.Contains(expected, OrdinalIgnoreCase)` |
| `EqualsValidator` | `equals:` | `output.Trim().Equals(expected.Trim(), OrdinalIgnoreCase)` |
| `RegexValidator` | `regex:` | `Regex.IsMatch(output, pattern, IgnoreCase)` |
| `NotContainsValidator` | `notcontains:` | `!output.Contains(expected, OrdinalIgnoreCase)` |

`ResponseValidatorFactory` 解析前缀，路由到对应验证器。

## 6. 废弃代码清理

### 6.1 删除列表

| 文件 | 原因 |
|------|------|
| `UTF.Core/IDUTScheduler.cs` | `[Obsolete]`，未使用 |
| `UTF.Core/DUTScheduler.cs` | `[Obsolete]` 实现 |
| `UTF.Core/ITestSessionManager.cs`（旧版） | `[Obsolete]`，新版替代 |
| `UTF.Core/TestSessionManager.cs` | `[Obsolete]` 实现 |
| `UTF.Core/IResourcePool.cs` | `[Obsolete]`，未使用 |
| `UTF.Core/OptimizedResourcePool.cs` | `[Obsolete]` 实现 |
| `UTF.Core/ConfigDrivenTestEngine.cs` | 合并到新 ITestExecutor |
| `UTF.Core/ConfigDrivenTestOrchestrator.cs` | 重复 |
| `UTF.Core/ConfigDrivenTestAdapter.cs` | 重复 |
| `UTF.Core/ConfigDrivenTestAnalyzer.cs` | 重复 |
| `UTF.Core/ConfigDrivenTestValidator.cs` | 重复 |
| `UTF.Core/ConfigDrivenReportGenerator.cs` | 合并到 Reporting |
| `UTF.Core/OptimizedTestEngine.cs` | 替换为新实现 |
| `UTF.Core/ITestEngine.cs` | 拆分为 3 个接口 |
| `UTF.Core/TestPlanModels.cs` | 模型统一后删除 |
| `UTF.Core/TestStepResultData.cs` | 模型统一后删除 |
| `UTF.Core/TestEventArgs.cs` | 替换为新事件模型 |
| `UTF.Core/DeviceModels.cs` | 评估后决定 |
| `UTF.Business/TestOrchestrator.cs` | `[Obsolete]`，1100+ 行 |
| `UTF.Business/ITestOrchestrator.cs` | `[Obsolete]` 接口 |

### 6.2 保留并重构

| 文件 | 动作 |
|------|------|
| `UTF.Core/Caching/ICache.cs` + `MemoryCache.cs` | 保留 |
| `UTF.Core/ObjectPool/` | 保留 |
| `UTF.Core/Validation/ValidationHelper.cs` | 保留，补充 ResponseValidator |
| `UTF.Core/OptimizationKit.cs` | 保留 |
| `UTF.Core/IPluginService.cs` | 保留 |
| `UTF.Core/IConfigurationService.cs` | 保留，补充实现 |
| `UTF.HAL/DUTCommunicationHelper.cs` | 重构为 SerialCommandExecutor 内部 |
| `UTF.HAL/ICommunicationChannel.cs` | 保留 |

## 7. DI 注册补全

### AddUtfCore()

```csharp
public static IServiceCollection AddUtfCore(this IServiceCollection services)
{
    // 缓存
    services.AddSingleton<ICache>(...);
    // 日志
    services.AddSingleton<ILogger>(...);
    // 验证
    services.AddSingleton<IResponseValidatorFactory, ResponseValidatorFactory>();
    services.AddSingleton<IResponseValidator, ContainsValidator>();
    services.AddSingleton<IResponseValidator, EqualsValidator>();
    services.AddSingleton<IResponseValidator, RegexValidator>();
    services.AddSingleton<IResponseValidator, NotContainsValidator>();
    // 命令执行工厂
    services.AddSingleton<ICommandExecutorFactory, CommandExecutorFactory>();
    // 测试执行
    services.AddSingleton<ITestExecutor, TestExecutor>();
    // 会话管理
    services.AddSingleton<ITestSessionManager, TestSessionManager>();
    // 结果存储
    services.AddSingleton<ITestResultStore, InMemoryTestResultStore>();
    return services;
}
```

### AddUtfHAL()（新增）

```csharp
public static IServiceCollection AddUtfHAL(this IServiceCollection services)
{
    services.AddSingleton<ICommandExecutor, SerialCommandExecutor>();
    services.AddSingleton<ICommandExecutor, NetworkCommandExecutor>();
    services.AddSingleton<ICommandExecutor, ProcessCommandExecutor>();
    return services;
}
```

### AddUtfPlugins()（新增）

```csharp
public static IServiceCollection AddUtfPlugins(this IServiceCollection services)
{
    services.AddSingleton<StepExecutorPluginHost>();
    services.AddSingleton<ICommandExecutor, PluginCommandExecutor>();
    return services;
}
```

### AddUtfVision()（新增）

```csharp
public static IServiceCollection AddUtfVision(this IServiceCollection services)
{
    services.AddSingleton<ICommandExecutor, VisionCommandExecutor>();
    return services;
}
```

### AddUtfBusiness()

```csharp
public static IServiceCollection AddUtfBusiness(this IServiceCollection services)
{
    services.AddSingleton<IDeviceRegistry, DeviceRegistry>();
    return services;
}
```

### AddUtfUI()

```csharp
public static IServiceCollection AddUtfUI(this IServiceCollection services)
{
    services.AddSingleton<DUTMonitorManager>();  // 瘦身后的纯 UI 状态管理
    services.AddSingleton<ConfigurationManager>();
    services.AddSingleton<IConfigurationAdapter, ConfigurationAdapter>();
    services.AddTransient<MainWindow>();
    services.AddTransient<ConfigurationCenterWindow>();
    services.AddTransient<TestPlanEditorWindow>();
    services.AddTransient<DUTTestListWindow>();
    return services;
}
```

## 8. DUTMonitorManager 瘦身

### 重构前（~1350 行）

- 加载 DUT 配置 → 生成 DUTMonitorItem
- 生成 DataGrid 动态列
- 执行测试（串口/网络/ADB/SCPI/插件）
- 验证响应
- 更新 UI 状态

### 重构后（~300 行）

仅保留：
1. `LoadDUTConfigurationAsync()` — 从配置生成 `ObservableCollection<DUTMonitorItem>`
2. `GenerateDynamicColumnsAsync()` — DataGrid 列生成
3. `StartAllTestsAsync()` — 调用 `ITestExecutor.ExecuteAllStepsAsync()`，订阅 `StepCompleted` 事件更新 UI
4. UI 状态更新方法（通过 `Dispatcher.Invoke`）

所有执行/通信/验证逻辑移至 Core/HAL 层。

## 9. Reporting 接入真实数据

### 重构方案

1. `ReportGenerator` 注入 `ITestResultStore`
2. 删除所有模拟数据生成代码
3. `GenerateReport()` 从 `ITestResultStore` 读取真实 `TestSessionResult`
4. 保留现有报告模板和格式化逻辑

```csharp
public class ReportGenerator
{
    private readonly ITestResultStore _resultStore;

    public async Task<Report> GenerateReportAsync(string sessionId)
    {
        var results = await _resultStore.GetAllResultsAsync(sessionId);
        // 用真实数据填充报告模板
    }
}
```

## 10. Vision 集成

### 重构方案

1. `VisionManager` 配置合并到 `unified-config.json` 的 `VisionSettings` 节
2. 创建 `VisionCommandExecutor : ICommandExecutor`
   - `CanHandle("vision", _)` → true
   - `ExecuteAsync` 调用 `VisionManager` 执行视觉检测
3. 测试步骤中 `Type: "vision"` 即可触发
4. 删除 `SimulatedVisionSystem`，保留真实视觉算法接口

## 11. 新增文件结构

```
UTF.Core/
├── Commands/
│   ├── ICommandExecutor.cs
│   ├── ICommandExecutorFactory.cs
│   ├── CommandExecutorFactory.cs
│   ├── CommandRequest.cs
│   └── CommandResult.cs
├── Execution/
│   ├── ITestExecutor.cs
│   ├── TestExecutor.cs
│   └── StepProgressInfo.cs
├── Session/
│   ├── ITestSessionManager.cs
│   ├── TestSessionManager.cs
│   └── TestSessionStatus.cs
├── Models/
│   ├── TestStepConfig.cs
│   ├── StepExecutionResult.cs
│   └── TestSessionResult.cs
├── Reporting/
│   ├── ITestResultStore.cs
│   └── InMemoryTestResultStore.cs
├── Validation/
│   ├── IResponseValidator.cs
│   ├── IResponseValidatorFactory.cs
│   ├── ResponseValidatorFactory.cs
│   ├── ResponseValidationResult.cs
│   ├── ContainsValidator.cs
│   ├── EqualsValidator.cs
│   ├── RegexValidator.cs
│   ├── NotContainsValidator.cs
│   └── ValidationHelper.cs (已有)
├── Events/
│   └── StepCompletedEventArgs.cs
├── Caching/ (已有，保留)
├── ObjectPool/ (已有，保留)
├── DependencyInjection/
│   └── ServiceCollectionExtensions.cs (重写)
└── OptimizationKit.cs (保留)

UTF.HAL/
├── Commands/
│   ├── SerialCommandExecutor.cs
│   ├── NetworkCommandExecutor.cs
│   └── ProcessCommandExecutor.cs
├── DependencyInjection/
│   └── ServiceCollectionExtensions.cs (新增)
├── IDevice.cs (保留)
├── IDUT.cs (保留)
├── IInstrument.cs (保留)
└── ICommunicationChannel.cs (保留)

UTF.Plugin.Host/
├── Commands/
│   └── PluginCommandExecutor.cs (新增)
├── DependencyInjection/
│   └── ServiceCollectionExtensions.cs (新增)
└── StepExecutorPluginHost.cs (保留)

UTF.Vision/
├── Commands/
│   └── VisionCommandExecutor.cs (新增)
├── DependencyInjection/
│   └── ServiceCollectionExtensions.cs (新增)
└── VisionManager.cs (重构)

UTF.Business/
├── IDeviceRegistry.cs (新增，从 DeviceManager 提取)
├── DeviceRegistry.cs (新增)
├── DependencyInjection/
│   └── ServiceCollectionExtensions.cs (重写)
└── DeviceManager.cs (删除或重构为 DeviceRegistry)

UTF.Reporting/
├── ReportGenerator.cs (重构，接入 ITestResultStore)
└── ... (保留现有模板)
```

## 12. 实施阶段

### Phase 1: 基础设施（接口 + 模型 + 验证）
1. 创建 UTF.Core/Models/ 统一模型
2. 创建 UTF.Core/Commands/ 接口
3. 创建 UTF.Core/Validation/ 响应验证器
4. 创建 UTF.Core/Execution/ 执行器接口
5. 创建 UTF.Core/Session/ 会话管理接口
6. 创建 UTF.Core/Reporting/ 结果存储接口
7. 创建 UTF.Core/Events/ 事件模型

### Phase 2: 命令执行器实现
1. UTF.HAL/Commands/ — Serial, Network, Process 执行器
2. UTF.Plugin.Host/Commands/ — Plugin 执行器
3. UTF.Vision/Commands/ — Vision 执行器
4. UTF.Core/Commands/CommandExecutorFactory 实现

### Phase 3: 核心引擎实现
1. UTF.Core/Execution/TestExecutor 实现
2. UTF.Core/Session/TestSessionManager 实现
3. UTF.Core/Reporting/InMemoryTestResultStore 实现

### Phase 4: DI 注册 + 废弃代码清理
1. 重写各层 ServiceCollectionExtensions
2. 删除所有废弃文件（见第 6 节）
3. 更新 App.xaml.cs 启动流程

### Phase 5: UI 层瘦身
1. DUTMonitorManager 重构（移除执行逻辑，订阅事件更新 UI）
2. MainWindow 适配新接口
3. ConfigurationCenterWindow / TestPlanEditorWindow 适配

### Phase 6: Reporting + Vision 集成
1. ReportGenerator 接入 ITestResultStore
2. VisionManager 配置合并
3. 删除模拟数据代码

### Phase 7: 编译验证
1. 全量编译
2. 修复编译错误
3. 运行验证
