# 配置文件使用说明

## 📁 配置文件列表

### 1. `unified-config.json` - QQ公仔当前使用的配置
**用途：** QQ公仔产品的生产测试配置文件  
**状态：** ✅ 当前使用中

**主要配置内容：**
- 产品信息：QQ公仔1.0
- 连接方式：串口 + 网络(Telnet)
- MAC地址范围：1C:78:39:09:F4:28 ~ 1C:78:39:09:FF:14
- 测试步骤：
  1. CMD命令测试
  2. 网络连接测试
  3. 软件版本检查
  4. MAC地址检查
  5. 音频功能测试

---

### 2. `unified-config.backup.json` - 完整配置备份
**用途：** 包含所有产品类别和功能的完整配置备份  
**状态：** 📦 备份文件

**包含内容：**
- 多种连接类型（USB、Serial、Network、GPIB、ADB）
- 多个产品类别（消费电子、智能手机、汽车电子）
- 仪器配置
- 机器视觉配置
- 多个测试模式（生产、开发、维护）

---

### 3. `dut-config-template.json` - 新产品配置模板
**用途：** 添加新产品时使用的配置模板  
**状态：** 📝 模板文件

---

## 🔧 如何为新产品创建配置

### 步骤 1：复制模板
```bash
copy config\dut-config-template.json config\unified-config-新产品名称.json
```

### 步骤 2：修改产品信息
编辑新文件中的 `DUTConfiguration.ProductInfo` 部分：
```json
"ProductInfo": {
  "Name": "新产品名称",
  "Model": "新产品型号 v1.0",
  "Icon": "🔧",  // 选择合适的图标
  "Category": "产品类别",
  "ExpectedSoftwareVersion": "期望的软件版本字符串"
}
```

### 步骤 3：配置连接方式
根据产品的通信接口，修改 `Connections` 部分：

**串口连接示例：**
```json
"Primary": {
  "Type": "Serial",
  "BaudRate": 115200,  // 根据产品修改
  "DataBits": 8,
  "StopBits": 1,
  "Parity": "None"
}
```

**网络连接示例：**
```json
"Secondary": {
  "Type": "Network",
  "Port": 23,
  "IPRange": "192.168.1.10-192.168.1.20"
}
```

### 步骤 4：配置MAC地址范围
修改 `MacRanges`：
```json
"MacRanges": [
  { "start": "AA:BB:CC:DD:EE:00", "end": "AA:BB:CC:DD:EE:FF" }
]
```

### 步骤 5：配置测试命令
修改 `TestCommands` 和 `ExpectedResponses`：
```json
"TestCommands": {
  "VersionCheck": "your_version_command",
  "MacCheck": "your_mac_command",
  "FunctionTest": "your_test_command"
},
"ExpectedResponses": {
  "Version": "expected_version_string",
  "MacPattern": "AA:BB:CC",
  "FunctionTest": "expected_result"
}
```

### 步骤 6：配置测试步骤
在 `TestProjectConfiguration.TestProject.Steps` 中添加测试步骤：
```json
{
  "Id": "step_001",
  "Name": "测试步骤名称",
  "Description": "测试步骤详细描述",
  "Order": 1,
  "Enabled": true,
  "Target": "dut",
  "Type": "serial",  // 可选：serial, custom, http等
  "Command": "your_test_command",
  "Expected": "contains:期望的响应内容",
  "Timeout": 5000,  // 超时时间（毫秒）
  "Delay": 500,     // 执行后延迟（毫秒）
  "Channel": "Serial"  // 通道：Serial, Cmd, Telnet等
}
```

### 步骤 7：启用新配置
将新配置文件重命名或复制为 `unified-config.json`：
```bash
copy config\unified-config-新产品名称.json config\unified-config.json
```

---

## 📋 测试步骤字段说明

