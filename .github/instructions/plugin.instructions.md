---
description: "Use when editing UTF.Plugins.Example, UTF.Plugins.Drivers, or plugin manifests under plugins/. Covers plugin manifest fields, versioned plugin layout, host compatibility, and packaging expectations."
name: "Plugin Packaging Guidelines"
applyTo:
  - "UTF.Plugins.Example/**"
  - "UTF.Plugins.Drivers/**"
  - "plugins/**"
---

# Plugin Packaging Guidelines

- Keep runtime plugin layout compatible with `plugins/<pluginId>/<version>/plugin.manifest.json` plus the entry assembly and its dependencies in the same version folder.
- Preserve required manifest fields and semantics: `pluginId`, `version`, `pluginApiVersion`, `entryAssembly`, `entryType`, supported step/channel fields, and `priority` where ordering matters.
- Plugin implementations must remain compatible with `IStepExecutorPlugin` and with host loading through `UTF.Plugin.Host.StepExecutorPluginHost`.
- Do not hardcode assumptions that bypass manifest discovery, versioned directories, or host-based dispatch.
- When changing plugin packaging behavior, validate against the existing build path driven by `scripts/pack-plugins.ps1` and the post-build target in `UTF.UI/UTF.UI.csproj`.
- Prefer updating `plugins/README.md` when plugin manifest rules, folder layout, or packaging steps change rather than duplicating that detail in code comments.

## Reference Files

- `plugins/README.md`
- `UTF.Plugin.Host/StepExecutorPluginHost.cs`
- `UTF.UI/UTF.UI.csproj`
- `scripts/pack-plugins.ps1`
