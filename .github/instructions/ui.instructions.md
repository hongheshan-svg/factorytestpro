---
description: "Use when editing UTF.UI WPF windows, XAML, view models, converters, or UI services. Covers WPF responsibilities, startup wiring, and keeping business logic out of the UI layer."
name: "WPF UI Guidelines"
applyTo: "UTF.UI/**"
---

# WPF UI Guidelines

- Treat `UTF.UI` as the Windows desktop shell. Keep it focused on startup, dependency wiring, windows, binding, converters, and user interaction flow.
- Do not move reusable business, orchestration, validation, or plugin execution logic into the UI layer. Put that work in `UTF.Core` or `UTF.Business` and consume it through services.
- Follow the startup and DI composition patterns already used in `UTF.UI/App.xaml.cs` and `UTF.UI/DependencyInjection/ServiceCollectionExtensions.cs`.
- Prefer constructor injection for windows and services resolved from the container.
- Keep XAML code-behind thin. If logic is not strictly view-specific, move it into a service or view model instead of growing event handlers.
- Preserve Windows-only assumptions. `UTF.UI` targets `net10.0-windows` with WPF; do not introduce cross-platform UI abstractions unless the task explicitly requires them.
- When UI behavior depends on configuration or test execution, integrate through `IConfigurationService`, `IDUTMonitorService`, or other existing service abstractions rather than reading files or building orchestration directly in the window.
- If a UI change affects configuration structure, plugin packaging, or config-driven execution semantics, update the linked docs instead of duplicating guidance here.

## Reference Files

- `UTF.UI/App.xaml.cs`
- `UTF.UI/DependencyInjection/ServiceCollectionExtensions.cs`
- `UTF.UI/MainWindow.xaml.cs`
- `UTF.UI/Services/ConfigurationManager.cs`
