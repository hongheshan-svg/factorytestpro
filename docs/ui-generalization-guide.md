# UI 通用化使用说明（P0–P5）

本文说明桌面端如何在**配置驱动**前提下支持多行业、多角色与多种工作台，而无需为每个产品重写主程序。

相关实现已合入：插件能力驱动表单、工艺包选择、`UiProfile` 壳层、通信端点抽象、`parameterSchema` 动态参数、多工作台视图。

---

## 1. 总体原则

| 原则 | 含义 |
|------|------|
| 配置是真相 | 运行行为以 `unified-config.json` 为准；UI 只编辑/投影配置 |
| 能力来自插件 | 步骤类型、通道、参数字段由已加载插件声明，不靠写死 ComboBox |
| 行业差异进 Pack | 换产线优先选模板/工艺包，而不是从空白步骤表开始 |
| 角色裁剪菜单 | 操作员少菜单；工程师/管理员可见配置与插件 |
| 有限工作台模式 | 主屏在有限几种模式间切换，避免无限自定义布局 |

执行链路（UI）：

```
unified-config.json
  → ConfigurationManager（UTF.UI 薄包装）
  → DUTMonitorManager（UI 投影）
  → ConfigDrivenTestOrchestrator
  → ConfigDrivenTestEngine + 插件
```

无头 CLI 使用同一配置模型与编排器，见 `UTF.CLI/README.md`。

---

## 2. 快速上手（工程师）

```powershell
# 编译（自动 pack 插件）
dotnet build UniversalTestFramework.sln -c Debug

# 运行（Debug 可跳过登录）
dotnet run --project UTF.UI/UTF.UI.csproj -c Debug -- --skip-login
```

推荐第一次走通：

1. **配置 → 选择工艺包/模板…** → 选 `factory-quick-start-minimal`
2. 确认主界面无「配置校验未通过」横幅
3. 按需改通信端点 / 步骤参数
4. 点「开始测试」（Mock 步骤可先不接真机）
5. 需要时用工具栏 **工作台模式** 切换视图

---

## 3. 功能分册

### 3.1 P0 — 插件能力下拉 + 配置校验横幅

**Type / Channel**

- 配置中心、测试计划编辑器中的类型/通道列表来自已加载插件的 `supportedStepTypes` / `supportedChannels` 并集。
- 无插件时回退：`custom` / `serial` / `cmd` 与 `cmd` / `serial`（仅 UI 可编辑；运行仍需匹配插件或 `MockOutput`）。
- 服务：`IPluginCapabilityService`（`UTF.Plugin.Host.PluginCapabilityService`）。

**配置校验横幅**

- 主窗口加载、导入配置、配置中心应用后会运行 `ValidateConfigurationWithErrors`。
- 有错误时顶部琥珀色横幅显示摘要，并**禁用开始测试**。
- 在配置中心修复并保存后，横幅应消失。

---

### 3.2 P1 — 工艺包 / 模板选择

| 项 | 说明 |
|----|------|
| 菜单位置 | **配置 → 选择工艺包/模板…** |
| 目录 | 运行目录下 `config/templates/*.json`（构建时从仓库 `config/templates` 复制） |
| 权限 | `SystemConfig` **或** `TestPlanManagement` |
| 应用效果 | 整文件替换 `unified-config.json`；先写备份 `unified-config.backup.<timestamp>.json` |
| 刷新 | 应用后触发 `ConfigurationApplied`，DUT 列表与产品型号刷新 |

模板列表与行业说明见 [config/templates/README.md](../config/templates/README.md)。

**注意：** 应用模板会覆盖当前端口、步骤与产品信息。产线换线前请确认备份或导出当前配置。

---

### 3.3 P2 — UiProfile 与操作员壳层

在 `unified-config.json` 根级（或与 `SystemSettings` 并列，模型字段为 `UiProfile`）可配置：

```json
"UiProfile": {
  "Mode": "MultiDutBoard",
  "ShowStepColumns": true,
  "ShowAdvancedMenus": true,
  "AllowConfigEdit": true,
  "PrimaryActions": [ "StartAll", "StopAll", "Reset" ],
  "UnitLabel": "DUT",
  "Terminology": { }
}
```

