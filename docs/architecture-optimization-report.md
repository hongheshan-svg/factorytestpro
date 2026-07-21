# UTF 架构优化实施报告

## 已完成优化

### ✅ 优化1：完善依赖注入（1天）

**新增服务注册：**
- `IResourcePool` → `OptimizedResourcePool` (Singleton)
- `ITestSessionManager` → `TestSessionManager` (Singleton)
- `IDUTScheduler` → `DUTScheduler` (Singleton)
- `ITestExecutor` → `TestExecutor` (Transient)
- `ITestValidator` → `TestValidator` (Transient)
- `IRetryPolicy` → `ExponentialBackoffRetryPolicy` (Transient)
- `TestOrchestrator` (Transient)

**收益：**
- 完整的依赖注入支持
- 便于单元测试和模块替换
- 清晰的服务生命周期管理

---

### ✅ 优化2：插件系统增强（部分）

**新增接口：**
```
UTF.Plugin.Abstractions/
├── IPlugin.cs                    # 插件基础接口
├── IDeviceDriverPlugin.cs        # 设备驱动插件
└── IStepExecutorPlugin.cs        # 已存在，测试步骤执行插件

UTF.Core/Plugins/
└── IPluginContainer.cs           # 插件容器接口
```

**扩展点：**
- 支持设备驱动插件化
- 插件健康检查机制
- 类型化插件查询

**待实现：**
- `IReportGeneratorPlugin` - 报告生成插件
- `IDataAnalyzerPlugin` - 数据分析插件
- `IVisionAlgorithmPlugin` - 视觉算法插件
- `PluginContainer` 具体实现

---

### ✅ 优化3：配置系统解耦

**新增抽象层：**
```
UTF.Configuration/Abstractions/
├── IConfigurationProvider<T>     # 配置提供者
├── IConfigurationSerializer      # 配置序列化器
└── IConfigurationValidator<T>    # 配置验证器

UTF.Configuration/Models/
├── SystemConfig.cs               # 系统配置模型
├── DUTConfig.cs                  # DUT配置模型
└── TestConfig.cs                 # 测试配置模型

UTF.Configuration/Providers/
└── FileConfigurationProvider<T>  # 文件配置提供者

UTF.Configuration/Serializers/
└── JsonConfigurationSerializer   # JSON序列化器
```

**收益：**
- 配置源可扩展（文件/数据库/远程）
- 独立的配置模型
- 配置验证标准化

---

### ✅ 优化4：测试引擎职责分离

**新增核心组件：**
```
UTF.Core/Execution/
├── ITestExecutor.cs              # 测试执行器接口
└── TestExecutor.cs               # 测试执行器实现

UTF.Core/Validation/
├── ITestValidator.cs             # 测试验证器接口
└── TestValidator.cs              # 测试验证器实现

UTF.Core/Retry/
├── IRetryPolicy.cs               # 重试策略接口
└── ExponentialBackoffRetryPolicy.cs  # 指数退避重试策略

UTF.Core/Orchestration/
└── TestOrchestrator.cs           # 测试编排器
```

**职责划分：**
- `ITestExecutor` - 单步执行
- `ITestValidator` - 结果验证（支持 equals/contains/regex/notcontains）
- `IRetryPolicy` - 重试策略（指数退避）
- `TestOrchestrator` - 流程编排

**收益：**
- 单一职责原则
- 可替换的验证规则
- 可配置的重试策略

---

## 架构改进对比

### 改进前
```
DUTMonitorManager (700行)
├── 监控UI更新
├── 测试执行
├── 结果验证
├── 重试逻辑
└── 插件调用

ConfigDrivenTestEngine
├── 执行
├── 验证
└── 重试
```

### 改进后
```
DUTMonitorManager
└── 监控UI更新

TestOrchestrator
├── 流程编排
└── 调用执行器/验证器

ITestExecutor → 单步执行
ITestValidator → 结果验证
IRetryPolicy → 重试策略
```

---

## 使用示例

### 1. 依赖注入使用
```csharp
public class MyService
{
    private readonly ITestExecutor _executor;
    private readonly ITestValidator _validator;

    public MyService(ITestExecutor executor, ITestValidator validator)
    {
        _executor = executor;
        _validator = validator;
    }
}
```

### 2. 配置加载
```csharp
var serializer = new JsonConfigurationSerializer();
var provider = new FileConfigurationProvider<DUTConfig>("config/dut.json", serializer);
var config = await provider.LoadAsync();
```

### 3. 测试编排
```csharp
var orchestrator = serviceProvider.GetRequiredService<TestOrchestrator>();
var result = await orchestrator.ExecuteStepWithRetryAsync(step, dutId);
```

---

## 下一步建议

### 短期（1周内）
1. 实现 `PluginContainer` 具体类
2. 迁移 `DUTMonitorManager` 使用 `TestOrchestrator`
3. 添加配置验证器实现

### 中期（2-4周）
1. 实现报告生成插件接口
2. 添加事件总线（IEventBus）
3. 实现数据持久化层

### 长期（1-3月）
1. HAL层通信抽象
2. 设备自动发现
3. 分布式测试支持

