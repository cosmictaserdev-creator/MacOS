using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace MacOSDock;

internal static class Glass
{
    private static readonly Brush ClearTint = new LinearGradientBrush(
        new GradientStopCollection
        {
            new(Color.FromArgb(70, 245, 249, 255), 0),
            new(Color.FromArgb(45, 160, 177, 206), 0.35),
            new(Color.FromArgb(68, 31, 41, 61), 1)
        }, new Point(0, 0), new Point(1, 1));

    private static readonly Brush DarkTint = new SolidColorBrush(Color.FromArgb(230, 24, 28, 40));
    private static readonly Brush Rim = new LinearGradientBrush(
        new GradientStopCollection
        {
            new(Color.FromArgb(204, 255, 255, 255), 0),
            new(Color.FromArgb(98, 255, 255, 255), 0.43),
            new(Color.FromArgb(35, 240, 248, 255), 0.75),
            new(Color.FromArgb(125, 255, 255, 255), 1)
        }, new Point(0, 0), new Point(1, 1));

    public static Border Surface(UIElement content, double radius, bool enabled = true)
    {
        var backdrop = new Rectangle
        {
            RadiusX = radius,
            RadiusY = radius,
            IsHitTestVisible = false,
            Visibility = enabled ? Visibility.Visible : Visibility.Collapsed
        };
        var grid = new Grid();
        grid.Children.Add(backdrop);
        grid.Children.Add(content);
        var pane = new Border
        {
            Child = grid,
            Tag = backdrop,
            Background = enabled ? ClearTint : DarkTint,
            BorderBrush = enabled ? Rim : new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(radius),
            Effect = new DropShadowEffect { BlurRadius = 28, ShadowDepth = 9, Opacity = 0.4, Color = Colors.Black }
        };

        Window? owner = null;
        void Refresh()
        {
            if (backdrop.Visibility != Visibility.Visible || pane.ActualWidth < 2 || pane.ActualHeight < 2) return;
            owner ??= Window.GetWindow(pane);
            if (owner == null) return;
            try
            {
                var point = pane.TranslatePoint(new Point(0, 0), owner);
                var bounds = new Rect(owner.Left + point.X, owner.Top + point.Y,
                    pane.ActualWidth, pane.ActualHeight);
                var source = WallpaperBackdrop.Render(bounds);
                backdrop.Fill = new ImageBrush(source) { Stretch = Stretch.Fill };
                backdrop.Effect = new LiquidGlassEffect
                {
                    PixelSize = new Point(1 / pane.ActualWidth, 1 / pane.ActualHeight)
                };
            }
            catch (Exception)
            {
                backdrop.Fill = new SolidColorBrush(Color.FromArgb(125, 92, 109, 137));
                backdrop.Effect = null;
            }
        }

        pane.Loaded += (_, _) =>
        {
            owner = Window.GetWindow(pane);
            if (owner != null) owner.LocationChanged += (_, _) => Refresh();
            Refresh();
        };
        pane.SizeChanged += (_, _) => Refresh();
        return pane;
    }

    public static void SetEnabled(Border surface, bool enabled)
    {
        surface.Background = enabled ? ClearTint : DarkTint;
        surface.BorderBrush = enabled ? Rim : new SolidColorBrush(Color.FromArgb(70, 255, 255, 255));
        if (surface.Tag is Rectangle backdrop)
            backdrop.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
    }
}
