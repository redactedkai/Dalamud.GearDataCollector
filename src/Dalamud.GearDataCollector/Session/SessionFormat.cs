namespace GearDataCollector.Session
{
    /// <summary>
    /// The names and numbers that make up the session file format. A script reads these strings, so they are a contract:
    /// change one only together with <see cref="SchemaVersion"/> (bump it when a field is renamed, removed or changes
    /// meaning; adding a field does not need a bump). The format is described in the README.
    /// </summary>
    internal static class SessionFormat
    {
        /// <summary>The schema version written in the sessionStart line.</summary>
        public const int SchemaVersion = 1;

        /// <summary>The first line: note, context, base stats, total stats and the equipped pieces.</summary>
        public const string SessionStart = "sessionStart";

        /// <summary>The equipped gear changed: what left, what entered, and the new total stats.</summary>
        public const string GearChange = "gearChange";

        /// <summary>The total stats changed with no gear change (for example a food buff).</summary>
        public const string StatsChange = "statsChange";

        /// <summary>The last line, written when the session ends cleanly.</summary>
        public const string SessionEnd = "sessionEnd";

        /// <summary>End reason: you pressed Stop session.</summary>
        public const string ReasonStopped = "Stopped";

        /// <summary>End reason: the plugin was unloaded or the game closed it.</summary>
        public const string ReasonPluginUnloaded = "PluginUnloaded";

        /// <summary>End reason: data collection was turned off.</summary>
        public const string ReasonCollectionDisabled = "CollectionDisabled";

        /// <summary>End reason: a different job or character was loaded.</summary>
        public const string ReasonJobOrCharacterChanged = "JobOrCharacterChanged";

        /// <summary>End reason: you moved to another territory, so the duty described in the first line no longer applies.</summary>
        public const string ReasonTerritoryChanged = "TerritoryChanged";

        /// <summary>End reason: the recorder hit an error (see the Dalamud log).</summary>
        public const string ReasonError = "Error";
    }
}
