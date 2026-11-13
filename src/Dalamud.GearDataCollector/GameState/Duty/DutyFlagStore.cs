using System;
using System.IO;
using System.Text.Json;

using Dalamud.Plugin.Services;

using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace GearDataCollector.GameState.Duty
{
    /// <summary>
    /// Watches the duty you are in and keeps <see cref="DutyFlagMemory"/> in a small file in the plugin's config folder,
    /// so the Minimum Item Level flag survives a game restart. The file is temporary: it is removed when the plugin sees you
    /// leave the duty, and it is safe to delete by hand. Read-only towards the game.
    /// </summary>
    internal sealed class DutyFlagStore : IDisposable
    {
        private const string FileName = "duty-flag.json";

        private readonly DutyFlagMemory memory;
        private readonly string path;

        /// <summary>
        /// Initializes a new instance of the <see cref="DutyFlagStore"/> class and starts watching.
        /// </summary>
        public DutyFlagStore()
        {
            this.path = Path.Combine(Service.Interface.GetPluginConfigDirectory(), FileName);
            this.memory = new DutyFlagMemory(this.Load());
            Service.Framework.Update += this.OnUpdate;
        }

        /// <inheritdoc cref="DutyFlagMemory.Resolve"/>
        public (bool MinimumItemLevelOn, string Source) Resolve(uint territoryId, uint contentFinderConditionId, bool gameFlag) =>
            this.memory.Resolve(territoryId, contentFinderConditionId, gameFlag);

        /// <inheritdoc/>
        public void Dispose()
        {
            // The file stays: a game quit inside the duty is the case it is for.
            Service.Framework.Update -= this.OnUpdate;
        }

        private unsafe void OnUpdate(IFramework framework)
        {
            try
            {
                var territoryId = (uint)Service.ClientState.TerritoryType;
                if (!Service.PlayerState.IsLoaded || territoryId == 0)
                {
                    return;
                }

                var game = GameMain.Instance();
                var finder = ContentsFinder.Instance();
                var queue = finder == null ? null : finder->GetQueueInfo();
                if (game == null || queue == null)
                {
                    return;
                }

                if (this.memory.Observe(territoryId, (uint)game->CurrentContentFinderConditionId, queue->PoppedContentIsMinimalIL))
                {
                    this.Save();
                }
            }
            catch (Exception ex)
            {
                Service.PluginLog.Error(ex, "The duty flag memory failed.");
            }
        }

        private DutyFlagRecord? Load()
        {
            try
            {
                return File.Exists(this.path) ? JsonSerializer.Deserialize<DutyFlagRecord>(File.ReadAllText(this.path)) : null;
            }
            catch (Exception ex)
            {
                Service.PluginLog.Warning(ex, "Could not read the saved duty flag; ignoring it.");
                return null;
            }
        }

        private void Save()
        {
            try
            {
                if (this.memory.Record == null)
                {
                    File.Delete(this.path);
                    return;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(this.path)!);
                File.WriteAllText(this.path, JsonSerializer.Serialize(this.memory.Record));
            }
            catch (Exception ex)
            {
                Service.PluginLog.Warning(ex, "Could not save the duty flag.");
            }
        }
    }
}
