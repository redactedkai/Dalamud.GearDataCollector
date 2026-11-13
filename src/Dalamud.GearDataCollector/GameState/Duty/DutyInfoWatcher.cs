using System;
using System.Diagnostics;
using System.Linq;

using Dalamud.Plugin.Services;

namespace GearDataCollector.GameState.Duty
{
    /// <summary>
    /// Reads the <see cref="DutyContext"/> about once a second and writes it to the Dalamud log when it changes, when login
    /// completes. Logout is logged too. Read-only towards the game.
    /// </summary>
    internal sealed class DutyInfoWatcher : IDisposable
    {
        private const long ReadIntervalMs = 1000;

        private readonly Stopwatch clock = Stopwatch.StartNew();
        private long lastReadMs = -ReadIntervalMs;
        private string? lastLoggedKey;
        private string? lastLoggedError;
        private bool loginPending;

        /// <summary>
        /// Initializes a new instance of the <see cref="DutyInfoWatcher"/> class and starts watching.
        /// </summary>
        public DutyInfoWatcher()
        {
            this.loginPending = Service.ClientState.IsLoggedIn;
            Service.ClientState.Login += this.OnLogin;
            Service.ClientState.Logout += this.OnLogout;
            Service.Framework.Update += this.OnUpdate;
        }

        /// <summary>
        /// Gets the last duty context that was read, or null before the first read and after logout.
        /// </summary>
        public DutyContext? Context { get; private set; }

        /// <summary>
        /// Gets the message of the last failed read, or an empty string when the last read worked.
        /// </summary>
        public string Error { get; private set; } = string.Empty;

        /// <inheritdoc/>
        public void Dispose()
        {
            Service.Framework.Update -= this.OnUpdate;
            Service.ClientState.Login -= this.OnLogin;
            Service.ClientState.Logout -= this.OnLogout;
        }

        private static string Key(DutyContext context) => string.Join("\n", context.ToLines().Skip(1));

        private void OnLogin() => this.loginPending = true;

        private void OnLogout(int type, int code)
        {
            this.loginPending = false;
            this.Context = null;
            this.Error = string.Empty;
            this.lastLoggedKey = null;
            this.lastLoggedError = null;
            Service.PluginLog.Information("Logged out.");
        }

        private void OnUpdate(IFramework framework)
        {
            if (!Service.PlayerState.IsLoaded || !Service.Configuration.CollectionEnabled)
            {
                return;
            }

            var now = this.clock.ElapsedMilliseconds;
            if (!this.loginPending && now - this.lastReadMs < ReadIntervalMs)
            {
                return;
            }

            this.lastReadMs = now;
            var login = this.loginPending;
            this.loginPending = false;
            if (login)
            {
                Service.PluginLog.Information("Logged in.");
            }

            this.Read(login);
        }

        // Reads the context. It is logged when forced or when it differs from the last logged one, not counting the read time.
        // A read error is logged once until a read works again.
        private void Read(bool force)
        {
            try
            {
                var context = DutyContext.Collect();
                this.Context = context;
                this.Error = string.Empty;
                this.lastLoggedError = null;

                var key = Key(context);
                if (force || key != this.lastLoggedKey)
                {
                    this.lastLoggedKey = key;
                    foreach (var line in context.ToLines())
                    {
                        Service.PluginLog.Information(line);
                    }
                }
            }
            catch (Exception ex)
            {
                this.Error = $"Could not read the duty info: {ex.Message}";
                if (this.Error != this.lastLoggedError)
                {
                    this.lastLoggedError = this.Error;
                    Service.PluginLog.Error(ex, "Could not read the duty info.");
                }
            }
        }
    }
}
