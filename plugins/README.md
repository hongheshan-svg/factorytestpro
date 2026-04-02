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
- `version`：插件版本（如 `1.0.0`）
- `pluginApiVersion`：当前固定 `1.0`
- `entryAssembly`：入口程序集（如 `UTF.Plugins.Example.dll`）
- `entryType`：入口类型全名，必须实现 `IStepExecutorPlugin`

## 加载入口
运行时由 `UTF.Plugin.Host.StepExecutorPluginHost` 扫描并加载：
- 扫描路径：`<UTF.UI.exe目录>/plugins/**/plugin.manifest.json`
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
