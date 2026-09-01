using System.Windows;
using RAWSelectionAssistant.Core.Models;

namespace PixelTart.Modules.AssetLibrary;

public sealed record AssetLayoutResult(IReadOnlyList<Rect> Items, Size Extent);

public static class AssetLayoutEngine
{
    private const double Gap = 8d;
    private const double CaptionHeight = 44d;

    public static AssetLayoutResult Arrange(
        AssetLibraryViewMode mode,
        IReadOnlyList<double> aspectRatios,
        double viewportWidth,
        double thumbnailWidth)
    {
        var width = double.IsFinite(viewportWidth) ? Math.Max(120d, viewportWidth) : 120d;
        var target = Math.Clamp(double.IsFinite(thumbnailWidth) ? thumbnailWidth : 180d, 120d, 280d);
        return mode switch
        {
            AssetLibraryViewMode.Masonry => ArrangeMasonry(aspectRatios, width, target),
            AssetLibraryViewMode.Justified => ArrangeJustified(aspectRatios, width, target),
            AssetLibraryViewMode.List => ArrangeList(aspectRatios.Count, width),
            _ => ArrangeGrid(aspectRatios.Count, width, target)
        };
    }

    private static AssetLayoutResult ArrangeGrid(int count, double width, double target)
    {
        var columns = Math.Max(1, (int)Math.Floor((width + Gap) / (target + Gap)));
        var itemWidth = Math.Max(96d, (width - Gap * (columns - 1)) / columns);
        var itemHeight = itemWidth + CaptionHeight;
        var items = new Rect[count];
        for (var index = 0; index < count; index++)
        {
            var row = index / columns;
            var column = index % columns;
            items[index] = new(column * (itemWidth + Gap), row * (itemHeight + Gap), itemWidth, itemHeight);
        }
        var rows = count == 0 ? 0 : (int)Math.Ceiling(count / (double)columns);
        return new(items, new(width, rows == 0 ? 0d : rows * itemHeight + (rows - 1) * Gap));
    }

    private static AssetLayoutResult ArrangeMasonry(IReadOnlyList<double> ratios, double width, double target)
    {
        var columns = Math.Max(1, (int)Math.Floor((width + Gap) / (target + Gap)));
        var itemWidth = Math.Max(96d, (width - Gap * (columns - 1)) / columns);
        var heights = new double[columns];
        var items = new Rect[ratios.Count];
        for (var index = 0; index < ratios.Count; index++)
        {
            var column = 0;
            for (var candidate = 1; candidate < columns; candidate++)
                if (heights[candidate] < heights[column]) column = candidate;
            var ratio = NormalizeRatio(ratios[index]);
            var itemHeight = Math.Clamp(itemWidth / ratio, 72d, itemWidth * 2.5d) + CaptionHeight;
            items[index] = new(column * (itemWidth + Gap), heights[column], itemWidth, itemHeight);
            heights[column] += itemHeight + Gap;
        }
        return new(items, new(width, Math.Max(0d, heights.DefaultIfEmpty(0d).Max() - Gap)));
    }

    private static AssetLayoutResult ArrangeJustified(IReadOnlyList<double> ratios, double width, double target)
    {
        var items = new Rect[ratios.Count];
        var targetImageHeight = Math.Clamp(target * 0.72d, 92d, 210d);
        var rowStart = 0;
        var y = 0d;
        while (rowStart < ratios.Count)
        {
            var ratioSum = 0d;
            var rowEnd = rowStart;
            while (rowEnd < ratios.Count)
            {
                ratioSum += NormalizeRatio(ratios[rowEnd]);
                rowEnd++;
                if (ratioSum * targetImageHeight + Gap * (rowEnd - rowStart - 1) >= width) break;
            }
            var itemCount = rowEnd - rowStart;
            var isLast = rowEnd == ratios.Count;
            var imageHeight = isLast
                ? Math.Min(targetImageHeight, (width - Gap * (itemCount - 1)) / Math.Max(0.2d, ratioSum))
                : (width - Gap * (itemCount - 1)) / Math.Max(0.2d, ratioSum);
            imageHeight = Math.Clamp(imageHeight, 72d, targetImageHeight * 1.45d);
            var x = 0d;
            for (var index = rowStart; index < rowEnd; index++)
            {
                var remaining = width - x;
                var itemWidth = index == rowEnd - 1 && !isLast
                    ? remaining
                    : Math.Min(remaining, NormalizeRatio(ratios[index]) * imageHeight);
                items[index] = new(x, y, Math.Max(1d, itemWidth), imageHeight + CaptionHeight);
                x += itemWidth + Gap;
            }
            y += imageHeight + CaptionHeight + Gap;
            rowStart = rowEnd;
        }
        return new(items, new(width, Math.Max(0d, y - Gap)));
    }

    private static AssetLayoutResult ArrangeList(int count, double width)
    {
        const double rowHeight = 68d;
        var items = new Rect[count];
        for (var index = 0; index < count; index++) items[index] = new(0d, index * rowHeight, width, rowHeight - 2d);
        return new(items, new(width, count * rowHeight));
    }

    private static double NormalizeRatio(double ratio) => double.IsFinite(ratio) ? Math.Clamp(ratio, 0.2d, 5d) : 1.5d;
}
