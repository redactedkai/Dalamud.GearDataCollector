using System;

using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

using GearDataCollector.GameState.Items;

namespace GearDataCollector.GameState
{
    /// <summary>
    /// Cheap reads of live game state (no strings, no sheet lookups), for the recorder's per-frame loop.
    /// Read-only: it only reads InventoryManager and PlayerState memory.
    /// Call it on the game's main thread (Draw and Framework.Update share it); not from a background task.
    /// </summary>
    internal static class StateReader
    {
        // BaseParam rows 7 to 11 are the resource pools (HP, MP, TP, GP, CP). Gear stat research does not need them,
        // so they are not read: they stay 0, are not compared and are not written to the file.
        private const int FirstResourceParamId = 7;
        private const int LastResourceParamId = 11;

        /// <summary>
        /// Fills <paramref name="into"/> with the state of each equipped slot (the default value for an empty slot).
        /// Returns false when the container is not ready or is empty (the game is still loading, which is not the same
        /// as taking everything off).
        /// </summary>
        /// <param name="into">One entry per slot, <see cref="ItemCollector.SlotCount"/> long.</param>
        /// <returns>True if the slots were read.</returns>
        public static unsafe bool TryReadSlots(SlotState[] into)
        {
            var manager = InventoryManager.Instance();
            if (manager == null)
            {
                return false;
            }

            var container = manager->GetInventoryContainer(InventoryType.EquippedItems);
            if (container == null || !container->IsLoaded)
            {
                return false;
            }

            var any = false;
            Span<uint> materia = stackalloc uint[5];
            for (var slot = 0; slot < ItemCollector.SlotCount; slot++)
            {
                var item = slot < container->Size ? container->GetInventorySlot(slot) : null;
                if (item == null || item->IsEmpty())
                {
                    into[slot] = default;
                    continue;
                }

                materia.Clear();
                for (byte index = 0; index < item->GetMateriaCount() && index < materia.Length; index++)
                {
                    materia[index] = ((uint)item->GetMateriaId(index) << 8) | item->GetMateriaGrade(index);
                }

                into[slot] = new SlotState(item->GetBaseItemId(), item->IsHighQuality(), materia[0], materia[1], materia[2], materia[3], materia[4]);
                any = true;
            }

            return any;
        }

        /// <summary>
        /// Copies PlayerState.Attributes into <paramref name="into"/>, to notice when the totals move. The array is
        /// created on the first call and reused; the attribute count does not change while the game runs.
        /// The resource pools (HP, MP, TP, GP, CP: BaseParam ids 7 to 11) are left at 0.
        /// </summary>
        /// <param name="into">The array to fill; replaced if it has the wrong length.</param>
        /// <returns>True if the totals were read.</returns>
        public static unsafe bool TryReadTotals(ref int[] into)
        {
            var player = PlayerState.Instance();
            if (player == null)
            {
                return false;
            }

            var count = player->Attributes.Length;
            if (into.Length != count)
            {
                into = new int[count];
            }

            for (var index = 0; index < count; index++)
            {
                into[index] = index >= FirstResourceParamId && index <= LastResourceParamId ? 0 : player->Attributes[index];
            }

            return true;
        }

        /// <summary>
        /// Reads the current class job id.
        /// </summary>
        /// <param name="jobId">The class job id.</param>
        /// <returns>False if the player state is not ready.</returns>
        public static unsafe bool TryReadJobId(out int jobId)
        {
            var player = PlayerState.Instance();
            if (player == null)
            {
                jobId = 0;
                return false;
            }

            jobId = player->CurrentClassJobId;
            return true;
        }

        /// <summary>
        /// Reads the synced level and the level sync flag.
        /// </summary>
        /// <param name="syncedLevel">The synced level the game reports.</param>
        /// <param name="isLevelSynced">Whether the player is level synced.</param>
        public static unsafe void ReadSyncState(out int syncedLevel, out bool isLevelSynced)
        {
            var player = PlayerState.Instance();
            if (player == null)
            {
                throw new InvalidOperationException("The game state is not ready yet.");
            }

            syncedLevel = player->SyncedLevel;
            isLevelSynced = player->IsLevelSynced;
        }
    }
}
