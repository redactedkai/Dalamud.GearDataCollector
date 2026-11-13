using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

using Dalamud.Plugin.Services;

using GearDataCollector.GameState;
using GearDataCollector.GameState.Duty;
using GearDataCollector.GameState.Items;
using GearDataCollector.Session.Derivation;
using GearDataCollector.Session.Lines;
using GearDataCollector.Session.Tracking;

namespace GearDataCollector.Session
{
    /// <summary>
    /// Records a session: one JSON Lines file with the starting stats, then one line each time your equipped gear
    /// (or your total stats alone) changes. You swap gear yourself; the plugin only watches.
    /// A session ends when you stop it, when collection is disabled, when you change job or character, or when the plugin unloads.
    /// Read-only: it reads InventoryManager and PlayerState memory and writes a local file. It never equips or moves anything.
    /// </summary>
    internal sealed class SessionRecorder : IDisposable
    {
        /// <summary>How long gear and totals must stay unchanged before a change line is written.</summary>
        private const long StableMilliseconds = 1000;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        };

        private static readonly JsonSerializerOptions PiecesJsonOptions = new(JsonOptions)
        {
            WriteIndented = true,
        };

        // The state is read every frame into these two buffers, which are reused (nothing is allocated per frame).
        // The detector compares them with what the last frame saw and with what the file already says.
        private readonly SlotState[] currentGear = new SlotState[ItemCollector.SlotCount];
        private readonly ChangeDetector detector = new(ItemCollector.SlotCount, StableMilliseconds);

        // The stats a line lists: those non-zero at the start or at any line since, so a stat that drops to 0 is written as 0, not left out.
        private readonly HashSet<uint> listedStats = new();

        // What the lines of the running session say, kept to work out the pieces when it stops.
        private readonly List<ChangeEntry> changes = new();

        private string? path;
        private DutyContext? startContext;
        private ChangeEntry? startEntry;
        private int changeCount;
        private ulong contentId;
        private int jobId;
        private uint territoryId;
        private DateTime gearChangedUtc;
        private DateTime statsChangedUtc;
        private Dictionary<int, ItemRecord> recordedItems = new();
        private int[] currentTotals = Array.Empty<int>();

        /// <summary>
        /// Initializes a new instance of the <see cref="SessionRecorder"/> class.
        /// </summary>
        public SessionRecorder()
        {
            Service.Framework.Update += this.OnUpdate;
        }

        /// <summary>Gets a value indicating whether a session is running.</summary>
        public bool IsRunning => this.path != null;

        /// <summary>
        /// Gets a value indicating whether a change was seen that is not written yet: the gear or the stats differ from the
        /// file and the stable wait is still running.
        /// </summary>
        public bool IsWaitingForChange => this.IsRunning && (this.detector.GearChanged || this.detector.TotalsChanged);

        /// <summary>Gets the note the last started session was saved with: the typed one, or the filled-in one when it was empty.</summary>
        public string Note { get; private set; } = string.Empty;

        /// <summary>Gets a short status line for the window.</summary>
        public string Status { get; private set; } = string.Empty;

        /// <inheritdoc/>
        public void Dispose()
        {
            Service.Framework.Update -= this.OnUpdate;
            this.Stop(SessionFormat.ReasonPluginUnloaded);
        }

