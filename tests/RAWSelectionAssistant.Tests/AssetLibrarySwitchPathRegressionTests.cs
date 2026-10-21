using RAWSelectionAssistant.Core.Services.AssetLibrary;
using RAWSelectionAssistant.Core.Models;
namespace RAWSelectionAssistant.Tests;
[TestClass]
public sealed class AssetLibrarySwitchPathRegressionTests
{
 [TestMethod] public async Task AlternatingCachedLibrariesPreserveIdentityQueryAndReleaseLocks()
 {
  using var temp=new TempDirectory();var service=new AssetLibraryContainerService();var a=await service.CreateAsync(temp.Combine("A.ptlibrary"),"A");var b=await service.CreateAsync(temp.Combine("B.ptlibrary"),"B");
  foreach(var library in new[]{a,b})
  {
   await using var repository=new SqliteAssetLibraryRepository(library.DatabasePath);await repository.InitializeAsync();
   foreach(var name in new[]{"z.jpg","a.jpg"}){var path=temp.CreateFile(library.DisplayName+"/"+name,[1,2,3]);await repository.ImportAsync([new AssetImportRequest(path)]);}
  }
  for(int i=0;i<30;i++)
  {
   var expected=i%2==0?a:b;var opened=await service.OpenAsync(expected.ContainerPath);Assert.AreEqual(expected.LibraryId,opened.LibraryId);Assert.AreEqual(expected.DatabasePath,opened.DatabasePath);
   await using(var repository=new SqliteAssetLibraryRepository(opened.DatabasePath)){await repository.InitializeAsync();var query=await repository.QueryAsync(new AssetLibraryQuery(PageSize:10,SortField:AssetLibrarySortField.FileName,SortDirection:AssetLibrarySortDirection.Ascending));Assert.AreEqual(2,query.TotalCount);CollectionAssert.AreEqual(new[]{"a.jpg","z.jpg"},query.Items.Select(x=>Path.GetFileName(x.SourcePath)).ToArray());var filtered=await repository.QueryAsync(new AssetLibraryQuery(SearchText:"a.jpg",PageSize:10));Assert.AreEqual(1,filtered.TotalCount);}
   await using(await service.AcquireWriteLeaseAsync(opened)){}
  }
  await using(await service.AcquireWriteLeaseAsync(a)){}await using(await service.AcquireWriteLeaseAsync(b)){}
 }
 [TestMethod] public async Task CachedOpenRejectsEveryExistingIntermediateReparseAndRecovers()
 {
  using var temp=new TempDirectory();var service=new AssetLibraryContainerService();
  foreach(var relative in new[]{"database","assets","assets/managed","previews","backups","locks"})
  {
   var library=await service.CreateAsync(temp.Combine(Guid.NewGuid()+".ptlibrary"),"test");await service.OpenAsync(library.ContainerPath);
   var target=Path.Combine(library.ContainerPath,relative.Replace('/',Path.DirectorySeparatorChar));var saved=target+"-saved";var foreign=temp.Combine("foreign-"+Guid.NewGuid());Directory.CreateDirectory(foreign);Directory.Move(target,saved);
   try
   {
    try{Directory.CreateSymbolicLink(target,foreign);}catch(UnauthorizedAccessException){Assert.Inconclusive("Host cannot create a synthetic directory symlink; no security change or fallback.");}
    await Assert.ThrowsExactlyAsync<InvalidDataException>(()=>service.OpenAsync(library.ContainerPath));
   }
   finally{if(Directory.Exists(target)&&File.GetAttributes(target).HasFlag(FileAttributes.ReparsePoint))Directory.Delete(target);Directory.Move(saved,target);}
   Assert.AreEqual(library.LibraryId,(await service.OpenAsync(library.ContainerPath)).LibraryId);
  }
 }
 [TestMethod] public async Task MissingOptionalPathsAndBadManifestKeepRecoverySemantics()
 {
  using var temp=new TempDirectory();var service=new AssetLibraryContainerService();var lib=await service.CreateAsync(temp.Combine("recover.ptlibrary"),"recover");await service.OpenAsync(lib.ContainerPath);
  Directory.Delete(lib.PreviewCachePath);Directory.Delete(Path.Combine(lib.ContainerPath,"backups"));Assert.AreEqual(lib.LibraryId,(await service.OpenAsync(lib.ContainerPath)).LibraryId);
  var manifest=Path.Combine(lib.ContainerPath,AssetLibraryContainerService.ManifestFileName);var bytes=await File.ReadAllBytesAsync(manifest);await File.WriteAllTextAsync(manifest,"{broken");await Assert.ThrowsExactlyAsync<InvalidDataException>(()=>service.OpenAsync(lib.ContainerPath));await File.WriteAllBytesAsync(manifest,bytes);Assert.AreEqual(lib.LibraryId,(await service.OpenAsync(lib.ContainerPath)).LibraryId);
 }
}
