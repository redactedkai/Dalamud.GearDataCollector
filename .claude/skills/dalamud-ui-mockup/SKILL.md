---
name: dalamud-ui-mockup
description: Design and change a Dalamud (ImGui) plugin window with an HTML mockup that matches the game. Use for any change to the plugin's window layout, sizes, tables, text or colours, and when comparing an in-game screenshot with the mockup.
---

# Dalamud window design and mockup loop

The plugin code is the source of truth. The mockup (`mockups/window-tabs.html`) previews it and should match the game within a few percent.

## The loop

1. Change the plugin code first when the question is about the game (a bug, a size, a column). Change the mockup first when the question is about a new design.
2. Edit `mockups/window-tabs.html`.
3. Render it: `powershell -File .claude/skills/dalamud-ui-mockup/render-mockup.ps1 -Tab info`. It writes a PNG to `scratch/shots/` and never opens a browser window. Read the PNG.
4. Compare with the user's in-game screenshot, area by area. Mark each difference VERIFIED (visible in both images), INFERRED (a guess at the cause) or UNKNOWN.
5. When a difference exists, find the cause in the plugin code before changing the mockup. The mockup can look right while the code is wrong (the Duty table's first value column was sized from the wrong values).
6. Build the plugin (`dotnet build -c Release`, see README) and copy it only when the user asks.

## Rules

- Never open the mockup in the user's browser (no `Start-Process` on the HTML). Render headlessly.
- Explain a proposed layout fix and its options before applying UI changes. Apply after the user picks.
- Keep the plugin read-only: no input, packets, equipping or settings changes. No network access. No hard-coded game offsets. No new dependencies without asking.
- Write docs short and plain. State what is true, not what is missing.
- Compare data with data: the same job, duty and digit counts. Four-digit stats make columns wider than three-digit ones.

## Sizes

All sizes are multiples of the font size, never pixels. See `calibration.md` for the measured numbers and how the mockup reproduces them. Dalamud's windowing facts are in `dalamud-windowing.md`.

## Layout pieces in this project

- `MainWindow.cs`: the window, its tabs, the session note row, status text and the title.
- `DutyInfoView.cs`: the tables (Duty, Player, Total stats, Duty Finder) and their column widths.
- The mockup computes column widths with a small script at the bottom of the HTML, like the plugin does with `CalcTextSize`. Keep the two in step.

## Checks before saying a UI change is done

- Build has 0 errors and 0 warnings (StyleCop treats warnings as errors).
- Tests pass (`cd tests/Dalamud.GearDataCollector.Tests && dotnet run`).
- Render the mockup and compare it with the newest in-game screenshot.
- The user's next in-game screenshot is the final check. Say so.
