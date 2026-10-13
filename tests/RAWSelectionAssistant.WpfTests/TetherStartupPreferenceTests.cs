using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Views;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;

[TestClass]
[DoNotParallelize]
public sealed class TetherStartupPreferenceTests
{
    [TestMethod]
    public Task FirstLoadAndSaveWithoutSelectionOrGeneratedTemplateNeverReadVisualGroups()=>RunSta(async()=>
    {
        EnsureTestApplication();var directory=Directory.CreateTempSubdirectory("Kitao-startup-empty-").FullName;
        try
        {
            var path=Path.Combine(directory,"preferences.json");var view=new TetherCaptureView(new(path));
            Assert.IsEmpty(Descendants(view).OfType<Expander>());
            await view.LoadWorkspacePreferencesAsync();await view.SaveWorkspacePreferencesAsync();
            Assert.AreEqual(new TetherWorkspacePreferences(),await new TetherWorkspacePreferenceStore(path).LoadAsync());
            var window=new Window{Content=view,Width=1000,Height=720,ShowInTaskbar=false};Exception? failure=null;
            void Capture(object s,DispatcherUnhandledExceptionEventArgs e){failure=e.Exception;e.Handled=true;}
            view.Dispatcher.UnhandledException+=Capture;
            try{window.Show();await view.PreferenceLifecycleTask;await view.Dispatcher.InvokeAsync(view.UpdateLayout,DispatcherPriority.ApplicationIdle);window.Close();await view.PreferenceLifecycleTask;Assert.IsNull(failure,failure?.ToString());Assert.IsNull(view.WorkspacePreferenceError);}
            finally{view.Dispatcher.UnhandledException-=Capture;if(window.IsVisible)window.Close();}
        }
        finally{Directory.Delete(directory,true);}
    });

    [TestMethod]
    public Task DeferredMainAndDrawerTemplatesShareExpansionAndSurviveCollapseSaveReopen()=>RunSta(async()=>
    {
        EnsureTestApplication();var directory=Directory.CreateTempSubdirectory("Kitao-startup-templates-").FullName;
        var expected=new TetherWorkspacePreferences(444,false,true,false,true,true,false,true,true,true);
        try
        {
            var path=Path.Combine(directory,"preferences.json");await new TetherWorkspacePreferenceStore(path).SaveAsync(expected);
            var view=new TetherCaptureView(new(path));await view.LoadWorkspacePreferencesAsync();
            var panel=(Border)view.FindName("InspectorPanel");var drawer=(Border)view.FindName("InspectorDrawer");
            var main=Descendants(panel).OfType<ContentControl>().Single(x=>x.ContentTemplate is not null);var secondary=Descendants(drawer).OfType<ContentControl>().Single(x=>x.ContentTemplate is not null);
            Assert.IsEmpty(Descendants(panel).OfType<Expander>());
            // Data arrives AFTER preferences, as on a no-selected-photo startup. Real production templates are used.
            main.Content=new object();secondary.Content=new object();panel.Visibility=Visibility.Visible;drawer.Visibility=Visibility.Visible;
            view.Width=1680;view.Height=900;view.Measure(new Size(1680,900));view.Arrange(new Rect(0,0,1680,900));view.UpdateLayout();
            var a=Descendants(panel).OfType<Expander>().Single(x=>x.Name=="HistogramGroup");var b=Descendants(drawer).OfType<Expander>().Single(x=>x.Name=="HistogramGroup");
            Assert.AreNotSame(a,b);Assert.IsFalse(a.IsExpanded);Assert.IsFalse(b.IsExpanded);
            a.IsExpanded=true;Assert.IsTrue(view.WorkspacePreferenceState.ExposureExpanded);Assert.IsTrue(b.IsExpanded);
            b.IsExpanded=false;Assert.IsFalse(view.WorkspacePreferenceState.ExposureExpanded);Assert.IsFalse(a.IsExpanded);
            // Switch to narrow drawer then collapse both and remove content: saving must preserve logical state.
            view.Width=1000;view.Measure(new Size(1000,900));view.Arrange(new Rect(0,0,1000,900));drawer.Visibility=Visibility.Visible;view.UpdateLayout();
            view.WorkspacePreferenceState.LutExpanded=true;view.WorkspacePreferenceState.FileExpanded=true;
            panel.Visibility=Visibility.Collapsed;drawer.Visibility=Visibility.Collapsed;main.Content=null;secondary.Content=null;
            await view.SaveWorkspacePreferencesAsync();var saved=await new TetherWorkspacePreferenceStore(path).LoadAsync();Assert.AreEqual(444d,saved.InspectorWidth);Assert.IsTrue(saved.LutExpanded);Assert.IsTrue(saved.FileExpanded);Assert.IsFalse(saved.ExposureExpanded);
            var reopened=new TetherCaptureView(new(path));await reopened.LoadWorkspacePreferencesAsync();Assert.AreEqual(saved,reopened.WorkspacePreferenceState.Snapshot(saved.InspectorWidth));await reopened.SaveWorkspacePreferencesAsync();
        }
        finally{Directory.Delete(directory,true);}
    });

