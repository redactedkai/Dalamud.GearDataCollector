using System;
using System.Collections.Generic;

using GearDataCollector.GameState.Items;
using GearDataCollector.Session.Lines;

namespace GearDataCollector.Session.Tracking
{
    /// <summary>
    /// What one session line says, kept in memory so the pieces can be worked out when the session stops.
    /// The first line is an entry with sequence number 0.
    /// </summary>
    internal sealed class ChangeEntry
    {
        /// <summary>Gets the sequence number of the line (0 for the first line).</summary>
        public required int Seq { get; init; }

        /// <summary>Gets the UTC time the line was written.</summary>
        public required DateTime TimestampUtc { get; init; }

        /// <summary>Gets the territory id the line carries.</summary>
        public required uint TerritoryId { get; init; }

        /// <summary>Gets the synced level the line carries.</summary>
        public required int SyncedLevel { get; init; }

        /// <summary>Gets a value indicating whether the line says the player was level synced.</summary>
        public required bool IsLevelSynced { get; init; }

        /// <summary>Gets a value indicating whether the equipped gear changed (false for a stats-only line and for the first line).</summary>
        public required bool IsGearChange { get; init; }

        /// <summary>Gets the pieces that left a slot.</summary>
        public required IReadOnlyList<SessionItem> Removed { get; init; }

        /// <summary>Gets the pieces that entered a slot.</summary>
        public required IReadOnlyList<SessionItem> Added { get; init; }

        /// <summary>Gets the total stats after the change, non-zero entries only.</summary>
        public required IReadOnlyList<StatValue> Totals { get; init; }
    }
}
