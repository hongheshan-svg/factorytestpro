using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UTF.Core.Mapping;
using UTF.Logging;

namespace UTF.Core;

/// <summary>
/// 配置驱动的测试编排器 - 整合配置加载、测试执行和插件系统。
/// 会话状态变更通过 _orchestrationLock 串行化；后台会话任务保留 TaskCompletionSource 供等待。
/// 生产主路径应使用 <see cref="CreateSessionAsync(ConfigTestProject, IReadOnlyList{string}, string?, Dictionary{string, object}?, DUTConfigInfo?, IReadOnlyDictionary{string, Dictionary{string, object}}?, CancellationToken)"/> 传入已构建的项目，避免 dynamic。
/// </summary>
public sealed class ConfigDrivenTestOrchestrator : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger _logger;
    private readonly IConfigurationService _configService;
    private readonly ConfigDrivenTestEngine _testEngine;
    private readonly ConcurrentDictionary<string, ConfigTestSession> _activeSessions = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _sessionCompletion = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _sessionCancellation = new();
    private readonly ConcurrentDictionary<string, Task> _sessionTasks = new();
    private readonly SemaphoreSlim _orchestrationLock = new(1, 1);
    private bool _disposed;

    public ConfigDrivenTestOrchestrator(
        IConfigurationService configService,
        ConfigDrivenTestEngine testEngine,
        ILogger? logger = null)
    {
        _logger = logger ?? LoggerFactory.CreateLogger<ConfigDrivenTestOrchestrator>();
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _testEngine = testEngine ?? throw new ArgumentNullException(nameof(testEngine));
    }

    public IReadOnlyList<ConfigTestSession> ActiveSessions => _activeSessions.Values.ToList().AsReadOnly();

    public event EventHandler<ConfigTestEventArgs>? SessionStarted;
    public event EventHandler<ConfigTestEventArgs>? SessionCompleted;
    public event EventHandler<ConfigTestEventArgs>? StepCompleted;
    public event EventHandler<ConfigTestEventArgs>? DutStarted;
    public event EventHandler<ConfigTestEventArgs>? DutCompleted;
    public event EventHandler<ConfigTestEventArgs>? ErrorOccurred;

    /// <summary>
    /// 初始化编排器 - 加载并验证配置文件（typed section DTOs，无 dynamic）。
    /// </summary>
    public async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _orchestrationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _logger.Info("初始化配置驱动测试编排器...");

            try
            {
                await _configService.RefreshAsync().ConfigureAwait(false);

                var section = await LoadSectionAsync<OrchestratorTestProjectSection>("TestProjectConfiguration")
                    .ConfigureAwait(false);
                if (section?.TestProject != null)
                {
                    var testProject = MapProject(section.TestProject, defaultRetryCount: 0);
                    var validator = new ConfigDrivenTestValidator(_logger);
                    var validationReport = validator.ValidateTestProject(testProject);
                    if (!validationReport.IsValid)
                    {
                        _logger.Error($"测试项目配置验证失败: {validationReport.Errors.Count} 个错误");
                        foreach (var err in validationReport.Errors)
                        {
                            _logger.Error($"  [{err.Code}] {err.Message}");
                        }

                        return false;
                    }
                }

                _logger.Info("配置驱动测试编排器初始化完成");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("配置文件验证失败", ex);
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
    /// 从配置服务创建测试会话（兼容路径；内部使用 typed DTO 映射，无 dynamic）。
    /// 生产 UI 主路径请使用 typed overload。
    /// </summary>
    public async Task<ConfigTestSession?> CreateSessionAsync(
        List<string> dutIds,
        string? operatorName = null,
        Dictionary<string, object>? sessionContext = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(dutIds);

        try
        {
            _logger.Info($"创建测试会话, DUT数量: {dutIds.Count}");

            var projectSection = await LoadSectionAsync<OrchestratorTestProjectSection>("TestProjectConfiguration")
                .ConfigureAwait(false);
            if (projectSection?.TestProject == null)
            {
                _logger.Error("加载测试项目失败");
                return null;
            }

            if (string.IsNullOrWhiteSpace(projectSection.TestProject.Id))
            {
                _logger.Error("测试项目缺少必需的 Id 字段，无法创建会话");
                return null;
            }

            var dutSection = await LoadSectionAsync<OrchestratorDutSection>("DUTConfiguration")
                .ConfigureAwait(false);
            var defaultRetry = dutSection?.GlobalSettings?.RetryCount ?? 3;
            var testProject = MapProject(projectSection.TestProject, defaultRetry);
            var dutConfig = MapDutConfig(dutSection);

            return await CreateSessionAsync(
                testProject,
                dutIds,
                operatorName,
                sessionContext,
                dutConfig,
                perDutContexts: null,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Error("创建测试会话失败", ex);
            return null;
        }
    }

    /// <summary>
    /// 使用已构建的 <see cref="ConfigTestProject"/> 创建会话（生产主路径，无 dynamic / 无配置服务读取）。
    /// </summary>
    /// <param name="project">已映射并校验过的测试项目。</param>
    /// <param name="dutIds">DUT 标识列表。</param>
    /// <param name="operatorName">操作员。</param>
    /// <param name="sessionContext">会话级共享上下文（所有 DUT 继承）。</param>
    /// <param name="dutConfig">并发/超时/端点配置；为 null 时使用默认并发 1。</param>
    /// <param name="perDutContexts">按 DUT 覆盖的上下文（SerialPort/Host 等）。</param>
    /// <param name="cancellationToken">取消标记。</param>
    public Task<ConfigTestSession?> CreateSessionAsync(
        ConfigTestProject project,
        IReadOnlyList<string> dutIds,
        string? operatorName = null,
        Dictionary<string, object>? sessionContext = null,
        DUTConfigInfo? dutConfig = null,
        IReadOnlyDictionary<string, Dictionary<string, object>>? perDutContexts = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(dutIds);
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedDutIds = dutIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (normalizedDutIds.Count == 0 || normalizedDutIds.Count != dutIds.Count)
        {
            _logger.Error("DUT ID must be non-empty and unique.");
            return Task.FromResult<ConfigTestSession?>(null);
        }

        if (!project.Enabled)
        {
            _logger.Error($"Test project is disabled: {project.Id}");
            return Task.FromResult<ConfigTestSession?>(null);
        }

        if (string.IsNullOrWhiteSpace(project.Id))
        {
            _logger.Error("测试项目缺少必需的 Id 字段，无法创建会话");
            return Task.FromResult<ConfigTestSession?>(null);
        }

        var validation = new ConfigDrivenTestValidator(_logger).ValidateTestProject(project);
        if (!validation.IsValid)
        {
            _logger.Error($"Test project validation failed: {string.Join("; ", validation.Errors.Select(e => e.Message))}");
            return Task.FromResult<ConfigTestSession?>(null);
        }

        var clonedProject = TestProjectMapper.CloneProject(project, defaultRetryCount: dutConfig?.RetryCount ?? 0);

        var sessionId = Guid.NewGuid().ToString();
        var perDut = new ConcurrentDictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);
        if (perDutContexts != null)
        {
            foreach (var kv in perDutContexts)
            {
                if (string.IsNullOrWhiteSpace(kv.Key) || kv.Value == null)
                {
                    continue;
                }

                perDut[kv.Key.Trim()] = new Dictionary<string, object>(kv.Value, StringComparer.OrdinalIgnoreCase);
            }
        }

        var session = new ConfigTestSession
        {
            SessionId = sessionId,
            TestProject = clonedProject,
            DutIds = normalizedDutIds,
            Operator = operatorName ?? "Unknown",
            Status = ConfigTestStatus.Created,
            CreatedTime = DateTime.UtcNow,
            Context = sessionContext != null
                ? new Dictionary<string, object>(sessionContext, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase),
            DutConfig = dutConfig ?? TestProjectMapper.BuildDutConfig(defaultMaxConcurrent: 1),
            DutResults = new ConcurrentDictionary<string, ConfigDrivenTestReport>(StringComparer.OrdinalIgnoreCase),
            PerDutContexts = perDut
        };

        _activeSessions.TryAdd(sessionId, session);
        _logger.Info($"测试会话创建成功: {sessionId}");
        return Task.FromResult<ConfigTestSession?>(session);
    }

    /// <summary>
    /// 启动测试会话 - 并行执行所有 DUT 的测试。
    /// </summary>
    public async Task<bool> StartSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_activeSessions.TryGetValue(sessionId, out var session))
        {
            _logger.Warning($"会话不存在: {sessionId}");
            return false;
        }

        try
        {
            await _orchestrationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (session.Status != ConfigTestStatus.Created)
                {
                    _logger.Warning($"Session cannot be started from state {session.Status}: {sessionId}");
                    return false;
                }

                session.Status = ConfigTestStatus.Running;
                session.StartTime = DateTime.UtcNow;
            }
            finally
            {
                _orchestrationLock.Release();
            }

            _logger.Info($"启动测试会话: {sessionId}");

            SessionStarted?.Invoke(this, new ConfigTestEventArgs
            {
                SessionId = sessionId,
                EventType = "SessionStarted",
                Timestamp = DateTime.UtcNow
            });

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var sessionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _sessionCompletion[sessionId] = tcs;
            _sessionCancellation[sessionId] = sessionCts;

            var sessionTask = RunSessionAsync(session, sessionCts, tcs);
            _sessionTasks[sessionId] = sessionTask;

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"启动测试会话失败: {sessionId}", ex);
            await SetSessionStatusAsync(session, ConfigTestStatus.Error).ConfigureAwait(false);
            await SetSessionErrorMessageAsync(session, ex.Message).ConfigureAwait(false);

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

    private async Task RunSessionAsync(
        ConfigTestSession session,
        CancellationTokenSource sessionCts,
        TaskCompletionSource<bool> completion)
    {
        var maxConcurrent = Math.Clamp(session.DutConfig?.DefaultMaxConcurrent ?? 1, 1, session.DutIds.Count);
        using var gate = new SemaphoreSlim(maxConcurrent, maxConcurrent);

        try
        {
            var tasks = session.DutIds.Select(async dutId =>
            {
                await gate.WaitAsync(sessionCts.Token).ConfigureAwait(false);
                try
                {
                    await ExecuteDutTestAsync(session.SessionId, dutId, sessionCts.Token).ConfigureAwait(false);
                }
                finally
                {
                    gate.Release();
                }
            });

            await Task.WhenAll(tasks).ConfigureAwait(false);
            if (session.Status == ConfigTestStatus.Running)
            {
                await CompleteSessionAsync(session.SessionId).ConfigureAwait(false);
            }

            completion.TrySetResult(session.Status == ConfigTestStatus.Completed && session.OverallPassed);
        }
        catch (OperationCanceledException)
        {
            await SetSessionStatusAsync(session, ConfigTestStatus.Stopped).ConfigureAwait(false);
            await SetSessionEndTimeAsync(session, DateTime.UtcNow).ConfigureAwait(false);
            await SetSessionOverallPassedAsync(session, false).ConfigureAwait(false);
            completion.TrySetResult(false);
        }
        catch (Exception ex)
        {
            _logger.Error($"测试会话后台任务故障: {session.SessionId}", ex);
            await SetSessionStatusAsync(session, ConfigTestStatus.Error).ConfigureAwait(false);
            await SetSessionEndTimeAsync(session, DateTime.UtcNow).ConfigureAwait(false);
            await SetSessionErrorMessageAsync(session, ex.Message).ConfigureAwait(false);
            completion.TrySetException(ex);
        }
    }

    /// <summary>
    /// 等待指定会话的完成。
    /// </summary>
    public Task WaitForSessionAsync(string sessionId)
    {
        if (_sessionCompletion.TryGetValue(sessionId, out var tcs))
        {
            return tcs.Task;
        }

        return _activeSessions.ContainsKey(sessionId)
            ? Task.FromException(new InvalidOperationException($"Session has not been started: {sessionId}"))
            : Task.FromException(new KeyNotFoundException($"Session not found: {sessionId}"));
    }

    private async Task ExecuteDutTestAsync(
        string sessionId,
        string dutId,
        CancellationToken cancellationToken)
    {
        if (!_activeSessions.TryGetValue(sessionId, out var session))
        {
            return;
        }

        DutStarted?.Invoke(this, new ConfigTestEventArgs
        {
            SessionId = sessionId,
            DutId = dutId,
            EventType = "DutStarted",
            Timestamp = DateTime.UtcNow
        });

        try
        {
            _logger.Info($"开始测试 DUT: {dutId}");

            var context = BuildExecutionContext(session, dutId);
            var report = await _testEngine.ExecuteTestProjectAsync(
                session.TestProject,
                dutId,
                context,
                cancellationToken).ConfigureAwait(false);

            session.DutResults.TryAdd(dutId, report);

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

            DutCompleted?.Invoke(this, new ConfigTestEventArgs
            {
                SessionId = sessionId,
                DutId = dutId,
                EventType = "DutCompleted",
                Data = report,
                Timestamp = DateTime.UtcNow
            });

            _logger.Info($"DUT 测试完成: {dutId}, 结果: {(report.Passed ? "PASS" : "FAIL")}");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error($"DUT 测试失败: {dutId}", ex);

            var errorReport = new ConfigDrivenTestReport
            {
                ProjectId = session.TestProject.Id,
                ProjectName = session.TestProject.Name,
                DutId = dutId,
                Passed = false,
                ErrorMessage = ex.Message,
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow
            };
            session.DutResults[dutId] = errorReport;

            ErrorOccurred?.Invoke(this, new ConfigTestEventArgs
            {
                SessionId = sessionId,
                DutId = dutId,
                EventType = "DutTestError",
                Data = ex.Message,
                Timestamp = DateTime.UtcNow
            });

            DutCompleted?.Invoke(this, new ConfigTestEventArgs
            {
                SessionId = sessionId,
                DutId = dutId,
                EventType = "DutCompleted",
                Data = errorReport,
                Timestamp = DateTime.UtcNow
            });
        }
    }

    private static Dictionary<string, object> BuildExecutionContext(ConfigTestSession session, string dutId)
    {
        var context = new Dictionary<string, object>(session.Context, StringComparer.OrdinalIgnoreCase);
        if (session.PerDutContexts.TryGetValue(dutId, out var perDut) && perDut != null)
        {
            foreach (var kv in perDut)
            {
                context[kv.Key] = kv.Value;
            }
        }

        // 若未提供 SerialPort/Host，按 DUT 序号从 DutConfig 端点列表填充。
        if (session.DutConfig != null)
        {
            var index = session.DutIds.FindIndex(id => string.Equals(id, dutId, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                index = 0;
            }

            if (!context.ContainsKey("SerialPort") && session.DutConfig.SerialPorts.Count > 0)
            {
                context["SerialPort"] = session.DutConfig.SerialPorts[index % session.DutConfig.SerialPorts.Count];
            }

            if (!context.ContainsKey("Host") && session.DutConfig.NetworkHosts.Count > 0)
            {
                context["Host"] = session.DutConfig.NetworkHosts[index % session.DutConfig.NetworkHosts.Count];
            }
        }

        context["DutId"] = dutId;
        return context;
    }

    private async Task CompleteSessionAsync(string sessionId)
    {
        if (!_activeSessions.TryGetValue(sessionId, out var session))
        {
            return;
        }

        try
        {
            await SetSessionStatusAsync(session, ConfigTestStatus.Completed).ConfigureAwait(false);
            await SetSessionEndTimeAsync(session, DateTime.UtcNow).ConfigureAwait(false);

            var expectedDuts = session.DutIds.Distinct(StringComparer.OrdinalIgnoreCase).Count();
            var overallPassed = expectedDuts > 0 &&
                                session.DutResults.Count == expectedDuts &&
                                session.DutResults.Values.All(r => r.Passed);
            await SetSessionOverallPassedAsync(session, overallPassed).ConfigureAwait(false);

            _logger.Info($"测试会话完成: {sessionId}, 整体结果: {(session.OverallPassed ? "PASS" : "FAIL")}");

            SessionCompleted?.Invoke(this, new ConfigTestEventArgs
            {
                SessionId = sessionId,
                EventType = "SessionCompleted",
                Data = session,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.Error($"完成测试会话失败: {sessionId}", ex);
        }
    }

    public ConfigTestSession? GetSession(string sessionId)
    {
        return _activeSessions.TryGetValue(sessionId, out var session) ? session : null;
    }

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

    public async Task<bool> StopSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (!_activeSessions.TryGetValue(sessionId, out var session))
        {
            return false;
        }

        try
        {
            _logger.Info($"停止测试会话: {sessionId}");

            if (_sessionCancellation.TryGetValue(sessionId, out var cts))
            {
                cts.Cancel();
            }

            if (_sessionTasks.TryGetValue(sessionId, out var task))
            {
                try
                {
                    await task.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // expected on stop
                }
                catch (Exception)
                {
                    // session fault already recorded
                }
            }
            else
            {
                await SetSessionStatusAsync(session, ConfigTestStatus.Stopped).ConfigureAwait(false);
                await SetSessionEndTimeAsync(session, DateTime.UtcNow).ConfigureAwait(false);
                await SetSessionOverallPassedAsync(session, false).ConfigureAwait(false);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"停止测试会话失败: {sessionId}", ex);
            return false;
        }
    }

    public async Task<bool> CleanupSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.Info($"清理测试会话: {sessionId}");

            if (_activeSessions.TryGetValue(sessionId, out var session) &&
                session.Status is ConfigTestStatus.Created or ConfigTestStatus.Running)
            {
                await StopSessionAsync(sessionId, cancellationToken).ConfigureAwait(false);
            }

            _activeSessions.TryRemove(sessionId, out _);
            _sessionCompletion.TryRemove(sessionId, out _);
            _sessionTasks.TryRemove(sessionId, out _);
            if (_sessionCancellation.TryRemove(sessionId, out var cts))
            {
                cts.Dispose();
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"清理测试会话失败: {sessionId}", ex);
            return false;
        }
    }

    private async Task SetSessionStatusAsync(ConfigTestSession session, ConfigTestStatus status)
    {
        await _orchestrationLock.WaitAsync().ConfigureAwait(false);
        try
        {
            session.Status = status;
        }
        finally
        {
            _orchestrationLock.Release();
        }
    }

    private async Task SetSessionEndTimeAsync(ConfigTestSession session, DateTime endTime)
    {
        await _orchestrationLock.WaitAsync().ConfigureAwait(false);
        try
        {
            session.EndTime = endTime;
        }
        finally
        {
            _orchestrationLock.Release();
        }
    }

    private async Task SetSessionOverallPassedAsync(ConfigTestSession session, bool overallPassed)
    {
        await _orchestrationLock.WaitAsync().ConfigureAwait(false);
        try
        {
            session.OverallPassed = overallPassed;
        }
        finally
        {
            _orchestrationLock.Release();
        }
    }

    private async Task SetSessionErrorMessageAsync(ConfigTestSession session, string errorMessage)
    {
        await _orchestrationLock.WaitAsync().ConfigureAwait(false);
        try
        {
            session.ErrorMessage = errorMessage;
        }
        finally
        {
            _orchestrationLock.Release();
        }
    }

    private async Task<T?> LoadSectionAsync<T>(string section) where T : class
    {
        // Prefer direct typed retrieval when the service already holds matching DTOs.
        var direct = await _configService.GetConfigurationSectionAsync<T>(section).ConfigureAwait(false);
        if (direct != null)
        {
            return direct;
        }

        // Fallback: structural JSON map from UI UnifiedConfiguration models (or any object graph).
        var raw = await _configService.GetConfigurationSectionAsync<object>(section).ConfigureAwait(false);
        if (raw == null)
        {
            return null;
        }

        if (raw is T typed)
        {
            return typed;
        }

        if (raw is JsonElement element)
        {
            return element.Deserialize<T>(JsonOptions);
        }

        var json = JsonSerializer.Serialize(raw, JsonOptions);
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    private static ConfigTestProject MapProject(OrchestratorTestProjectData data, int defaultRetryCount)
    {
        var steps = (data.Steps ?? new List<OrchestratorStepData>()).Select(step => new ConfigTestStep
        {
            Id = step.Id ?? Guid.NewGuid().ToString("N"),
            Name = step.Name ?? string.Empty,
            Description = step.Description ?? string.Empty,
            Order = step.Order ?? 0,
            Enabled = step.Enabled ?? true,
            Type = step.Type,
            TargetDeviceId = step.TargetDeviceId ?? step.Target,
            Command = step.Command,
            Expected = step.Expected,
            Timeout = step.Timeout,
            Delay = step.Delay,
            RetryCount = step.RetryCount,
            Channel = step.Channel,
            StoreResultAs = step.StoreResultAs,
            ConditionExpression = step.ConditionExpression,
            ContinueOnFailure = step.ContinueOnFailure ?? false,
            ValidationRules = CopyDictionary(step.ValidationRules),
            Parameters = CopyDictionary(step.Parameters)
        });

        return TestProjectMapper.BuildProject(
            data.Id ?? string.Empty,
            data.Name ?? "未命名项目",
            data.Description,
            data.Enabled ?? true,
            steps,
            defaultRetryCount);
    }

    private static DUTConfigInfo MapDutConfig(OrchestratorDutSection? section)
    {
        if (section == null)
        {
            return TestProjectMapper.BuildDutConfig();
        }

        return TestProjectMapper.BuildDutConfig(
            productName: section.ProductInfo?.Name,
            productModel: section.ProductInfo?.Model,
            expectedSoftwareVersion: section.ProductInfo?.ExpectedSoftwareVersion,
            defaultMaxConcurrent: section.GlobalSettings?.DefaultMaxConcurrent ?? 16,
            testTimeout: section.GlobalSettings?.TestTimeout ?? 300,
            retryCount: section.GlobalSettings?.RetryCount ?? 3,
            serialPorts: section.CommunicationEndpoints?.SerialPorts,
            networkHosts: section.CommunicationEndpoints?.NetworkHosts);
    }

    private static Dictionary<string, object>? CopyDictionary(Dictionary<string, object>? source)
    {
        return source == null ? null : new Dictionary<string, object>(source);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            var ctsList = _sessionCancellation.Values.ToList();

            foreach (var cts in ctsList)
            {
                cts.Cancel();
            }

            try
            {
                Task.WhenAll(_sessionTasks.Values).Wait(TimeSpan.FromSeconds(2));
            }
            catch
            {
                // Disposal is best effort; cancellation was already signaled.
            }

            foreach (var cts in ctsList)
            {
                cts.Dispose();
            }

            _orchestrationLock.Dispose();
            _activeSessions.Clear();
            _sessionCompletion.Clear();
            _sessionCancellation.Clear();
            _sessionTasks.Clear();
            _disposed = true;
        }
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

    /// <summary>
    /// Per-DUT execution context overrides (SerialPort, Host, etc.).
    /// </summary>
    public ConcurrentDictionary<string, Dictionary<string, object>> PerDutContexts { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

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
