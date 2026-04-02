using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using UTF.Core;
using UTF.Plugin.Abstractions;
using UTF.Plugin.Host;
using UTF.UI.Models;

namespace UTF.UI.Services
{
    /// <summary>
    /// DUT监控台管理器
    /// </summary>
    public class DUTMonitorManager : IDUTMonitorService
    {
        private readonly ConfigurationManager _configManager;
        private readonly IConfigurationAdapter _configAdapter;
        private readonly StepExecutorPluginHost _pluginHost;
        private readonly System.Threading.SemaphoreSlim _pluginInitSemaphore = new(1, 1);
        private ObservableCollection<DUTMonitorItem> _dutItems;
        private DataGrid? _dataGrid;
        private bool _pluginsInitialized;

        // 事件：请求更新统计信息
        public event Action? StatisticsUpdateRequested;

        // 事件：所有测试已完成
        public event Action? AllTestsCompleted;

        public ObservableCollection<DUTMonitorItem> DUTItems => _dutItems;

        /// <summary>
        /// 已加载的插件列表（InitializeAsync 后可用）
        /// </summary>
        public IReadOnlyList<PluginMetadata> LoadedPlugins => _pluginHost.LoadedPlugins;

        /// <summary>
        /// 最近一次加载报告
        /// </summary>
        public PluginLoadReport? LastLoadReport { get; private set; }

        public DUTMonitorManager(ConfigurationManager configManager, IConfigurationAdapter configAdapter, StepExecutorPluginHost pluginHost)
        {
            _configManager = configManager;
            _configAdapter = configAdapter;
            _pluginHost = pluginHost;
            _dutItems = new ObservableCollection<DUTMonitorItem>();
        }

        /// <summary>
        /// 获取DUT监控项列表
        /// </summary>
        public List<DUTMonitorItem> GetDUTItems() => _dutItems.ToList();

        /// <summary>
        /// 初始化DUT监控台
        /// </summary>
        public async Task InitializeAsync(DataGrid dataGrid)
        {
            _dataGrid = dataGrid;
            System.Diagnostics.Debug.WriteLine("开始初始化DUT监控台...");
            
            await LoadDUTConfigurationAsync();
            System.Diagnostics.Debug.WriteLine($"配置加载完成，共有 {_dutItems.Count} 个DUT项");
            
            await GenerateDynamicColumnsAsync();
            System.Diagnostics.Debug.WriteLine("动态列生成完成");
            
            // 确保ItemsSource被设置，即使生成列失败
            if (_dataGrid != null && _dataGrid.ItemsSource == null)
            {
                System.Diagnostics.Debug.WriteLine("强制设置ItemsSource...");
                _dataGrid.ItemsSource = _dutItems;
                
                // 强制刷新
                _dataGrid.Items.Refresh();
                _dataGrid.UpdateLayout();
                _dataGrid.InvalidateVisual();
                
                System.Diagnostics.Debug.WriteLine($"强制设置完成，ItemsSource包含 {_dutItems.Count} 个项目");
            }
            
            // 通知统计更新
            System.Diagnostics.Debug.WriteLine("DUT监控台初始化完成，准备更新统计信息");
            StatisticsUpdateRequested?.Invoke();
        }

        /// <summary>
        /// 从统一配置文件加载DUT配置并生成DUT项
        /// </summary>
        private async Task LoadDUTConfigurationAsync()
        {
            try
            {
                // 使用统一配置管理器
                var unifiedConfig = await _configManager.GetUnifiedConfigurationAsync();
                
                // 获取DUT配置和测试项目配置
                var dutConfig = unifiedConfig.DUTConfiguration;
                var testProjectConfig = unifiedConfig.TestProjectConfiguration;

                _dutItems.Clear();

                // 使用ConfigurationAdapter统一获取配置值（自动兼容新旧格式）
                var dutCount = _configAdapter.GetMaxConcurrent(unifiedConfig);
                var namingTemplate = _configAdapter.GetNamingTemplate(unifiedConfig);
                var idTemplate = _configAdapter.GetIdTemplate(unifiedConfig);
                var productName = dutConfig.ProductInfo?.Name
                               ?? _configAdapter.GetProductModel(unifiedConfig);
                var serialPorts = _configAdapter.GetSerialPorts(unifiedConfig);
                var networkHosts = _configAdapter.GetNetworkHosts(unifiedConfig);
                var deviceType = dutConfig.ProductInfo?.Category
                              ?? "通用DUT";

                System.Diagnostics.Debug.WriteLine($"准备生成 {dutCount} 个DUT监控项，命名模板: {namingTemplate}");

                // 生成DUT监控项
                for (int i = 1; i <= dutCount; i++)
                {
                    var serialPort = GetSerialPortForDUT(i, serialPorts);
                    var dutName = namingTemplate
                        .Replace("{TypeName}", productName)
                        .Replace("{Index}", i.ToString());
                    var dutId = idTemplate.Replace("{Index}", i.ToString());

                    var dutItem = new DUTMonitorItem
                    {
                        DutId = dutId,
                        DutName = $"🔧 {dutName}",
                        DeviceType = deviceType,
                        SerialNumber = serialPort,
                        OverallStatus = DUTMonitorStatus.Idle,
                        CurrentStepText = "待机中",
                        TestSteps = new ObservableCollection<DUTTestStep>(),
                        Logs = new ObservableCollection<string>()
                    };

                    // 根据测试项目配置生成测试步骤（使用配置适配器，支持新旧格式）
                    var testSteps = _configAdapter.GetTestSteps(unifiedConfig);
                    if (testSteps != null && testSteps.Any())
                    {
                        // 动态解析配置文件中的测试步骤
                        foreach (var configStep in testSteps.Where(s => s.Enabled).OrderBy(s => s.Order))
                        {
                            dutItem.TestSteps.Add(new DUTTestStep
                            {
                                StepId = configStep.Id,
                                StepName = configStep.Name,
                                Order = configStep.Order,
                                Status = DUTMonitorStepStatus.Pending,
                                Parameters = new Dictionary<string, object>
                                {
                                    ["Description"] = configStep.Description ?? "",
                                    ["Enabled"] = configStep.Enabled,
                                    ["Target"] = configStep.Target ?? "dut",
                                    ["Type"] = configStep.Type ?? "custom",
                                    ["Command"] = configStep.Command ?? "",
                                    ["Timeout"] = configStep.Timeout ?? 5000,
                                    ["Expected"] = configStep.Expected ?? "",
                                    ["Delay"] = configStep.Delay ?? 500,
                                    ["ContinueOnFailure"] = configStep.ContinueOnFailure,
                                    ["Channel"] = configStep.Channel ?? ""
                                }
                            });
                            
                            // 如果有验证规则，也添加进去
                            if (configStep.ValidationRules != null)
                            {
                                foreach (var rule in configStep.ValidationRules)
                                {
                                    dutItem.TestSteps.Last().Parameters[rule.Key] = rule.Value;
                                }
                            }
                        }
                    }

                    // 如果没有从配置加载到测试步骤，保持为空

                    _dutItems.Add(dutItem);
                    System.Diagnostics.Debug.WriteLine($"已添加DUT项: {dutItem.DutId} - {dutItem.DutName}");
                }
                
                System.Diagnostics.Debug.WriteLine($"总共生成了 {_dutItems.Count} 个DUT项");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载DUT配置失败: {ex.Message}");
                
                // 如果配置加载失败，创建默认DUT项
                _dutItems.Clear();
                for (int i = 1; i <= 5; i++)
                {
                    _dutItems.Add(new DUTMonitorItem
                    {
                        DutId = $"DUT-{i:D2}",
                        DutName = $"默认设备{i}",
                        DeviceType = "Unknown",
                        SerialNumber = $"SN{DateTime.Now:yyyyMMdd}{i:D3}",
                        OverallStatus = DUTMonitorStatus.Error,
                        CurrentStepText = "配置加载失败",
                        TestSteps = new ObservableCollection<DUTTestStep>()
                    });
                }
            }
        }

