using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace MacOSDock;

internal sealed class DockWindow : Window
{
    private readonly DockApp _app;
    private readonly StackPanel _icons = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly ScrollViewer _scroll;
    private readonly Border _surface;
    private readonly List<Border> _iconViews = [];
    private readonly DispatcherTimer _hideTimer = new() { Interval = TimeSpan.FromMilliseconds(650) };
    private bool _concealed;
    private bool IsVertical => _app.Settings.DockPosition != "bottom";

    public DockWindow(DockApp app)
    {
        _app = app;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.Manual;
        AllowDrop = true;

        _scroll = new ScrollViewer { Content = _icons };
        _surface = Glass.Surface(_scroll, 23, app.Settings.UseLiquidGlass, live: true);
        _surface.Padding = new Thickness(7);
        Content = new Border { Child = _surface, Padding = new Thickness(16) };

        Loaded += (_, _) => Reposition();
        SizeChanged += (_, _) => Reposition();
        DragOver += OnDragOver;
        Drop += OnDrop;
        ContextMenu = CreateDockMenu();
        _icons.MouseMove += OnIconMouseMove;
        _icons.MouseLeave += (_, _) => ResetMagnification();
        MouseEnter += (_, _) => { _hideTimer.Stop(); _concealed = false; Reposition(); };
        MouseLeave += (_, _) => { if (_app.Settings.AutoHideDock) _hideTimer.Start(); };
        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer.Stop();
            if (_app.Settings.AutoHideDock && !IsMouseOver) { _concealed = true; Reposition(); }
        };
    }

    public void RefreshItems()
    {
        var vertical = IsVertical;
        _icons.Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal;
        _scroll.VerticalScrollBarVisibility = vertical ? ScrollBarVisibility.Hidden : ScrollBarVisibility.Disabled;
        _scroll.HorizontalScrollBarVisibility = vertical ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Hidden;
        _scroll.MaxHeight = vertical ? Math.Max(350, SystemParameters.PrimaryScreenHeight - 90) : double.PositiveInfinity;
        _scroll.MaxWidth = vertical ? double.PositiveInfinity : Math.Max(420, SystemParameters.PrimaryScreenWidth - 40);
        Glass.SetEnabled(_surface, _app.Settings.UseLiquidGlass);
        if (!_app.Settings.AutoHideDock) _concealed = false;
        _icons.Children.Clear();
        _iconViews.Clear();

        var shown = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pin in _app.Settings.Pinned)
        {
            if (pin.Kind == "separator")
            {
                _icons.Children.Add(CreateDivider());
                continue;
            }
            var running = _app.Running.FirstOrDefault(w => Matches(pin, w));
            _icons.Children.Add(CreateIcon(pin.Name, pin.Target, pin.Glyph, pin.IconPath, pin.RealPath, pin, running));
            if (running != null) shown.Add(running.ProcessName);
        }

        if (_app.Settings.ShowRunningApps)
        {
            var unpinned = _app.Running
                .Where(w => !shown.Contains(w.ProcessName))
                .GroupBy(w => w.ProcessName, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .Take(14);
            foreach (var window in unpinned)
                _icons.Children.Add(CreateIcon(window.ProcessName, window.ExecutablePath, "◈", null,
                    window.ExecutablePath, null, window));
        }

        _icons.Children.Add(CreateDivider());
        _icons.Children.Add(CreateAction("▦", "Apps", _app.ShowSearch));
        var trashIcon = _app.Settings.SourceFolder == null ? null :
            Path.Combine(_app.Settings.SourceFolder, "themes", "mac os", "icons", "Trash_Empty.png");
        _icons.Children.Add(CreateAction("♜", "Trash", () => _app.Launch("shell:RecycleBinFolder"), trashIcon));
        Dispatcher.BeginInvoke(new Action(Reposition), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private static bool Matches(PinnedApp pin, RunningWindow window)
    {
        if (window.ExecutablePath != null &&
            (string.Equals(pin.Target, window.ExecutablePath, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(pin.RealPath, window.ExecutablePath, StringComparison.OrdinalIgnoreCase)))
            return true;
        return string.Equals(Path.GetFileNameWithoutExtension(pin.RealPath ?? pin.Target),
            window.ProcessName, StringComparison.OrdinalIgnoreCase);
    }

    private Border CreateIcon(string name, string? target, string glyph, string? iconPath,
        string? realPath, PinnedApp? pin, RunningWindow? running)
    {
        var size = _app.Settings.IconSize;
        var icon = IconService.Load(iconPath) ?? IconService.Load(realPath) ?? IconService.Load(target);
        FrameworkElement visual;
        if (icon != null)
            visual = new Image { Source = icon, Width = size, Height = size, Stretch = Stretch.Uniform };
        else
            visual = new TextBlock
            {
                Text = glyph,
                FontFamily = new FontFamily("Segoe UI Symbol"),
                FontSize = size * 0.64,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

        var center = new Grid { Width = size, Height = size };
        center.Children.Add(visual);
        if (running != null)
        {
            center.Children.Add(new Border
            {
                Width = 5,
                Height = 5,
                CornerRadius = new CornerRadius(3),
                Background = Brushes.White,
                HorizontalAlignment = IsVertical
                    ? (_app.Settings.DockPosition == "right" ? HorizontalAlignment.Right : HorizontalAlignment.Left)
                    : HorizontalAlignment.Center,
                VerticalAlignment = IsVertical ? VerticalAlignment.Center : VerticalAlignment.Bottom,
                Margin = IsVertical
                    ? (_app.Settings.DockPosition == "right" ? new Thickness(0, 0, -8, 0) : new Thickness(-8, 0, 0, 0))
                    : new Thickness(0, 0, 0, -8)
            });
        }

        var holder = WrapIcon(center, running?.Title ?? name);
        holder.MouseLeftButtonUp += (_, _) =>
        {
            if (running != null) Windows.Activate(running.Handle);
            else if (target != null) _app.Launch(target);
        };
        holder.ContextMenu = CreateItemMenu(pin, running, target);
        return holder;
    }

    private Border CreateAction(string glyph, string label, Action action, string? iconPath = null)
    {
        var size = _app.Settings.IconSize;
        var icon = IconService.Load(iconPath);
        FrameworkElement visual = icon == null
            ? new TextBlock
            {
                Text = glyph,
                FontFamily = new FontFamily("Segoe UI Symbol"),
                FontSize = size * 0.63,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
            : new Image { Source = icon, Width = size, Height = size };
        var center = new Grid { Width = size, Height = size };
        center.Children.Add(visual);
        var holder = WrapIcon(center, label);
        holder.MouseLeftButtonUp += (_, _) => action();
        return holder;
    }

    private Border WrapIcon(UIElement content, string tooltip)
    {
        var vertical = IsVertical;
        var holder = new Border
        {
            Child = content,
            Background = Brushes.Transparent,
            CornerRadius = new CornerRadius(14),
            Padding = vertical ? new Thickness(4, 3, 4, 3) : new Thickness(4, 3, 4, 10),
            Margin = new Thickness(1),
            ToolTip = tooltip,
            Cursor = Cursors.Hand,
            RenderTransformOrigin = vertical
                ? new Point(_app.Settings.DockPosition == "right" ? 1 : 0, 0.5)
                : new Point(0.5, 1),
            RenderTransform = new ScaleTransform(1, 1)
        };
        _iconViews.Add(holder);
        return holder;
    }

    private Border CreateDivider()
    {
        var vertical = IsVertical;
        return new Border
        {
            Width = vertical ? _app.Settings.IconSize * 0.73 : 1,
            Height = vertical ? 1 : _app.Settings.IconSize * 0.73,
            Background = new SolidColorBrush(Color.FromArgb(105, 255, 255, 255)),
            Margin = vertical ? new Thickness(0, 9, 0, 9) : new Thickness(9, 0, 9, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    private void OnIconMouseMove(object sender, MouseEventArgs e)
    {
        var pointer = e.GetPosition(_icons);
        var radius = _app.Settings.IconSize * 2.5;
        foreach (var view in _iconViews)
        {
            var center = view.TranslatePoint(new Point(view.ActualWidth / 2, view.ActualHeight / 2), _icons);
            var distance = IsVertical ? Math.Abs(pointer.Y - center.Y) : Math.Abs(pointer.X - center.X);
            var influence = Math.Max(0, 1 - distance / radius);
            Animate(view, 1 + 0.43 * influence * influence);
        }
    }

    private void ResetMagnification()
    {
        foreach (var view in _iconViews) Animate(view, 1);
    }

    private static void Animate(FrameworkElement element, double scale)
    {
        var transform = (ScaleTransform)element.RenderTransform;
        var animation = new DoubleAnimation(scale, TimeSpan.FromMilliseconds(120))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        transform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
    }

    private ContextMenu CreateItemMenu(PinnedApp? pin, RunningWindow? running, string? target)
    {
        var menu = new ContextMenu();
        if (target != null) Add(menu, "Open", () => _app.Launch(target));
        if (running != null) Add(menu, "Bring to front", () => Windows.Activate(running.Handle));
        if (pin != null) Add(menu, "Remove from Dock", () => _app.Unpin(pin));
        else if (running?.ExecutablePath != null) Add(menu, "Keep in Dock", () => _app.Pin(running.ExecutablePath, running.ProcessName));
        var location = pin?.RealPath ?? target;
        if (location != null && File.Exists(location))
        {
            Add(menu, "Show in File Explorer", () =>
            {
                var info = new ProcessStartInfo("explorer.exe") { UseShellExecute = true };
                info.ArgumentList.Add("/select,");
                info.ArgumentList.Add(location);
                Process.Start(info);
            });
        }
        return menu;
    }

    private ContextMenu CreateDockMenu()
    {
        var menu = new ContextMenu();
        Add(menu, "Add app…", _app.AddAppFromDialog);
        Add(menu, "Apps", _app.ShowSearch);
        Add(menu, "Settings…", _app.ShowSettings);
        Add(menu, "Exit", _app.Shutdown);
        return menu;
    }

    private static void Add(ContextMenu menu, string label, Action action)
    {
        var item = new MenuItem { Header = label };
        item.Click += (_, _) => action();
        menu.Items.Add(item);
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = HasTarget(e.Data) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;
        foreach (var path in paths.Where(IsTarget)) _app.Pin(path);
        e.Handled = true;
    }

    private static bool HasTarget(IDataObject data) =>
        data.GetData(DataFormats.FileDrop) is string[] paths && paths.Any(IsTarget);

    private static bool IsTarget(string path) => File.Exists(path) || Directory.Exists(path);

    private void Reposition()
    {
        if (!IsLoaded) return;
        var work = SystemParameters.WorkArea;
        switch (_app.Settings.DockPosition)
        {
            case "left":
                Left = _concealed ? work.Left - ActualWidth + 9 : work.Left + 4;
                Top = work.Top + (work.Height - ActualHeight) / 2;
                break;
            case "right":
                Left = _concealed ? work.Right - 9 : work.Right - ActualWidth - 4;
                Top = work.Top + (work.Height - ActualHeight) / 2;
                break;
            default:
                Left = work.Left + (work.Width - ActualWidth) / 2;
                Top = _concealed ? work.Bottom - 9 : work.Bottom - ActualHeight - 4;
                break;
        }
    }
}
