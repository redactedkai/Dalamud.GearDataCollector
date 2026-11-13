using GearDataCollector.GameState.Duty;
using GearDataCollector.GameState.Items;
using GearDataCollector.Session;
using GearDataCollector.Session.Derivation;
using GearDataCollector.Session.Lines;
using GearDataCollector.Session.Tracking;

// Checks for ChangeDetector. Times are tick counts in milliseconds; the stable time is 1000 ms, as in the plugin.
// Exit code 0 when every check passes, 1 otherwise.
const long Stable = 1000;
const int Slots = 4;

var failures = 0;

SlotState Item(uint id) => new(id, false, 0, 0, 0, 0, 0);

SlotState[] Gear(params uint[] ids)
{
    var gear = new SlotState[Slots];
    for (var slot = 0; slot < ids.Length; slot++)
    {
        gear[slot] = ids[slot] == 0 ? default : Item(ids[slot]);
    }

    return gear;
}

// Index 0 of the attributes is not a stat. Index 1 is the first stat, 2 the second, and so on.
int[] Totals(int strength, int dexterity) => new[] { 0, strength, dexterity, 0 };

void Check(string name, bool condition)
{
    if (!condition)
    {
        failures++;
    }

    Console.WriteLine($"{(condition ? "pass" : "FAIL")}  {name}");
}

ChangeDetector Started(SlotState[] gear, int[] totals)
{
    var detector = new ChangeDetector(Slots, Stable);
    detector.Begin(gear, totals, 0);
    return detector;
}

var start = Gear(10, 20, 30, 0);
var startTotals = Totals(100, 200);

// 1. Nothing changes: a line is never due.
{
    var detector = Started(start, startTotals);
    var due = false;
    for (long now = 0; now <= 5000; now += 100)
    {
        due |= detector.Update(start, startTotals, now);
    }

    Check("1  nothing changes: never due", !due);
}

// 2. Gear changes at 1000: not due at 1999, due at 2000, and it is a gear line.
{
    var detector = Started(start, startTotals);
    var swapped = Gear(11, 20, 30, 0);
    Check("2a gear change is not due the frame it moves", !detector.Update(swapped, startTotals, 1000));
    Check("2b gear change is not due at 1999", !detector.Update(swapped, startTotals, 1999));
    Check("2c gear change is due at 2000", detector.Update(swapped, startTotals, 2000));
    Check("2d it is a gear line", detector.GearChanged && !detector.TotalsChanged);
}

// 3. Totals move 400 ms after the gear: the wait restarts, one line is due, and it has both changes.
{
    var detector = Started(start, startTotals);
    var swapped = Gear(11, 20, 30, 0);
    var newTotals = Totals(150, 200);
    detector.Update(swapped, startTotals, 100);
    detector.Update(swapped, newTotals, 500);
    Check("3a wait restarted by the late totals", !detector.Update(swapped, newTotals, 1400));
    Check("3b due 1000 ms after the totals moved", detector.Update(swapped, newTotals, 1500));
    Check("3c the line carries gear and totals", detector.GearChanged && detector.TotalsChanged);
}

// 4. Totals change alone: a stats-only line.
{
    var detector = Started(start, startTotals);
    var newTotals = Totals(100, 210);
    detector.Update(start, newTotals, 100);
    Check("4a stats-only change is due after the wait", detector.Update(start, newTotals, 1100));
    Check("4b it is not a gear line", !detector.GearChanged && detector.TotalsChanged);
}

// 5. After Commit, the same state is never due again.
{
    var detector = Started(start, startTotals);
    var swapped = Gear(11, 20, 30, 0);
    var newTotals = Totals(150, 200);
    detector.Update(swapped, newTotals, 100);
    var due = detector.Update(swapped, newTotals, 1100);
    detector.Commit();
    var dueAgain = false;
    for (long now = 1200; now <= 6000; now += 100)
    {
        dueAgain |= detector.Update(swapped, newTotals, now);
    }

    Check("5a the line was due", due);
    Check("5b nothing is due after Commit", !dueAgain && !detector.GearChanged && !detector.TotalsChanged);
}

// 6. Gear changes and returns to the recorded state before it settles: nothing is due.
{
    var detector = Started(start, startTotals);
    var swapped = Gear(11, 20, 30, 0);
    var due = detector.Update(swapped, startTotals, 100);
    var movedAway = detector.GearMoved;
    due |= detector.Update(start, startTotals, 500);
    var movedBack = detector.GearMoved;
    for (long now = 600; now <= 5000; now += 100)
    {
        due |= detector.Update(start, startTotals, now);
    }

    Check("6a the detector saw both moves", movedAway && movedBack);
    Check("6b nothing is due", !due);
}

