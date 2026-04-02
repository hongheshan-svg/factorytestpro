# 配置驱动测试模块 - 快速开始指南

## 5 分钟快速上手

### 步骤 1: 准备配置文件

创建或编辑 `config/unified-config.json`：

```json
{
  "TestProjectConfiguration": {
    "TestProject": {
      "Id": "my_first_test",
      "Name": "我的第一个测试",
      "Description": "快速入门示例",
      "Enabled": true,
      "Steps": [
        {
          "Id": "step_001",
          "Name": "版本检查",
          "Order": 1,
          "Enabled": true,
          "Type": "serial",
          "Command": "system_manager version",
          "Expected": "contains:SW_VERSION",
          "Timeout": 5000,
          "Channel": "Serial"
        }
      ]
    }
  }
}
```

### 步骤 2: 编写测试代码

```csharp
using UTF.Core;
using UTF.Logging;

// 创建日志记录器
var logger = LoggerFactory.CreateLogger<Program>();

// 创建测试编排器
var orchestrator = new ConfigDrivenTestOrchestrator(
    "config/unified-config.json",
    logger
);

// 初始化
await orchestrator.InitializeAsync();

// 创建测试会话
var session = await orchestrator.CreateSessionAsync(
    dutIds: new List<string> { "DUT-001" },
    operatorName: "测试员"
);

// 启动测试
await orchestrator.StartSessionAsync(session.SessionId);

// 等待完成
while (session.Status == ConfigTestStatus.Running)
{
    await Task.Delay(1000);
    session = orchestrator.GetSession(session.SessionId);
}

// 输出结果
Console.WriteLine($"测试结果: {(session.OverallPassed ? "✅ PASS" : "❌ FAIL")}");

// 清理
orchestrator.Dispose();
```

### 步骤 3: 运行测试

```bash
dotnet run
```

就这么简单！🎉

---

## 完整示例：生产测试流程

### 配置文件

```json
{
  "ConfigurationInfo": {
    "Name": "生产测试配置",
    "Version": "1.0.0",
    "Description": "完整的生产测试流程"
  },

  "DUTConfiguration": {
    "ProductInfo": {
      "Name": "智能音箱",
      "Model": "Speaker-X1",
      "ExpectedSoftwareVersion": "SW_VERSION:V2.0"
    },
    "GlobalSettings": {
      "DefaultMaxConcurrent": 16,
      "TestTimeout": 300,
      "RetryCount": 3
    },
    "CommunicationEndpoints": {
      "SerialPorts": ["COM3", "COM4", "COM5", "COM6"],
      "NetworkHosts": ["192.168.1.10", "192.168.1.11"]
    }
  },

  "TestProjectConfiguration": {
    "TestProject": {
      "Id": "production_test",
      "Name": "生产测试",
      "Description": "完整的生产测试流程",
      "Enabled": true,
      "Steps": [
        {
          "Id": "step_001",
          "Name": "硬件自检",
          "Order": 1,
          "Enabled": true,
          "Type": "serial",
          "Command": "system_manager selftest",
          "Expected": "contains:PASS",
          "Timeout": 10000,
          "Delay": 1000,
          "Channel": "Serial"
        },
        {
          "Id": "step_002",
          "Name": "软件版本检查",
          "Order": 2,
          "Enabled": true,
          "Type": "serial",
          "Command": "system_manager version",
          "Expected": "contains:SW_VERSION:V2.0",
          "Timeout": 5000,
          "Channel": "Serial"
        },
        {
          "Id": "step_003",
          "Name": "网络连接测试",
          "Order": 3,
          "Enabled": true,
          "Type": "custom",
          "Command": "ping -n 2 www.baidu.com",
          "Expected": "contains:来自",
          "Timeout": 10000,
          "Channel": "Cmd"
        },
        {
          "Id": "step_004",
          "Name": "音频功能测试",
          "Order": 4,
          "Enabled": true,
          "Type": "serial",
          "Command": "audio_test play_and_record",
          "Expected": "contains:audio test pass",
          "Timeout": 30000,
          "Channel": "Serial"
        },
        {
          "Id": "step_005",
          "Name": "WiFi 功能测试",
          "Order": 5,
          "Enabled": true,
          "Type": "serial",
          "Command": "wifi_test scan",
          "Expected": "regex:Found \\d+ networks",
          "Timeout": 15000,
          "Channel": "Serial"
        },
        {
          "Id": "step_006",
          "Name": "蓝牙功能测试",
          "Order": 6,
          "Enabled": true,
          "Type": "serial",
          "Command": "bluetooth_test scan",
          "Expected": "contains:bluetooth ready",
          "Timeout": 10000,
          "Channel": "Serial"
        },
        {
          "Id": "step_007",
          "Name": "LED 指示灯测试",
          "Order": 7,
          "Enabled": true,
          "Type": "serial",
          "Command": "led_test all",
          "Expected": "equals:OK",
          "Timeout": 5000,
          "Channel": "Serial"
        },
        {
          "Id": "step_008",
          "Name": "按键功能测试",
          "Order": 8,
          "Enabled": true,
          "Type": "serial",
          "Command": "button_test all",
          "Expected": "contains:all buttons ok",
          "Timeout": 10000,
          "Channel": "Serial"
        },
        {
          "Id": "step_009",
          "Name": "MAC 地址写入",
          "Order": 9,
          "Enabled": true,
          "Type": "serial",
          "Command": "mac_write auto",
          "Expected": "contains:MAC written",
          "Timeout": 5000,
          "Channel": "Serial"
        },
        {
          "Id": "step_010",
          "Name": "最终验证",
          "Order": 10,
          "Enabled": true,
          "Type": "serial",
          "Command": "final_check",
          "Expected": "contains:ALL PASS",
          "Timeout": 5000,
          "Channel": "Serial"
        }
      ]
    }
  }
}
```

