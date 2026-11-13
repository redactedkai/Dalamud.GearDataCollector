using System.Collections.Generic;

using GearDataCollector.GameState.Duty;
using GearDataCollector.GameState.Items;

namespace GearDataCollector.Session.Lines
{
    /// <summary>
    /// The first line: note, duty and character context, base stats, total stats and the equipped pieces.
    /// </summary>
    internal sealed class SessionStartLine : SessionLine
    {
        /// <summary>Gets the schema version of the file.</summary>
        public int SchemaVersion { get; init; } = SessionFormat.SchemaVersion;

        /// <summary>Gets the note typed in the window when the session started.</summary>
        public required string Note { get; init; }

        /// <summary>Gets the duty and character context.</summary>
        public required DutyContext Context { get; init; }

        /// <summary>Gets the territory id when the session started.</summary>
        public required uint TerritoryId { get; init; }

        /// <summary>Gets the synced level when the session started.</summary>
        public required int SyncedLevel { get; init; }

        /// <summary>Gets a value indicating whether the player was level synced when the session started.</summary>
        public required bool IsLevelSynced { get; init; }

        /// <summary>Gets the base stats PlayerState reports, raw (UNKNOWN whether the game syncs these down).</summary>
        public required IReadOnlyList<StatValue> BaseStats { get; init; }

        /// <summary>Gets the total stats now, non-zero entries only.</summary>
        public required IReadOnlyList<StatValue> Totals { get; init; }

        /// <summary>Gets the equipped pieces with their sheet data.</summary>
        public required IReadOnlyList<ItemRecord> Equipped { get; init; }
    }
}
