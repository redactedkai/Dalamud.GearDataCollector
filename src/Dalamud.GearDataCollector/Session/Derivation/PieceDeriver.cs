using System;
using System.Collections.Generic;
using System.Linq;

using GearDataCollector.GameState.Items;
using GearDataCollector.Session.Lines;
using GearDataCollector.Session.Tracking;

namespace GearDataCollector.Session.Derivation
{
    /// <summary>
    /// Works out what each piece of gear contributes in the duty, from the lines of a session. Pure logic: it reads no game
    /// memory and no file. The total stats after each change are the game's own numbers, so the difference between two
    /// lines is exactly what the change did. Only a change of ONE piece is used: remove a piece and its stats are minus the
    /// difference; equip one and they are the difference (the base stats cancel). A swap counts as two pieces and is
    /// listed as skipped, so remove and equip separately.
    /// </summary>
    internal static class PieceDeriver
    {
        /// <summary>
        /// A <c>statsChange</c> line this many seconds after a gear change at most is the game's stat update arriving late,
        /// and is merged into that change.
        /// </summary>
        public const double MergeSeconds = 10;

        /// <summary>
        /// Derives the pieces.
        /// </summary>
        /// <param name="start">The first line of the session (sequence 0).</param>
        /// <param name="changes">The change lines, in the order they were written.</param>
        /// <returns>The measured pieces, the pieces measured twice with different results, and a note for each change that was not used.</returns>
        public static (List<DerivedPiece> Pieces, List<string> Contradictions, List<string> Skipped) Derive(ChangeEntry start, IReadOnlyList<ChangeEntry> changes)
        {
            var groups = GroupChanges(changes);
            var names = new Dictionary<uint, string>();
            foreach (var stat in start.Totals.Concat(changes.SelectMany(change => change.Totals)))
            {
                names[stat.ParamId] = stat.Name;
            }

            var pieces = new Dictionary<string, DerivedPiece>();
            var values = new Dictionary<string, Dictionary<uint, int>>();
            var contradictions = new List<string>();
            var skipped = new List<string>();
            var previous = ToMap(start.Totals);
            var previousInScope = true;
            foreach (var group in groups)
            {
                var totals = ToMap(group.Totals);
                var delta = Subtract(totals, previous);
                var inScope = group.Entries.All(entry => SameScope(entry, start));
                var valid = inScope && previousInScope;
                var pieceCount = group.Removed.Count + group.Added.Count;
                previous = totals;
                previousInScope = inScope;

                if (!group.IsGear)
                {
                    skipped.Add($"#{group.Seq}: the stats changed without a gear change (a buff or food?); not used");
                }
                else if (!inScope)
                {
                    skipped.Add($"#{group.Seq}: a different territory or sync state; not used");
                }
                else if (!valid)
                {
                    skipped.Add($"#{group.Seq}: follows a change that was not used, so the difference is not clean; not used");
                }
                else if (pieceCount != 1)
                {
                    skipped.Add($"#{group.Seq}: {pieceCount} pieces changed; not used (change one piece at a time)");
                }
                else
                {
                    AddMeasurement(group, delta, pieces, values, contradictions);
                }
            }

            var result = pieces.Select(pair => WithStats(pair.Value, values[pair.Key], names)).ToList();
            return (result, contradictions, skipped);
        }

        private static List<Group> GroupChanges(IReadOnlyList<ChangeEntry> changes)
        {
            var groups = new List<Group>();
            foreach (var entry in changes)
            {
                var last = groups.Count > 0 ? groups[^1] : null;
                if (!entry.IsGearChange && last != null && last.IsGear && (entry.TimestampUtc - last.LastUtc).TotalSeconds <= MergeSeconds)
                {
                    last.Merge(entry);
                }
                else
                {
                    groups.Add(new Group(entry));
                }
            }

            return groups;
        }

