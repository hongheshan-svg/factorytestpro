using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using System.IO;
using System.Text.Json;
using UTF.UI.Services;

namespace UTF.UI
{
    public partial class TestPlanEditorWindow : Window
    {
        public ObservableCollection<TestPlanStep> TestSteps { get; set; }
        private string _currentFilePath = "";
        private readonly ConfigurationManager? _configManager;

        public TestPlanEditorWindow(ConfigurationManager configManager)
        {
            _configManager = configManager;
            InitializeComponent();
            TestSteps = new ObservableCollection<TestPlanStep>();
            TestStepsDataGrid.ItemsSource = TestSteps;
            UpdateStepCount();
        }

        private void UpdateStepCount()
        {
            StepCountText.Text = $"步骤数: {TestSteps.Count}";
        }

        private void OpenPlan_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "打开测试计划",
                Filter = "测试计划文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                DefaultExt = "json"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    LoadTestPlan(dialog.FileName);
                    StatusText.Text = $"已打开: {Path.GetFileName(dialog.FileName)}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"打开文件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SavePlan_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentFilePath))
                SavePlanAs();
            else
                SaveTestPlan(_currentFilePath);
        }

        private void SavePlanAs()
        {
            var dialog = new SaveFileDialog
            {
                Title = "保存测试计划",
                Filter = "测试计划文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                DefaultExt = "json",
                FileName = PlanNameTextBox.Text + ".json"
            };

            if (dialog.ShowDialog() == true)
                SaveTestPlan(dialog.FileName);
        }

        private void NewPlan_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("确定要创建新的测试计划吗？当前未保存的更改将丢失。", "确认", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                ClearPlan();
                StatusText.Text = "已创建新测试计划";
            }
        }

        private async void RunTest_Click(object sender, RoutedEventArgs e)
        {
            if (TestSteps.Count == 0)
            {
                MessageBox.Show("请先添加测试步骤", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var validationErrors = ValidateStepsForRun();
            if (validationErrors.Count > 0)
            {
                MessageBox.Show(string.Join("\n", validationErrors), "测试计划不完整", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show($"确定要将测试计划 '{PlanNameTextBox.Text}' 写入系统配置并执行吗？", "确认运行", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                if (_configManager != null)
                {
                    var config = await _configManager.GetUnifiedConfigurationAsync();
                    if (config.TestProjectConfiguration == null)
                        config.TestProjectConfiguration = new TestProjectConfiguration();
                    if (config.TestProjectConfiguration.TestProject == null)
                        config.TestProjectConfiguration.TestProject = new TestProject();

                    config.TestProjectConfiguration.TestProject.Steps = TestSteps.Select((s, i) =>
                        new TestStepConfig
                        {
                            Id = $"step_{i + 1:D3}",
                            Name = s.StepName,
                            Description = s.Description,
                            Order = i + 1,
                            Enabled = true,
                            Type = s.StepType,
                            Channel = s.Channel,
                            Target = "dut",
                            Command = s.Command,
                            Expected = s.Expected,
                            Timeout = s.TimeoutSeconds * 1000
                        }).ToList();

                    await _configManager.SaveUnifiedConfigurationAsync(config);
                    StatusText.Text = "已写入系统配置";
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"写入配置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddStep_Click(object sender, RoutedEventArgs e)
        {
            TestSteps.Add(new TestPlanStep
            {
                StepName = $"新步骤{TestSteps.Count + 1}",
                Description = "请填写步骤描述",
                StepType = "",
                Channel = "",
                Command = "",
                Expected = "",
                TimeoutSeconds = 60,
                IsRequired = true
            });
            UpdateStepCount();
        }

        private List<string> ValidateStepsForRun()
        {
            var errors = new List<string>();

            for (int index = 0; index < TestSteps.Count; index++)
            {
                var step = TestSteps[index];
                var name = string.IsNullOrWhiteSpace(step.StepName) ? $"第 {index + 1} 步" : step.StepName;

                if (string.IsNullOrWhiteSpace(step.StepType))
                    errors.Add($"{name}: 缺少 StepType");

                if (string.IsNullOrWhiteSpace(step.Channel))
                    errors.Add($"{name}: 缺少 Channel");

                if (string.IsNullOrWhiteSpace(step.Command))
                    errors.Add($"{name}: 缺少 Command");
            }

            return errors;
        }

        private void RemoveStep_Click(object sender, RoutedEventArgs e)
        {
            if (TestStepsDataGrid.SelectedItem is TestPlanStep selectedStep)
            {
                TestSteps.Remove(selectedStep);
                UpdateStepCount();
            }
            else
            {
                MessageBox.Show("请先选择要删除的步骤", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void MoveStepUp_Click(object sender, RoutedEventArgs e)
        {
            if (TestStepsDataGrid.SelectedItem is TestPlanStep selectedStep)
            {
                int index = TestSteps.IndexOf(selectedStep);
                if (index > 0) TestSteps.Move(index, index - 1);
            }
        }

        private void MoveStepDown_Click(object sender, RoutedEventArgs e)
        {
            if (TestStepsDataGrid.SelectedItem is TestPlanStep selectedStep)
            {
                int index = TestSteps.IndexOf(selectedStep);
                if (index < TestSteps.Count - 1) TestSteps.Move(index, index + 1);
            }
        }

        private void LoadTestPlan(string filePath)
        {
            var json = File.ReadAllText(filePath);
            var plan = JsonSerializer.Deserialize<TestPlan>(json);

            if (plan != null)
            {
                PlanNameTextBox.Text = plan.Name;
                DescriptionTextBox.Text = plan.Description;
                EstimatedDurationTextBox.Text = plan.EstimatedDurationMinutes.ToString();
                AutoRunCheckBox.IsChecked = plan.AutoRun;
                GenerateReportCheckBox.IsChecked = plan.GenerateReport;

                TestSteps.Clear();
                foreach (var step in plan.TestSteps)
                    TestSteps.Add(step);

                _currentFilePath = filePath;
                UpdateStepCount();
            }
        }

        private void SaveTestPlan(string filePath)
        {
            try
            {
                var plan = new TestPlan
                {
                    Name = PlanNameTextBox.Text,
                    Description = DescriptionTextBox.Text,
                    EstimatedDurationMinutes = int.TryParse(EstimatedDurationTextBox.Text, out int duration) ? duration : 0,
                    AutoRun = AutoRunCheckBox.IsChecked ?? false,
                    GenerateReport = GenerateReportCheckBox.IsChecked ?? true,
                    TestSteps = new List<TestPlanStep>(TestSteps),
                    CreatedAt = DateTime.Now,
                    ModifiedAt = DateTime.Now
                };

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(plan, options);
                File.WriteAllText(filePath, json);

                _currentFilePath = filePath;
                StatusText.Text = $"已保存: {Path.GetFileName(filePath)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearPlan()
        {
            PlanNameTextBox.Text = "";
            DescriptionTextBox.Text = "";
            EstimatedDurationTextBox.Text = "";
            AutoRunCheckBox.IsChecked = false;
            GenerateReportCheckBox.IsChecked = true;
            TestSteps.Clear();
            _currentFilePath = "";
            UpdateStepCount();
        }
    }

    public class TestPlan
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public int EstimatedDurationMinutes { get; set; }
        public bool AutoRun { get; set; }
        public bool GenerateReport { get; set; } = true;
        public List<TestPlanStep> TestSteps { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
    }

    public class TestPlanStep
    {
        public string StepName { get; set; } = "";
        public string Description { get; set; } = "";
        public string StepType { get; set; } = "";
        public string Channel { get; set; } = "";
        public string Command { get; set; } = "";
        public string Expected { get; set; } = "";
        public int TimeoutSeconds { get; set; } = 60;
        public bool IsRequired { get; set; } = true;
    }
}
