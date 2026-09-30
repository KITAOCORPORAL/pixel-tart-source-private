using System.Runtime.InteropServices;

namespace PixelTart.NativeAcceptance;

internal static class Win32
{
    [StructLayout(LayoutKind.Sequential)] internal readonly record struct POINT(int X, int Y);
    [StructLayout(LayoutKind.Sequential)] internal struct RECT { internal int Left, Top, Right, Bottom; internal readonly ScreenBounds Bounds => new(Left, Top, Right - Left, Bottom - Top); }
    [StructLayout(LayoutKind.Explicit, Size = 40)] internal struct INPUT
    { [FieldOffset(0)] public uint Type; [FieldOffset(8)] public MOUSEINPUT Mouse; [FieldOffset(8)] public KEYBDINPUT Key; }
    [StructLayout(LayoutKind.Sequential)] internal struct MOUSEINPUT { public int X, Y; public uint Data, Flags, Time; public nint Extra; }
    [StructLayout(LayoutKind.Sequential)] internal struct KEYBDINPUT { public ushort Vk, Scan; public uint Flags, Time; public nint Extra; }
    [StructLayout(LayoutKind.Sequential)] internal struct BITMAPINFO
    { public uint Size; public int Width, Height; public ushort Planes, BitCount; public uint Compression, SizeImage; public int Xppm, Yppm; public uint Used, Important; }
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GetWindowRect(nint hwnd, out RECT bounds);
    [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern nint WindowFromPoint(POINT point);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GetCursorPos(out POINT point);
    [DllImport("user32.dll")] internal static extern nint GetAncestor(nint hwnd, uint flags);
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(nint hwnd);
    [DllImport("user32.dll")] internal static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll", SetLastError = true)] internal static extern uint SendInput(uint count, INPUT[] inputs, int size);
    [DllImport("user32.dll")] internal static extern nint SetThreadDpiAwarenessContext(nint context);
    [DllImport("user32.dll")] internal static extern nint GetWindowDC(nint hwnd);
    [DllImport("user32.dll")] internal static extern int ReleaseDC(nint hwnd, nint dc);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool PrintWindow(nint hwnd, nint dc, uint flags);
    [DllImport("gdi32.dll")] internal static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll")] internal static extern nint CreateCompatibleBitmap(nint dc, int width, int height);
    [DllImport("gdi32.dll")] internal static extern nint SelectObject(nint dc, nint obj);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool DeleteObject(nint obj);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool DeleteDC(nint dc);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool BitBlt(nint dest, int x, int y, int width, int height, nint source, int sx, int sy, uint operation);
    [DllImport("gdi32.dll")] internal static extern int GetDIBits(nint dc, nint bitmap, uint start, uint lines, byte[] data, ref BITMAPINFO info, uint usage);
}