        /// <summary>
        /// Starts a session: creates the file and writes the first line. Call it on the game's main thread (Draw and Framework.Update share it); not from a background task.
        /// </summary>
        /// <param name="note">The free-text note to store in the first line. When empty, "duty name - sync type" is stored (see <see cref="Note"/>).</param>
        /// <returns>True if the session started.</returns>
        public bool Start(string note)
        {
            if (this.IsRunning)
            {
                return false;
            }

            try
            {
                if (!StateReader.TryReadSlots(this.currentGear) || !StateReader.TryReadTotals(ref this.currentTotals))
                {
                    this.Status = "Session status: not started (the equipped gear or the player state is not loaded yet)";
                    return false;
                }

                var context = DutyContext.Collect();
                this.Note = SessionNote.Resolve(note, context.DutyName, context.MinimumItemLevelOn, context.IsLevelSynced);
                var items = ItemCollector.Collect();
                var folder = SaveFolder.Get();
                Directory.CreateDirectory(folder);
                var randomId = RandomNumberGenerator.GetHexString(16, lowercase: true);
                var fileName = $"session-{context.TimestampUtc:yyyyMMdd-HHmmss}Z-territory{context.TerritoryId}-{randomId}.jsonl";
                var newPath = Path.Combine(folder, fileName);

                Append(newPath, new SessionStartLine
                {
                    TimestampUtc = context.TimestampUtc,
                    Note = this.Note,
                    Context = context,
                    TerritoryId = context.TerritoryId,
                    SyncedLevel = context.SyncedLevel,
                    IsLevelSynced = context.IsLevelSynced,
                    BaseStats = DutyContext.ReadBaseStats(),
                    Totals = context.AttributeTotals,
                    Equipped = items,
                });

                this.path = newPath;
                this.changeCount = 0;
                this.startContext = context;
                this.listedStats.Clear();
                foreach (var stat in context.AttributeTotals)
                {
                    this.listedStats.Add(stat.ParamId);
                }

                this.changes.Clear();
                this.startEntry = new ChangeEntry
                {
                    Seq = 0,
                    TimestampUtc = context.TimestampUtc,
                    TerritoryId = context.TerritoryId,
                    SyncedLevel = context.SyncedLevel,
                    IsLevelSynced = context.IsLevelSynced,
                    IsGearChange = false,
                    Removed = Array.Empty<SessionItem>(),
                    Added = Array.Empty<SessionItem>(),
                    Totals = context.AttributeTotals,
                };
                this.contentId = Service.PlayerState.ContentId;
                this.jobId = context.JobId;
                this.territoryId = context.TerritoryId;
                this.detector.Begin(this.currentGear, this.currentTotals, Environment.TickCount64);
                this.recordedItems = items.ToDictionary(item => item.Slot);
                this.Status = $"Session status: running (0 changes)\nSession path: {ShownPath(newPath)}";
                Service.PluginLog.Information($"Session started: {newPath}");
                return true;
            }
            catch (Exception ex)
            {
                this.path = null;
                this.Status = $"Session status: not started ({ex.Message})";
                Service.PluginLog.Error(ex, "Could not start the session.");
                return false;
            }
        }

        /// <summary>
        /// Stops the running session and writes the last line. Does nothing if no session is running.
        /// </summary>
        /// <param name="reason">Why the session ended, stored in the last line.</param>
        public void Stop(string reason = SessionFormat.ReasonStopped)
        {
            if (this.path == null)
            {
                return;
            }

            var finished = this.path;
            this.path = null;
            try
            {
                Append(finished, new SessionEndLine
                {
                    TimestampUtc = DateTime.UtcNow,
                    Changes = this.changeCount,
                    Reason = reason,
                });
                this.Status = $"Session status: {reason} ({this.changeCount} changes)\nSession path: {ShownPath(finished)}";
                this.WritePieces(finished);
            }
            catch (Exception ex)
            {
                this.Status = $"Session status: {reason} (the last line could not be written: {ex.Message})\nSession path: {ShownPath(finished)}";
                Service.PluginLog.Error(ex, "Could not finish the session file.");
            }
        }

        // The window shows a file in the save folder as "[save-directory]\file name", not with the whole folder path.
        private static string ShownPath(string file) => $"[save-directory]{Path.DirectorySeparatorChar}{Path.GetFileName(file)}";

        private static void Append(string file, SessionLine line)
        {
            File.AppendAllText(file, JsonSerializer.Serialize(line, JsonOptions) + "\n");
        }

        private static ChangeEntry NewEntry(
            int seq,
            DateTime timestamp,
            uint territoryId,
            int syncedLevel,
            bool isLevelSynced,
            bool isGearChange,
            IReadOnlyList<SessionItem> removed,
            IReadOnlyList<SessionItem> added,
            IReadOnlyList<StatValue> totals) => new()
        {
            Seq = seq,
            TimestampUtc = timestamp,
            TerritoryId = territoryId,
            SyncedLevel = syncedLevel,
            IsLevelSynced = isLevelSynced,
            IsGearChange = isGearChange,
            Removed = removed,
            Added = added,
            Totals = totals,
        };

        /// <summary>
        /// Works out what each piece contributed from the lines of the session that just ended, and saves it next to
        /// the session file. A failure here never stops the session from ending.
        /// </summary>
        private void WritePieces(string sessionFile)
        {
            if (this.startEntry == null || this.startContext == null || this.changes.Count == 0)
            {
                this.Status += $"\nDerived gear path: none (no changes recorded)";
                return;
            }

            try
            {
                var (pieces, contradictions, skipped) = PieceDeriver.Derive(this.startEntry, this.changes);
                var piecesPath = Path.ChangeExtension(sessionFile, ".pieces.json");
                var file = new PiecesFile
                {
                    SessionFile = Path.GetFileName(sessionFile),
                    TerritoryId = this.startEntry.TerritoryId,
                    SyncedLevel = this.startEntry.SyncedLevel,
                    IsLevelSynced = this.startEntry.IsLevelSynced,
                    Context = this.startContext,
                    Pieces = pieces,
                    Contradictions = contradictions,
                    Skipped = skipped,
                };
                File.WriteAllText(piecesPath, JsonSerializer.Serialize(file, PiecesJsonOptions));
                this.Status += $"\nDerived gear path: {ShownPath(piecesPath)}";
            }
            catch (Exception ex)
            {
                this.Status += $"\nDerived gear path: could not be written ({ex.Message})";
                Service.PluginLog.Error(ex, "Could not write the pieces file.");
            }
        }

