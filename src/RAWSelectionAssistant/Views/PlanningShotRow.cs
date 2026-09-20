using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;

namespace RAWSelectionAssistant.Views;

public sealed class PlanningShotRow : Border
{
    public Action? OpenRequested { get; set; }
    protected override AutomationPeer OnCreateAutomationPeer() => new ShotPeer(this);
    private sealed class ShotPeer(PlanningShotRow owner) : FrameworkElementAutomationPeer(owner), IInvokeProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;
        protected override string GetClassNameCore() => nameof(PlanningShotRow);
        public override object? GetPattern(PatternInterface patternInterface) => patternInterface == PatternInterface.Invoke ? this : base.GetPattern(patternInterface);
        public void Invoke()
        {
            if (!owner.IsEnabled) throw new ElementNotEnabledException();
            owner.Dispatcher.BeginInvoke(new Action(() => owner.OpenRequested?.Invoke()));
        }
    }
}
