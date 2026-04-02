using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UTF.Core.Caching;
using UTF.Core.Validation;
using UTF.HAL;
using UTF.Logging;

namespace UTF.Core;

/// <summary>
/// 优化的测试引擎实现
/// </summary>
public sealed class OptimizedTestEngine : ITestEngine, IDisposable
{
    private readonly ConcurrentDictionary<string, TestTask> _runningTasks = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _taskCancellationTokens = new();
    private readonly SemaphoreSlim _initializationSemaphore = new(1, 1);
    private readonly SemaphoreSlim _executionSemaphore;
    private readonly ILogger? _logger;
    private readonly ICache? _cache;
    
    private TestStatus _status = TestStatus.NotStarted;
    private bool _disposed = false;

    public OptimizedTestEngine(ILogger? logger = null, ICache? cache = null, int maxConcurrentTasks = 8)
    {
        _logger = logger ?? LoggerFactory.CreateLogger<OptimizedTestEngine>();
        _cache = cache;
        _executionSemaphore = new SemaphoreSlim(maxConcurrentTasks, maxConcurrentTasks);
    }

    public TestStatus Status => _status;
    public IReadOnlyList<TestTask> RunningTasks => _runningTasks.Values.ToList().AsReadOnly();

    public event EventHandler<TestEventArgs>? TaskStatusChanged;
    public event EventHandler<TestEventArgs>? StepCompleted;
    public event EventHandler<TestEventArgs>? TestCompleted;
    public event EventHandler<TestEventArgs>? ErrorOccurred;

    public async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _initializationSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (_status != TestStatus.NotStarted)
                return true;

            _logger?.Info("正在初始化优化的测试引擎...");
            
            _status = TestStatus.Preparing;
            _logger?.Info("优化的测试引擎初始化完成");
            