---

## Architecture truth (2026-07 audit)

This section records what the code **actually does today**, correcting dual-engine / dual-path narratives that lingered in older docs.

### Dual path (still true after legacy cleanup)

| Path | Role | Status |
|------|------|--------|
| **Desktop production** | `ConfigurationManager` → **`DUTMonitorManager`** → **`ConfigDrivenTestEngine`** | **Current UI test-run entry** |
| **Core session API** | **`ConfigDrivenTestOrchestrator`** → `ConfigDrivenTestEngine` | Registered in DI; preferred for headless/shared sessions; **UI migration is Phase B** |

- There is **one** step engine: `ConfigDrivenTestEngine` (also `IStepExecutionService`). `OptimizedTestEngine` / `ITestEngine` are **deleted** — do not reintroduce or document them as live types.
- Result validation single source of truth: `UTF.Plugin.Abstractions.ExpectedResultMatcher` (thin re-export: `UTF.Core.Validation.ExpectedResultMatcher`). Prefixes: `contains:` / `equals:` / `regex:` (2s timeout) / `notcontains:` / bare text = contains.

### Phase A (止血) fixes applied

1. **`ConfigDrivenTestEngine.ValidateExpectedPattern`** delegates to `ExpectedResultMatcher.Match(..., out reason)` — no more bare `Regex.IsMatch` without timeout on the Expected prefix path.
2. **`--skip-login` / `/skip-login`** is **DEBUG-only** (`#if DEBUG`); Release logs and still shows `LoginWindow`. `IPermissionManager.SignInAsDevelopmentUser` prefers existing SuperAdmin/Admin, else in-memory SuperAdmin `dev`.
3. Docs (`AGENTS.md`, `CLAUDE.md`, this section) state the real entry path and kill the dual-engine narrative; XML docs on `DUTMonitorManager` and `ConfigDrivenTestOrchestrator` annotate production vs preferred roles.

### Not Phase A (deferred)

- Phase B: migrate desktop UI from `DUTMonitorManager` onto `ConfigDrivenTestOrchestrator`.
- Phase C: headless CLI host project.

---

## 2026-07 全面架构优化（本批次）

本轮针对全代码库（约 32K 行）的 4 份并行深度审计结果，分 6 个阶段实施，重点关注安全、稳定性、死代码清理与架构收敛。

### 安全加固
- **插件加载**：`StepExecutorPluginHost` 现强制要求 manifest `sha256`（`PLG002`），并校验 `EntryAssembly` 不逃逸清单目录（`PLG001` 路径遍历防护）；扫描收敛到 `plugins/<id>/<version>/` 两层结构，不再递归任意深度。`UTFF_ALLOW_UNSIGNED_PLUGINS=1` 仅限测试/开发放行。
- **Shell 注入**：`CmdStepExecutorPlugin` 与 `DUTCommunicationHelper` 改用 `ProcessStartInfo.ArgumentList` 逐参数追加，不再字符串拼接 `-Command`。
- **权限系统**：`PermissionManager.HasPermission` 不再恒返回 `true`，改为按当前用户角色 + 自定义权限真实判定；DI 由 `Transient` 改 `Singleton`，`MainWindow` 改构造注入，`_users` 改 `ConcurrentDictionary`。
- **配置秘钥**：`FileAuditLog`、`FileTestResultRepository` 修复并发覆盖与缺省路径；`DynamicConfigurationProvider` 异步 void 改为防抖 + try/catch。

### 并发与稳定性
- `UTF.Core.Caching.MemoryCache`：删除 `ReaderWriterLockSlim` 读路径写操作的死锁隐患，`GetOrCreateAsync` 改用 `Lazy<Task<T>>` 保证单次工厂执行。
- `UTF.Core.Events.EventBus`：内部 `List<Delegate>` 改 `ImmutableList<Delegate>` 原子替换；`PublishAsync` 改并行派发，单 handler 异常不阻塞其他。
- `ConfigDrivenTestOrchestrator`：停止 fire-and-forget 会话完成，加 `ContinueWith(OnlyOnFaulted)` 兜底 + `WaitForSessionAsync(sessionId)`；会话状态写入全部经 `_orchestrationLock` 串行化。
- `ConfigDrivenTestEngine.BuildWorkingContext`：永远复制调用方字典，消除跨步骤上下文污染。
- `IDisposable` 同步阻塞全面修复：`StepExecutorPluginHost`、`DeviceDriverPluginBase`、`VisionManager`、`SimulatedVisionSystem`、`DeviceManager` 均改 `IAsyncDisposable`，同步 `Dispose` 加硬超时放弃，不再 `.GetAwaiter().GetResult()` / `.Wait()` 阻塞。
- `async void` 事件处理器统一包 try/catch + 日志；`.Result` 改 `await`。

