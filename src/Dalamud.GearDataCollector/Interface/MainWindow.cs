using System.Numerics;
using System.Reflection;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

using GearDataCollector.GameState.Duty;
using GearDataCollector.Session;

namespace GearDataCollector.Interface
{
    /// <summary>
    /// Main window with two tabs: "Config" for the plugin settings and "Duty / Gear Info" for the data.
    /// </summary>
    internal class MainWindow : Window
    {
        // Widths in multiples of the font size, so they follow the game's font and UI scale. The window itself fits its content.
        // Measured from in-game screenshots (font size 20 px): the Duty / Gear Info content is about 46 wide, the note row is as wide
        // as the Total stats table, and the status wraps a little inside the window so a long path does not widen it.
        private const float MinimumWidth = 46;
        private const float ConfigBoxWidth = 30;
        private const float NoteBoxWidth = 35;
        private const float StatusWrapWidth = 44;

        private static readonly string Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty;
        private static readonly string IdleTitle = Title();

        private readonly FileDialogManager fileDialog = new();
        private readonly SessionRecorder recorder;
        private readonly DutyInfoWatcher watcher;
        private string note = string.Empty;
        private bool wasRunning;
        private string saveDirectoryInput;

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow"/> class.
        /// </summary>
        /// <param name="recorder">The session recorder the window starts and stops, and whose status it shows.</param>
        /// <param name="watcher">The watcher whose latest duty info the window shows.</param>
        public MainWindow(SessionRecorder recorder, DutyInfoWatcher watcher)
            : base(Title())
        {
            this.recorder = recorder;
            this.watcher = watcher;
            this.saveDirectoryInput = Service.Configuration.SaveDirectory;
            this.Flags |= ImGuiWindowFlags.AlwaysAutoResize;
        }

        /// <inheritdoc/>
        public override void Update()
        {
            // The window fits its content. Dalamud multiplies the constraint by the UI scale, the font size already has it.
            this.SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(MinimumWidth * ImGui.GetFontSize() / ImGuiHelpers.GlobalScale, 0),
                MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
            };

            // The title bar shows a change that is waiting to be written, so it is visible when the window is collapsed too.
            this.WindowName = this.recorder.IsWaitingForChange
                ? Title($" - waiting{new string('.', 1 + ((int)(ImGui.GetTime() * 3) % 3))}")
                : IdleTitle;
        }

        /// <inheritdoc/>
        public override void Draw()
        {
            if (!Service.PlayerState.IsLoaded)
            {
                ImGui.Text("Please log in before using this plugin.");
                return;
            }

            if (ImGui.BeginTabBar("tabs"))
            {
                if (ImGui.BeginTabItem("Config"))
                {
                    this.DrawConfigTab();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Duty / Gear Info"))
                {
                    this.DrawInfoTab();
                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }

            this.fileDialog.Draw();
        }

        // The text after ### is the window's identity, so its saved position and size survive a version or title change.
        private static string Title(string suffix = "") =>
            $"Gear Data Collector v{Version}{suffix}###GearDataCollector";

        private void DrawConfigTab()
        {
            var config = Service.Configuration;

            ImGui.TextUnformatted("Save directory");
            ImGui.SetNextItemWidth(ConfigBoxWidth * ImGui.GetFontSize());
            if (ImGui.InputText("##saveDirectory", ref this.saveDirectoryInput, 512))
            {
                config.SaveDirectory = this.saveDirectoryInput;
            }

            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                config.Save();
            }

            ImGui.SameLine();
            if (ImGui.Button("Browse..."))
            {
                this.fileDialog.OpenFolderDialog("Choose the session folder", this.OnFolderChosen, SaveFolder.Get());
            }

            if (config.SaveDirectory.Trim().Length == 0)
            {
                ImGui.TextDisabled($"Empty: using {SaveFolder.GetDefault()}");
            }

            ImGui.Spacing();
            var enabled = config.CollectionEnabled;
            if (ImGui.Checkbox("Enable data collection", ref enabled))
            {
                config.CollectionEnabled = enabled;
                config.Save();
            }
        }

        private void OnFolderChosen(bool accepted, string path)
        {
            if (!accepted || path.Length == 0)
            {
                return;
            }

            this.saveDirectoryInput = path;
            Service.Configuration.SaveDirectory = path;
            Service.Configuration.Save();
        }

        private void DrawInfoTab()
        {
            var collecting = Service.Configuration.CollectionEnabled;
            if (!collecting)
            {
                ImGui.TextDisabled("Collection is disabled. Tick Enable data collection in the Config tab.");
            }

            // The note belongs to the session: it is cleared once the session has ended, however it ended.
            var running = this.recorder.IsRunning;
            if (this.wasRunning && !running)
            {
                this.note = string.Empty;
            }

            this.wasRunning = running;
            ImGui.BeginDisabled(running);
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted("Session note");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(NoteBoxWidth * ImGui.GetFontSize());
            ImGui.InputText("##note", ref this.note, 512);
            ImGui.EndDisabled();

            ImGui.SameLine();
            ImGui.BeginDisabled(!collecting);
            if (ImGui.Button(running ? "Stop session" : "Start session"))
            {
                if (running)
                {
                    this.recorder.Stop();
                }
                else
                {
                    if (this.recorder.Start(this.note))
                    {
                        this.note = this.recorder.Note;
                    }
                }
            }

            ImGui.EndDisabled();

            ImGui.TextColored(new Vector4(1f, 0.85f, 0.3f, 1f), "Remove food and other stat buffs first: they add to your stats and spoil the per-piece numbers.");
            ImGui.TextDisabled("Instructions: Add or remove one piece at a time. The weapon and Soul Crystal can't be removed.");

            if (this.recorder.Status.Length > 0)
            {
                ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + (StatusWrapWidth * ImGui.GetFontSize()));
                ImGui.TextUnformatted(this.recorder.Status);
                ImGui.PopTextWrapPos();
            }

            ImGui.Separator();
            if (this.watcher.Error.Length > 0)
            {
                ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + (StatusWrapWidth * ImGui.GetFontSize()));
                ImGui.TextUnformatted(this.watcher.Error);
                ImGui.PopTextWrapPos();
            }

            if (this.watcher.Context != null)
            {
                DutyInfoView.Draw(this.watcher.Context);
            }
        }
    }
}
