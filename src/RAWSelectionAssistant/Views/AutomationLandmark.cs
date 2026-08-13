using System.Windows;
using System.Windows.Automation.Peers;

namespace RAWSelectionAssistant.Views;

public sealed class AutomationLandmark : FrameworkElement
{
    public AutomationLandmark()
    {
        IsHitTestVisible = false;
        Focusable = false;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new AutomationLandmarkPeer(this);

    private sealed class AutomationLandmarkPeer(AutomationLandmark owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;

        protected override string GetClassNameCore() => nameof(AutomationLandmark);

        protected override bool IsControlElementCore() => true;

        protected override bool IsContentElementCore() => true;
    }
}
