# 配置模板库

本目录包含面向不同行业和产品类型的测试配置模板 v2.0，可直接复制并修改为实际产品配置。

## 模板列表

| 文件 | 行业 | 产品类型 | 通信协议 | 仪器 |
|------|------|---------|---------|------|
| `consumer-electronics-smart-speaker.json` | 消费电子 | 智能音箱 | Serial + SCPI | 电源、音频分析仪 |
| `consumer-electronics-android.json` | 消费电子 | Android 终端 | ADB (USB) | — |
| `automotive-ecu-eol.json` | 汽车电子 | ECU 控制器 | Serial + SCPI | 电源/万用表 |
| `instrument-integration-pcba.json` | PCBA 制造 | WiFi 射频模组 | Serial + SCPI | 电源/万用表/示波器/频谱仪 |

## 使用方法

1. 将模板复制到 `config/unified-config.json`
2. 修改 `DUTConfiguration.ProductInfo` 为实际产品信息
3. 修改 `CommunicationEndpoints` 为实际串口/网络地址
4. 修改各步骤的 `Parameters.InstrumentAddress` 为实际仪器地址
5. 调整 `ValidationRules.NumericRange` 为实际测试规格

## v2.0 新特性

所有模板均支持以下高级功能：

- **变量模板**: 命令中使用 `{{key}}` 引用运行时变量（如 `{{serialNumber}}`、`{{macAddress}}`）
- **结果传递**: `StoreResultAs` 将步骤输出存入上下文，后续步骤可引用
- **条件执行**: `ConditionExpression` 支持 `exists:key`、`equals:key:value` 等条件
- **多仪器编排**: `TargetDeviceId` 标识步骤目标仪器，支持电源→万用表→示波器→DUT 协作
- **扩展验证**: `ValidationRules` 支持 `NumericRange`、`MustContainAll`、`MustNotContainAny`、`Regex`
- **重试策略**: `RetryCount` 步骤级重试（覆盖全局设置）

## 对应驱动插件

| 通信类型 | 插件 ID | 说明 |
|---------|---------|------|
| Serial / UART | `utf.driver.serial` | RS232/RS485 串口 |
| Telnet / TCP | `utf.driver.telnet` | 网络 Telnet 连接 |
| SCPI / LXI | `utf.driver.scpi` | 仪器控制（万用表/电源/示波器/频谱仪） |
| ADB / Android | `utf.driver.adb` | Android Debug Bridge |
| CMD / PowerShell | `utf.executor.cmd` | 本地命令行 |
