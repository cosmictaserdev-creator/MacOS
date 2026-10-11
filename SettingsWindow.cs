using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MacOSDock;

internal sealed class SettingsWindow : Window
{
    public SettingsWindow(DockApp app)
    {
        Title = "MacOSDock Settings";
        Width = 400;
        Height = 290;
        MinWidth = 360;
        MinHeight = 270;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(28, 32, 43));
        Foreground = Brushes.White;
        Topmost = true;
        ShowInTaskbar = false;

        var layout = new StackPanel { Margin = new Thickness(24) };
        layout.Children.Add(Ui.Text("Dock settings", 22, FontWeights.SemiBold));
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

        var dockCheck = Check("Show dock", app.Settings.ShowDock);
        dockCheck.Checked += (_, _) => { app.Settings.ShowDock = true; app.SaveAndRefresh(); };
        dockCheck.Unchecked += (_, _) => { app.Settings.ShowDock = false; app.SaveAndRefresh(); };
        layout.Children.Add(dockCheck);
        var barCheck = Check("Show menu bar", app.Settings.ShowMenuBar);
        barCheck.Checked += (_, _) => { app.Settings.ShowMenuBar = true; app.SaveAndRefresh(); };
        barCheck.Unchecked += (_, _) => { app.Settings.ShowMenuBar = false; app.SaveAndRefresh(); };
        layout.Children.Add(barCheck);

        var note = Ui.Text("Drop an EXE or shortcut onto the dock to pin it.", 12);
        note.Foreground = Ui.Muted;
        note.Margin = new Thickness(0, 19, 0, 0);
        layout.Children.Add(note);
        Content = layout;
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
