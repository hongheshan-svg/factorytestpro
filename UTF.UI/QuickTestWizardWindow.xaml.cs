using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using UTF.Plugin.Abstractions;
using UTF.Plugin.Host;
using UTF.UI.Models;
using UTF.UI.Services;
using UTF.UI.ViewModels;

namespace UTF.UI
{
    public partial class QuickTestWizardWindow : Window
    {
        private readonly StepExecutorPluginHost _pluginHost;
        private readonly QuickTestWizardViewModel _viewModel;
        private int _currentStep = 1;

        /// <summary>
        /// 配置已创建事件，WindowFactory / MainWindow 可监听以刷新 UI。
        /// </summary>
        public event EventHandler? ConfigurationCreated;

        public QuickTestWizardWindow(
            StepExecutorPluginHost pluginHost,
            QuickTestWizardViewModel viewModel)
        {
            _pluginHost = pluginHost;
            _viewModel = viewModel;
            InitializeComponent();
            DataContext = _viewModel;
            Loaded += OnWindowLoaded;
        }

        // ────────────────── Window Load ──────────────────

        private async void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            // P1-8: wrap async-void body so plugin init failures surface to the user instead of crashing the app.
            try
            {
                await _pluginHost.InitializeAsync();
                BuildStepCategories();
                PopulatePluginInfo();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化插件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ────────────────── Plugin Discovery ──────────────────

        /// <summary>
        /// 从已加载的插件构建用户友好的测试类别列表，填充到 VM 的
        /// <see cref="QuickTestWizardViewModel.AvailableStepCategories"/>。
        /// </summary>
        private void BuildStepCategories()
        {
            _viewModel.AvailableStepCategories.Clear();

            // 仅从插件中发现能力，避免 UI 维护硬编码类型表。
            var loadedPlugins = _pluginHost.LoadedPlugins;
            foreach (var plugin in loadedPlugins)
            {
                foreach (var stepType in plugin.SupportedStepTypes)
                {
                    foreach (var channel in plugin.SupportedChannels)
                    {
                        if (!_viewModel.AvailableStepCategories.Any(c => c.StepType == stepType && c.Channel == channel))
                        {
                            _viewModel.AvailableStepCategories.Add(new WizardStepCategory
                            {
                                Label = BuildCategoryLabel(plugin.Name, stepType, channel),
                                StepType = stepType,
                                Channel = channel,
                                CommandHint = BuildCommandHint(plugin.Name, stepType, channel),
                                PluginId = plugin.PluginId,
                                PluginName = plugin.Name
                            });
                        }
                    }
                }
            }

            if (_viewModel.AvailableStepCategories.Count > 0)
            {
                _viewModel.SelectedStepCategory = _viewModel.AvailableStepCategories[0];
            }
            else
            {
                _viewModel.StepCommandHint = "💡 未发现可用插件能力。请先安装并加载步骤执行插件。";
            }
        }

        private static string BuildCategoryLabel(string pluginName, string stepType, string channel)
            => $"🔧 {pluginName} · {stepType}/{channel}";

        private static string BuildCommandHint(string pluginName, string stepType, string channel)
            => $"插件 {pluginName} 将处理 {stepType}/{channel}，请输入该能力对应的命令或请求内容。";

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
            if (string.IsNullOrWhiteSpace(_viewModel.ProductName))
                errors.Add("请输入产品名称");
            if (string.IsNullOrWhiteSpace(_viewModel.ProductModel))
                errors.Add("请输入产品型号");

            if (errors.Count > 0)
            {
                MessageBox.Show(string.Join("\n", errors), "请完善产品信息", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            return true;
        }

        // ────────────────── Step 2 Validation ──────────────────

        private bool ValidateStep2()
        {
            if (_viewModel.Steps.Count == 0)
            {
                MessageBox.Show("请至少添加一个测试步骤", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            return true;
        }

        // ────────────────── Step 3: Review ──────────────────

        private void PopulateReview()
        {
            var productName = _viewModel.ProductName?.Trim() ?? string.Empty;
            var productModel = _viewModel.ProductModel?.Trim() ?? string.Empty;

            // 验证
            var validation = ValidateFullConfig();
            WizValidationPanel.Visibility = Visibility.Visible;
            if (validation.Count == 0)
            {
                WizValidationTitle.Text = "✅ 配置验证通过";
                WizValidationTitle.Foreground = new SolidColorBrush(Color.FromRgb(40, 167, 69));
                WizValidationDetail.Text = $"产品: {productName} | 型号: {productModel} | {_viewModel.Steps.Count} 个测试步骤";
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
            if (string.IsNullOrWhiteSpace(_viewModel.ProductName))
                errors.Add("产品名称未填写");
            if (_viewModel.Steps.Count == 0)
                errors.Add("没有测试步骤");

            // 检查无插件支持的步骤
            var loadedPlugins = _pluginHost.LoadedPlugins;
            foreach (var step in _viewModel.Steps)
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

                var input = _viewModel.BuildInput();
                if (!_viewModel.ValidateInput(input, out var inputErrors))
                {
                    MessageBox.Show(string.Join("\n", inputErrors), "请完善配置", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string? exportPath = null;
                if (_viewModel.ExportCopy)
                {
                    var dialog = new SaveFileDialog
                    {
                        Title = "导出测试配置",
                        Filter = "JSON 配置文件 (*.json)|*.json",
                        FileName = $"{_viewModel.ProductName?.Trim()}-test-config.json",
                        DefaultExt = "json"
                    };
                    if (dialog.ShowDialog() == true)
                    {
                        exportPath = dialog.FileName;
                    }
                }

                // 委托给 VM（VM 内部 Build + 可选保存 + 触发 ConfigurationCreated）。
                await _viewModel.SaveAsync(
                    saveAsDefault: _viewModel.SaveAsDefault,
                    exportPath: exportPath);

                // 将 VM 触发的 ConfigurationCreated 事件转发到窗口事件（向后兼容订阅）。
                ConfigurationCreated?.Invoke(this, EventArgs.Empty);

                MessageBox.Show(
                    $"测试配置已成功创建！\n\n产品: {_viewModel.ProductName?.Trim()}\n测试步骤: {_viewModel.Steps.Count} 步\n工位数量: {_viewModel.DutCount}\n\n返回主界面后点击【开始测试】即可运行。",
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
            if (_viewModel.Steps.Count > 0)
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
