using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace MacOSDock;

internal sealed record RunningWindow(IntPtr Handle, string Title, string ProcessName, string? ExecutablePath);

internal static class Windows
{
    private delegate bool EnumWindowsProc(IntPtr handle, IntPtr data);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr data);
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr handle);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr handle, StringBuilder text, int maxCount);
    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr handle);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);
    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowLongPtr(IntPtr handle, int index);
    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr handle);
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr handle, int command);
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr handle);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr handle, StringBuilder name, int count);
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr handle, int attribute, out int value, int size);
    [DllImport("user32.dll")]
    private static extern void keybd_event(byte virtualKey, byte scan, uint flags, UIntPtr extraInfo);

    private static readonly HashSet<IntPtr> HiddenTaskbars = [];

    public static List<RunningWindow> GetRunningWindows()
    {
        var result = new List<RunningWindow>();
        var currentPid = Environment.ProcessId;
        EnumWindows((handle, _) =>
        {
            if (!IsWindowVisible(handle) || GetWindowTextLength(handle) <= 0)
                return true;
            if (((long)GetWindowLongPtr(handle, -20) & 0x80) != 0) // WS_EX_TOOLWINDOW
                return true;
            if (DwmGetWindowAttribute(handle, 14, out var cloaked, sizeof(int)) == 0 && cloaked != 0)
                return true;

            GetWindowThreadProcessId(handle, out var pid);
            if (pid == 0 || pid == currentPid)
                return true;

            try
            {
                using var process = Process.GetProcessById((int)pid);
                var text = new StringBuilder(Math.Min(GetWindowTextLength(handle) + 1, 1024));
                GetWindowText(handle, text, text.Capacity);
                string? path = null;
                try { path = process.MainModule?.FileName; } catch (Exception) { }
                if (text.Length > 0 && text.ToString() != "Program Manager")
                    result.Add(new RunningWindow(handle, text.ToString(), process.ProcessName, path));
            }
            catch (Exception) { }
            return true;
        }, IntPtr.Zero);
        return result;
    }

    public static void Activate(IntPtr handle)
    {
        if (IsIconic(handle)) ShowWindow(handle, 9); // SW_RESTORE
        SetForegroundWindow(handle);
    }

    public static IntPtr ForegroundWindow => GetForegroundWindow();

    public static void SetTaskbarHidden(bool hidden)
    {
        if (!hidden)
        {
            foreach (var handle in HiddenTaskbars) ShowWindow(handle, 5); // SW_SHOW
            HiddenTaskbars.Clear();
            return;
        }
        EnumWindows((handle, _) =>
        {
            var name = new StringBuilder(64);
            GetClassName(handle, name, name.Capacity);
            if ((name.ToString() == "Shell_TrayWnd" || name.ToString() == "Shell_SecondaryTrayWnd") &&
                IsWindowVisible(handle) && HiddenTaskbars.Add(handle))
                ShowWindow(handle, 0); // SW_HIDE
            return true;
        }, IntPtr.Zero);
    }

    public static void ShowTaskView()
    {
        keybd_event(0x5B, 0, 0, UIntPtr.Zero); // Win down
        keybd_event(0x09, 0, 0, UIntPtr.Zero); // Tab down
        keybd_event(0x09, 0, 2, UIntPtr.Zero);
        keybd_event(0x5B, 0, 2, UIntPtr.Zero);
    }
}
