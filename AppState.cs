using System.IO;
using System.Text.Json;

namespace MacOSDock;

internal sealed class PinnedApp
{
    public string Name { get; set; } = "";
    public string Target { get; set; } = "";
    public string Glyph { get; set; } = "◻";
    public string? IconPath { get; set; }
    public string? RealPath { get; set; }
    public string Kind { get; set; } = "app";
}

internal sealed class AppSettings
{
    public List<PinnedApp> Pinned { get; set; } = [];
    public int IconSize { get; set; } = 58;
    public bool ShowMenuBar { get; set; } = true;
    public bool ShowDock { get; set; } = true;
    public string DockPosition { get; set; } = "right";
    public bool ShowRunningApps { get; set; } = true;
    public bool UseLiquidGlass { get; set; } = true;
    public bool AutoHideDock { get; set; }
    public bool HideWindowsTaskbar { get; set; }
    public string? SourceFolder { get; set; }

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
                    if (loaded.DockPosition is not ("left" or "right" or "bottom")) loaded.DockPosition = "right";
                    return loaded;
                }
            }
            catch (Exception)
            {
                // A damaged settings file should not prevent the dock from starting.
            }
        }

        var initial = new AppSettings
        {
            Pinned =
            [
                new() { Name = "File Explorer", Target = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), Glyph = "📁" },
                new() { Name = "Settings", Target = "ms-settings:", Glyph = "⚙" },
                new() { Name = "Terminal", Target = "wt.exe", Glyph = "⌘" }
            ]
        };
        if (Directory.Exists(@"E:\MyDockFinder")) initial.ImportMyDockFinder(@"E:\MyDockFinder");
        return initial;
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        var temp = SettingsPath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, SettingsPath, true);
    }

    public int ImportMyDockFinder(string folder)
    {
        var ini = Path.Combine(folder, "ico.ini");
        if (!File.Exists(ini)) return 0;
        var sections = new SortedDictionary<int, Dictionary<string, string>>();
        Dictionary<string, string>? current = null;
        foreach (var raw in File.ReadLines(ini))
        {
            var line = raw.Trim();
            if (line.StartsWith("[ico", StringComparison.OrdinalIgnoreCase) && line.EndsWith(']') &&
                int.TryParse(line[4..^1], out var number))
            {
                current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                sections[number] = current;
            }
            else if (line.StartsWith('[')) current = null;
            else if (current != null)
            {
                var split = line.IndexOf('=');
                if (split > 0) current[line[..split]] = line[(split + 1)..];
            }
        }

        var imported = new List<PinnedApp>();
        foreach (var section in sections.Values)
        {
            if (section.TryGetValue("delimiter", out var divider) && divider == "1")
            {
                imported.Add(new PinnedApp { Kind = "separator" });
                continue;
            }
            if (!section.TryGetValue("filepath", out var target) || string.IsNullOrWhiteSpace(target)) continue;
            section.TryGetValue("tag", out var tag);
            section.TryGetValue("realpath", out var realPath);
            section.TryGetValue("icopath", out var iconPath);
            var name = string.IsNullOrWhiteSpace(tag) ? Path.GetFileNameWithoutExtension(target) : tag;
            if (!File.Exists(iconPath))
            {
                var iconFolder = Path.Combine(folder, "themes", "mac os", "icons");
                var themed = Path.Combine(iconFolder, name + ".png");
                if (File.Exists(themed)) iconPath = themed;
                else if (name.Equals("Settings", StringComparison.OrdinalIgnoreCase)) iconPath = Path.Combine(iconFolder, "System Settings.png");
                else if (name.Equals("Visual Studio Code", StringComparison.OrdinalIgnoreCase)) iconPath = Path.Combine(iconFolder, "code.png");
            }
            imported.Add(new PinnedApp
            {
                Name = name,
                Target = target,
                RealPath = realPath,
                IconPath = File.Exists(iconPath) ? iconPath : null,
                Glyph = name.Equals("Finder", StringComparison.OrdinalIgnoreCase) ? "▣" : "◈"
            });
        }
        if (imported.Count == 0) return 0;
        Pinned = imported;
        SourceFolder = folder;
        var config = Path.Combine(folder, "config.ini");
        if (File.Exists(config))
        {
            foreach (var line in File.ReadLines(config))
            {
                if (line.StartsWith("screenposition=", StringComparison.OrdinalIgnoreCase))
                    DockPosition = line.EndsWith("3") ? "right" : line.EndsWith("1") ? "left" : "bottom";
                if (line.StartsWith("defaultsize=", StringComparison.OrdinalIgnoreCase) && int.TryParse(line[12..], out var size))
                    IconSize = Math.Clamp(size, 40, 76);
            }
        }
        return imported.Count(p => p.Kind == "app");
    }
}
