using System.IO;

namespace GearDataCollector.Session
{
    /// <summary>
    /// Works out the folder session files are written to. Local files only: no network access. The folder is named "snapshots" (the name predates sessions).
    /// </summary>
    internal static class SaveFolder
    {
        /// <summary>
        /// Gets the default folder: a "snapshots" folder inside the folder the plugin's DLL is in. If Dalamud
        /// gives no location, a "snapshots" folder in the plugin's config directory is used instead.
        /// </summary>
        /// <returns>The full folder path.</returns>
        public static string GetDefault()
        {
            var pluginFolder = Service.Interface.AssemblyLocation.DirectoryName;
            var baseFolder = string.IsNullOrEmpty(pluginFolder) ? Service.Interface.GetPluginConfigDirectory() : pluginFolder;
            return Path.Combine(baseFolder, "snapshots");
        }

        /// <summary>
        /// Gets the folder session files are written to: the configured folder, or the default.
        /// </summary>
        /// <returns>The full folder path.</returns>
        public static string Get()
        {
            var configured = Service.Configuration.SaveDirectory.Trim();
            return configured.Length > 0 ? configured : GetDefault();
        }
    }
}