            return true;
        }
        catch (Exception ex)
        {
            _logger?.Error($"测试引擎初始化失败: {ex.Message}", ex);
            _status = TestStatus.Error;
            return false;
        }
        finally
        {
            _initializationSemaphore.Release();
        }
    }

    public async Task<bool> ShutdownAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.Info("正在关闭优化的测试引擎...");
            
            var stopTasks = _runningTasks.Keys.Select(taskId => StopTestTaskAsync(taskId, cancellationToken)).ToArray();
            await Task.WhenAll(stopTasks);
            
            _status = TestStatus.NotStarted;
            _logger?.Info("优化的测试引擎已关闭");
            
            return true;
        }
        catch (Exception ex)
        {
            _logger?.Error($"测试引擎关闭失败: {ex.Message}", ex);
            return false;
        }
    }

    public async Task<TestPlan?> LoadTestPlanAsync(string planPath, CancellationToken cancellationToken = default)
    {
        // 输入验证
        var validationResult = ValidationHelper.ValidateFileExists(planPath, "测试计划文件");
        if (!validationResult.IsValid)
        {
            _logger?.Error($"加载测试计划失败: {validationResult.GetFirstError()}");
            return null;
        }

        try
        {
            _logger?.Info($"正在加载测试计划: {planPath}");
            
            // 尝试从缓存获取
            var cacheKey = $"testplan_{planPath}_{new System.IO.FileInfo(planPath).LastWriteTimeUtc.Ticks}";
            if (_cache != null)
            {
                var cachedPlan = await _cache.GetAsync<TestPlan>(cacheKey, cancellationToken);
                if (cachedPlan != null)
                {
                    _logger?.Debug("从缓存加载测试计划");
                    return cachedPlan;
                }
            }
            
            // 实际加载逻辑（这里简化为创建示例计划）
            await Task.Delay(500, cancellationToken);
            
            var testPlan = CreateExampleTestPlan();
            
            // 缓存测试计划
            if (_cache != null)
            {
                await _cache.SetAsync(cacheKey, testPlan, TimeSpan.FromMinutes(30), cancellationToken);
            }
            
            _logger?.Info($"测试计划加载成功: {testPlan.Name}");
            return testPlan;
        }
        catch (Exception ex)
        {
            _logger?.Error($"加载测试计划失败: {ex.Message}", ex);
            return null;
        }
    }

    public async Task<bool> ValidateTestPlanAsync(TestPlan testPlan, CancellationToken cancellationToken = default)
    {
        // 输入验证
        var validationResults = new List<ValidationResult>
        {
            ValidationHelper.ValidateNotEmpty(testPlan.PlanId, "测试计划ID"),
            ValidationHelper.ValidateNotEmpty(testPlan.Name, "测试计划名称"),
            ValidationHelper.ValidateNotEmpty(testPlan.Sequences, "测试序列")
        };

        var combinedResult = ValidationHelper.Combine(validationResults.ToArray());
        if (!combinedResult.IsValid)
        {
            _logger?.Error($"测试计划验证失败: {combinedResult.GetAllErrors()}");
            return false;
        }

        try
        {
            _logger?.Info($"正在验证测试计划: {testPlan.Name}");
            
            foreach (var sequence in testPlan.Sequences)
            {
                if (string.IsNullOrWhiteSpace(sequence.SequenceId) || !sequence.Steps.Any())
                {
                    _logger?.Error($"测试序列 {sequence.Name} 无效");
                    return false;
                }
                
                foreach (var step in sequence.Steps)
                {
                    if (string.IsNullOrWhiteSpace(step.StepId) || string.IsNullOrWhiteSpace(step.Command))
                    {
                        _logger?.Error($"测试步骤 {step.Name} 无效");
                        return false;
                    }
                }
            }
            
            await Task.Delay(100, cancellationToken);
            
            _logger?.Info($"测试计划验证成功: {testPlan.Name}");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.Error($"测试计划验证失败: {ex.Message}", ex);
            return false;
        }
    }

    public async Task<TestTask?> CreateTestTaskAsync(TestPlan testPlan, List<string> dutIds, string? operatorName = null, CancellationToken cancellationToken = default)
    {
        // 输入验证
        var validationResults = new List<ValidationResult>
        {
            ValidationHelper.ValidateNotNull(testPlan, "测试计划"),
            ValidationHelper.ValidateNotEmpty(dutIds, "DUT列表")
        };

        var combinedResult = ValidationHelper.Combine(validationResults.ToArray());
        if (!combinedResult.IsValid)
        {
            _logger?.Error($"创建测试任务失败: {combinedResult.GetAllErrors()}");
            return null;
        }

        try
        {
            var taskId = Guid.NewGuid().ToString();
            _logger?.Info($"正在创建测试任务: {taskId}");
            
            var testTask = new TestTask
            {
                TaskId = taskId,
                Name = $"测试任务 - {testPlan.Name}",
                TestPlan = testPlan,
                TargetDUTIds = dutIds,
                Status = TestStatus.NotStarted,
                Priority = TestPriority.Normal,
                Operator = operatorName ?? "Unknown",
                TestStation = Environment.MachineName,
                Variables = new Dictionary<string, object>(),
                CreatedTime = DateTime.UtcNow
            };
            
            _runningTasks.TryAdd(taskId, testTask);
            await Task.Delay(100, cancellationToken);
            
            _logger?.Info($"测试任务创建成功: {taskId}");
            return testTask;
        }
        catch (Exception ex)
        {
            _logger?.Error($"创建测试任务失败: {ex.Message}", ex);
            return null;
        }
    }

    public async Task<TestReport?> ExecuteTestTaskAsync(TestTask testTask, CancellationToken cancellationToken = default)
    {
        // 使用信号量限制并发执行
        await _executionSemaphore.WaitAsync(cancellationToken);
        
        var taskCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _taskCancellationTokens.TryAdd(testTask.TaskId, taskCts);
        
        try
        {
            _logger?.Info($"开始执行测试任务: {testTask.TaskId}");
            
            var updatedTask = testTask with { Status = TestStatus.Running, StartTime = DateTime.UtcNow };
            _runningTasks.TryUpdate(testTask.TaskId, updatedTask, testTask);
            
            TaskStatusChanged?.Invoke(this, new TestEventArgs
            {
                TaskId = testTask.TaskId,
                EventType = "TaskStarted",
                Timestamp = DateTime.UtcNow
            });
            
            var stepResults = new List<TestStepExecutionResult>();
            
            foreach (var dutId in testTask.TargetDUTIds)
            {
                if (taskCts.Token.IsCancellationRequested)
                    break;
                
                _logger?.Info($"开始测试DUT: {dutId}");
                
                foreach (var sequence in testTask.TestPlan.Sequences)
                {
                    var sequenceResults = await ExecuteTestSequenceAsync(sequence, dutId, taskCts.Token);
                    stepResults.AddRange(sequenceResults);
                }
            }
            
            var report = new TestReport
            {
                ReportId = Guid.NewGuid().ToString(),
                TaskId = testTask.TaskId,
                DUTId = string.Join(",", testTask.TargetDUTIds),
                OverallResult = stepResults.All(r => r.Passed),
                StepResults = stepResults,
                StartTime = testTask.StartTime ?? DateTime.UtcNow,
                EndTime = DateTime.UtcNow,
                Operator = testTask.Operator,
                TestStation = testTask.TestStation
            };
            
            var completedTask = updatedTask with { Status = TestStatus.Completed, EndTime = DateTime.UtcNow };
            _runningTasks.TryUpdate(testTask.TaskId, completedTask, updatedTask);
            
            TestCompleted?.Invoke(this, new TestEventArgs
            {
                TaskId = testTask.TaskId,
                EventType = "TaskCompleted",
                Data = report,
                Timestamp = DateTime.UtcNow
            });
            
            _logger?.Info($"测试任务执行完成: {testTask.TaskId}, 结果: {(report.OverallResult ? "通过" : "失败")}");
            
            return report;
        }
        catch (OperationCanceledException)
        {
            _logger?.Warning($"测试任务被取消: {testTask.TaskId}");
            return null;
        }
        catch (Exception ex)
        {
            _logger?.Error($"执行测试任务失败: {ex.Message}", ex);
            
            ErrorOccurred?.Invoke(this, new TestEventArgs
            {
                TaskId = testTask.TaskId,
                EventType = "TaskError",
                Data = ex.Message,
                Timestamp = DateTime.UtcNow
            });
            
            return null;
        }
        finally
        {
            _taskCancellationTokens.TryRemove(testTask.TaskId, out _);
            taskCts.Dispose();
            _executionSemaphore.Release();
        }
    }

    public async Task<List<TestStepExecutionResult>> ExecuteTestSequenceAsync(TestSequence sequence, string dutId, CancellationToken cancellationToken = default)
    {
        var results = new List<TestStepExecutionResult>();
        
        _logger?.Info($"开始执行测试序列: {sequence.Name} (DUT: {dutId})");
        
        foreach (var step in sequence.Steps)
        {
            if (cancellationToken.IsCancellationRequested)
                break;
            
            var result = await ExecuteTestStepAsync(step, dutId, cancellationToken);
            results.Add(result);
            
            StepCompleted?.Invoke(this, new TestEventArgs
            {
                DUTId = dutId,
                EventType = "StepCompleted",
                Data = result,
                Timestamp = DateTime.UtcNow
            });
            
            if (!result.Passed && sequence.StopOnFailure)
            {
                _logger?.Warning($"步骤失败，停止序列执行: {step.Name}");
                break;
            }
        }
        
        _logger?.Info($"测试序列执行完成: {sequence.Name}, 通过步骤: {results.Count(r => r.Passed)}/{results.Count}");
        
        return results;
    }

    public async Task<TestStepExecutionResult> ExecuteTestStepAsync(TestStep step, string dutId, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        _logger?.Debug($"执行测试步骤: {step.Name} (DUT: {dutId})");
        
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(step.Timeout);
            
            // 根据步骤类型执行不同的操作
            object? measuredValue = step.StepType switch
            {
                "Connection" => await SimulateConnectionTest(step, dutId, timeoutCts.Token),
                "Measurement" => await SimulateMeasurement(step, dutId, timeoutCts.Token),
                "Command" => await SimulateCommand(step, dutId, timeoutCts.Token),
                _ => await SimulateGenericTest(step, dutId, timeoutCts.Token)
            };
            
            var endTime = DateTime.UtcNow;
            bool passed = ValidateResult(measuredValue, step);
            
            return new TestStepExecutionResult
            {
                StepId = step.StepId,
                StepName = step.Name,
                Passed = passed,
                MeasuredValue = measuredValue,
                ExpectedValue = step.ExpectedResult,
                StartTime = startTime,
                EndTime = endTime,
                RetryCount = 0
            };
        }
        catch (OperationCanceledException)
        {
            return new TestStepExecutionResult
            {
                StepId = step.StepId,
                StepName = step.Name,
                Passed = false,
                ErrorMessage = "步骤执行超时",
                StartTime = startTime,
                EndTime = DateTime.UtcNow,
                RetryCount = 0
            };
        }
        catch (Exception ex)
        {
            _logger?.Error($"步骤执行失败: {step.Name}, 错误: {ex.Message}", ex);
            
            return new TestStepExecutionResult
            {
                StepId = step.StepId,
                StepName = step.Name,
                Passed = false,
                ErrorMessage = ex.Message,
                StartTime = startTime,
                EndTime = DateTime.UtcNow,
                RetryCount = 0
            };
        }
    }

    private async Task<object?> SimulateConnectionTest(TestStep step, string dutId, CancellationToken cancellationToken)
    {
        await Task.Delay(100, cancellationToken); // 优化：减少模拟延迟
        return true;
    }

    private async Task<object?> SimulateMeasurement(TestStep step, string dutId, CancellationToken cancellationToken)
    {
        await Task.Delay(50, cancellationToken); // 优化：减少模拟延迟
        
        if (step.ExpectedResult is double expectedDouble)
        {
            var random = new Random();
            return expectedDouble + (random.NextDouble() - 0.5) * 0.2;
        }
        
        return step.ExpectedResult;
    }

    private async Task<object?> SimulateCommand(TestStep step, string dutId, CancellationToken cancellationToken)
    {
        await Task.Delay(50, cancellationToken); // 优化：减少模拟延迟
        return "OK";
    }

    private async Task<object?> SimulateGenericTest(TestStep step, string dutId, CancellationToken cancellationToken)
    {
        await Task.Delay(50, cancellationToken); // 优化：减少模拟延迟
        return true;
    }

    private bool ValidateResult(object? measuredValue, TestStep step)
    {
        if (measuredValue == null)
            return false;
        
        if (step.MinValue != null && step.MaxValue != null)
        {
            if (measuredValue is double doubleValue && step.MinValue is double minDouble && step.MaxValue is double maxDouble)
            {
                return doubleValue >= minDouble && doubleValue <= maxDouble;
            }
        }
        
        if (step.ExpectedResult != null)
        {
            return measuredValue.Equals(step.ExpectedResult);
        }
        
        return true;
    }

    public async Task<bool> PauseTestTaskAsync(string taskId, CancellationToken cancellationToken = default)
    {
        if (_runningTasks.TryGetValue(taskId, out var task))
        {
            var pausedTask = task with { Status = TestStatus.Paused };
            _runningTasks.TryUpdate(taskId, pausedTask, task);
            
            TaskStatusChanged?.Invoke(this, new TestEventArgs
            {
                TaskId = taskId,
                EventType = "TaskPaused",
                Timestamp = DateTime.UtcNow
            });
            
            await Task.Delay(100, cancellationToken);
            return true;
        }
        return false;
    }

    public async Task<bool> ResumeTestTaskAsync(string taskId, CancellationToken cancellationToken = default)
    {
        if (_runningTasks.TryGetValue(taskId, out var task) && task.Status == TestStatus.Paused)
        {
            var resumedTask = task with { Status = TestStatus.Running };
            _runningTasks.TryUpdate(taskId, resumedTask, task);
            
            TaskStatusChanged?.Invoke(this, new TestEventArgs
            {
                TaskId = taskId,
                EventType = "TaskResumed",
                Timestamp = DateTime.UtcNow
            });
            
            await Task.Delay(100, cancellationToken);
            return true;
        }
        return false;
    }

    public async Task<bool> StopTestTaskAsync(string taskId, CancellationToken cancellationToken = default)
    {
        if (_taskCancellationTokens.TryGetValue(taskId, out var cts))
        {
            cts.Cancel();
        }
        
        if (_runningTasks.TryGetValue(taskId, out var task))
        {
            var stoppedTask = task with { Status = TestStatus.Aborted, EndTime = DateTime.UtcNow };
            _runningTasks.TryUpdate(taskId, stoppedTask, task);
            
            TaskStatusChanged?.Invoke(this, new TestEventArgs
            {
                TaskId = taskId,
                EventType = "TaskStopped",
                Timestamp = DateTime.UtcNow
            });
            
            await Task.Delay(100, cancellationToken);
            return true;
        }
        return false;
    }

    public async Task<TestStatus> GetTaskStatusAsync(string taskId, CancellationToken cancellationToken = default)
    {
        await Task.Delay(10, cancellationToken);
        return _runningTasks.TryGetValue(taskId, out var task) ? task.Status : TestStatus.NotStarted;
    }

    public async Task<TestReport?> GetTestReportAsync(string taskId, string dutId, CancellationToken cancellationToken = default)
    {
        await Task.Delay(100, cancellationToken);
        
        if (_runningTasks.TryGetValue(taskId, out var task))
        {
            return new TestReport
            {
                ReportId = Guid.NewGuid().ToString(),
                TaskId = taskId,
                DUTId = dutId,
                OverallResult = true,
                StepResults = new List<TestStepExecutionResult>(),
                StartTime = task.StartTime ?? DateTime.UtcNow,
                EndTime = task.EndTime ?? DateTime.UtcNow,
                Operator = task.Operator,
                TestStation = task.TestStation
            };
        }
        
        return null;
    }

    public async Task<bool> CleanupTestEnvironmentAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.Info("正在清理测试环境...");
            
            var completedTasks = _runningTasks.Values.Where(t => t.Status == TestStatus.Completed || t.Status == TestStatus.Aborted).ToList();
            foreach (var task in completedTasks)
            {
                _runningTasks.TryRemove(task.TaskId, out _);
            }
            
            await Task.Delay(100, cancellationToken);
            
            _logger?.Info("测试环境清理完成");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.Error($"清理测试环境失败: {ex.Message}", ex);
            return false;
        }
    }

    private TestPlan CreateExampleTestPlan()
    {
        return new TestPlan
        {
            PlanId = Guid.NewGuid().ToString(),
            Name = "优化测试计划",
            Description = "优化的测试计划示例",
            Version = "1.0",
            TargetDUTType = "Smartphone",
            CreatedBy = "System",
            MaxParallelDUTs = 8,
            Sequences = new List<TestSequence>
            {
                new TestSequence
                {
                    SequenceId = "SEQ_001",
                    Name = "基础功能测试",
                    Description = "基础功能测试序列",
                    Steps = new List<TestStep>
                    {
                        new TestStep
                        {
                            StepId = "STEP_001",
                            Name = "连接测试",
                            Description = "测试设备连接",
                            StepType = "Connection",
                            Command = "Connect",
                            Timeout = TimeSpan.FromSeconds(30)
                        }
                    },
                    RequiredDevices = new List<string> { "DMM_001" },
                    EstimatedDuration = TimeSpan.FromMinutes(5),
                    AllowParallelExecution = true
                }
            }
        };
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            foreach (var cts in _taskCancellationTokens.Values)
            {
                cts.Cancel();
                cts.Dispose();
            }
            
            _taskCancellationTokens.Clear();
            _runningTasks.Clear();
            _initializationSemaphore.Dispose();
            _executionSemaphore.Dispose();
            
            _disposed = true;
        }
    }
}

