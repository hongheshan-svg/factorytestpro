---
description: "Use when editing docs, config, plugin manifests, or test planning markdown. Covers link-first documentation updates, avoiding duplicated guidance, and keeping docs aligned with implemented behavior."
name: "Documentation Guidelines"
applyTo:
  - "docs/**"
  - "config/**"
  - "plugins/**"
  - "tests/**"
---

# Documentation Guidelines

- Prefer link-first updates. If a topic already has a stable home, update that document and link to it instead of copying the same explanation into another file.
- Keep docs aligned with implemented behavior, not planned architecture. If a file mixes future plans and current state, make that distinction explicit.
- When changing configuration schema or step semantics, update `config/README.md`.
- When changing plugin layout, manifest requirements, or packaging flow, update `plugins/README.md`.
- When changing test scope or conventions, update `tests/README.md` and keep planned test projects clearly marked as planned.
- Use concise, task-oriented documentation. Avoid embedding large code samples unless they are the clearest way to explain a contract or file format.
- Prefer referencing authoritative source files such as `UTF.UI/App.xaml.cs`, `UTF.Core/ConfigDrivenTestEngine.cs`, or `UTF.Plugin.Host/StepExecutorPluginHost.cs` when documenting behavior.

## Reference Files

- `config/README.md`
- `plugins/README.md`
- `tests/README.md`
- `docs/migration-guide.md`
- `docs/completeness-check.md`
- `docs/implementation-complete.md`