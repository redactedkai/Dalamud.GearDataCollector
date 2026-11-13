using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;

using GearDataCollector.GameState.Duty;
using GearDataCollector.GameState.Items;

namespace GearDataCollector.Interface
{
    /// <summary>
    /// Draws a <see cref="DutyContext"/> in the window as tables: the duty, the player, the total stats, and the Duty Finder
    /// settings now beside those of the queue the duty popped from.
    /// </summary>
    internal static class DutyInfoView
    {
        // Gap between columns, in multiples of the font size.
        private const float ColumnGap = 0.8f;

        // Extra space after a right-aligned number, in multiples of the font size.
        private const float NumberGap = 0.8f;

        // The Total stats table has this many columns. It is filled down the columns: the first column holds the first
        // stats of StatOrder, the next column the following ones, and so on.
        private const int StatColumns = 3;

        // The order of the Total stats, as BaseParam ids, read down the columns. A stat that is not listed (another job's
        // stats, for example) comes after these, by id. A stat that is 0 is not shown.
        private static readonly uint[] StatOrder =
        {
            3, 1, 2, 4, 5, 19, 47,      // Vitality, Strength, Dexterity, Intelligence, Mind, Tenacity, Haste
            27, 44, 22, 6, 45, 46, 23,  // Critical Hit, Determination, Direct Hit Rate, Piety, Skill Speed, Spell Speed, Evasion
            14, 20, 33, 34, 21, 24,     // Auto-attack Delay, Attack Power, Attack Magic Potency, Healing Magic Potency, Defense, Magic Defense
        };

        // Label, the setting now, the same setting for the queue that popped (names as in DutyContext.FinderSettings).
        private static readonly (string Label, string Now, string Popped)[] FinderRows =
        {
            ("Unrestricted party", "IsUnrestrictedParty", "PoppedContentIsUnrestrictedParty"),
            ("Minimum IL", "IsMinimalIL", "PoppedContentIsMinimalIL"),
            ("Level sync", "IsLevelSync", "PoppedContentIsLevelSync"),
            ("Silence Echo", "IsSilenceEcho", "PoppedContentIsSilenceEcho"),
        };

        /// <summary>
        /// Draws the context.
        /// </summary>
        /// <param name="context">The duty context to show.</param>
        public static void Draw(DutyContext context)
        {
            // Outside a duty there is no sheet row, so its values show a dash. Inside one, 0 stays 0 (it means no cap).
            var inDuty = context.ContentFinderConditionId != 0;
            string FromSheet(string value) => inDuty ? value : "-";
            var duty = new[]
            {
                ("Duty", FromSheet(context.DutyName)),
                ("Territory", context.TerritoryId.ToString()),
                ("Duty ID", FromSheet(context.ContentFinderConditionId.ToString())),
                ("Minimum IL allowed", FromSheet(YesNo(context.AllowMinimumIL))),
                ("Level required", FromSheet(context.ClassJobLevelRequired.ToString())),
                ("Level sync", FromSheet(context.ClassJobLevelSync.ToString())),
                ("Item level required", FromSheet(context.ItemLevelRequired.ToString())),
                ("Item level sync", FromSheet(context.ItemLevelSync.ToString())),
                ("Fixed item level sync", FromSheet(YesNo(context.FixedItemLevelSync))),
            };
            var player = new[]
            {
                ("Job", $"{context.JobAbbreviation} (id {context.JobId})"),
                ("Level", context.RealLevel.ToString()),
                ("Synced to", context.IsLevelSynced ? context.SyncedLevel.ToString() : "not synced"),
                ("Average item level", context.CurrentItemLevel.ToString()),
                ("Minimum IL used", $"{(context.MinimumItemLevelOn ? "on" : "off")} ({context.MinimumItemLevelSource})"),
                ("Clan", $"{context.Clan} (tribe {context.TribeId})"),
            };
            var totals = StatsByColumn(context.AttributeTotals);

            // Every label column is as wide as the longest label anywhere, so labels and values line up in all the tables.
            var labelWidth = duty.Concat(player).Concat(totals).Select(pair => pair.Item1)
                .Concat(FinderRows.Select(row => row.Label))
                .Max(label => ImGui.CalcTextSize(label).X) + (ColumnGap * ImGui.GetFontSize());

            // The first value column is as wide as the widest first value in the Duty and Player tables, so their second
            // label columns line up too.
            var firstValueWidth = new[] { duty, player }
                .SelectMany(table => table.Where((pair, index) => index % 2 == 0))
                .Max(pair => ImGui.CalcTextSize(pair.Item2).X) + (ColumnGap * ImGui.GetFontSize());

            Heading("Duty");
            DrawPairs("duty", 2, false, labelWidth, firstValueWidth, duty);
            Heading("Player");
            DrawPairs("player", 2, false, labelWidth, firstValueWidth, player);
            Heading("Total stats");
            DrawPairs("totals", 3, true, labelWidth, 0, totals);

            Heading("Duty Finder");
            if (BeginTable("finder", 3))
            {
                ImGui.TableSetupColumn("Setting", ImGuiTableColumnFlags.WidthFixed, labelWidth);
                ImGui.TableSetupColumn("Now", ImGuiTableColumnFlags.WidthFixed, 4 * ImGui.GetFontSize());
                ImGui.TableSetupColumn("Popped queue", ImGuiTableColumnFlags.WidthFixed, 0);
                ImGui.TableHeadersRow();
                foreach (var (label, now, popped) in FinderRows)
                {
                    ImGui.TableNextColumn();
                    ImGui.TextDisabled(label);
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(Flag(context.FinderSettings, now));
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(Flag(context.FinderSettings, popped));
                }

                ImGui.EndTable();
            }

            ImGui.Spacing();
            ImGui.TextDisabled(
                $"Plugin {context.PluginVersion}, game {context.GameVersion}, game started {context.GameStartedUtc:yyyy-MM-dd HH:mm:ss} UTC");
        }