| 字段 | 说明 | 示例 |
|------|------|------|
| `Id` | 步骤唯一标识 | "step_001" |
| `Name` | 步骤名称 | "软件版本检查" |
| `Description` | 步骤描述 | "通过串口检查DUT软件版本" |
| `Order` | 执行顺序 | 1 |
| `Enabled` | 是否启用 | true |
| `Target` | 目标设备 | "dut" 或 "instrument" |
| `Type` | 命令类型 | "serial", "custom", "http", "adb" |
| `TargetDeviceId` | 目标设备ID（插件/仪器） | "DMM_001" |
| `Command` | 执行的命令 | "system_manager version" |
| `Expected` | 期望结果 | "contains:SW_VERSION" |
| `Timeout` | 超时时间(ms) | 5000 |
| `Delay` | 执行后延迟(ms) | 500 |
| `RetryCount` | 失败后重试次数 | 2 |
| `Channel` | 通信通道 | "Serial", "Cmd", "Telnet" |
| `StoreResultAs` | 将本步骤输出存到上下文变量 | "measuredVoltage" |
| `ConditionExpression` | 条件表达式，不满足时自动跳过步骤 | "exists:measuredVoltage" |

---

## 🔍 期望结果格式

期望结果支持多种匹配模式：

| 格式 | 说明 | 示例 |
|------|------|------|
| `contains:xxx` | 包含指定字符串 | "contains:SW_VERSION:V1.0" |
| `equals:xxx` | 完全匹配 | "equals:OK" |
| `regex:xxx` | 正则表达式匹配 | "regex:V[0-9]+\\.[0-9]+" |
| `notcontains:xxx` | 不包含指定字符串 | "notcontains:ERROR" |

---

## 🚀 高级配置（推荐用于通用设备框架）

### 1) 变量模板（命令/期望值可引用上下文）
支持 `{{变量名}}` 或 `${变量名}` 两种写法。

```json
{
  "Id": "step_read_voltage",
  "Name": "读取电压",
  "Type": "instrument",
  "TargetDeviceId": "DMM_001",
  "Command": "MEASURE:VOLTAGE? CH{{channelIndex}}",
  "Expected": "contains:{{expectedTag}}",
  "StoreResultAs": "measuredVoltage"
}
```

### 2) 条件执行（按上下文决定是否执行）
支持格式：
- `exists:key`
- `notexists:key`
- `equals:key:value`
- `contains:key:value`

```json
{
  "Id": "step_validate_voltage",
  "Name": "电压范围校验",
  "Type": "custom",
  "ConditionExpression": "exists:measuredVoltage",
  "Command": "echo ${measuredVoltage}",
  "Expected": "notcontains:ERROR"
}
```

### 3) 扩展校验规则（ValidationRules）
除 `Expected` 外，还支持规则组合：

```json
{
  "ValidationRules": {
    "MustContainAll": ["PASS", "VOLTAGE"],
    "MustNotContainAny": ["ERROR", "FAIL"],
    "Regex": "[0-9]+\\.[0-9]+",
    "NumericRange": {
      "Min": 3.3,
      "Max": 4.2
    }
  }
}
```

### 4) 无插件调试模式（MockOutput）
当新设备插件尚未完成时，可先通过 `Parameters.MockOutput` 验证流程。

```json
{
  "Id": "step_mock_driver",
  "Name": "模拟设备响应",
  "Type": "serial",
  "Command": "any command",
  "Parameters": {
    "MockOutput": "VOLTAGE=3.78 PASS"
  },
  "Expected": "contains:PASS"
}
```

---

## 💡 常见配置示例

### QQ公仔配置（当前使用）
- **连接方式：** 串口(115200) + Telnet(端口23)
- **测试项目：** CMD测试 → 网络测试 → 版本检查 → MAC检查 → 音频测试
- **测试时长：** 约30-40秒/台

### 配置切换
```bash
# 切换到QQ公仔配置
copy config\unified-config-QQ公仔.json config\unified-config.json

# 切换到其他产品配置
copy config\unified-config-产品名称.json config\unified-config.json
```

---

## ⚠️ 注意事项

1. **修改前备份**  
   修改任何配置前，先备份当前配置文件

2. **JSON格式**  
   确保配置文件是有效的JSON格式，注意逗号和引号

3. **端口和IP配置**  
   根据实际硬件连接情况配置串口号和IP地址

4. **超时时间设置**  
   根据实际测试时间设置合理的超时值，避免误判

5. **MAC地址范围**  
   确保配置的MAC地址范围与实际生产批次一致

---

## 📞 技术支持

如有问题，请查阅：
- `通用自动化测试框架架构说明.md`
- `配置功能完成总结.md`
- `配置界面功能总结.md`