// 7. A change is committed, then the gear goes back: a line is due and only the swapped slot is marked.
{
    var detector = Started(start, startTotals);
    var swapped = Gear(11, 20, 30, 0);
    detector.Update(swapped, startTotals, 100);
    detector.Update(swapped, startTotals, 1100);
    detector.Commit();
    detector.Update(start, startTotals, 2000);
    Check("7a going back is due after the wait", detector.Update(start, startTotals, 3000));
    Check(
        "7b only slot 0 is marked",
        detector.IsSlotChanged(0) && !detector.IsSlotChanged(1) && !detector.IsSlotChanged(2) && !detector.IsSlotChanged(3));
}

// 8. Two slots change one after the other inside the window: one line, both slots marked.
{
    var detector = Started(start, startTotals);
    detector.Update(Gear(11, 20, 30, 0), startTotals, 100);
    var both = Gear(11, 21, 30, 0);
    detector.Update(both, startTotals, 600);
    Check("8a not due 999 ms after the second slot", !detector.Update(both, startTotals, 1599));
    Check("8b due 1000 ms after the second slot", detector.Update(both, startTotals, 1600));
    Check("8c both slots are marked", detector.IsSlotChanged(0) && detector.IsSlotChanged(1) && !detector.IsSlotChanged(2));
}

// 9. Equipping into an empty slot and a change of materia both count; index 0 of the totals does not.
{
    var detector = Started(start, startTotals);
    var filled = Gear(10, 20, 30, 40);
    detector.Update(filled, startTotals, 100);
    Check("9a equipping into an empty slot is due", detector.Update(filled, startTotals, 1100) && detector.IsSlotChanged(3));

    detector = Started(start, startTotals);
    var melded = (SlotState[])start.Clone();
    melded[1] = melded[1] with { Materia0 = (5u << 8) | 1 };
    detector.Update(melded, startTotals, 100);
    Check("9b a materia change is due", detector.Update(melded, startTotals, 1100) && detector.IsSlotChanged(1));

    detector = Started(start, startTotals);
    var noise = new[] { 99, 100, 200, 0 };
    var due = false;
    for (long now = 100; now <= 5000; now += 100)
    {
        due |= detector.Update(start, noise, now);
    }

    Check("9c a change in totals[0] is ignored", !due && !detector.TotalsMoved);
}

// 10. The moved flags are true only on the frame the state moved.
{
    var detector = Started(start, startTotals);
    var swapped = Gear(11, 20, 30, 0);
    detector.Update(swapped, startTotals, 100);
    var onFrame = detector.GearMoved && !detector.TotalsMoved;
    detector.Update(swapped, startTotals, 200);
    Check("10 GearMoved is true only on the frame the gear moved", onFrame && !detector.GearMoved);
}

// GearDiff: which pieces left and which entered, from the detector's changed slots and the item records.
ItemRecord Record(int slot, uint id, MateriaRecord? materia = null) => new()
{
    Slot = slot,
    SlotName = $"Slot{slot}",
    ItemId = id,
    Name = $"Item{id}",
    Materia = materia == null ? new List<MateriaRecord>() : new List<MateriaRecord> { materia },
};

Dictionary<int, ItemRecord> Records(SlotState[] gear)
{
    var records = new Dictionary<int, ItemRecord>();
    for (var slot = 0; slot < gear.Length; slot++)
    {
        if (gear[slot].ItemId != 0)
        {
            records[slot] = Record(slot, gear[slot].ItemId);
        }
    }

    return records;
}

ChangeDetector Changed(SlotState[] from, SlotState[] to)
{
    var detector = Started(from, startTotals);
    detector.Update(to, startTotals, 100);
    return detector;
}

// 11. A swap: one entry in each list, only for the swapped slot.
{
    var now = Gear(11, 20, 30, 0);
    var (removed, added) = GearDiff.Compute(Changed(start, now), Slots, Records(start), Records(now));
    Check(
        "11 swap: slot 0 removed 10 and added 11, nothing else",
        removed.Count == 1 && added.Count == 1 && removed[0].ItemId == 10 && added[0].ItemId == 11 && removed[0].Slot == 0 && added[0].Slot == 0);
}