        /// <summary>
        /// 动态生成DataGrid列
        /// </summary>
        private async Task GenerateDynamicColumnsAsync()
        {
            System.Diagnostics.Debug.WriteLine($"GenerateDynamicColumnsAsync开始: DataGrid={_dataGrid != null}, DUT项数量={_dutItems.Count}");
            
            if (_dataGrid == null)
            {
                System.Diagnostics.Debug.WriteLine("DataGrid为空，退出");
                return;
            }
            
            if (!_dutItems.Any())
            {
                System.Diagnostics.Debug.WriteLine("没有DUT项，退出");
                return;
            }

            try
            {
                // 使用统一配置
                var unifiedConfig = await _configManager.GetUnifiedConfigurationAsync();
                var testProjectConfig = unifiedConfig.TestProjectConfiguration;
                var dutConfig = unifiedConfig.DUTConfiguration;

                // 清除现有的动态列（保留基本列）
                var columnsToRemove = _dataGrid.Columns
                    .Where(c => c.Header?.ToString()?.Contains("步骤") == true || 
                               c.Header?.ToString()?.Contains("Step") == true ||
                               c.Header?.ToString()?.Contains("检查") == true ||
                               c.Header?.ToString()?.Contains("测试") == true ||
                               c.Header?.ToString()?.Contains("连接") == true ||
                               c.Header?.ToString()?.Contains("版本") == true ||
                               c.Header?.ToString()?.Contains("功能") == true)
                    .ToList();

                foreach (var column in columnsToRemove)
                {
                    _dataGrid.Columns.Remove(column);
                }

                // 基于配置动态生成测试步骤列
                // 使用ConfigurationAdapter获取测试步骤（自动兼容新旧格式）
                var testSteps = _configAdapter.GetTestSteps(unifiedConfig);

                if (testSteps != null && testSteps.Any())
                {
                    var enabledSteps = testSteps.Where(s => s.Enabled).OrderBy(s => s.Order).ToList();
                    
                    Console.WriteLine($"[DUTMonitor] 生成DataGrid列，测试步骤数: {enabledSteps.Count}");
                    
                    // 为每个测试步骤生成列
                    for (int i = 0; i < enabledSteps.Count; i++)
                    {
                        var step = enabledSteps[i];
                        var column = CreateTestStepColumnFromName(step.Name, i);
                        _dataGrid.Columns.Insert(_dataGrid.Columns.Count - 1, column); // 插入到最新日志列之前
                        Console.WriteLine($"[DUTMonitor] 添加列 {i + 1}: {step.Name}");
                    }
                }
                else
                {
                    // 如果没有配置，使用默认步骤列
                    var defaultSteps = new List<string> { "连接测试", "版本检查", "功能测试" };
                    for (int i = 0; i < defaultSteps.Count; i++)
                    {
                        var column = CreateTestStepColumnFromName(defaultSteps[i], i);
                        _dataGrid.Columns.Insert(_dataGrid.Columns.Count - 1, column);
                    }
                }

                // 绑定数据源
                _dataGrid.ItemsSource = _dutItems;
                
                // 强制刷新DataGrid
                _dataGrid.Items.Refresh();
                _dataGrid.UpdateLayout();
                _dataGrid.InvalidateVisual();
                
                System.Diagnostics.Debug.WriteLine($"已绑定 {_dutItems.Count} 个DUT项到DataGrid并强制刷新");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"生成动态列失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 根据步骤名称创建测试步骤列
        /// </summary>
        private DataGridTemplateColumn CreateTestStepColumnFromName(string stepName, int stepIndex)
        {
            var column = new DataGridTemplateColumn
            {
                Header = stepName,
                Width = new DataGridLength(80), // 稍微减少宽度，给日志列更多空间
                MinWidth = 60 // 设置最小宽度，防止过小
            };

            // 创建单元格模板
            var cellTemplate = new DataTemplate();
            
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(2));
            borderFactory.SetValue(Border.PaddingProperty, new Thickness(3, 1, 3, 1));
            borderFactory.SetValue(Border.MarginProperty, new Thickness(1));
            borderFactory.SetBinding(Border.BackgroundProperty, new Binding($"TestSteps[{stepIndex}].StatusBrush"));

            var stackPanelFactory = new FrameworkElementFactory(typeof(StackPanel));
            borderFactory.AppendChild(stackPanelFactory);

            // 状态文本
            var statusTextFactory = new FrameworkElementFactory(typeof(TextBlock));
            statusTextFactory.SetBinding(TextBlock.TextProperty, new Binding($"TestSteps[{stepIndex}].StatusText"));
            statusTextFactory.SetValue(TextBlock.ForegroundProperty, System.Windows.Media.Brushes.White);
            statusTextFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            statusTextFactory.SetValue(TextBlock.FontSizeProperty, 7.0);
            statusTextFactory.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center);
            stackPanelFactory.AppendChild(statusTextFactory);

            cellTemplate.VisualTree = borderFactory;
            column.CellTemplate = cellTemplate;

            return column;
        }

