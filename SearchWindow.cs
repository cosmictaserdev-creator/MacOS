using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MacOSDock;

// Tahoe presents installed apps in a floating Spotlight browsing window.
internal sealed class SearchWindow : Window
{
    private readonly DockApp _app;
    private readonly TextBox _query = new();
    private readonly WrapPanel _tiles = new();
    private readonly TextBlock _heading = Ui.Text("All Apps", 19, FontWeights.SemiBold);
    private readonly StackPanel _categories = new();
    private readonly TextBlock _count = Ui.Text("", 12);
    private List<PinnedApp> _apps = [];
    private List<PinnedApp> _visible = [];
    private string _category = "All Apps";
    private bool _listView;

    public SearchWindow(DockApp app)
    {
        _app = app;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        ShowInTaskbar = false;
        Topmost = true;
        Background = Brushes.Transparent;
        Width = Math.Min(890, SystemParameters.WorkArea.Width - 40);
        Height = Math.Min(670, SystemParameters.WorkArea.Height - 70);
        Left = SystemParameters.WorkArea.Left + (SystemParameters.WorkArea.Width - Width) / 2;
        Top = SystemParameters.WorkArea.Top + 58;

        var root = new Grid { Margin = new Thickness(16), RenderTransformOrigin = new Point(0.5, 0.5) };
        var entrance = new TransformGroup();
        entrance.Children.Add(new ScaleTransform(0.96, 0.96));
        entrance.Children.Add(new TranslateTransform(0, 14));
        root.RenderTransform = entrance;
        var body = new Grid { Margin = new Thickness(20) };
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = new Grid { Margin = new Thickness(0, 0, 0, 17) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(new TextBlock
        {
            Text = "Apps", FontSize = 24, FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(3, 0, 24, 0)
        });

        _query.Height = 38;
        _query.FontSize = 14;
        _query.Padding = new Thickness(12, 7, 12, 5);
        _query.Background = Brushes.Transparent;
        _query.Foreground = Brushes.White;
        _query.CaretBrush = Brushes.White;
        _query.BorderThickness = new Thickness(0);
        _query.ToolTip = "Search apps";
        _query.TextChanged += (_, _) => Filter();
        _query.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && _visible.Count > 0) Launch(_visible[0]);
            if (e.Key == Key.Escape) Close();
        };
        var search = Glass.Surface(_query, 16, app.Settings.UseLiquidGlass);
        Grid.SetColumn(search, 1);
        header.Children.Add(search);

