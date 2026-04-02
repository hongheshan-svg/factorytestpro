# 移除旧版 API，全面升级为新格式 Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 删除所有旧版兼容字段和类，仅保留新格式 API，消除 `GlobalDUTSettings`、`CustomTestProjects`、`DUTTypes` 等遗留代码。

**Architecture:** 新格式以 `GlobalSettings`、`ProductInfo`、`NamingConfig`、`TestProject` 为核心；`ConfigurationAdapter` 简化为纯读方法，不再做格式转换；`TestStepConfig` 使用 `Target/Type/Expected/Delay/Channel`，删除旧别名。

**Tech Stack:** .NET 10, C#, WPF, System.Text.Json — 仅修改 C# 源文件和 JSON 配置文件。

---

## 总览：受影响文件

| 文件 | 变更类型 |
|------|---------|
| `UTF.UI/Models/UnifiedConfigurationModels.cs` | 删除 ~200 行旧类和旧属性 |
| `UTF.UI/Services/ConfigurationAdapter.cs` | 删除规范化方法，简化 Get* 方法 |
| `UTF.UI/Services/ConfigurationManager.cs` | 删除 NormalizeConfiguration 调用 |
| `UTF.UI/Services/DUTMonitorManager.cs` | 删除 GetDeviceTypeName 旧方法 |
| `UTF.UI/Services/ConfigurationSchema.cs` | 删除 GetGlobalDUTSettingsSchema |
| `UTF.UI/MainWindow.xaml.cs` | 修复 GlobalDUTSettings / CustomTestProjects 读写 |
| `UTF.Core/RealTestExecutionEngine.cs` | 用新格式替换 CustomTestProjects 读取 |
| `config/unified-config.json` | 删除 BasicInfo/TestParameters/DisplaySettings 等旧字段 |

---

## Task 1: 清理 UnifiedConfigurationModels.cs — DUTConfiguration 旧属性

**Files:**
- Modify: `UTF.UI/Models/UnifiedConfigurationModels.cs`

**Step 1: 删除 DUTConfiguration 中的旧属性（4个）**

在 `DUTConfiguration` 类中，删除以下属性及其 `[JsonIgnore]` 特性：
```csharp
// 删除这些（约 80-92 行附近）：
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
public GlobalDUTSettings? GlobalDUTSettings { get; set; }

[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
public List<DUTType>? DUTTypes { get; set; }

[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
public ActiveDUTConfiguration? ActiveDUTConfiguration { get; set; }

[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
public List<PreConfiguredDUT>? PreConfiguredDUTs { get; set; }

// 兼容性辅助属性也删除：
public DUTBasicInfo BasicInfo { get; set; } = new();
public DUTTestParameters TestParameters { get; set; } = new();
public DUTDisplaySettings DisplaySettings { get; set; } = new();
public List<DUTCustomField> CustomFields { get; set; } = new();
```

**Step 2: 删除 TestProjectConfiguration 中的旧属性（5个）**

```csharp
// 删除（约 194-206 行附近）：
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
public List<SimpleCustomTestProject>? CustomTestProjects { get; set; }

[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
public List<TestMode>? TestModes { get; set; }

[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
public TestDefaultSettings? DefaultSettings { get; set; }

[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
public CustomStepSettings? CustomSteps { get; set; }

[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
public ReportSettings? ReportSettings { get; set; }
```

**Step 3: 简化 TestStepConfig — 删除兼容别名**

删除所有私有backing字段和旧名称属性（约 259-308 行），只保留新字段：
```csharp
// 删除这些私有字段：
private string? _targetType;
private string? _commandType;
private string? _expectedResult;
private int? _postExecutionDelay;
private string? _channelOverride;

// 删除这些属性（它们是只是 Target/Type/Expected/Delay/Channel 的别名）：
public string? TargetType { ... }
public string? CommandType { ... }
public string? ExpectedResult { ... }
public int? PostExecutionDelay { ... }
public string? ChannelOverride { ... }
```

**Step 4: 删除所有旧模型类（约 317-495 行）**