        private void OnUpdate(IFramework framework)
        {
            if (this.path == null)
            {
                return;
            }

            try
            {
                if (!Service.Configuration.CollectionEnabled)
                {
                    this.Stop(SessionFormat.ReasonCollectionDisabled);
                    return;
                }

                if (!Service.PlayerState.IsLoaded)
                {
                    return;
                }

                if (!StateReader.TryReadJobId(out var jobId))
                {
                    return;
                }

                // A different job or character has different base stats: its totals can't be compared with the earlier lines.
                if (Service.PlayerState.ContentId != this.contentId || jobId != this.jobId)
                {
                    this.Stop(SessionFormat.ReasonJobOrCharacterChanged);
                    return;
                }

                // The duty and its sync rules are described once, in the first line: another territory is another duty.
                if ((uint)Service.ClientState.TerritoryType != this.territoryId)
                {
                    this.Stop(SessionFormat.ReasonTerritoryChanged);
                    return;
                }

                if (!StateReader.TryReadSlots(this.currentGear) || !StateReader.TryReadTotals(ref this.currentTotals))
                {
                    return;
                }

                var due = this.detector.Update(this.currentGear, this.currentTotals, Environment.TickCount64);

                // The time each one last moved goes into the line, to show how long the game takes to update the stats.
                var moment = DateTime.UtcNow;
                if (this.detector.GearMoved)
                {
                    this.gearChangedUtc = moment;
                }

                if (this.detector.TotalsMoved)
                {
                    this.statsChangedUtc = moment;
                }

                if (due)
                {
                    this.WriteChange();
                }
            }
            catch (Exception ex)
            {
                Service.PluginLog.Error(ex, "The session recorder failed.");
                this.Stop(SessionFormat.ReasonError);
            }
        }

        private void WriteChange()
        {
            var path = this.path!;
            var seq = this.changeCount + 1;
            var timestamp = DateTime.UtcNow;
            var totals = DutyContext.TotalsFrom(this.currentTotals, this.listedStats);
            DateTime? statsChangedUtc = this.detector.TotalsChanged ? this.statsChangedUtc : null;
            var territoryId = (uint)Service.ClientState.TerritoryType;
            StateReader.ReadSyncState(out var syncedLevel, out var isLevelSynced);

            if (!this.detector.GearChanged)
            {
                Append(path, new StatsChangeLine
                {
                    TimestampUtc = timestamp,
                    Seq = seq,
                    TerritoryId = territoryId,
                    SyncedLevel = syncedLevel,
                    IsLevelSynced = isLevelSynced,
                    StatsChangedUtc = statsChangedUtc,
                    Totals = totals,
                });
                this.changes.Add(NewEntry(seq, timestamp, territoryId, syncedLevel, isLevelSynced, false, Array.Empty<SessionItem>(), Array.Empty<SessionItem>(), totals));
                this.changeCount = seq;
                this.detector.Commit();
                this.Status = $"Session status: running ({seq} changes, last: stats only)\nSession path: {ShownPath(path)}";
                return;
            }

            var items = ItemCollector.Collect().ToDictionary(item => item.Slot);
            var (removed, added) = GearDiff.Compute(this.detector, ItemCollector.SlotCount, this.recordedItems, items);

            Append(path, new GearChangeLine
            {
                TimestampUtc = timestamp,
                Seq = seq,
                TerritoryId = territoryId,
                SyncedLevel = syncedLevel,
                IsLevelSynced = isLevelSynced,
                GearChangedUtc = this.gearChangedUtc,
                StatsChangedUtc = statsChangedUtc,
                Removed = removed,
                Added = added,
                Totals = totals,
            });
            this.changes.Add(NewEntry(seq, timestamp, territoryId, syncedLevel, isLevelSynced, true, removed, added, totals));
            this.changeCount = seq;
            this.detector.Commit();
            this.recordedItems = items;
            this.Status = $"Session status: running ({seq} changes, last: {string.Join(", ", removed.Concat(added).Select(item => item.SlotName).Distinct())})\nSession path: {ShownPath(path)}";
        }
    }
}
