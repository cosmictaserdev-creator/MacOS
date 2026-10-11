using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace MacOSDock;

internal sealed class DockApp : Application
{
    private Mutex? _singleInstance;
    private Forms.NotifyIcon? _tray;
    private DockWindow? _dock;
    private MenuBarWindow? _bar;
    private SearchWindow? _search;
    private ControlCenterWindow? _controlCenter;
    private readonly DispatcherTimer _refresh = new() { Interval = TimeSpan.FromSeconds(4) };

    public AppSettings Settings { get; private set; } = AppSettings.Load();
    public List<RunningWindow> Running { get; private set; } = [];

    [STAThread]
    private static void Main() => new DockApp().Run();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _singleInstance = new Mutex(true, "Local\\MacOSDock.SingleInstance", out var created);
        if (!created)
        {
            Shutdown();
            return;
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        _dock = new DockWindow(this);
        _bar = new MenuBarWindow(this);
        CreateTray();
        RefreshWindows();
        _dock.RefreshItems();
        _refresh.Tick += (_, _) => RefreshWindows();
        _refresh.Start();
        ApplyVisibility();
    }

    private void CreateTray()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Show dock", null, (_, _) => { Settings.ShowDock = !Settings.ShowDock; SaveAndRefresh(); });
        menu.Items.Add("Show menu bar", null, (_, _) => { Settings.ShowMenuBar = !Settings.ShowMenuBar; SaveAndRefresh(); });
        menu.Items.Add("Add app…", null, (_, _) => AddAppFromDialog());
        menu.Items.Add("Settings…", null, (_, _) => ShowSettings());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Shutdown());
        menu.Opening += (_, _) =>
        {
            ((Forms.ToolStripMenuItem)menu.Items[0]).Checked = Settings.ShowDock;
            ((Forms.ToolStripMenuItem)menu.Items[1]).Checked = Settings.ShowMenuBar;
        };
        _tray = new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "MacOSDock",
            ContextMenuStrip = menu,
            Visible = true
        };
        _tray.DoubleClick += (_, _) => { Settings.ShowDock = true; SaveAndRefresh(); };
    }

    public void RefreshWindows()
    {
        var current = MacOSDock.Windows.GetRunningWindows();
        var changed = current.Count != Running.Count ||
            current.Where((window, index) => index >= Running.Count || window.Handle != Running[index].Handle).Any();
        Running = current;
        if (changed) _dock?.RefreshItems();
        _bar?.RefreshActiveApp();
    }

    public void SaveAndRefresh()
    {
        try { Settings.Save(); }
        catch (Exception ex) { MessageBox.Show($"Could not save settings: {ex.Message}", "MacOSDock"); }
        _dock?.RefreshItems();
        ApplyVisibility();
    }

    public void ApplyVisibility()
    {
        MacOSDock.Windows.SetTaskbarHidden(Settings.HideWindowsTaskbar);
        if (_dock != null)
        {
            if (Settings.ShowDock) _dock.Show(); else _dock.Hide();
        }
        if (_bar != null)
        {
            if (Settings.ShowMenuBar) _bar.Show(); else _bar.Hide();
        }
    }

    public void Pin(string target, string? name = null)
    {
        if (Settings.Pinned.Any(p => string.Equals(p.Target, target, StringComparison.OrdinalIgnoreCase))) return;
        var displayName = name ?? Path.GetFileNameWithoutExtension(target);
        Settings.Pinned.Add(new PinnedApp { Name = displayName, Target = target, Glyph = "◈" });
        SaveAndRefresh();
    }

    public int ImportMyDockFinder(string folder)
    {
        var count = Settings.ImportMyDockFinder(folder);
        if (count > 0)
        {
            _bar?.Close();
            _bar = new MenuBarWindow(this);
            SaveAndRefresh();
        }
        return count;
    }

    public void Unpin(PinnedApp app)
    {
        Settings.Pinned.Remove(app);
        SaveAndRefresh();
    }

    public void AddAppFromDialog()
    {
        using var dialog = new Forms.OpenFileDialog
        {
            Title = "Choose an app or shortcut",
            Filter = "Applications and shortcuts (*.exe;*.lnk)|*.exe;*.lnk",
            CheckFileExists = true
        };
        if (dialog.ShowDialog() == Forms.DialogResult.OK) Pin(dialog.FileName);
    }

    public void Launch(string target)
    {
        try
        {
            if (target.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
            {
                var shell = new ProcessStartInfo("explorer.exe") { UseShellExecute = true };
                shell.ArgumentList.Add(target);
                Process.Start(shell);
                return;
            }
            if (target.Contains('!') && !File.Exists(target))
            {
                var shell = new ProcessStartInfo("explorer.exe") { UseShellExecute = true };
                shell.ArgumentList.Add("shell:AppsFolder\\" + target);
                Process.Start(shell);
                return;
            }
            var info = new ProcessStartInfo(target) { UseShellExecute = true };
            if (File.Exists(target)) info.WorkingDirectory = Path.GetDirectoryName(target)!;
            Process.Start(info);
            Dispatcher.BeginInvoke(async () =>
            {
                await Task.Delay(900);
                RefreshWindows();
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open {target}: {ex.Message}", "MacOSDock");
        }
    }

    public void ShowSettings()
    {
        var window = new SettingsWindow(this);
        window.ShowDialog();
    }

    public void ShowSearch()
    {
        if (_search == null || !_search.IsVisible)
        {
            _search = new SearchWindow(this);
            _search.Show();
        }
        _search.Activate();
        _search.FocusSearch();
    }

    public void ShowControlCenter()
    {
        if (_controlCenter != null && _controlCenter.IsVisible)
        {
            _controlCenter.Close();
            return;
        }
        _controlCenter = new ControlCenterWindow(this);
        _controlCenter.Show();
        _controlCenter.Activate();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _refresh.Stop();
        MacOSDock.Windows.SetTaskbarHidden(false);
        if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