        // The stats in StatOrder, then any others by id, as the cells of a table filled row by row but ordered down the columns.
        // A column that runs short is the last one, so the missing cells are at the end of their rows.
        private static List<(string Label, string Value)> StatsByColumn(IReadOnlyList<StatValue> stats)
        {
            var ordered = StatOrder.Select(id => stats.FirstOrDefault(stat => stat.ParamId == id)).OfType<StatValue>()
                .Concat(stats.Where(stat => !StatOrder.Contains(stat.ParamId)).OrderBy(stat => stat.ParamId))
                .ToList();
            var rows = (ordered.Count + StatColumns - 1) / StatColumns;
            var cells = new List<(string Label, string Value)>();
            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < StatColumns; column++)
                {
                    var index = (column * rows) + row;
                    if (index < ordered.Count)
                    {
                        cells.Add((ordered[index].Name, ordered[index].Value.ToString()));
                    }
                }
            }

            return cells;
        }

        // A table of label and value pairs, several pairs per row. The label columns have the given width; a value column is
        // as wide as its widest value, except the first one when firstValueWidth is above 0. The label is dimmed, the value is
        // normal. Text values are left aligned, numbers right.
        private static void DrawPairs(string id, int pairsPerRow, bool numbers, float labelWidth, float firstValueWidth, IEnumerable<(string Label, string Value)> pairs)
        {
            if (!BeginTable(id, pairsPerRow * 2))
            {
                return;
            }

            for (var pair = 0; pair < pairsPerRow; pair++)
            {
                ImGui.TableSetupColumn("label", ImGuiTableColumnFlags.WidthFixed, labelWidth);
                ImGui.TableSetupColumn("value", ImGuiTableColumnFlags.WidthFixed, pair == 0 ? firstValueWidth : 0);
            }

            var index = 0;
            foreach (var (label, value) in pairs)
            {
                ImGui.TableNextColumn();
                ImGui.TextDisabled(label);
                ImGui.TableNextColumn();
                if (numbers)
                {
                    // The last column needs no space after it.
                    RightAligned(value, index % pairsPerRow == pairsPerRow - 1 ? 0 : NumberGap * ImGui.GetFontSize());
                    index++;
                }
                else
                {
                    ImGui.TextUnformatted(value);
                }
            }

            ImGui.EndTable();
        }

        // Starts a table with a small gap between columns and rows close together.
        private static bool BeginTable(string id, int columns)
        {
            ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(ColumnGap * ImGui.GetFontSize(), 1));
            var open = ImGui.BeginTable(id, columns, ImGuiTableFlags.SizingFixedFit);
            ImGui.PopStyleVar();
            return open;
        }

        // Draws text against the right edge of the current table cell, with extra space after it before the next column.
        private static void RightAligned(string text, float gap)
        {
            var free = ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(text).X - gap;
            if (free > 0)
            {
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + free);
            }

            ImGui.TextUnformatted(text);
            if (gap > 0)
            {
                ImGui.SameLine(0, 0);
                ImGui.Dummy(new Vector2(gap, 0));
            }
        }

        private static void Heading(string text)
        {
            ImGui.Spacing();
            ImGui.TextColored(new Vector4(0.55f, 0.75f, 1f, 1f), text);
            ImGui.Separator();
        }

        private static string YesNo(bool value) => value ? "yes" : "no";

        private static string Flag(IReadOnlyDictionary<string, bool> settings, string name) =>
            settings.TryGetValue(name, out var value) ? YesNo(value) : "unknown";
    }
}
