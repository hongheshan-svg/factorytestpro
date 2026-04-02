using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UTF.Core;
using UTF.Logging;
using UTF.Plugin.Host;

namespace UTF.Examples;

/// <summary>
/// 配置驱动测试模块示例程序
/// </summary>
public class ConfigDrivenTestExample
{
    private readonly ILogger _logger;

    public ConfigDrivenTestExample()
    {
        _logger = LoggerFactory.CreateLogger<ConfigDrivenTestExample>();
    }

    /// <summary>
    /// 示例 1：基本使用 - 不使用插件
    /// </summary>
    public async Task Example1_BasicUsageAsync()
    {
        _logger.Info("=== 示例 1：基本使用 ===");

        // 创建编排器
        var orchestrator = new ConfigDrivenTestOrchestrator(
            "config/unified-config.json",
            _logger
        );

        // 初始化
        if (!await orchestrator.InitializeAsync())
        {
            _logger.Error("初始化失败");
            return;
        }

        // 创建测试会话
        var dutIds = new List<string> { "DUT-001", "DUT-002", "DUT-003" };
        var session = await orchestrator.CreateSessionAsync(
            dutIds: dutIds,
            operatorName: "张三"
        );

        if (session == null)
        {
            _logger.Error("创建会话失败");
            return;
        }

        _logger.Info($"会话创建成功: {session.SessionId}");

        // 订阅事件
        orchestrator.StepCompleted += (sender, e) =>
        {
            var stepResult = e.Data as ConfigDrivenStepResult;
            _logger.Info($"[{e.DutId}] 步骤完成: {stepResult?.StepName} - {(stepResult?.Passed == true ? "PASS" : "FAIL")}");
        };

        orchestrator.SessionCompleted += (sender, e) =>
        {
            _logger.Info($"会话完成: {e.SessionId}");
        };

        // 启动测试
        await orchestrator.StartSessionAsync(session.SessionId);

        // 等待完成
        await WaitForSessionCompletionAsync(orchestrator, session.SessionId);

        // 获取最终结果
        var finalSession = orchestrator.GetSession(session.SessionId);
        _logger.Info($"测试完成，整体结果: {(finalSession?.OverallPassed == true ? "✅ PASS" : "❌ FAIL")}");

        // 显示详细统计
        var stats = orchestrator.GetSessionStatistics(session.SessionId);
        if (stats != null)
        {
            _logger.Info($"统计信息:");
            _logger.Info($"  总 DUT 数: {stats.TotalDuts}");
            _logger.Info($"  完成 DUT 数: {stats.CompletedDuts}");
            _logger.Info($"  通过 DUT 数: {stats.PassedDuts}");
            _logger.Info($"  失败 DUT 数: {stats.FailedDuts}");
            _logger.Info($"  总步骤数: {stats.TotalSteps}");
            _logger.Info($"  通过步骤数: {stats.PassedSteps}");
            _logger.Info($"  失败步骤数: {stats.FailedSteps}");
            _logger.Info($"  通过率: {stats.PassRate:P2}");
            _logger.Info($"  耗时: {stats.Duration.TotalSeconds:F2} 秒");
        }

        // 清理
        await orchestrator.CleanupSessionAsync(session.SessionId);
        orchestrator.Dispose();
    }

    /// <summary>
    /// 示例 2：集成插件系统
    /// </summary>
    public async Task Example2_WithPluginSystemAsync()
    {
        _logger.Info("=== 示例 2：集成插件系统 ===");

        // 初始化插件主机
        var pluginHost = new StepExecutorPluginHost("plugins", _logger);
        var pluginReport = await pluginHost.InitializeAsync();

        _logger.Info($"插件加载报告:");
        _logger.Info($"  成功加载: {pluginReport.LoadedCount} 个插件");
        _logger.Info($"  加载失败: {pluginReport.FailedCount} 个插件");

        if (pluginReport.Issues.Any())
        {
            _logger.Warning("插件加载问题:");
            foreach (var issue in pluginReport.Issues)
            {
                _logger.Warning($"  [{issue.ErrorCode}] {issue.ManifestPath}: {issue.Message}");
            }
        }

        // 显示已加载的插件
        foreach (var plugin in pluginHost.LoadedPlugins)
        {
            _logger.Info($"插件: {plugin.Name} v{plugin.Version}");
            _logger.Info($"  支持步骤类型: {string.Join(", ", plugin.SupportedStepTypes)}");
            _logger.Info($"  支持通道: {string.Join(", ", plugin.SupportedChannels)}");
            _logger.Info($"  优先级: {plugin.Priority}");
        }

        // 创建编排器（传入插件主机）
        var orchestrator = new ConfigDrivenTestOrchestrator(
            "config/unified-config.json",
            _logger,
            pluginHost
        );

        await orchestrator.InitializeAsync();

        // 创建并执行测试会话
        var session = await orchestrator.CreateSessionAsync(
            dutIds: new List<string> { "DUT-001" },
            operatorName: "李四"
        );

        if (session != null)
        {
            await orchestrator.StartSessionAsync(session.SessionId);
            await WaitForSessionCompletionAsync(orchestrator, session.SessionId);

            var finalSession = orchestrator.GetSession(session.SessionId);
            _logger.Info($"测试结果: {(finalSession?.OverallPassed == true ? "✅ PASS" : "❌ FAIL")}");

            await orchestrator.CleanupSessionAsync(session.SessionId);
        }

        orchestrator.Dispose();
        pluginHost.Dispose();
    }

