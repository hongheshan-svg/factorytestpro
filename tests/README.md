# 测试目录

当前仓库已实现一个正式测试项目：`UTF.Core.Tests`。

## 已实现

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

## 规划中

以下测试项目仍属于后续扩展计划，当前尚未实现：

- `tests/UTF.Business.Tests/`
- `tests/UTF.Plugin.Host.Tests/`
- `tests/UTF.SystemStability.Runner/`

建议方向：

- `UTF.Business.Tests`：设备管理、健康检查、断连恢复、状态一致性。
- `UTF.Plugin.Host.Tests`：更大粒度的插件加载、版本冲突、超时和坏清单恢复场景。
- `UTF.SystemStability.Runner`：长稳、压测、故障注入、指标采集与报告汇总。

相关设计文档：

- `docs/plans/2026-03-10-system-stability-test-design.md`
