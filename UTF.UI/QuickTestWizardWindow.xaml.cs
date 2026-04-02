using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using UTF.Plugin.Abstractions;
using UTF.Plugin.Host;
using UTF.UI.Services;

namespace UTF.UI
{
    public partial class QuickTestWizardWindow : Window
    {
        private readonly ConfigurationManager _configManager;
        private readonly StepExecutorPluginHost _pluginHost;
        private readonly ObservableCollection<WizardStepItem> _wizardSteps = new();
        private readonly List<StepCategoryInfo> _stepCategories = new();
        private int _currentStep = 1;
        private int _nextStepId = 1;

        /// <summary>
        /// 配置已创建事件，MainWindow 可监听以刷新 UI
        /// </summary>
        public event EventHandler? ConfigurationCreated;

        public QuickTestWizardWindow(
            ConfigurationManager configManager,
            StepExecutorPluginHost pluginHost)
        {
            _configManager = configManager;
            _pluginHost = pluginHost;
            InitializeComponent();
            Loaded += OnWindowLoaded;
        }

        // ────────────────── Window Load ──────────────────

        private async void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            await _pluginHost.InitializeAsync();
            BuildStepCategories();
            PopulatePluginInfo();
            PopulateCategoryCombo();
        }

        // ────────────────── Plugin Discovery ──────────────────

        /// <summary>
        /// 从已加载的插件构建用户友好的测试类别列表
        /// </summary>
        private void BuildStepCategories()
        {
            _stepCategories.Clear();

            // 仅从插件中发现能力，避免 UI 维护硬编码类型表。
            var loadedPlugins = _pluginHost.LoadedPlugins;
            foreach (var plugin in loadedPlugins)
            {
                foreach (var stepType in plugin.SupportedStepTypes)
                {
                    foreach (var channel in plugin.SupportedChannels)
                    {
                        var label = BuildCategoryLabel(plugin.Name, stepType, channel);
                        var hint = BuildCommandHint(plugin.Name, stepType, channel);

                        if (!_stepCategories.Any(c => c.StepType == stepType && c.Channel == channel))
                        {
                            _stepCategories.Add(new StepCategoryInfo
                            {
                                Label = label,
                                StepType = stepType,
                                Channel = channel,
                                CommandHint = hint,
                                PluginId = plugin.PluginId,
                                PluginName = plugin.Name
                            });
                        }
                    }
                }
            }
        }

        private static string BuildCategoryLabel(string pluginName, string stepType, string channel)
        {
            return $"🔧 {pluginName} · {stepType}/{channel}";
        }

        private static string BuildCommandHint(string pluginName, string stepType, string channel)
        {
            return $"插件 {pluginName} 将处理 {stepType}/{channel}，请输入该能力对应的命令或请求内容。";
        }

        private void PopulatePluginInfo()
        {
            var loadedPlugins = _pluginHost.LoadedPlugins;
            if (loadedPlugins.Count > 0)
            {
                var pluginInfos = loadedPlugins.Select(p => new PluginDisplayInfo
                {
                    Name = p.Name,
                    Version = $"v{p.Version}",
                    Description = $"支持: {string.Join(", ", p.SupportedStepTypes)} | 通道: {string.Join(", ", p.SupportedChannels)}"
                }).ToList();

                WizPluginList.ItemsSource = pluginInfos;
                WizPluginSummary.Text = $"已检测到 {loadedPlugins.Count} 个插件，支持以下测试能力：";
                WizNoPluginHint.Visibility = Visibility.Collapsed;
            }
            else
            {
                WizPluginSummary.Text = "未检测到已安装插件。系统提供内置的基础测试类型，安装插件可扩展更多能力。";
                WizNoPluginHint.Visibility = Visibility.Visible;
            }
        }

        private void PopulateCategoryCombo()
        {
            WizStepCategory.Items.Clear();
            foreach (var category in _stepCategories)
            {
                WizStepCategory.Items.Add(new ComboBoxItem
                {
                    Content = category.Label,
                    Tag = category
                });
            }
            if (WizStepCategory.Items.Count > 0)
            {
                WizStepCategory.SelectedIndex = 0;
            }
            else
            {
                WizCommandHint.Text = "💡 未发现可用插件能力。请先安装并加载步骤执行插件。";
            }
        }

