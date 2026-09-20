using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace RAWSelectionAssistant.Views;

// A real content parent, unlike a sibling AutomationLandmark.
public sealed class PlanningAutomationScope : StackPanel
{
    protected override AutomationPeer OnCreateAutomationPeer() => new ScopePeer(this);
    private sealed class ScopePeer(PlanningAutomationScope owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;
        protected override string GetClassNameCore() => nameof(PlanningAutomationScope);
        protected override bool IsControlElementCore() => true;
        protected override bool IsContentElementCore() => true;
    }
}
