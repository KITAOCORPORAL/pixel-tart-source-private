using System.Windows;
using System.Windows.Controls;

namespace RAWSelectionAssistant.Views;

/// <summary>
/// Lays out a title/context and its action group without overlapping either.
/// The Shell remains the sole owner of its existing 56 DIP close reserve.
/// </summary>
public sealed class StudioPageHeader : Panel
{
    private const double Gap = 20;
    private const double RowGap = 12;
    private bool _stacked;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 1600 : availableSize.Width;
        if (InternalChildren.Count == 0) return new Size();
        var title = InternalChildren[0];
        var actions = InternalChildren.Count > 1 ? InternalChildren[1] : null;
        title.Measure(new Size(width, double.PositiveInfinity));
        actions?.Measure(new Size(width, double.PositiveInfinity));
        var actionSize = actions?.DesiredSize ?? new Size();
        _stacked = actions is not null && (width < 780 || title.DesiredSize.Width + Gap + actionSize.Width > width);
        if (!_stacked)
            title.Measure(new Size(Math.Max(0, width - actionSize.Width - (actions is null ? 0 : Gap)), double.PositiveInfinity));
        return new Size(Math.Min(width, _stacked ? Math.Max(title.DesiredSize.Width, actionSize.Width) : title.DesiredSize.Width + actionSize.Width + (actions is null ? 0 : Gap)),
            _stacked ? title.DesiredSize.Height + RowGap + actionSize.Height : Math.Max(title.DesiredSize.Height, actionSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (InternalChildren.Count == 0) return finalSize;
        var title = InternalChildren[0];
        var actions = InternalChildren.Count > 1 ? InternalChildren[1] : null;
        if (actions is null) title.Arrange(new Rect(finalSize));
        else if (_stacked)
        {
            title.Arrange(new Rect(0, 0, finalSize.Width, title.DesiredSize.Height));
            actions.Arrange(new Rect(0, title.DesiredSize.Height + RowGap, finalSize.Width, actions.DesiredSize.Height));
        }
        else
        {
            var width = Math.Min(actions.DesiredSize.Width, finalSize.Width);
            title.Arrange(new Rect(0, 0, Math.Max(0, finalSize.Width - width - Gap), finalSize.Height));
            actions.Arrange(new Rect(finalSize.Width - width, Math.Max(0, (finalSize.Height - actions.DesiredSize.Height) / 2), width, actions.DesiredSize.Height));
        }
        return finalSize;
    }
}
