using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;

using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

using GearDataCollector.GameState.Items;

using Lumina.Excel.Sheets;

namespace GearDataCollector.GameState.Duty
{
    /// <summary>
    /// A read-only snapshot of the duty, the player's level state and the Duty Finder settings.
    /// Every value comes from a Dalamud service, a FFXIVClientStructs field or a Lumina sheet.
    /// Collect it on the game's main thread (Draw and Framework.Update share it); not from a background task.
    /// </summary>
    internal sealed class DutyContext
    {
        // Stat names shown instead of the sheet's: BaseParam 14 is called just "Delay", which reads as a stat of the character.
        private static readonly Dictionary<uint, string> StatNames = new() { [14] = "Auto-attack Delay" };

        // The start time never changes, so it is looked up once.
        private static readonly Lazy<DateTime> GameStarted = new(() =>
        {
            using var process = Process.GetCurrentProcess();
            return process.StartTime.ToUniversalTime();
        });

        /// <summary>Gets the UTC time the context was read. Not written inside the context: every line has its own <c>timestampUtc</c>.</summary>
        [JsonIgnore]
        public DateTime TimestampUtc { get; private init; }

        /// <summary>Gets the plugin version.</summary>
        public string PluginVersion { get; private init; } = string.Empty;

        /// <summary>Gets the game version string from the game data.</summary>
        public string GameVersion { get; private init; } = string.Empty;

        /// <summary>
        /// Gets the UTC time the game process started. A session that begins soon after this, inside a duty, began after a game restart.
        /// </summary>
        public DateTime GameStartedUtc { get; private init; }

        /// <summary>Gets the territory type row id. Not written inside the context: the lines carry it at the top level.</summary>
        [JsonIgnore]
        public uint TerritoryId { get; private init; }

        /// <summary>Gets the ContentFinderCondition row id the game reports, or 0 if none.</summary>
        public uint ContentFinderConditionId { get; private init; }

        /// <summary>Gets the duty name from the ContentFinderCondition row.</summary>
        public string DutyName { get; private init; } = string.Empty;

        /// <summary>Gets ClassJobLevelRequired from the sheet: the level needed to enter. Minimum Item Level syncs the level to it.</summary>
        public int ClassJobLevelRequired { get; private init; }

        /// <summary>Gets ClassJobLevelSync from the sheet (0 means no cap).</summary>
        public int ClassJobLevelSync { get; private init; }

        /// <summary>Gets ItemLevelRequired from the sheet.</summary>
        public int ItemLevelRequired { get; private init; }

        /// <summary>Gets ItemLevelSync from the sheet (0 means no cap).</summary>
        public int ItemLevelSync { get; private init; }

        /// <summary>Gets a value indicating whether the sheet allows Minimum Item Level.</summary>
        public bool AllowMinimumIL { get; private init; }

        /// <summary>Gets a value indicating whether the sheet sets FixedItemLevelSync (the duty sets stats instead of syncing).</summary>
        public bool FixedItemLevelSync { get; private init; }

        /// <summary>Gets the current class job id.</summary>
        public int JobId { get; private init; }

        /// <summary>Gets the current class job abbreviation.</summary>
        public string JobAbbreviation { get; private init; } = string.Empty;

        /// <summary>Gets the Tribe (clan) row id; the clan changes a character's base stats (gender does not).</summary>
        public int TribeId { get; private init; }

        /// <summary>Gets the clan name from the Tribe sheet.</summary>
        public string Clan { get; private init; } = string.Empty;

        /// <summary>Gets the level of the current job itself, whether or not it is synced down (read with shouldGetSynced false). The synced level is <see cref="SyncedLevel"/>.</summary>
        public int RealLevel { get; private init; }

        /// <summary>Gets the synced level the game reports. Not written inside the context: the lines carry it at the top level.</summary>
        [JsonIgnore]
        public int SyncedLevel { get; private init; }

        /// <summary>Gets a value indicating whether the player is level synced. Not written inside the context: the lines carry it at the top level.</summary>
        [JsonIgnore]
        public bool IsLevelSynced { get; private init; }

        /// <summary>
        /// Gets a value indicating whether the Duty Finder queue this duty popped from had Minimum Item Level on
        /// (ContentsFinderQueueInfo.PoppedContentIsMinimalIL, as remembered by <see cref="DutyFlagMemory"/>: the game only keeps
        /// the last pop, which a restart can lose). Minimum IL syncs gear to the duty's ItemLevelRequired and the level to its
        /// ClassJobLevelRequired (seen in a real Dusk Vigil session: level 51, item level 100). The raw Duty Finder settings are
        /// also in <see cref="FinderSettings"/>.
        /// Level sync is <see cref="IsLevelSynced"/>.
        /// </summary>
        public bool MinimumItemLevelOn { get; private init; }

        /// <summary>
        /// Gets where <see cref="MinimumItemLevelOn"/> came from: <c>atEntry</c> (read when the plugin saw you enter the duty),
        /// <c>savedAtEntry</c> (the same, restored from a file after a game restart) or <c>gameMemory</c> (the game's last
        /// Duty Finder pop, not verified).
        /// </summary>
        public string MinimumItemLevelSource { get; private init; } = string.Empty;