在 `// ==================== 向后兼容的旧结构模型 ====================` 注释以下，全部删除：
- `GlobalDUTSettings`
- `SimpleCustomTestProject`
- `DUTType`
- `DUTDefaultConnections`
- `ActiveDUTConfiguration`
- `PreConfiguredDUT`
- `DUTConnection`
- `DUTBasicInfo`
- `DeviceType`（在 DUTBasicInfo 里）
- `DUTTestParameters`
- `ParameterRange`
- `DUTDisplaySettings`
- `DUTCustomField`
- `TestDefaultSettings`
- `CustomStepSettings`
- `StepCategory`
- `TestStep`（旧版）
- `ReportSettings`

同时删除 `TestMode.NameEn` 属性（旧版兼容字段）。

**Step 5: 构建验证**
```bash
cd "D:\toolsource\test-m-c#-yh"
dotnet build UTF.UI/UTF.UI.csproj 2>&1 | tail -5
```
预期：出现编译错误（其他文件还在使用旧字段） — 这些错误将在后续 Task 中逐一修复。记录所有错误行号。

---

## Task 2: 简化 ConfigurationAdapter.cs

**Files:**
- Modify: `UTF.UI/Services/ConfigurationAdapter.cs`

**Step 1: 删除所有 Normalize 方法**

删除以下方法（约行 15-203）：
- `NormalizeConfiguration()`
- `NormalizeDUTConfiguration()` (private)
- `EnsureDUTDefaults()` (private)
- `NormalizeTestProjectConfiguration()` (private)
- `NormalizeTestSteps()` (private)

**Step 2: 简化 GetProductModel()**

替换为：
```csharp
public static string GetProductModel(UnifiedConfiguration config)
{
    return config?.DUTConfiguration?.ProductInfo?.Model ?? "Generic";
}
```

**Step 3: 简化 GetTestSteps()**

替换为：
```csharp
public static List<TestStepConfig> GetTestSteps(UnifiedConfiguration config)
{
    return config?.TestProjectConfiguration?.TestProject?.Steps
        ?? new List<TestStepConfig>();
}
```

**Step 4: 简化 GetMaxConcurrent()**

替换为：
```csharp
public static int GetMaxConcurrent(UnifiedConfiguration config)
{
    return config?.DUTConfiguration?.GlobalSettings?.DefaultMaxConcurrent ?? 16;
}
```

**Step 5: 简化 GetSerialPorts() / GetNetworkHosts()**

```csharp
public static List<string> GetSerialPorts(UnifiedConfiguration config)
{
    return config?.DUTConfiguration?.CommunicationEndpoints?.SerialPorts
        ?? new List<string> { "COM3", "COM4", "COM5", "COM6" };
}

public static List<string> GetNetworkHosts(UnifiedConfiguration config)
{
    return config?.DUTConfiguration?.CommunicationEndpoints?.NetworkHosts
        ?? new List<string> { "192.168.1.10", "192.168.1.11" };
}
```

**Step 6: 简化 GetNamingTemplate() / GetIdTemplate()**

```csharp
public static string GetNamingTemplate(UnifiedConfiguration config)
{
    return config?.DUTConfiguration?.NamingConfig?.Template
        ?? "{TypeName}测试工位{Index}";
}

public static string GetIdTemplate(UnifiedConfiguration config)
{
    return config?.DUTConfiguration?.NamingConfig?.IdTemplate
        ?? "DUT-{Index}";
}
```

**Step 7: 简化 ValidateConfiguration()**

```csharp
public static bool ValidateConfiguration(UnifiedConfiguration config)
{
    if (config == null) return false;
    if (string.IsNullOrEmpty(config.ConfigurationInfo?.Name)) return false;
    if (config.SystemSettings == null) return false;
    if (config.DUTConfiguration == null) return false;
    if (config.TestProjectConfiguration == null) return false;
    var steps = GetTestSteps(config);
    return steps != null && steps.Any();
}
```

**Step 8: 构建验证**
```bash
dotnet build UTF.UI/UTF.UI.csproj 2>&1 | tail -5
```

---

## Task 3: 更新 ConfigurationManager.cs

**Files:**
- Modify: `UTF.UI/Services/ConfigurationManager.cs`

**Step 1: 删除 NormalizeConfiguration 调用**

找到并删除以下两处（约行 118 和 139）：
```csharp
// 删除此行（约 118）：
config = ConfigurationAdapter.NormalizeConfiguration(config);

// 删除此行（约 139）：
config = ConfigurationAdapter.NormalizeConfiguration(config);
```

