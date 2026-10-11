using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace MacOSDock;

internal sealed class SearchWindow : Window
{
    private readonly DockApp _app;
    private readonly TextBox _query = new();
    private readonly UniformGrid _tiles = new();
    private readonly TextBlock _pageLabel = Ui.Text("", 12);
    private List<PinnedApp> _apps = [];
    private List<PinnedApp> _filtered = [];
    private int _page;
    private int _pageSize;

    public SearchWindow(DockApp app)
    {
        _app = app;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        ShowInTaskbar = false;
        Topmost = true;
        Background = Brushes.Transparent;
        Width = SystemParameters.PrimaryScreenWidth;
        Height = SystemParameters.PrimaryScreenHeight;
        Left = 0;
        Top = 0;

        var width = (int)Width;
        var height = (int)Height;
        var columns = Math.Clamp((width - 160) / 142, 4, 9);
        var rows = Math.Clamp((height - 270) / 132, 2, 5);
        _pageSize = columns * rows;
        _tiles.Columns = columns;
        _tiles.Rows = rows;
        _tiles.HorizontalAlignment = HorizontalAlignment.Center;
        _tiles.VerticalAlignment = VerticalAlignment.Center;

        var root = new Grid();
        var wallpaper = DesktopWallpaper();
        if (wallpaper != null)
        {
            root.Children.Add(new Border
            {
                Background = new ImageBrush(wallpaper) { Stretch = Stretch.UniformToFill },
                Effect = new BlurEffect { Radius = 34 },
                IsHitTestVisible = false
            });
        }
        else root.Background = new LinearGradientBrush(Color.FromRgb(42, 60, 93), Color.FromRgb(28, 31, 49), 45);
        root.Children.Add(new Border { Background = new SolidColorBrush(Color.FromArgb(145, 8, 13, 27)), IsHitTestVisible = false });

        var layout = new Grid { Margin = new Thickness(48, 44, 48, 54) };
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        header.Children.Add(new TextBlock
        {
            Text = "Launchpad",
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 30,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            Effect = new DropShadowEffect { BlurRadius = 8, Opacity = 0.7 }
        });
        _query.Width = Math.Min(420, width - 100);
        _query.Height = 42;
        _query.FontSize = 17;
        _query.Padding = new Thickness(14, 8, 14, 7);
        _query.Background = Brushes.Transparent;
        _query.Foreground = Brushes.White;
        _query.CaretBrush = Brushes.White;
        _query.BorderThickness = new Thickness(0);
        _query.TextChanged += (_, _) => Filter();
        _query.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && _filtered.Count > 0) Launch(_filtered[0]);
            if (e.Key == Key.Escape) Close();
            if (e.Key == Key.PageDown) ChangePage(1);
            if (e.Key == Key.PageUp) ChangePage(-1);
        };
        var search = Glass.Surface(_query, 18, app.Settings.UseLiquidGlass);
        search.Margin = new Thickness(0, 18, 0, 0);
        header.Children.Add(search);
        Grid.SetRow(header, 0);
        layout.Children.Add(header);

        Grid.SetRow(_tiles, 1);
        layout.Children.Add(_tiles);

        var pager = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        pager.Children.Add(PagerButton("‹", () => ChangePage(-1)));
        _pageLabel.Margin = new Thickness(17, 0, 17, 0);
        pager.Children.Add(_pageLabel);
        pager.Children.Add(PagerButton("›", () => ChangePage(1)));
        Grid.SetRow(pager, 2);
        layout.Children.Add(pager);

        root.Children.Add(layout);
        Content = root;
        Loaded += async (_, _) =>
        {
            FocusSearch();
            var pinned = _app.Settings.Pinned.Where(p => p.Kind == "app").ToList();
            _apps = await Task.Run(() => FindApps(pinned));
            Filter();
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
        };
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Close();
            if (e.Key == Key.Right) ChangePage(1);
            if (e.Key == Key.Left) ChangePage(-1);
        };
    }

    public void FocusSearch()
    {
        _query.Focus();
        _query.SelectAll();
    }

    private static ImageSource? DesktopWallpaper()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
            var path = key?.GetValue("WallPaper") as string;
            if (path == null || !File.Exists(path)) return null;
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path);
            image.DecodePixelWidth = 1920;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception) { return null; }
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
        return result.GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();
    }

    private void Filter()
    {
        var query = _query.Text.Trim();
        _filtered = _apps.Where(a => query.Length == 0 || a.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        _page = 0;
        ShowPage();
    }

    private void ChangePage(int delta)
    {
        var pages = Math.Max(1, (int)Math.Ceiling(_filtered.Count / (double)_pageSize));
        _page = Math.Clamp(_page + delta, 0, pages - 1);
        ShowPage();
    }

    private void ShowPage()
    {
        _tiles.Children.Clear();
        foreach (var app in _filtered.Skip(_page * _pageSize).Take(_pageSize))
            _tiles.Children.Add(Tile(app));
        var pages = Math.Max(1, (int)Math.Ceiling(_filtered.Count / (double)_pageSize));
        _pageLabel.Text = $"{_page + 1} / {pages}";
    }

    private Border Tile(PinnedApp app)
    {
        var stack = new StackPanel { Width = 128, Margin = new Thickness(6), VerticalAlignment = VerticalAlignment.Center };
        var icon = IconService.Load(app.IconPath) ?? IconService.Load(app.RealPath) ?? IconService.Load(app.Target);
        FrameworkElement visual = icon == null
            ? new TextBlock { Text = "◈", FontSize = 58, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center }
            : new Image { Source = icon, Width = 74, Height = 74, Stretch = Stretch.Uniform };
        stack.Children.Add(visual);
        stack.Children.Add(new TextBlock
        {
            Text = app.Name,
            FontSize = 12,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 120,
            Margin = new Thickness(0, 9, 0, 0),
            Effect = new DropShadowEffect { BlurRadius = 5, Opacity = 0.8 }
        });
        var tile = new Border
        {
            Child = stack,
            Background = Brushes.Transparent,
            CornerRadius = new CornerRadius(18),
            Cursor = Cursors.Hand,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new ScaleTransform(1, 1)
        };
        tile.MouseEnter += (_, _) =>
        {
            tile.Background = new SolidColorBrush(Color.FromArgb(35, 255, 255, 255));
            ((ScaleTransform)tile.RenderTransform).BeginAnimation(ScaleTransform.ScaleXProperty,
                new DoubleAnimation(1.09, TimeSpan.FromMilliseconds(130)));
            ((ScaleTransform)tile.RenderTransform).BeginAnimation(ScaleTransform.ScaleYProperty,
                new DoubleAnimation(1.09, TimeSpan.FromMilliseconds(130)));
        };
        tile.MouseLeave += (_, _) =>
        {
            tile.Background = Brushes.Transparent;
            ((ScaleTransform)tile.RenderTransform).BeginAnimation(ScaleTransform.ScaleXProperty,
                new DoubleAnimation(1, TimeSpan.FromMilliseconds(130)));
            ((ScaleTransform)tile.RenderTransform).BeginAnimation(ScaleTransform.ScaleYProperty,
                new DoubleAnimation(1, TimeSpan.FromMilliseconds(130)));
        };
        tile.MouseLeftButtonUp += (_, _) => Launch(app);
        return tile;
    }

    private Border PagerButton(string label, Action action)
    {
        var text = Ui.Text(label, 28);
        text.HorizontalAlignment = HorizontalAlignment.Center;
        var button = Glass.Surface(text, 20, _app.Settings.UseLiquidGlass);
        button.Width = 38;
        button.Height = 38;
        button.Cursor = Cursors.Hand;
        button.MouseLeftButtonUp += (_, _) => action();
        return button;
    }

    private void Launch(PinnedApp app)
    {
        _app.Launch(app.Target);
        Close();
    }
}
