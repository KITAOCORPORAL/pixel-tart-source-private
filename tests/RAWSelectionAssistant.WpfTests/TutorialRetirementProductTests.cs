using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Views;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass][DoNotParallelize]
public sealed class TutorialRetirementProductTests
{
    [TestMethod]
    public Task RealShellAndHelpHaveNoRetiredTutorialTreeOrLegacyVisibleBrand() => RunSta(() =>
    {
        EnsureTestApplication(); var shell=new MainWindow();
        foreach(var name in new[]{"TutorialOverlay","TutorialPrimaryButton","TutorialCard","TutorialMaskTop"})
            Assert.IsNull(shell.FindName(name),name);
        var help=new HelpWindow();
        var texts=Descendants(help).OfType<TextBlock>().Select(x=>x.Text)
            .Concat(Descendants(help).OfType<ContentControl>().Select(x=>x.Content as string??""));
        Assert.IsFalse(texts.Any(t=>t.Contains("教程")||t.Contains("像素蛋挞")||t.Contains("Pixel Tart",StringComparison.OrdinalIgnoreCase)));
        Assert.AreEqual(RAWSelectionAssistant.Services.HelpAction.None,help.SelectedAction);
        Assert.AreEqual("光镜",new PublishingWatermarkLayerViewModel().Text);
        Assert.IsFalse(shell.Title.Contains("像素蛋挞")); return Task.CompletedTask;
    });
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)
        {var child=VisualTreeHelper.GetChild(parent,i);yield return child;foreach(var item in Descendants(child))yield return item;}
    }
    // Dedicated process fixture. Runs actual App.OnStartup and normal product commands.
    // Not physical mouse acceptance and not a replacement tutorial host.
    [TestMethod]
    public void FullAppFirstUpgradeAndRestartDoNotActivateTutorialAndNormalSplitWorks()
    {
        var output=Environment.GetEnvironmentVariable("KITAO_RETIREMENT_APP_EVIDENCE");
        if(string.IsNullOrWhiteSpace(output)){Assert.Inconclusive("Dedicated isolated full app evidence required");return;}
        Exception? failure=null;bool finished=false;
        var thread=new Thread(()=>
        {
            try
            {
                if(Environment.GetEnvironmentVariable("PIXEL_TART_ISOLATED_RUNTIME")!="1")throw new InvalidOperationException("Isolation required");
                Directory.CreateDirectory(output);
                var settingsService=new SettingsService(new FileLogService());
                var scenario=Environment.GetEnvironmentVariable("KITAO_RETIREMENT_SCENARIO")??"first";
                if(scenario=="upgrade")
                {
                    var old=new AppSettings{OnboardingCompleted=false,OnboardingCurrentStep=18,OnboardingVersion="1.2.0",
                        OnboardingCompletionProof="legacy-incomplete",OnboardingUpgradeOfferShown=false,OnboardingLegacyUser=false,
                        RecentProjectName="保留的旧项目",LastPrimaryPage="Workflow"};
                    settingsService.SaveAsync(old).GetAwaiter().GetResult();
                    Directory.CreateDirectory(AppDataPaths.TutorialDirectory);
                    File.WriteAllBytes(Path.Combine(AppDataPaths.TutorialDirectory,"user-kept.jpg"),[1,3,5]);
                }
                var app=new App();app.InitializeComponent();var start=DateTime.UtcNow;bool running=false;
                var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(150)};
                timer.Tick+=async(_,_)=>
                {
                    if(running)return;
                    if(DateTime.UtcNow-start>TimeSpan.FromSeconds(55)){failure=new TimeoutException("Full application startup timed out");timer.Stop();app.Shutdown(-1);return;}
                    if(app.MainWindow is not MainWindow{IsLoaded:true,DataContext:MainViewModel vm} window)return;
                    running=true;
                    try
                    {
                        if(vm.IsOnboardingActive||vm.IsOnboardingRequired||vm.NeedsUpgradeTutorialOffer)throw new InvalidOperationException("Retired tutorial active");
                        if(window.FindName("TutorialOverlay") is not null)throw new InvalidOperationException("Overlay still exists");
                        if(app.Windows.OfType<Window>().Any(w=>w.GetType().Name.Contains("UpgradeTutorial")))throw new InvalidOperationException("Upgrade offer appeared");
                        if(scenario=="upgrade"&&vm.ProjectName!="保留的旧项目")throw new InvalidOperationException("Upgrade erased project name");
                        vm.NavigateCommand.Execute("LocalSplit");
                        if(!vm.IsLocalSplitPage)throw new InvalidOperationException("Normal split route unavailable");
                        var source=Path.Combine(output,"synthetic-source");Directory.CreateDirectory(source);
                        for(int n=0;n<3;n++)await File.WriteAllBytesAsync(Path.Combine(source,$"DSC{1234+n:D5}.JPG"),[255,216,255,217]);
                        vm.Sources.Clear();vm.Sources.Add(new SourceDirectoryEntry{Path=source,DirectoryType=SourceDirectoryType.Jpeg});
                        vm.CollectionCategory=CollectionCategory.JpegOnly;
                        await vm.ScanCommand.ExecuteAsync(null);
                        if(vm.IndexedMediaCount!=3)throw new InvalidOperationException("Normal scan failed: "+vm.StatusMessage);
                        vm.TextInput="1234,1235,1236";vm.ParseTextCommand.Execute(null);await vm.MatchCommand.ExecuteAsync(null);
                        if(vm.Selections.Count!=3||vm.Selections.Any(x=>x.OverallStatus!=MediaOverallStatus.CompleteMatched))throw new InvalidOperationException("Normal match failed: "+vm.StatusMessage);
                        if(!vm.CopyCommand.CanExecute(null))throw new InvalidOperationException("Normal copy command locked by retired tutorial");
                        vm.OutputBaseDirectory=Path.Combine(output,"split-output");vm.ProjectName="合成正常分片";
                        var sourceHashes=Directory.GetFiles(source).ToDictionary(p=>p,p=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(p))));
                        await vm.CopyCommand.ExecuteAsync(null);
                        if(vm.Selections.Any(x=>x.OverallStatus!=MediaOverallStatus.FullyCopied))throw new InvalidOperationException("Normal copy failed: "+vm.StatusMessage);
                        if(Directory.GetFiles(vm.OutputDirectory,"*.JPG",SearchOption.AllDirectories).Length!=3)throw new InvalidOperationException("Copied file count differs");
                        foreach(var pair in sourceHashes)if(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(pair.Key)))!=pair.Value)throw new InvalidOperationException("Source bytes changed");
                        await vm.ExportReportCommand.ExecuteAsync(null);
                        if(!File.Exists(Path.Combine(vm.OutputDirectory,"匹配报告.csv")))throw new InvalidOperationException("Normal report missing");
                        if(vm.IsOnboardingActive)throw new InvalidOperationException("Entering split reactivated tutorial");
                        if(scenario=="first"&&Directory.Exists(AppDataPaths.TutorialDirectory))throw new InvalidOperationException("First startup generated tutorial data");
                        if(scenario!="first")
                        {
                            var retained=Path.Combine(AppDataPaths.TutorialDirectory,"user-kept.jpg");
                            if(!File.Exists(retained)||!File.ReadAllBytes(retained).SequenceEqual(new byte[]{1,3,5}))throw new InvalidOperationException("Existing tutorial folder touched");
                        }
                        await vm.SaveSettingsAsync();
                        await File.WriteAllTextAsync(Path.Combine(output,"RESULT.json"),JsonSerializer.Serialize(new{scenario,pid=Environment.ProcessId,
                            dataRoot=AppDataPaths.Root,title=window.Title,localSplit=vm.IsLocalSplitPage,tutorialActive=vm.IsOnboardingActive,
                            scanCount=vm.IndexedMediaCount,matched=vm.Selections.Count,copied=vm.Selections.Count(x=>x.OverallStatus==MediaOverallStatus.FullyCopied),sourceBytesUnchanged=true,report=true,
                            scope="Actual App startup and commands in isolated testhost; NOT native mouse/DPI workflow"},new JsonSerializerOptions{WriteIndented=true}));
                        finished=true;
                    }
                    catch(Exception error){failure=error;}
                    finally{timer.Stop();app.Shutdown();}
                };
                timer.Start();app.Run();
            }
            catch(Exception error){failure=error;}
        });
        thread.SetApartmentState(ApartmentState.STA);thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(90)),"Full app fixture timed out");
        if(failure is not null)throw failure;Assert.IsTrue(finished);
    }
}