**Step 2: 构建验证**
```bash
dotnet build UTF.UI/UTF.UI.csproj 2>&1 | tail -5
```

---

## Task 4: 清理 DUTMonitorManager.cs 和 ConfigurationSchema.cs

**Files:**
- Modify: `UTF.UI/Services/DUTMonitorManager.cs`
- Modify: `UTF.UI/Services/ConfigurationSchema.cs`

**Step 1: 删除 DUTMonitorManager 中的 GetDeviceTypeName 方法**

找到（约行 392）并删除：
```csharp
private string GetDeviceTypeName(DUTConfiguration dutConfig, string typeId)
{
    var deviceType = dutConfig.BasicInfo.DeviceTypes.FirstOrDefault(t => t.Id == typeId);
    return deviceType?.Name ?? "未知设备";
}
```

**Step 2: 删除 ConfigurationSchema 中的 GetGlobalDUTSettingsSchema**

找到（约行 107-145）并删除整个方法：
```csharp
public static ConfigSchema GetGlobalDUTSettingsSchema()
{
    // ... 删除整个方法
}
```

同时搜索是否有地方调用了 `GetGlobalDUTSettingsSchema()`，一并删除调用点：
```bash
grep -rn "GetGlobalDUTSettingsSchema" "D:\toolsource\test-m-c#-yh\UTF.UI\"
```

**Step 3: 构建验证**
```bash
dotnet build UTF.UI/UTF.UI.csproj 2>&1 | tail -5
```

---

## Task 5: 修复 MainWindow.xaml.cs — GlobalDUTSettings 读取

**Files:**
- Modify: `UTF.UI/MainWindow.xaml.cs`

**Step 1: 修复 DUT 参数读取（约行 3737-3786）**

将所有带双重 `?? GlobalDUTSettings?.xxx` 的读取改为只读 `GlobalSettings`：

```csharp
// 旧代码（约 3737-3739）：
Text = (currentConfig.DUTConfiguration.GlobalSettings?.DefaultMaxConcurrent
     ?? currentConfig.DUTConfiguration.GlobalDUTSettings?.DefaultMaxConcurrent
     ?? 16).ToString(),
// 新代码：
Text = (currentConfig.DUTConfiguration.GlobalSettings?.DefaultMaxConcurrent ?? 16).ToString(),

// 旧代码（约 3760）：
Text = currentConfig.DUTConfiguration.GlobalDUTSettings?.TestTimeout?.ToString() ?? "300",
// 新代码：
Text = (currentConfig.DUTConfiguration.GlobalSettings?.TestTimeout ?? 300).ToString(),

// 旧代码（约 3770）：
IsChecked = currentConfig.DUTConfiguration.GlobalDUTSettings?.EnablePreTestCheck ?? true,
// 新代码：
IsChecked = currentConfig.DUTConfiguration.GlobalSettings?.EnablePreTestCheck ?? true,

// 旧代码（约 3786）：
Text = currentConfig.DUTConfiguration.GlobalDUTSettings?.RetryCount?.ToString() ?? "3",
// 新代码：
Text = (currentConfig.DUTConfiguration.GlobalSettings?.RetryCount ?? 3).ToString(),
```

**Step 2: 修复 GlobalSettings 显示区块（约行 6075-6115）**

将整个带 `if (dutConfig.GlobalSettings != null || dutConfig.GlobalDUTSettings != null)` 的区块替换为：
```csharp
var globalSettings = dutConfig.GlobalSettings ?? new GlobalSettings();
var maxConcurrent = globalSettings.DefaultMaxConcurrent ?? 16;
var testTimeout = globalSettings.TestTimeout ?? 300;
var retryCount = globalSettings.RetryCount ?? 3;
var enablePreTestCheck = globalSettings.EnablePreTestCheck ?? true;

AddConfigField(basicSettingsPanel, "最大并发DUT数量:", maxConcurrent.ToString(), "DefaultMaxConcurrent");
AddConfigField(basicSettingsPanel, "测试超时(秒):", testTimeout.ToString(), "TestTimeout");
AddConfigField(basicSettingsPanel, "重试次数:", retryCount.ToString(), "RetryCount");
AddConfigCheckBox(basicSettingsPanel, "启用预测试检查", enablePreTestCheck, "EnablePreTestCheck");
```

