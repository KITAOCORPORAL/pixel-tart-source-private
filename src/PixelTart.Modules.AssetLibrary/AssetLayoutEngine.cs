using System.Windows;
using RAWSelectionAssistant.Core.Models;

namespace PixelTart.Modules.AssetLibrary;

public sealed record AssetLayoutResult(IReadOnlyList<Rect> Items, Size Extent);

public static class AssetLayoutEngine
{
    private const double Gap = 8d;
    private const double CaptionHeight = 40d;

    public static AssetLayoutResult Arrange(
        AssetLibraryViewMode mode,
        IReadOnlyList<double> aspectRatios,
        double viewportWidth,
        double thumbnailWidth,
        double? thumbnailMaximumWidth = null)
    {
        var width = double.IsFinite(viewportWidth) ? Math.Max(120d, viewportWidth) : 120d;
        var target = Math.Clamp(double.IsFinite(thumbnailWidth) ? thumbnailWidth : 180d, 120d, width);
        // The slider belongs to the outer gallery; the content viewport can lose
        // width to its scrollbar. Normalize against the actual slider endpoint.
        var maximum = thumbnailMaximumWidth is > 120 && double.IsFinite(thumbnailMaximumWidth.Value)
            ? thumbnailMaximumWidth.Value : Math.Max(120, width - 24);
        var progress = Math.Clamp((thumbnailWidth - 120) / Math.Max(1, maximum - 120), 0, 1);
        target = double.IsFinite(progress) ? 120 + progress * (width - 120) : target;
        return mode switch
        {
            AssetLibraryViewMode.Masonry => ArrangeMasonry(aspectRatios, width, target),
            AssetLibraryViewMode.Justified => ArrangeJustified(aspectRatios, width, target),
            AssetLibraryViewMode.List => ArrangeList(aspectRatios.Count, width),
            _ => ArrangeGrid(aspectRatios, width, target)
        };
    }

    private static AssetLayoutResult ArrangeGrid(IReadOnlyList<double> ratios, double width, double target)
    {
        var columns = Math.Max(1, (int)Math.Floor((width + Gap) / (target + Gap)));
        var itemWidth = target;
        var items = new Rect[ratios.Count];
        var y = 0d;
        for (var rowStart = 0; rowStart < ratios.Count; rowStart += columns)
        {
            var rowEnd = Math.Min(ratios.Count, rowStart + columns);
            var rowHeight = 0d;
            for (var index = rowStart; index < rowEnd; index++)
            {
                var itemHeight = itemWidth / NormalizeRatio(ratios[index]) + CaptionHeight;
                items[index] = new((index - rowStart) * (itemWidth + Gap), y, itemWidth, itemHeight);
                rowHeight = Math.Max(rowHeight, itemHeight);
            }
            y += rowHeight + Gap;
        }
        return new(items, new(width, Math.Max(0d, y - Gap)));
    }

    private static AssetLayoutResult ArrangeMasonry(IReadOnlyList<double> ratios, double width, double target)
    {
        var columns = Math.Max(1, (int)Math.Floor((width + Gap) / (target + Gap)));
        var itemWidth = target;
        var heights = new double[columns];
        var items = new Rect[ratios.Count];
        for (var index = 0; index < ratios.Count; index++)
        {
            var column = 0;
            for (var candidate = 1; candidate < columns; candidate++)
                if (heights[candidate] < heights[column]) column = candidate;
            var ratio = NormalizeRatio(ratios[index]);
            var itemHeight = itemWidth / ratio + CaptionHeight;
            items[index] = new(column * (itemWidth + Gap), heights[column], itemWidth, itemHeight);
            heights[column] += itemHeight + Gap;
        }
        return new(items, new(width, Math.Max(0d, heights.DefaultIfEmpty(0d).Max() - Gap)));
    }

    private static AssetLayoutResult ArrangeJustified(IReadOnlyList<double> ratios, double width, double target)
    {
        var items = new Rect[ratios.Count];
        // Blend equal-height small previews into a full-width inspection card.
        // No integer column rounding or fixed height cap may consume slider travel.
        var largestRatio = ratios.Select(NormalizeRatio).DefaultIfEmpty(1.5d).Max();
        var progress = Math.Clamp((target - 120) / Math.Max(1, width - 120), 0, 1);
        var x = 0d;
        var y = 0d;
        var rowHeight = 0d;
        for (var index = 0; index < ratios.Count; index++)
        {
            var ratio = NormalizeRatio(ratios[index]);
            var minimum = 120 * ratio / largestRatio;
            var itemWidth = minimum + (width - minimum) * progress;
            var height = itemWidth / ratio + CaptionHeight;
            if (x > 0 && x + itemWidth > width + .001) { x = 0; y += rowHeight + Gap; rowHeight = 0; }
            items[index] = new(x, y, itemWidth, height);
            rowHeight = Math.Max(rowHeight, height);
            x += itemWidth + Gap;
        }
        if (ratios.Count > 0) y += rowHeight + Gap;
        return new(items, new(width, Math.Max(0d, y - Gap)));
    }

    private static AssetLayoutResult ArrangeList(int count, double width)
    {
        const double rowHeight = 68d;
        var items = new Rect[count];
        for (var index = 0; index < count; index++) items[index] = new(0d, index * rowHeight, width, rowHeight - 2d);
        return new(items, new(width, count * rowHeight));
    }

    private static double NormalizeRatio(double ratio) => double.IsFinite(ratio) ? Math.Clamp(ratio, 0.01d, 100d) : 1.5d;
}
