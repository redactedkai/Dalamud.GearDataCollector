using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

using GearDataCollector.Config;
using GearDataCollector.GameState.Duty;

namespace GearDataCollector
{
    /// <summary>
    /// Dalamud and plugin services.
    /// </summary>
    internal class Service
    {
        /// <summary>
        /// Gets or sets the plugin configuration.
        /// </summary>
        internal static GearDataCollectorConfig Configuration { get; set; } = null!;

        /// <summary>
        /// Gets the Dalamud plugin interface.
        /// </summary>
        [PluginService]
        internal static IDalamudPluginInterface Interface { get; private set; } = null!;

        /// <summary>
        /// Gets the Dalamud framework service.
        /// </summary>
        [PluginService]
        internal static IFramework Framework { get; private set; } = null!;

        /// <summary>
        /// Gets the Dalamud client state.
        /// </summary>
        [PluginService]
        internal static IClientState ClientState { get; private set; } = null!;

        /// <summary>
        /// Gets the Dalamud command manager.
        /// </summary>
        [PluginService]
        internal static ICommandManager CommandManager { get; private set; } = null!;

        /// <summary>
        /// Gets the game data (Lumina) service.
        /// </summary>
        [PluginService]
        internal static IDataManager DataManager { get; private set; } = null!;

        /// <summary>
        /// Gets the player state module.
        /// </summary>
        [PluginService]
        internal static IPlayerState PlayerState { get; private set; } = null!;

        /// <summary>
        /// Gets or sets the memory of the current duty's Minimum Item Level flag.
        /// </summary>
        internal static DutyFlagStore DutyFlags { get; set; } = null!;

        /// <summary>
        /// Gets or sets the Dalamud plugin logger.
        /// </summary>
        internal static IPluginLog PluginLog { get; set; } = null!;
    }
}
