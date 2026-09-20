using System.IO;
using System.Text.Json;
using System.Windows.Automation;

namespace PixelTart.InstalledAcceptance;

internal static partial class Program
{
    internal static T RequireUnique<T>(IReadOnlyCollection<T> matches, string step) => matches.Count == 1
        ? matches.Single() : throw new InvalidOperationException($"Selector must resolve exactly one element: {step}; MatchCount={matches.Count}");
    private sealed class AutomationElementIdentity : IEqualityComparer<AutomationElement>
    {
        internal static readonly AutomationElementIdentity Instance = new();
        public bool Equals(AutomationElement? x, AutomationElement? y) => x is not null && y is not null && Automation.Compare(x, y);
        public int GetHashCode(AutomationElement obj) => string.Join(",", obj.GetRuntimeId()).GetHashCode(StringComparison.Ordinal);
    }
    internal sealed record ElementInfo(string Name, string AutomationId, string ControlType, string ClassName, double[] BoundingRectangle);
    internal sealed record MatchInfo(ElementInfo Element, string? ParentName, string? ParentAutomationId, ElementInfo[] AncestorChain, string? Error = null);
    internal sealed record SelectorDiagnostic(string StepId, Step RequestedSelector, int MatchCount, string Phase, MatchInfo[] Matches)
    {
        public string Action => RequestedSelector.Action;
        public object Scope => new { RequestedSelector.ScopePreset, Resolved = ResolvePreset(RequestedSelector).ScopePath, RequestedSelector.AncestorAutomationId, RequestedSelector.AncestorName, RequestedSelector.AncestorControlType };
        public string? RequestedAutomationId => RequestedSelector.AutomationId ?? RequestedSelector.IdSelector;
        public string? RequestedName => RequestedSelector.Name;
        public string? RequestedControlType => RequestedSelector.ControlType;
    }
    internal static SelectorDiagnostic Diagnostic(Step step, string phase, MatchInfo[] matches) => new(step.Id, step, matches.Length, phase, matches);
    private sealed partial class Runner
    {
        private void VerifyOwnedProcess()
        {
            if (process is null || process.HasExited || process.Id != processId || !string.Equals(process.MainModule?.FileName, Exe, StringComparison.OrdinalIgnoreCase) || window?.Current.ProcessId != process.Id)
                throw new InvalidOperationException("Refusing close: PID/path/window do not belong to this acceptance run");
        }
        private readonly List<SelectorDiagnostic> diagnostics = [];
        private static ElementInfo Describe(AutomationElement element)
        {
            var c = element.Current; var b = c.BoundingRectangle;
            return new(c.Name, c.AutomationId, c.ControlType.ProgrammaticName, c.ClassName, b.IsEmpty ? [] : [b.X,b.Y,b.Width,b.Height]);
        }
        private static MatchInfo DescribeMatch(AutomationElement element)
        {
            try
            {
                var chain = new List<ElementInfo>();
                for (var parent = TreeWalker.RawViewWalker.GetParent(element); parent is not null && chain.Count < 64; parent = TreeWalker.RawViewWalker.GetParent(parent)) chain.Add(Describe(parent));
                var immediate = TreeWalker.RawViewWalker.GetParent(element);
                return new(Describe(element), immediate?.Current.Name, immediate?.Current.AutomationId, chain.ToArray());
            }
            catch (Exception e) { return new(new("<unavailable>", "", "", "", []), null, null, [], e.Message); }
        }
        private void Diagnose(Step s, AutomationElement[] matches, string phase)
        {
            diagnostics.Add(Diagnostic(s, phase, matches.Select(DescribeMatch).ToArray()));
            File.WriteAllText(Out("selector-diagnostics.json"), JsonSerializer.Serialize(diagnostics, Json));
        }
        private void AuditCheckpoint(string id)
        {
            if (plan.AuditCheckpoints is null || !plan.AuditCheckpoints.TryGetValue(id, out var selectors)) return;
            // Resolve all siblings even when one is ambiguous; never perform their actions.
            var failures = new List<string>();
            foreach (var s in selectors)
            {
                try { One(s); Record("audit:" + id + ":" + s.Id, "UNIQUE_1"); }
                catch (Exception e) { failures.Add(s.Id); Record("audit:" + id + ":" + s.Id, "AUDIT_FAIL", e.Message); }
            }
            if (failures.Count != 0) throw new InvalidOperationException("Selector checkpoint failed: " + string.Join(", ", failures));
        }
    }
    private static int SelectorTests()
    {
        var count = 0;
        void Test(string name, Action test) { test(); count++; Console.WriteLine(name + " PASS"); }
        void MustFail(Action action) { try { action(); } catch (InvalidOperationException) { return; } throw new Exception("Ambiguity unexpectedly accepted"); }
        var outer = new ElementInfo("拍摄日期", "", "ControlType.Custom", "DatePicker", [0,0,100,30]);
        var inner = new ElementInfo("拍摄日期", "PART_TextBox", "ControlType.Edit", "DatePickerTextBox", [0,0,80,30]);
        Test("AmbiguousSelectorFailsClosedTests", () => { MustFail(() => RequireUnique(new[] { outer, inner }, "date-value")); MustFail(() => RequireUnique(Array.Empty<ElementInfo>(), "date-value")); });
        Test("DuplicateNameDoesNotChooseFirstTests", () => { MustFail(() => RequireUnique(new[] { inner, outer }, "reverse")); });
        Test("ScopedAutomationIdResolvesUniqueElementTests", () => {
            var fixture = new[] { (Scope: "create", Item: inner), (Scope: "other-modal", Item: inner), (Scope: "create", Item: outer) };
            var matches = fixture.Where(x => x.Scope == "create" && x.Item.AutomationId == "PART_TextBox" && x.Item.ControlType == "ControlType.Edit").Select(x => x.Item).ToArray();
            if (RequireUnique(matches, "scoped") != inner) throw new Exception("Wrong target");
        });
        foreach (var file in new[] { "planning-full.plan.json", "upgrade-full.plan.json" })
        {
            var p = LoadPlan(Path.Combine(AppContext.BaseDirectory, file));
            Test("DateValueSelectorUsesInnerPartTextBoxTests:" + file, () => {
                foreach (var id in new[] { "date-value", "date-check" }) {
                    var s = RequireUnique(p.Steps.Where(s => s.Id == id).ToArray(), id);
                    if (s.AutomationId != "PART_TextBox" || s.ControlType != "Edit" || s.ScopePath?.Length != 3 || s.ScopePath[1].Name != "新建策划案" || !s.ScopePath[1].Heading || s.ScopePath[2].ControlType != "Custom") throw new Exception("Date selector contract violated");
                }
            });
            Test("FullSelectorScopeAuditTests:" + file, () => {
                foreach (var s in p.Steps.Where(s => s.Name is not null || s.AutomationId is not null || s.DescendantName is not null)) {
                    if (s.ControlType is null) throw new Exception("Missing type: " + s.Id);
                    if (s.Name is not null && s.ScopePreset is null && s.ScopePath is null && s.AncestorName is null && s.AncestorAutomationId is null && s.AncestorControlType is null && s.ControlType != "Window") throw new Exception("Unscoped name: " + s.Id);
                }
                foreach (var required in new[] { "create-open", "edit", "preview-open" }) if (p.AuditCheckpoints?.ContainsKey(required) != true) throw new Exception("Missing checkpoint " + required);
            });
        }
        Test("SelectorDiagnosticContainsAllMatchesTests", () => {
            var s = new Step("date-value", "setValue", Name: "拍摄日期");
            var d = Diagnostic(s, "target", new[] { new MatchInfo(outer,"新建策划案","",[outer]), new MatchInfo(inner,"拍摄日期","",[outer]) });
            var roundtrip = JsonSerializer.Deserialize<SelectorDiagnostic>(JsonSerializer.Serialize(d))!;
            if (roundtrip.MatchCount != 2 || roundtrip.Matches.Length != 2 || roundtrip.Matches.Any(x => x.AncestorChain.Length == 0) || roundtrip.RequestedSelector != s) throw new Exception("Incomplete diagnostics");
        });
        Console.WriteLine($"Selector tests: {count} PASS; offline fixtures, NO UI executed"); return 0;
    }
}
