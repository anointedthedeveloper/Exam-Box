using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ExamBox.Admin;

/// <summary>Colours the native title bar to match the app (Windows 11; older Windows simply keeps its default bar).</summary>
internal static class TitleBar
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private const int BorderColor = 34, CaptionColor = 35, TextColor = 36;

    private static int ColorRef(byte r, byte g, byte b) => r | (g << 8) | (b << 16);

    public static void Apply(Window window)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;
            var blue = ColorRef(0x08, 0x47, 0xB8);
            var white = ColorRef(0xFF, 0xFF, 0xFF);
            DwmSetWindowAttribute(hwnd, CaptionColor, ref blue, sizeof(int));
            DwmSetWindowAttribute(hwnd, BorderColor, ref blue, sizeof(int));
            DwmSetWindowAttribute(hwnd, TextColor, ref white, sizeof(int));
        }
        catch { /* cosmetic only */ }
    }
}
