using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services;

public sealed class TutorialSpotlightLayoutService
{
    public TutorialSpotlightLayout Calculate(
        double viewportWidth,
        double viewportHeight,
        double targetLeft,
        double targetTop,
        double targetWidth,
        double targetHeight,
        double cardWidth = 360,
        double cardHeight = 230,
        double padding = 8)
    {
        var left = Math.Clamp(targetLeft - padding, 0, Math.Max(0, viewportWidth - 1));
        var top = Math.Clamp(targetTop - padding, 0, Math.Max(0, viewportHeight - 1));
        var width = Math.Min(targetWidth + padding * 2, viewportWidth - left);
        var height = Math.Min(targetHeight + padding * 2, viewportHeight - top);
        var preferredRight = left + width + 20;
        var cardLeft = preferredRight + cardWidth <= viewportWidth
            ? preferredRight
            : Math.Max(16, left - cardWidth - 20);
        var cardTop = Math.Clamp(top, 16, Math.Max(16, viewportHeight - cardHeight - 16));
        return new TutorialSpotlightLayout(left, top, width, height, cardLeft, cardTop);
    }
}