        /// <summary>
        /// 开始测试指定DUT
        /// </summary>
        public async Task StartTestAsync(string dutId)
        {
            var dutItem = _dutItems.FirstOrDefault(d => d.DutId == dutId);
            if (dutItem == null) return;

            dutItem.OverallStatus = DUTMonitorStatus.Running;
            dutItem.StartTime = DateTime.Now;
            dutItem.CurrentStepText = "开始测试";
            dutItem.AddLog("测试开始");

            // 模拟测试执行
            await ExecuteTestStepsAsync(dutItem);
        }

        /// <summary>
        /// 执行测试步骤
        /// </summary>
        private async Task ExecuteTestStepsAsync(DUTMonitorItem dutItem)
        {
            try
            {
                bool allPassed = true;

                foreach (var step in dutItem.TestSteps.OrderBy(s => s.Order))
                {
                    dutItem.CurrentStepText = $"执行: {step.StepName}";
                    step.Status = DUTMonitorStepStatus.Running;
                    step.StartTime = DateTime.Now;

                    // 模拟测试步骤执行
                    await Task.Delay(Random.Shared.Next(1000, 3000));

                    // 随机生成测试结果 (80%通过率)
                    bool stepPassed = Random.Shared.NextDouble() > 0.2;
                    
                    step.Status = stepPassed ? DUTMonitorStepStatus.Passed : DUTMonitorStepStatus.Failed;
                    step.EndTime = DateTime.Now;

                    if (!stepPassed)
                    {
                        allPassed = false;
                        step.ErrorMessage = $"步骤 {step.StepName} 执行失败";
                        dutItem.AddLog($"步骤失败: {step.StepName}");
                    }
                    else
                    {
                        dutItem.AddLog($"步骤完成: {step.StepName}");
                    }

                    // 如果关键步骤失败，停止测试
                    if (!stepPassed && step.StepName.Contains("初始化"))
                    {
                        break;
                    }
                }

                // 设置最终状态
                dutItem.OverallStatus = allPassed ? DUTMonitorStatus.Passed : DUTMonitorStatus.Failed;
                dutItem.EndTime = DateTime.Now;
                dutItem.CurrentStepText = allPassed ? "测试完成 - PASS" : "测试完成 - FAIL";
                dutItem.AddLog($"测试完成: {(allPassed ? "PASS" : "FAIL")}");
            }
            catch (Exception ex)
            {
                dutItem.OverallStatus = DUTMonitorStatus.Error;
                dutItem.EndTime = DateTime.Now;
                dutItem.CurrentStepText = "测试错误";
                dutItem.AddLog($"测试错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 开始所有DUT测试
        /// </summary>
        public async Task StartAllTestsAsync()
        {
            var logger = UTF.Logging.LoggerFactory.CreateLogger("DUTMonitor");
            
            // 获取配置信息（使用配置适配器，支持新旧格式）
            var unifiedConfig = await _configManager.GetUnifiedConfigurationAsync();
            
            // 调试输出
            logger.Info($"[DEBUG] TestProjectConfiguration: {unifiedConfig.TestProjectConfiguration != null}");
            logger.Info($"[DEBUG] TestProject: {unifiedConfig.TestProjectConfiguration?.TestProject != null}");
            logger.Info($"[DEBUG] Steps Count: {unifiedConfig.TestProjectConfiguration?.TestProject?.Steps?.Count ?? 0}");
            if (unifiedConfig.TestProjectConfiguration?.TestProject?.Steps != null)
            {
                foreach (var step in unifiedConfig.TestProjectConfiguration.TestProject.Steps)
                {
                    logger.Info($"[DEBUG] Step: {step.Name}, Type: {step.Type}, Command: {step.Command}, Enabled: {step.Enabled}");
                }
            }

            var testSteps = _configAdapter.GetTestSteps(unifiedConfig);
            logger.Info($"[DEBUG] GetTestSteps返回: {testSteps?.Count ?? 0} 个步骤");
            
            if (testSteps == null || !testSteps.Any())
            {
                logger.Error("未找到有效的测试步骤配置");
                return;
            }
            
            logger.Info($"开始执行测试，包含 {testSteps.Count} 个测试步骤");
            
            // 收集需要测试的DUT
            var dutsToTest = _dutItems.Where(d => d.OverallStatus == DUTMonitorStatus.Idle).ToList();
            
            if (!dutsToTest.Any())
            {
                logger.Warning("没有待测试的DUT");
                return;
            }
            
            logger.Info($"开始测试 {dutsToTest.Count} 个DUT");
            
            // 启动所有测试任务
            var testTasks = new List<Task>();
            
            foreach (var dutItem in dutsToTest)
            {
                // 启动测试
                dutItem.OverallStatus = DUTMonitorStatus.Running;
                dutItem.CurrentStepText = "正在执行测试...";
                AddDUTLog(dutItem.DutId, "开始执行测试");
                
                // 创建测试任务
                var testTask = Task.Run(async () =>
                {
                    await ExecuteConfigBasedTestAsync(dutItem, testSteps, logger);
                });
                
                testTasks.Add(testTask);
            }
            
            // 等待所有测试完成并触发完成事件（使用独立的监控任务）
            _ = Task.Run(async () =>
            {
                try
                {
                    // 设置超时时间（防止测试任务永久卡住）
                    var timeout = TimeSpan.FromMinutes(30); // 30分钟超时
                    var allTestsTask = Task.WhenAll(testTasks);
                    var timeoutTask = Task.Delay(timeout);
                    var completedTask = await Task.WhenAny(allTestsTask, timeoutTask);
                    
                    if (completedTask == timeoutTask)
                    {
                        logger.Warning($"测试任务超时（{timeout.TotalMinutes}分钟），强制完成");
                    }
                    else
                    {
                        logger.Info("所有DUT测试正常完成");
                    }
                    
                    logger.Info("所有DUT测试流程结束");
                    
                    // 触发测试完成事件
                    AllTestsCompleted?.Invoke();
                }
                catch (Exception ex)
                {
                    logger.Error($"测试过程中发生错误: {ex.Message}");
                    logger.Error($"错误堆栈: {ex.StackTrace}");
                    
                    // 即使有错误也要触发完成事件
                    AllTestsCompleted?.Invoke();
                }
                finally
                {
                    // 确保在任何情况下都触发完成事件
                    logger.Info("确保触发测试完成事件");
                }
            });
        }

        /// <summary>
        /// 基于配置执行测试（支持新旧配置格式）
        /// </summary>
        public async Task ExecuteConfigBasedTestAsync(DUTMonitorItem dutItem, List<TestStepConfig> testSteps, UTF.Logging.ILogger logger)
        {
            try
            {
                if (testSteps == null || !testSteps.Any())
                {
                    throw new InvalidOperationException("测试项目中没有定义测试步骤");
                }

                var enabledSteps = testSteps.Where(s => s.Enabled).OrderBy(s => s.Order).ToList();
                logger.Info($"[DUT][Test] {dutItem.DutId}: 开始执行 {enabledSteps.Count} 个测试步骤");
                
                bool allStepsPassed = true;
                string failureReason = "";
                
                for (int i = 0; i < enabledSteps.Count; i++)
                {
                    var configStep = enabledSteps[i];
                    var dutStep = dutItem.TestSteps.FirstOrDefault(s => s.StepId == configStep.Id);
                    
                    if (dutStep == null) continue;
                    
                    // 更新UI状态
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        dutStep.Status = DUTMonitorStepStatus.Running;
                        dutStep.StartTime = DateTime.Now;
                        dutItem.CurrentStepText = $"执行步骤: {configStep.Name}";
                    });
                    
                    AddDUTLog(dutItem.DutId, $"开始执行步骤 {configStep.Order}: {configStep.Name}");
                    
                    try
                    {
                        // 执行具体的测试步骤
                        var stepResult = await ExecuteTestStepAsync(dutItem, configStep, logger);
                        
                        // 更新步骤结果
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            dutStep.Status = stepResult.Passed ? DUTMonitorStepStatus.Passed : DUTMonitorStepStatus.Failed;
                            dutStep.EndTime = DateTime.Now;
                            dutStep.ErrorMessage = stepResult.Passed ? "" : stepResult.ErrorMessage;
                        });
                        
                        if (stepResult.Passed)
                        {
                            AddDUTLog(dutItem.DutId, $"步骤 {configStep.Name} 执行成功");
                        }
                        else
                        {
                            AddDUTLog(dutItem.DutId, $"步骤 {configStep.Name} 执行失败: {stepResult.ErrorMessage}");
                            allStepsPassed = false;
                            failureReason = stepResult.ErrorMessage;
                            
                            if (!configStep.ContinueOnFailure)
                            {
                                logger.Warning($"DUT {dutItem.DutId}: 步骤失败且设置为不继续执行，终止测试");
                                break;
                            }
                        }
                        
                        // 执行后延时
                        var delay = configStep.Delay ?? 0;
                        if (delay > 0)
                        {
                            await Task.Delay(delay);
                        }
                    }
                    catch (Exception stepEx)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            dutStep.Status = DUTMonitorStepStatus.Error;
                            dutStep.EndTime = DateTime.Now;
                            dutStep.ErrorMessage = stepEx.Message;
                        });
                        
