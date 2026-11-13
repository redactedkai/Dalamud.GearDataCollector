namespace GearDataCollector.GameState.Duty
{
    /// <summary>
    /// Remembers whether Minimum Item Level was on for the duty you are in. The game only keeps that flag as "the last
    /// Duty Finder pop", which a game restart may lose, and the flag cannot change while you stay in the duty. So the
    /// flag is taken when the plugin sees you enter a duty, and kept until you leave it. Pure logic: it reads no game
    /// memory and no file; the caller passes in what it saw and saves <see cref="Record"/> when <see cref="Observe"/> says it changed.
    /// </summary>
    internal sealed class DutyFlagMemory
    {
        /// <summary>The flag was read from the game when the plugin saw you enter the duty, so it is the flag of that queue pop.</summary>
        public const string SourceAtEntry = "atEntry";

        /// <summary>An <see cref="SourceAtEntry"/> value that was saved to the file and restored after a game restart.</summary>
        public const string SourceSavedAtEntry = "savedAtEntry";

        /// <summary>The flag is whatever the game's queue memory held when the plugin first looked. Not verified: a restart may have reset it.</summary>
        public const string SourceGameMemory = "gameMemory";

        private bool started;
        private bool restored;
        private bool entryPending;
        private bool firstSighting;
        private uint watchedTerritory;

        /// <summary>
        /// Initializes a new instance of the <see cref="DutyFlagMemory"/> class.
        /// </summary>
        /// <param name="saved">The record saved by an earlier run, or null.</param>
        public DutyFlagMemory(DutyFlagRecord? saved)
        {
            this.Record = saved;
        }

        /// <summary>Gets the remembered duty, or null when the plugin is not in a duty it knows about.</summary>
        public DutyFlagRecord? Record { get; private set; }

        /// <summary>
        /// Feeds what the plugin sees this frame. Call it every frame while the player is loaded.
        /// </summary>
        /// <param name="territoryId">The current territory type row id.</param>
        /// <param name="contentFinderConditionId">The current ContentFinderCondition row id (0 outside a duty, and for a few frames after entering one).</param>
        /// <param name="gameFlag">The game's own PoppedContentIsMinimalIL.</param>
        /// <returns>True if <see cref="Record"/> changed, so the caller should save it.</returns>
        public bool Observe(uint territoryId, uint contentFinderConditionId, bool gameFlag)
        {
            var changed = false;
            if (!this.started || territoryId != this.watchedTerritory)
            {
                this.firstSighting = !this.started;
                this.started = true;
                this.watchedTerritory = territoryId;
                this.entryPending = true;
                if (this.Record != null && this.Record.TerritoryId != territoryId)
                {
                    // Another territory: the remembered duty is over.
                    this.Record = null;
                    this.restored = false;
                    changed = true;
                }
            }

            if (this.entryPending && contentFinderConditionId != 0)
            {
                this.entryPending = false;
                if (this.firstSighting && this.Record != null && this.Record.ContentFinderConditionId == contentFinderConditionId)
                {
                    // The plugin loaded inside the duty it remembers (the game restarted): keep the saved value.
                    this.restored = true;
                }
                else
                {
                    var source = this.firstSighting ? SourceGameMemory : SourceAtEntry;
                    this.Record = new DutyFlagRecord(territoryId, contentFinderConditionId, gameFlag, source);
                    this.restored = false;
                    changed = true;
                }
            }

            return changed;
        }

        /// <summary>
        /// Gives the flag to write for the current duty.
        /// </summary>
        /// <param name="territoryId">The current territory type row id.</param>
        /// <param name="contentFinderConditionId">The current ContentFinderCondition row id.</param>
        /// <param name="gameFlag">The game's own PoppedContentIsMinimalIL, used when nothing is remembered for this duty.</param>
        /// <returns>The flag and where it came from.</returns>
        public (bool MinimumItemLevelOn, string Source) Resolve(uint territoryId, uint contentFinderConditionId, bool gameFlag)
        {
            if (this.Record != null && this.Record.TerritoryId == territoryId && this.Record.ContentFinderConditionId == contentFinderConditionId)
            {
                var source = this.restored && this.Record.Source == SourceAtEntry ? SourceSavedAtEntry : this.Record.Source;
                return (this.Record.MinimumItemLevelOn, source);
            }

            return (gameFlag, SourceGameMemory);
        }
    }
}
