using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;

namespace PixelTart.Modules.AssetLibrary;

internal sealed class RatingAutomationPeer(PixelTartRatingControl control) : FrameworkElementAutomationPeer(control), IRangeValueProvider
{
    protected override string GetClassNameCore() => nameof(PixelTartRatingControl);
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;
    public override object? GetPattern(PatternInterface patternInterface) => patternInterface == PatternInterface.RangeValue ? this : base.GetPattern(patternInterface);
    public bool IsReadOnly => !control.IsEnabled;
    public double LargeChange => 1;
    public double SmallChange => 1;
    public double Maximum => 5;
    public double Minimum => 0;
    public double Value => control.Rating;
    public void SetValue(double value)
    {
        if (IsReadOnly) throw new ElementNotEnabledException();
        if (!double.IsFinite(value) || value < 0 || value > 5 || value != Math.Truncate(value)) throw new ArgumentOutOfRangeException(nameof(value));
        if (control.Command is { } command && !command.CanExecute((int)value)) throw new ElementNotEnabledException();
        if (control.Rating != (int)value) control.Commit((int)value);
    }
}
