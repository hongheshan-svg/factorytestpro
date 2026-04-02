# 2026-03-10 系统稳定性测试设计

## 目标

本设计面向当前 `UniversalTestFramework.sln` 的整体系统稳定性验证，目标不是单次功能通过，而是回答以下问题：

1. 系统在长时间连续运行下是否会崩溃、卡死、资源泄漏或状态漂移。
2. 多 DUT 并发、设备反复上下线、插件异常、配置异常时，系统是否能保持可恢复。
3. 现有日志、异常处理和健康检查是否足以支撑问题定位。

## 当前仓库现状

基于 2026 年 3 月 10 日仓库检查，当前稳定性测试设计需适配以下现实：

- 解决方案包含 UI、业务、核心、日志、插件宿主、视觉和配置模块，但没有独立的正式测试工程。
- [UTF.UI/App.xaml.cs](/D:/toolsource/test-m-c#-yh/UTF.UI/App.xaml.cs) 已注册 `AppDomain.CurrentDomain.UnhandledException` 和 `DispatcherUnhandledException`，但目前主要是弹框，缺少统一稳定性指标输出。
- [UTF.Business/DeviceManager.cs](/D:/toolsource/test-m-c#-yh/UTF.Business/DeviceManager.cs) 已有设备健康检查定时器、设备注册/连接/断开逻辑，适合作为设备稳定性场景入口。
- [UTF.Core/OptimizedTestEngine.cs](/D:/toolsource/test-m-c#-yh/UTF.Core/OptimizedTestEngine.cs) 已有并发执行、取消、缓存使用等路径，适合作为并发和资源泄漏验证入口。
- [UTF.Plugin.Host/StepExecutorPluginHost.cs](/D:/toolsource/test-m-c#-yh/UTF.Plugin.Host/StepExecutorPluginHost.cs) 已有插件加载报告、超时控制、多插件冲突处理，适合作为故障注入点。
- [UTF.UI/Services/DUTMonitorManager.cs](/D:/toolsource/test-m-c#-yh/UTF.UI/Services/DUTMonitorManager.cs) 涉及动态列生成、配置读取、插件初始化和 DUT 运行态展示，是 UI 长稳和卡顿风险集中区。
- `config/unified-config.json` 与 `verify-migration.ps1` 已存在，可作为配置稳定性校验入口。

## 稳定性测试范围

本轮设计覆盖以下模块：

- UI 层：启动、配置加载、DUT 列表初始化、异常处理、长时间界面存活。
- Business 层：设备注册、连接、断开、健康检查、批量 DUT 管理。
- Core 层：测试引擎初始化、任务创建、并发执行、缓存使用、取消与关闭。
- Plugin 层：插件扫描、加载、冲突、执行超时、异常返回。
- Config 层：配置文件合法性、缺省值、坏配置恢复、配置切换。

本轮暂不覆盖：

- 真机硬件极限边界。
- 真实产线网络抖动模型的全量仿真。
- 视觉算法精度回归。

## 关键风险面

| 风险面 | 当前入口 | 主要失效模式 |
| --- | --- | --- |
| UI 长时间运行 | `App`、`MainWindow`、`DUTMonitorManager` | 卡死、内存持续增长、Dispatcher 异常被吞掉后状态失真 |
| 设备生命周期 | `DeviceManager` | 重复注册、连接状态漂移、断开后残留分配、健康检查线程异常 |
| 并发执行 | `OptimizedTestEngine` | 并发信号量阻塞、取消未释放、任务堆积 |
| 插件链路 | `StepExecutorPluginHost` | 插件扫描失败、重复匹配、执行超时、异常未隔离 |
| 配置链路 | `ConfigurationManager`、`ConfigurationAdapter` | 坏配置导致初始化失败、默认值回退不一致 |
| 进程资源 | 整体进程 | Working Set、Handle、Thread、日志体积持续增长 |

## 测试分层

### 1. 冒烟稳定性

目标是确认系统基础链路可重复启动和收尾，不发生明显异常。

场景：

- 连续启动和关闭应用 30 次。
- 连续加载统一配置 100 次。
- 连续初始化插件宿主 100 次。
- 连续创建和清理 DUT 监控项 100 次。

通过标准：

- 无未处理异常。
- 无启动失败。
- 每轮执行后线程数和句柄数回到稳定区间。

### 2. 长稳运行

目标是发现慢性资源泄漏、定时器失控、状态漂移和 UI 停顿。

场景：

- UI 空载运行 8 小时。
- 16 DUT 配置下持续轮询和状态刷新运行 8 小时。
- 插件宿主初始化后持续执行步骤调度 8 小时。
- 设备健康检查定时器持续触发运行 8 小时。

建议阈值：

- 未处理异常数 = 0。
- 进程崩溃数 = 0。
- 稳态后 Working Set 相对增长不超过 20%。
- Thread Count 不持续单调增长。
- Handle Count 不持续单调增长。
- UI 关键操作 p95 响应时间小于 500 ms。

### 3. 并发压力

目标是验证并发 DUT 和高频任务下的调度稳定性。

场景：

- 8、16、32 DUT 三档并发执行。
- 高频创建/取消测试任务。
- 同时执行设备连接、测试步骤、插件调用和配置读取。
- 多轮连续运行，每轮 500 到 1000 次步骤执行。

关注指标：

- 吞吐量。
- 平均/95 分位步骤耗时。
- 超时率。
- 取消完成率。
- 任务残留数。

### 4. 故障注入与恢复

目标是验证系统在异常条件下能否继续工作或安全失败。

场景：

- 插件缺失、插件 manifest 损坏、同优先级冲突插件并存。
- 配置文件缺字段、格式错误、字段类型错误。
- 设备连接失败、连接后立刻断开、健康检查失败。
- 测试步骤超时、取消、抛异常。
- UI 初始化中途抛异常。

通过标准：

- 错误可记录。
- 失败可定位到 DUT、步骤、插件或配置。
- 无无限重试和死循环。
- 失败后系统能继续处理其他 DUT 或允许人工恢复。

### 5. 资源泄漏专项

目标是定位长期运行后的内存、句柄、线程和日志膨胀。

场景：

- 反复创建/销毁 `StepExecutorPluginHost`。
- 反复注册/注销设备。
- 反复创建/停止测试任务。
- 反复刷新 DUT 列和 DataGrid 绑定。

采集指标：

- `WorkingSet64`
- `PrivateMemorySize64`
- `HandleCount`
- `Threads.Count`
- GC Collection Count
- 日志目录体积

## 场景矩阵

| 编号 | 场景 | 入口模块 | 运行时长/次数 | 主要判定 |
| --- | --- | --- | --- | --- |
| STAB-001 | 应用反复启动关闭 | UI | 30 次 | 无崩溃、无未处理异常 |
| STAB-002 | 配置反复加载 | UI/Config | 100 次 | 无坏状态残留 |
| STAB-003 | 插件宿主反复初始化 | Plugin | 100 次 | 加载报告稳定、无泄漏 |
| STAB-004 | 设备注册/连接/断开循环 | Business | 1000 次 | 状态一致、无分配残留 |
| STAB-005 | 健康检查长稳 | Business | 8 小时 | 定时器持续有效、无线程异常 |
| STAB-006 | 16 DUT 并发测试 | Core/UI | 8 小时 | 无吞吐骤降、无卡死 |
| STAB-007 | 32 DUT 压测 | Core | 2 小时 | 超时率受控、系统可回收 |
| STAB-008 | 坏插件与冲突插件 | Plugin | 50 轮 | 错误可隔离、不影响其他插件 |
| STAB-009 | 坏配置恢复 | Config/UI | 50 轮 | 启动失败可控、回退明确 |
| STAB-010 | 任务创建与取消风暴 | Core | 1000 次 | 无悬挂任务、无信号量死锁 |

## 可观测性基线

现有系统已有日志和部分异常处理，但不足以支撑长稳测试闭环。稳定性测试要求最少增加以下观测项：

### 必采集项

- 应用启动时间、主界面显示时间。
- 未处理异常计数。
- DUT 总数、运行数、失败数、超时数。
- 测试步骤平均耗时与 p95。
- 插件加载成功数、失败数、超时数。
- 设备连接成功率、重连次数、健康检查失败次数。
- 进程内存、线程、句柄、日志大小的时间序列。

### 建议采样频率

- 进程资源：每 60 秒。
- DUT 与步骤统计：每轮测试结束后。
- 插件错误：实时记录。
- UI 响应：每 5 分钟做一次操作探测。

### 当前缺口

- `App.xaml.cs` 只有弹框，未把未处理异常归档到统一稳定性报告。
- `DUTMonitorManager` 大量使用 `Debug.WriteLine`，不利于长稳回溯。
- 仓库缺少专门的稳定性测试工程、结果汇总器和基线比较脚本。

## 建议的仓库落地结构

建议按以下结构逐步补齐，而不是直接把所有稳定性逻辑塞进 UI 项目：

```text
tests/
  UTF.Core.Tests/
  UTF.Business.Tests/
  UTF.Plugin.Host.Tests/
  UTF.SystemStability.Runner/
docs/
  plans/
    2026-03-10-system-stability-test-design.md
scripts/
  run-stability-baseline.ps1
  collect-process-metrics.ps1
```

说明：

- `UTF.Core.Tests`：做引擎并发、取消、缓存和任务生命周期测试。
- `UTF.Business.Tests`：做设备注册、连接、健康检查和恢复测试。
- `UTF.Plugin.Host.Tests`：做插件加载、冲突、超时和坏 manifest 测试。
- `UTF.SystemStability.Runner`：做长稳、压测、故障注入与结果汇总。

## 分阶段实施建议

### 阶段 1：先建立基线

1. 增加测试目录和稳定性场景编号。
2. 建立统一结果格式，至少输出 `json` 或 `csv`。
3. 先跑冒烟稳定性，确认系统可重复初始化和退出。

### 阶段 2：补长稳与压力

1. 增加进程资源采集。
2. 增加 16 DUT 长稳和 32 DUT 压测。
3. 对插件、配置、设备断连引入故障注入。

### 阶段 3：收敛准入标准

1. 固化资源阈值。
2. 固化回归门禁。
3. 将稳定性报告纳入发布前验证。

## 本轮建议的首批落地任务

优先级从高到低如下：

1. 创建 `tests/` 目录并定义测试工程边界。
2. 为 `PluginHost`、`DeviceManager`、`OptimizedTestEngine` 补第一批可自动化稳定性测试。
3. 增加进程级指标采集脚本。
4. 把 UI 未处理异常和关键状态切换写入统一日志。
5. 将 `verify-migration.ps1` 纳入稳定性冒烟链路。

## 验收标准

当以下条件满足时，可认为第一阶段“系统稳定性设计”完成并具备实施基础：

- 仓库内存在明确的稳定性设计文档。
- 已定义分层、场景、阈值、指标和实施顺序。
- 已明确当前代码中的观测缺口和后续改造点。
- 已预留测试目录入口，后续可以直接补测试工程和脚本。

