using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UTF.Logging;

namespace UTF.Core;

/// <summary>
/// 配置驱动的测试编排器 - 整合配置加载、测试执行和插件系统
/// </summary>
public sealed class ConfigDrivenTestOrchestrator : IDisposable
{
    private readonly ILogger _logger;
    private readonly IConfigurationService _configService;
    private readonly ConfigDrivenTestEngine _testEngine;
    private readonly ConcurrentDictionary<string, ConfigTestSession> _activeSessions = new();
    private readonly SemaphoreSlim _orchestrationLock = new(1, 1);
    private bool _disposed;

    public ConfigDrivenTestOrchestrator(
        IConfigurationService configService,
        ILogger? logger = null,
        IPluginService? pluginService = null)
    {
        _logger = logger ?? LoggerFactory.CreateLogger<ConfigDrivenTestOrchestrator>();
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _testEngine = new ConfigDrivenTestEngine(_logger, pluginService);
    }

    public IReadOnlyList<ConfigTestSession> ActiveSessions => _activeSessions.Values.ToList().AsReadOnly();

    public event EventHandler<ConfigTestEventArgs>? SessionStarted;
    public event EventHandler<ConfigTestEventArgs>? SessionCompleted;
    public event EventHandler<ConfigTestEventArgs>? StepCompleted;
    public event EventHandler<ConfigTestEventArgs>? ErrorOccurred;

    /// <summary>
    /// 初始化编排器 - 验证配置文件
    /// </summary>
    public async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _orchestrationLock.WaitAsync(cancellationToken);
        try
        {
            _logger.Info("初始化配置驱动测试编排器...");

            // 验证配置可用性 - 尝试加载配置
            try
            {
                await _configService.RefreshAsync();
                _logger.Info("配置驱动测试编排器初始化完成");
                return true;
            }
            catch
            {
                _logger.Error("配置文件验证失败");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.Error("初始化配置驱动测试编排器失败", ex);
            return false;
        }
        finally
        {
            _orchestrationLock.Release();
        }
    }