        private static void AddMeasurement(
            Group group,
            Dictionary<uint, int> delta,
            Dictionary<string, DerivedPiece> pieces,
            Dictionary<string, Dictionary<uint, int>> values,
            List<string> contradictions)
        {
            var item = group.Added.Count > 0 ? group.Added[0] : group.Removed[0];
            var sign = group.Added.Count > 0 ? 1 : -1;
            var measured = delta.Where(pair => pair.Value != 0).ToDictionary(pair => pair.Key, pair => sign * pair.Value);
            var key = KeyOf(item);
            if (!pieces.TryGetValue(key, out var existing))
            {
                pieces[key] = new DerivedPiece
                {
                    Slot = item.Slot,
                    SlotName = item.SlotName,
                    ItemId = item.ItemId,
                    Name = item.Name,
                    IsHq = item.IsHq,
                    Materia = item.Materia,
                    Stats = Array.Empty<StatValue>(),
                    Measurements = 1,
                };
                values[key] = measured;
                return;
            }

            pieces[key] = new DerivedPiece
            {
                Slot = existing.Slot,
                SlotName = existing.SlotName,
                ItemId = existing.ItemId,
                Name = existing.Name,
                IsHq = existing.IsHq,
                Materia = existing.Materia,
                Stats = existing.Stats,
                Measurements = existing.Measurements + 1,
            };
            if (!SameValues(values[key], measured))
            {
                contradictions.Add($"#{group.Seq}: {item.Name} was measured again and gave different stats");
            }
        }

        private static DerivedPiece WithStats(DerivedPiece piece, Dictionary<uint, int> stats, Dictionary<uint, string> names) => new()
        {
            Slot = piece.Slot,
            SlotName = piece.SlotName,
            ItemId = piece.ItemId,
            Name = piece.Name,
            IsHq = piece.IsHq,
            Materia = piece.Materia,
            Measurements = piece.Measurements,
            Stats = stats
                .OrderBy(pair => pair.Key)
                .Select(pair => new StatValue(pair.Key, names.GetValueOrDefault(pair.Key, string.Empty), pair.Value))
                .ToList(),
        };

        private static string KeyOf(SessionItem item) =>
            $"{item.ItemId}|{(item.IsHq ? 1 : 0)}|{string.Join(",", item.Materia.Select(materia => $"{materia.MateriaId}:{materia.Grade}"))}";

        private static bool SameScope(ChangeEntry entry, ChangeEntry start) =>
            entry.TerritoryId == start.TerritoryId && entry.SyncedLevel == start.SyncedLevel && entry.IsLevelSynced == start.IsLevelSynced;

        private static Dictionary<uint, int> ToMap(IReadOnlyList<StatValue> totals) => totals.ToDictionary(stat => stat.ParamId, stat => stat.Value);

        private static Dictionary<uint, int> Subtract(Dictionary<uint, int> after, Dictionary<uint, int> before)
        {
            var delta = new Dictionary<uint, int>();
            foreach (var paramId in after.Keys.Union(before.Keys))
            {
                delta[paramId] = after.GetValueOrDefault(paramId) - before.GetValueOrDefault(paramId);
            }

            return delta;
        }

        private static bool SameValues(Dictionary<uint, int> first, Dictionary<uint, int> second) =>
            first.Count == second.Count && first.All(pair => second.TryGetValue(pair.Key, out var value) && value == pair.Value);

        /// <summary>One gear change, with a late stats line merged in.</summary>
        private sealed class Group
        {
            public Group(ChangeEntry entry)
            {
                this.Entries.Add(entry);
                this.Seq = entry.Seq;
                this.IsGear = entry.IsGearChange;
                this.Removed = entry.Removed;
                this.Added = entry.Added;
                this.Totals = entry.Totals;
                this.LastUtc = entry.TimestampUtc;
            }

            public List<ChangeEntry> Entries { get; } = new();

            public int Seq { get; }

            public bool IsGear { get; }

            public IReadOnlyList<SessionItem> Removed { get; }

            public IReadOnlyList<SessionItem> Added { get; }

            public IReadOnlyList<StatValue> Totals { get; private set; }

            public DateTime LastUtc { get; private set; }

            public void Merge(ChangeEntry entry)
            {
                this.Entries.Add(entry);
                this.Totals = entry.Totals;
                this.LastUtc = entry.TimestampUtc;
            }
        }
    }
}