        // ────────────────── Step Navigation ──────────────────

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep == 1)
            {
                if (!ValidateStep1()) return;
                GoToStep(2);
            }
            else if (_currentStep == 2)
            {
                if (!ValidateStep2()) return;
                PopulateReview();
                GoToStep(3);
            }
        }

        private void BtnPrevious_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep > 1)
                GoToStep(_currentStep - 1);
        }

        private void GoToStep(int step)
        {
            _currentStep = step;

            Step1Panel.Visibility = step == 1 ? Visibility.Visible : Visibility.Collapsed;
            Step2Panel.Visibility = step == 2 ? Visibility.Visible : Visibility.Collapsed;
            Step3Panel.Visibility = step == 3 ? Visibility.Visible : Visibility.Collapsed;

            BtnPrevious.Visibility = step > 1 ? Visibility.Visible : Visibility.Collapsed;
            BtnNext.Visibility = step < 3 ? Visibility.Visible : Visibility.Collapsed;
            BtnSave.Visibility = step == 3 ? Visibility.Visible : Visibility.Collapsed;

            UpdateStepIndicators(step);
        }

        private void UpdateStepIndicators(int step)
        {
            // Step 1
            Step1Circle.Background = step >= 1 ? new SolidColorBrush(Color.FromRgb(0, 122, 204)) : new SolidColorBrush(Color.FromRgb(228, 230, 235));
            Step1Label.Foreground = step >= 1 ? new SolidColorBrush(Color.FromRgb(0, 122, 204)) : new SolidColorBrush(Color.FromRgb(136, 136, 136));
            Step1Label.FontWeight = step == 1 ? FontWeights.SemiBold : FontWeights.Normal;

            // Step 2
            Step2Circle.Background = step >= 2 ? new SolidColorBrush(Color.FromRgb(0, 122, 204)) : new SolidColorBrush(Color.FromRgb(228, 230, 235));
            Step2Number.Foreground = step >= 2 ? Brushes.White : new SolidColorBrush(Color.FromRgb(136, 136, 136));
            Step2Label.Foreground = step >= 2 ? new SolidColorBrush(Color.FromRgb(0, 122, 204)) : new SolidColorBrush(Color.FromRgb(136, 136, 136));
            Step2Label.FontWeight = step == 2 ? FontWeights.SemiBold : FontWeights.Normal;

            // Step 3
            Step3Circle.Background = step >= 3 ? new SolidColorBrush(Color.FromRgb(0, 122, 204)) : new SolidColorBrush(Color.FromRgb(228, 230, 235));
            Step3Number.Foreground = step >= 3 ? Brushes.White : new SolidColorBrush(Color.FromRgb(136, 136, 136));
            Step3Label.Foreground = step >= 3 ? new SolidColorBrush(Color.FromRgb(0, 122, 204)) : new SolidColorBrush(Color.FromRgb(136, 136, 136));
            Step3Label.FontWeight = step == 3 ? FontWeights.SemiBold : FontWeights.Normal;

            // Lines
            Line1.Background = step >= 2 ? new SolidColorBrush(Color.FromRgb(0, 122, 204)) : new SolidColorBrush(Color.FromRgb(224, 224, 224));
            Line2.Background = step >= 3 ? new SolidColorBrush(Color.FromRgb(0, 122, 204)) : new SolidColorBrush(Color.FromRgb(224, 224, 224));
        }

        // ────────────────── Step 1 Validation ──────────────────

        private bool ValidateStep1()
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(WizProductName.Text))
                errors.Add("请输入产品名称");
            if (string.IsNullOrWhiteSpace(WizProductModel.Text))
                errors.Add("请输入产品型号");

            if (errors.Count > 0)
            {
                MessageBox.Show(string.Join("\n", errors), "请完善产品信息", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            return true;
        }

        // ────────────────── Step 2: Test Steps ──────────────────

        private void OnStepCategoryChanged(object sender, SelectionChangedEventArgs e)
        {
            if (WizStepCategory.SelectedItem is ComboBoxItem item && item.Tag is StepCategoryInfo category)
            {
                WizCommandHint.Text = "💡 " + category.CommandHint;
            }
        }

        private void AddWizardStep_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(WizStepName.Text))
            {
                MessageBox.Show("请输入步骤名称", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (WizStepCategory.SelectedItem is not ComboBoxItem categoryItem ||
                categoryItem.Tag is not StepCategoryInfo category)
            {
                MessageBox.Show("请选择测试类型", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 构建 Expected 表达式
            string? expected = BuildExpectedExpression();

            var timeout = 5000;
            if (WizStepTimeout.SelectedItem is ComboBoxItem timeoutItem && timeoutItem.Tag is string timeoutStr)
                int.TryParse(timeoutStr, out timeout);

            var stepItem = new WizardStepItem
            {
                Id = $"step_{_nextStepId:D3}",
                Order = _wizardSteps.Count + 1,
                Name = WizStepName.Text.Trim(),
                StepType = category.StepType,
                Channel = category.Channel,
                CategoryLabel = category.Label,
                Command = WizStepCommand.Text.Trim(),
                Expected = expected,
                Timeout = timeout
            };

            _wizardSteps.Add(stepItem);
            _nextStepId++;
            RefreshStepList();

            // 清空输入
            WizStepName.Text = "";
            WizStepCommand.Text = "";
            WizExpectedValue.Text = "";
            WizExpectedMode.SelectedIndex = 0;
            WizStepTimeout.SelectedIndex = 0;
        }

        private string? BuildExpectedExpression()
        {
            if (WizExpectedMode.SelectedItem is not ComboBoxItem modeItem)
                return null;

            var mode = modeItem.Tag?.ToString() ?? "none";
            var value = WizExpectedValue.Text.Trim();

            if (mode == "none" || string.IsNullOrEmpty(value))
                return null;

            return mode switch
            {
                "contains" => $"contains:{value}",
                "equals" => $"equals:{value}",
                "regex" => $"regex:{value}",
                _ => null
            };
        }

        private void RemoveWizStep_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string stepId)
            {
                var item = _wizardSteps.FirstOrDefault(s => s.Id == stepId);
                if (item != null)
                {
                    _wizardSteps.Remove(item);
                    RenumberSteps();
                    RefreshStepList();
                }
            }
        }

        private void MoveWizStepUp_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string stepId)
            {
                int idx = -1;
                for (int i = 0; i < _wizardSteps.Count; i++)
                {
                    if (_wizardSteps[i].Id == stepId) { idx = i; break; }
                }
                if (idx > 0)
                {
                    _wizardSteps.Move(idx, idx - 1);
                    RenumberSteps();
                    RefreshStepList();
                }
            }
        }

        private void MoveWizStepDown_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string stepId)
            {
                int idx = -1;
                for (int i = 0; i < _wizardSteps.Count; i++)
                {
                    if (_wizardSteps[i].Id == stepId) { idx = i; break; }
                }
                if (idx >= 0 && idx < _wizardSteps.Count - 1)
                {
                    _wizardSteps.Move(idx, idx + 1);
                    RenumberSteps();
                    RefreshStepList();
                }
            }
        }

        private void RenumberSteps()
        {
            for (int i = 0; i < _wizardSteps.Count; i++)
                _wizardSteps[i].Order = i + 1;
        }

        private void RefreshStepList()
        {
            WizStepList.ItemsSource = null;
            WizStepList.ItemsSource = _wizardSteps;
            WizStepCountLabel.Text = $"共 {_wizardSteps.Count} 步";
            WizNoStepHint.Visibility = _wizardSteps.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private bool ValidateStep2()
        {
            if (_wizardSteps.Count == 0)
            {
                MessageBox.Show("请至少添加一个测试步骤", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            return true;
        }

        // ────────────────── Step 3: Review ──────────────────

        private void PopulateReview()
        {
            var icon = (WizProductIcon.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "📱";
            var category = WizProductCategory.Text;
            if (string.IsNullOrWhiteSpace(category) && WizProductCategory.SelectedItem is ComboBoxItem catItem)
                category = catItem.Content?.ToString() ?? "";

            WizReviewIcon.Text = icon;
            WizReviewProductName.Text = WizProductName.Text.Trim();
            WizReviewProductModel.Text = WizProductModel.Text.Trim();
            WizReviewCategory.Text = string.IsNullOrWhiteSpace(category) ? "未分类" : category;
            WizReviewDUTCount.Text = $"{(int)WizDUTCount.Value} 个工位";

            WizReviewStepList.ItemsSource = _wizardSteps;

            // 验证
            var validation = ValidateFullConfig();
            WizValidationPanel.Visibility = Visibility.Visible;
            if (validation.Count == 0)
            {
                WizValidationTitle.Text = "✅ 配置验证通过";
                WizValidationTitle.Foreground = new SolidColorBrush(Color.FromRgb(40, 167, 69));
                WizValidationDetail.Text = $"产品: {WizProductName.Text.Trim()} | 型号: {WizProductModel.Text.Trim()} | {_wizardSteps.Count} 个测试步骤";
            }
            else
            {
                WizValidationTitle.Text = "⚠️ 发现以下问题";
                WizValidationTitle.Foreground = new SolidColorBrush(Color.FromRgb(240, 173, 78));
                WizValidationDetail.Text = string.Join("\n", validation);
            }
        }

        private List<string> ValidateFullConfig()
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(WizProductName.Text))
                errors.Add("产品名称未填写");
            if (_wizardSteps.Count == 0)
                errors.Add("没有测试步骤");

            // 检查无插件支持的步骤
            var loadedPlugins = _pluginHost.LoadedPlugins;
            foreach (var step in _wizardSteps)
            {
                bool hasPlugin = loadedPlugins.Any(p =>
                    p.SupportedStepTypes.Contains(step.StepType, StringComparer.OrdinalIgnoreCase) &&
                    p.SupportedChannels.Any(c => c.Equals(step.Channel, StringComparison.OrdinalIgnoreCase)));

                if (!hasPlugin)
                {
                    errors.Add($"步骤 \"{step.Name}\" 的类型 ({step.StepType}/{step.Channel}) 暂无插件支持，运行时将失败。请安装匹配插件或改用受支持的能力。");
                }
            }

            return errors;
        }

        // ────────────────── Save ──────────────────

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                BtnSave.IsEnabled = false;
                var config = BuildUnifiedConfiguration();

                if (WizSaveAsDefault.IsChecked == true)
                {
                    await _configManager.SaveUnifiedConfigurationAsync(config);
                    await _configManager.RefreshConfiguration();
                }

                if (WizExportCopy.IsChecked == true)
                {
                    var dialog = new SaveFileDialog
                    {
                        Title = "导出测试配置",
                        Filter = "JSON 配置文件 (*.json)|*.json",
                        FileName = $"{WizProductName.Text.Trim()}-test-config.json",
                        DefaultExt = "json"
                    };
                    if (dialog.ShowDialog() == true)
                    {
                        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
                        {
                            WriteIndented = true,
                            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                        });
                        await File.WriteAllTextAsync(dialog.FileName, json);
                    }
                }

                ConfigurationCreated?.Invoke(this, EventArgs.Empty);

                MessageBox.Show(
                    $"测试配置已成功创建！\n\n产品: {WizProductName.Text.Trim()}\n测试步骤: {_wizardSteps.Count} 步\n工位数量: {(int)WizDUTCount.Value}\n\n返回主界面后点击【开始测试】即可运行。",
                    "创建成功",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnSave.IsEnabled = true;
            }
        }

        private UnifiedConfiguration BuildUnifiedConfiguration()
        {
            var productName = WizProductName.Text.Trim();
            var productModel = WizProductModel.Text.Trim();
            var icon = (WizProductIcon.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "📱";
            var category = WizProductCategory.Text;
            if (string.IsNullOrWhiteSpace(category) && WizProductCategory.SelectedItem is ComboBoxItem catItem)
                category = catItem.Content?.ToString() ?? "";

            var dutCount = (int)WizDUTCount.Value;

            // 通信端点
            var serialPorts = new List<string>();
            var networkHosts = new List<string>();
            if (WizUseSerial.IsChecked == true)
            {
                for (int i = 0; i < dutCount; i++)
                    serialPorts.Add($"COM{3 + i}");
            }
            if (WizUseNetwork.IsChecked == true)
            {
                for (int i = 0; i < dutCount; i++)
                    networkHosts.Add($"192.168.1.{10 + i}");
            }

            // 构建步骤配置
            var steps = _wizardSteps.Select(s => new TestStepConfig
            {
                Id = s.Id,
                Name = s.Name,
                Order = s.Order,
                Enabled = true,
                Type = s.StepType,
                Channel = s.Channel,
                Target = "dut",
                Command = s.Command,
                Expected = s.Expected,
                Timeout = s.Timeout,
                Delay = 500
            }).ToList();

            return new UnifiedConfiguration
            {
                ConfigurationInfo = new ConfigurationInfo
                {
                    Name = $"{productName}测试配置",
                    Version = "1.0.0",
                    CreatedDate = DateTime.UtcNow.ToString("O"),
                    Description = $"由快速向导创建的 {productName} 测试配置",
                    Author = "UTF Quick Wizard"
                },
                SystemSettings = new SystemSettings
                {
                    LogLevel = "Info",
                    AutoSaveResults = true,
                    ResultsPath = "./test-results",
                    DefaultLanguage = "zh-CN",
                    Theme = "Light"
                },
                DUTConfiguration = new DUTConfiguration
                {
                    ProductInfo = new ProductInfo
                    {
                        Name = productName,
                        Model = productModel,
                        Icon = icon,
                        Category = category
                    },
                    GlobalSettings = new GlobalSettings
                    {
                        DefaultMaxConcurrent = dutCount,
                        TestTimeout = 300,
                        RetryCount = 2,
                        RetryDelay = 2000
                    },
                    CommunicationEndpoints = new CommunicationEndpoints
                    {
                        SerialPorts = serialPorts,
                        NetworkHosts = networkHosts
                    },
                    NamingConfig = new NamingConfig
                    {
                        Template = $"{{TypeName}}测试工位{{Index}}",
                        IdTemplate = "DUT-{Index}"
                    },
                    Connections = new DUTConnections
                    {
                        Primary = WizUseSerial.IsChecked == true
                            ? new ConnectionConfig { Type = "Serial", BaudRate = 115200, DataBits = 8, StopBits = 1, Parity = "None" }
                            : WizUseNetwork.IsChecked == true
                                ? new ConnectionConfig { Type = "Network", TelnetPort = 23 }
                                : null
                    }
                },
                TestProjectConfiguration = new TestProjectConfiguration
                {
                    TestMode = new TestMode
                    {
                        Id = "production",
                        Name = "生产测试",
                        Description = $"{productName}生产测试流程",
                        DefaultTimeout = 300000,
                        EnableParallel = true,
                        MaxRetries = 2
                    },
                    TestProject = new TestProject
                    {
                        Id = $"{productName.ToLowerInvariant().Replace(" ", "_")}_test",
                        Name = $"{productName}生产测试",
                        Enabled = true,
                        Steps = steps
                    }
                }
            };
        }

        // ────────────────── UI Event Handlers ──────────────────

        private void OnProductInfoChanged(object sender, TextChangedEventArgs e)
        {
            // 可选：实时预览
        }

        private void OnDUTCountChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (WizDUTCountLabel != null)
                WizDUTCountLabel.Text = ((int)e.NewValue).ToString();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            if (_wizardSteps.Count > 0)
            {
                var result = MessageBox.Show("已添加的测试步骤将丢失，确定退出吗？", "确认退出",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes) return;
            }
            DialogResult = false;
            Close();
        }

        // ────────────────── Inner Models ──────────────────

        /// <summary>
        /// 向导中的步骤项
        /// </summary>
        public class WizardStepItem : INotifyPropertyChanged
        {
            public string Id { get; set; } = "";
            private int _order;
            public int Order
            {
                get => _order;
                set { _order = value; OnPropertyChanged(); }
            }
            public string Name { get; set; } = "";
            public string StepType { get; set; } = "";
            public string Channel { get; set; } = "";
            public string CategoryLabel { get; set; } = "";
            public string? Command { get; set; }
            public string? Expected { get; set; }
            public int Timeout { get; set; } = 5000;

            public string CommandPreview =>
                string.IsNullOrWhiteSpace(Command)
                    ? "(无命令)"
                    : Command.Length > 40 ? Command[..37] + "..." : Command;

            public event PropertyChangedEventHandler? PropertyChanged;
            protected void OnPropertyChanged([CallerMemberName] string? name = null)
                => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        /// <summary>
        /// 测试类别信息（映射插件能力到用户友好的类别）
        /// </summary>
        private sealed class StepCategoryInfo
        {
            public string Label { get; set; } = "";
            public string StepType { get; set; } = "";
            public string Channel { get; set; } = "";
            public string CommandHint { get; set; } = "";
            public string PluginId { get; set; } = "";
            public string PluginName { get; set; } = "";
        }

        /// <summary>
        /// 插件显示信息
        /// </summary>
        private sealed class PluginDisplayInfo
        {
            public string Name { get; set; } = "";
            public string Version { get; set; } = "";
            public string Description { get; set; } = "";
        }
    }
}
