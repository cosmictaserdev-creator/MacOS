using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace MacOSDock;

internal sealed class MenuBarWindow : Window
{
    private readonly DockApp _app;
    private readonly TextBlock _active = Ui.Text("MacOSDock", 13, FontWeights.SemiBold);
    private readonly TextBlock _clock = Ui.Text("", 12);
    private readonly TextBlock _battery = Ui.Text("", 12);
    private readonly TextBlock _network = Ui.Text("", 12);
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(20) };

    public MenuBarWindow(DockApp app)
    {
        _app = app;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        ShowInTaskbar = false;
        Topmost = true;
        Background = Brushes.Transparent;
        Width = SystemParameters.PrimaryScreenWidth;
        Height = 29;
        Left = 0;
        Top = 0;

        var bar = new DockPanel { LastChildFill = false, Margin = new Thickness(17, 0, 18, 0) };
        bar.Effect = new DropShadowEffect { BlurRadius = 5, ShadowDepth = 1, Opacity = 0.8, Color = Colors.Black };
        var left = new StackPanel { Orientation = Orientation.Horizontal };
        var logoPath = app.Settings.SourceFolder == null ? null :
            System.IO.Path.Combine(app.Settings.SourceFolder, "themes", "mac os", "logo", "logo.png");
        var logo = IconService.Load(logoPath);
        if (logo == null)
        {
            var mark = MenuButton("◆", CreateMainMenu());
            mark.FontSize = 16;
            left.Children.Add(mark);
        }
        else
        {
            var mark = new Image { Source = logo, Width = 16, Height = 18, Margin = new Thickness(0, 0, 18, 0), Cursor = Cursors.Hand };
            var menu = CreateMainMenu();
            mark.MouseLeftButtonDown += (_, _) => { menu.PlacementTarget = mark; menu.IsOpen = true; };
            left.Children.Add(mark);
        }
        _active.Margin = new Thickness(12, 0, 17, 0);
        left.Children.Add(_active);
        left.Children.Add(MenuButton("File", CreateFileMenu()));
        left.Children.Add(MenuButton("View", CreateViewMenu()));
        left.Children.Add(MenuButton("Window", CreateWindowMenu()));
        DockPanel.SetDock(left, Dock.Left);
        bar.Children.Add(left);

        var right = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        right.Children.Add(StatusButton(_network, () => _app.Launch("ms-settings:network")));
        right.Children.Add(StatusButton(Ui.Text("◖))", 12), () => _app.Launch("sndvol.exe")));
        right.Children.Add(StatusButton(_battery, () => _app.Launch("ms-settings:powersleep")));
        right.Children.Add(StatusButton(Ui.Text("⌕", 18), _app.ShowSearch));
        right.Children.Add(StatusButton(Ui.Text("☷", 16), _app.ShowControlCenter));
        _clock.Margin = new Thickness(10, 0, 0, 0);
        right.Children.Add(_clock);
        DockPanel.SetDock(right, Dock.Right);
        bar.Children.Add(right);

        var glass = Glass.Surface(bar, 0, app.Settings.UseLiquidGlass, live: true);
        glass.BorderThickness = new Thickness(0);
        glass.Effect = null;
        Content = glass;
        UpdateStatus();
        _timer.Tick += (_, _) => UpdateStatus();
        _timer.Start();
        Closed += (_, _) => _timer.Stop();
    }

    public void RefreshActiveApp()
    {
        var foreground = Windows.ForegroundWindow;
        var window = _app.Running.FirstOrDefault(w => w.Handle == foreground);
        if (window != null) _active.Text = window.ProcessName;
    }

    private void UpdateStatus()
    {
        _clock.Text = DateTime.Now.ToString("ddd d MMM  h:mm tt");
        _network.Text = NetworkInterface.GetIsNetworkAvailable() ? "●" : "○";
        var power = Forms.SystemInformation.PowerStatus;
        _battery.Text = power.BatteryChargeStatus.HasFlag(Forms.BatteryChargeStatus.NoSystemBattery)
            ? ""
            : $"▰ {Math.Round(power.BatteryLifePercent * 100)}%";
    }

    private static TextBlock MenuButton(string label, ContextMenu menu)
    {
        var item = Ui.Text(label, 12);
        item.Margin = new Thickness(0, 0, 19, 0);
        item.Cursor = Cursors.Hand;
        item.ContextMenu = menu;
        item.MouseLeftButtonDown += (_, _) =>
        {
            menu.PlacementTarget = item;
            menu.IsOpen = true;
        };
        return item;
    }

    private static Border StatusButton(TextBlock text, Action action)
    {
        text.HorizontalAlignment = HorizontalAlignment.Center;
        var border = new Border
        {
            Child = text,
            MinWidth = 24,
            Margin = new Thickness(5, 0, 5, 0),
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand
        };
        border.MouseLeftButtonUp += (_, _) => action();
        return border;
    }

    private ContextMenu CreateMainMenu()
    {
        var menu = new ContextMenu();
        Add(menu, "About MacOSDock", () => MessageBox.Show("MacOSDock — Windows desktop dock", "About"));
        Add(menu, "Settings…", _app.ShowSettings);
        Add(menu, "Exit", _app.Shutdown);
        return menu;
    }

    private ContextMenu CreateFileMenu()
    {
        var menu = new ContextMenu();
        Add(menu, "Open File Explorer", () => _app.Launch("explorer.exe"));
        Add(menu, "Add app to dock…", _app.AddAppFromDialog);
        Add(menu, "Search apps…", _app.ShowSearch);
        return menu;
    }

    private ContextMenu CreateViewMenu()
    {
        var menu = new ContextMenu();
        Add(menu, "Toggle dock", () => { _app.Settings.ShowDock = !_app.Settings.ShowDock; _app.SaveAndRefresh(); });
        Add(menu, "Dock settings…", _app.ShowSettings);
        return menu;
    }

    private ContextMenu CreateWindowMenu()
    {
        var menu = new ContextMenu();
        menu.Opened += (_, _) =>
        {
            menu.Items.Clear();
            foreach (var window in _app.Running.Take(25))
            {
                var selected = window;
                Add(menu, selected.Title.Length > 50 ? selected.Title[..50] + "…" : selected.Title,
                    () => Windows.Activate(selected.Handle));
            }
            if (menu.Items.Count == 0) Add(menu, "No open windows", () => { });
        };
        return menu;
    }

    private static void Add(ContextMenu menu, string label, Action action)
    {
        var item = new MenuItem { Header = label };
        item.Click += (_, _) => action();
        menu.Items.Add(item);
    }
}
