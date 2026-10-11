using System.IO;
using System.Text.Json;

namespace MacOSDock;

internal sealed class PinnedApp
{
    public string Name { get; set; } = "";
    public string Target { get; set; } = "";
    public string Glyph { get; set; } = "◻";
}

internal sealed class AppSettings
{
    public List<PinnedApp> Pinned { get; set; } = [];
    public int IconSize { get; set; } = 54;
    public bool ShowMenuBar { get; set; } = true;
    public bool ShowDock { get; set; } = true;

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MacOSDock", "settings.json");

    public static AppSettings Load()
    {
        if (File.Exists(SettingsPath))
        {
            try
            {
                var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath));
                if (loaded != null)
                {
                    loaded.Pinned ??= [];
                    loaded.IconSize = Math.Clamp(loaded.IconSize, 40, 76);
                    return loaded;
                }
            }
            catch (Exception)
            {
                // A damaged settings file should not prevent the dock from starting.
            }
        }

        return new AppSettings
        {
            Pinned =
            [
                new() { Name = "File Explorer", Target = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), Glyph = "📁" },
                new() { Name = "Settings", Target = "ms-settings:", Glyph = "⚙" },
                new() { Name = "Terminal", Target = "wt.exe", Glyph = "⌘" }
            ]
        };
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        var temp = SettingsPath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, SettingsPath, true);
    }
}
