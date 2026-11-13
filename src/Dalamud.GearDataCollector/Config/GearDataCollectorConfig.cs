using Dalamud.Configuration;

namespace GearDataCollector.Config
{
    /// <summary>
    /// Plugin configuration.
    /// </summary>
    public class GearDataCollectorConfig : IPluginConfiguration
    {
        /// <inheritdoc/>
        public int Version { get; set; } = 1;

        /// <summary>
        /// Gets or sets the folder session files are written to. Empty means the default folder next to the plugin DLL.
        /// </summary>
        public string SaveDirectory { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether data collection is enabled.
        /// </summary>
        public bool CollectionEnabled { get; set; }

        /// <summary>
        /// Save the configuration.
        /// </summary>
        public void Save() => Service.Interface.SavePluginConfig(this);
    }
}
