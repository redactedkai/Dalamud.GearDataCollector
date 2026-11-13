using System.Collections.Generic;

using GearDataCollector.GameState.Items;

namespace GearDataCollector.Session.Lines
{
    /// <summary>
    /// A piece of gear named in a session line: which slot, which item (join key: <see cref="ItemId"/>), and its materia.
    /// </summary>
    internal sealed class SessionItem
    {
        /// <summary>Gets the slot position in the equipped container (same as <c>slot</c> in the first line's equipped list).</summary>
        public int Slot { get; init; }

        /// <summary>Gets the equipped slot name, for example "Head" or "FingerL".</summary>
        public string SlotName { get; init; } = string.Empty;

        /// <summary>Gets the Item row id.</summary>
        public uint ItemId { get; init; }

        /// <summary>Gets the item name.</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>Gets a value indicating whether this copy is high quality.</summary>
        public bool IsHq { get; init; }

        /// <summary>Gets the materia melded into this copy.</summary>
        public IReadOnlyList<MateriaRecord> Materia { get; init; } = new List<MateriaRecord>();
    }
}