        /// <summary>Gets the average item level the Character window shows.</summary>
        public int CurrentItemLevel { get; private init; }

        /// <summary>
        /// Gets the character's total stats as the game holds them (PlayerState.Attributes, indexed by BaseParam id),
        /// non-zero entries only. Inside a sync duty these are the game's own synced totals. Not written inside
        /// the context in the session file: the sessionStart line carries them as its top-level <c>totals</c>.
        /// </summary>
        [JsonIgnore]
        public IReadOnlyList<StatValue> AttributeTotals { get; private init; } = new List<StatValue>();

        /// <summary>Gets the Duty Finder settings the game currently holds, by name.</summary>
        public IReadOnlyDictionary<string, bool> FinderSettings { get; private init; } = new Dictionary<string, bool>();

        /// <summary>
        /// Reads the current context. Call it on the game's main thread (Draw and Framework.Update share it); not from a background task.
        /// </summary>
        /// <returns>The snapshot.</returns>
        public static unsafe DutyContext Collect()
        {
            // These are static game singletons that exist once the player is loaded; a null means the game is not ready.
            var game = GameMain.Instance();
            var player = PlayerState.Instance();
            var finder = ContentsFinder.Instance();
            var ui = UIState.Instance();
            var queue = finder == null ? null : finder->GetQueueInfo();
            if (game == null || player == null || finder == null || ui == null || queue == null)
            {
                throw new InvalidOperationException("The game state is not ready yet.");
            }

            var territoryId = (uint)Service.ClientState.TerritoryType;
            var cfcId = (uint)game->CurrentContentFinderConditionId;

            var cfcName = string.Empty;
            var levelRequired = 0;
            var levelSync = 0;
            var itemLevelRequired = 0;
            var itemLevelSync = 0;
            var allowMinimumIL = false;
            var fixedItemLevelSync = false;
            if (cfcId != 0 && Service.DataManager.GetExcelSheet<ContentFinderCondition>().TryGetRow(cfcId, out var cfc))
            {
                cfcName = cfc.Name.ExtractText();
                levelRequired = cfc.ClassJobLevelRequired;
                levelSync = cfc.ClassJobLevelSync;
                itemLevelRequired = cfc.ItemLevelRequired;
                itemLevelSync = cfc.ItemLevelSync;
                allowMinimumIL = cfc.AllowMinimumIL;
                fixedItemLevelSync = cfc.FixedItemLevelSync;
            }

            var jobId = player->CurrentClassJobId;
            var jobAbbreviation = Service.DataManager.GetExcelSheet<ClassJob>().TryGetRow(jobId, out var job)
                ? job.Abbreviation.ExtractText()
                : string.Empty;

            int tribeId = player->Tribe;
            var clan = string.Empty;
            if (Service.DataManager.GetExcelSheet<Tribe>().TryGetRow((uint)tribeId, out var tribe))
            {
                clan = tribe.Masculine.ExtractText();
            }

            var totals = ReadAttributeTotals();
            var (minimumItemLevelOn, minimumItemLevelSource) = Service.DutyFlags.Resolve(territoryId, cfcId, queue->PoppedContentIsMinimalIL);

            var settings = new Dictionary<string, bool>
            {
                ["IsUnrestrictedParty"] = finder->IsUnrestrictedParty,
                ["IsMinimalIL"] = finder->IsMinimalIL,
                ["IsSilenceEcho"] = finder->IsSilenceEcho,
                ["IsLevelSync"] = finder->IsLevelSync,
                ["PoppedContentIsUnrestrictedParty"] = queue->PoppedContentIsUnrestrictedParty,
                ["PoppedContentIsMinimalIL"] = queue->PoppedContentIsMinimalIL,
                ["PoppedContentIsLevelSync"] = queue->PoppedContentIsLevelSync,
                ["PoppedContentIsSilenceEcho"] = queue->PoppedContentIsSilenceEcho,
            };

            return new DutyContext
            {
                TimestampUtc = DateTime.UtcNow,
                PluginVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty,
                GameVersion = Service.DataManager.GameData.Repositories["ffxiv"].Version,
                GameStartedUtc = GameStarted.Value,
                TerritoryId = territoryId,
                ContentFinderConditionId = cfcId,
                DutyName = cfcName,
                ClassJobLevelRequired = levelRequired,
                ClassJobLevelSync = levelSync,
                ItemLevelRequired = itemLevelRequired,
                ItemLevelSync = itemLevelSync,
                AllowMinimumIL = allowMinimumIL,
                FixedItemLevelSync = fixedItemLevelSync,
                JobId = jobId,
                JobAbbreviation = jobAbbreviation,
                TribeId = tribeId,
                Clan = clan,
                RealLevel = player->GetClassJobLevel(jobId, false),
                SyncedLevel = player->SyncedLevel,
                IsLevelSynced = player->IsLevelSynced,
                MinimumItemLevelOn = minimumItemLevelOn,
                MinimumItemLevelSource = minimumItemLevelSource,
                CurrentItemLevel = ui->CurrentItemLevel,
                AttributeTotals = totals,
                FinderSettings = settings,
            };
        }

