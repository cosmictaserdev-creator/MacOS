using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MacOSDock;

internal sealed class SearchWindow : Window
{
    private readonly DockApp _app;
    private readonly TextBox _query = new();
    private readonly ListBox _results = new();
    private List<PinnedApp> _apps = [];

    public SearchWindow(DockApp app)
    {
        _app = app;
        Title = "Search apps";
        Width = 520;
        Height = 440;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(28, 32, 43));
        Foreground = Brushes.White;
        Topmost = true;
        ShowInTaskbar = false;

        var layout = new DockPanel { Margin = new Thickness(18) };
        _query.FontSize = 18;
        _query.Padding = new Thickness(10);
        _query.Background = Ui.GlassLight;
        _query.Foreground = Brushes.White;
        _query.BorderThickness = new Thickness(0);
        _query.TextChanged += (_, _) => Filter();
        _query.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) LaunchSelection();
            if (e.Key == Key.Escape) Close();
            if (e.Key == Key.Down && _results.Items.Count > 0)
            {
                _results.SelectedIndex = 0;
                _results.Focus();
            }
        };
        DockPanel.SetDock(_query, Dock.Top);
        layout.Children.Add(_query);

        _results.Margin = new Thickness(0, 12, 0, 0);
        _results.Background = Brushes.Transparent;
        _results.Foreground = Brushes.White;
        _results.BorderThickness = new Thickness(0);
        _results.MouseDoubleClick += (_, _) => LaunchSelection();
        _results.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) LaunchSelection();
            if (e.Key == Key.Escape) Close();
        };
        layout.Children.Add(_results);
        Content = layout;
        Loaded += async (_, _) =>
        {
            FocusSearch();
            _apps = await Task.Run(FindApps);
            Filter();
        };
    }

    public void FocusSearch()
    {
        _query.Focus();
        _query.SelectAll();
    }

    private List<PinnedApp> FindApps()
    {
        var result = new List<PinnedApp>(_app.Settings.Pinned);
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
        _results.Items.Clear();
        var query = _query.Text.Trim();
        foreach (var app in _apps
                     .Where(a => query.Length == 0 || a.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                     .Take(40))
        {
            var item = new ListBoxItem { Content = app.Name, Tag = app.Target, FontSize = 14, Padding = new Thickness(9) };
            _results.Items.Add(item);
        }
        if (_results.Items.Count > 0) _results.SelectedIndex = 0;
    }

    private void LaunchSelection()
    {
        if (_results.SelectedItem is not ListBoxItem item || item.Tag is not string target) return;
        _app.Launch(target);
        Close();
    }
}
