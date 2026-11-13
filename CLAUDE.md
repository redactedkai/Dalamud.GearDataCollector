# Dalamud.GearDataCollector

A read-only Dalamud (FFXIV, API 15) plugin. It records equipped gear and total stats in a duty, to check the sync model of the gear tool. Build and test steps are in `README.md`.

## Rules

- Read-only: no automated gameplay, no packets, no moving or equipping items, no setting changes.
- No network access and no telemetry.
- No hard-coded game offsets. Use Dalamud services and FFXIVClientStructs.
- No new dependencies without asking.
- StyleCop warnings are errors: one type per file, members in StyleCop order.
- Do not commit or push unless asked. Commits use the redactedkai profile, which Git selects from the remote URL.

## Commands

- Build: `DALAMUD_HOME=<Dalamud Hooks\dev folder> dotnet build -c Release` from the repo root.
- Tests: `cd tests/Dalamud.GearDataCollector.Tests && dotnet run`.

## Window design

For any change to the window layout, use the `dalamud-ui-mockup` skill (`.claude/skills/dalamud-ui-mockup/`). The mockup is `mockups/window-tabs.html`.

## Working with the owner

- Mark claims VERIFIED, INFERRED or UNKNOWN. Ask rather than guess.
- Keep answers short and plain.