| 字段 | 作用 |
|------|------|
| `Mode` | 默认工作台（见 P5）；会话中可临时改，不写盘 |
| `ShowStepColumns` | 多 DUT 表是否生成动态步骤列 |
| `ShowAdvancedMenus` / `AllowConfigEdit` | 是否显示工程菜单（配置中心、插件、计划编辑等） |
| `UnitLabel` | 统计/标题用语（如「工位」「ECU」） |

**安全优先：**

- 角色为 **Operator / Observer** 时，即使 profile 允许编辑，也强制精简菜单。
- 无 `SystemConfig` 且无测试计划相关权限时，工程壳关闭。
- 仍有 `TestPlanManagement` 等权限的工程师可看到工程菜单（含模板选择）。

---

### 3.4 P3 — 通信端点 Endpoints

在 `DUTConfiguration` 中推荐使用：

```json
"Endpoints": [
  {
    "Id": "serial-1",
    "Kind": "serial",
    "Address": "COM3",
    "DisplayName": "工位串口",
    "Settings": { }
  },
  {
    "Id": "net-1",
    "Kind": "telnet",
    "Address": "192.168.1.10",
    "DisplayName": "DUT 网口"
  }
]
```

| Kind | 典型用途 | 回写到 Legacy |
|------|----------|----------------|
| `serial` / `uart` | 串口 | `SerialPorts` |
| `network` / `telnet` / `adb` / `scpi` | 网络/仪器 | `NetworkHosts` |
| `custom` | 自定义 | 不自动归类 |

**兼容：**

- 若 `Endpoints` 为空，会从旧的 `CommunicationEndpoints.SerialPorts` / `NetworkHosts` 合成端点。
- 保存时 `EndpointMapper` 会镜像回 Legacy 列表，保证旧上下文键 `SerialPort` / `Host` 仍可用。
- 运行上下文额外提供 `Endpoint:{Id}` → Address。

配置中心：**通信端点**为主编辑区；**Legacy** 列表在展开区只读/自动同步。

---

### 3.5 P4 — 步骤 parameterSchema 动态表单

插件清单可选字段 `parameterSchema`（见 [plugins/README.md](../plugins/README.md)）：

```json
"parameterSchema": [
  {
    "name": "BaudRate",
    "type": "int",
    "label": "波特率",
    "default": "115200"
  },
  {
    "name": "SerialPort",
    "type": "string",
    "label": "串口",
    "required": true
  },
  {
    "name": "Parity",
    "type": "string",
    "default": "None",
    "enumValues": [ "None", "Odd", "Even" ]
  }
]
```

配置中心选中步骤并设置 Type/Channel 后：

1. 按插件能力匹配（优先级数值越小越高）
2. 生成 TextBox / CheckBox / ComboBox
3. 写入 `step.Parameters`；**未出现在 schema 中的键会保留**

内置样例 schema 已写在：`utf.driver.serial` / `telnet` / `adb` / `scpi` / `utf.executor.cmd`。

---

### 3.6 P5 — 工作台模式

| Mode | 视图 | 用途 |
|------|------|------|
| `MultiDutBoard` | 多 DUT 表格（默认） | 产线多工位并行看板 |
| `SingleStation` | 单工位大状态 + 步骤 + 日志 | 研发台 / 单工位调试 |
| `ScanToTest` | 扫码框 + PASS/FAIL | 过站扫码启测 |
| `InstrumentBench` | 端点列表 + 会话摘要 | 仪器/连接一览（只读） |

**切换方式：**

1. **持久：** `UiProfile.Mode`（启动、导入配置、配置应用后生效）
2. **会话临时：** 工具栏「工作台模式」下拉，或 **视图 → 工作台模式**
3. **恢复配置：** 视图 → 恢复配置模式（清除会话覆盖）

单工位 / 扫码模式通过 `DUTMonitorManager.StartTestsForDutAsync` 只跑选中/匹配的 DUT，不改变引擎语义。

