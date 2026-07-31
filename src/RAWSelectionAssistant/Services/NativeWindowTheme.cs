using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace RAWSelectionAssistant.Services;

internal static class NativeWindowTheme
{
    private const int UseImmersiveDarkMode = 20;
    private const int BorderColor = 34;
    private const int CaptionColor = 35;
    private const int TextColor = 36;

    public static void ApplyAll(bool dark)
    {
        if (Application.Current is null) return;
        foreach (Window window in Application.Current.Windows)
        {
            Apply(window, dark);
        }
    }

    public static void Apply(Window window, bool dark)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;
        var enabled = dark ? 1 : 0;
        _ = DwmSetWindowAttribute(handle, UseImmersiveDarkMode, ref enabled, Marshal.SizeOf<int>());
        var border = dark ? ColorRef(35, 37, 42) : ColorRef(218, 221, 226);
        var caption = dark ? ColorRef(32, 33, 36) : ColorRef(248, 249, 251);
        var text = dark ? ColorRef(244, 244, 245) : ColorRef(31, 35, 41);
        _ = DwmSetWindowAttribute(handle, BorderColor, ref border, Marshal.SizeOf<int>());
        _ = DwmSetWindowAttribute(handle, CaptionColor, ref caption, Marshal.SizeOf<int>());
        _ = DwmSetWindowAttribute(handle, TextColor, ref text, Marshal.SizeOf<int>());
    }

    private static int ColorRef(byte red, byte green, byte blue) => red | green << 8 | blue << 16;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int valueSize);
}