        var controls = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(12, 0, 0, 0) };
        controls.Children.Add(Ui.Button("Grid", () => { _listView = false; ShowApps(); }));
        controls.Children.Add(Ui.Button("List", () => { _listView = true; ShowApps(); }));
        controls.Children.Add(Ui.Button("✕", Close));
        Grid.SetColumn(controls, 2);
        header.Children.Add(controls);
        Grid.SetRow(header, 0);
        body.Children.Add(header);

        var content = new Grid();
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _categories.Margin = new Thickness(0, 0, 17, 0);
        content.Children.Add(_categories);

        var main = new Grid();
        main.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var title = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 0, 0, 10) };
        title.Children.Add(_heading);
        _count.Margin = new Thickness(12, 3, 0, 0);
        _count.Foreground = Ui.Muted;
        title.Children.Add(_count);
        main.Children.Add(title);
        var scroll = new ScrollViewer
        {
            Content = _tiles,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        Grid.SetRow(scroll, 1);
        main.Children.Add(scroll);
        Grid.SetColumn(main, 1);
        content.Children.Add(main);
        Grid.SetRow(content, 1);
        body.Children.Add(content);

        root.Children.Add(Glass.Surface(body, 27, app.Settings.UseLiquidGlass, live: true));
        Content = root;
        Loaded += async (_, _) =>
        {
            FocusSearch();
            var pinned = _app.Settings.Pinned.Where(p => p.Kind == "app").ToList();
            _apps = await Task.Run(() => FindApps(pinned));
            ShowCategories();
            Filter();
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(170)));
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            ((ScaleTransform)entrance.Children[0]).BeginAnimation(ScaleTransform.ScaleXProperty,
                new DoubleAnimation(1, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease });
            ((ScaleTransform)entrance.Children[0]).BeginAnimation(ScaleTransform.ScaleYProperty,
                new DoubleAnimation(1, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease });
            ((TranslateTransform)entrance.Children[1]).BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(0, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease });
        };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }

    public void FocusSearch()
    {
        _query.Focus();
        _query.SelectAll();
    }

    private static List<PinnedApp> FindApps(List<PinnedApp> pinned)
    {
        var result = new List<PinnedApp>(pinned);
        foreach (var folder in new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu)
        })
        {
            try
            {
                foreach (var path in Directory.EnumerateFiles(folder, "*.lnk", SearchOption.AllDirectories))
                    result.Add(new PinnedApp { Name = Path.GetFileNameWithoutExtension(path), Target = path });
            }
            catch (Exception) { }
        }
        return result.GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First()).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string Category(PinnedApp app)
    {
        var value = (app.Name + " " + app.Target).ToLowerInvariant();
        if (new[] { "settings", "terminal", "powershell", "control panel", "utility", "explorer" }.Any(value.Contains)) return "Utilities";
        if (new[] { "code", "studio", "developer", "git", "python", "visual" }.Any(value.Contains)) return "Developer";
        if (new[] { "photo", "paint", "design", "music", "video", "editor" }.Any(value.Contains)) return "Creative";
        if (new[] { "office", "word", "excel", "mail", "calendar", "note", "pdf" }.Any(value.Contains)) return "Productivity";
        if (new[] { "chrome", "firefox", "edge", "browser", "zen" }.Any(value.Contains)) return "Internet";
        return "Other";
    }

    private void ShowCategories()
    {
        _categories.Children.Clear();
        foreach (var category in new[] { "All Apps", "Suggested", "Utilities", "Developer", "Creative", "Productivity", "Internet", "Other" })
        {
            var label = Ui.Text(category, 13, category == _category ? FontWeights.SemiBold : FontWeights.Normal);
            label.Margin = new Thickness(13, 8, 0, 8);
            var item = new Border
            {
                Child = label, CornerRadius = new CornerRadius(11), Cursor = Cursors.Hand,
                Background = category == _category ? Ui.GlassLight : Brushes.Transparent,
                Margin = new Thickness(0, 2, 0, 2)
            };
            item.MouseLeftButtonUp += (_, _) =>
            {
                _category = category;
                ShowCategories();
                Filter();
            };
            _categories.Children.Add(item);
        }
    }

    private void Filter()
    {
        var query = _query.Text.Trim();
        _visible = _apps.Where(a =>
            (query.Length == 0 || a.Name.Contains(query, StringComparison.OrdinalIgnoreCase)) &&
            (_category == "All Apps" ||
             (_category == "Suggested" && _app.Settings.Pinned.Any(p => p.Target == a.Target)) ||
             Category(a) == _category)).ToList();
        ShowApps();
    }

    private void ShowApps()
    {
        _tiles.Children.Clear();
        _heading.Text = _query.Text.Length > 0 ? "Search Results" : _category;
        _count.Text = _visible.Count.ToString();
        foreach (var app in _visible)
            _tiles.Children.Add(Tile(app));
    }

    private Border Tile(PinnedApp app)
    {
        var icon = IconService.Load(app.IconPath) ?? IconService.Load(app.RealPath) ?? IconService.Load(app.Target);
        var image = icon == null
            ? (FrameworkElement)new TextBlock { Text = "◈", FontSize = _listView ? 29 : 49, Foreground = Brushes.White }
            : new Image { Source = icon, Width = _listView ? 37 : 60, Height = _listView ? 37 : 60, Stretch = Stretch.Uniform };
        var name = Ui.Text(app.Name, _listView ? 13 : 12);
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        var layout = new StackPanel
        {
            Orientation = _listView ? Orientation.Horizontal : Orientation.Vertical,
            HorizontalAlignment = _listView ? HorizontalAlignment.Left : HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        layout.Children.Add(image);
        name.Margin = _listView ? new Thickness(13, 0, 0, 0) : new Thickness(0, 8, 0, 0);
        name.TextAlignment = _listView ? TextAlignment.Left : TextAlignment.Center;
        name.HorizontalAlignment = _listView ? HorizontalAlignment.Left : HorizontalAlignment.Center;
        name.MaxWidth = _listView ? 490 : 92;
        layout.Children.Add(name);
        var tile = new Border
        {
            Child = layout,
            Width = _listView ? 570 : 106,
            Height = _listView ? 50 : 103,
            CornerRadius = new CornerRadius(13),
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            Margin = new Thickness(3),
            ToolTip = app.Name
        };
        tile.MouseEnter += (_, _) => tile.Background = new SolidColorBrush(Color.FromArgb(49, 255, 255, 255));
        tile.MouseLeave += (_, _) => tile.Background = Brushes.Transparent;
        tile.MouseLeftButtonUp += (_, _) => Launch(app);
        var menu = new ContextMenu();
        var pin = new MenuItem { Header = "Keep in Dock" };
        pin.Click += (_, _) => _app.Pin(app.Target, app.Name);
        menu.Items.Add(pin);
        tile.ContextMenu = menu;
        return tile;
    }

    private void Launch(PinnedApp app)
    {
        _app.Launch(app.Target);
        Close();
    }
}
