using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Views;

namespace RAWSelectionAssistant.WpfTests;

internal readonly record struct VisualCaptureSize(int Width, int Height)
{
    public override string ToString() => $"{Width}x{Height}";
    internal static readonly VisualCaptureSize[] Matrix = [new(1180,720), new(1600,920), new(1920,1080)];
}

// Invoked by the existing whole-app host, seeder, PNG writer and contact-sheet builder.
internal static class UiGuardianCapture
{
    internal static string RepositoryRoot()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "RAWSelectionAssistant.sln"))) return d.FullName;
        throw new DirectoryNotFoundException();
    }
    internal static async Task Run(Window window, MainViewModel vm, string fixtures, string output)
    {
        Assert.AreEqual("1", Environment.GetEnvironmentVariable("PIXEL_TART_UI_GUARDIAN_HARD_GATE"));
        Assert.AreNotEqual("1", Environment.GetEnvironmentVariable("PIXEL_TART_APPROVE_VISUAL_BASELINE"));
        var repo = RepositoryRoot();
        var stamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ");
        var received = Path.Combine(repo, "tests/UIVisual/Received", stamp);
        var report = Path.Combine(repo, "docs/evidence/ui-guardian");
        Directory.CreateDirectory(received); Directory.CreateDirectory(report);
        var productSha = Environment.GetEnvironmentVariable("PIXEL_TART_PRODUCT_SOURCE_SHA") ?? throw new InvalidOperationException("Product SHA required");
        var implementationSha = Environment.GetEnvironmentVariable("PIXEL_TART_GUARDIAN_SHA") ?? "UNCOMMITTED";
        var rows = new List<CaptureRow>();
        var routes = new[] { ("Workbench","Workbench"), ("AssetLibrary","AssetLibrary"), ("Tether","Tether"), ("Planning","Planning"), ("OnlineSelection","OnlineSelection"), ("ColorStudio","ReferenceColor"), ("Publishing","Publishing"), ("Settings","Settings") };
        foreach (var size in VisualCaptureSize.Matrix)
        {
            foreach (var (name, route) in routes)
            {
                try
                {
                    vm.NavigateCommand.Execute(route);
                    await Settle();
                    if (route != "Settings" && vm.CurrentPage != route) throw new InvalidOperationException("Route not reached: " + route);
                    await Capture(name, size);
                }
                catch (Exception error) { RecordUnreachable(name, size, error); }
                finally { if (route == "Settings") vm.CloseSettingsCommand.Execute(null); }
            }
        }
        foreach (var size in VisualCaptureSize.Matrix)
        {
            vm.NavigateCommand.Execute("ReferenceColor"); await Settle();
            var view = StudioVisualEvidence.Walk<ReferenceColorWorkspaceView>(window).Single();
            var expander = StudioVisualEvidence.Walk<Expander>(view).Single(x => x.Header?.ToString() == "3D 色彩空间");
            var viewport = (ColorSpace3DViewport)view.FindName("ColorSpaceViewport");
            // Reloading the page VM uses the product source/matched pipeline. Each size starts without a generated model.
            var workspace = vm.ReferenceColorPage;
            try
            {
                var contextImage = new System.Windows.Media.Imaging.BitmapImage();
                contextImage.BeginInit(); contextImage.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad; contextImage.UriSource = new Uri(Path.GetFullPath(Path.Combine(fixtures, "demo-shot-01.png"))); contextImage.EndInit(); contextImage.Freeze();
                await workspace.AcceptContextAsync(RAWSelectionAssistant.Services.PlanningHumanAcceptanceDemoSeeder.ProjectId, null, contextImage);
                await workspace.LoadTargetAsync(Path.Combine(fixtures, "demo-shot-01.png"));
                if (workspace.Editor.SourceImage is null) throw new InvalidOperationException("SourceImage unavailable after product preview load");
                await workspace.Editor.ApplyCommand.ExecuteAsync(null);
                await workspace.LoadTargetAsync(Path.Combine(fixtures, "demo-shot-01.png"));
                await workspace.Editor.ApplyCommand.ExecuteAsync(null);
                workspace.Editor.WorkspaceMode = "专业";
                await SizeWindow(size);
                workspace.Editor.ContextRailOpen = true;
                viewport.State = null;
                expander.IsExpanded = false;
                await Capture("ColorStudio-3D-closed", size, expander);
                expander.IsExpanded = true;
                await Capture("ColorStudio-3D-open", size, expander);
                if (!workspace.BuildColorSpaceModelCommand.CanExecute(null)) throw new InvalidOperationException("SourceImage/MatchedImage unavailable; model command disabled");
                await workspace.BuildColorSpaceModelCommand.ExecuteAsync(null);
                if (workspace.ColorSpaceModel is null || viewport.State is null) throw new InvalidOperationException("Product model/view binding failed");
                await Capture("ColorStudio-3D-model", size, expander);
            }
            catch(Exception error)
            {
                foreach (var state in new[]{"closed","open","model"})
                    if (!rows.Any(r=>r.View=="ColorStudio-3D-"+state && r.Size==size.ToString())) RecordUnreachable("ColorStudio-3D-"+state,size,error);
            }
            finally { }
        }
        var paths = rows.Where(r=>r.Path != null).Select(r=>Path.Combine(repo,r.Path!)).ToArray();
        StudioVisualEvidence.ContactSheet(output,"WHOLE_APP_CURRENT_CONTACT_SHEET",paths);
        StudioVisualEvidence.ContactSheet(output,"COLOR_STUDIO_3D_CURRENT_CONTACT_SHEET",rows.Where(r=>r.Path != null && r.View.StartsWith("ColorStudio-3D-")).Select(r=>Path.Combine(repo,r.Path!)));
        foreach (var name in new[]{"WHOLE_APP_CURRENT_CONTACT_SHEET","COLOR_STUDIO_3D_CURRENT_CONTACT_SHEET"})
        {
            var sheet = Path.Combine(output,"contact-sheets",name+".png");
            if(File.Exists(sheet)) File.Copy(sheet,Path.Combine(report,name+".png"),true);
        }
        WriteMatrix();
        var summary = new { ProductSourceSha=productSha, GuardianImplementationSha=implementationSha, EvidenceGeneratedAt=DateTimeOffset.UtcNow, HardGateEnabled=true, Total=rows.Count, Captured=rows.Count(r=>r.Status=="CAPTURED"), Failed=rows.Count(r=>r.Status=="FAILED"), NotReachable=rows.Count(r=>r.Status=="NOT_REACHABLE"), Received=Path.GetRelativePath(repo,received), ApprovedBaseline=false, VisualApproved=false, Status=rows.Any(r=>r.P0Count>0)?"UI_GUARDIAN_CORRECTION_REQUIRED":rows.Any(r=>r.Path==null)?"UI_GUARDIAN_CAPTURE_BLOCKED":"READY_FOR_USER_UI_REVIEW", Renderer="PARTIAL" };
        File.WriteAllText(Path.Combine(report,"CAPTURE_RUN.json"),JsonSerializer.Serialize(summary,new JsonSerializerOptions{WriteIndented=true}));
        Assert.HasCount(33, rows);
        Assert.IsFalse(rows.Any(r=>r.Status!="CAPTURED"), "Guardian states failed; all evidence retained. See SCREENSHOT_MATRIX.json");

        async Task Settle() { await window.Dispatcher.InvokeAsync(window.UpdateLayout,DispatcherPriority.ApplicationIdle); await Task.Delay(220); }
        async Task SizeWindow(VisualCaptureSize size)
        {
            window.WindowState=WindowState.Normal; window.MinWidth=0; window.MinHeight=0; window.Width=size.Width; window.Height=size.Height;
            await Settle();
            // Same explicit production-client arrangement as StudioVisualEvidence.Dpi: independent of monitor work-area caps.
            var root=(FrameworkElement)window.Content; root.Width=size.Width; root.Height=size.Height;
            root.Measure(new Size(size.Width,size.Height)); root.Arrange(new Rect(0,0,size.Width,size.Height)); root.UpdateLayout(); await Settle();
        }
        async Task Capture(string name, VisualCaptureSize size, FrameworkElement? bringIntoView=null)
        {
            await SizeWindow(size);
            if(bringIntoView != null) { bringIntoView.BringIntoView(); await Settle(); }
            var root=(FrameworkElement)window.Content;
            var key=name+"-"+size;
            var path=Path.Combine(received,key+".png");
            var violations=StudioVisualEvidence.FindGeometryViolations(root);
            string? geometryError=null;
            try { StudioVisualEvidence.AuditGeometry(root,key,1,output); }
            catch(InvalidOperationException error) { geometryError=error.Message; }
            StudioVisualEvidence.Png(root,path);
            var audits=UiRenderedAudit.Run(root,path);
            var auditPath=Path.Combine(received,key+".audit.json");
            File.WriteAllText(auditPath,JsonSerializer.Serialize(new { Violations=violations, Audit=audits, HardGateEnabled=true, GeometryError=geometryError },new JsonSerializerOptions{WriteIndented=true}));
            var baseline=Path.Combine(repo,"tests/UIVisual/Baselines",key+".png");
            var visualStatus="BASELINE_MISSING";
            if(File.Exists(baseline))
            {
                var diffPath=Path.Combine(repo,"tests/UIVisual/Diff",stamp,key+".png");
                var metrics=UiVisualDiffEngine.Compare(baseline,path,diffPath); visualStatus=metrics.Status;
                var metricsPath=Path.Combine(repo,"tests/UIVisual/Reports",stamp,key+".json"); Directory.CreateDirectory(Path.GetDirectoryName(metricsPath)!);
                File.WriteAllText(metricsPath,JsonSerializer.Serialize(metrics,new JsonSerializerOptions{WriteIndented=true}));
            }
            var p0=violations.Count(v=>v.Severity=="P0");
            rows.Add(new(name,size.ToString(),p0>0||geometryError!=null?"FAILED":"CAPTURED",Path.GetRelativePath(repo,path).Replace('\\','/'),Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))),p0>0||geometryError!=null?"FAIL":"PASS",p0,"IN_PROCESS_WPF_RENDER",productSha,visualStatus,Path.GetRelativePath(repo,auditPath).Replace('\\','/'),null));
            if(name.StartsWith("ColorStudio-3D-")) File.Copy(path,Path.Combine(report,$"COLOR_STUDIO_3D_CURRENT_{size.Width}_{name[15..].ToUpperInvariant()}.png"),true);
            WriteMatrix();
        }
        void RecordUnreachable(string name,VisualCaptureSize size,Exception error) { rows.Add(new(name,size.ToString(),"NOT_REACHABLE",null,null,"NOT_RUN",null,"IN_PROCESS_WPF_RENDER",productSha,"NOT_RUN",null,error.ToString())); WriteMatrix(); }
        void WriteMatrix() => File.WriteAllText(Path.Combine(report,"SCREENSHOT_MATRIX.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
    }
    internal sealed record CaptureRow(string View,string Size,string Status,string? Path,string? SHA256,string GeometryStatus,int? P0Count,string CaptureKind,string ProductSourceSha,string VisualRegression,string? AuditPath,string? Error);
}