### 死代码与并行栈收敛
- 删除 `OptimizedTestEngine`（模拟实现却注册为 `ITestEngine`）、`Orchestration/TestOrchestrator`、`Execution/TestExecutor`、`Validation/TestValidator` 及对应 DI 注册。
- 删除零引用的 `ConfigDrivenTestAdapter`、`ConfigDrivenTestAnalyzer`、`ConfigDrivenReportGenerator`、`TestStepResultData`、`TestPlanModels`。
- 删除 UI 死窗口：`LoginWindow`、`DUTTestListWindow`、`DeviceListWindow`、`DeviceScanProgressWindow`、`DUTTestCard`、`MainWindow.xaml.cs.backup`；`UTF.UI.csproj` 清理 5 个不存在的 `<Compile Remove>`。
- 配置栈合并：删除 `JsonConfigurationProvider` 及内联 DTO；保留 `Abstractions/`+`Providers/`+`Models/`+`Validators/` 单一栈；`IFileConfigurationProvider<T>` 重命名消歧。
- 日志栈：`ILogger` 加 `IDisposable`，`FileLogWriter` 改长驻 `FileStream` + 按大小滚动，`CreateScopedLogger` 共享父队列。
- 本地化：删除空实现的 `LocalizationService`/`LocalizationHelper`，统一走 `LanguageManager`。

### 架构收敛
- `ExpectedResultMatcher`（位于 `UTF.Plugin.Abstractions`，`UTF.Core.Validation` 转发）成为 `contains:`/`equals:`/`regex:`/`notcontains:` 前缀匹配的唯一真相，消除 4 处重复实现（`ConfigDrivenTestEngine`、`CmdStepExecutorPlugin`、`DUTMonitorManager`、`ConfigurationAdapter` 全部委托调用，补齐缺失的 `notcontains:`）。
- `CanHandle` 语义统一为类型 AND 通道匹配（`"*"` 作通配），消除 driver 与 adapter 间 OR/AND 不一致导致的幻影 "no matching plugin" 错误。
- `ConfigDrivenTestEngine` 注入 `IRetryPolicy` 替换内联 `Task.Delay(1000)` 魔数；`ExponentialBackoffRetryPolicy` 加 `maxDelay` cap。
- `pack-plugins.ps1` 仅拷贝插件私有依赖，不再整目录拷贝共享 `UTF.*.dll`，避免插件 ALC 类型身份断裂。
- HAL `IDevice`/`IDeviceFactory`/`IDeviceDiscovery`/`ICommunicationChannel` 标记 `[Obsolete]`，统一走 `UTF.Plugins.Drivers` 插件栈。
- `UTF.Vision` 全部模拟类标记 `[Obsolete("Simulation only")]`，`VisionImage` 改 `ArrayPool<byte>`。

### 报表真实化
- `ReportGenerator` HTML 修复 `<tr>` 全替换 bug，绑定 `{{SessionId}}` 等占位符从 `dataSet`；`ChartGenerator` 移除随机/硬编码数据改消费 `dataSet`；GDI `SolidBrush`/`Pen` 全 `using`；`DataAnalyzer.CalculateMTTR` 不再 `new Random()` 伪造。
- PDF 路径显式抛 `NotSupportedException`（手写 PDF 偏移错误已知），从 `SupportedFormats` 移除，待接入 QuestPDF。

### 构建与 CI
- `Directory.Build.props`：启用 `NuGetAudit`、`AnalysisLevel=latest-recommended`、`EnforceCodeStyleInBuild`；`CS1591`/`CS0618` 全局降级（迁移期不阻塞）。
- 新增 `.editorconfig`（C# 命名/格式规则）。
- 新增 `.github/workflows/ci.yml`（`windows-latest`，restore/build/test 三步）。
- `TreatWarningsAsErrors` 暂未开启（仍有 ~490 个分析器警告待逐项修复），列入后续清理。

### 测试
- `tests/UTF.Core.Tests` 新增 `CriticalRegressionTests.cs`（7 个回归测试覆盖 `ExpectedResultMatcher`、`MemoryCache` 并发、`EventBus` 并行、`PermissionManager` 真实判定、路径遍历拒绝等）。
- 测试夹具在 `StepExecutorPluginHostTests` 构造函数设置 `UTFF_ALLOW_UNSIGNED_PLUGINS=1` 以适配 SHA-256 强制校验。
- 当前 79/79 通过。

### 仍待完成（后续批次）
- **UI 全量 MVVM 迁移**：`CommunityToolkit.Mvvm` 引入与各窗口 ViewModel 落地（本轮仅完成 `PermissionManager` 安全与 DI 修复，未引入 ViewModel）。
- **业务逻辑下沉**：`DUTMonitorManager` 中 ~436 行命令执行逻辑迁入 `UTF.Business` 的 `IStepExecutionEngine`。
- **新测试项目**：`tests/UTF.Business.Tests`、`UTF.Plugin.Host.Tests`、`UTF.Reporting.Tests`、`UTF.Configuration.Tests`、`UTF.UI.Tests`。
- **Central Package Management**：`Directory.Packages.props` 统一包版本。
- **`TreatWarningsAsErrors`**：逐项消除 490 个分析器警告后开启。
- **SemVer 插件版本比较**：`CompareVersions` 升级为 `NuGet.Versioning.SemanticVersion`。

## Analyzer Warning Triage (2026-07)