    [TestMethod]
    public Task NarrowFirstStartupRestoresStoredWidthEvenWithBothHostsCollapsed()=>RunSta(async()=>
    {
        EnsureTestApplication();var directory=Directory.CreateTempSubdirectory("Kitao-startup-narrow-").FullName;
        try
        {
            var path=Path.Combine(directory,"preferences.json");var expected=new TetherWorkspacePreferences(420,false,false,true,false,true,false,true,false,true);await new TetherWorkspacePreferenceStore(path).SaveAsync(expected);
            var view=new TetherCaptureView(new(path));await view.LoadWorkspacePreferencesAsync();view.Width=900;view.Height=600;view.Measure(new Size(900,600));view.Arrange(new Rect(0,0,900,600));view.UpdateLayout();
            var panel=(Border)view.FindName("InspectorPanel");var drawer=(Border)view.FindName("InspectorDrawer");panel.Visibility=Visibility.Collapsed;drawer.Visibility=Visibility.Collapsed;
            foreach(var host in Descendants(view).OfType<ContentControl>().Where(x=>x.ContentTemplate is DataTemplate).ToArray())host.ContentTemplate=null;
            view.UpdateLayout();Assert.IsEmpty(Descendants(view).OfType<Expander>());await view.SaveWorkspacePreferencesAsync();Assert.AreEqual(expected,await new TetherWorkspacePreferenceStore(path).LoadAsync());
        }
        finally{Directory.Delete(directory,true);}
    });
    [TestMethod]
    public Task LateTemplateGenerationWithNoSelectedPhotoAppliesStoredGroupState()=>RunSta(async()=>
    {
        EnsureTestApplication();var directory=Directory.CreateTempSubdirectory("Kitao-startup-selection-").FullName;
        try
        {
            var path=Path.Combine(directory,"preferences.json");var expected=new TetherWorkspacePreferences(400,false,false,false,true,true,false,true,false,true);await new TetherWorkspacePreferenceStore(path).SaveAsync(expected);
            var view=new TetherCaptureView(new(path));await view.LoadWorkspacePreferencesAsync();
            var panel=(Border)view.FindName("InspectorPanel");var drawer=(Border)view.FindName("InspectorDrawer");
            var template=(DataTemplate)view.Resources["InspectorTemplate"];var main=Descendants(panel).OfType<ContentControl>().First(x=>x.ContentTemplate==template);var secondary=Descendants(drawer).OfType<ContentControl>().First(x=>x.ContentTemplate==template);
            main.ContentTemplate=null;secondary.ContentTemplate=null;view.DataContext=new EmptySelection();
            view.Width=1600;view.Height=800;view.Measure(new Size(1600,800));view.Arrange(new Rect(0,0,1600,800));view.UpdateLayout();
            await view.SaveWorkspacePreferencesAsync();Assert.AreEqual(expected,await new TetherWorkspacePreferenceStore(path).LoadAsync());
            main.ContentTemplate=template;secondary.ContentTemplate=template;panel.Visibility=Visibility.Visible;drawer.Visibility=Visibility.Visible;view.UpdateLayout();
            foreach(var expander in Descendants(view).OfType<Expander>().Where(x=>x.Name=="HistogramGroup"))Assert.IsFalse(expander.IsExpanded);
            Assert.HasCount(2,Descendants(view).OfType<Expander>().Where(x=>x.Name=="HistogramGroup"));
            view.DataContext=null;await view.SaveWorkspacePreferencesAsync();Assert.AreEqual(expected,await new TetherWorkspacePreferenceStore(path).LoadAsync());
        }
        finally{Directory.Delete(directory,true);}
    });
    [TestMethod]
    public Task SaveFailureIsRecordedInsteadOfHiddenAndCurrentStateRetained()=>RunSta(async()=>
    {
        EnsureTestApplication();var directory=Directory.CreateTempSubdirectory("Kitao-startup-io-").FullName;
        try
        {
            var blocker=Path.Combine(directory,"not-a-directory");await File.WriteAllTextAsync(blocker,"keep");
            var view=new TetherCaptureView(new(Path.Combine(blocker,"prefs.json")));await view.LoadWorkspacePreferencesAsync();view.WorkspacePreferenceState.ExposureExpanded=false;
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));await view.PreferenceLifecycleTask;
            Assert.IsNotNull(view.WorkspacePreferenceError);Assert.IsInstanceOfType<IOException>(view.WorkspacePreferenceException);Assert.IsFalse(view.WorkspacePreferenceState.ExposureExpanded);Assert.AreEqual("keep",await File.ReadAllTextAsync(blocker));
        }
        finally{Directory.Delete(directory,true);}
    });
    private sealed class EmptySelection{public object? SelectedAsset{get;set;}public bool HasSelection=>false;public bool IsInspectorCollapsed{get;set;}public bool ShowInspectorDrawer{get;set;}}
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {for(var i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var child=VisualTreeHelper.GetChild(parent,i);yield return child;foreach(var nested in Descendants(child))yield return nested;}}
}