        /// <summary>
        /// Reads the character's total stats (PlayerState.Attributes, indexed by BaseParam id), non-zero entries only.
        /// The resource pools (HP, MP, TP, GP, CP) are left out, as in <see cref="StateReader.TryReadTotals"/>.
        /// Call it on the game's main thread (Draw and Framework.Update share it); not from a background task.
        /// </summary>
        /// <returns>One entry per non-zero attribute.</returns>
        public static List<StatValue> ReadAttributeTotals()
        {
            var attributes = Array.Empty<int>();
            if (!StateReader.TryReadTotals(ref attributes))
            {
                throw new InvalidOperationException("The game state is not ready yet.");
            }

            return TotalsFrom(attributes);
        }

        /// <summary>
        /// Turns an attributes array (indexed by BaseParam id, as PlayerState.Attributes is) into stat entries.
        /// A stat is listed when it is non-zero, or when its id is in <paramref name="listed"/> (so a stat that dropped to 0
        /// stays in the list as 0). Non-zero ids are added to <paramref name="listed"/>. Index 0 is not a stat and is skipped.
        /// </summary>
        /// <param name="attributes">The attributes, indexed by BaseParam id.</param>
        /// <param name="listed">The ids to keep listing at 0, or null to list non-zero stats only.</param>
        /// <returns>One entry per listed attribute, in id order.</returns>
        public static List<StatValue> TotalsFrom(ReadOnlySpan<int> attributes, ISet<uint>? listed = null)
        {
            var paramSheet = Service.DataManager.GetExcelSheet<BaseParam>();
            var totals = new List<StatValue>();
            for (var paramId = 1; paramId < attributes.Length; paramId++)
            {
                var value = attributes[paramId];
                if (value != 0 || (listed != null && listed.Contains((uint)paramId)))
                {
                    listed?.Add((uint)paramId);
                    var name = StatNames.TryGetValue((uint)paramId, out var shown)
                        ? shown
                        : paramSheet.TryGetRow((uint)paramId, out var param) ? param.Name.ExtractText() : string.Empty;
                    totals.Add(new StatValue((uint)paramId, name, value));
                }
            }

            return totals;
        }

        /// <summary>
        /// Reads the base stats PlayerState holds, raw. They stay at the unsynced values in a sync duty (seen in six real
        /// sessions); the totals carry the synced values. The file records what the game reports.
        /// Call it on the game's main thread (Draw and Framework.Update share it); not from a background task.
        /// </summary>
        /// <returns>The six base stats (Strength to Piety).</returns>
        public static unsafe List<StatValue> ReadBaseStats()
        {
            var player = PlayerState.Instance();
            if (player == null)
            {
                throw new InvalidOperationException("The game state is not ready yet.");
            }

            var sheet = Service.DataManager.GetExcelSheet<BaseParam>();
            var values = new (uint ParamId, int Value)[]
            {
                (1, player->BaseStrength),
                (2, player->BaseDexterity),
                (3, player->BaseVitality),
                (4, player->BaseIntelligence),
                (5, player->BaseMind),
                (6, player->BasePiety),
            };
            return values
                .Select(pair => new StatValue(pair.ParamId, sheet.TryGetRow(pair.ParamId, out var param) ? param.Name.ExtractText() : string.Empty, pair.Value))
                .ToList();
        }

        /// <summary>
        /// Formats the context as plain text lines for the log and the window.
        /// </summary>
        /// <returns>One line per value.</returns>
        public IReadOnlyList<string> ToLines()
        {
            var lines = new List<string>
            {
                $"Time (UTC): {this.TimestampUtc:O}",
                $"Plugin {this.PluginVersion}, game {this.GameVersion}, game started (UTC) {this.GameStartedUtc:O}",
                $"Territory {this.TerritoryId}, ContentFinderCondition {this.ContentFinderConditionId}: {this.DutyName}",
                $"Sheet: ClassJobLevelRequired {this.ClassJobLevelRequired}, ClassJobLevelSync {this.ClassJobLevelSync}, ItemLevelRequired {this.ItemLevelRequired}, ItemLevelSync {this.ItemLevelSync}, AllowMinimumIL {this.AllowMinimumIL}, FixedItemLevelSync {this.FixedItemLevelSync}",
                $"Player: {this.JobAbbreviation} (id {this.JobId}), level {this.RealLevel}, synced level {this.SyncedLevel}, level synced {this.IsLevelSynced}, Minimum IL on {this.MinimumItemLevelOn} ({this.MinimumItemLevelSource})",
                $"Clan: {this.Clan} (tribe {this.TribeId})",
                $"Average item level (Character window): {this.CurrentItemLevel}",
                $"Total stats: {string.Join(", ", this.AttributeTotals.Select(stat => $"{stat.Name} {stat.Value}"))}",
            };
            foreach (var setting in this.FinderSettings)
            {
                lines.Add($"Finder {setting.Key}: {setting.Value}");
            }

            return lines;
        }
    }
}
