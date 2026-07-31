using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class Baseline2031AcceptanceTests
{
    [TestMethod]
    public void ToolRegistry_HasUniqueDefinitionsAndRequiredEntries()
    {
        Assert.HasCount(11, ToolRegistry.All);
        Assert.HasCount(10, ToolRegistry.Catalog);
        Assert.HasCount(10, ToolRegistry.Pinnable);
        CollectionAssert.AreEqual(
            new[] { "本地分片", "归片工作区", "整理图片", "拼图", "批量压缩", "批量水印", "删废片", "FTP 工具", "批量重命名", "批量转档", "工具箱" },
            ToolRegistry.All.Select(definition => definition.DisplayName).ToArray());
    }

    [TestMethod]
    public void ToolRegistry_UsesRequiredNamingAndPages()
    {
        var organize = ToolRegistry.Get(ToolId.PhotoOrganize);
        var collage = ToolRegistry.Get(ToolId.Collage);
        Assert.AreEqual("整理图片", organize.DisplayName);
        Assert.AreEqual("PhotoGrouping", organize.TargetPageKey);
        Assert.AreEqual("拼图", collage.DisplayName);
        Assert.AreEqual("Collage", collage.TargetPageKey);
    }

    [TestMethod]
    public void QuickTools_IgnoresUnknownAndNonPinnableLegacyValues()
    {
        CollectionAssert.AreEqual(new[] { "Collage", "PhotoOrganize" }, QuickToolsService.Normalize(["Collage", "Toolbox", "missing", "Collage", "PhotoOrganize"]));
        CollectionAssert.AreEqual(Array.Empty<string>(), QuickToolsService.Normalize(Array.Empty<string>()));
    }

    [TestMethod]
    public void ToolIcons_ContainStableOriginalVectors()
    {
        var path = Path.Combine(Root(), "src/RAWSelectionAssistant/Resources/DesignSystem/Icons.Tools.xaml");
        var text = File.ReadAllText(path.Replace('/', Path.DirectorySeparatorChar));
        foreach (var key in new[] { "ToolIconOrganize", "ToolIconCollage", "ToolIconBatchCompress", "ToolIconToolbox", "ToolIconPin", "ToolIconUnpin" })
        {
            StringAssert.Contains(text, $"x:Key=\"{key}\"");
        }
    }

    [TestMethod]
    public void ToolPages_StayFrameworkOnly()
    {
        var organize = File.ReadAllText(Path.Combine(Root(), "src/RAWSelectionAssistant/Views/OrganizePhotosView.xaml".Replace('/', Path.DirectorySeparatorChar)));
        var collage = File.ReadAllText(Path.Combine(Root(), "src/RAWSelectionAssistant/Views/CollageView.xaml".Replace('/', Path.DirectorySeparatorChar)));
        StringAssert.Contains(organize, "不会执行文件复制、移动、重命名或删除");
        StringAssert.Contains(collage, "不会生成或修改文件");
        StringAssert.Contains(collage, "IsEnabled=\"False\"");
    }

    [TestMethod]
    public void VersionChain_Uses2031Everywhere()
    {
        var root = Root();
        foreach (var path in new[] { "build/Version.props", "src/RAWSelectionAssistant.Core/Models/Branding.cs", "src/RAWSelectionAssistant/RAWSelectionAssistant.csproj", "src/RAWSelectionAssistant/app.manifest", "installer/RAWSelectionAssistant.iss" })
        {
            var text = File.ReadAllText(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar)));
            StringAssert.Contains(text, "2.0.3.1");
        }
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