### 完整测试程序

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UTF.Core;
using UTF.Logging;
using UTF.Plugin.Host;

namespace ProductionTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== 生产测试系统 ===");
            Console.WriteLine();

            // 创建日志记录器
            var logger = LoggerFactory.CreateLogger<Program>();

            try
            {
                // 1. 初始化插件系统（可选）
                StepExecutorPluginHost? pluginHost = null;
                if (Directory.Exists("plugins"))
                {
                    pluginHost = new StepExecutorPluginHost("plugins", logger);
                    var pluginReport = await pluginHost.InitializeAsync();
                    logger.Info($"插件加载: 成功 {pluginReport.LoadedCount}, 失败 {pluginReport.FailedCount}");
                }

                // 2. 创建测试编排器
                var orchestrator = new ConfigDrivenTestOrchestrator(
                    "config/unified-config.json",
                    logger,
                    pluginHost
                );

                // 3. 初始化编排器
                if (!await orchestrator.InitializeAsync())
                {
                    logger.Error("编排器初始化失败");
                    return;
                }

                // 4. 获取 DUT 列表（实际应用中可能从硬件扫描获取）
                var dutIds = GetAvailableDuts();
                logger.Info($"检测到 {dutIds.Count} 个 DUT");

                // 5. 创建测试会话
                var session = await orchestrator.CreateSessionAsync(
                    dutIds: dutIds,
                    operatorName: GetOperatorName(),
                    sessionContext: new Dictionary<string, object>
                    {
                        { "BatchNumber", GetBatchNumber() },
                        { "ProductionLine", "Line-A" },
                        { "Shift", GetCurrentShift() }
                    }
                );

                if (session == null)
                {
                    logger.Error("创建测试会话失败");
                    return;
                }

                logger.Info($"测试会话创建成功: {session.SessionId}");

                // 6. 订阅事件
                var completedDuts = 0;
                var totalDuts = dutIds.Count;

                orchestrator.StepCompleted += (sender, e) =>
                {
                    var stepResult = e.Data as ConfigDrivenStepResult;
                    var status = stepResult?.Passed == true ? "✅" : "❌";
                    logger.Info($"[{e.DutId}] {status} {stepResult?.StepName}");
                };

                orchestrator.SessionCompleted += (sender, e) =>
                {
                    logger.Info("所有 DUT 测试完成！");
                };

                // 7. 启动测试
                logger.Info("开始测试...");
                var startTime = DateTime.Now;
                await orchestrator.StartSessionAsync(session.SessionId);

                // 8. 等待完成并显示进度
                while (session.Status == ConfigTestStatus.Running)
                {
                    await Task.Delay(1000);
                    session = orchestrator.GetSession(session.SessionId);

                    var stats = orchestrator.GetSessionStatistics(session.SessionId);
                    if (stats != null)
                    {
                        var progress = (double)stats.CompletedDuts / stats.TotalDuts * 100;
                        Console.Write($"\r进度: {progress:F1}% ({stats.CompletedDuts}/{stats.TotalDuts}) | " +
                                    $"通过率: {stats.PassRate:P2} | " +
                                    $"耗时: {stats.Duration.TotalSeconds:F0}s");
                    }
                }

                Console.WriteLine();
                var endTime = DateTime.Now;
                var totalDuration = endTime - startTime;

                // 9. 显示测试结果
                Console.WriteLine();
                Console.WriteLine("=== 测试结果 ===");
                var finalStats = orchestrator.GetSessionStatistics(session.SessionId);
                if (finalStats != null)
                {
                    Console.WriteLine($"总 DUT 数: {finalStats.TotalDuts}");
                    Console.WriteLine($"通过 DUT: {finalStats.PassedDuts} ({(double)finalStats.PassedDuts / finalStats.TotalDuts:P2})");
                    Console.WriteLine($"失败 DUT: {finalStats.FailedDuts}");
                    Console.WriteLine($"总步骤数: {finalStats.TotalSteps}");
                    Console.WriteLine($"步骤通过率: {finalStats.PassRate:P2}");
                    Console.WriteLine($"总耗时: {totalDuration.TotalSeconds:F2} 秒");
                    Console.WriteLine($"平均每 DUT: {totalDuration.TotalSeconds / finalStats.TotalDuts:F2} 秒");
                }

                // 10. 生成报告
                Console.WriteLine();
                Console.WriteLine("生成测试报告...");
                var reportGenerator = new ConfigDrivenReportGenerator(logger);
                var reportDir = Path.Combine("reports", DateTime.Now.ToString("yyyyMMdd"));
                await reportGenerator.GenerateAllReportsAsync(session, reportDir);
                Console.WriteLine($"报告已保存到: {reportDir}");

                // 11. 分析结果
                Console.WriteLine();
                Console.WriteLine("分析测试结果...");
                var analyzer = new ConfigDrivenTestAnalyzer(logger);
                var analysis = analyzer.AnalyzeSession(session);

                // 显示性能最差的步骤
                Console.WriteLine();
                Console.WriteLine("=== 性能最慢的步骤 ===");
                foreach (var step in analysis.StepPerformance.Take(5))
                {
                    Console.WriteLine($"{step.StepName}: {step.AverageDuration:F0}ms " +
                                    $"(最小: {step.MinDuration:F0}ms, 最大: {step.MaxDuration:F0}ms)");
                }

                // 显示失败原因
                if (analysis.FailureReasons.Any())
                {
                    Console.WriteLine();
                    Console.WriteLine("=== 失败原因统计 ===");
                    foreach (var reason in analysis.FailureReasons.Take(5))
                    {
                        Console.WriteLine($"{reason.Reason}: {reason.Count} 次 ({reason.Percentage:P2})");
                    }
                }

                // 12. 清理
                await orchestrator.CleanupSessionAsync(session.SessionId);
                orchestrator.Dispose();
                pluginHost?.Dispose();

                Console.WriteLine();
                Console.WriteLine($"测试完成！整体结果: {(session.OverallPassed ? "✅ PASS" : "❌ FAIL")}");
            }
            catch (Exception ex)
            {
                logger.Error("测试执行异常", ex);
                Console.WriteLine($"错误: {ex.Message}");
            }

            Console.WriteLine();
            Console.WriteLine("按任意键退出...");
            Console.ReadKey();
        }

        static List<string> GetAvailableDuts()
        {
            // 实际应用中，这里应该扫描硬件获取 DUT 列表
            // 这里为演示目的，返回固定列表
            return Enumerable.Range(1, 4).Select(i => $"DUT-{i:D3}").ToList();
        }

        static string GetOperatorName()
        {
            Console.Write("请输入操作员姓名: ");
            var name = Console.ReadLine();
            return string.IsNullOrWhiteSpace(name) ? "Unknown" : name;
        }

        static string GetBatchNumber()
        {
            return $"BATCH-{DateTime.Now:yyyyMMdd}-{new Random().Next(1000, 9999)}";
        }

        static string GetCurrentShift()
        {
            var hour = DateTime.Now.Hour;
            if (hour >= 8 && hour < 16) return "早班";
            if (hour >= 16 && hour < 24) return "中班";
            return "夜班";
        }
    }
}
```

### 运行效果

```
=== 生产测试系统 ===

