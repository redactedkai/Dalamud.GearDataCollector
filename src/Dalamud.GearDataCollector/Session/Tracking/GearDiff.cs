using System.Collections.Generic;

using GearDataCollector.GameState.Items;
using GearDataCollector.Session.Lines;

namespace GearDataCollector.Session.Tracking
{
    /// <summary>
    /// Works out which pieces left and which entered, for a gear change line. Pure logic: it reads no game memory and no file.
    /// </summary>
    internal static class GearDiff
    {
        /// <summary>
        /// Compares the recorded gear with the current gear, slot by slot, for the slots the detector marks as changed.
        /// A swap gives one entry in each list; a removal only a removed entry; equipping into an empty slot only an added entry.
        /// </summary>
        /// <param name="detector">Says which slots differ from the recorded state.</param>
        /// <param name="slotCount">How many slots to look at.</param>
        /// <param name="recorded">The pieces the file already says are equipped, by slot.</param>
        /// <param name="current">The pieces equipped now, by slot.</param>
        /// <returns>The pieces that left and the pieces that entered, in slot order.</returns>
        public static (List<SessionItem> Removed, List<SessionItem> Added) Compute(
            ChangeDetector detector,
            int slotCount,
            IReadOnlyDictionary<int, ItemRecord> recorded,
            IReadOnlyDictionary<int, ItemRecord> current)
        {
            var removed = new List<SessionItem>();
            var added = new List<SessionItem>();
            for (var slot = 0; slot < slotCount; slot++)
            {
                if (!detector.IsSlotChanged(slot))
                {
                    continue;
                }

                if (recorded.TryGetValue(slot, out var before))
                {
                    removed.Add(ToSessionItem(before));
                }

                if (current.TryGetValue(slot, out var after))
                {
                    added.Add(ToSessionItem(after));
                }
            }

            return (removed, added);
        }

        private static SessionItem ToSessionItem(ItemRecord item) => new()
        {
            Slot = item.Slot,
            SlotName = item.SlotName,
            ItemId = item.ItemId,
            Name = item.Name,
            IsHq = item.IsHq,
            Materia = item.Materia,
        };
    }
}
