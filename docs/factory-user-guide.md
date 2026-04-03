# 工厂用户使用手册

## 适用场景

本手册面向需要建设或扩展自动化测试能力的工厂、产线工程师、测试工程师和设备集成人员。

Universal Test Framework 适合以下场景：

- 需要同时测试 16 个以上 DUT 的产线工位
- 需要快速切换不同产品测试方案的工厂
- 需要把串口、Telnet、SCPI、ADB 或自定义执行器统一到同一测试平台的团队
- 需要通过配置和插件持续扩展测试内容，而不是每次都改核心程序的项目

## 这套系统能解决什么问题

- 把不同产品、不同 DUT、不同通信方式的测试流程统一到一套配置驱动平台中
- 降低新增产品导入测试的成本
- 降低人工操作和人工判定带来的质量波动
- 支持在同一测试会话中并行调度多个 DUT，提高产线吞吐量
- 通过可重复、可配置、可扩展的流程帮助工厂提升产品质量保障能力

## 系统组成

- `UTF.UI`：Windows 桌面主程序，负责操作界面、配置加载和流程入口
- `UTF.Core`：测试执行、调度、验证、上下文变量、重试、条件执行等核心能力
- `UTF.Configuration`：配置模型、配置解析和配置校验
- `UTF.Plugin.Host`：插件发现、加载、优先级选择和执行分发
- `UTF.Plugins.Drivers` / `UTF.Plugins.Example`：现有驱动与示例插件实现

## 工厂落地时的典型流程

### 1. 准备测试工位

- Windows 环境
- .NET 10 SDK 或已构建好的发布版本
- DUT 所需连接方式，如串口、网络、ADB、仪器等
- 插件输出目录和运行配置文件

### 2. 配置产品测试方案

核心配置文件位于 [config/README.md](../config/README.md) 对应的 `config/unified-config.json`。

通常需要配置这些内容：

- 产品信息
- DUT 连接方式
- MAC 范围或设备识别规则
- 测试步骤清单
- 每个步骤的命令、通道、期望结果、超时、重试次数

### 3. 配置测试步骤

每个测试步骤至少可以定义：

- `Id`
- `Name`
- `Type`
- `Channel`
- `Command`
- `Expected`
- `Timeout`

还可以进一步配置：

- `TargetDeviceId`
- `RetryCount`
- `StoreResultAs`
- `ConditionExpression`
- `ValidationRules`
- `Parameters`

支持的校验前缀包括：

- `contains:`
- `equals:`
- `regex:`
- `notcontains:`

命令和期望值支持上下文变量模板：

- `{{key}}`
- `${key}`

## 如何扩展新的测试内容

### 方式 1：只改配置

如果现有插件已经支持你的步骤类型和通道，通常只需要修改配置文件，无需改动核心代码。

适用情况：

- 新增命令检查
- 新增版本校验
- 新增 MAC 或 SN 校验
- 新增条件执行步骤
- 新增上下文变量传递步骤

### 方式 2：新增插件

如果测试对象需要新的执行方式，可以新增插件实现并接入插件宿主。

插件目录规范见 [plugins/README.md](../plugins/README.md)。

适用情况：

- 新设备驱动
- 新协议执行器
- 新仪器控制方式
- 特殊工厂设备联动

## 多 DUT 并行测试建议

如果你计划在一个工位上同时运行 16 个以上 DUT，建议注意以下事项：

- 为每类连接资源预留明确的端口或地址范围
- 在配置中明确区分 DUT 通道和目标设备标识
- 尽量避免多个 DUT 共享同一个不可并发访问的外设资源
- 对关键步骤设置合理的 `Timeout` 和 `RetryCount`
- 对结果依赖步骤使用 `StoreResultAs` 和 `ConditionExpression` 做串联控制
- 在正式量产前先用小批量 DUT 做并发压测和异常恢复验证

## 推荐导入步骤

### 新工厂第一次接入

1. 先构建并运行主程序
2. 先从 `config/templates/factory-quick-start-minimal.json` 准备一份最小可运行的 `unified-config.json`
3. 只接入一台 DUT 跑通完整流程
4. 再扩展到多台 DUT 并发运行
5. 最后再增加插件、仪器或复杂验证逻辑

## 快速上手配置样例

如果你希望最快验证平台是否跑通，优先使用：

- `config/templates/factory-quick-start-minimal.json`

这个样例适合先验证以下内容：

- 主机上的命令执行能力是否正常
- 串口通信是否已接通
- 测试结果能否写入并传递到后续步骤
- 条件执行是否按预期生效

### 新产品导入

1. 复制配置模板
2. 替换产品信息和连接信息
3. 先接入基础版本检查和连通性测试
4. 再补充功能测试、校验规则和工艺步骤
5. 在产线导入前进行连续稳定性验证

## 运行与验证

常用命令：

```powershell
dotnet restore UniversalTestFramework.sln
dotnet build UniversalTestFramework.sln -c Debug
dotnet run --project UTF.UI/UTF.UI.csproj -c Debug
dotnet test tests/UTF.Core.Tests/UTF.Core.Tests.csproj --logger "console;verbosity=minimal"
```

## 故障排查建议

如果测试流程不符合预期，优先检查：

- `config/unified-config.json` 是否与当前产品一致
- 步骤的 `Type` 和 `Channel` 是否有对应插件支持
- 插件 manifest 是否被正确打包到运行目录
- `Expected` 或 `ValidationRules` 是否与真实设备输出一致
- 并发场景下是否存在资源冲突或超时设置过短的问题

## 进一步协作

- 如果你所在工厂希望扩展自动化测试能力，可以联系：hongheshan@gmail.com
- 如果你想一起完善代码、驱动、插件或文档，欢迎提交 Issue 和 Pull Request
- 协作方式说明见 [CONTRIBUTING.md](../CONTRIBUTING.md)
