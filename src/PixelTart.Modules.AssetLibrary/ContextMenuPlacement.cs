using System.Windows;

namespace PixelTart.Modules.AssetLibrary;

public readonly record struct ContextMenuPlacementResult(double Left, double Top, bool OpensLeft);

public static class ContextMenuPlacement
{
    public static ContextMenuPlacementResult Calculate(Rect parent, Size submenu, Rect workArea)
    {
        var opensLeft = parent.Right + submenu.Width > workArea.Right && parent.Left - submenu.Width >= workArea.Left;
        var left = opensLeft ? parent.Left - submenu.Width : parent.Right;
        var top = Math.Clamp(parent.Top, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - submenu.Height));
        return new(left, top, opensLeft);
    }
}
