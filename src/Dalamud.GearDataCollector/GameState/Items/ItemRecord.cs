using System.Collections.Generic;

namespace GearDataCollector.GameState.Items
{
    /// <summary>
    /// One equipped piece of gear, with its game data (Item sheet) values.
    /// All stats are the item's own, unsynced values.
    /// </summary>
    internal sealed class ItemRecord
    {
        /// <summary>Gets the slot position inside the equipped container, starting at 0.</summary>
        public int Slot { get; init; }

        /// <summary>Gets the equipped slot name, for example "Head" or "FingerL".</summary>
        public string SlotName { get; init; } = string.Empty;

        /// <summary>Gets the Item row id.</summary>
        public uint ItemId { get; init; }

        /// <summary>Gets the item name.</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>Gets a value indicating whether this copy is high quality.</summary>
        public bool IsHq { get; init; }

        /// <summary>Gets the equip level (the character level needed to wear it).</summary>
        public int LevelEquip { get; init; }

        /// <summary>Gets the item's own item level.</summary>
        public int ItemLevel { get; init; }

        /// <summary>Gets Item.SubStatCategory (2 means the gear scales with the wearer's level).</summary>
        public int SubStatCategory { get; init; }

        /// <summary>Gets the physical damage (weapons).</summary>
        public int DamagePhys { get; init; }

        /// <summary>Gets the magic damage (weapons).</summary>
        public int DamageMag { get; init; }

        /// <summary>Gets the physical defense.</summary>
        public int DefensePhys { get; init; }

        /// <summary>Gets the magic defense.</summary>
        public int DefenseMag { get; init; }

        /// <summary>Gets the block rate (shields).</summary>
        public int BlockRate { get; init; }

        /// <summary>Gets the block strength (shields).</summary>
        public int Block { get; init; }

        /// <summary>Gets the stats from the Item sheet (normal quality values).</summary>
        public IReadOnlyList<StatValue> BaseStats { get; init; } = new List<StatValue>();

        /// <summary>Gets the extra stats a high quality copy has (Item sheet "special" columns).</summary>
        public IReadOnlyList<StatValue> HqBonusStats { get; init; } = new List<StatValue>();

        /// <summary>Gets the materia melded into this copy.</summary>
        public IReadOnlyList<MateriaRecord> Materia { get; init; } = new List<MateriaRecord>();
    }
}