删除旧的 `if (dutConfig.GlobalDUTSettings != null)` 分支（ProductModel行）。ProductModel 改为从 `dutConfig.ProductInfo?.Model` 显示：
```csharp
AddConfigField(basicSettingsPanel, "产品型号:", dutConfig.ProductInfo?.Model ?? "", "ProductModel");
```

**Step 3: 修复配置验证区块（约行 5851-5868）**

将旧的验证代码替换为：
```csharp
// 验证DUT配置
if (config.DUTConfiguration?.GlobalSettings != null)
{
    var dutSettings = config.DUTConfiguration.GlobalSettings;
    results.Add($"✅ 最大并发DUT数量: {dutSettings.DefaultMaxConcurrent}");
    results.Add($"✅ 测试超时: {dutSettings.TestTimeout}秒");
    results.Add($"✅ 重试次数: {dutSettings.RetryCount}");
    results.Add($"✅ 预检查: {(dutSettings.EnablePreTestCheck == true ? "启用" : "禁用")}");
}

// 验证测试项目配置
if (config.TestProjectConfiguration?.TestProject != null)
{
    var project = config.TestProjectConfiguration.TestProject;
    results.Add($"✅ 测试项目: {project.Name}");
    results.Add($"✅ 测试步骤数量: {project.Steps?.Count ?? 0}个");
    var enabledSteps = project.Steps?.Count(s => s.Enabled) ?? 0;
    results.Add($"✅ 启用的步骤: {enabledSteps}个");
}
```

**Step 4: 构建验证**
```bash
dotnet build UTF.UI/UTF.UI.csproj 2>&1 | tail -5
```

---

## Task 6: 修复 MainWindow.xaml.cs — GlobalDUTSettings 写入

**Files:**
- Modify: `UTF.UI/MainWindow.xaml.cs`

**Step 1: 修复配置保存 switch/case 区块（约行 5616-5668）**

找到 switch/case 中写入 `GlobalDUTSettings` 的代码，全部改为写入 `GlobalSettings`：

```csharp
// case "DefaultMaxConcurrent"（约 5616）：删除旧格式写入部分
case "DefaultMaxConcurrent":
    if (control is TextBox maxDutTextBox && int.TryParse(maxDutTextBox.Text, out var maxDutCount))
    {
        if (config.DUTConfiguration.GlobalSettings == null)
            config.DUTConfiguration.GlobalSettings = new GlobalSettings();
        config.DUTConfiguration.GlobalSettings.DefaultMaxConcurrent = maxDutCount;
        _logger?.Info($"更新最大并发DUT数量: {maxDutCount}");
    }
    break;

// case "DUTTimeout"（约 5633）：
case "DUTTimeout":
    if (control is TextBox timeoutTextBox && int.TryParse(timeoutTextBox.Text, out var timeout))
    {
        if (config.DUTConfiguration.GlobalSettings == null)
            config.DUTConfiguration.GlobalSettings = new GlobalSettings();
        config.DUTConfiguration.GlobalSettings.TestTimeout = timeout;
        _logger?.Info($"更新测试超时时间: {timeout}秒");
    }
    break;

// case "MaxRetryCount"（约 5643）：
case "MaxRetryCount":
    if (control is TextBox retryTextBox && int.TryParse(retryTextBox.Text, out var retryCount))
    {
        if (config.DUTConfiguration.GlobalSettings == null)
            config.DUTConfiguration.GlobalSettings = new GlobalSettings();
        config.DUTConfiguration.GlobalSettings.RetryCount = retryCount;
        _logger?.Info($"更新重试次数: {retryCount}");
    }
    break;

// case "AutoReconnect"（约 5653）：
case "AutoReconnect":
    if (control is CheckBox preTestCheckBox)
    {
        if (config.DUTConfiguration.GlobalSettings == null)
            config.DUTConfiguration.GlobalSettings = new GlobalSettings();
        config.DUTConfiguration.GlobalSettings.EnablePreTestCheck = preTestCheckBox.IsChecked ?? false;
        _logger?.Info($"更新预测试检查设置: {preTestCheckBox.IsChecked}");
    }
    break;

// case "ProductModel"（约 5663）：改为写入 ProductInfo
case "ProductModel":
    if (control is TextBox productModelTextBox)
    {
        if (config.DUTConfiguration.ProductInfo == null)
            config.DUTConfiguration.ProductInfo = new ProductInfo();
        config.DUTConfiguration.ProductInfo.Model = productModelTextBox.Text;
        _logger?.Info($"更新产品型号: {productModelTextBox.Text}");
    }
    break;
```

