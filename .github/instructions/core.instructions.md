---
description: "Use when editing UTF.Core, UTF.Configuration, or UTF.Plugin.Host. Covers config-driven execution, validation prefixes, template variables, plugin dispatch, and dependency injection boundaries."
name: "Core Config-Driven Guidelines"
applyTo:
  - "UTF.Core/**"
  - "UTF.Configuration/**"
  - "UTF.Plugin.Host/**"
---

# Core Config-Driven Guidelines

- Keep reusable execution, orchestration, validation, and configuration logic in `UTF.Core`, `UTF.Configuration`, or `UTF.Plugin.Host`, not in `UTF.UI`.
- Preserve the default execution path: `config/unified-config.json` -> `UTF.UI.Services.ConfigurationManager` -> `ConfigDrivenTestOrchestrator` -> `ConfigDrivenTestEngine`.
- Keep config-driven behavior backward compatible when changing step execution. Existing step fields include `TargetDeviceId`, `RetryCount`, `StoreResultAs`, and `ConditionExpression`.
- Preserve command and expected-value template substitution using both `{{key}}` and `${key}` syntaxes.
- Preserve result validation prefixes: `contains:`, `equals:`, `regex:`, `notcontains:`. New validation logic should extend this format rather than replace it.
- Plugin-based execution should continue to route through `IPluginService` and `StepExecutorPluginHost`. Respect manifest-based discovery under `plugins/<pluginId>/<version>/plugin.manifest.json`.
- Follow existing DI patterns in `UTF.Core/DependencyInjection/ServiceCollectionExtensions.cs` and `UTF.Configuration/ServiceCollectionExtensions.cs`. Prefer constructor injection and register abstractions where they already exist.
- Prefer updating detailed docs instead of expanding local explanations: `config/README.md`, `plugins/README.md`, `docs/migration-guide.md`.

## Reference Files

- `UTF.Core/ConfigDrivenTestEngine.cs`
- `UTF.Core/ConfigDrivenTestOrchestrator.cs`
- `UTF.Core/DependencyInjection/ServiceCollectionExtensions.cs`
- `UTF.Configuration/ServiceCollectionExtensions.cs`
- `UTF.Plugin.Host/StepExecutorPluginHost.cs`
