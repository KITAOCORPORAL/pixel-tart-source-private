using System.IO;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows;
using System.Windows.Media;
using System.Windows.Automation;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Views;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class StudioLocalizationTests
{
    [TestMethod]
    public void ProductXamlLiteralsAndResourcesHaveThreeLanguageCoverage()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src", "RAWSelectionAssistant"))) directory = directory.Parent;
        Assert.IsNotNull(directory);
        var source = File.ReadAllText(Path.Combine(directory.FullName, "src/RAWSelectionAssistant/Views/ReferenceColorWorkspaceView.xaml"));
        Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(source, "(?:Text|Content|Header|ToolTip|AutomationProperties.Name)=\"[^\"{]*[\\u4e00-\\u9fff][^\"{]*\""), "Product literals must use resources, not hard-coded Chinese.");
        var keys = System.Text.RegularExpressions.Regex.Matches(source, @"\{views:StudioText (?:Key=')?([^}']+)'?\}").Select(m => m.Groups[1].Value).Distinct();
        var service = new StudioLocalizationService(null);
        foreach (var language in service.Languages)
        {
            foreach (var key in keys) Assert.IsTrue(service.HasTranslation(language.Code, key), $"Missing {language.Code}/{key}");
            using var stream = typeof(StudioLocalizationService).Assembly.GetManifestResourceStream($"RAWSelectionAssistant.Resources.Studio.{language.Code}.json");
            using var json = System.Text.Json.JsonDocument.Parse(stream!);
            var names = json.RootElement.EnumerateObject().Select(p => p.Name).ToArray();
            Assert.AreEqual(names.Length, names.Distinct().Count(), $"Duplicate keys in {language.Code}");
        }
    }
    [TestMethod]
    public void StudioLanguagePersistsAndDoesNotChangeStableIdentifiers()
    {
        var root=Path.Combine(Path.GetTempPath(),"PixelTart-Locale-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var path=Path.Combine(root,"studio-language.json");var service=new StudioLocalizationService(path);
            Assert.AreEqual("zh-CN",service.Language);Assert.AreEqual("参考图片",service["Reference"]);
            Assert.IsTrue(service.SetLanguage("en-US"));Assert.AreEqual("Reference",service["Reference"]);Assert.AreEqual("Reference",new StudioLocalizationService(path)["Reference"]);
            Assert.IsTrue(service.SetLanguage("zh-TW"));Assert.AreEqual("參考圖片",service["Reference"]);
            Assert.IsFalse(service.SetLanguage("invalid"));Assert.AreEqual("zh-TW",service.Language);
            Assert.AreEqual("unchanged-user-key",service["unchanged-user-key"]);
            Assert.IsNull(service.LastPersistenceError);
            foreach(var language in service.Languages)foreach(var key in new[]{"Reference","Space","Nodes","FilmPreset","Color","Levels","Curve","Details","Film","Creative","QuickExport","SphereLabel","SelectionSoftness"})Assert.IsTrue(service.HasTranslation(language.Code,key));
        }
        finally{Directory.Delete(root,true);}
    }
    [TestMethod]
    public Task BoundPresentationUpdatesWithoutTranslatingTagsOrUserNames() => RunSta(()=>
    {
        var service=new StudioLocalizationService(null);var label=new TextBlock{Tag="Reference"};
        label.SetBinding(TextBlock.TextProperty,new Binding("[Reference]"){Source=service});Assert.AreEqual("参考图片",label.Text);
        service.SetLanguage("en-US");Assert.AreEqual("Reference",label.Text);Assert.AreEqual("Reference",label.Tag);
        service.SetLanguage("zh-TW");Assert.AreEqual("參考圖片",label.Text);Assert.AreEqual("Reference",label.Tag);
        return Task.CompletedTask;
    });
    [TestMethod]
    public Task RealStudioModeLabelsSwitchWhileStableTagsRemainUnchanged() => RunSta(async()=>
    {
        EnsureTestApplication();var strings=StudioLocalizationService.Current;var original=strings.Language;
        try
        {
            strings.SetLanguage("zh-CN",persist:false);var view=new ReferenceColorWorkspaceView();
            var aux=(ListBox)view.FindName("AuxModes");var tools=(ListBox)view.FindName("ToolModes");
            Assert.AreEqual("参考图片",((ListBoxItem)aux.Items[0]).Content);Assert.AreEqual("色彩",((ListBoxItem)tools.Items[1]).Content);
            strings.SetLanguage("en-US",persist:false);await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.DataBind);
            Assert.AreEqual("Reference",((ListBoxItem)aux.Items[0]).Content);Assert.AreEqual("Color",((ListBoxItem)tools.Items[1]).Content);
            Assert.AreEqual("Reference",((ListBoxItem)aux.Items[0]).Tag);Assert.AreEqual("Space",((ListBoxItem)aux.Items[1]).Tag);
            aux.SelectedIndex=1;tools.SelectedIndex=2;
            Assert.AreEqual(System.Windows.Visibility.Visible,((System.Windows.FrameworkElement)view.FindName("LevelsToolPage")).Visibility);
            strings.SetLanguage("zh-TW",persist:false);await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.DataBind);
            Assert.AreEqual("參考圖片",((ListBoxItem)aux.Items[0]).Content);Assert.AreEqual("色階",((ListBoxItem)tools.Items[2]).Content);
        }
        finally { strings.SetLanguage(original,persist:false); }
    });
    [TestMethod]
    public Task ParameterAndCurveLabelsSwitchWithoutMutatingStackKeysOrUserNodeName() => RunSta(async () =>
    {
        EnsureTestApplication(); var strings = StudioLocalizationService.Current; var original = strings.Language;
        using var editor = new TetherReferenceModeViewModel();
        var exposure = ColorStudioToolCatalog.GetParameters(ColorStudioNodeType.BasicTone).Single(p => p.Key == "exposure");
        editor.SetToolParameter(exposure, .7); editor.SelectedAdjustmentNode = editor.ToolNode(ColorStudioNodeType.BasicTone); editor.SelectedNodeName = "用户保留 English 名称";
        var before = System.Text.Json.JsonSerializer.Serialize(editor.AdjustmentStack);
        try
        {
            strings.SetLanguage("zh-CN", persist: false);
            var panel = new StudioToolPanel { NodeTypes = "BasicTone,WhiteBalance", DataContext = editor };
            panel.Measure(new Size(320, 2000)); panel.Arrange(new Rect(0, 0, 320, 2000)); panel.UpdateLayout();
            var slider = Descendants<Slider>(panel).Single(s => Equals(s.Tag, "exposure"));
            Assert.AreEqual("曝光", AutomationProperties.GetName(slider));
            var curve = new StudioCurveEditor();
            strings.SetLanguage("en-US", persist: false); await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.DataBind);
            Assert.AreEqual("Exposure", AutomationProperties.GetName(slider));
            Assert.IsTrue(Descendants<TextBlock>(panel).Any(t => t.Text == "Exposure · EV"));
            Assert.AreEqual("Reset channel", curve.Children.OfType<WrapPanel>().Single().Children.OfType<Button>().First().Content);
            strings.SetLanguage("zh-TW", persist: false); await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.DataBind);
            Assert.IsTrue(Descendants<TextBlock>(panel).Any(t => t.Text == "基礎曝光"));
            Assert.AreEqual("重設通道", curve.Children.OfType<WrapPanel>().Single().Children.OfType<Button>().First().Content);
            Assert.AreEqual(before, System.Text.Json.JsonSerializer.Serialize(editor.AdjustmentStack));
            Assert.AreEqual(.7, slider.Value); Assert.AreEqual("exposure", slider.Tag); Assert.AreEqual("用户保留 English 名称", editor.SelectedAdjustmentNode!.Name);
            foreach (var language in strings.Languages)
                foreach (var parameter in ColorStudioToolCatalog.Parameters)
                { Assert.IsTrue(strings.HasTranslation(language.Code, parameter.Label), parameter.Label); Assert.IsTrue(strings.HasTranslation(language.Code, parameter.Group), parameter.Group); }
        }
        finally { strings.SetLanguage(original, persist: false); }
    });
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        { var child = VisualTreeHelper.GetChild(root, i); if (child is T typed) yield return typed; foreach (var item in Descendants<T>(child)) yield return item; }
    }
}
