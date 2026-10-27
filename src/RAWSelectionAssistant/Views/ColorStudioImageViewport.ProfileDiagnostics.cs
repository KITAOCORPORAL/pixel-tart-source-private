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
        monitorRefresh.Tick+=(_,_)=>{if(DisplayColorEnabled)RefreshProfileAssociation(this,EventArgs.Empty);};
        Loaded+=(_,_)=>
        {
            var next=Window.GetWindow(this);if(!ReferenceEquals(_profileWindow,next))
            {
                DetachProfileDiagnostics();_profileWindow=next;
                if(next is not null){next.LocationChanged+=RefreshProfileAssociation;next.Activated+=RefreshProfileAssociation;}
            }
            monitorRefresh.Start();RefreshProfileAssociation(this,EventArgs.Empty);
        };
        Unloaded+=(_,_)=>{DetachProfileDiagnostics();DetachDisplayColor();};
    }
    private void DetachProfileDiagnostics()
    {
        if(_profileWindow is not null){_profileWindow.LocationChanged-=RefreshProfileAssociation;_profileWindow.Activated-=RefreshProfileAssociation;_profileWindow=null;}
    }
    private void RefreshProfileAssociation(object? sender,EventArgs e)
    {
        DisplayProfileObservation=WindowsMonitorProfileProbe.ObserveWindow(_profileWindow is null?IntPtr.Zero:new WindowInteropHelper(_profileWindow).Handle);
        RefreshDisplayColorContext();ToolTip=DescribeProfileAssociation(DisplayProfileObservation)+"\nExplicit display path: "+DisplayConversionStatus;
    }
    internal static string DescribeProfileAssociation(MonitorProfileObservation value)
        => $"Color Studio: {value.Device} · {value.State}\nICC SHA-256: {value.ProfileSha256??"unavailable"}\nPreview: sRGB bitmap; monitor-profile conversion not applied by Color Studio. OS/compositor behavior unverified. Export ICC remains independent.";
}
