using System.Windows;
using System.Windows.Interop;
using RAWSelectionAssistant.Services;
namespace RAWSelectionAssistant.Views;
public sealed partial class ColorStudioImageViewport
{
    private Window? _profileWindow;
    public MonitorProfileObservation DisplayProfileObservation {get;private set;}=new("unknown","NotObserved",null,null,0);
    private void AttachProfileDiagnostics()
    {
        Loaded+=(_,_)=>
        {
            var next=Window.GetWindow(this);if(!ReferenceEquals(_profileWindow,next))
            {
                DetachProfileDiagnostics();_profileWindow=next;
                if(next is not null){next.LocationChanged+=RefreshProfileAssociation;next.Activated+=RefreshProfileAssociation;}
            }
            RefreshProfileAssociation(this,EventArgs.Empty);
        };
        Unloaded+=(_,_)=>DetachProfileDiagnostics();
    }
    private void DetachProfileDiagnostics()
    {
        if(_profileWindow is not null){_profileWindow.LocationChanged-=RefreshProfileAssociation;_profileWindow.Activated-=RefreshProfileAssociation;_profileWindow=null;}
    }
    private void RefreshProfileAssociation(object? sender,EventArgs e)
    {
        DisplayProfileObservation=WindowsMonitorProfileProbe.ObserveWindow(_profileWindow is null?IntPtr.Zero:new WindowInteropHelper(_profileWindow).Handle);
        ToolTip=DescribeProfileAssociation(DisplayProfileObservation);
    }
    internal static string DescribeProfileAssociation(MonitorProfileObservation value)
        => $"Color Studio: {value.Device} · {value.State}\nICC SHA-256: {value.ProfileSha256??"unavailable"}\nPreview: sRGB bitmap; monitor-profile conversion not applied by Color Studio. OS/compositor behavior unverified. Export ICC remains independent.";
}