// 12. A removal gives only a removed entry; equipping into an empty slot gives only an added entry.
{
    var taken = Gear(10, 20, 0, 0);
    var (removed, added) = GearDiff.Compute(Changed(start, taken), Slots, Records(start), Records(taken));
    Check("12a removal: only a removed entry", removed.Count == 1 && added.Count == 0 && removed[0].ItemId == 30);

    var filled = Gear(10, 20, 30, 40);
    (removed, added) = GearDiff.Compute(Changed(start, filled), Slots, Records(start), Records(filled));
    Check("12b equipping into an empty slot: only an added entry", removed.Count == 0 && added.Count == 1 && added[0].ItemId == 40);
}

// 13. Several slots at once come out in slot order.
{
    var now = Gear(11, 0, 31, 40);
    var (removed, added) = GearDiff.Compute(Changed(start, now), Slots, Records(start), Records(now));
    Check(
        "13 several slots: slot order, unchanged slots left out",
        removed.Select(item => item.Slot).SequenceEqual(new[] { 0, 1, 2 }) && added.Select(item => item.Slot).SequenceEqual(new[] { 0, 2, 3 }));
}

// 14. A materia-only change is a removed and an added entry for the same item, with the materia on each.
{
    var meld = new MateriaRecord(5, 1, 0, 0, string.Empty, 0);
    var melded = (SlotState[])start.Clone();
    melded[1] = melded[1] with { Materia0 = (5u << 8) | 1 };
    var before = Records(start);
    var after = Records(melded);
    after[1] = Record(1, 20, meld);
    var (removed, added) = GearDiff.Compute(Changed(start, melded), Slots, before, after);
    Check(
        "14 materia change: same item on both sides, materia only on the added one",
        removed.Count == 1 && added.Count == 1 && removed[0].ItemId == 20 && added[0].ItemId == 20 && removed[0].Materia.Count == 0 && added[0].Materia.Count == 1);
}

// 15. A changed slot with no record on either side (a slot the item reader did not return) gives no entry and no crash.
{
    var now = Gear(11, 20, 30, 0);
    var (removed, added) = GearDiff.Compute(Changed(start, now), Slots, new Dictionary<int, ItemRecord>(), new Dictionary<int, ItemRecord>());
    Check("15 no record on either side: empty lists", removed.Count == 0 && added.Count == 0);
}

// DutyFlagMemory: the Minimum Item Level flag is taken when the plugin sees you enter a duty and kept until you leave it.
const uint Overworld = 100;
const uint DutyTerritory = 1066;
const uint DutyCondition = 800;

// 16. Entering a duty while the plugin runs: the flag is taken at entry (cfc is 0 for the first frames), and the memory wins over the game later.
{
    var memory = new DutyFlagMemory(null);
    memory.Observe(Overworld, 0, false);
    var zoning = memory.Observe(DutyTerritory, 0, true);
    var entered = memory.Observe(DutyTerritory, DutyCondition, true);
    var again = memory.Observe(DutyTerritory, DutyCondition, true);
    var (flag, source) = memory.Resolve(DutyTerritory, DutyCondition, false);
    Check("16a nothing is saved while the duty id is still 0", !zoning);
    Check("16b the flag is saved once the duty id appears, and not again", entered && !again);
    Check("16c the remembered flag wins over the game's value", flag && source == DutyFlagMemory.SourceAtEntry);
}

// 17. A game restart inside the duty: the saved flag is kept, even though the game now says false.
{
    var saved = new DutyFlagRecord(DutyTerritory, DutyCondition, true, DutyFlagMemory.SourceAtEntry);
    var memory = new DutyFlagMemory(saved);
    memory.Observe(DutyTerritory, 0, false);
    var changed = memory.Observe(DutyTerritory, DutyCondition, false);
    var (flag, source) = memory.Resolve(DutyTerritory, DutyCondition, false);
    Check("17a the saved record is kept", !changed && memory.Record == saved);
    Check("17b the flag is the saved one and marked as restored", flag && source == DutyFlagMemory.SourceSavedAtEntry);
}

// 18. A restart that ends outside a duty, or in another territory: the saved record is dropped.
{
    var saved = new DutyFlagRecord(DutyTerritory, DutyCondition, true, DutyFlagMemory.SourceAtEntry);
    var memory = new DutyFlagMemory(saved);
    var changed = memory.Observe(Overworld, 0, false);
    var (flag, source) = memory.Resolve(Overworld, 0, false);
    Check("18a the saved record is dropped", changed && memory.Record == null);
    Check("18b outside a duty the game's value is used", !flag && source == DutyFlagMemory.SourceGameMemory);
}

