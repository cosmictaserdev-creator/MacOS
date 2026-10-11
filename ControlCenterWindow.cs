using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Forms = System.Windows.Forms;

namespace MacOSDock;

internal sealed class ControlCenterWindow : Window
{
    private readonly DockApp _app;
    private bool _closed;

    public ControlCenterWindow(DockApp app)
    {
        _app = app;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        Width = 335;
        Height = 368;
        Left = SystemParameters.WorkArea.Right - Width - 15;
        Top = 34;

        var body = new StackPanel { Margin = new Thickness(18) };
        body.Children.Add(Ui.Text("Control Center", 18, FontWeights.SemiBold));
        body.Children.Add(new Border { Height = 13 });

        var grid = new UniformGrid { Columns = 2, Rows = 2 };
        grid.Children.Add(Tile("⌁", "Network", NetworkInterface.GetIsNetworkAvailable() ? "Connected" : "Disconnected",
            () => Open("ms-settings:network")));
        grid.Children.Add(Tile("ᛒ", "Bluetooth", "Devices", () => Open("ms-settings:bluetooth")));
        grid.Children.Add(Tile("☀", "Display", "Brightness", () => Open("ms-settings:display")));
        grid.Children.Add(Tile("◖))", "Sound", "Volume", () => Open("sndvol.exe")));
        body.Children.Add(grid);

        var battery = Forms.SystemInformation.PowerStatus;
        if (!battery.BatteryChargeStatus.HasFlag(Forms.BatteryChargeStatus.NoSystemBattery))
        {
            var label = Ui.Text($"Battery  {Math.Round(battery.BatteryLifePercent * 100)}%", 13);
            label.Margin = new Thickness(3, 14, 0, 0);
            body.Children.Add(label);
        }

        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 18, 0, 0) };
        row.Children.Add(Ui.Button("Settings", () => Open("ms-settings:")));
        row.Children.Add(Ui.Button("Task view", () => { Close(); Windows.ShowTaskView(); }));
        body.Children.Add(row);

        var snapshot = LiveBackdrop.CaptureUncovered(new Rect(Left + 12, Top + 12, Width - 24, Height - 24));
        var glass = Glass.Surface(body, 24, app.Settings.UseLiquidGlass, snapshot: snapshot);
        glass.RenderTransformOrigin = new Point(0.85, 0);
        var entrance = new TransformGroup();
        entrance.Children.Add(new ScaleTransform(0.96, 0.96));
        entrance.Children.Add(new TranslateTransform(0, -12));
        glass.RenderTransform = entrance;
        Content = new Border { Child = glass, Padding = new Thickness(12) };
        // Activation can change while Close is already running. Defer dismissal
        // until the current window message has completed.
        Deactivated += (_, _) => Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!_closed && IsVisible) Close();
        }));
        Closed += (_, _) => _closed = true;
        Loaded += (_, _) =>
        {
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(170)));
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            ((ScaleTransform)entrance.Children[0]).BeginAnimation(ScaleTransform.ScaleXProperty,
                new DoubleAnimation(1, TimeSpan.FromMilliseconds(240)) { EasingFunction = ease });
            ((ScaleTransform)entrance.Children[0]).BeginAnimation(ScaleTransform.ScaleYProperty,
                new DoubleAnimation(1, TimeSpan.FromMilliseconds(240)) { EasingFunction = ease });
            ((TranslateTransform)entrance.Children[1]).BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(0, TimeSpan.FromMilliseconds(240)) { EasingFunction = ease });
        };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }

    private Border Tile(string glyph, string title, string detail, Action action)
    {
        var stack = new StackPanel { Margin = new Thickness(12, 10, 8, 9) };
        stack.Children.Add(Ui.Text(glyph, 24));
        stack.Children.Add(Ui.Text(title, 13, FontWeights.SemiBold));
        var subtitle = Ui.Text(detail, 11);
        subtitle.Foreground = Ui.Muted;
        stack.Children.Add(subtitle);
        var tile = Glass.Surface(stack, 16, true);
        tile.Margin = new Thickness(3);
        tile.Cursor = Cursors.Hand;
        tile.MouseLeftButtonUp += (_, _) => action();
        return tile;
    }

    private void Open(string target)
    {
        Close();
        _app.Launch(target);
    }
}
