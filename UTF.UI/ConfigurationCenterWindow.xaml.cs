using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using UTF.UI.Models;
using UTF.UI.Services;

namespace UTF.UI
{
    public partial class ConfigurationCenterWindow : Window
    {
        private readonly ConfigurationManager _configManager;
        private UnifiedConfiguration _config = new();

        public ConfigurationCenterWindow(ConfigurationManager configManager)
        {
            InitializeComponent();
            _configManager = configManager;
            Loaded += Window_Loaded;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _config = await _configManager.GetUnifiedConfigurationAsync();
                PopulateAllFields();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载配置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PopulateAllFields()
        {
            // 系统设置
            var sys = _config.SystemSettings;
            SetComboByContent(LogLevelCombo, sys.LogLevel);
            ResultsPathText.Text = sys.ResultsPath;
            AutoSaveCheck.IsChecked = sys.AutoSaveResults;
            SetComboByContent(LanguageCombo, sys.DefaultLanguage, partialMatch: true);
            SetComboByContent(ThemeCombo, sys.Theme);

            // DUT配置 - 产品信息
            var product = _config.DUTConfiguration?.ProductInfo;
            if (product != null)
            {
                ProductNameText.Text = product.Name;
                ProductModelText.Text = product.Model;
                ProductCategoryText.Text = product.Category;
            }

            // DUT配置 - 全局参数
            var global = _config.DUTConfiguration?.GlobalSettings;
            if (global != null)
            {
                MaxConcurrentText.Text = global.DefaultMaxConcurrent?.ToString() ?? "16";
                TestTimeoutText.Text = global.TestTimeout?.ToString() ?? "300";
                RetryCountText.Text = global.RetryCount?.ToString() ?? "3";
            }

            // DUT配置 - 串口列表
            var endpoints = _config.DUTConfiguration?.CommunicationEndpoints;
            SerialPortsList.Items.Clear();
            if (endpoints?.SerialPorts != null)
                foreach (var port in endpoints.SerialPorts)
                    SerialPortsList.Items.Add(port);

            // DUT配置 - 网络主机列表
            NetworkHostsList.Items.Clear();
            if (endpoints?.NetworkHosts != null)
                foreach (var host in endpoints.NetworkHosts)
                    NetworkHostsList.Items.Add(host);

            // DUT配置 - 命名模板
            var naming = _config.DUTConfiguration?.NamingConfig;
            NamingTemplateText.Text = naming?.Template ?? "{TypeName}测试工位{Index}";
            NamingIdTemplateText.Text = naming?.IdTemplate ?? "DUT-{Index}";
            UpdateNamingPreview();

            // 测试模式信息
            var testMode = _config.TestProjectConfiguration?.TestMode;
            if (testMode != null)
            {
                TestModeNameText.Text = testMode.Name;
                MaxRetriesText.Text = testMode.MaxRetries?.ToString() ?? "0";
                EnableParallelCheck.IsChecked = testMode.EnableParallel ?? false;
            }

            // 测试步骤
            if (_config.TestProjectConfiguration == null)
                _config.TestProjectConfiguration = new TestProjectConfiguration();
            if (_config.TestProjectConfiguration.TestProject == null)
                _config.TestProjectConfiguration.TestProject = new TestProject();

            var steps = _config.TestProjectConfiguration.TestProject.Steps;
            if (steps != null)
                TestStepsGrid.ItemsSource = new System.Collections.ObjectModel.ObservableCollection<TestStepConfig>(steps);

            StepDetailPanel.Visibility = Visibility.Collapsed;
        }

        private void CollectAllFields()
        {
            // 系统设置
            _config.SystemSettings.LogLevel = (LogLevelCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Info";
            _config.SystemSettings.ResultsPath = ResultsPathText.Text;
            _config.SystemSettings.AutoSaveResults = AutoSaveCheck.IsChecked == true;

            var langItem = (LanguageCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            if (langItem.Contains("zh-CN")) _config.SystemSettings.DefaultLanguage = "zh-CN";
            else if (langItem.Contains("en-US")) _config.SystemSettings.DefaultLanguage = "en-US";
            else if (langItem.Contains("ja-JP")) _config.SystemSettings.DefaultLanguage = "ja-JP";

            _config.SystemSettings.Theme = (ThemeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Light";

            // DUT配置 - 产品信息
            if (_config.DUTConfiguration == null) _config.DUTConfiguration = new DUTConfiguration();
            if (_config.DUTConfiguration.ProductInfo == null) _config.DUTConfiguration.ProductInfo = new ProductInfo();
            _config.DUTConfiguration.ProductInfo.Name = ProductNameText.Text;
            _config.DUTConfiguration.ProductInfo.Model = ProductModelText.Text;
            _config.DUTConfiguration.ProductInfo.Category = ProductCategoryText.Text;

            // DUT配置 - 全局参数
            if (_config.DUTConfiguration.GlobalSettings == null) _config.DUTConfiguration.GlobalSettings = new GlobalSettings();
            if (int.TryParse(MaxConcurrentText.Text, out int maxC)) _config.DUTConfiguration.GlobalSettings.DefaultMaxConcurrent = maxC;
            if (int.TryParse(TestTimeoutText.Text, out int timeout)) _config.DUTConfiguration.GlobalSettings.TestTimeout = timeout;
            if (int.TryParse(RetryCountText.Text, out int retry)) _config.DUTConfiguration.GlobalSettings.RetryCount = retry;

            // DUT配置 - 串口列表
            if (_config.DUTConfiguration.CommunicationEndpoints == null)
                _config.DUTConfiguration.CommunicationEndpoints = new CommunicationEndpoints();
            _config.DUTConfiguration.CommunicationEndpoints.SerialPorts =
                SerialPortsList.Items.Cast<string>().ToList();

            // DUT配置 - 网络主机列表
            _config.DUTConfiguration.CommunicationEndpoints.NetworkHosts =
                NetworkHostsList.Items.Cast<string>().ToList();

            // DUT配置 - 命名模板
            if (_config.DUTConfiguration.NamingConfig == null) _config.DUTConfiguration.NamingConfig = new NamingConfig();
            _config.DUTConfiguration.NamingConfig.Template = NamingTemplateText.Text;
            _config.DUTConfiguration.NamingConfig.IdTemplate = NamingIdTemplateText.Text;

            // 测试步骤
            if (_config.TestProjectConfiguration == null)
                _config.TestProjectConfiguration = new TestProjectConfiguration();
            if (_config.TestProjectConfiguration.TestProject == null)
                _config.TestProjectConfiguration.TestProject = new TestProject();

            if (TestStepsGrid.ItemsSource is System.Collections.ObjectModel.ObservableCollection<TestStepConfig> steps)
                _config.TestProjectConfiguration.TestProject.Steps = steps.ToList();
        }

        // ── 串口管理 ────────────────────────────────────────────────────────────

        private void AddSerialPort_Click(object sender, RoutedEventArgs e)
        {
            var port = NewSerialPortText.Text.Trim().ToUpper();
            if (string.IsNullOrEmpty(port) || !port.StartsWith("COM"))
            {
                MessageBox.Show("请输入有效的串口名（如 COM3）", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (SerialPortsList.Items.Contains(port))
            {
                MessageBox.Show($"{port} 已存在", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            SerialPortsList.Items.Add(port);
            NewSerialPortText.Text = "COM";
        }

        private void DeleteSerialPort_Click(object sender, RoutedEventArgs e)
        {
            if (SerialPortsList.SelectedItem != null)
                SerialPortsList.Items.Remove(SerialPortsList.SelectedItem);
        }

        // ── 网络主机管理 ─────────────────────────────────────────────────────────

        private void AddNetworkHost_Click(object sender, RoutedEventArgs e)
        {
            var host = NewNetworkHostText.Text.Trim();
            if (string.IsNullOrEmpty(host))
            {
                MessageBox.Show("请输入有效的IP地址", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (NetworkHostsList.Items.Contains(host))
            {
                MessageBox.Show($"{host} 已存在", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            NetworkHostsList.Items.Add(host);
            NewNetworkHostText.Text = "192.168.1.";
        }

        private void DeleteNetworkHost_Click(object sender, RoutedEventArgs e)
        {
            if (NetworkHostsList.SelectedItem != null)
                NetworkHostsList.Items.Remove(NetworkHostsList.SelectedItem);
        }

        // ── 命名模板预览 ─────────────────────────────────────────────────────────

        private void NamingTemplate_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateNamingPreview();
        }

        private void UpdateNamingPreview()
        {
            if (NamingPreviewText == null) return;
            var productName = ProductNameText?.Text?.Trim();
            if (string.IsNullOrEmpty(productName)) productName = "产品";

            var nameTemplate = NamingTemplateText?.Text ?? "{TypeName}测试工位{Index}";
            var idTemplate = NamingIdTemplateText?.Text ?? "DUT-{Index}";

            var displayName = nameTemplate
                .Replace("{TypeName}", productName)
                .Replace("{Index}", "1");
            var idName = idTemplate.Replace("{Index}", "1");

            NamingPreviewText.Text = $"{idName} → {displayName}";
        }

        // ── 保存 / 加载 / 验证 ──────────────────────────────────────────────────

        private async void SaveConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CollectAllFields();
                await _configManager.SaveUnifiedConfigurationAsync(_config);
                await _configManager.RefreshConfiguration();
                ValidationStatusText.Text = "✅ 配置已保存";
                ValidationStatusText.Foreground = System.Windows.Media.Brushes.Green;
                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ReloadConfig_Click(object sender, RoutedEventArgs e)
        {
            Window_Loaded(sender, e);
        }

        private void ValidateConfig_Click(object sender, RoutedEventArgs e)
        {
            var errors = new List<string>();

            // 基本字段
            if (string.IsNullOrWhiteSpace(ProductNameText.Text))
                errors.Add("产品名称不能为空");
            if (!int.TryParse(MaxConcurrentText.Text, out int maxC) || maxC <= 0)
                errors.Add("最大并发数必须为正整数");
            if (!int.TryParse(TestTimeoutText.Text, out int tout) || tout <= 0)
                errors.Add("测试超时必须为正整数（秒）");

            // 测试步骤验证
            if (TestStepsGrid.ItemsSource is System.Collections.ObjectModel.ObservableCollection<TestStepConfig> steps)
            {
                if (!steps.Any())
                {
                    errors.Add("至少需要配置一个测试步骤");
                }
                else
                {
                    // ID唯一性
                    var ids = steps.Select(s => s.Id).Where(id => !string.IsNullOrEmpty(id)).ToList();
                    if (ids.Count != ids.Distinct(StringComparer.OrdinalIgnoreCase).Count())
                        errors.Add("存在重复的步骤ID");

                    foreach (var step in steps)
                    {
                        if (string.IsNullOrWhiteSpace(step.Name))
                            errors.Add($"步骤 {step.Id} 名称不能为空");
                        if (step.Timeout.HasValue && step.Timeout.Value < 0)
                            errors.Add($"步骤 '{step.Name}' 超时值不能为负数");

                        // 验证 Expected 前缀
                        if (!string.IsNullOrEmpty(step.Expected))
                        {
                            var validPrefixes = new[] { "contains:", "equals:", "regex:", "notcontains:" };
                            bool hasPrefix = validPrefixes.Any(p => step.Expected.StartsWith(p, StringComparison.OrdinalIgnoreCase));
                            if (!hasPrefix && step.Expected.Contains(':'))
                                errors.Add($"步骤 '{step.Name}' 的期望结果前缀无效（支持 contains:/equals:/regex:）");
                        }
                    }
                }
            }

            if (errors.Any())
            {
                ValidationStatusText.Text = $"❌ {errors.First()}";
                ValidationStatusText.Foreground = System.Windows.Media.Brushes.Red;
                MessageBox.Show(string.Join("\n", errors), "验证失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
            {
                ValidationStatusText.Text = "✅ 配置有效";
                ValidationStatusText.Foreground = System.Windows.Media.Brushes.Green;
            }
        }

        // ── 测试步骤管理 ─────────────────────────────────────────────────────────

        private void AddStep_Click(object sender, RoutedEventArgs e)
        {
            if (TestStepsGrid.ItemsSource is System.Collections.ObjectModel.ObservableCollection<TestStepConfig> steps)
            {
                var step = new TestStepConfig
                {
                    Id = Guid.NewGuid().ToString("N")[..8],
                    Name = "新步骤",
                    Order = steps.Count + 1,
                    Enabled = true,
                    Type = string.Empty,
                    Channel = string.Empty,
                    Target = "dut",
                    Timeout = 5000
                };
                steps.Add(step);
                TestStepsGrid.SelectedItem = step;
                ShowStepDetail();
            }
        }

        private void DeleteStep_Click(object sender, RoutedEventArgs e)
        {
            if (TestStepsGrid.SelectedItem is TestStepConfig step &&
                TestStepsGrid.ItemsSource is System.Collections.ObjectModel.ObservableCollection<TestStepConfig> steps)
            {
                steps.Remove(step);
                RenumberSteps(steps);
                StepDetailPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void MoveStepUp_Click(object sender, RoutedEventArgs e)
        {
            if (TestStepsGrid.SelectedItem is TestStepConfig step &&
                TestStepsGrid.ItemsSource is System.Collections.ObjectModel.ObservableCollection<TestStepConfig> steps)
            {
                int idx = steps.IndexOf(step);
                if (idx > 0) { steps.Move(idx, idx - 1); RenumberSteps(steps); }
            }
        }

        private void MoveStepDown_Click(object sender, RoutedEventArgs e)
        {
            if (TestStepsGrid.SelectedItem is TestStepConfig step &&
                TestStepsGrid.ItemsSource is System.Collections.ObjectModel.ObservableCollection<TestStepConfig> steps)
            {
                int idx = steps.IndexOf(step);
                if (idx < steps.Count - 1) { steps.Move(idx, idx + 1); RenumberSteps(steps); }
            }
        }

        private static void RenumberSteps(System.Collections.ObjectModel.ObservableCollection<TestStepConfig> steps)
        {
            for (int i = 0; i < steps.Count; i++) steps[i].Order = i + 1;
        }

        private void CopyStep_Click(object sender, RoutedEventArgs e)
        {
            if (TestStepsGrid.SelectedItem is TestStepConfig step &&
                TestStepsGrid.ItemsSource is System.Collections.ObjectModel.ObservableCollection<TestStepConfig> steps)
            {
                var copy = new TestStepConfig
                {
                    Id = Guid.NewGuid().ToString("N")[..8],
                    Name = step.Name + " (副本)",
                    Order = steps.Count + 1,
                    Enabled = step.Enabled,
                    Type = step.Type,
                    Channel = step.Channel,
                    Target = step.Target,
                    Description = step.Description,
                    Command = step.Command,
                    Expected = step.Expected,
                    Timeout = step.Timeout,
                    Delay = step.Delay,
                    ContinueOnFailure = step.ContinueOnFailure
                };
                steps.Add(copy);
                TestStepsGrid.SelectedItem = copy;
                ShowStepDetail();
            }
        }

        private void TestStepsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ShowStepDetail();
        }

        private void ShowStepDetail()
        {
            if (TestStepsGrid.SelectedItem is TestStepConfig step)
            {
                StepDetailPanel.Visibility = Visibility.Visible;
                DetailIdText.Text = step.Id;
                DetailNameText.Text = step.Name;
                SetComboByContent(DetailTypeCombo, step.Type ?? string.Empty);
                SetComboByContent(DetailChannelCombo, step.Channel ?? string.Empty);
                SetComboByContent(DetailTargetCombo, step.Target ?? "dut");
                DetailDescriptionText.Text = step.Description ?? "";
                DetailCommandText.Text = step.Command ?? "";
                DetailExpectedText.Text = step.Expected ?? "";
                DetailTimeoutText.Text = step.Timeout?.ToString() ?? "";
                DetailDelayText.Text = step.Delay?.ToString() ?? "";
                DetailContinueOnFailureCheck.IsChecked = step.ContinueOnFailure;

                // MaxRetries 存储在 Parameters["MaxRetries"]
                var maxRetries = step.Parameters?.ContainsKey("MaxRetries") == true
                    ? step.Parameters["MaxRetries"]?.ToString() ?? ""
                    : "";
                DetailMaxRetriesText.Text = maxRetries;
            }
            else
            {
                StepDetailPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void ApplyStepDetail_Click(object sender, RoutedEventArgs e)
        {
            if (TestStepsGrid.SelectedItem is TestStepConfig step)
            {
                step.Id = DetailIdText.Text;
                step.Name = DetailNameText.Text;
                step.Type = !string.IsNullOrWhiteSpace(DetailTypeCombo.Text) ? DetailTypeCombo.Text : step.Type;
                step.Channel = !string.IsNullOrWhiteSpace(DetailChannelCombo.Text) ? DetailChannelCombo.Text : step.Channel;
                step.Target = !string.IsNullOrWhiteSpace(DetailTargetCombo.Text) ? DetailTargetCombo.Text : step.Target;
                step.Description = DetailDescriptionText.Text;
                step.Command = DetailCommandText.Text;
                step.Expected = DetailExpectedText.Text;
                if (int.TryParse(DetailTimeoutText.Text, out int timeout)) step.Timeout = timeout;
                if (int.TryParse(DetailDelayText.Text, out int delay)) step.Delay = delay;
                step.ContinueOnFailure = DetailContinueOnFailureCheck.IsChecked == true;

                // MaxRetries
                if (!string.IsNullOrWhiteSpace(DetailMaxRetriesText.Text) &&
                    int.TryParse(DetailMaxRetriesText.Text, out int maxRetries) && maxRetries > 0)
                {
                    step.Parameters ??= new Dictionary<string, object>();
                    step.Parameters["MaxRetries"] = maxRetries;
                }
                else if (step.Parameters != null)
                {
                    step.Parameters.Remove("MaxRetries");
                }

                TestStepsGrid.Items.Refresh();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

        private static void SetComboByContent(ComboBox combo, string value, bool partialMatch = false)
        {
            foreach (ComboBoxItem item in combo.Items)
            {
                var content = item.Content?.ToString() ?? "";
                if (partialMatch ? content.Contains(value) : content == value)
                {
                    combo.SelectedItem = item;
                    return;
                }
            }

            if (combo.IsEditable)
            {
                combo.Text = value;
                combo.SelectedItem = null;
                return;
            }

            if (combo.Items.Count > 0) combo.SelectedIndex = 0;
        }
    }
}
