using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace MacOSDock;

internal sealed class SettingsWindow : Window
{
    public SettingsWindow(DockApp app)
    {
        Title = "MacOSDock Settings";
        Width = 430;
        Height = 540;
        MinWidth = 390;
        MinHeight = 510;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Foreground = Brushes.White;
        Topmost = true;
        ShowInTaskbar = false;

        var layout = new StackPanel { Margin = new Thickness(24) };
        var header = new DockPanel();
        var close = Ui.Button("✕", Close);
        DockPanel.SetDock(close, Dock.Right);
        header.Children.Add(close);
        header.Children.Add(Ui.Text("Dock settings", 22, FontWeights.SemiBold));
        layout.Children.Add(header);
        layout.Children.Add(new Border { Height = 14 });
        var sizeLabel = Ui.Text($"Icon size: {app.Settings.IconSize}", 13);
        layout.Children.Add(sizeLabel);
        var slider = new Slider
        {
            Minimum = 40,
            Maximum = 76,
            TickFrequency = 2,
            IsSnapToTickEnabled = true,
            Value = app.Settings.IconSize,
            Margin = new Thickness(0, 6, 0, 14)
        };
        slider.ValueChanged += (_, _) =>
        {
            app.Settings.IconSize = (int)slider.Value;
            sizeLabel.Text = $"Icon size: {app.Settings.IconSize}";
            app.SaveAndRefresh();
        };
        layout.Children.Add(slider);

        layout.Children.Add(Ui.Text("Dock position", 13));
        var positions = new ComboBox
        {
            ItemsSource = new[] { "Right", "Left", "Bottom" },
            SelectedItem = char.ToUpperInvariant(app.Settings.DockPosition[0]) + app.Settings.DockPosition[1..],
            Margin = new Thickness(0, 6, 0, 14),
            Height = 30
        };
        positions.SelectionChanged += (_, _) =>
        {
            if (positions.SelectedItem is string selected)
            {
                app.Settings.DockPosition = selected.ToLowerInvariant();
                app.SaveAndRefresh();
            }
        };
        layout.Children.Add(positions);

        var dockCheck = Check("Show dock", app.Settings.ShowDock);
        dockCheck.Checked += (_, _) => { app.Settings.ShowDock = true; app.SaveAndRefresh(); };
        dockCheck.Unchecked += (_, _) => { app.Settings.ShowDock = false; app.SaveAndRefresh(); };
        layout.Children.Add(dockCheck);
        var barCheck = Check("Show menu bar", app.Settings.ShowMenuBar);
        barCheck.Checked += (_, _) => { app.Settings.ShowMenuBar = true; app.SaveAndRefresh(); };
        barCheck.Unchecked += (_, _) => { app.Settings.ShowMenuBar = false; app.SaveAndRefresh(); };
        layout.Children.Add(barCheck);

        var runningCheck = Check("Show running apps", app.Settings.ShowRunningApps);
        runningCheck.Checked += (_, _) => { app.Settings.ShowRunningApps = true; app.SaveAndRefresh(); };
        runningCheck.Unchecked += (_, _) => { app.Settings.ShowRunningApps = false; app.SaveAndRefresh(); };
        layout.Children.Add(runningCheck);

        var glassCheck = Check("Liquid Glass appearance", app.Settings.UseLiquidGlass);
        glassCheck.Checked += (_, _) => { app.Settings.UseLiquidGlass = true; app.SaveAndRefresh(); };
        glassCheck.Unchecked += (_, _) => { app.Settings.UseLiquidGlass = false; app.SaveAndRefresh(); };
        layout.Children.Add(glassCheck);

        var autoHideCheck = Check("Automatically hide dock", app.Settings.AutoHideDock);
        autoHideCheck.Checked += (_, _) => { app.Settings.AutoHideDock = true; app.SaveAndRefresh(); };
        autoHideCheck.Unchecked += (_, _) => { app.Settings.AutoHideDock = false; app.SaveAndRefresh(); };
        layout.Children.Add(autoHideCheck);

        var taskbarCheck = Check("Hide Windows taskbar while running", app.Settings.HideWindowsTaskbar);
        taskbarCheck.Checked += (_, _) => { app.Settings.HideWindowsTaskbar = true; app.SaveAndRefresh(); };
        taskbarCheck.Unchecked += (_, _) => { app.Settings.HideWindowsTaskbar = false; app.SaveAndRefresh(); };
        layout.Children.Add(taskbarCheck);

        layout.Children.Add(new Border { Height = 13 });
        layout.Children.Add(Ui.Button("Import MyDockFinder layout…", () =>
        {
            using var folder = new Forms.FolderBrowserDialog
            {
                Description = "Choose the MyDockFinder folder containing ico.ini",
                SelectedPath = System.IO.Directory.Exists(@"E:\MyDockFinder") ? @"E:\MyDockFinder" : ""
            };
            if (folder.ShowDialog() != Forms.DialogResult.OK) return;
            var count = app.ImportMyDockFinder(folder.SelectedPath);
            MessageBox.Show(count > 0 ? $"Imported {count} pinned apps." : "No dock icons were found in ico.ini.", "MacOSDock");
        }));

        var note = Ui.Text("Drop an EXE or shortcut onto the dock to pin it.", 12);
        note.Foreground = Ui.Muted;
        note.Margin = new Thickness(0, 13, 0, 0);
        layout.Children.Add(note);
        var glass = Glass.Surface(layout, 25, app.Settings.UseLiquidGlass);
        Content = new Border { Child = glass, Padding = new Thickness(14) };
    }

    private static CheckBox Check(string label, bool value) => new()
    {
        Content = label,
        IsChecked = value,
        Foreground = Brushes.White,
        FontSize = 13,
        Margin = new Thickness(0, 4, 0, 4)
    };
}
