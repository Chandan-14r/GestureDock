using System;
using System.IO;
using System.Text.Json;
using GestureDock.Models;

namespace GestureDock.Services
{
    public class SettingsManager
    {
        private static readonly string SettingsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GestureDock");
        private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

        public AppSettings Current { get; private set; }

        public event Action? SettingsChanged;

        public SettingsManager()
        {
            Current = Load();
        }

        public AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                    if (loaded != null)
                    {
                        return loaded;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
            }

            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                if (!Directory.Exists(SettingsDir))
                {
                    Directory.CreateDirectory(SettingsDir);
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(Current, options);
                File.WriteAllText(SettingsPath, json);

                SettingsChanged?.Invoke();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
            }
        }
    }
}
