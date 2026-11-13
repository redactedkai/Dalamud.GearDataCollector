using System;
using System.Collections.Generic;

using FFXIVClientStructs.FFXIV.Client.Game;

using Lumina.Excel.Sheets;

namespace GearDataCollector.GameState.Items
{
    /// <summary>
    /// Reads the gear in the equipped container.
    /// Read-only: it only reads InventoryManager memory and Lumina sheets.
    /// Collect on the game's main thread (Draw and Framework.Update share it); not from a background task.
    /// </summary>
    internal static class ItemCollector
    {
        /// <summary>
        /// Names of the equipped-container slots, by slot index. Rings follow the game's gearset order (11 right, 12 left).
        /// </summary>
        private static readonly string[] ContainerSlotNames =
        {
            "MainHand", "OffHand", "Head", "Body", "Hands", "Waist", "Legs", "Feet",
            "Ears", "Neck", "Wrists", "FingerR", "FingerL", "SoulCrystal",
        };

        /// <summary>
        /// Gets the number of equipped-container slots the plugin reads. The recorder and the item reader both use it.
        /// </summary>
        public static int SlotCount => ContainerSlotNames.Length;

        /// <summary>
        /// Gets the name of an equipped-container slot.
        /// </summary>
        /// <param name="slot">The slot index in the equipped container.</param>
        /// <returns>The slot name.</returns>
        public static string ContainerSlotName(int slot) =>
            slot >= 0 && slot < ContainerSlotNames.Length ? ContainerSlotNames[slot] : $"Slot{slot}";

        /// <summary>
        /// Reads the equipped gear: every non-empty slot, the same slots <see cref="StateReader.TryReadSlots"/> compares
        /// each frame. An item missing from the Item sheet is still recorded (id, slot, quality and materia, with no sheet values),
        /// so the two readers never disagree about which slots hold gear.
        /// </summary>
        /// <returns>One record per equipped piece, in slot order.</returns>
        public static unsafe List<ItemRecord> Collect()
        {
            var records = new List<ItemRecord>();
            var manager = InventoryManager.Instance();
            if (manager == null)
            {
                return records;
            }

            var itemSheet = Service.DataManager.GetExcelSheet<Item>();
            var materiaSheet = Service.DataManager.GetExcelSheet<Materia>();

            var container = manager->GetInventoryContainer(InventoryType.EquippedItems);
            if (container == null || !container->IsLoaded)
            {
                return records;
            }

            for (var slot = 0; slot < Math.Min((int)container->Size, SlotCount); slot++)
            {
                var inventoryItem = container->GetInventorySlot(slot);
                if (inventoryItem == null || inventoryItem->IsEmpty())
                {
                    continue;
                }

                var itemId = inventoryItem->GetBaseItemId();
                var materia = new List<MateriaRecord>();
                for (byte index = 0; index < inventoryItem->GetMateriaCount(); index++)
                {
                    var materiaId = inventoryItem->GetMateriaId(index);
                    var grade = inventoryItem->GetMateriaGrade(index);
                    materia.Add(ReadMateria(materiaSheet, materiaId, grade));
                }

                var isHq = inventoryItem->IsHighQuality();
                records.Add(itemSheet.TryGetRow(itemId, out var item)
                    ? ReadItem(item, slot, isHq, materia)
                    : new ItemRecord { Slot = slot, SlotName = ContainerSlotName(slot), ItemId = itemId, IsHq = isHq, Materia = materia });
            }

            return records;
        }

        private static MateriaRecord ReadMateria(Lumina.Excel.ExcelSheet<Materia> sheet, uint materiaId, int grade)
        {
            if (!sheet.TryGetRow(materiaId, out var row))
            {
                return new MateriaRecord(materiaId, grade, 0, 0, string.Empty, 0);
            }

            var itemId = grade < row.Item.Count ? row.Item[grade].RowId : 0;
            var value = grade < row.Value.Count ? row.Value[grade] : (short)0;
            return new MateriaRecord(
                materiaId,
                grade,
                itemId,
                row.BaseParam.RowId,
                row.BaseParam.ValueNullable?.Name.ExtractText() ?? string.Empty,
                value);
        }

        private static ItemRecord ReadItem(Item item, int slot, bool isHq, List<MateriaRecord> materia)
        {
            var stats = new List<StatValue>();
            var hqBonusStats = new List<StatValue>();
            for (var index = 0; index < item.BaseParam.Count; index++)
            {
                AddStat(stats, item.BaseParam[index], item.BaseParamValue[index]);
                AddStat(hqBonusStats, item.BaseParamSpecial[index], item.BaseParamValueSpecial[index]);
            }

            return new ItemRecord
            {
                Slot = slot,
                SlotName = ContainerSlotName(slot),
                ItemId = item.RowId,
                Name = item.Name.ExtractText(),
                IsHq = isHq,
                LevelEquip = item.LevelEquip,
                ItemLevel = (int)item.LevelItem.RowId,
                SubStatCategory = item.SubStatCategory,
                DamagePhys = item.DamagePhys,
                DamageMag = item.DamageMag,
                DefensePhys = item.DefensePhys,
                DefenseMag = item.DefenseMag,
                BlockRate = item.BlockRate,
                Block = item.Block,
                BaseStats = stats,
                HqBonusStats = hqBonusStats,
                Materia = materia,
            };
        }

        private static void AddStat(List<StatValue> list, Lumina.Excel.RowRef<BaseParam> param, int value)
        {
            if (param.RowId == 0)
            {
                return;
            }

            list.Add(new StatValue(param.RowId, param.ValueNullable?.Name.ExtractText() ?? string.Empty, value));
        }
    }
}
