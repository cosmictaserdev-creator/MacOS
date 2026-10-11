using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MacOSDock;

internal static class LiveBackdrop
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowDisplayAffinity(IntPtr window, uint affinity);
    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr handle);

    public static BitmapSource? TryCapture(Window owner, Rect screenBounds)
    {
        if (Environment.OSVersion.Version.Build < 19041) return null;
        var handle = new WindowInteropHelper(owner).Handle;
        if (handle == IntPtr.Zero) return null;
        var source = PresentationSource.FromVisual(owner);
        var transform = source?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
        var x = (int)Math.Round(screenBounds.X * transform.M11);
        var y = (int)Math.Round(screenBounds.Y * transform.M22);
        var width = (int)Math.Ceiling(screenBounds.Width * transform.M11);
        var height = (int)Math.Ceiling(screenBounds.Height * transform.M22);
        var screen = System.Windows.Forms.Screen.PrimaryScreen?.Bounds;
        if (screen == null || width < 2 || height < 2 || x < screen.Value.Left || y < screen.Value.Top ||
            x + width > screen.Value.Right || y + height > screen.Value.Bottom)
            return null;

        // The window is excluded only during this capture. The shader does all blur and refraction.
        if (!SetWindowDisplayAffinity(handle, 0x11)) return null;
        try
        {
            using var bitmap = new System.Drawing.Bitmap(width, height);
            using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
                graphics.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(width, height));

            var center = bitmap.GetPixel(width / 2, height / 2);
            if (center.R < 3 && center.G < 3 && center.B < 3) return null;

            var hbitmap = bitmap.GetHbitmap();
            try
            {
                var image = Imaging.CreateBitmapSourceFromHBitmap(
                    hbitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                image.Freeze();
                return image;
            }
            finally { DeleteObject(hbitmap); }
        }
        catch (Exception) { return null; }
        finally { SetWindowDisplayAffinity(handle, 0); }
    }
}
