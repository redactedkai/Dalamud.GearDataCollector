using System;

using GearDataCollector.GameState.Items;

namespace GearDataCollector.Session.Tracking
{
    /// <summary>
    /// Decides when a change line is due: when the equipped gear or the total stats differ from what the file already
    /// says, and nothing has moved for the stable time. Pure logic: it reads no game memory, no clock and no file;
    /// the caller passes in what it read and the current tick count.
    /// Feed it once per frame with <see cref="Update"/>, call <see cref="Commit"/> after the line was written.
    /// </summary>
    internal sealed class ChangeDetector
    {
        private readonly long stableMilliseconds;

        // observed: what the last Update saw (to tell when things are still moving).
        // recorded: what the file already says.
        private readonly SlotState[] observedGear;
        private readonly SlotState[] recordedGear;
        private int[] observedTotals = Array.Empty<int>();
        private int[] recordedTotals = Array.Empty<int>();
        private long stableSince;

        /// <summary>
        /// Initializes a new instance of the <see cref="ChangeDetector"/> class.
        /// </summary>
        /// <param name="slotCount">How many gear slots are compared.</param>
        /// <param name="stableMilliseconds">How long the state must stay unchanged before a line is due.</param>
        public ChangeDetector(int slotCount, long stableMilliseconds)
        {
            this.stableMilliseconds = stableMilliseconds;
            this.observedGear = new SlotState[slotCount];
            this.recordedGear = new SlotState[slotCount];
        }

        /// <summary>Gets a value indicating whether the gear differed from the previous frame in the last <see cref="Update"/>.</summary>
        public bool GearMoved { get; private set; }

        /// <summary>Gets a value indicating whether the totals differed from the previous frame in the last <see cref="Update"/>.</summary>
        public bool TotalsMoved { get; private set; }

        /// <summary>Gets a value indicating whether the gear differs from the recorded state as of the last <see cref="Update"/>.</summary>
        public bool GearChanged { get; private set; }

        /// <summary>Gets a value indicating whether the totals differ from the recorded state as of the last <see cref="Update"/>.</summary>
        public bool TotalsChanged { get; private set; }

        /// <summary>
        /// Takes the state the first line recorded. Allocates the two totals copies (once per session).
        /// </summary>
        /// <param name="gear">The equipped gear, one entry per slot.</param>
        /// <param name="totals">The total stats (PlayerState.Attributes).</param>
        /// <param name="now">The current tick count in milliseconds.</param>
        public void Begin(ReadOnlySpan<SlotState> gear, ReadOnlySpan<int> totals, long now)
        {
            gear.CopyTo(this.observedGear);
            gear.CopyTo(this.recordedGear);
            this.observedTotals = totals.ToArray();
            this.recordedTotals = totals.ToArray();
            this.stableSince = now;
            this.GearMoved = false;
            this.TotalsMoved = false;
            this.GearChanged = false;
            this.TotalsChanged = false;
        }

        /// <summary>
        /// Feeds this frame's reading. Copies into arrays it already owns, so it allocates nothing.
        /// The totals must have the same length as in <see cref="Begin"/> (the attribute count does not change while the game runs).
        /// </summary>
        /// <param name="gear">The equipped gear, one entry per slot.</param>
        /// <param name="totals">The total stats (PlayerState.Attributes).</param>
        /// <param name="now">The current tick count in milliseconds.</param>
        /// <returns>True when a line is due: something differs from the recorded state and nothing has moved for the stable time.
        /// <see cref="GearChanged"/> then tells a gear line from a stats-only line.</returns>
        public bool Update(ReadOnlySpan<SlotState> gear, ReadOnlySpan<int> totals, long now)
        {
            // Gear or totals still moving (several slots swap one by one, and totals follow): restart the wait.
            this.GearMoved = !gear.SequenceEqual(this.observedGear);
            this.TotalsMoved = !SameTotals(totals, this.observedTotals);
            if (this.GearMoved || this.TotalsMoved)
            {
                gear.CopyTo(this.observedGear);
                totals.CopyTo(this.observedTotals);
                this.stableSince = now;
            }

            this.GearChanged = !this.observedGear.AsSpan().SequenceEqual(this.recordedGear);
            this.TotalsChanged = !SameTotals(this.observedTotals, this.recordedTotals);
            return (this.GearChanged || this.TotalsChanged) && now - this.stableSince >= this.stableMilliseconds;
        }

        /// <summary>
        /// Tells whether a slot differs between the recorded state and the last <see cref="Update"/>.
        /// </summary>
        /// <param name="slot">The slot index.</param>
        /// <returns>True if the slot changed.</returns>
        public bool IsSlotChanged(int slot) => this.observedGear[slot] != this.recordedGear[slot];

        /// <summary>
        /// Marks the state of the last <see cref="Update"/> as recorded. Call it only after the line was written.
        /// </summary>
        public void Commit()
        {
            this.observedGear.CopyTo(this.recordedGear, 0);
            this.observedTotals.CopyTo(this.recordedTotals, 0);
        }

        /// <summary>Compares two attribute arrays. Index 0 is not a stat (BaseParam ids start at 1), so it is skipped.</summary>
        private static bool SameTotals(ReadOnlySpan<int> first, ReadOnlySpan<int> second) => first[1..].SequenceEqual(second[1..]);
    }
}
