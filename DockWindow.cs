using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace MacOSDock;

internal sealed class DockWindow : Window
{
    private readonly DockApp _app;
    private readonly StackPanel _icons = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

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

        var scroll = new ScrollViewer
        {
            Content = _icons,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxWidth = Math.Max(420, SystemParameters.PrimaryScreenWidth - 40)
        };
        var content = Ui.Card(scroll, 21);
        content.Padding = new Thickness(8, 7, 8, 6);
        content.Effect = new DropShadowEffect { BlurRadius = 30, ShadowDepth = 9, Opacity = 0.42, Color = Colors.Black };
        Content = new Border { Child = content, Padding = new Thickness(16, 7, 16, 18) };

        Loaded += (_, _) => Reposition();
        SizeChanged += (_, _) => Reposition();
        DragOver += OnDragOver;
        Drop += OnDrop;
        ContextMenu = CreateDockMenu();
    }

    public void RefreshItems()
    {
        _icons.Children.Clear();
        var shown = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pin in _app.Settings.Pinned)
        {
            var running = _app.Running.FirstOrDefault(w => Matches(pin, w));
            _icons.Children.Add(CreateIcon(pin.Name, pin.Target, pin.Glyph, pin, running));
            if (running != null) shown.Add(running.ProcessName);
        }

        var unpinned = _app.Running
            .Where(w => !shown.Contains(w.ProcessName))
            .GroupBy(w => w.ProcessName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Take(14);
        foreach (var window in unpinned)
            _icons.Children.Add(CreateIcon(window.ProcessName, window.ExecutablePath, "◈", null, window));

        var divider = new Border
        {
            Width = 1,
            Height = _app.Settings.IconSize * 0.7,
            Background = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)),
            Margin = new Thickness(7, 0, 7, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        _icons.Children.Add(divider);
        _icons.Children.Add(CreateAction("＋", "Add app", _app.AddAppFromDialog));
        _icons.Children.Add(CreateAction("⌕", "Search", _app.ShowSearch));
        Dispatcher.BeginInvoke(Reposition, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private static bool Matches(PinnedApp pin, RunningWindow window)
    {
        if (window.ExecutablePath != null && string.Equals(pin.Target, window.ExecutablePath, StringComparison.OrdinalIgnoreCase))
            return true;
        return string.Equals(Path.GetFileNameWithoutExtension(pin.Target), window.ProcessName, StringComparison.OrdinalIgnoreCase);
    }

    private Border CreateIcon(string name, string? target, string glyph, PinnedApp? pin, RunningWindow? running)
    {
        var size = _app.Settings.IconSize;
        var image = LoadIcon(target);
        FrameworkElement visual;
        if (image != null)
        {
            visual = new Image { Source = image, Width = size - 6, Height = size - 6, Stretch = Stretch.Uniform };
        }
        else
        {
            visual = Ui.Text(glyph, size * 0.61);
            visual.HorizontalAlignment = HorizontalAlignment.Center;
        }

        var center = new Grid { Width = size, Height = size };
        center.Children.Add(visual);
        var indicator = new Border
        {
            Width = running == null ? 0 : 5,
            Height = 5,
            CornerRadius = new CornerRadius(3),
            Background = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, -8)
        };
        center.Children.Add(indicator);

        var holder = new Border
        {
            Child = center,
            Background = Brushes.Transparent,
            CornerRadius = new CornerRadius(13),
            Padding = new Thickness(4, 2, 4, 10),
            Margin = new Thickness(1, 0, 1, 0),
            ToolTip = running?.Title ?? name,
            Cursor = Cursors.Hand,
            RenderTransformOrigin = new Point(0.5, 1),
            RenderTransform = new ScaleTransform(1, 1)
        };
        holder.MouseEnter += (_, _) => Animate(holder, 1.22);
        holder.MouseLeave += (_, _) => Animate(holder, 1);
        holder.MouseLeftButtonUp += (_, _) =>
        {
            if (running != null) Windows.Activate(running.Handle);
            else if (target != null) _app.Launch(target);
        };
        holder.ContextMenu = CreateItemMenu(pin, running, target);
        return holder;
    }

    private Border CreateAction(string glyph, string label, Action action)
    {
        var size = _app.Settings.IconSize;
        var text = Ui.Text(glyph, size * 0.64);
        text.HorizontalAlignment = HorizontalAlignment.Center;
        var button = new Border
        {
            Child = text,
            Width = size + 7,
            Height = size + 12,
            Background = Brushes.Transparent,
            ToolTip = label,
            Cursor = Cursors.Hand,
            RenderTransformOrigin = new Point(0.5, 1),
            RenderTransform = new ScaleTransform(1, 1)
        };
        button.MouseEnter += (_, _) => Animate(button, 1.18);
        button.MouseLeave += (_, _) => Animate(button, 1);
        button.MouseLeftButtonUp += (_, _) => action();
        return button;
    }

    private static void Animate(FrameworkElement element, double scale)
    {
        var transform = (ScaleTransform)element.RenderTransform;
        var animation = new DoubleAnimation(scale, TimeSpan.FromMilliseconds(130))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        transform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
    }

    private static ImageSource? LoadIcon(string? path)
    {
        if (path == null || !File.Exists(path)) return null;
        try
        {
            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
            if (icon == null) return null;
            var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(64, 64));
            source.Freeze();
            return source;
        }
        catch (Exception) { return null; }
    }

    private ContextMenu CreateItemMenu(PinnedApp? pin, RunningWindow? running, string? target)
    {
        var menu = new ContextMenu();
        if (target != null) Add(menu, "Open", () => _app.Launch(target));
        if (running != null) Add(menu, "Bring to front", () => Windows.Activate(running.Handle));
        if (pin != null) Add(menu, "Remove from dock", () => _app.Unpin(pin));
        else if (running?.ExecutablePath != null) Add(menu, "Keep in dock", () => _app.Pin(running.ExecutablePath, running.ProcessName));
        if (target != null && File.Exists(target))
        {
            Add(menu, "Show in File Explorer", () =>
            {
                var info = new ProcessStartInfo("explorer.exe") { UseShellExecute = true };
                info.ArgumentList.Add("/select,");
                info.ArgumentList.Add(target);
                Process.Start(info);
            });
        }
        return menu;
    }

    private ContextMenu CreateDockMenu()
    {
        var menu = new ContextMenu();
        Add(menu, "Add app…", _app.AddAppFromDialog);
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
        e.Effects = HasAppFile(e.Data) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;
        foreach (var path in paths.Where(IsAppFile)) _app.Pin(path);
        e.Handled = true;
    }

    private static bool HasAppFile(IDataObject data) =>
        data.GetData(DataFormats.FileDrop) is string[] paths && paths.Any(IsAppFile);

    private static bool IsAppFile(string path) => File.Exists(path) &&
        (string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(Path.GetExtension(path), ".lnk", StringComparison.OrdinalIgnoreCase));

    private void Reposition()
    {
        if (!IsLoaded) return;
        var work = SystemParameters.WorkArea;
        Left = work.Left + (work.Width - ActualWidth) / 2;
        Top = work.Bottom - ActualHeight - 5;
    }
}