本轮在配置层（不改动任何 `.cs` 源码）完成两件事：开启 Central Package Management，并对 ~982 个 `latest-recommended` 分析器警告做 `.editorconfig` 严重性分级，使构建信号聚焦到真正有价值的缺陷上。

### Central Package Management（已开启）
- 新增 `Directory.Packages.props`（仓库根），`ManagePackageVersionsCentrally=true`、`CentralPackageTransitivePinningEnabled=true`。
- 全部 9 个唯一包统一在此声明版本；所有 `.csproj` 的 `<PackageReference>` 已移除 `Version` 属性。
- 版本决策：`Microsoft.Extensions.*`（DI / Hosting / Logging.Abstractions）由 9.0.0 统一升级到 **10.0.0**（对齐 net10.0 TFM）；`System.IO.Ports` 由 `UTF.HAL` 的 10.0.0 与 `UTF.Plugins.Drivers` 的 `9.*` 统一为 **10.0.0**；`System.Drawing.Common` 锁 10.0.0；`CommunityToolkit.Mvvm` 8.4.0；测试栈 `Microsoft.NET.Test.Sdk` 17.14.1 / `xunit` 2.9.3 / `xunit.runner.visualstudio` 2.8.2（取恢复图实际解析版本）；`NSubstitute` 5.3.0。
- `dotnet restore UniversalTestFramework.sln` 通过；`project.assets.json` 确认直接依赖不再内嵌版本号（全部走中心版本）。
- 注：在执行过程中并行 track 又新增了 `tests/UTF.Business.Tests`、`UTF.Configuration.Tests`、`UTF.Plugin.Host.Tests`、`UTF.Reporting.Tests`、`UTF.UI.Tests`，这些 csproj 的 `Version` 属性也已被（各 track 自行）移除，与 CPM 一致。

### 分析器警告分级（`.editorconfig`）
全量重建警告数从 **508 → 18**（减少 96.5%）。分级原则如下：

**降级为 `none`（噪声型风格规则）**
- `CA1707`（名称中下划线）：本仓库约定 `_camelCase` 私有字段（见 `.editorconfig` 的 `private_fields` 命名规则），CA1707 对几乎每个字段/测试方法都误报，全项目禁用。
- `CA1305` / `CA1304` / `CA1311` / `CA1310`（区域敏感字符串操作）：本应用为 Windows-only WPF 测试框架，不变文化转换另列清理批次。
- `CA1860`（冗余 null/空比较）、`CA1805`（冗余默认初始化）：高误报量，降级。

**降级为 `suggestion`（规则成立但噪声大，IDE 可见、不进构建输出）**
- `CA1822`（可改 static）、`CA1869`（缓存 `JsonSerializerOptions`）、`CA1854`/`CA1862`/`CA1861`（冗余代码）、`CA1852`（密封内部类型）、`CA1716`/`CA1711`（命名）、`CA1816`（Dispose 调 GC.SuppressFinalize，前轮已处理）、`CA1510`/`CA1513`（空 catch，前轮已处理）、`CA1859`/`CA1840`（性能/接口）。

**保留为 `warning`（真正有价值的缺陷信号）**
- `CA2016`（未转发 `CancellationToken`）— 10 处，真实的取消传播缺陷。
- `CA1001`（持有 `IDisposable` 字段但类型未实现 `IDisposable`）— 6 处，真实资源泄漏。
- `CA1806`（未使用 `TryParse` 返回值）— 4 处，真实逻辑缺陷。
- `CS8620`/`CS8604`/`CS8633`/`CS8601`（可空引用流分析）— 共 16 处，真实空引用风险，分布在 `UTF.Logging`/`UTF.Core`/`UTF.Vision`。

### `TreatWarningsAsErrors` 状态
**仍为关闭**。当前剩余 18 个 warning 均为有价值的真实信号，但需在源码层逐项修复后才能开启 `TreatWarningsAsErrors`（这些 `.cs` 文件由其他 track 所有，本轮不触碰源码）。目标：当上述保留 warning 清零后开启 `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`，使 CI 真正强制零警告。

### 验证
- `dotnet restore UniversalTestFramework.sln`：成功。
- `dotnet build UniversalTestFramework.sln -c Debug --no-incremental`：**0 errors，18 warnings**（保留的 CA2016/CA1001/CA1806/CS86xx）。
- `dotnet test tests/UTF.Core.Tests/UTF.Core.Tests.csproj`：**79/79 通过**。


---

## 2026-07 后续批次收尾（本批次）

承接上一批次"仍待完成"清单，本轮通过 3 个并行 agent + 集成阶段全部落地。最终状态：**全量重建 0 错误 / 0 警告（`TreatWarningsAsErrors=true` 已启用），93/93 测试通过**。

