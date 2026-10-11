using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace MacOSDock;

internal static class WallpaperBackdrop
{
    private static string? _cachedPath;
    private static BitmapSource? _cachedImage;

    public static BitmapSource Render(Rect screenBounds)
    {
        var width = Math.Clamp((int)Math.Ceiling(screenBounds.Width), 1, 2048);
        var height = Math.Clamp((int)Math.Ceiling(screenBounds.Height), 1, 2048);
        var target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            var image = CurrentWallpaper();
            if (image != null)
            {
                var screenWidth = SystemParameters.PrimaryScreenWidth;
                var screenHeight = SystemParameters.PrimaryScreenHeight;
                var scale = Math.Max(screenWidth / image.PixelWidth, screenHeight / image.PixelHeight);
                var displayWidth = image.PixelWidth * scale;
                var displayHeight = image.PixelHeight * scale;
                var left = (screenWidth - displayWidth) / 2 - screenBounds.X;
                var top = (screenHeight - displayHeight) / 2 - screenBounds.Y;
                drawing.DrawImage(image, new Rect(left, top, displayWidth, displayHeight));
            }
            else
            {
                drawing.DrawRectangle(new LinearGradientBrush(
                    Color.FromRgb(63, 78, 105), Color.FromRgb(31, 37, 59), 45),
                    null, new Rect(0, 0, width, height));
            }
        }
        target.Render(visual);
        target.Freeze();
        return target;
    }

    private static BitmapSource? CurrentWallpaper()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
            var path = key?.GetValue("WallPaper") as string;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            if (path == _cachedPath && _cachedImage != null) return _cachedImage;
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path);
            image.EndInit();
            image.Freeze();
            _cachedPath = path;
            _cachedImage = image;
            return image;
        }
        catch (Exception) { return null; }
    }
}