    /// <summary>
    /// 创建测试会话
    /// </summary>
    public async Task<ConfigTestSession?> CreateSessionAsync(
        List<string> dutIds,
        string? operatorName = null,
        Dictionary<string, object>? sessionContext = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.Info($"创建测试会话, DUT数量: {dutIds.Count}");

            // 加载测试项目配置 (使用 dynamic 避免直接依赖 UTF.Configuration)
            var testProjectConfig = await _configService.GetConfigurationSectionAsync<dynamic>("TestProjectConfiguration");
            if (testProjectConfig?.TestProject == null)
            {
                _logger.Error("加载测试项目失败");
                return null;
            }

            var testProjectData = testProjectConfig.TestProject;

            // 转换为 ConfigTestProject
            var testProject = new ConfigTestProject
            {
                Id = testProjectData.Id?.ToString() ?? "default",
                Name = testProjectData.Name?.ToString() ?? "未命名项目",
                Description = testProjectData.Description?.ToString() ?? "",
                Enabled = testProjectData.Enabled ?? true,
                Steps = new List<ConfigTestStep>()
            };

            // 转换步骤
            if (testProjectData.Steps != null)
            {
                foreach (var step in testProjectData.Steps)
                {
                    var validationRules = TryConvertDictionary(step.ValidationRules);
                    var parameters = TryConvertDictionary(step.Parameters);

                    testProject.Steps.Add(new ConfigTestStep
                    {
                        Id = step.Id?.ToString() ?? Guid.NewGuid().ToString(),
                        Name = step.Name?.ToString() ?? "",
                        Description = step.Description?.ToString() ?? "",
                        Order = step.Order ?? 0,
                        Enabled = step.Enabled ?? true,
                        Type = step.Type?.ToString(),
                        TargetDeviceId = step.TargetDeviceId?.ToString(),
                        Command = step.Command?.ToString(),
                        Expected = step.Expected?.ToString(),
                        Timeout = step.Timeout,
                        Delay = step.Delay,
                        RetryCount = step.RetryCount,
                        Channel = step.Channel?.ToString(),
                        StoreResultAs = step.StoreResultAs?.ToString(),
                        ConditionExpression = step.ConditionExpression?.ToString(),
                        ContinueOnFailure = step.ContinueOnFailure ?? false,
                        ValidationRules = validationRules,
                        Parameters = parameters
                    });
                }
            }

            // 加载 DUT 配置
            var dutConfiguration = await _configService.GetConfigurationSectionAsync<dynamic>("DUTConfiguration");
            DUTConfigInfo? dutConfig = null;

            if (dutConfiguration != null)
            {
                dutConfig = new DUTConfigInfo
                {
                    ProductName = dutConfiguration.ProductInfo?.Name?.ToString() ?? "",
                    ProductModel = dutConfiguration.ProductInfo?.Model?.ToString() ?? "",
                    ExpectedSoftwareVersion = dutConfiguration.ProductInfo?.ExpectedSoftwareVersion?.ToString() ?? "",
                    DefaultMaxConcurrent = dutConfiguration.GlobalSettings?.DefaultMaxConcurrent ?? 16,
                    TestTimeout = dutConfiguration.GlobalSettings?.TestTimeout ?? 300,
                    RetryCount = dutConfiguration.GlobalSettings?.RetryCount ?? 3,
                    SerialPorts = new List<string>(),
                    NetworkHosts = new List<string>()
                };

                if (dutConfiguration.CommunicationEndpoints?.SerialPorts != null)
                {
                    foreach (var port in dutConfiguration.CommunicationEndpoints.SerialPorts)
                    {
                        dutConfig.SerialPorts.Add(port.ToString());
                    }
                }

                if (dutConfiguration.CommunicationEndpoints?.NetworkHosts != null)
                {
                    foreach (var host in dutConfiguration.CommunicationEndpoints.NetworkHosts)
                    {
                        dutConfig.NetworkHosts.Add(host.ToString());
                    }
                }
            }

            var sessionId = Guid.NewGuid().ToString();
            var session = new ConfigTestSession
            {
                SessionId = sessionId,
                TestProject = testProject,
                DutIds = dutIds,
                Operator = operatorName ?? "Unknown",
                Status = ConfigTestStatus.Created,
                CreatedTime = DateTime.UtcNow,
                Context = sessionContext ?? new Dictionary<string, object>(),
                DutConfig = dutConfig,
                DutResults = new ConcurrentDictionary<string, ConfigDrivenTestReport>()
            };

            _activeSessions.TryAdd(sessionId, session);

            _logger.Info($"测试会话创建成功: {sessionId}");
            return session;
        }
        catch (Exception ex)
        {
            _logger.Error("创建测试会话失败", ex);
            return null;
        }
    }

    /// <summary>
    /// 启动测试会话 - 并行执行所有 DUT 的测试
    /// </summary>
    public async Task<bool> StartSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        if (!_activeSessions.TryGetValue(sessionId, out var session))
        {
            _logger.Warning($"会话不存在: {sessionId}");
            return false;
        }

        try
        {
            _logger.Info($"启动测试会话: {sessionId}");

            session.Status = ConfigTestStatus.Running;
            session.StartTime = DateTime.UtcNow;

            SessionStarted?.Invoke(this, new ConfigTestEventArgs
            {
                SessionId = sessionId,
                EventType = "SessionStarted",
                Timestamp = DateTime.UtcNow
            });

            // 并行执行所有 DUT 的测试
            var testTasks = session.DutIds.Select(dutId =>
                ExecuteDutTestAsync(sessionId, dutId, cancellationToken)).ToArray();

            _ = Task.Run(async () =>
            {
                await Task.WhenAll(testTasks);
                await CompleteSessionAsync(sessionId);
            }, cancellationToken);

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"启动测试会话失败: {sessionId}", ex);
            session.Status = ConfigTestStatus.Error;
            session.ErrorMessage = ex.Message;

            ErrorOccurred?.Invoke(this, new ConfigTestEventArgs
            {
                SessionId = sessionId,
                EventType = "SessionStartError",
                Data = ex.Message,
                Timestamp = DateTime.UtcNow
            });

            return false;
        }
    }

    /// <summary>
    /// 执行单个 DUT 的测试
    /// </summary>
    private async Task ExecuteDutTestAsync(
        string sessionId,
        string dutId,
        CancellationToken cancellationToken)
    {
        if (!_activeSessions.TryGetValue(sessionId, out var session))
        {
            return;
        }

        try
        {
            _logger.Info($"开始测试 DUT: {dutId}");

            // 执行测试项目
            var report = await _testEngine.ExecuteTestProjectAsync(
                session.TestProject,
                dutId,
                session.Context,
                cancellationToken);

            // 保存测试结果
            session.DutResults.TryAdd(dutId, report);

            // 触发步骤完成事件
            foreach (var stepResult in report.StepResults)
            {
                StepCompleted?.Invoke(this, new ConfigTestEventArgs
                {
                    SessionId = sessionId,
                    DutId = dutId,
                    EventType = "StepCompleted",
                    Data = stepResult,
                    Timestamp = DateTime.UtcNow
                });
            }

            _logger.Info($"DUT 测试完成: {dutId}, 结果: {(report.Passed ? "PASS" : "FAIL")}");
        }
        catch (Exception ex)
        {
            _logger.Error($"DUT 测试失败: {dutId}", ex);

            ErrorOccurred?.Invoke(this, new ConfigTestEventArgs
            {
                SessionId = sessionId,
                DutId = dutId,
                EventType = "DutTestError",
                Data = ex.Message,
                Timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// 完成测试会话
    /// </summary>
    private async Task CompleteSessionAsync(string sessionId)
    {
        if (!_activeSessions.TryGetValue(sessionId, out var session))
        {
            return;
        }

        try
        {
            session.Status = ConfigTestStatus.Completed;
            session.EndTime = DateTime.UtcNow;

            // 计算整体结果
            session.OverallPassed = session.DutResults.Values.All(r => r.Passed);

            _logger.Info($"测试会话完成: {sessionId}, 整体结果: {(session.OverallPassed ? "PASS" : "FAIL")}");

            SessionCompleted?.Invoke(this, new ConfigTestEventArgs
            {
                SessionId = sessionId,
                EventType = "SessionCompleted",
                Data = session,
                Timestamp = DateTime.UtcNow
            });

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.Error($"完成测试会话失败: {sessionId}", ex);
        }
    }

    /// <summary>
    /// 获取会话状态
    /// </summary>
    public ConfigTestSession? GetSession(string sessionId)
    {
        return _activeSessions.TryGetValue(sessionId, out var session) ? session : null;
    }

    /// <summary>
    /// 获取会话统计信息
    /// </summary>
    public ConfigTestStatistics? GetSessionStatistics(string sessionId)
    {
        if (!_activeSessions.TryGetValue(sessionId, out var session))
        {
            return null;
        }

        var completedDuts = session.DutResults.Count;
        var totalDuts = session.DutIds.Count;
        var passedDuts = session.DutResults.Values.Count(r => r.Passed);
        var failedDuts = completedDuts - passedDuts;

        var totalSteps = session.DutResults.Values.Sum(r => r.StepResults.Count);
        var passedSteps = session.DutResults.Values.Sum(r => r.StepResults.Count(s => s.Passed));
        var failedSteps = totalSteps - passedSteps;

        return new ConfigTestStatistics
        {
            SessionId = sessionId,
            TotalDuts = totalDuts,
            CompletedDuts = completedDuts,
            PassedDuts = passedDuts,
            FailedDuts = failedDuts,
            TotalSteps = totalSteps,
            PassedSteps = passedSteps,
            FailedSteps = failedSteps,
            PassRate = totalSteps > 0 ? (double)passedSteps / totalSteps : 0,
            Duration = session.EndTime.HasValue
                ? session.EndTime.Value - session.StartTime.GetValueOrDefault()
                : DateTime.UtcNow - session.StartTime.GetValueOrDefault()
        };
    }

    /// <summary>
    /// 停止测试会话
    /// </summary>
    public async Task<bool> StopSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (!_activeSessions.TryGetValue(sessionId, out var session))
        {
            return false;
        }

        try
        {
            _logger.Info($"停止测试会话: {sessionId}");

            session.Status = ConfigTestStatus.Stopped;
            session.EndTime = DateTime.UtcNow;

            await Task.Delay(100, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"停止测试会话失败: {sessionId}", ex);
            return false;
        }
    }

    /// <summary>
    /// 清理会话
    /// </summary>
    public async Task<bool> CleanupSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.Info($"清理测试会话: {sessionId}");

            _activeSessions.TryRemove(sessionId, out _);

            await Task.Delay(100, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"清理测试会话失败: {sessionId}", ex);
            return false;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _testEngine?.Dispose();
            _orchestrationLock?.Dispose();
            _activeSessions.Clear();
            _disposed = true;
        }
    }

    private static Dictionary<string, object>? TryConvertDictionary(object? raw)
    {
        if (raw == null)
        {
            return null;
        }

        if (raw is Dictionary<string, object> typed)
        {
            return new Dictionary<string, object>(typed);
        }

        if (raw is IDictionary dictionary)
        {
            var result = new Dictionary<string, object>();
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = entry.Key?.ToString();
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                result[key] = entry.Value ?? string.Empty;
            }

            return result;
        }

        return null;
    }
}

