using System.Security.Cryptography;
using System.Text.Json;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Tests;
[TestClass]
public sealed class PlanningImageStorageTests
{
    private static (PlanningDocumentImage Image,string Path) Fixture(string root)
    {
        var doc=Guid.NewGuid();var id=Guid.NewGuid();var data=new byte[]{1,2,3,4};var path=Path.Combine(root,PlanningImageStorage.RelativePath(doc,id));Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllBytes(path,data);
        return(new(id,doc,null,PlanningImageStorage.RelativePath(doc,id),Convert.ToHexString(SHA256.HashData(data)),"正文","合成测试图","图注",10,10,0){StorageRoot=root},path);
    }
    [TestMethod]
    public void RelativePathsRequireBoundRootIdentityHashAndRejectTraversal()
    {
        using var temp=new TempDirectory();var (image,path)=Fixture(temp.Path);
        Assert.AreEqual(Path.GetFullPath(path),PlanningImageStorage.Resolve(image));
        Assert.IsFalse(JsonSerializer.Serialize(image).Contains("StorageRoot"));
        foreach(var value in new[]{"../"+image.CopyPath,"images/../"+image.DocumentId.ToString("N")+"/"+image.DocumentImageId.ToString("N")+".png",$"images/{Guid.NewGuid():N}/{image.DocumentImageId:N}.png","C:relative.png","images/other.png"})
            Assert.Throws<InvalidDataException>(()=>PlanningImageStorage.Bind(image with{CopyPath=value},temp.Path));
        Assert.Throws<InvalidDataException>(()=>PlanningImageStorage.Bind(image with{StorageRoot=null}));
        var error=Assert.Throws<InvalidDataException>(()=>PlanningImageStorage.Bind(image with{CopyHash=new string('A',64)}));StringAssert.Contains(error.Message,image.DocumentImageId.ToString("D"));StringAssert.Contains(error.Message,image.Title);
        File.Delete(path);var missing=Assert.Throws<InvalidDataException>(()=>PlanningImageStorage.Bind(image));StringAssert.Contains(missing.Message,"副本缺失");
    }
    [TestMethod]
    public void LegacyExactIdentityFallbackMigratesOnlyAfterHashValidation()
    {
        using var temp=new TempDirectory();var old=Path.Combine(temp.Path,"old");var moved=Path.Combine(temp.Path,"moved");var (relative,path)=Fixture(old);var image=relative with{CopyPath=path,StorageRoot=null};
        Assert.AreEqual(Path.GetFullPath(path),PlanningImageStorage.Resolve(image));
        var target=Path.Combine(moved,relative.CopyPath);Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(path,target);
        File.WriteAllBytes(path,[99]);Assert.Throws<InvalidDataException>(()=>PlanningImageStorage.Bind(image,moved)); // Existing but corrupt old path may not silently fall back.
        File.Delete(path);var rebound=PlanningImageStorage.Bind(image,moved);Assert.AreEqual(relative.CopyPath,rebound.CopyPath);Assert.AreEqual(Path.GetFullPath(target),PlanningImageStorage.Resolve(rebound));
        File.WriteAllBytes(target,[88]);Assert.Throws<InvalidDataException>(()=>PlanningImageStorage.Bind(image,moved));
    }
    [TestMethod]
    public void ReparseEscapeIsRefusedWithoutOpeningForeignCopy()
    {
        using var temp=new TempDirectory();var root=Path.Combine(temp.Path,"root");var foreign=Path.Combine(temp.Path,"foreign");var (image,path)=Fixture(foreign);
        Directory.CreateDirectory(root);var junction=Path.Combine(root,"images");
        using var command=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe"){UseShellExecute=false,CreateNoWindow=true,ArgumentList={"/c","mklink","/J",junction,Path.Combine(foreign,"images")}})!;command.WaitForExit();Assert.AreEqual(0,command.ExitCode,"Required junction test could not prepare fixture.");
        try{Assert.Throws<InvalidDataException>(()=>PlanningImageStorage.Bind(image with{StorageRoot=root},root));Assert.IsTrue(File.Exists(path));}
        finally{Directory.Delete(junction);}
    }
    [TestMethod]
    public void InventoryIsReadOnlyProtectsDraftRecoveryAndUnknownFiles()
    {
        using var temp=new TempDirectory();var (image,path)=Fixture(temp.Path);var extra=Path.Combine(Path.GetDirectoryName(path)!,Guid.NewGuid().ToString("N")+".png");File.WriteAllBytes(extra,[7]);
        File.WriteAllText(Path.Combine(temp.Path,"plan.planning.json.draft"),JsonSerializer.Serialize(new{DocumentImageId=image.DocumentImageId,DocumentId=image.DocumentId}));
        var result=PlanningImageInventoryScanner.Scan(temp.Path,image.DocumentId);Assert.IsFalse(result.DeletionAllowed);Assert.IsTrue(result.Files.Any(f=>f.Disposition=="REFERENCED_OR_RECOVERY_KEEP"));Assert.IsTrue(result.Files.Any(f=>f.Disposition=="UNREFERENCED_CANDIDATE_KEEP"));Assert.IsTrue(File.Exists(extra));
        File.WriteAllText(Path.Combine(temp.Path,"broken.planning.json"),"{broken");result=PlanningImageInventoryScanner.Scan(temp.Path,image.DocumentId);Assert.IsTrue(result.OpenIssues.Count>0);Assert.IsFalse(result.Files.Any(f=>f.Disposition=="UNREFERENCED_CANDIDATE_KEEP"));
    }
}
