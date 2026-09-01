using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using RAWSelectionAssistant.Core.Models;

namespace PixelTart.Modules.AssetLibrary;

public sealed class VirtualizingAssetPanel : VirtualizingPanel, IScrollInfo
{
    public static readonly DependencyProperty ViewModeProperty = DependencyProperty.Register(
        nameof(ViewMode), typeof(AssetLibraryViewMode), typeof(VirtualizingAssetPanel),
        new FrameworkPropertyMetadata(AssetLibraryViewMode.Grid, FrameworkPropertyMetadataOptions.AffectsMeasure));
    public static readonly DependencyProperty ThumbnailWidthProperty = DependencyProperty.Register(
        nameof(ThumbnailWidth), typeof(double), typeof(VirtualizingAssetPanel),
        new FrameworkPropertyMetadata(180d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    private AssetLayoutResult _layout = new([], default);
    private Size _viewport;
    private Point _offset;

    public AssetLibraryViewMode ViewMode { get => (AssetLibraryViewMode)GetValue(ViewModeProperty); set => SetValue(ViewModeProperty, value); }
    public double ThumbnailWidth { get => (double)GetValue(ThumbnailWidthProperty); set => SetValue(ThumbnailWidthProperty, value); }
    public int FirstVisibleIndex { get; private set; } = -1;
    public int RealizedItemCount => InternalChildren.Count;

    protected override Size MeasureOverride(Size availableSize)
    {
        var owner = ItemsControl.GetItemsOwner(this);
        var count = owner?.Items.Count ?? 0;
        var width = double.IsInfinity(availableSize.Width) ? Math.Max(120d, ActualWidth) : Math.Max(120d, availableSize.Width);
        var height = double.IsInfinity(availableSize.Height) ? Math.Max(120d, ActualHeight) : Math.Max(0d, availableSize.Height);
        var ratios = owner?.Items.Cast<object>().Select(item => item is AssetVisualMatchView card ? card.AspectRatio : 1.5d).ToArray() ?? [];
        _layout = AssetLayoutEngine.Arrange(ViewMode, ratios, width, ThumbnailWidth);
        _viewport = new(width, height);
        ClampOffset();
        ScrollOwner?.InvalidateScrollInfo();

        if (count == 0)
        {
            FirstVisibleIndex = -1;
            CleanUp(0, -1);
            return _viewport;
        }

        var top = VerticalOffset;
        var bottom = top + Math.Max(1d, height);
        var first = -1;
        var last = -1;
        for (var index = 0; index < _layout.Items.Count; index++)
        {
            var rect = _layout.Items[index];
            if (rect.Bottom < top - 80d || rect.Top > bottom + 80d) continue;
            if (first < 0) first = index;
            last = index;
        }
        if (first < 0)
        {
            first = Math.Clamp(FindNearestIndex(top), 0, count - 1);
            last = first;
        }
        FirstVisibleIndex = first;
        Realize(first, last);
        CleanUp(first, last);
        return _viewport;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        for (var childIndex = 0; childIndex < InternalChildren.Count; childIndex++)
        {
            var itemIndex = ItemContainerGenerator.IndexFromGeneratorPosition(new(childIndex, 0));
            if (itemIndex < 0 || itemIndex >= _layout.Items.Count) continue;
            var rect = _layout.Items[itemIndex];
            InternalChildren[childIndex].Arrange(new(rect.X, rect.Y - VerticalOffset, rect.Width, rect.Height));
        }
        return finalSize;
    }

    protected override void BringIndexIntoView(int index)
    {
        if (index < 0 || index >= _layout.Items.Count) return;
        var rect = _layout.Items[index];
        if (rect.Top < VerticalOffset) SetVerticalOffset(rect.Top);
        else if (rect.Bottom > VerticalOffset + ViewportHeight) SetVerticalOffset(rect.Bottom - ViewportHeight);
    }

    private void Realize(int firstIndex, int lastIndex)
    {
        var generator = ItemContainerGenerator;
        if (generator is null) return;
        var start = generator.GeneratorPositionFromIndex(firstIndex);
        var childIndex = start.Offset == 0 ? start.Index : start.Index + 1;
        using (generator.StartAt(start, GeneratorDirection.Forward, true))
        {
            for (var itemIndex = firstIndex; itemIndex <= lastIndex; itemIndex++, childIndex++)
            {
                var child = (UIElement)generator.GenerateNext(out var newlyRealized)!;
                if (newlyRealized)
                {
                    if (childIndex >= InternalChildren.Count) AddInternalChild(child);
                    else InsertInternalChild(childIndex, child);
                    generator.PrepareItemContainer(child);
                }
                var rect = _layout.Items[itemIndex];
                child.Measure(new(rect.Width, rect.Height));
            }
        }
    }

    private void CleanUp(int firstIndex, int lastIndex)
    {
        for (var childIndex = InternalChildren.Count - 1; childIndex >= 0; childIndex--)
        {
            var itemIndex = ItemContainerGenerator.IndexFromGeneratorPosition(new(childIndex, 0));
            if (itemIndex >= firstIndex && itemIndex <= lastIndex) continue;
            ItemContainerGenerator.Remove(new(childIndex, 0), 1);
            RemoveInternalChildRange(childIndex, 1);
        }
    }

    private int FindNearestIndex(double y)
    {
        if (_layout.Items.Count == 0) return -1;
        var best = 0;
        var distance = double.MaxValue;
        for (var index = 0; index < _layout.Items.Count; index++)
        {
            var next = Math.Abs(_layout.Items[index].Top - y);
            if (next >= distance) continue;
            distance = next;
            best = index;
        }
        return best;
    }

    private void ClampOffset()
    {
        _offset.X = 0d;
        _offset.Y = Math.Clamp(_offset.Y, 0d, Math.Max(0d, ExtentHeight - ViewportHeight));
    }

    public bool CanHorizontallyScroll { get; set; }
    public bool CanVerticallyScroll { get; set; } = true;
    public double ExtentWidth => _layout.Extent.Width;
    public double ExtentHeight => _layout.Extent.Height;
    public double ViewportWidth => _viewport.Width;
    public double ViewportHeight => _viewport.Height;
    public double HorizontalOffset => 0d;
    public double VerticalOffset => _offset.Y;
    public ScrollViewer? ScrollOwner { get; set; }
    public void LineUp() => SetVerticalOffset(VerticalOffset - 32d);
    public void LineDown() => SetVerticalOffset(VerticalOffset + 32d);
    public void LineLeft() { }
    public void LineRight() { }
    public void MouseWheelUp() => SetVerticalOffset(VerticalOffset - 96d);
    public void MouseWheelDown() => SetVerticalOffset(VerticalOffset + 96d);
    public void MouseWheelLeft() { }
    public void MouseWheelRight() { }
    public void PageUp() => SetVerticalOffset(VerticalOffset - ViewportHeight);
    public void PageDown() => SetVerticalOffset(VerticalOffset + ViewportHeight);
    public void PageLeft() { }
    public void PageRight() { }
    public Rect MakeVisible(Visual visual, Rect rectangle)
    {
        DependencyObject? child = visual;
        while (child is not null && VisualTreeHelper.GetParent(child) != this) child = VisualTreeHelper.GetParent(child);
        if (child is UIElement element)
        {
            var position = new GeneratorPosition(InternalChildren.IndexOf(element), 0);
            BringIndexIntoView(ItemContainerGenerator.IndexFromGeneratorPosition(position));
        }
        return rectangle;
    }
    public void SetHorizontalOffset(double offset) { }
    public void SetVerticalOffset(double offset)
    {
        var next = Math.Max(0d, offset);
        if (Math.Abs(next - _offset.Y) < 0.1d) return;
        _offset.Y = next;
        ClampOffset();
        InvalidateMeasure();
        ScrollOwner?.InvalidateScrollInfo();
    }
}