/// <summary>
/// 配置测试会话
/// </summary>
public class ConfigTestSession
{
    public string SessionId { get; set; } = "";
    public ConfigTestProject TestProject { get; set; } = new();
    public List<string> DutIds { get; set; } = new();
    public string Operator { get; set; } = "";
    public ConfigTestStatus Status { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public Dictionary<string, object> Context { get; set; } = new();
    public DUTConfigInfo? DutConfig { get; set; }
    public ConcurrentDictionary<string, ConfigDrivenTestReport> DutResults { get; set; } = new();
    public bool OverallPassed { get; set; }
    public string ErrorMessage { get; set; } = "";
}

/// <summary>
/// 配置测试状态
/// </summary>
public enum ConfigTestStatus
{
    Created,
    Running,
    Completed,
    Stopped,
    Error
}

/// <summary>
/// 配置测试事件参数
/// </summary>
public class ConfigTestEventArgs : EventArgs
{
    public string SessionId { get; set; } = "";
    public string? DutId { get; set; }
    public string EventType { get; set; } = "";
    public object? Data { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// 配置测试统计信息
/// </summary>
public class ConfigTestStatistics
{
    public string SessionId { get; set; } = "";
    public int TotalDuts { get; set; }
    public int CompletedDuts { get; set; }
    public int PassedDuts { get; set; }
    public int FailedDuts { get; set; }
    public int TotalSteps { get; set; }
    public int PassedSteps { get; set; }
    public int FailedSteps { get; set; }
    public double PassRate { get; set; }
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// DUT 配置信息
/// </summary>
public class DUTConfigInfo
{
    public string ProductName { get; set; } = "";
    public string ProductModel { get; set; } = "";
    public string ExpectedSoftwareVersion { get; set; } = "";
    public int DefaultMaxConcurrent { get; set; } = 16;
    public int TestTimeout { get; set; } = 300;
    public int RetryCount { get; set; } = 3;
    public List<string> SerialPorts { get; set; } = new();
    public List<string> NetworkHosts { get; set; } = new();
}
