using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MacOSDock;

internal static class Ui
{
    public static readonly Brush Glass = new SolidColorBrush(Color.FromArgb(224, 21, 25, 36));
    public static readonly Brush GlassLight = new SolidColorBrush(Color.FromArgb(180, 52, 59, 76));
    public static readonly Brush White = Brushes.White;
    public static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(184, 190, 207));

    public static TextBlock Text(string text, double size = 13, FontWeight? weight = null) => new()
    {
        Text = text,
        FontFamily = new FontFamily("Segoe UI"),
        FontSize = size,
        FontWeight = weight ?? FontWeights.Normal,
        Foreground = White,
        VerticalAlignment = VerticalAlignment.Center
    };

    public static Border Card(UIElement child, double radius = 18, Brush? background = null) => new()
    {
        Background = background ?? Glass,
        BorderBrush = new SolidColorBrush(Color.FromArgb(65, 255, 255, 255)),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(radius),
        Child = child
    };

    public static Button Button(string label, Action action)
    {
        var button = new Button
        {
            Content = label,
            Padding = new Thickness(12, 7, 12, 7),
            Margin = new Thickness(3, 0, 3, 0),
            Background = GlassLight,
            Foreground = White,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        button.Click += (_, _) => action();
        return button;
    }
}
