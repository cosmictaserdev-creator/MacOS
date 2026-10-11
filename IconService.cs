using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MacOSDock;

internal static class IconService
{
    private const uint ShgfiIcon = 0x100;
    private const uint ShgfiLargeIcon = 0;
    private static readonly Dictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFileInfo
    {
        public IntPtr Icon;
        public int IconIndex;
        public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string TypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string path, uint attributes, ref SHFileInfo info, uint size, uint flags);
    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr icon);

    public static ImageSource? Load(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || (!File.Exists(path) && !Directory.Exists(path))) return null;
        if (Cache.TryGetValue(path, out var cached)) return cached;
        ImageSource? source = null;
        try
        {
            if (Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase) ||
                Path.GetExtension(path).Equals(".ico", StringComparison.OrdinalIgnoreCase))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(path);
                bitmap.DecodePixelWidth = 128;
                bitmap.EndInit();
                bitmap.Freeze();
                source = bitmap;
            }
            else
            {
                var info = new SHFileInfo();
                SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf<SHFileInfo>(), ShgfiIcon | ShgfiLargeIcon);
                if (info.Icon != IntPtr.Zero)
                {
                    try
                    {
                        var bitmap = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                            info.Icon, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(96, 96));
                        bitmap.Freeze();
                        source = bitmap;
                    }
                    finally { DestroyIcon(info.Icon); }
                }
            }
        }
        catch (Exception) { }
        Cache[path] = source;
        return source;
    }
}
