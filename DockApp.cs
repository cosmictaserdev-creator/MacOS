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
        Running = MacOSDock.Windows.GetRunningWindows();
        _dock?.RefreshItems();
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

    protected override void OnExit(ExitEventArgs e)
    {
        _refresh.Stop();
        if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
