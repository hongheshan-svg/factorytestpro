# 快速迁移指南

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
    private readonly TestOrchestrator _orchestrator;

    public DUTMonitorManager(..., TestOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public async Task ExecuteTestAsync(TestStep step, string dutId)
    {
        var result = await _orchestrator.ExecuteStepWithRetryAsync(step, dutId);
        // 只负责UI更新
        UpdateUI(result);
    }
}
```

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
services.AddTransient<IRetryPolicy, FixedDelayRetryPolicy>();
```

### 4. 自定义验证规则

```csharp
public class RangeValidator : ITestValidator
{
    public ValidationResult Validate(string actual, string expected, string? rule)
    {
        if (rule == "range")
        {
            // expected格式: "range:10-20"
            var parts = expected.Split(':')[1].Split('-');
            var value = double.Parse(actual);
            var min = double.Parse(parts[0]);
            var max = double.Parse(parts[1]);
            return new ValidationResult(
                value >= min && value <= max,
                $"值 {value} 不在范围 [{min}, {max}]"
            );
        }
        return new ValidationResult(true);
    }
}
```

## 编译验证

```bash
dotnet build UTF.Core/UTF.Core.csproj
dotnet build UTF.Configuration/UTF.Configuration.csproj
dotnet build UTF.Plugin.Abstractions/UTF.Plugin.Abstractions.csproj
```
