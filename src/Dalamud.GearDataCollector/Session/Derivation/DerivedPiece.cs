using System.Collections.Generic;

using GearDataCollector.GameState.Items;

namespace GearDataCollector.Session.Derivation
{
    /// <summary>
    /// A piece of gear and what it contributed to the total stats in the duty, as the game applied it (synced stats).
    /// </summary>
    internal sealed class DerivedPiece
    {
        /// <summary>Gets the slot position in the equipped container.</summary>
        public required int Slot { get; init; }

        /// <summary>Gets the equipped slot name, for example "Head" or "FingerL".</summary>
        public required string SlotName { get; init; }

        /// <summary>Gets the Item row id.</summary>
        public required uint ItemId { get; init; }

        /// <summary>Gets the item name.</summary>
        public required string Name { get; init; }

        /// <summary>Gets a value indicating whether this copy is high quality.</summary>
        public required bool IsHq { get; init; }

        /// <summary>Gets the materia melded into this copy.</summary>
        public required IReadOnlyList<MateriaRecord> Materia { get; init; }

        /// <summary>Gets what the piece added to the total stats, non-zero entries only.</summary>
        public required IReadOnlyList<StatValue> Stats { get; init; }

        /// <summary>Gets how many times the piece was measured (a piece removed and put back is measured twice).</summary>
        public required int Measurements { get; init; }
    }
}
