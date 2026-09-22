using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace RAWSelectionAssistant.WpfTests;

internal static class StudioGlobalRolloutEvidence
{
    internal static async Task ScrollAndGeometry(FrameworkElement root, string state, string output)
    {
        var expanded = StudioVisualEvidence.Walk<Expander>(root).Where(x => x.IsVisible).Select(x => (Element: x, Before: x.IsExpanded)).ToArray();
        foreach (var item in expanded) item.Element.SetCurrentValue(Expander.IsExpandedProperty, true);
        await root.Dispatcher.InvokeAsync(root.UpdateLayout, DispatcherPriority.ApplicationIdle);
        var viewers = StudioVisualEvidence.Walk<ScrollViewer>(root).Where(x => x.IsVisible && x.ViewportHeight > 0).ToArray();
        var offsets = viewers.Select(x => (Element: x, X: x.HorizontalOffset, Y: x.VerticalOffset)).ToArray();
        var rows = new List<object>();
        foreach (var viewer in viewers)
        {
            viewer.ScrollToRightEnd(); viewer.ScrollToBottom();
            await root.Dispatcher.InvokeAsync(root.UpdateLayout, DispatcherPriority.ApplicationIdle);
            var remaining = Math.Max(0, viewer.ScrollableHeight - viewer.VerticalOffset);
            var horizontalRemaining = Math.Max(0, viewer.ScrollableWidth - viewer.HorizontalOffset);
            var last = StudioVisualEvidence.Walk<Control>(viewer)
                .Where(x => x.IsVisible && x.ActualWidth > 0 && x.ActualHeight > 0 && x is ButtonBase or TextBox or ComboBox)
                .OrderBy(x => x.TransformToAncestor(viewer).TransformBounds(new Rect(x.RenderSize)).Bottom).LastOrDefault();
            if (last is not null) { last.BringIntoView(); await root.Dispatcher.InvokeAsync(root.UpdateLayout, DispatcherPriority.ApplicationIdle); }
            Rect? bounds = last?.TransformToAncestor(viewer).TransformBounds(new Rect(last.RenderSize));
            var rootBounds = last?.TransformToAncestor(root).TransformBounds(new Rect(last.RenderSize));
            var visible = bounds is null || (bounds.Value.Top >= -1 && bounds.Value.Bottom <= viewer.ActualHeight + 1
                && rootBounds!.Value.Top >= -1 && rootBounds.Value.Bottom <= root.ActualHeight + 1);
            rows.Add(new { Name = viewer.Name, viewer.ScrollableHeight, viewer.VerticalOffset, Remaining = remaining, HorizontalRemaining = horizontalRemaining,
                LastControl = last?.GetType().Name, LastControlBounds = bounds?.ToString(),
                LastOperationVisible = visible, Result = remaining <= 1 && horizontalRemaining <= 1 && visible ? "END_REACHED" : "FAIL" });
        }
        StudioVisualEvidence.Png(root, Path.Combine(output, "scroll", state + ".png"));
        File.AppendAllText(Path.Combine(output, "scroll-interaction.jsonl"), JsonSerializer.Serialize(new { State = state, Rows = rows }) + Environment.NewLine);
        AuditControlText(root, state + "/scroll-end", output);
        foreach (var offset in offsets) { offset.Element.ScrollToHorizontalOffset(offset.X); offset.Element.ScrollToVerticalOffset(offset.Y); }
        foreach (var item in expanded) item.Element.SetCurrentValue(Expander.IsExpandedProperty, item.Before);
        await root.Dispatcher.InvokeAsync(root.UpdateLayout, DispatcherPriority.ApplicationIdle);
    }

    internal static void AuditControlText(FrameworkElement root, string state, string output)
    {
        var rows = new List<object>();
        foreach (var text in StudioVisualEvidence.Walk<TextBlock>(root).Where(x => x.IsVisible && x.ActualWidth > 0 && !string.IsNullOrWhiteSpace(x.Text)))
        {
            var width = Math.Max(1, text.ActualWidth - text.Padding.Left - text.Padding.Right);
            var measured = new FormattedText(text.Text, System.Globalization.CultureInfo.CurrentCulture, text.FlowDirection,
                new Typeface(text.FontFamily, text.FontStyle, text.FontWeight, text.FontStretch), text.FontSize, Brushes.Black, 1);
            if (text.TextWrapping != TextWrapping.NoWrap) measured.MaxTextWidth = width;
            if (!double.IsNaN(text.LineHeight)) measured.LineHeight = text.LineHeight;
            var bounds = text.TransformToAncestor(root).TransformBounds(new Rect(text.RenderSize));
            var scroll = false; var clipped = bounds.Left < -2 || bounds.Top < -2 || bounds.Right > root.ActualWidth + 2 || bounds.Bottom > root.ActualHeight + 2;
            string? owner = null; var tooltip = text.ToolTip is not null;
            for (DependencyObject? p = VisualTreeHelper.GetParent(text); p is not null && !ReferenceEquals(p, root); p = VisualTreeHelper.GetParent(p))
            {
                if (p is Control c && owner is null) owner = c.GetType().Name;
                if (p is FrameworkElement e) tooltip |= e.ToolTip is not null;
                if (p is ScrollViewer) scroll = true;
                if (p is FrameworkElement clip && (clip.ClipToBounds || clip is ScrollContentPresenter))
                {
                    var relative = text.TransformToAncestor(clip).TransformBounds(new Rect(text.RenderSize));
                    clipped |= relative.Left < -2 || relative.Top < -2 || relative.Right > clip.ActualWidth + 2 || relative.Bottom > clip.ActualHeight + 2;
                }
            }
            var overflow = measured.WidthIncludingTrailingWhitespace > width + 3 || measured.Height > text.ActualHeight + 4;
            var result = clipped && scroll ? "SCROLL_REACHABLE" : clipped ? "CLIPPED" :
                text.TextTrimming != TextTrimming.None ? tooltip ? "TOOLTIP_FULL_VALUE" : "ELLIPSIS_VALID" :
                overflow ? "CLIPPED" : text.TextWrapping != TextWrapping.NoWrap ? "WRAPPED_VALID" : "VISIBLE_FULL";
            rows.Add(new { Owner = owner ?? "TextBlock", text.Text, Result = result, Bounds = bounds.ToString(), DesiredWidth = measured.WidthIncludingTrailingWhitespace, ActualWidth = width });
        }
        File.AppendAllText(Path.Combine(output, "global-control-text.jsonl"), JsonSerializer.Serialize(new { State = state, Rows = rows }) + Environment.NewLine);
    }
}
