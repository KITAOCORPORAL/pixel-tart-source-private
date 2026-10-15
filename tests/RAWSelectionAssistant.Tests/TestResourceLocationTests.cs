namespace RAWSelectionAssistant.Tests;
[TestClass]
public sealed class TestResourceLocationTests
{
    [TestMethod] public void ExplicitConfigurationDoesNotDependOnAssemblyAncestorOrDrive()
    {
        var root=TestResources.Root;
        foreach(var host in new[]{Path.Combine(Path.GetTempPath(),"external-host-a"),Path.Combine(Path.GetTempPath(),"deep","host-b")})
        {
            Directory.CreateDirectory(host);
            try{File.WriteAllText(Path.Combine(host,"test-resource-root.txt"),root);Assert.AreEqual(root,TestResources.Resolve(null,host));Assert.AreEqual(root,TestResources.Resolve(root,host));}
            finally{File.Delete(Path.Combine(host,"test-resource-root.txt"));Directory.Delete(host);}
        }
        Assert.Throws<InvalidDataException>(()=>TestResources.Resolve("relative-root",Path.GetTempPath()));
        Assert.Throws<DirectoryNotFoundException>(()=>TestResources.Resolve(Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N")),Path.GetTempPath()));
    }
}
