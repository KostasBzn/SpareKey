using System;
using System.IO;
using System.Text.Json;

namespace SpareKey.Services
{
    /// <summary>
    /// App settings, saved as a small JSON file:
    ///   Windows: %APPDATA%\SpareKey\settings.json
    ///   Linux:   ~/.config/SpareKey/settings.json
    /// </summary>
    public class AppSettings
    {
        /// <summary>The vault that was opened last time. "" = none yet.</summary>
        public string LastVaultPath { get; set; } = "";

        public static AppSettings Load()
        {
            try
            {
                string path = GetSettingsFilePath();
                if (!File.Exists(path))
                    return new AppSettings();

                string json = File.ReadAllText(path);
                AppSettings settings = JsonSerializer.Deserialize<AppSettings>(json);
                return settings ?? new AppSettings();
            }
            catch (Exception)
            {
                // A broken settings file is not a problem
                // we start with empty settings again
                return new AppSettings();
            }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(GetSettingsFolder());

                JsonSerializerOptions options = new JsonSerializerOptions();
                options.WriteIndented = true;   // readable if you open it in an editor

                File.WriteAllText(GetSettingsFilePath(), JsonSerializer.Serialize(this, options));
            }
            catch (Exception) { }
        }

        /// <summary>Default folder for a new vault is Documents/SpareKey</summary>
        public static string GetDefaultVaultFolder()
        {
            // windows
            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            // Linux
            if (documents == "")
                documents = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            return Path.Combine(documents, "SpareKey");
        }

        // appdata on Windows, .config on Linux
        private static string GetSettingsFolder()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "SpareKey");
        }

        private static string GetSettingsFilePath()
        {
            return Path.Combine(GetSettingsFolder(), "settings.json");
        }
    }
}