// 19. A restart in a duty with nothing saved: the game's value is used and marked as unverified.
{
    var memory = new DutyFlagMemory(null);
    memory.Observe(DutyTerritory, DutyCondition, true);
    var (flag, source) = memory.Resolve(DutyTerritory, DutyCondition, true);
    Check("19 no saved record: the game value, marked gameMemory", flag && source == DutyFlagMemory.SourceGameMemory);
}

// 20. Leaving the duty clears the record; entering the same duty again takes the new flag.
{
    var memory = new DutyFlagMemory(null);
    memory.Observe(Overworld, 0, false);
    memory.Observe(DutyTerritory, DutyCondition, true);
    var left = memory.Observe(Overworld, 0, false);
    var cleared = memory.Record == null;
    memory.Observe(DutyTerritory, DutyCondition, false);
    var (flag, source) = memory.Resolve(DutyTerritory, DutyCondition, true);
    Check("20a leaving the duty clears the record", left && cleared);
    Check("20b entering again takes the new flag", !flag && source == DutyFlagMemory.SourceAtEntry);
}

// 21. A saved record for another duty in the same territory is replaced, and a record is never used for a different duty.
{
    var saved = new DutyFlagRecord(DutyTerritory, DutyCondition, true, DutyFlagMemory.SourceAtEntry);
    var memory = new DutyFlagMemory(saved);
    memory.Observe(DutyTerritory, DutyCondition + 1, false);
    var (flag, source) = memory.Resolve(DutyTerritory, DutyCondition + 1, false);
    var (otherFlag, otherSource) = memory.Resolve(DutyTerritory, DutyCondition, true);
    Check("21a the old record is replaced", !flag && source == DutyFlagMemory.SourceGameMemory);
    Check("21b another duty falls back to the game value", otherFlag && otherSource == DutyFlagMemory.SourceGameMemory);
}

// PieceDeriver: what each piece contributes, from the totals of a session. Stat 5 is Mind, stat 3 is Vitality.
const uint Mind = 5;
const uint Vitality = 3;
var t0 = new DateTime(2026, 11, 12, 8, 0, 0, DateTimeKind.Utc);

SessionItem Piece(uint id, int slot = 0) => new() { Slot = slot, SlotName = $"Slot{slot}", ItemId = id, Name = $"Item{id}", IsHq = true, Materia = new List<MateriaRecord>() };

List<StatValue> Sum(int mind, int vitality) => new() { new StatValue(Vitality, "Vitality", vitality), new StatValue(Mind, "Mind", mind) };

ChangeEntry Entry(int seq, double seconds, bool gear, SessionItem[] removed, SessionItem[] added, List<StatValue> totals, uint territory = 1066, int level = 58) => new()
{
    Seq = seq,
    TimestampUtc = t0.AddSeconds(seconds),
    TerritoryId = territory,
    SyncedLevel = level,
    IsLevelSynced = true,
    IsGearChange = gear,
    Removed = removed,
    Added = added,
    Totals = totals,
};

var begin = Entry(0, 0, false, Array.Empty<SessionItem>(), Array.Empty<SessionItem>(), Sum(600, 500));
int MindOf(DerivedPiece piece) => piece.Stats.Single(stat => stat.ParamId == Mind).Value;

// 22. Removing a piece: its stats are minus the difference. Equipping one: the difference.
{
    var (pieces, contradictions, skipped) = PieceDeriver.Derive(begin, new[]
    {
        Entry(1, 20, true, new[] { Piece(10) }, Array.Empty<SessionItem>(), Sum(565, 467)),
        Entry(2, 40, true, Array.Empty<SessionItem>(), new[] { Piece(11, 1) }, Sum(590, 490)),
    });
    Check("22a a removal gives the piece positive stats", pieces.Single(piece => piece.ItemId == 10).Stats.Single(stat => stat.ParamId == Mind).Value == 35);
    Check("22b an equip gives the difference", MindOf(pieces.Single(piece => piece.ItemId == 11)) == 25);
    Check("22c nothing skipped, nothing contradicted", contradictions.Count == 0 && skipped.Count == 0);
}

// 23. A piece removed and put back is measured twice, and agrees.
{
    var (pieces, contradictions, _) = PieceDeriver.Derive(begin, new[]
    {
        Entry(1, 20, true, new[] { Piece(10) }, Array.Empty<SessionItem>(), Sum(565, 467)),
        Entry(2, 40, true, Array.Empty<SessionItem>(), new[] { Piece(10) }, Sum(600, 500)),
    });
    Check("23 removed and put back: one piece, measured twice, no contradiction", pieces.Count == 1 && pieces[0].Measurements == 2 && contradictions.Count == 0);
}

