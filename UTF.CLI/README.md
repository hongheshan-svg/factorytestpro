# UTF.CLI (`utf-run`) — Headless runner

Phase C productization slice: run config-driven tests without the WPF UI.

## Build

```powershell
dotnet build UTF.CLI/UTF.CLI.csproj -c Debug
```

Binary: `UTF.CLI/bin/Debug/net10.0/utf-run.exe` (or `utf-run.dll` under `dotnet run`).

## Run

```powershell
# Help
dotnet run --project UTF.CLI -- --help

# Single DUT against repo config (MockOutput / plugins as available)
dotnet run --project UTF.CLI -- `
  --config config/unified-config.json `
  --dut-count 1 `
  --operator "line-1"

# Multi-DUT with explicit IDs and packed plugins from UTF.UI build
dotnet build UTF.UI/UTF.UI.csproj -c Debug
dotnet run --project UTF.CLI -- `
  --config config `
  --duts DUT-1,DUT-2,DUT-3 `
  --plugins UTF.UI/bin/Debug/net10.0-windows/plugins `
  --operator qa
```

## Plugins

`UTF.CLI` does **not** run `scripts/pack-plugins.ps1` on build (that target is on `UTF.UI`).

Options:

1. Point `--plugins` at `UTF.UI/bin/<Config>/net10.0-windows/plugins` after a UI build.
2. Copy a packed `plugins/` tree next to `utf-run.exe`.
3. Rely on `Parameters.MockOutput` in steps (no plugins required) for dry-run / CI.

Unsigned plugins require `UTFF_ALLOW_UNSIGNED_PLUGINS=1` (dev/test only).

## Exit codes

| Code | Meaning |
|------|---------|
| 0 | All DUTs passed |
| 1 | One or more DUTs failed / session stopped |
| 2 | Config or initialization error |

## Results

- Per-DUT JSON via `FileTestResultRepository` under `SystemSettings.ResultsPath` (default `./test-results`).
- Session summary JSON path is printed on stdout after the run.

## Phase C limitations

- Plugins are packed automatically on `dotnet build` of UTF.CLI (same script as UI).
- PDF reports are available via UTF.Reporting + QuestPDF when using the library (CLI prints session JSON summary).
- Vision remains simulated.
- Real serial/instrument I/O requires matching driver plugins under `--plugins`.
- UI MVVM migration is out of scope for this slice.
