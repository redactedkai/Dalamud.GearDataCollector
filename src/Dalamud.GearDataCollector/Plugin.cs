using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

using GearDataCollector.Config;
using GearDataCollector.GameState.Duty;
using GearDataCollector.Interface;
using GearDataCollector.Session;

namespace GearDataCollector
{
    /// <summary>
    /// Main plugin class.
    /// </summary>
    internal class Plugin : IDalamudPlugin
    {
        private const string CommandName = "/geardata";

        private readonly DutyFlagStore dutyFlags;
        private readonly DutyInfoWatcher dutyInfo;
        private readonly WindowSystem windowSystem;
        private readonly MainWindow mainWindow;
        private readonly SessionRecorder sessionRecorder;

        /// <summary>
        /// Initializes a new instance of the <see cref="Plugin"/> class.
        /// </summary>
        /// <param name="interf">PluginInterface from dalamud.</param>
        /// <param name="logger">Dalamud provided logger for the plugin.</param>
        public Plugin(IDalamudPluginInterface interf, IPluginLog logger)
        {
            interf.Create<Service>();
            Service.PluginLog = logger;

            Service.Configuration = Service.Interface.GetPluginConfig() as GearDataCollectorConfig ?? new GearDataCollectorConfig();

            this.dutyFlags = new();
            Service.DutyFlags = this.dutyFlags;
            this.dutyInfo = new();
            this.sessionRecorder = new();
            this.mainWindow = new(this.sessionRecorder, this.dutyInfo);
            this.windowSystem = new("GearDataCollector");
            this.windowSystem.AddWindow(this.mainWindow);

            Service.Interface.UiBuilder.Draw += this.windowSystem.Draw;
            Service.Interface.UiBuilder.OpenMainUi += this.OnOpenUi;
            Service.Interface.UiBuilder.OpenConfigUi += this.OnOpenUi;

            Service.CommandManager.AddHandler(CommandName, new CommandInfo(this.OnChatCommand)
            {
                HelpMessage = "Open the Gear Data Collector window.",
                ShowInHelp = true,
            });

            Service.PluginLog.Information($"Gear Data Collector loaded. Reason: {Service.Interface.Reason}");
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Service.CommandManager.RemoveHandler(CommandName);
            Service.Interface.UiBuilder.Draw -= this.windowSystem.Draw;
            Service.Interface.UiBuilder.OpenMainUi -= this.OnOpenUi;
            Service.Interface.UiBuilder.OpenConfigUi -= this.OnOpenUi;
            this.windowSystem.RemoveAllWindows();
            this.sessionRecorder.Dispose();
            this.dutyInfo.Dispose();
            this.dutyFlags.Dispose();
        }

        private void OnOpenUi()
        {
            this.mainWindow.Toggle();
        }

        private void OnChatCommand(string command, string arguments)
        {
            this.mainWindow.Toggle();
        }
    }
}
