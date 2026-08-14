# Universal Test Framework 文档首页

欢迎来到 Universal Test Framework 文档中心。

本项目面向希望扩展自动化测试能力的工厂、产线团队和测试工程团队，支持 16 个以上 DUT 并行测试，并通过配置与插件持续扩展测试内容。

如果仓库启用 GitHub Pages 并选择 `docs/` 作为发布目录，本页可直接作为文档首页。

## 从哪里开始

### 工厂用户

- [工厂用户使用手册](factory-user-guide.md)
- [快速上手最小配置模板](../config/templates/factory-quick-start-minimal.json)
- [配置文件说明](../config/README.md)

### 开发者与集成人员

- [README](../README.md)
- [贡献指南](../CONTRIBUTING.md)
- [插件目录规范](../plugins/README.md)
- [测试说明](../tests/README.md)

## 文档导航

### 使用与接入

- [工厂用户使用手册](factory-user-guide.md)
- [UI 通用化使用说明（P0–P5）](ui-generalization-guide.md)
- [配置文件说明](../config/README.md)
- [模板库说明](../config/templates/README.md)
- [快速上手配置模板](../config/templates/factory-quick-start-minimal.json)
- [无头 CLI（utf-run）](../UTF.CLI/README.md)

### 架构与迁移

- [迁移指南](migration-guide.md)
- [架构优化报告](architecture-optimization-report.md)
- [UI 通用化使用说明](ui-generalization-guide.md)
- [完成度检查（历史快照，pre-2026-07）](completeness-check.md)
- [实现完成说明](implementation-complete.md)

### 协作与开源

- [贡献指南](../CONTRIBUTING.md)
- [项目主页](../README.md)
- [许可证](../LICENSE)

## 推荐阅读路径

### 第一次了解项目

1. 先看 [项目主页](../README.md)
2. 再看 [工厂用户使用手册](factory-user-guide.md)
3. 最后看 [快速上手配置模板](../config/templates/factory-quick-start-minimal.json)

### 第一次导入到工厂

1. 阅读 [工厂用户使用手册](factory-user-guide.md)
2. 用 UI **配置 → 选择工艺包/模板…** 应用 [快速上手最小配置模板](../config/templates/factory-quick-start-minimal.json)，或手动复制该文件
3. 对照 [配置文件说明](../config/README.md) 与 [UI 通用化说明](ui-generalization-guide.md) 修改产品信息、端点与测试步骤

### 第一次扩展插件或测试能力

1. 阅读 [插件目录规范](../plugins/README.md)
2. 阅读 [贡献指南](../CONTRIBUTING.md)
3. 根据当前能力在配置或插件层扩展测试内容

## 项目目标

- 帮助工厂扩展自动化测试能力
- 提升多 DUT 并行测试吞吐量
- 让新产品导入测试更快、更稳定
- 通过可重复、可配置、可扩展的流程保障产品质量

## 联系方式

- 工厂合作与方案交流：hongheshan@gmail.com
- 开源协作：欢迎提交 Issue 和 Pull Request