### 1. 分析器警告清零 + `TreatWarningsAsErrors`
- 修复全部 20 个真实警告（10 个唯一位置）：
  - **CA2016（5 处）**：`UTF.Business/DeviceManager.cs` 三处 `ICache.SetAsync/RemoveAsync/GetOrCreateAsync`、`UTF.Plugins.Example/CmdStepExecutorPlugin.cs` 两处 `ReadToEndAsync` 全部转发 `cancellationToken`。
  - **CS8620/CS8604（6 处）**：`UTF.Vision/Algorithms/MeasurementAlgorithm.cs` 与 `ObjectDetectionAlgorithm.cs` 的 `Dictionary<string,object>` 改 `TryGetValue` 显式取值 + `?? string.Empty`/`?? "circle"` 源头规范化，消除下游可空污染。
  - **CS8633**：`UTF.Logging/MicrosoftExtensionsLoggerAdapter.cs` 移除冲突的 `where TState : notnull` 约束，改显式接口实现 `void ILogger.Log<TState>(...)` 委托到公共方法。
  - **CS8601**：`UTF.Core/ConfigDrivenTestOrchestrator.cs:171` `Id = projectId ?? string.Empty`。
  - **CA1001（2 处）**：`FileAuditLog` 与 `UTF.UI/Services/ConfigurationManager` 实现 `IDisposable`，释放 `_fileLock`（SemaphoreSlim）。
  - **CS8600**：`ConfigurationCenterWindow.xaml.cs:293` `(Brush)BrushConverter.ConvertFromString(...)` 改 `as Brush ?? Brushes.Green`。
  - **CA1806**：`QuickTestWizardWindow.xaml.cs:271` `int.TryParse` 加返回值检查 + 友好提示。
  - **CS8603（额外）**：`StringToNullableIntConverter.cs:33` `return null!`（WPF ConvertBack 约定）。
- `Directory.Build.props` 启用 `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`（`NoWarn` 仅保留 `CS1591`/`CS0618`）。
- 全量 `--no-incremental` 重建：**0 错误 / 0 警告**。

### 2. UI MVVM 迁移完成
- **权限门控进 VM**：`MainWindowViewModel` 新增 12 个 `[ObservableProperty]` 权限标志（`CanStartTest`/`CanStopTest`/`CanImportConfig`/`CanExportReport`/`CanClearLogs`/`CanRetestDut`/`CanConfigureSystem`/`CanManageTestPlans`/`CanCreateTestPlan`/`CanEditTestPlan`/`CanManageDevices`/`CanManageUsers`），`RefreshPermissions()` 查 `IPermissionManager.HasPermission`，订阅 `PermissionChanged` 事件，`[NotifyCanExecuteChangedFor]` 联动命令。`MainWindow.xaml` 所有菜单项 + 3 个工具栏按钮绑定 `IsEnabled="{Binding CanXxx}"`，`ApplyPermissions` 改委托 VM。
- **Click -> Command**：`RefreshDUTs` 按钮转为 `Command` 绑定；其余 13 个 `Click` 保留（理由：Start/Stop 需组合 IsTestRunning+permission+按钮文本、Import/Export 涉及文件对话框、各 Open* 涉及 RequirePermission+Owner+ShowDialog，转为纯 Command 风险大于收益）。所有保留的 handler 仍调用 VM 命令。
- **ConfigurationCenter 双向绑定**：9 个字段（`ResultsPath`/`AutoSaveResults`/`ProductName`/`ProductModel`/`ProductCategory`/`MaxConcurrent`/`TestTimeout`/`RetryCount`/`NamingTemplate`/`NamingIdTemplate`）改为 TwoWay 绑定 `Config.X.Y.Z`；新增 `StringToNullableIntConverter`。ComboBox/LBox/DataGrid 仍手动管理（ComboBoxItem 字符串映射、ObservableCollection 生命周期，绑定风险高）。`EnsureNestedObjects` 防止绑定路径命中 null。
- **QuickTestWizard 双向绑定**：6 个控件（ProductName/ProductModel/DutCount/UseSerial/UseNetwork/SaveAsDefault/ExportCopy）TwoWay 绑定 VM 属性。图标/类别 ComboBox 与步骤构造输入仍留 code-behind（可编辑 ComboBox 提取逻辑 + 每步添加流程）。
- **诚实记录**：`IDialogService` 抽象跳过（无 VM 直接调 MessageBox，引入需穿透 3 个 VM 构造函数，无行为变更）。

### 3. `StepExecutionService` 迁至 `UTF.Business`
- 新建 `UTF.Business/IStepExecutionService.cs` + `StepExecutionService.cs`（命名空间 `UTF.Business`，实现完全一致，删除 `// TODO` 注释）。
- 删除 `UTF.UI/Services/IStepExecutionService.cs` + `StepExecutionService.cs`。
- `UTF.UI/DependencyInjection/ServiceCollectionExtensions.cs` 注册改为 `UTF.Business.IStepExecutionService` -> `UTF.Business.StepExecutionService`（UI 组合根注入 Business 服务，分层正确；`UTF.Core` 不能引用 `UTF.Business` 否则循环，注册保留在 UI 组合根）。
- 新增 `tests/UTF.Business.Tests/StepExecutionServiceTests.cs`（3 个烟雾测试：ctor null engine、ctor null logger、ExecuteStepAsync null request），全部通过。

