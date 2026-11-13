# Calibration: mockup against the game

Measured from in-game screenshots on the owner's setup (Dalamud UI scale 1.25, default font 16 px). VERIFIED unless marked.

| Measure | Value | How it was found |
|---|---|---|
| Font size | 20 px | The gap from a right-aligned number to the next label is 49 px. The plugin makes that gap 2.4 font sizes (two cell paddings of 0.8 plus a number gap of 0.8), so 49 / 2.4 is about 20.4 (INFERRED). The windowing research gives 16 x 1.25 = 20. |
| Game font width | 0.865 of Segoe UI at the same size | Three strings measured: the yellow warning (720 px in game, 835 in Segoe at 20 px), the instructions line (731 / 847) and "Healing Magic Potency" (177 / 203). Ratios 0.862, 0.863, 0.872. |
| Cell padding | 0.8 font size between columns, none at the table edges | The first column starts at the same x as the heading text. |
| Table row pitch | 22 px | 20 px text plus 1 px cell padding above and below. |
| Window width | fits its content, minimum 46 font sizes | `AlwaysAutoResize` with a minimum size. |

## How the mockup uses them

- `.win` sets `--f: 20px` and `font-size: calc(var(--f) * 0.865)`, so text widths match the game font.
- Every gap and width is a multiple of `--f`: column gap 0.8, number gap 0.8, note box 35, Config box 30, status wrap 44, minimum width 46.
- `--label` (the widest label) and `--first` (the widest first value) are measured by `layout()` at the bottom of the file, like the plugin's `CalcTextSize` code.
- Letter shapes and colours still differ (Segoe UI against the game font, a blue title bar against the game's dark red).

## Remeasure when

The owner changes the UI scale or font, or sends screenshots that do not fit these numbers. Redo the gap method above and update this file and `--f`.
