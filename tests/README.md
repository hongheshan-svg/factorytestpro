# 测试目录

当前仓库已实现一个正式测试项目：`UTF.Core.Tests`，并新增 5 个测试项目（含冒烟测试）。

## 已实现

### UTF.Core.Tests（基线）

当前测试项目位于 `tests/UTF.Core.Tests/`，覆盖的重点包括：

- 配置驱动测试引擎行为
- 配置验证器组合与桥接逻辑
- 事件总线集成行为
- 插件宿主的初始化、升级、重载与冲突路径

当前已落地的代表性测试文件：

- `tests/UTF.Core.Tests/ConfigDrivenTestEngineTests.cs`
- `tests/UTF.Core.Tests/ConfigDrivenTestValidatorTests.cs`
- `tests/UTF.Core.Tests/CompositeConfigurationValidatorTests.cs`
- `tests/UTF.Core.Tests/ConfigDrivenReportBridgeTests.cs`
- `tests/UTF.Core.Tests/EventBusIntegrationTests.cs`
- `tests/UTF.Core.Tests/StepExecutorPluginHostTests.cs`

运行方式：

- `dotnet test`
- `dotnet test tests/UTF.Core.Tests/UTF.Core.Tests.csproj --logger "console;verbosity=detailed"`

### 已 scaffold（冒烟测试，2026-07-20）

以下 5 个测试项目已搭建完成，每个包含少量冒烟/真实测试（`[Fact]` + `[Trait("Category","Unit")]`，`Method_Scenario_ExpectedResult` 命名，Arrange/Act/Assert 结构）：

- `tests/UTF.Business.Tests/` — `DeviceManagerTests.cs`（3 项：空注册列表、按类型查询为空、null 设备注册失败）。scaffolded (smoke tests)
- `tests/UTF.Plugin.Host.Tests/` — `PluginHostSmokeTests.cs`（2 项：null 插件根目录抛 `ArgumentNullException`、`LoadedPlugins` 初始为空）。scaffolded (smoke tests)
- `tests/UTF.Reporting.Tests/` — `ReportGeneratorSmokeTests.cs`（2 项：`SupportedFormats` 含 HTML、空数据集 HTML 报告生成成功）。scaffolded (smoke tests)
- `tests/UTF.Configuration.Tests/` — `ValidatorsSmokeTests.cs`（3 项：`SystemConfigValidator` 合法配置无错误、`DUTConfigValidator` 非法并发数报错、合法配置无错误）。scaffolded (smoke tests)
- `tests/UTF.UI.Tests/` — `SmokeTests.cs`（1 项占位 `True_IsTrue`，目标 `net10.0-windows` + `<UseWPF>true</UseWPF>`）。scaffolded (smoke tests)

### 构建与测试状态（2026-07-20）

- `dotnet restore UniversalTestFramework.sln` - 通过。
- `dotnet build` 各非 UI 测试项目 - 0 错误。
- `dotnet test`：
  - `UTF.Core.Tests` - 79/79 通过（未受影响）。
  - `UTF.Business.Tests` - 6/6 通过。
  - `UTF.Plugin.Host.Tests` - 2/2 通过。
  - `UTF.Reporting.Tests` - 2/2 通过。
  - `UTF.Configuration.Tests` - 3/3 通过。
  - `UTF.UI.Tests` - 1/1 通过（占位冒烟测试）。
  - 合计可运行测试：93/93 通过。

### Coverage gaps

`tests/UTF.UI.Tests/` 当前仅包含占位冒烟测试（`SmokeTests.cs` 中的 `True_IsTrue`）。后续应补充针对 `UTF.UI` 转换器（`NullToVisibilityConverter`、`StringToNullableIntConverter`）与视图模型（`MainWindowViewModel`、`ConfigurationCenterViewModel`、`QuickTestWizardViewModel`）的真实单元测试，使用 NSubstitute 模拟 `IPermissionManager`、`DUTMonitorManager`、`ConfigurationManager` 等依赖。

其他规划中项目的覆盖缺口见下方"规划中"一节。

## 规划中

以下测试项目仍属于后续扩展计划，当前尚未实现：

- `tests/UTF.SystemStability.Runner/`

建议方向：

- `UTF.Business.Tests`：设备管理、健康检查、断连恢复、状态一致性。
- `UTF.Plugin.Host.Tests`：更大粒度的插件加载、版本冲突、超时和坏清单恢复场景。
  - TODO：将 `tests/UTF.Core.Tests/StepExecutorPluginHostTests.cs` 中更丰富的场景迁移至本工程（保留 79 基线计数）。
- `UTF.SystemStability.Runner`：长稳、压测、故障注入、指标采集与报告汇总。

相关设计文档：

- `docs/plans/2026-03-10-system-stability-test-design.md`