### 最终验证
- 全量重建：`dotnet build UniversalTestFramework.sln -c Debug --no-incremental` -> **0 错误 / 0 警告**（`TreatWarningsAsErrors=true`）。
- 全部测试：`UTF.Core.Tests` 79 + `UTF.Business.Tests` 6 + `UTF.Configuration.Tests` 3 + `UTF.Plugin.Host.Tests` 2 + `UTF.Reporting.Tests` 2 + `UTF.UI.Tests` 1 = **93/93 通过**。
- 本批次共改动 139 个文件（87 改 / 28 删 / 23 新增 / 1 重命名）。

### 仍可继续优化（非阻塞，后续可选）
- UI 13 个 `Click` handler 全转 Command（需 `IDialogService`/`IWindowFactory` 抽象）。
- ConfigurationCenter 剩余 ComboBox/ListBox/DataGrid 全双向绑定。
- QuickTestWizard 图标/类别 ComboBox 与步骤构造输入进 VM。
- `StepExecutionService` 与 `ConfigDrivenTestEngine` 进一步融合（目前是薄包装）。

---

## 2026-07 收尾优化（本批次）

承接上一批次"仍可继续优化"清单的 4 项全部落地。最终状态：**全量重建 0 错误 / 0 警告（`TreatWarningsAsErrors=true`），93/93 测试通过**。

### 1. `IDialogService` + `IWindowFactory` 抽象落地
- 新建 `UTF.UI/Services/IDialogService.cs` + `DialogService.cs`（sealed/stateless，封装 `MessageBox` + `OpenFileDialog`/`SaveFileDialog`）。
- 新建 `UTF.UI/Services/IWindowFactory.cs` + `WindowFactory.cs`（注入 `IServiceProvider`/`IDialogService`/`IPermissionManager`/`DUTMonitorManager`；每个 `Show*Dialog` 先查权限→拒绝则警告返回，否则解析窗口 + 设 Owner + `ShowDialog` + 配置变更时触发 `ConfigurationApplied` 事件）。
- 在 `UTF.UI/DependencyInjection/ServiceCollectionExtensions.cs` 注册为 Singleton。

### 2. 13 个 Click handler 全转 Command
- `ImportConfig`/`ExportReport`/`StartTest`/`StopTest`/`ClearAllLogs`/`OpenConfigCenter`/`OpenQuickTestWizard`/`OpenTestPlanEditor`/`OpenPluginManager`/`OpenDeviceManager`/`OpenUserManager`/`ExitApp`/`Logout` 全部转为 `Command="{Binding XxxCommand}"`。
- `MainWindowViewModel` 注入 `IDialogService` + `IWindowFactory`；新增 `StartTestButtonText`（"▶️ 开始测试"↔"⏸️ 测试进行中..."）、`ExitAppCommand`、`LogoutCommand`、`ClearAllLogsCommand`；`ExportReportCommand` 从占位改为真实调用 `ReportGenerator`；`ImportConfigCommand` 自带 OpenFileDialog；`RefreshConfigurationAfterImportAsync` 迁入 VM。
- Start 按钮的 `Content` 改为绑定 `StartTestButtonText`（取代 code-behind 文本切换）。
- `MainWindow.xaml.cs` 删除 13 个 `*_Click` 方法；保留生命周期/leak-fix/步骤预览/3 个 DUT 上下文菜单 handler（需选中 DUT，留 TODO）。
- 诚实记录：DUT 上下文菜单的 3 个 handler 保留（需 `CommandParameter` 传选中 DUTMonitorItem，按任务规格允许保留）。

### 3. ConfigurationCenter 剩余控件全双向绑定
- `LogLevel`/`Language`/`Theme` 3 个 ComboBox：`ItemsSource` 绑 VM 的 `LogLevelOptions`/`LanguageOptions`/`ThemeOptions`，`SelectedItem` TwoWay 绑 VM 属性。
- `SerialPortsList`/`NetworkHostsList` 2 个 ListBox：`ItemsSource` 绑 VM 的 `ObservableCollection<string>`，`SelectedItem` 用于 Remove。
- `TestStepsGrid` DataGrid：`ItemsSource` 绑 VM 的 `ObservableCollection<TestStepConfig>`，`SelectedItem` 绑 `SelectedStep`。
- 新增命令：`AddSerialPort`/`RemoveSerialPort`/`AddNetworkHost`/`RemoveNetworkHost`/`AddStep`/`RemoveStep`/`MoveStepUp`/`MoveStepDown`/`CopyStep`。
- `PopulateManualFields`/`CollectManualFields` 删除；步骤详情编辑面板保留在 code-behind（react to `SelectedStep` PropertyChanged）以避免 ObservableProperty 爆炸。

### 4. QuickTestWizard 图标/类别/步骤输入进 VM
- `ProductIcon`/`ProductCategory` ComboBox：`ItemsSource` 绑 VM 的 `AvailableIcons`/`AvailableCategories`，`SelectedItem`/`Text` TwoWay。
- `Steps` 集合迁入 VM（`ObservableCollection<WizardStepInput>`）；新增 `SelectedStep`、`NewStepName`/`NewStepCommand`/`NewStepExpectedMode`/`NewStepExpectedValue`/`NewStepTimeout` 输入属性。
- 新增命令：`AddStep`/`RemoveStep`/`MoveStepUp`/`MoveStepDown`。
- 删除 `_wizardSteps` 与 `WizardStepItem` 内部类；`BuildQuickTestWizardInput` 替换为 `_viewModel.BuildInput()`。
- 审查面板（icon/name/model/category/dut-count/steps）全部绑定到 VM。

