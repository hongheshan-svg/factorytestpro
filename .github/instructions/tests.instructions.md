---
description: "Use when writing or updating tests under tests/. Covers xUnit naming, Arrange-Act-Assert structure, async tests, and patterns for engine, plugin host, and orchestration coverage."
name: "UTF Test Guidelines"
applyTo: "tests/**"
---

# UTF Test Guidelines

- Use xUnit and follow `Method_Scenario_ExpectedResult` naming.
- Structure tests with clear Arrange, Act, Assert phases. Keep setup local unless shared fixtures materially reduce duplication.
- Prefer focused unit tests around one behavior at a time. Add integration-style coverage only when interaction between orchestrator, plugin host, event bus, or persistence is the behavior under test.
- For async code, use async test methods and await the full behavior under test rather than forcing synchronous wrappers.
- When testing config-driven behavior, cover success, skip/condition paths, retry behavior, validation prefix behavior, and template substitution when the change affects those areas.
- When testing plugin host or plugin service behavior, verify discovery, dispatch, priority handling, and failure paths without coupling tests to packaged build outputs unless the scenario specifically requires packaging.
- Prefer existing test patterns and helpers from `tests/UTF.Core.Tests/ConfigDrivenTestEngineTests.cs` and neighboring test files before inventing a new style.
- Keep test changes aligned with current implemented test projects. Broader test project plans live in `tests/README.md` and should not be treated as already implemented.

## Reference Files

- `tests/UTF.Core.Tests/ConfigDrivenTestEngineTests.cs`
- `tests/UTF.Core.Tests/StepExecutorPluginHostTests.cs`
- `tests/UTF.Core.Tests/EventBusIntegrationTests.cs`
- `tests/README.md`