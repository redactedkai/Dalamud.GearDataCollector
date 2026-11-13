using System.Collections.Generic;

using GearDataCollector.GameState.Duty;

namespace GearDataCollector.Session.Derivation
{
    /// <summary>
    /// The file written when a session stops: what each piece of gear contributed to the total stats in the duty, worked
    /// out from the session's lines. It is one JSON object, saved next to the session file with the extension
    /// <c>.pieces.json</c>.
    /// </summary>
    internal sealed class PiecesFile
    {
        /// <summary>Gets the schema version of the session file this was worked out from.</summary>
        public int SchemaVersion { get; init; } = SessionFormat.SchemaVersion;

        /// <summary>Gets the name of the session file the pieces came from.</summary>
        public required string SessionFile { get; init; }

        /// <summary>Gets the territory id of the session.</summary>
        public required uint TerritoryId { get; init; }

        /// <summary>Gets the synced level of the session.</summary>
        public required int SyncedLevel { get; init; }

        /// <summary>Gets a value indicating whether the player was level synced.</summary>
        public required bool IsLevelSynced { get; init; }

        /// <summary>Gets the duty and character context from the first line of the session.</summary>
        public required DutyContext Context { get; init; }

        /// <summary>Gets the measured pieces and their stats in the duty.</summary>
        public required IReadOnlyList<DerivedPiece> Pieces { get; init; }

        /// <summary>Gets a note for each piece that was measured twice with different results.</summary>
        public required IReadOnlyList<string> Contradictions { get; init; }

        /// <summary>Gets a note for each change that was not used, and why.</summary>
        public required IReadOnlyList<string> Skipped { get; init; }
    }
}
