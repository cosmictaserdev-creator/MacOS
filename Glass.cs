using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace MacOSDock;

internal static class Glass
{
    private static readonly Brush ClearTint = new LinearGradientBrush(
        new GradientStopCollection
        {
            new(Color.FromArgb(96, 248, 252, 255), 0),
            new(Color.FromArgb(34, 191, 212, 242), 0.28),
            new(Color.FromArgb(62, 21, 34, 55), 1)
        }, new Point(0, 0), new Point(1, 1));

    private static readonly Brush DarkTint = new SolidColorBrush(Color.FromArgb(230, 24, 28, 40));
    private static readonly Brush Rim = new LinearGradientBrush(
        new GradientStopCollection
        {
            new(Color.FromArgb(233, 255, 255, 255), 0),
            new(Color.FromArgb(115, 246, 252, 255), 0.37),
            new(Color.FromArgb(38, 227, 241, 255), 0.68),
            new(Color.FromArgb(171, 255, 255, 255), 1)
        }, new Point(0, 0), new Point(1, 1));

    public static Border Surface(UIElement content, double radius, bool enabled = true, bool live = false)
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
            Effect = new DropShadowEffect { BlurRadius = 22, ShadowDepth = 7, Opacity = 0.31, Color = Colors.Black }
        };

        Window? owner = null;
        var refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
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
                var source = live ? LiveBackdrop.TryCapture(owner, bounds) : null;
                source ??= WallpaperBackdrop.Render(bounds);
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
            if (owner != null && !live) owner.LocationChanged += (_, _) => Refresh();
            Refresh();
            if (live) refreshTimer.Start();
        };
        refreshTimer.Tick += (_, _) => { if (owner?.IsVisible == true) Refresh(); };
        pane.Unloaded += (_, _) => refreshTimer.Stop();
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