插件加载: 成功 2, 失败 0
检测到 4 个 DUT
请输入操作员姓名: 张三
测试会话创建成功: abc123...
开始测试...
[DUT-001] ✅ 硬件自检
[DUT-002] ✅ 硬件自检
[DUT-001] ✅ 软件版本检查
[DUT-003] ✅ 硬件自检
[DUT-002] ✅ 软件版本检查
...
进度: 100.0% (4/4) | 通过率: 97.50% | 耗时: 45s

=== 测试结果 ===
总 DUT 数: 4
通过 DUT: 4 (100.00%)
失败 DUT: 0
总步骤数: 40
步骤通过率: 97.50%
总耗时: 45.23 秒
平均每 DUT: 11.31 秒

生成测试报告...
报告已保存到: reports/20260304

分析测试结果...

=== 性能最慢的步骤 ===
音频功能测试: 28500ms (最小: 27800ms, 最大: 29200ms)
网络连接测试: 9800ms (最小: 9500ms, 最大: 10100ms)
WiFi 功能测试: 14200ms (最小: 13900ms, 最大: 14500ms)
蓝牙功能测试: 9500ms (最小: 9200ms, 最大: 9800ms)
硬件自检: 8900ms (最小: 8700ms, 最大: 9100ms)

测试完成！整体结果: ✅ PASS