---

## 4. 角色与菜单（对照）

| 能力 | 操作员（典型） | 工程师 | 管理员 |
|------|----------------|--------|--------|
| 开始 / 停止测试 | ✅ | ✅ | ✅ |
| 配置中心 / 插件 / 导入 | ❌（壳层隐藏） | ✅* | ✅ |
| 工艺包选择 | ❌ | ✅* | ✅ |
| 切换工作台（会话） | ✅ | ✅ | ✅ |
| 用户管理 | ❌ | 视权限 | ✅ |

\* 依赖 `UiProfile` 与具体权限标志。

---

## 5. 配置片段速查

完整最小可运行样例见模板库；下列为扩展字段拼装示意：

```json
{
  "ConfigurationInfo": { "Name": "Line-A" },
  "SystemSettings": { "ResultsPath": "./test-results" },
  "UiProfile": {
    "Mode": "MultiDutBoard",
    "ShowStepColumns": true,
    "AllowConfigEdit": true,
    "ShowAdvancedMenus": true,
    "UnitLabel": "工位"
  },
  "DUTConfiguration": {
    "ProductInfo": { "Name": "Demo", "Model": "D1", "Category": "Generic" },
    "GlobalSettings": { "DefaultMaxConcurrent": 8, "RetryCount": 1 },
    "Endpoints": [
      { "Id": "s1", "Kind": "serial", "Address": "COM3" }
    ],
    "CommunicationEndpoints": {
      "SerialPorts": [ "COM3" ],
      "NetworkHosts": []
    },
    "NamingConfig": {
      "Template": "{TypeName}工位{Index}",
      "IdTemplate": "DUT-{Index}"
    }
  },
  "TestProjectConfiguration": {
    "TestProject": {
      "Id": "demo",
      "Name": "Demo",
      "Enabled": true,
      "Steps": [
        {
          "Id": "s1",
          "Name": "Mock",
          "Order": 1,
          "Enabled": true,
          "Type": "custom",
          "Channel": "cmd",
          "Command": "echo",
          "Timeout": 5000,
          "Expected": "contains:OK",
          "Parameters": { "MockOutput": "OK" }
        }
      ]
    }
  }
}
```

---

## 6. 开发与扩展清单

| 要扩展… | 做这些 |
|---------|--------|
| 新步骤类型 | 实现插件 + manifest 的 `supportedStepTypes` / `supportedChannels` + 可选 `parameterSchema` |
| 新产线默认 | 在 `config/templates/` 增加 JSON；必要时更新模板 README 表格 |
| 新主屏布局 | 新增 `WorkbenchModes` 常量 + `UTF.UI/Views/*View` + `WorkbenchHost` 可见性绑定 |
| 校验规则 | 改 `UnifiedConfigurationAdapter.ValidateConfigurationWithErrors`（主界面横幅会消费） |

**测试：**

- UI：`tests/UTF.UI.Tests`（模板、工作台、配置中心）
- 端点：`tests/UTF.Configuration.Tests/EndpointMapperTests.cs`
- 插件 schema：`tests/UTF.Plugin.Host.Tests/PluginManifestParameterSchemaTests.cs`

---

## 7. 已知限制（有意保留）

1. **InstrumentBench** 不控制仪器，仅展示端点与会话摘要。  
2. **扫码模式** 不创建新 DUT 身份，映射到已有 DUT-1 / 序列号匹配项。  
3. **Endpoint → 步骤** 尚无「步骤直接绑定 Endpoint Id」的一等字段；驱动仍主要读 `SerialPort` / `Host` / 参数中的 Endpoint。  
4. **应用工艺包** 为全量替换，不做字段级 merge。  
5. 配置中心内联编辑后，主界面校验横幅需保存/应用事件才会刷新。

---

## 8. 相关文档

- [配置说明](../config/README.md)
- [模板库](../config/templates/README.md)
- [插件规范](../plugins/README.md)
- [CLI 无头运行](../UTF.CLI/README.md)
- [架构优化报告](architecture-optimization-report.md)
- [工厂用户手册](factory-user-guide.md)
