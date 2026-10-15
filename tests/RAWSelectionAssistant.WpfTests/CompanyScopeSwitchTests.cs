using System.IO;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Windows.Threading;
using Microsoft.Data.Sqlite;
using PixelTart.Modules.AssetLibrary;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
using RAWSelectionAssistant.Core.Services.Tasks;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass]
public sealed class CompanyScopeSwitchTests
{
 [TestMethod]
 public Task ScopeSwitchReusesGlobalCountsAndPreservesVisibleSortAndMutationRefresh()=>RunSta(async()=>
 {
  EnsureTestApplication();var root=Path.Combine(Path.GetTempPath(),"company-scope-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);var db=Path.Combine(root,"assets.db");
  await using var repo=new SqliteAssetLibraryRepository(db);await repo.InitializeAsync();
  await using(var c=new SqliteConnection("Data Source="+db)){await c.OpenAsync();await using var tx=(SqliteTransaction)await c.BeginTransactionAsync();for(int i=0;i<10128;i++){await using var cmd=c.CreateCommand();cmd.Transaction=tx;cmd.CommandText="INSERT INTO AssetItems(AssetId,SourcePath,NormalizedSourcePath,DuplicateDiscriminator,DisplayName,Extension,MediaType,FileSize,AddedAt,ModifiedAt,Rating,Comment,IsMissing,IsArchived,ImportMode) VALUES($id,$path,$path,'',$name,'.jpg','Image',1024,$date,$date,$rating,'batch',0,$archive,'Reference');";cmd.Parameters.AddWithValue("$id",Guid.NewGuid().ToString("D"));cmd.Parameters.AddWithValue("$path","synthetic://scope/"+i);cmd.Parameters.AddWithValue("$name",$"synthetic-{i:00000}");cmd.Parameters.AddWithValue("$date",new DateTimeOffset(2026,9,2,0,0,0,TimeSpan.Zero).AddSeconds(-i).ToString("O"));cmd.Parameters.AddWithValue("$rating",i%6);cmd.Parameters.AddWithValue("$archive",i>=10000?1:0);await cmd.ExecuteNonQueryAsync();}await tx.CommitAsync();}
  await using var vm=new AssetLibraryViewModel(db,new TaskOperationBridge(),productDatabasePath:Path.Combine(root,"product.db"),inspirationTrayDatabasePath:Path.Combine(root,"tray.db"),onlineSelectionWorkspaceFile:Path.Combine(root,"online.json"));await vm.InitializeAsync();var counts=vm.SystemCollections.ToDictionary(x=>x.Collection,x=>x.Count);var traces=new List<AssetLibraryQueryDiagnostics.Query>();using var capture=AssetLibraryQueryDiagnostics.Capture(traces.Add);
  traces.Clear();
  for(int i=0;i<3;i++){vm.P3QueryScope=AssetQueryScope.Current;await Scope(vm);traces.Clear();var watch=Stopwatch.StartNew();vm.P3QueryScope=AssetQueryScope.AllAssets;await Scope(vm);await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);watch.Stop();Assert.AreEqual(10000,vm.P2QueryTotalCount);Assert.HasCount(1,traces,"Scope-only navigation must not repeat global count page queries.");foreach(var item in vm.SystemCollections)Assert.AreEqual(counts[item.Collection],item.Count);var expected=await repo.QueryAsync(new(PageSize:500){Document=new(){Scope=AssetQueryScope.AllAssets,RootGroup=AssetQueryNode.Group(AssetQueryLogic.All)}});CollectionAssert.AreEqual(expected.Items.Select(x=>x.AssetId).ToArray(),vm.AssetCards.Select(x=>x.Asset.AssetId).ToArray());}
  vm.RefreshCommand.Execute(null);await vm.RefreshCommand.ExecutionTask;Assert.IsTrue(traces.Count>1,"Ordinary refresh still recalculates counts after mutation/external refresh.");
 });
 static Task Scope(AssetLibraryViewModel vm)=>(Task)typeof(AssetLibraryViewModel).GetField("_p3ScopeRefreshTask",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(vm)!;
}
