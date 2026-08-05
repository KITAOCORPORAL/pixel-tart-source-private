namespace RAWSelectionAssistant.Tests;

#pragma warning disable MSTEST0037

[TestClass]
public sealed class Version210UiArchitectureTests
{
    private static readonly string Root=FindRoot();
    private static string MainXaml=>File.ReadAllText(Path.Combine(Root,"src","RAWSelectionAssistant","MainWindow.xaml"));
    private static string MainVm=>File.ReadAllText(Path.Combine(Root,"src","RAWSelectionAssistant","ViewModels","MainViewModel.cs"));
    private static string TaskVm=>File.ReadAllText(Path.Combine(Root,"src","RAWSelectionAssistant","ViewModels","TaskCenterViewModels.cs"));

    [TestMethod] public void TaskCenter_UsesDedicatedViewModel()=>StringAssert.Contains(MainVm,"TaskCenterViewModel TaskCenter");
    [TestMethod] public void TaskCenter_DisplaysPersistedTaskCollection()=>StringAssert.Contains(MainXaml,"ItemsSource=\"{Binding TaskCenter.Tasks}\"");
    [TestMethod] public void TaskCenter_HasPauseAction()=>StringAssert.Contains(MainXaml,"TaskCenter.PauseCommand");
    [TestMethod] public void TaskCenter_HasResumeAction()=>StringAssert.Contains(MainXaml,"TaskCenter.ResumeCommand");
    [TestMethod] public void TaskCenter_HasCancelAction()=>StringAssert.Contains(MainXaml,"TaskCenter.CancelCommand");
    [TestMethod] public void TaskCenter_HasRetryAction()=>StringAssert.Contains(MainXaml,"TaskCenter.RetryCommand");
    [TestMethod] public void TaskCenter_CompletedTasksCanBeCleared()=>StringAssert.Contains(MainXaml,"TaskCenter.ClearCompletedCommand");
    [TestMethod] public void TaskCenter_ProvidesAutomationNames()=>StringAssert.Contains(MainXaml,"AutomationProperties.Name=\"暂停任务\"");
    [TestMethod] public void MainOperations_AreBridgedToTaskEngine()=>StringAssert.Contains(MainVm,"_taskOperationBridge.RunAsync");
    [TestMethod] public void OrganizeAndCollage_AreBridgedToTaskEngine(){var text=File.ReadAllText(Path.Combine(Root,"src","RAWSelectionAssistant","ViewModels","ToolPageViewModels.cs"));Assert.IsTrue(Count(text,"_taskOperationBridge.RunAsync")>=2);}
    [TestMethod] public void RecoveryAndNotificationViewModelsExist(){StringAssert.Contains(TaskVm,"RecoveryCenterViewModel");StringAssert.Contains(TaskVm,"NotificationCenterViewModel");StringAssert.Contains(TaskVm,"DatabaseRecoveryViewModel");}
    [TestMethod] public void NavigationServiceExists()=>StringAssert.Contains(TaskVm,"class NavigationService");
    [TestMethod] public void CalendarPageUsesIndependentHost()=>Contains(MainXaml,"<views:WorkCalendarView", "DataContext=\"{Binding WorkCalendarPage}\"");
    [TestMethod] public void VersionIs230(){var version=File.ReadAllText(Path.Combine(Root,"build","Version.props"));StringAssert.Contains(version,"<PixelTartVersion>2.3.0</PixelTartVersion>");StringAssert.Contains(version,"<PixelTartAssemblyVersion>2.3.0.0</PixelTartAssemblyVersion>");}
    [TestMethod] public void ReleaseStillDisallowsMockProvider(){var app=File.ReadAllText(Path.Combine(Root,"src","RAWSelectionAssistant","App.xaml.cs"));StringAssert.Contains(app,"allowMockProvider: false");}

    private static void Contains(string text,params string[] values){foreach(var value in values)StringAssert.Contains(text,value);}

    private static int Count(string text,string value){var count=0;var start=0;while((start=text.IndexOf(value,start,StringComparison.Ordinal))>=0){count++;start+=value.Length;}return count;}
    private static string FindRoot(){var directory=new DirectoryInfo(AppContext.BaseDirectory);while(directory is not null&&!File.Exists(Path.Combine(directory.FullName,"RAWSelectionAssistant.sln")))directory=directory.Parent;return directory?.FullName??throw new DirectoryNotFoundException();}
}
