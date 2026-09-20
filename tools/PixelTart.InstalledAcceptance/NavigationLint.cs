using System.IO;
using System.Text.Json;

namespace PixelTart.InstalledAcceptance;
internal static partial class Program
{
    internal sealed class MissingScopeException(string message) : TimeoutException(message);
    internal static T RequireScope<T>(IReadOnlyCollection<T> matches, string step) => matches.Count == 1 ? matches.Single() : matches.Count == 0 ? throw new MissingScopeException($"Scope resolution failed: {step}; MatchCount=0") : throw new InvalidOperationException($"Scope resolution failed: {step}; MatchCount={matches.Count}");
    internal static Step ResolvePreset(Step s) => s.ScopePreset switch
    {
        null => s,
        "PrimaryNavigation" => s with { ScopePath = [new(AutomationId: "SidebarNavigationScroll", ControlType: "Pane")] },
        "MainWindow" => s,
        _ => throw new InvalidDataException("Unknown ScopePreset: " + s.ScopePreset)
    };
    internal sealed record LintIssue(string StepId, string Severity, string Code, string Explanation);
    private static readonly string[] SelectorActions = ["invoke","setValue","select","expand","focusKey","assertPresent","assertAbsent","waitPresent","waitAbsent","assertEnabled","assertSelected","assertText","assertDate"];
    private static bool HasSelector(Step s) => SelectorActions.Contains(s.Action);
    private static bool Scoped(Step s) => s.ScopePreset is not null || s.ScopePath is {Length: >0} || s.AncestorAutomationId is not null || s.AncestorName is not null || s.AncestorControlType is not null || s.ScopeAnchorName is not null;
    internal static List<LintIssue> Lint(Plan p)
    {
        var issues = new List<LintIssue>();
        void Add(Step s,string severity,string code,string why) => issues.Add(new(s.Id,severity,code,why));
        foreach(var group in p.Steps.GroupBy(s=>s.Id).Where(g=>g.Count()>1)) issues.Add(new(group.Key,"ERROR","DUPLICATE_ID","Step ID must be unique"));
        foreach(var s in p.Steps.Concat(p.AuditCheckpoints?.Values.SelectMany(x=>x) ?? []))
        {
            if(!HasSelector(s)) continue;
            if(s.AutomationId is null && s.IdSelector is null && s.Name is null && s.DescendantName is null && !(Scoped(s)&&s.ControlType is not null)) Add(s,"ERROR","MISSING_SELECTOR","No specific selector");
            if(s.ControlType is null) Add(s,"ERROR","MISSING_TYPE","ControlType required");
            if(s.AncestorAutomationId=="SidebarRoot" || s.AncestorName=="侧栏根区域" || s.ScopePath?.Any(x=>x.AutomationId=="SidebarRoot" || x.Name=="侧栏根区域")==true) Add(s,"ERROR","LANDMARK_AS_SCOPE","SidebarRoot is a sibling landmark, never an ancestor");
            if(s.ScopePreset is not (null or "PrimaryNavigation" or "MainWindow")) Add(s,"ERROR","UNKNOWN_PRESET","Unrecognized preset");
            if(s.ScopePreset is not null && (s.ScopePath is not null || s.AncestorAutomationId is not null || s.AncestorName is not null || s.AncestorControlType is not null)) Add(s,"ERROR","MIXED_PRESET","Preset cannot be combined with manual ancestor");
            if((s.AutomationId?.StartsWith("PrimaryNavigation",StringComparison.Ordinal)==true || s.AutomationId is "AssetLibraryNavigationButton" or "SidebarSettingsButton") && s.ScopePreset!="PrimaryNavigation") Add(s,"ERROR","NAV_PRESET_REQUIRED","All sidebar navigation must use PrimaryNavigation");
            if(!Scoped(s) && s.ControlType!="Window") Add(s,"ERROR","UNSCOPED_GLOBAL","Scope required even for unique global ID");
            if(s.Name is not null && s.AutomationId is null && s.IdSelector is null)
                Add(s,"WARNING","SCOPED_NAME","No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback.");
            if(s.Name is "编辑" or "关闭" or "确定" or "保存" or "导出" or "更多" or "下一步")
                Add(s,Scoped(s)?"WARNING":"ERROR","GENERIC_LABEL","Generic label must stay in its declared content/modal scope; live checkpoint verifies uniqueness.");
        }
        return issues;
    }
    private static string Category(Step s)
    {
        var id=s.Id;
        if(s.ScopePreset=="PrimaryNavigation")return "GLOBAL_NAV";
        if(id.Contains("date"))return "DATE_PICKER";
        if(s.ExternalDialog)return "FILE_DIALOG";
        if(id.Contains("booking"))return "BOOKING_PICKER";
        if(s.ControlType=="MenuItem" || id.StartsWith("menu"))return "CONTEXT_MENU";
        if(id.StartsWith("tether") || id=="shoot")return "TETHER";
        if(id.StartsWith("online"))return "ONLINE_SELECTION";
        if(id.StartsWith("create"))return "PLANNING_MODAL";
        if(id.StartsWith("module") || new[]{"文字","参考图","情绪板","镜头清单","灯光图","服化道","文件"}.Contains(s.Name))return "PLANNING_CONTENT_NAV";
        if(id.StartsWith("quick") || id.StartsWith("reference") || id.StartsWith("import"))return "REFERENCE_GRID";
        if(id.StartsWith("export") || id.StartsWith("quality"))return "PLANNING_MODAL";
        if(id.Contains("preview") || id=="more")return "PLANNING_HEADER";
        return HasSelector(s)?"PLANNING_CONTENT":"LIFECYCLE_OR_EVIDENCE";
    }
    private static int LintPlanFile(string file)
    {
        try {
            var p=JsonSerializer.Deserialize<Plan>(File.ReadAllText(file),Json)!;var issues=Lint(p);
            var output=Path.ChangeExtension(Path.GetFullPath(file),null)!.Replace(".plan","")+".selector-lint.json";
            File.WriteAllText(output,JsonSerializer.Serialize(new {Plan=Path.GetFileName(file), StepCount=p.Steps.Length,Errors=issues.Count(x=>x.Severity=="ERROR"),Warnings=issues.Count(x=>x.Severity=="WARNING"),Issues=issues,Steps=p.Steps.Select(s=>new {s.Id,s.Action,Category=Category(s),Selector=s,ResolvedScope=ResolvePreset(s).ScopePath,ExpectedMatchCount=HasSelector(s)?(s.Action is "assertAbsent" or "waitAbsent"?"0":s.Optional?"0 or 1":"1"):"N/A",Evidence="Static schema/source audit; not live verification"})},Json));
            Console.WriteLine($"Selector lint: {p.Steps.Length} steps, {issues.Count(x=>x.Severity=="ERROR")} ERROR, {issues.Count(x=>x.Severity=="WARNING")} WARNING: {output}");
            return issues.Any(x=>x.Severity=="ERROR")?1:0;
        } catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
    private static int NavigationTests()
    {
        int count=0;void Test(string n,Action f){f();count++;Console.WriteLine(n+" PASS");}
        Step Nav(string id)=>new("nav","assertPresent",AutomationId:id,ControlType:"Button",ScopePreset:"PrimaryNavigation");
        void Correct(Step s){var x=ResolvePreset(s);if(x.ScopePath?.Single().AutomationId!="SidebarNavigationScroll" || x.ControlType!="Button")throw new Exception("Bad navigation preset");}
        Test("PrimaryNavigationUsesSidebarNavigationScrollTests",()=>Correct(Nav("PrimaryNavigationPlanning")));
        var p=JsonSerializer.Deserialize<Plan>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"planning-full.plan.json")),Json)!;
        Test("SidebarRootCannotBeUsedAsNavigationAncestorTests",()=>{if(!Lint(p with {Steps=[new("bad","invoke",AutomationId:"PrimaryNavigationPlanning",ControlType:"Button",AncestorAutomationId:"SidebarRoot")]}).Any(x=>x.Code=="LANDMARK_AS_SCOPE"))throw new Exception("Landmark accepted");});
        Test("PlanningNavigationSelectorResolvesUniqueButtonTests",()=>{var nodes=new[]{(Parent:"SidebarRoot",Id:""),(Parent:"SidebarNavigationScroll",Id:"PrimaryNavigationPlanning")};var s=ResolvePreset(Nav("PrimaryNavigationPlanning"));RequireUnique(nodes.Where(x=>x.Parent==s.ScopePath!.Single().AutomationId&&x.Id==s.AutomationId).ToArray(),"nav");});
        Test("OnlineSelectionNavigationSelectorUsesSameScopeTests",()=>Correct(Nav("PrimaryNavigationOnlineSelection")));
        Test("TetherNavigationSelectorUsesSameScopeTests",()=>Correct(Nav("PrimaryNavigationTether")));
        Test("DateValueStillTargetsPartTextBoxTests",()=>{var s=p.Steps.Single(x=>x.Id=="date-value");if(s.AutomationId!="PART_TextBox"||s.ControlType!="Edit"||s.ScopePath?.Length!=3)throw new Exception("Date regression");});
        Test("AmbiguousSelectorStillFailsClosedTests",()=>{foreach(var n in new[]{0,2}){try{RequireUnique(Enumerable.Range(0,n).ToArray(),"ambiguous");}catch(InvalidOperationException){continue;}throw new Exception("Uniqueness weakened");}});
        Test("ScopeResolutionFailureReportsScopeTests",()=>{try{RequireScope(Array.Empty<int>(),"planning-enabled");}catch(MissingScopeException e)when(e.Message.StartsWith("Scope resolution failed",StringComparison.Ordinal)){return;}throw new Exception("Wrong diagnostic");});
        Test("LintRegressionTests",()=>{var bad=p with {Steps=[new("same","invoke",Name:"编辑"),new("same","invoke")]};var codes=Lint(bad).Select(x=>x.Code).ToArray();foreach(var code in new[]{"MISSING_TYPE","MISSING_SELECTOR","UNSCOPED_GLOBAL","DUPLICATE_ID","GENERIC_LABEL"})if(!codes.Contains(code))throw new Exception(code);});
        Console.WriteLine($"Navigation regression: {count} PASS, 0 FAIL; offline only");return 0;
    }
}