### 5. `StepExecutionService` 与 `ConfigDrivenTestEngine` 融合
- DTO `StepExecutionRequest`/`StepExecutionResult` + 接口 `IStepExecutionService` 从 `UTF.Business` 迁至 `UTF.Core/StepExecutionContracts.cs`（为避免与 `UTF.Plugin.Abstractions.StepExecutionRequest` 同名冲突，重命名为 `CoreStepExecutionRequest`/`CoreStepExecutionResult`）。
- `ConfigDrivenTestEngine` 声明 `: IStepExecutionService, IDisposable`，新增 `ExecuteStepAsync(CoreStepExecutionRequest, CT)` 重载，内部适配请求后委托给既有 `ExecuteStepAsync(ConfigTestStep, ...)`（保留供测试与 orchestrator 使用）。
- 删除 `UTF.Business/IStepExecutionService.cs` + `StepExecutionService.cs`（薄包装不再需要）。
- DI 注册改为 `IStepExecutionService` -> `ConfigDrivenTestEngine`（Transient，匹配引擎生命周期，避免 `ValidateScopes` captive-dependency 错误）。
- `tests/UTF.Business.Tests/StepExecutionServiceTests.cs` 改为通过 `IStepExecutionService` 测试 `ConfigDrivenTestEngine`（null-request/disabled-step-skip/no-plugin-failure 三场景）。

### 最终验证
- 全量重建：`dotnet build UniversalTestFramework.sln -c Debug --no-incremental` -> **0 错误 / 0 警告**。
- 全部测试：UTF.Core 79 + UTF.Business 6 + UTF.Configuration 3 + UTF.Plugin.Host 2 + UTF.Reporting 2 + UTF.UI 1 = **93/93 通过**。
- 本批次共改动 142 个文件（87 改 / 28 删 / 26 新增 / 1 重命名）。

### 仍可继续优化（非阻塞，已最小化）
- 3 个 DUT 上下文菜单 handler 转 parameterized command（需 `CommandParameter` 传选中 DUTMonitorItem）。
- ConfigurationCenter 步骤详情编辑面板（Id/Name/Type/Channel/Command/Expected/Timeout 等 ~10 字段）从 code-behind 迁到 SelectedStep 子属性双向绑定。
- `WindowFactory.TrySetOwner` 用 `Func<Window>` 注入器替代 `Application.Current.MainWindow`。

---

## 2026-07 收尾优化（最终三项）

承接上一批次"仍可继续优化"清单的 3 项全部落地。最终状态：**全量重建 0 错误 / 0 警告（`TreatWarningsAsErrors=true`），93/93 测试通过**。

### 1. DUT 上下文菜单：3 个 Click -> parameterized RelayCommand&lt;DUTMonitorItem&gt;
- `MainWindowViewModel` 新增 `ViewDutLogCommand` / `ViewDutDetailCommand` / `RetestDutCommand`，均接收 `DUTMonitorItem?` 参数；`RetestDutCommand` 用 `[RelayCommand(CanExecute = nameof(CanRetestDut))]` 门控，`_canRetestDut` 加 `[NotifyCanExecuteChangedFor(nameof(RetestDutCommand))]`。
  - `ViewDutLog`：聚合 `DUTMonitorItem.Logs` 经 `_dialogService.ShowInformation` 展示。
  - `ViewDutDetail`：格式化 ID/名称/类型/序列号/状态/步骤序列展示。
  - `RetestDut`：二次校验 `Permission.TestStart`（即便 CanExecute 已门控，仍保留运行时检查）后提示在工具栏启动整体测试会话（DUTMonitorManager 当前仅支持整体重测，避免误触单 DUT 重跑）。
- `MainWindow.xaml` 的 `<ContextMenu>` 改用 Option C 模式：
  - `<ContextMenu DataContext="{Binding PlacementTarget.DataContext, RelativeSource={RelativeSource Self}}">`：将 ContextMenu 的 DC 设为 Border 的 DC（即行 DUTMonitorItem）。
  - `CommandParameter="{Binding}"`：以行 DUTMonitorItem 作为参数。
  - `Command="{Binding DataContext.ViewDutLogCommand, RelativeSource={RelativeSource FindAncestor, AncestorType=Window}}"`：跨过 ContextMenu popup 边界，回主窗口 VM 解析命令。
- `MainWindow.xaml.cs` 删除 `ViewDUTLogBtn_Click` / `ViewDUTDetailBtn_Click` / `RetestDUTBtn_Click` 与上方 TODO 注释块。

