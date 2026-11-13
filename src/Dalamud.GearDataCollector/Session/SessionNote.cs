namespace GearDataCollector.Session
{
    /// <summary>
    /// Chooses the note a session is saved with. Pure logic: it reads no game state.
    /// </summary>
    internal static class SessionNote
    {
        /// <summary>
        /// Returns the typed note, or "duty name - sync type" when the note is empty.
        /// </summary>
        /// <param name="note">The note the user typed.</param>
        /// <param name="dutyName">The duty name, empty outside a duty.</param>
        /// <param name="minimumItemLevelOn">Whether Minimum Item Level is on for the duty.</param>
        /// <param name="levelSynced">Whether the player's level is synced.</param>
        /// <returns>The note to save.</returns>
        public static string Resolve(string note, string dutyName, bool minimumItemLevelOn, bool levelSynced)
        {
            if (!string.IsNullOrWhiteSpace(note))
            {
                return note;
            }

            var syncType = minimumItemLevelOn ? "Min IL" : levelSynced ? "Lvl Synced" : "Unsynced";
            return dutyName.Length == 0 ? syncType : $"{dutyName} - {syncType}";
        }
    }
}