    /// <summary>
    /// 示例 3：配置文件验证（使用 ConfigDrivenTestValidator）
    /// </summary>
    public async Task Example3_ConfigValidationAsync()
    {
        _logger.Info("=== 示例 3：配置文件验证 ===");

        var validator = new ConfigDrivenTestValidator(_logger);

        // 手动构建测试项目并验证
        var testProject = new ConfigTestProject
        {
            Id = "validate_demo",
            Name = "验证示例项目",
            Description = "演示配置验证功能",
            Enabled = true,
            Steps = new List<ConfigTestStep>
            {
                new ConfigTestStep
                {
                    Id = "step_001",
                    Name = "串口版本检查",
                    Type = "serial",
                    Command = "system_manager version",
                    Expected = "contains:SW_VERSION:V1.0",
                    Timeout = 5000,
                    Channel = "Serial",
                    Order = 1
                }
            }
        };

        var result = validator.ValidateTestProject(testProject);

        if (!result.IsValid)
        {
            _logger.Error("❌ 配置验证失败:");
            foreach (var error in result.Errors)
            {
                _logger.Error($"  - [{error.Code}] {error.Message}");
            }
            return;
        }

        _logger.Info("✅ 配置验证通过");
        _logger.Info($"测试项目: {testProject.Name}");
        _logger.Info($"  ID: {testProject.Id}");
        _logger.Info($"  描述: {testProject.Description}");
        _logger.Info($"  启用: {testProject.Enabled}");
        _logger.Info($"  步骤数: {testProject.Steps?.Count ?? 0}");

        if (testProject.Steps != null)
        {
            _logger.Info("测试步骤:");
            foreach (var step in testProject.Steps.OrderBy(s => s.Order))
            {
                _logger.Info($"  [{step.Order}] {step.Name}");
                _logger.Info($"      类型: {step.Type}, 通道: {step.Channel}");
                _logger.Info($"      命令: {step.Command}");
                _logger.Info($"      期望: {step.Expected}");
                _logger.Info($"      超时: {step.Timeout}ms, 延迟: {step.Delay}ms");
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// 示例 4：单独使用测试引擎
    /// </summary>
    public async Task Example4_DirectEngineUsageAsync()
    {
        _logger.Info("=== 示例 4：单独使用测试引擎 ===");

        var engine = new ConfigDrivenTestEngine(_logger);

        // 手动构建测试步骤
        var steps = new List<ConfigTestStep>
        {
            new ConfigTestStep
            {
                Id = "step_001",
                Name = "串口版本检查",
                Type = "serial",
                Command = "system_manager version",
                Expected = "contains:SW_VERSION:V1.0",
                Timeout = 5000,
                Channel = "Serial",
                Order = 1
            },
            new ConfigTestStep
            {
                Id = "step_002",
                Name = "MAC地址检查",
                Type = "serial",
                Command = "mac",
                Expected = "contains:1C:78:39",
                Timeout = 5000,
                Channel = "Serial",
                Order = 2
            },
            new ConfigTestStep
            {
                Id = "step_003",
                Name = "音频功能测试",
                Type = "serial",
                Command = "system_manager factory_test",
                Expected = "contains:test voice pass",
                Timeout = 25000,
                Channel = "Serial",
                Order = 3
            }
        };

        // 构建测试项目
        var testProject = new ConfigTestProject
        {
            Id = "manual_test",
            Name = "手动构建的测试项目",
            Description = "演示如何手动构建测试项目",
            Enabled = true,
            Steps = steps
        };

        // 执行测试
        var report = await engine.ExecuteTestProjectAsync(testProject, "DUT-001");

        // 显示结果
        _logger.Info($"测试项目: {report.ProjectName}");
        _logger.Info($"DUT: {report.DutId}");
        _logger.Info($"整体结果: {(report.Passed ? "✅ PASS" : "❌ FAIL")}");
        _logger.Info($"开始时间: {report.StartTime:yyyy-MM-dd HH:mm:ss}");
        _logger.Info($"结束时间: {report.EndTime:yyyy-MM-dd HH:mm:ss}");
        _logger.Info($"耗时: {(report.EndTime - report.StartTime).TotalSeconds:F2} 秒");

        _logger.Info("步骤结果:");
        foreach (var stepResult in report.StepResults)
        {
            var status = stepResult.Passed ? "✅ PASS" : "❌ FAIL";
            if (stepResult.Skipped) status = "⏭️ SKIP";

            _logger.Info($"  [{status}] {stepResult.StepName}");
            _logger.Info($"      输出: {stepResult.RawOutput}");
            if (!stepResult.Passed && !string.IsNullOrEmpty(stepResult.ErrorMessage))
            {
                _logger.Info($"      错误: {stepResult.ErrorMessage}");
            }
            _logger.Info($"      耗时: {(stepResult.EndTime - stepResult.StartTime).TotalMilliseconds:F0} ms");
        }

        engine.Dispose();
    }

    /// <summary>
    /// 示例 5：批量测试多个 DUT
    /// </summary>
    public async Task Example5_BatchTestingAsync()
    {
        _logger.Info("=== 示例 5：批量测试多个 DUT ===");

        var orchestrator = new ConfigDrivenTestOrchestrator(
            "config/unified-config.json",
            _logger
        );

        await orchestrator.InitializeAsync();

        // 生成 16 个 DUT
        var dutIds = Enumerable.Range(1, 16).Select(i => $"DUT-{i:D3}").ToList();

        _logger.Info($"准备测试 {dutIds.Count} 个 DUT");

        var session = await orchestrator.CreateSessionAsync(
            dutIds: dutIds,
            operatorName: "批量测试"
        );

        if (session == null)
        {
            _logger.Error("创建会话失败");
            return;
        }

        // 订阅事件以显示进度
        var completedCount = 0;
        orchestrator.StepCompleted += (sender, e) =>
        {
            // 每完成一个步骤，更新进度
            var stats = orchestrator.GetSessionStatistics(session.SessionId);
            if (stats != null)
            {
                _logger.Debug($"进度: {stats.CompletedDuts}/{stats.TotalDuts} DUTs, " +
                             $"步骤: {stats.PassedSteps}/{stats.TotalSteps}, " +
                             $"通过率: {stats.PassRate:P2}");
            }
        };

        orchestrator.SessionCompleted += (sender, e) =>
        {
            _logger.Info("所有 DUT 测试完成！");
        };

        // 启动测试
        var startTime = DateTime.UtcNow;
        await orchestrator.StartSessionAsync(session.SessionId);

        // 等待完成
        await WaitForSessionCompletionAsync(orchestrator, session.SessionId);

        var endTime = DateTime.UtcNow;
        var duration = endTime - startTime;

        // 显示最终统计
        var finalStats = orchestrator.GetSessionStatistics(session.SessionId);
        if (finalStats != null)
        {
            _logger.Info("=== 批量测试完成 ===");
            _logger.Info($"总 DUT 数: {finalStats.TotalDuts}");
            _logger.Info($"通过 DUT 数: {finalStats.PassedDuts}");
            _logger.Info($"失败 DUT 数: {finalStats.FailedDuts}");
            _logger.Info($"DUT 通过率: {(double)finalStats.PassedDuts / finalStats.TotalDuts:P2}");
            _logger.Info($"总步骤数: {finalStats.TotalSteps}");
            _logger.Info($"步骤通过率: {finalStats.PassRate:P2}");
            _logger.Info($"总耗时: {duration.TotalSeconds:F2} 秒");
            _logger.Info($"平均每 DUT 耗时: {duration.TotalSeconds / finalStats.TotalDuts:F2} 秒");
        }

        // 显示每个 DUT 的结果
        var finalSession = orchestrator.GetSession(session.SessionId);
        if (finalSession != null)
        {
            _logger.Info("各 DUT 测试结果:");
            foreach (var dutId in finalSession.DutIds)
            {
                if (finalSession.DutResults.TryGetValue(dutId, out var report))
                {
                    var status = report.Passed ? "✅ PASS" : "❌ FAIL";
                    var duration2 = report.EndTime - report.StartTime;
                    _logger.Info($"  {dutId}: {status} ({duration2.TotalSeconds:F2}s)");
                }
            }
        }

        await orchestrator.CleanupSessionAsync(session.SessionId);
        orchestrator.Dispose();
    }

    /// <summary>
    /// 示例 6：自定义上下文和参数
    /// </summary>
    public async Task Example6_CustomContextAsync()
    {
        _logger.Info("=== 示例 6：自定义上下文和参数 ===");

        var orchestrator = new ConfigDrivenTestOrchestrator(
            "config/unified-config.json",
            _logger
        );

        await orchestrator.InitializeAsync();

        // 创建自定义上下文
        var sessionContext = new Dictionary<string, object>
        {
            { "BatchNumber", "BATCH-2024-001" },
            { "ProductionLine", "Line-A" },
            { "QualityLevel", "Premium" },
            { "Operator", "张三" },
            { "Shift", "早班" },
            { "Temperature", 25.5 },
            { "Humidity", 60 }
        };

        _logger.Info("会话上下文:");
        foreach (var kvp in sessionContext)
        {
            _logger.Info($"  {kvp.Key}: {kvp.Value}");
        }

        var session = await orchestrator.CreateSessionAsync(
            dutIds: new List<string> { "DUT-001" },
            operatorName: "张三",
            sessionContext: sessionContext
        );

        if (session != null)
        {
            await orchestrator.StartSessionAsync(session.SessionId);
            await WaitForSessionCompletionAsync(orchestrator, session.SessionId);

            var finalSession = orchestrator.GetSession(session.SessionId);
            _logger.Info($"测试结果: {(finalSession?.OverallPassed == true ? "✅ PASS" : "❌ FAIL")}");

            await orchestrator.CleanupSessionAsync(session.SessionId);
        }

        orchestrator.Dispose();
    }

    /// <summary>
    /// 等待会话完成
    /// </summary>
    private async Task WaitForSessionCompletionAsync(
        ConfigDrivenTestOrchestrator orchestrator,
        string sessionId,
        int timeoutSeconds = 300)
    {
        var startTime = DateTime.UtcNow;
        var timeout = TimeSpan.FromSeconds(timeoutSeconds);

        while (true)
        {
            var session = orchestrator.GetSession(sessionId);
            if (session == null)
            {
                _logger.Warning("会话不存在");
                break;
            }

            if (session.Status == ConfigTestStatus.Completed ||
                session.Status == ConfigTestStatus.Stopped ||
                session.Status == ConfigTestStatus.Error)
            {
                break;
            }

            if (DateTime.UtcNow - startTime > timeout)
            {
                _logger.Warning("等待会话完成超时");
                break;
            }

            await Task.Delay(500);
        }
    }

    /// <summary>
    /// 运行所有示例
    /// </summary>
    public async Task RunAllExamplesAsync()
    {
        try
        {
            await Example1_BasicUsageAsync();
            Console.WriteLine();

            await Example2_WithPluginSystemAsync();
            Console.WriteLine();

            await Example3_ConfigValidationAsync();
            Console.WriteLine();

            await Example4_DirectEngineUsageAsync();
            Console.WriteLine();

            await Example5_BatchTestingAsync();
            Console.WriteLine();

            await Example6_CustomContextAsync();
            Console.WriteLine();

            _logger.Info("所有示例执行完成！");
        }
        catch (Exception ex)
        {
            _logger.Error("示例执行失败", ex);
        }
    }
}

/// <summary>
/// 示例程序入口
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("配置驱动测试模块示例程序");
        Console.WriteLine("================================");
        Console.WriteLine();

        var example = new ConfigDrivenTestExample();

        if (args.Length > 0)
        {
            // 运行指定的示例
            switch (args[0])
            {
                case "1":
                    await example.Example1_BasicUsageAsync();
                    break;
                case "2":
                    await example.Example2_WithPluginSystemAsync();
                    break;
                case "3":
                    await example.Example3_ConfigValidationAsync();
                    break;
                case "4":
                    await example.Example4_DirectEngineUsageAsync();
                    break;
                case "5":
                    await example.Example5_BatchTestingAsync();
                    break;
                case "6":
                    await example.Example6_CustomContextAsync();
                    break;
                default:
                    Console.WriteLine("无效的示例编号，运行所有示例...");
                    await example.RunAllExamplesAsync();
                    break;
            }
        }
        else
        {
            // 运行所有示例
            await example.RunAllExamplesAsync();
        }

        Console.WriteLine();
        Console.WriteLine("按任意键退出...");
        Console.ReadKey();
    }
}