                        AddDUTLog(dutItem.DutId, $"步骤 {configStep.Name} 执行异常: {stepEx.Message}");
                        allStepsPassed = false;
                        failureReason = stepEx.Message;
                        
                        if (!configStep.ContinueOnFailure)
                        {
                            break;
                        }
                    }
                }
                
                // 更新最终状态
                Application.Current.Dispatcher.Invoke(() =>
                {
                    dutItem.OverallStatus = allStepsPassed ? DUTMonitorStatus.Passed : DUTMonitorStatus.Failed;
                    dutItem.CurrentStepText = allStepsPassed ? "测试通过" : $"测试失败: {failureReason}";
                });
                
                AddDUTLog(dutItem.DutId, $"测试完成: {(allStepsPassed ? "通过" : "失败")}");
                logger.Info($"[DUT][Test] {dutItem.DutId}: 测试完成，结果: {(allStepsPassed ? "通过" : "失败")}");
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    dutItem.OverallStatus = DUTMonitorStatus.Error;
                    dutItem.CurrentStepText = $"测试异常: {ex.Message}";
                });
                
                AddDUTLog(dutItem.DutId, $"测试异常: {ex.Message}");
                logger.Error($"[DUT][Test] {dutItem.DutId}: 测试执行异常", ex);
            }
        }

        /// <summary>
        /// 执行单个测试步骤
        /// </summary>
        private async Task EnsurePluginsInitializedAsync(UTF.Logging.ILogger logger)
        {
            if (_pluginsInitialized)
            {
                return;
            }

            await _pluginInitSemaphore.WaitAsync();
            try
            {
                if (_pluginsInitialized)
                {
                    return;
                }

                var report = await _pluginHost.InitializeAsync();
                LastLoadReport = report;
                _pluginsInitialized = true;
                logger.Info($"[Plugin] 初始化完成，已加载 {report.LoadedCount} 个插件，失败 {report.FailedCount} 个。");

                if (report.FailedCount > 0)
                {
                    foreach (var issue in report.Issues)
                    {
                        logger.Warning($"[Plugin] {issue.ErrorCode}: {issue.ManifestPath} - {issue.Message}");
                    }
                }
            }
            finally
            {
                _pluginInitSemaphore.Release();
            }
        }

        private static StepExecutionRequest BuildPluginRequest(DUTMonitorItem dutItem, TestStepConfig configStep)
        {
            var parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Expected"] = configStep.Expected ?? string.Empty,
                ["Description"] = configStep.Description ?? string.Empty,
                ["Type"] = configStep.Type ?? string.Empty,
                ["Channel"] = configStep.Channel ?? string.Empty
            };

            if (configStep.Parameters != null)
            {
                foreach (var pair in configStep.Parameters)
                {
                    parameters[pair.Key] = pair.Value;
                }
            }

            return new StepExecutionRequest
            {
                StepId = configStep.Id ?? string.Empty,
                StepName = configStep.Name ?? string.Empty,
                StepType = configStep.Type ?? "custom",
                Channel = configStep.Channel ?? string.Empty,
                Command = configStep.Command ?? string.Empty,
                TimeoutMs = configStep.Timeout ?? 5000,
                DutId = dutItem.DutId ?? string.Empty,
                SessionId = "ui-config-test",
                StationId = Environment.MachineName,
                Parameters = parameters
            };
        }

        private static bool ShouldFallbackToLegacy(string errorCode)
        {
            return string.Equals(errorCode, PluginErrorCodes.NoMatchingPlugin, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(errorCode, PluginErrorCodes.MultipleMatchingPlugins, StringComparison.OrdinalIgnoreCase);
        }

        private async Task<(bool Passed, string ErrorMessage)> ExecuteTestStepAsync(DUTMonitorItem dutItem, TestStepConfig configStep, UTF.Logging.ILogger logger)
        {
            try
            {
                logger.Debug($"DUT {dutItem.DutId}: 执行命令 '{configStep.Command}' (类型: {configStep.Type})");
                
                // 模拟命令执行（这里可以根据CommandType实现真实的命令执行）
                
                await EnsurePluginsInitializedAsync(logger);
                var pluginRequest = BuildPluginRequest(dutItem, configStep);
                var pluginResult = await _pluginHost.ExecuteAsync(pluginRequest);

                if (!ShouldFallbackToLegacy(pluginResult.ErrorCode))
                {
                    if (!string.IsNullOrWhiteSpace(pluginResult.RawOutput))
                    {
                        AddDUTLog(dutItem.DutId, $"[Plugin:{pluginResult.PluginId}] {pluginResult.RawOutput}");
                    }

                    var pluginPassed = pluginResult.Status == StepExecutionStatus.Passed;
                    var pluginError = pluginPassed ? string.Empty : pluginResult.ErrorMessage;
                    if (!pluginPassed && string.IsNullOrWhiteSpace(pluginError))
                    {
                        pluginError = pluginResult.ErrorCode;
                    }

                    return (pluginPassed, pluginError ?? "Plugin execution failed");
                }

                var commandType = (configStep.Type ?? string.Empty).ToLowerInvariant();
                switch (commandType)
                {
                    case "serial":
                        return await ExecuteSerialCommandAsync(dutItem, configStep, logger);
                    case "network":
                        return await ExecuteNetworkCommandAsync(dutItem, configStep, logger);
                    case "adb":
                        return await ExecuteAdbCommandAsync(dutItem, configStep, logger);
                    case "scpi":
                        return await ExecuteScpiCommandAsync(dutItem, configStep, logger);
                    case "custom":
                        return await ExecuteCustomCommandAsync(dutItem, configStep, logger);
                    default:
                        return await ExecuteDefaultCommandAsync(dutItem, configStep, logger);
                }
            }
            catch (Exception ex)
            {
                logger.Error($"[DUT][Step] {dutItem.DutId}: 执行步骤 {configStep.Name} 时发生异常", ex);
                return (false, ex.Message);
            }
        }

        // 各种命令类型的执行方法
        private async Task<(bool, string)> ExecuteSerialCommandAsync(DUTMonitorItem dutItem, TestStepConfig configStep, UTF.Logging.ILogger logger)
        {
            try
            {
                logger.Info($"[DUT][Serial] {dutItem.DutId}: 准备执行串口命令: {configStep.Command}");
                AddDUTLog(dutItem.DutId, $"[串口] 准备执行命令: {configStep.Command}");
                
                // 获取串口号（从DUT的SerialNumber属性获取，如COM3）
                var portName = dutItem.SerialNumber;
                if (string.IsNullOrEmpty(portName) || !portName.StartsWith("COM"))
                {
                    var errorMsg = $"无效的串口号: {portName}";
                    AddDUTLog(dutItem.DutId, $"[串口] 错误: {errorMsg}");
                    return (false, errorMsg);
                }
                
                // 获取通信配置
                var unifiedConfig = await _configManager.GetUnifiedConfigurationAsync();
                var dutConfig = unifiedConfig.DUTConfiguration;
                var primaryConn = dutConfig.Connections?.Primary;
                var baudRate = primaryConn?.BaudRate ?? 115200;
                var dataBits = primaryConn?.DataBits ?? 8;
                var stopBits = primaryConn?.StopBits ?? 1;

                logger.Info($"[DUT][Serial] {dutItem.DutId}: 尝试打开串口 {portName}, 波特率: {baudRate}");
                AddDUTLog(dutItem.DutId, $"[串口] 打开 {portName}, 波特率: {baudRate}");

                // 尝试打开串口并发送命令
                using (var serialPort = new System.IO.Ports.SerialPort())
                {
                    serialPort.PortName = portName;
                    serialPort.BaudRate = baudRate;
                    serialPort.DataBits = dataBits;
                    serialPort.StopBits = (System.IO.Ports.StopBits)stopBits;
                    serialPort.Parity = System.IO.Ports.Parity.None;
                    serialPort.ReadTimeout = configStep.Timeout ?? 5000;
                    serialPort.WriteTimeout = 1000;
                    
                    // 用于收集接收到的数据
                    var receivedData = new System.Text.StringBuilder();
                    var dataReceived = false;
                    
                    // 优先绑定数据接收事件
                    serialPort.DataReceived += (sender, e) =>
                    {
                        try
                        {
                            if (serialPort.IsOpen && serialPort.BytesToRead > 0)
                            {
                                var data = serialPort.ReadExisting();
                                receivedData.Append(data);
                                dataReceived = true;
                                
                                // 实时显示接收到的数据
                                var displayData = data.Trim();
                                if (!string.IsNullOrEmpty(displayData))
                                {
                                    AddDUTLog(dutItem.DutId, $"[串口] 接收: {displayData}");
                                    logger.Info($"[DUT][Serial] {dutItem.DutId}: 实时接收数据: {displayData}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.Error($"[DUT][Serial] {dutItem.DutId}: 数据接收事件异常: {ex.Message}", ex);
                            AddDUTLog(dutItem.DutId, $"[串口] 接收异常: {ex.Message}");
                        }
                    };
                    
                    try
                    {
                        // 打开串口
                        serialPort.Open();
                        logger.Info($"[DUT][Serial] {dutItem.DutId}: 串口 {portName} 打开成功");
                        AddDUTLog(dutItem.DutId, $"[串口] {portName} 连接成功");
                        
                        // 清空缓冲区
                        serialPort.DiscardInBuffer();
                        serialPort.DiscardOutBuffer();
                        
                        // 发送命令
                        var command = configStep.Command + "\r\n"; // 添加回车换行
                        serialPort.Write(command);
                        logger.Info($"[DUT][Serial] {dutItem.DutId}: 发送命令: {configStep.Command}");
                        AddDUTLog(dutItem.DutId, $"[串口] 发送: {configStep.Command}");
                        
                        // 等待响应
                        var startTime = DateTime.Now;
                        var response = "";
                        
                        // 等待数据接收完成
                        while ((DateTime.Now - startTime).TotalMilliseconds < configStep.Timeout)
                        {
                            if (dataReceived)
                            {
                                // 等待一小段时间确保所有数据都接收完毕
                                await Task.Delay(100);
                                response = receivedData.ToString();
                                
                                // 如果响应包含预期结果，提前退出
                                if (!string.IsNullOrEmpty(response) && IsExpectedResult(response, configStep.Expected ?? ""))
                                {
                                    break;
                                }
                            }
                            await Task.Delay(50); // 短暂等待更多数据
                        }
                        
                        // 最终获取所有接收到的数据
                        response = receivedData.ToString();
                        
                        if (string.IsNullOrEmpty(response))
                        {
                            AddDUTLog(dutItem.DutId, $"[串口] 超时: 未收到任何响应 ({configStep.Timeout}ms)");
                        }
                        else
                        {
                            AddDUTLog(dutItem.DutId, $"[串口] 完整响应: {response.Trim()}");
                        }
                        
                        logger.Info($"[DUT][Serial] {dutItem.DutId}: 收到完整响应: {response.Trim()}");
                        
                        // 验证响应
                        var isValid = IsExpectedResult(response, configStep.Expected ?? "");
                        var errorMessage = isValid ? "" : $"响应不匹配。期望: {configStep.Expected}, 实际: {response.Trim()}";
                        
                        if (isValid)
                        {
                            AddDUTLog(dutItem.DutId, $"[串口] 验证: 通过 ✓");
                        }
                        else
                        {
                            AddDUTLog(dutItem.DutId, $"[串口] 验证: 失败 ✗ - {errorMessage}");
                        }
                        
                        return (isValid, errorMessage);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        var error = $"串口 {portName} 被其他程序占用或无访问权限";
                        logger.Warning($"[DUT][Serial] {dutItem.DutId}: {error}");
                        AddDUTLog(dutItem.DutId, $"[串口] 错误: {error}");
                        return (false, error);
                    }
                    catch (System.IO.IOException ioEx)
                    {
                        var error = $"串口 {portName} IO错误: {ioEx.Message}";
                        logger.Warning($"[DUT][Serial] {dutItem.DutId}: {error}");
                        AddDUTLog(dutItem.DutId, $"[串口] IO错误: {ioEx.Message}");
                        return (false, error);
                    }
                    catch (TimeoutException)
                    {
                        var error = $"串口 {portName} 通信超时 ({configStep.Timeout}ms)";
                        logger.Warning($"[DUT][Serial] {dutItem.DutId}: {error}");
                        AddDUTLog(dutItem.DutId, $"[串口] 超时: {configStep.Timeout}ms");
                        return (false, error);
                    }
                }
            }
            catch (Exception ex)
            {
                var error = $"串口通信异常: {ex.Message}";
                logger.Error($"[DUT][Serial] {dutItem.DutId}: {error}", ex);
                AddDUTLog(dutItem.DutId, $"[串口] 异常: {ex.Message}");
                return (false, error);
            }
        }
        
        /// <summary>
        /// 验证响应是否符合期望结果
        /// </summary>
        private bool IsExpectedResult(string response, string expectedResult)
        {
            if (string.IsNullOrEmpty(expectedResult)) return true;
            if (string.IsNullOrEmpty(response)) return false;
            
            // 去除前后空白字符，统一比较
            response = response.Trim();
            
            if (expectedResult.StartsWith("contains:"))
            {
                var expectedText = expectedResult.Substring(9); // 移除 "contains:" 前缀
                return response.Contains(expectedText, StringComparison.OrdinalIgnoreCase);
            }
            else if (expectedResult.StartsWith("equals:"))
            {
                var expectedText = expectedResult.Substring(7); // 移除 "equals:" 前缀
                return string.Equals(response, expectedText, StringComparison.OrdinalIgnoreCase);
            }
            else if (expectedResult.StartsWith("regex:"))
            {
                var pattern = expectedResult.Substring(6); // 移除 "regex:" 前缀
                return System.Text.RegularExpressions.Regex.IsMatch(response, pattern);
            }
            else
            {
                // 默认使用包含匹配
                return response.Contains(expectedResult, StringComparison.OrdinalIgnoreCase);
            }
        }

        private async Task<(bool, string)> ExecuteNetworkCommandAsync(DUTMonitorItem dutItem, TestStepConfig configStep, UTF.Logging.ILogger logger)
        {
            await Task.Delay(300);
            logger.Info($"[DUT][Network] {dutItem.DutId}: 执行网络命令: {configStep.Command}");
            return (true, "");
        }

        private async Task<(bool, string)> ExecuteAdbCommandAsync(DUTMonitorItem dutItem, TestStepConfig configStep, UTF.Logging.ILogger logger)
        {
            await Task.Delay(800);
            logger.Info($"[DUT][ADB] {dutItem.DutId}: 执行ADB命令: {configStep.Command}");
            return (true, "");
        }

        private async Task<(bool, string)> ExecuteScpiCommandAsync(DUTMonitorItem dutItem, TestStepConfig configStep, UTF.Logging.ILogger logger)
        {
            await Task.Delay(400);
            logger.Info($"[Instrument][SCPI] {dutItem.DutId}: 执行SCPI命令: {configStep.Command}");
            return (true, "");
        }

        private async Task<(bool, string)> ExecuteCustomCommandAsync(DUTMonitorItem dutItem, TestStepConfig configStep, UTF.Logging.ILogger logger)
        {
            try
            {
                logger.Info($"[DUT][Custom] {dutItem.DutId}: 准备执行自定义命令: {configStep.Command}");
                
                // 根据Channel决定执行方式
                switch (configStep.Channel?.ToLower())
                {
                    case "cmd":
                    case "command":
                        return await ExecuteCmdCommandAsync(dutItem, configStep, logger);
                    
                    case "powershell":
                    case "ps":
                        return await ExecutePowerShellCommandAsync(dutItem, configStep, logger);
                    
                    default:
                        // 默认使用命令提示符执行
                        return await ExecuteCmdCommandAsync(dutItem, configStep, logger);
                }
            }
            catch (Exception ex)
            {
                var error = $"执行自定义命令异常: {ex.Message}";
                logger.Error($"[DUT][Custom] {dutItem.DutId}: {error}", ex);
                return (false, error);
            }
        }

        private async Task<(bool, string)> ExecuteDefaultCommandAsync(DUTMonitorItem dutItem, TestStepConfig configStep, UTF.Logging.ILogger logger)
        {
            await Task.Delay(500);
            logger.Info($"[DUT][Default] {dutItem.DutId}: 执行默认命令: {configStep.Command}");
            return (true, "");
        }

        /// <summary>
        /// 执行CMD命令
        /// </summary>
        private async Task<(bool, string)> ExecuteCmdCommandAsync(DUTMonitorItem dutItem, TestStepConfig configStep, UTF.Logging.ILogger logger)
        {
            try
            {
                logger.Info($"[DUT][Cmd] {dutItem.DutId}: 执行CMD命令: {configStep.Command}");
                AddDUTLog(dutItem.DutId, $"[CMD] 准备执行命令: {configStep.Command}");

                var processStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {configStep.Command}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = new System.Diagnostics.Process())
                {
                    // 优先绑定数据接收事件
                    var outputBuilder = new System.Text.StringBuilder();
                    var errorBuilder = new System.Text.StringBuilder();
                    
                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            outputBuilder.AppendLine(e.Data);
                            // 实时显示输出数据
                            AddDUTLog(dutItem.DutId, $"[CMD] 输出: {e.Data.Trim()}");
                            logger.Info($"[DUT][Cmd] {dutItem.DutId}: 实时输出: {e.Data.Trim()}");
                        }
                    };
                    
                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            errorBuilder.AppendLine(e.Data);
                            // 实时显示错误数据
                            AddDUTLog(dutItem.DutId, $"[CMD] 错误: {e.Data.Trim()}");
                            logger.Warning($"[DUT][Cmd] {dutItem.DutId}: 实时错误: {e.Data.Trim()}");
                        }
                    };
                    
                    process.StartInfo = processStartInfo;
                    process.Start();
                    
                    // 开始异步读取
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    
                    AddDUTLog(dutItem.DutId, $"[CMD] 命令启动，等待结果...");

                    var timeout = configStep.Timeout ?? 5000;
                    var completed = await Task.Run(() => process.WaitForExit(timeout));
                    
                    if (!completed)
                    {
                        process.Kill();
                        var timeoutError = $"命令执行超时 ({timeout}ms)";
                        logger.Warning($"[DUT][Cmd] {dutItem.DutId}: {timeoutError}");
                        AddDUTLog(dutItem.DutId, $"[CMD] 超时: {timeout}ms");
                        return (false, timeoutError);
                    }

                    // 等待一小段时间确保所有数据都收集完毕
                    await Task.Delay(100);
                    
                    var output = outputBuilder.ToString();
                    var error = errorBuilder.ToString();
                    
                    // 显示最终完整结果
                    if (!string.IsNullOrEmpty(output))
                    {
                        AddDUTLog(dutItem.DutId, $"[CMD] 完整输出: {output.Trim()}");
                        logger.Info($"[DUT][Cmd] {dutItem.DutId}: 完整输出: {output.Trim()}");
                    }
                    
                    if (!string.IsNullOrEmpty(error))
                    {
                        AddDUTLog(dutItem.DutId, $"[CMD] 完整错误: {error.Trim()}");
                        logger.Warning($"[DUT][Cmd] {dutItem.DutId}: 完整错误: {error.Trim()}");
                    }

                    // 验证结果
                    var fullResponse = output + error;
                    var isValid = IsExpectedResult(fullResponse, configStep.Expected ?? "");
                    var errorMessage = isValid ? "" : $"响应不匹配。期望: {configStep.Expected}, 实际: {fullResponse.Trim()}";

                    if (isValid)
                    {
                        AddDUTLog(dutItem.DutId, $"[CMD] 验证: 通过 ✓");
                    }
                    else
                    {
                        AddDUTLog(dutItem.DutId, $"[CMD] 验证: 失败 ✗ - {errorMessage}");
                    }

                    return (isValid, errorMessage);
                }
            }
            catch (Exception ex)
            {
                var error = $"CMD命令执行异常: {ex.Message}";
                logger.Error($"[DUT][Cmd] {dutItem.DutId}: {error}", ex);
                AddDUTLog(dutItem.DutId, $"[CMD] 异常: {ex.Message}");
                return (false, error);
            }
        }

        /// <summary>
        /// 执行PowerShell命令
        /// </summary>
        private async Task<(bool, string)> ExecutePowerShellCommandAsync(DUTMonitorItem dutItem, TestStepConfig configStep, UTF.Logging.ILogger logger)
        {
            try
            {
                logger.Info($"[DUT][PowerShell] {dutItem.DutId}: 执行PowerShell命令: {configStep.Command}");

                var processStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-Command \"{configStep.Command}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = new System.Diagnostics.Process())
                {
                    process.StartInfo = processStartInfo;
                    process.Start();

                    var outputTask = process.StandardOutput.ReadToEndAsync();
                    var errorTask = process.StandardError.ReadToEndAsync();

                    var timeout = configStep.Timeout ?? 5000;
                    var completed = await Task.Run(() => process.WaitForExit(timeout));
                    
                    if (!completed)
                    {
                        process.Kill();
                        var timeoutError = $"PowerShell命令执行超时 ({timeout}ms)";
                        logger.Warning($"[DUT][PowerShell] {dutItem.DutId}: {timeoutError}");
                        return (false, timeoutError);
                    }

                    var output = await outputTask;
                    var error = await errorTask;
                    
                    logger.Info($"[DUT][PowerShell] {dutItem.DutId}: 命令输出: {output.Trim()}");
                    
                    if (!string.IsNullOrEmpty(error))
                    {
                        logger.Warning($"[DUT][PowerShell] {dutItem.DutId}: 命令错误: {error.Trim()}");
                    }

                    // 验证结果
                    var fullResponse = output + error;
                    var isValid = IsExpectedResult(fullResponse, configStep.Expected ?? "");
                    var errorMessage = isValid ? "" : $"响应不匹配。期望: {configStep.Expected}, 实际: {fullResponse.Trim()}";

                    return (isValid, errorMessage);
                }
            }
            catch (Exception ex)
            {
                var error = $"PowerShell命令执行异常: {ex.Message}";
                logger.Error($"[DUT][PowerShell] {dutItem.DutId}: {error}", ex);
                return (false, error);
            }
        }

        /// <summary>
        /// 停止所有测试
        /// </summary>
        public void StopAllTests()
        {
            foreach (var dutItem in _dutItems)
            {
                if (dutItem.OverallStatus == DUTMonitorStatus.Running)
                {
                    dutItem.OverallStatus = DUTMonitorStatus.Idle;
                    dutItem.CurrentStepText = "测试已停止";
                    dutItem.AddLog("测试已停止");
                }
            }
        }

        /// <summary>
        /// 重置所有DUT状态以准备下一次测试
        /// </summary>
        public void ResetAllDUTStatusForNextTest()
        {
            foreach (var dutItem in _dutItems)
            {
                // 只重置已完成测试的DUT，保留其他状态
                if (dutItem.OverallStatus == DUTMonitorStatus.Passed || 
                    dutItem.OverallStatus == DUTMonitorStatus.Failed || 
                    dutItem.OverallStatus == DUTMonitorStatus.Error ||
                    dutItem.OverallStatus == DUTMonitorStatus.Timeout)
                {
                    dutItem.OverallStatus = DUTMonitorStatus.Idle;
                    dutItem.CurrentStepText = "准备就绪";
                    // 保留测试历史，不清除日志
                    // dutItem.RecentLogs.Clear(); // 注释掉，保留测试历史
                }
                
                // 重置所有测试步骤状态
                foreach (var step in dutItem.TestSteps)
                {
                    step.Status = DUTMonitorStepStatus.Pending;
                    step.StartTime = null;
                    step.EndTime = null;
                    step.ErrorMessage = "";
                }
            }
        }
        
        /// <summary>
        /// 重置所有DUT状态
        /// </summary>
        public void ResetAllDUTs()
        {
            foreach (var dutItem in _dutItems)
            {
                dutItem.OverallStatus = DUTMonitorStatus.Idle;
                dutItem.CurrentStepText = "待机中";
                dutItem.StartTime = null;
                dutItem.EndTime = null;
                dutItem.RecentLogs.Clear();

                foreach (var step in dutItem.TestSteps)
                {
                    step.Status = DUTMonitorStepStatus.Pending;
                    step.StartTime = null;
                    step.EndTime = null;
                    step.ErrorMessage = "";
                }
            }
        }

        /// <summary>
        /// 获取DUT的串口号
        /// </summary>
        private string GetSerialPortForDUT(int dutIndex, List<string>? serialPorts)
        {
            // 如果有串口列表，循环使用
            if (serialPorts != null && serialPorts.Count > 0)
            {
                var index = (dutIndex - 1) % serialPorts.Count;
                return serialPorts[index];
            }
            
            // 默认值
            return $"COM{dutIndex + 2}";
        }

        /// <summary>
        /// 获取DUT的网络主机地址
        /// </summary>
        private string GetNetworkHostForDUT(int dutIndex, List<string>? networkHosts)
        {
            // 如果有网络主机列表，循环使用
            if (networkHosts != null && networkHosts.Count > 0)
            {
                var index = (dutIndex - 1) % networkHosts.Count;
                return networkHosts[index];
            }
            
            return $"192.168.1.{10 + dutIndex - 1}";
        }

        /// <summary>
        /// 添加DUT日志
        /// </summary>
        public void AddDUTLog(string dutId, string logMessage)
        {
            var dutItem = _dutItems.FirstOrDefault(d => d.DutId == dutId);
            if (dutItem != null)
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                var logEntry = $"[{timestamp}] {logMessage}";
                
                // 添加到日志集合
                if (dutItem.Logs == null)
                    dutItem.Logs = new ObservableCollection<string>();
                    
                dutItem.Logs.Add(logEntry);
                
                // 保持最近200条日志
                while (dutItem.Logs.Count > 200)
                {
                    dutItem.Logs.RemoveAt(0);
                }
                
                // 更新最新日志显示
                dutItem.LatestLog = logEntry;
                
                // 更新RecentLogs显示（UI绑定的是这个）
                Application.Current.Dispatcher.Invoke(() =>
                {
                    dutItem.AddLog(logMessage, UTF.Logging.LogLevel.Info); // 这个方法会更新RecentLogs
                });
            }
        }

        // IDUTMonitorService 接口实现
        Task IDUTMonitorService.InitializeAsync(int dutCount) => InitializeAsync(_dataGrid!);

        Task IDUTMonitorService.StartAllTestsAsync(CancellationToken ct) => StartAllTestsAsync();

        Task IDUTMonitorService.StopAllTestsAsync()
        {
            StopAllTests();
            return Task.CompletedTask;
        }

        IReadOnlyList<object> IDUTMonitorService.GetLoadedPlugins() => LoadedPlugins.Cast<object>().ToList();
    }
}