### 2. ConfigurationCenter 步骤详情面板：~10 字段 TwoWay 绑定到 SelectedStep 子属性
- `ConfigurationCenterViewModel`：
  - 新增 `[ObservableProperty] private int? _selectedStepMaxRetries;`（读 `SelectedStep.Parameters["MaxRetries"]`）。
  - 新增 `partial void OnSelectedStepChanged(TestStepConfig? value)`：选中步骤切换时同步 MaxRetries 输入框（兼容 int 与字符串存储）。
  - 新增 `ApplyStepMaxRetries()`：将输入框值回写到 `SelectedStep.Parameters["MaxRetries"]`。
  - 新增 `RefreshStepsCommand`：调用 `ApplyStepMaxRetries()` + `OnPropertyChanged(nameof(TestSteps))`，驱动 UI 重绘。
- `ConfigurationCenterWindow.xaml`：
  - 注册 `NullToVisibilityConverter`（新增 `UTF.UI/Converters/NullToVisibilityConverter.cs`：null->Collapsed、非 null->Visible）。
  - `StepDetailPanel.Visibility` 绑定 `SelectedStep` + NullToVisibilityConverter，自动 Collapsed/Visible。
  - 12 个输入控件 TwoWay 绑定：`DetailIdText`->`SelectedStep.Id`、`DetailNameText`->`SelectedStep.Name`、`DetailTypeCombo`/`DetailChannelCombo`/`DetailTargetCombo`->对应 `SelectedStep.Type/Channel/Target`（IsEditable ComboBox 用 `Text` 绑定）、`DetailDescriptionText`->`Description`、`DetailCommandText`->`Command`、`DetailExpectedText`->`Expected`、`DetailTimeoutText`/`DetailDelayText`->`Timeout/Delay`（int? + NullableIntConverter）、`DetailMaxRetriesText`->`SelectedStepMaxRetries`、`DetailContinueOnFailureCheck`->`SelectedStep.ContinueOnFailure`。
  - 原"应用修改"按钮（`Click="ApplyStepDetail_Click"`）改为"刷新列表"按钮（`Command="{Binding RefreshStepsCommand}"`）：因 `TestStepConfig` 未实现 INPC，TwoWay 直接写属性不会自动重绘 DataGrid；按钮触发 `RefreshStepsCommand` 提示 UI 重绘（同一选中行文本在下次选择或刷新前可能滞后，配置保存由"💾 保存配置"统一持久化）。
- `ConfigurationCenterWindow.xaml.cs`：
  - 删除 `ShowStepDetail()` / `ApplyStepDetail_Click(object, RoutedEventArgs)` / `SetComboByContent(ComboBox, string, bool)` 三个方法。
  - 移除 `OnViewModelPropertyChanged` 中 `SelectedStep` 分支（XAML 双向绑定 + NullToVisibilityConverter 自动接管）。

### 3. WindowFactory：注入 `Func&lt;Window?&gt;` owner 解析器
- `WindowFactory.cs`：
  - 构造函数新增 `Func<Window?> ownerResolver` 参数，存入 `_ownerResolver` 字段。
  - `TrySetOwner` 从 `static` 改为实例方法，调用 `_ownerResolver()` 取 owner（null/同窗口则跳过）；删除原 `Application.Current?.MainWindow` 直接访问与 TODO 注释。
- `DependencyInjection/ServiceCollectionExtensions.cs`：
  - `IWindowFactory` 注册从 `AddSingleton<IWindowFactory, WindowFactory>()` 改为 `AddSingleton<IWindowFactory>(sp => new WindowFactory(..., () => Application.Current?.MainWindow as Window))`，将 owner 解析逻辑收口到组合根。
  - 新增 `using System.Windows;`。
- 测试可注入 `() => null` 或 mock 窗口，避免对 `Application.Current` 的隐式依赖。

### 最终验证
- 全量重建：`dotnet build UniversalTestFramework.sln -c Debug --no-incremental` -> **0 错误 / 0 警告**（`TreatWarningsAsErrors=true`）。
- 全部测试：UTF.Core 79 + UTF.Business 6 + UTF.Configuration 3 + UTF.Plugin.Host 2 + UTF.Reporting 2 + UTF.UI 1 = **93/93 通过**。
- 本批次共改动 9 个文件（7 改 / 1 新增 NullToVisibilityConverter / 1 文档追加）。

### 设计取舍
- DUT `RetestDut` 仍为占位提示：`DUTMonitorManager.StartAllTestsAsync` 当前仅支持整体重测（按 `OverallStatus != Running` 筛选候选 DUT）；为避免单 DUT 触发整批运行误判，命令提示用户在工具栏启动整体会话。若后续 `DUTMonitorManager` 提供 `RetestDUTAsync(dutId)` API，命令可直接改为真实调用。
- 步骤详情面板 DataGrid 刷新滞后：因 `TestStepConfig` 不实现 INPC（该模型文件不在本批次编辑范围），TwoWay 绑定回写属性时 DataGrid 当前行文本不自动重绘；以"刷新列表"按钮 + `RefreshStepsCommand` 提示重绘作为最小可接受方案。如需即时刷新，可后续将 `TestStepConfig` 改为实现 INPC 或将 `TestSteps` 集合换为 `BindingList<T>`。