注意：同时查找 `"TestTimeout"` case（区分 "DUTTimeout" key 和 "TestTimeout" case 键名），确保统一。

**Step 2: 修复行 5517/5522 的读取**

```bash
grep -n "GlobalDUTSettings" "D:\toolsource\test-m-c#-yh\UTF.UI\MainWindow.xaml.cs"
```

把这两行替换为读 `GlobalSettings`。

**Step 3: 构建验证**
```bash
dotnet build UTF.UI/UTF.UI.csproj 2>&1 | tail -5
```

---

## Task 7: 修复 MainWindow.xaml.cs — CustomTestProjects 引用

**Files:**
- Modify: `UTF.UI/MainWindow.xaml.cs`

**Step 1: 修复项目下拉加载（约行 4654-4662）**

```csharp
// 旧代码（从 JsonElement 里找 CustomTestProjects）：
if (configData.TryGetProperty("CustomTestProjects", out var customProjects)) { ... }

// 新代码（从 TestProjectConfiguration.TestProject 加载）：
if (configData.TryGetProperty("TestProjectConfiguration", out var testProjectConfig) &&
    testProjectConfig.TryGetProperty("TestProject", out var testProject))
{
    var projectName = testProject.TryGetProperty("Name", out var n) ? n.GetString() : "默认项目";
    var projectId = testProject.TryGetProperty("Id", out var id) ? id.GetString() : "";
    projectCombo.Items.Add(new ComboBoxItem { Content = $"📋 {projectName}", Tag = testProject });
}
```

**Step 2: 修复 LoadTestScenariosIntoComboBox（约行 7196-7235）**

删除 `else if (projectConfig?.CustomTestProjects?.Any() == true)` 整个分支，以及调试日志中的 `CustomTestProjects` 引用：
```csharp
private void LoadTestScenariosIntoComboBox(ComboBox comboBox, TestProjectConfiguration? projectConfig)
{
    comboBox.Items.Add(new ComboBoxItem { Content = "请选择测试场景...", Tag = "", IsSelected = true });

    if (projectConfig?.TestProject != null)
    {
        var project = projectConfig.TestProject;
        var statusIcon = project.Enabled ? "✅" : "❌";
        var modeName = projectConfig.TestMode?.Name ?? "生产测试";
        var displayText = $"{statusIcon} {project.Name} ({modeName})";
        comboBox.Items.Add(new ComboBoxItem { Content = displayText, Tag = project.Id });
        _logger?.Info($"[配置显示] 加载测试项目: {project.Name}, 步骤数: {project.Steps?.Count ?? 0}");
    }
    else
    {
        _logger?.Warning("[配置显示] 没有找到任何测试项目配置！");
    }
}
```

**Step 3: 修复 UpdateTestStepsList（约行 7241-7266）**

删除 `else if (projectConfig?.CustomTestProjects != null)` 分支，只保留新格式路径：
```csharp
private void UpdateTestStepsList(ListView listView, string projectId, TestProjectConfiguration? projectConfig)
{
    listView.Items.Clear();
    List<TestStepConfig>? testSteps = null;

    if (projectConfig?.TestProject != null)
    {
        testSteps = projectConfig.TestProject.Steps;
        _logger?.Info($"[配置显示] 获取测试步骤, 步骤数: {testSteps?.Count ?? 0}");
    }

    // ... 继续之后的步骤显示逻辑（保留不变）
}
```

**Step 4: 修复 ShowCustomTestProjectDialog 的 WriteBack 路径（约行 5001-5034）**

找到写入 `CustomTestProjects` 的代码，改为写入 `TestProjectConfiguration.TestProject`：
```csharp
// 旧代码检查:
if (configDict?.TryGetValue("CustomTestProjects", out var customProjectsObj) == true) { ... }
configDict["CustomTestProjects"] = customProjectsList;

// 新代码（写入 TestProject 字段）：
if (configDict?.TryGetValue("TestProjectConfiguration", out var testProjObj) == true &&
    testProjObj is Dictionary<string, object> testProjDict)
{
    testProjDict["TestProject"] = newProjectData;
}
```

