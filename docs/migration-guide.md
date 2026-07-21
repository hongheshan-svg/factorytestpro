# 快速迁移指南

> **Status: updated 2026-07.** 本指南已对齐 2026-07 重构后的代码：原 `TestOrchestrator` 与 `ITestValidator` 已删除，当前并发入口为 `ConfigDrivenTestOrchestrator` + `ConfigDrivenTestEngine.ExecuteStepAsync`。

## 如何使用新架构

### 1. 更新 App.xaml.cs

现有的 `services.AddUtfCore()` 已自动包含所有新服务，无需修改。

### 2. 迁移 DUTMonitorManager

**原代码（简化）：**
```csharp
// 执行测试步骤
var result = await ExecuteStepAsync(step, dutId);
if (!ValidateResult(result, expected)) {
    // 重试逻辑
}
```

**新代码：**
```csharp
public class DUTMonitorManager
{
    private readonly ConfigDrivenTestOrchestrator _orchestrator;
    private readonly ConfigDrivenTestEngine _engine;

    public DUTMonitorManager(..., ConfigDrivenTestOrchestrator orchestrator, ConfigDrivenTestEngine engine)
    {
        _orchestrator = orchestrator;
        _engine = engine;
    }

    public async Task ExecuteTestAsync(CoreStepExecutionRequest step, string dutId, CancellationToken ct)
    {
        // 步骤执行（包含重试、条件跳过、上下文变量、MockOutput 等内置行为）
        var result = await _engine.ExecuteStepAsync(step, ct);
        // 只负责UI更新
        UpdateUI(result);
    }
}
```

`ConfigDrivenTestEngine` 同时实现 `IStepExecutionService`，因此需要解耦的调用方也可注入 `IStepExecutionService` 接口而非具体实现。`ConfigDrivenTestOrchestrator` 负责并发会话编排与共享会话状态，不应在 VM 层直接调用 `ExecuteStepWithRetryAsync`（该方法已合并入引擎内部）。

### 3. 自定义重试策略

```csharp
// 固定延迟策略
public class FixedDelayRetryPolicy : IRetryPolicy
{
    public bool ShouldRetry(int attemptCount, Exception? ex)
        => attemptCount < 3;

    public TimeSpan GetNextDelay(int attemptCount)
        => TimeSpan.FromSeconds(2);
}

// 注册
services.AddSingleton<IRetryPolicy, FixedDelayRetryPolicy>();
```

### 4. 自定义验证规则

结果验证现在通过 `UTF.Plugin.Abstractions.ExpectedResultMatcher.Match(expected, actual)` 进行（在 `UTF.Core` 中作为 `UTF.Core.Validation.ExpectedResultMatcher` 重新导出）。新增自定义验证应扩展现有的前缀语义（`contains:` / `equals:` / `regex:` / `notcontains:`），或通过配置侧的 `ValidationRules`（`MustContainAll` / `MustNotContainAny` / `Regex` / `NumericRange`）声明，而不是再实现一个独立的 `ITestValidator`。

```csharp
// 在配置中声明扩展校验规则
{
  "ValidationRules": {
    "MustContainAll": ["PASS", "VOLTAGE"],
    "MustNotContainAny": ["ERROR", "FAIL"],
    "NumericRange": { "Min": 3.3, "Max": 4.2 }
  }
}
```

## 编译验证

```bash
dotnet build UTF.Core/UTF.Core.csproj
dotnet build UTF.Configuration/UTF.Configuration.csproj
dotnet build UTF.Plugin.Abstractions/UTF.Plugin.Abstractions.csproj
```