// 24. A piece that gives a different result the second time is reported.
{
    var (_, contradictions, _) = PieceDeriver.Derive(begin, new[]
    {
        Entry(1, 20, true, new[] { Piece(10) }, Array.Empty<SessionItem>(), Sum(565, 467)),
        Entry(2, 40, true, Array.Empty<SessionItem>(), new[] { Piece(10) }, Sum(590, 500)),
    });
    Check("24 a different second result is a contradiction", contradictions.Count == 1);
}

// 25. A swap is two pieces: not used. The next change still starts from the new totals.
{
    var (pieces, _, skipped) = PieceDeriver.Derive(begin, new[]
    {
        Entry(1, 20, true, new[] { Piece(10) }, new[] { Piece(11) }, Sum(620, 500)),
        Entry(2, 40, true, new[] { Piece(12, 2) }, Array.Empty<SessionItem>(), Sum(590, 500)),
    });
    Check("25a the swap is skipped as two pieces", skipped.Count == 1 && skipped[0].StartsWith("#1") && skipped[0].Contains("2 pieces"));
    Check("25b the next change is measured from the swap's totals", pieces.Count == 1 && MindOf(pieces[0]) == 30);
}

// 26. A stats line right after a gear change (within 10 s) is merged into it.
{
    var (pieces, _, skipped) = PieceDeriver.Derive(begin, new[]
    {
        Entry(1, 20, true, new[] { Piece(10) }, Array.Empty<SessionItem>(), Sum(600, 500)),
        Entry(2, 23, false, Array.Empty<SessionItem>(), Array.Empty<SessionItem>(), Sum(565, 467)),
    });
    Check("26 the late stats line is merged, and its totals are used", skipped.Count == 0 && pieces.Count == 1 && MindOf(pieces[0]) == 35);
}

// 27. A stats line long after a gear change is a buff or food: not used, and the baseline moves.
{
    var (pieces, _, skipped) = PieceDeriver.Derive(begin, new[]
    {
        Entry(1, 20, true, new[] { Piece(10) }, Array.Empty<SessionItem>(), Sum(565, 467)),
        Entry(2, 60, false, Array.Empty<SessionItem>(), Array.Empty<SessionItem>(), Sum(605, 467)),
        Entry(3, 80, true, new[] { Piece(12, 2) }, Array.Empty<SessionItem>(), Sum(575, 467)),
    });
    Check("27a the stats-only change is skipped", skipped.Count == 1 && skipped[0].StartsWith("#2"));
    Check("27b the next piece starts from the new totals", pieces.Single(piece => piece.ItemId == 12).Stats.Single(stat => stat.ParamId == Mind).Value == 30);
}

// 28. A change in another territory is not used, and neither is the change after it.
{
    var (pieces, _, skipped) = PieceDeriver.Derive(begin, new[]
    {
        Entry(1, 20, true, new[] { Piece(10) }, Array.Empty<SessionItem>(), Sum(565, 467), territory: 999),
        Entry(2, 40, true, new[] { Piece(12, 2) }, Array.Empty<SessionItem>(), Sum(540, 467)),
        Entry(3, 60, true, new[] { Piece(13, 3) }, Array.Empty<SessionItem>(), Sum(520, 467)),
    });
    Check("28a the other territory and the change after it are skipped", skipped.Count == 2);
    Check("28b the change after that is measured again", pieces.Count == 1 && pieces[0].ItemId == 13 && MindOf(pieces[0]) == 20);
}

// 29. The session note: the typed note is kept, an empty one is filled in with the duty name and the sync type.
Check("29a a typed note is kept", SessionNote.Resolve("my note", "the Dusk Vigil", true, true) == "my note");
Check("29b no sync: Unsynced", SessionNote.Resolve("", "the Dusk Vigil", false, false) == "the Dusk Vigil - Unsynced");
Check("29c level synced: Lvl Synced", SessionNote.Resolve("", "the Dusk Vigil", false, true) == "the Dusk Vigil - Lvl Synced");
Check("29d Minimum IL wins over level sync", SessionNote.Resolve("", "the Dusk Vigil", true, true) == "the Dusk Vigil - Min IL");
Check("29e a note of spaces counts as empty", SessionNote.Resolve("   ", "the Pool of Tribute", false, false) == "the Pool of Tribute - Unsynced");
Check("29f outside a duty there is no name", SessionNote.Resolve("", "", false, false) == "Unsynced");

Console.WriteLine(failures == 0 ? "All checks passed." : $"{failures} check(s) failed.");
return failures == 0 ? 0 : 1;