**Step 5: 构建验证**
```bash
dotnet build UTF.UI/UTF.UI.csproj 2>&1 | tail -5
```

---

## Task 8: 更新 RealTestExecutionEngine.cs

**Files:**
- Modify: `UTF.Core/RealTestExecutionEngine.cs`

**Step 1: 查看上下文**
```bash
# 查看行 480-520
```
用 Read 工具读 `UTF.Core/RealTestExecutionEngine.cs` 第 480-520 行，了解该方法的完整逻辑。

**Step 2: 替换 CustomTestProjects 读取**

旧代码（约行 492-500）从独立的 `test-project-config.json` 中读取 `CustomTestProjects`。
新代码应从统一配置中读取 `TestProject`：

```csharp
// 旧代码：
if (configData.TryGetProperty("CustomTestProjects", out var projects))
{
    foreach (var project in projects.EnumerateArray())
    {
        if (project.GetProperty("Id").GetString() == testId ||
            project.GetProperty("Name").GetString() == testId)
        {
            return JsonSerializer.Deserialize<TestProjectConfiguration>(project.GetRawText());
        }
    }
}

// 新代码（从统一配置文件读取）：
var unifiedConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "unified-config.json");
if (File.Exists(unifiedConfigPath))
{
    var unifiedJson = await File.ReadAllTextAsync(unifiedConfigPath);
    var unifiedData = JsonSerializer.Deserialize<JsonElement>(unifiedJson);
    if (unifiedData.TryGetProperty("TestProjectConfiguration", out var testProjConfig))
    {
        return JsonSerializer.Deserialize<TestProjectConfiguration>(testProjConfig.GetRawText(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }
}
```

**Step 3: 构建验证**
```bash
dotnet build UTF.Core/UTF.Core.csproj 2>&1 | tail -5
```

---

## Task 9: 清理 unified-config.json

**Files:**
- Modify: `config/unified-config.json`

**Step 1: 删除旧字段节**

用 Read 工具查看当前 `config/unified-config.json`，找到并删除以下顶层字段（在 `DUTConfiguration` 内）：
- `BasicInfo` 整个对象
- `TestParameters` 整个对象
- `DisplaySettings` 整个对象
- `CustomFields` 整个数组

**Step 2: 更新测试步骤字段名**

在 `TestProjectConfiguration.TestProject.Steps[]` 中，将旧字段名替换为新字段名：
- `TargetType` → `Target`
- `CommandType` → `Type`
- `ExpectedResult` → `Expected`
- `PostExecutionDelay` → `Delay`
- `ChannelOverride` → `Channel`

**Step 3: 验证 JSON 格式**
```bash
cd "D:\toolsource\test-m-c#-yh"
dotnet run --project UTF.UI 2>&1 | head -20
# 或直接构建验证
dotnet build UniversalTestFramework.sln 2>&1 | tail -5
```

---

## Task 10: 全量构建与最终验证

**Step 1: 完整构建**
```bash
cd "D:\toolsource\test-m-c#-yh"
dotnet build UniversalTestFramework.sln 2>&1
```
预期：**0 错误，0 警告**

**Step 2: 确认无旧字段残留**
```bash
grep -rn "GlobalDUTSettings\|CustomTestProjects\|DUTTypes\|ActiveDUTConfiguration\|PreConfiguredDUT\|BasicInfo\|TargetType\|CommandType\|ExpectedResult\|PostExecutionDelay\|ChannelOverride\|NormalizeConfiguration" \
  "D:\toolsource\test-m-c#-yh\UTF.UI\" \
  "D:\toolsource\test-m-c#-yh\UTF.Core\" 2>/dev/null
```
预期：**无结果**（或仅注释中的说明性文字）

**Step 3: 检查 unified-config.json 新格式**

验证配置文件中：
- `DUTConfiguration.GlobalSettings` 存在且有值
- `DUTConfiguration.ProductInfo` 存在且有值
- `TestProjectConfiguration.TestProject.Steps` 存在且有步骤
- 无 `BasicInfo`、`TestParameters`、`DisplaySettings` 等旧字段