按任意键退出...
```

---

## 高级功能示例

### 1. 趋势分析

```csharp
// 加载历史会话
var historySessions = LoadHistorySessions(); // 从数据库或文件加载

// 分析每个会话
var analyzer = new ConfigDrivenTestAnalyzer(logger);
var analyses = historySessions.Select(s => analyzer.AnalyzeSession(s)).ToList();

// 趋势分析
var trend = analyzer.AnalyzeTrend(analyses);

Console.WriteLine($"通过率趋势: {trend.PassRateTrend}");
Console.WriteLine($"耗时趋势: {trend.DurationTrend}");
Console.WriteLine($"平均通过率: {trend.AveragePassRate:P2}");
Console.WriteLine($"最佳会话: {trend.BestSession}");
Console.WriteLine($"最差会话: {trend.WorstSession}");
```

### 2. 会话比较

```csharp
var analyzer = new ConfigDrivenTestAnalyzer(logger);

var baselineAnalysis = analyzer.AnalyzeSession(baselineSession);
var currentAnalysis = analyzer.AnalyzeSession(currentSession);

var comparison = analyzer.CompareSession(baselineAnalysis, currentAnalysis);

Console.WriteLine($"通过率变化: {comparison.PassRateChangePercentage:F2}%");
Console.WriteLine($"耗时变化: {comparison.DurationChangePercentage:F2}%");

// 显示性能变化最大的步骤
foreach (var change in comparison.StepPerformanceChanges.Take(5))
{
    Console.WriteLine($"{change.StepName}: {change.DurationChangePercentage:F2}%");
}
```

### 3. 自定义事件处理

```csharp
orchestrator.StepCompleted += async (sender, e) =>
{
    var stepResult = e.Data as ConfigDrivenStepResult;

    // 发送实时通知
    if (!stepResult.Passed)
    {
        await SendNotification($"步骤失败: {e.DutId} - {stepResult.StepName}");
    }

    // 更新数据库
    await UpdateDatabase(e.SessionId, e.DutId, stepResult);

    // 触发外部系统
    await TriggerExternalSystem(stepResult);
};
```

### 4. 动态配置修改

```csharp
// 加载配置
var loader = new ConfigurationLoader("config/unified-config.json", logger);
var testProject = await loader.LoadTestProjectAsync();

// 动态修改步骤
testProject.Steps[0].Timeout = 10000; // 增加超时时间
testProject.Steps[1].Enabled = false; // 禁用某个步骤

// 使用修改后的配置
var engine = new ConfigDrivenTestEngine(logger);
var report = await engine.ExecuteTestProjectAsync(testProject, "DUT-001");
```

---

## 常见问题 FAQ

### Q1: 如何添加新的测试步骤？
**A**: 只需在配置文件的 `Steps` 数组中添加新的步骤对象，无需修改代码。

### Q2: 如何支持新的验证规则？
**A**: 在 `ConfigDrivenTestEngine.ValidateResult` 方法中添加新的前缀处理逻辑。

### Q3: 如何提高测试速度？
**A**:
- 增加并行 DUT 数量
- 优化步骤超时时间
- 使用插件系统（性能更好）
- 减少不必要的延迟

### Q4: 如何处理测试失败？
**A**:
- 检查 `ErrorMessage` 了解失败原因
- 使用重试机制（`Parameters.MaxRetries`）
- 设置 `ContinueOnFailure` 继续执行后续步骤
- 查看详细日志

### Q5: 如何集成到现有系统？
**A**:
- 使用事件系统集成外部通知
- 通过 API 包装提供 REST 接口
- 使用数据库持久化测试结果
- 集成到 CI/CD 流程

---

## 下一步

### 学习资源
1. 📖 [完整功能文档](CONFIG_DRIVEN_FEATURES.md)
2. 📖 [详细使用指南](README_CONFIG_DRIVEN.md)
3. 💻 [示例代码](../UTF.Examples/ConfigDrivenTestExample.cs)
4. 📊 [配置文件示例](../config/unified-config.json)

### 进阶主题
1. 插件开发
2. 自定义验证规则
3. 报告模板定制
4. 分布式测试
5. 性能优化

### 获取帮助
- 查看日志文件了解详细信息
- 使用配置验证功能检查配置文件
- 参考示例代码
- 查阅 API 文档

---

**祝测试顺利！** 🎉
