# 插件目录规范

## 目录结构
每个插件使用如下结构：

```text
plugins/
  <pluginId>/
    <version>/
      plugin.manifest.json
      <entryAssembly>.dll
      ...依赖文件
```

示例：

```text
plugins/utf.executor.cmd/1.0.0/plugin.manifest.json
```

## manifest 关键字段
- `pluginId`：插件唯一标识（如 `utf.executor.cmd`）
- `version`：插件版本（如 `1.0.0`，须为 `Major.Minor[.Build[.Revision]]` 形式）
- `pluginApiVersion`：当前固定 `1.0`
- `entryAssembly`：入口程序集（如 `UTF.Plugins.Example.dll`）。**必须是相对清单所在目录的路径，禁止包含 `..` 跨目录逃逸**，否则加载时抛出 `PLG001`。
- `entryType`：入口类型全名，必须实现 `IStepExecutorPlugin`，且须为带命名空间的全限定名。
- `priority`：非负整数，数值越小优先级越高（同类型+通道下取最低值）。
- `sha256`：**必填**。入口程序集的 SHA-256 哈希（大写十六进制字符串）。生产环境加载插件时会强制校验，缺失或不匹配抛出 `PLG002`。本地开发或测试场景若需加载未签名夹具，可设置环境变量 `UTFF_ALLOW_UNSIGNED_PLUGINS=1` 放行（仅限测试/开发，生产禁用）。

## 加载入口
运行时由 `UTF.Plugin.Host.StepExecutorPluginHost` 扫描并加载：
- 扫描路径：`<UTF.UI.exe目录>/plugins/<pluginId>/<version>/plugin.manifest.json`（仅扫描两层固定结构，不再递归任意深度）
- 载入顺序：按 `priority`（小优先）

## 打包方式
### 自动打包（推荐）
执行：

```powershell
dotnet build UniversalTestFramework.sln -c Debug
```

`UTF.UI.csproj` 会在构建后自动执行 `scripts/pack-plugins.ps1`：
- 扫描仓库 `plugins/**/plugin.manifest.json`
- 根据 `entryAssembly` 查找对应插件构建输出
- 复制到 `UTF.UI/bin/<Config>/net10.0-windows/plugins/...`

### 手动打包
可直接运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/pack-plugins.ps1 `
  -SolutionDir . `
  -OutDir UTF.UI/bin/Debug/net10.0-windows `
  -Configuration Debug
```
