using RAWSelectionAssistant.Core.Models;
namespace RAWSelectionAssistant.Tests;
[TestClass]
public sealed class CompanyQueryAggregateTests
{
 [TestMethod]
 public async Task AggregateBytesMatchesPagedOracleAcrossScopeArchiveAndFilter()
 {
  await using var setup=await AssetLibraryP3TestSetup.CreateCanonicalAsync();var folder=await setup.Repository.SaveFolderAsync(new(Guid.NewGuid(),null,"aggregate"));await setup.Repository.AddToFolderAsync([setup.A,setup.B],folder.FolderId);
  foreach(var scope in new[]{AssetQueryScope.Current,AssetQueryScope.AllAssets})foreach(var archived in new[]{false,true})foreach(var rating in new[]{0,3})
  {
   var query=new AssetLibraryQuery(FolderId:folder.FolderId,PageSize:1,IncludeArchived:archived){Document=new(){Scope=scope,IncludeArchived=archived,RootGroup=AssetQueryNode.Group(AssetQueryLogic.All,[AssetQueryNode.Rule(AssetQueryField.Rating,AssetQueryOperator.GreaterThanOrEqual,[rating.ToString()])])}};
   var oracle=await setup.ReadAllAsync(query);Assert.AreEqual(oracle.Sum(x=>x.FileSize),await setup.Repository.QueryTotalBytesAsync(query));
  }
 }
}
