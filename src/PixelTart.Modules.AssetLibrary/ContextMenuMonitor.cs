using System.Runtime.InteropServices;
using System.Windows;

namespace PixelTart.Modules.AssetLibrary;

public static class ContextMenuMonitor
{
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct Bounds { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public Bounds Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern nint MonitorFromPoint(Point point, uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    public static Rect WorkArea(System.Windows.Point origin)
    {
        var monitor = MonitorFromPoint(new Point { X = (int)origin.X, Y = (int)origin.Y }, 2);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info)) throw new InvalidOperationException("Cannot locate context menu monitor.");
        return new(info.Work.Left, info.Work.Top, info.Work.Right - info.Work.Left, info.Work.Bottom - info.Work.Top);
    }

    public static Rect OwnerWorkArea(Window owner)
    {
        var center = owner.PointToScreen(new System.Windows.Point(owner.ActualWidth / 2, owner.ActualHeight / 2));
        var pixels = WorkArea(center);
        var transform = PresentationSource.FromVisual(owner)?.CompositionTarget?.TransformFromDevice
            ?? System.Windows.Media.Matrix.Identity;
        return Rect.Transform(pixels, transform);
    }

    public static Rect InspectionBounds(Rect owner, Rect work, Size preferred)
    {
        var width = Math.Min(preferred.Width, work.Width);
        var height = Math.Min(preferred.Height, work.Height);
        return new Rect(Math.Clamp(owner.Right - width - 16, work.Left, work.Left + (work.Width - width)),
            Math.Clamp(owner.Top + 100, work.Top, work.Top + (work.Height - height)), width, height);
    }
}
