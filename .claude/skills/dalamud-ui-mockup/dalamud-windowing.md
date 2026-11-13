# Dalamud windowing facts (API 15)

Read from Dalamud source (master at 43b3411, version 15.0.3.9) and the goatcorp ImGui 1.88 docking fork. The installed Dalamud (15.0.4.0) has the same structure (INFERRED, not decompiled).

| Question | Answer | Status |
|---|---|---|
| Is `Size` scaled by `ImGuiHelpers.GlobalScale`? | Yes. `SizeConstraints` too (min and max). `Position` is not scaled. | VERIFIED |
| Does `ImGui.GetFontSize()` include the scale? | Yes. Dalamud scales no widths, paddings or style values for you. Use multiples of `GetFontSize()`. | VERIFIED |
| When does `Update()` run? | Every frame while the window is open, also when collapsed, before `DrawConditions`, `PreDraw` and `Begin`. | VERIFIED |
| Can `WindowName` change at runtime? | Yes. Text before `###` keeps the ImGui id, so the window keeps its position and state. Used for the "waiting..." title. | VERIFIED |
| `AlwaysAutoResize` and a minimum size? | The minimum is honoured after the auto-fit. The size lags the content by one frame. | VERIFIED (lag INFERRED from code) |
| Wrapped text in an auto-resize window | `TextWrapped` wraps at the current window width, so it never widens the window and is unreliable on frame 0. Set an explicit wrap position (`PushTextWrapPos(cursor + width * fontSize)`). | VERIFIED |
| Saved state | Position, size and collapsed state live in `dalamudUI.ini` (ImGui). Pin, click-through and opacity live in Dalamud's config. `AlwaysAutoResize` overwrites a saved size every frame. | VERIFIED |

## Pattern used in this plugin

- `Flags |= ImGuiWindowFlags.AlwaysAutoResize`, with no `Size`.
- `Update()` sets `SizeConstraints` (minimum width = `MinimumWidth * GetFontSize() / GlobalScale`, because constraints are multiplied by the scale again) and the title.
- Tables use `ImGuiTableFlags.SizingFixedFit` with explicit column widths from `CalcTextSize`.
- Right-aligned numbers: move the cursor by the free width in the cell.

## Watch for

- The user's `dalamudUI.ini` can hold `Collapsed=1`, so the window opens collapsed.
- A long text line can widen an auto-resize window. Wrap it at a fixed width.
