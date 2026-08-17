using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace PixelTart.Modules.AssetLibrary;

public sealed class VirtualizingWrapPanel : VirtualizingPanel, IScrollInfo
{
    public static readonly DependencyProperty ItemWidthProperty = DependencyProperty.Register(nameof(ItemWidth), typeof(double), typeof(VirtualizingWrapPanel), new FrameworkPropertyMetadata(180d, FrameworkPropertyMetadataOptions.AffectsMeasure));
    public static readonly DependencyProperty ItemHeightProperty = DependencyProperty.Register(nameof(ItemHeight), typeof(double), typeof(VirtualizingWrapPanel), new FrameworkPropertyMetadata(166d, FrameworkPropertyMetadataOptions.AffectsMeasure));
    private Size _extent; private Size _viewport; private Point _offset;

    public double ItemWidth { get => (double)GetValue(ItemWidthProperty); set => SetValue(ItemWidthProperty, value); }
    public double ItemHeight { get => (double)GetValue(ItemHeightProperty); set => SetValue(ItemHeightProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        var owner = ItemsControl.GetItemsOwner(this); var count = owner?.Items.Count ?? 0;
        var width = double.IsInfinity(availableSize.Width) ? Math.Max(ItemWidth, ActualWidth) : availableSize.Width;
        var height = double.IsInfinity(availableSize.Height) ? Math.Max(ItemHeight, ActualHeight) : availableSize.Height;
        var columns = Math.Max(1, (int)Math.Floor(width / ItemWidth)); var rows = (int)Math.Ceiling(count / (double)columns);
        _viewport = new(width, height); _extent = new(width, rows * ItemHeight); ClampOffset(); ScrollOwner?.InvalidateScrollInfo();
        var firstRow = Math.Max(0, (int)Math.Floor(VerticalOffset / ItemHeight)); var visibleRows = Math.Max(1, (int)Math.Ceiling(height / ItemHeight) + 1); var firstIndex = firstRow * columns; var lastIndex = Math.Min(count - 1, (firstRow + visibleRows) * columns - 1);
        if (count == 0) { CleanUp(0, -1); return new(width, height); }
        var generator = ItemContainerGenerator;
        if (generator is null) return new(width, height);
        var start = generator.GeneratorPositionFromIndex(firstIndex); var childIndex = start.Offset == 0 ? start.Index : start.Index + 1;
        using (generator.StartAt(start, GeneratorDirection.Forward, true))
        {
            for (var itemIndex = firstIndex; itemIndex <= lastIndex; itemIndex++, childIndex++)
            {
                var child = (UIElement)generator.GenerateNext(out var newlyRealized)!;
                if (newlyRealized) { if (childIndex >= InternalChildren.Count) AddInternalChild(child); else InsertInternalChild(childIndex, child); generator.PrepareItemContainer(child); }
                child.Measure(new(ItemWidth, ItemHeight));
            }
        }
        CleanUp(firstIndex, lastIndex);
        return new(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var owner = ItemsControl.GetItemsOwner(this); var count = owner?.Items.Count ?? 0; var columns = Math.Max(1, (int)Math.Floor(finalSize.Width / ItemWidth));
        for (var childIndex = 0; childIndex < InternalChildren.Count; childIndex++)
        {
            var itemIndex = ItemContainerGenerator.IndexFromGeneratorPosition(new(childIndex, 0));
            if (itemIndex < 0 || itemIndex >= count) continue;
            var row = itemIndex / columns; var column = itemIndex % columns;
            InternalChildren[childIndex].Arrange(new(column * ItemWidth, row * ItemHeight - VerticalOffset, ItemWidth, ItemHeight));
        }
        return finalSize;
    }

    private void CleanUp(int firstIndex, int lastIndex)
    {
        for (var childIndex = InternalChildren.Count - 1; childIndex >= 0; childIndex--)
        {
            var itemIndex = ItemContainerGenerator.IndexFromGeneratorPosition(new(childIndex, 0));
            if (itemIndex >= firstIndex && itemIndex <= lastIndex) continue;
            ItemContainerGenerator.Remove(new(childIndex, 0), 1); RemoveInternalChildRange(childIndex, 1);
        }
    }

    private void ClampOffset() { _offset.X = Math.Clamp(_offset.X, 0, Math.Max(0, ExtentWidth - ViewportWidth)); _offset.Y = Math.Clamp(_offset.Y, 0, Math.Max(0, ExtentHeight - ViewportHeight)); }
    public bool CanHorizontallyScroll { get; set; } public bool CanVerticallyScroll { get; set; } = true;
    public double ExtentWidth => _extent.Width; public double ExtentHeight => _extent.Height; public double ViewportWidth => _viewport.Width; public double ViewportHeight => _viewport.Height; public double HorizontalOffset => _offset.X; public double VerticalOffset => _offset.Y; public ScrollViewer? ScrollOwner { get; set; }
    public void LineUp() => SetVerticalOffset(VerticalOffset - 24); public void LineDown() => SetVerticalOffset(VerticalOffset + 24); public void LineLeft() => SetHorizontalOffset(HorizontalOffset - 24); public void LineRight() => SetHorizontalOffset(HorizontalOffset + 24);
    public void MouseWheelUp() => SetVerticalOffset(VerticalOffset - 72); public void MouseWheelDown() => SetVerticalOffset(VerticalOffset + 72); public void MouseWheelLeft() => SetHorizontalOffset(HorizontalOffset - 72); public void MouseWheelRight() => SetHorizontalOffset(HorizontalOffset + 72);
    public void PageUp() => SetVerticalOffset(VerticalOffset - ViewportHeight); public void PageDown() => SetVerticalOffset(VerticalOffset + ViewportHeight); public void PageLeft() => SetHorizontalOffset(HorizontalOffset - ViewportWidth); public void PageRight() => SetHorizontalOffset(HorizontalOffset + ViewportWidth);
    public Rect MakeVisible(Visual visual, Rect rectangle) { var child = visual as DependencyObject; while (child is not null && VisualTreeHelper.GetParent(child) != this) child = VisualTreeHelper.GetParent(child); if (child is UIElement element) { var index = InternalChildren.IndexOf(element); if (index >= 0) { var itemIndex = ItemContainerGenerator.IndexFromGeneratorPosition(new(index, 0)); var columns = Math.Max(1, (int)Math.Floor(ViewportWidth / ItemWidth)); var y = itemIndex / columns * ItemHeight; if (y < VerticalOffset) SetVerticalOffset(y); else if (y + ItemHeight > VerticalOffset + ViewportHeight) SetVerticalOffset(y + ItemHeight - ViewportHeight); } } return rectangle; }
    public void SetHorizontalOffset(double offset) { _offset.X = Math.Max(0, offset); ClampOffset(); InvalidateArrange(); ScrollOwner?.InvalidateScrollInfo(); }
    public void SetVerticalOffset(double offset) { _offset.Y = Math.Max(0, offset); ClampOffset(); InvalidateMeasure(); ScrollOwner?.InvalidateScrollInfo(); }
}
