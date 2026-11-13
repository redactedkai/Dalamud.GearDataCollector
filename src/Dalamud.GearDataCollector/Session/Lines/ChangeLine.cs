using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

using GearDataCollector.GameState.Items;

namespace GearDataCollector.Session.Lines
{
    /// <summary>
    /// What gear change lines and stats change lines have in common: where and in which sync state the change happened,
    /// and the total stats after it.
    /// </summary>
    internal abstract class ChangeLine : SessionLine
    {
        /// <summary>Gets the 1-based number of this change line within the session (both change kinds are counted together).</summary>
        public required int Seq { get; init; }

        /// <summary>Gets the territory id when the line was written.</summary>
        public required uint TerritoryId { get; init; }

        /// <summary>Gets the synced level when the line was written.</summary>
        public required int SyncedLevel { get; init; }

        /// <summary>Gets a value indicating whether the player was level synced when the line was written.</summary>
        public required bool IsLevelSynced { get; init; }

        /// <summary>
        /// Gets the UTC time the total stats last moved before this line was written (null when they did not move since
        /// the previous line, for example two pieces with identical stats swapped). Compare with the gear change time to
        /// see how long the game takes to update the stats.
        /// </summary>
        [JsonPropertyOrder(9)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DateTime? StatsChangedUtc { get; init; }

        /// <summary>Gets the total stats after the change, non-zero entries only.</summary>
        [JsonPropertyOrder(10)]
        public required IReadOnlyList<StatValue> Totals { get; init; }
    }
}
