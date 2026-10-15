using System.IO;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using PixelTart.Modules.AssetLibrary;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Tasks;
class Program{
 [STAThread]static void Main(string[] args){var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};app.Startup+=async(_,_)=>{try{await Run(args);}catch(Exception e){Console.Error.WriteLine(e);Environment.ExitCode=1;}finally{app.Shutdown();}};app.Run();}
 static async Task Run(string[] args){var root=Path.GetFullPath(args[1]);Directory.CreateDirectory(root);var db=Path.Combine(root,"ui-10128.db");File.Copy(Path.GetFullPath(args[0]),db,false);await using var vm=new AssetLibraryViewModel(db,new TaskOperationBridge(),productDatabasePath:Path.Combine(root,"product.db"),inspirationTrayDatabasePath:Path.Combine(root,"tray.db"),onlineSelectionWorkspaceFile:Path.Combine(root,"online.json"));await vm.InitializeAsync();var folder=vm.Folders.Single(f=>f.Name=="scope");vm.SelectedFolder=folder;await vm.RefreshCommand.ExecutionTask;await Task.Delay(100);vm.EditQuickFilter(AssetQueryField.Rating);var rule=vm.P3QueryRoot.DescendantsAndSelf().First(n=>n.IsRule&&n.Field==AssetQueryField.Rating);rule.Operator=AssetQueryOperator.GreaterThanOrEqual;rule.ValueText="3";await vm.RefreshCommand.ExecutionTask;vm.RefreshCommand.Execute(null);await vm.RefreshCommand.ExecutionTask;
 var initialCounts=vm.SystemCollections.ToDictionary(x=>x.Collection,x=>x.Count);var timings=new List<AssetLibraryOperationTiming.Sample>();using var operationCapture=AssetLibraryOperationTiming.Capture(timings.Add);var samples=new List<object>();var queries=new List<AssetLibraryQueryDiagnostics.Query>();using var capture=AssetLibraryQueryDiagnostics.Capture(queries.Add);
 for(int i=0;i<4;i++){vm.P3QueryScope=AssetQueryScope.Current;await Scope(vm);await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);var gaps=new List<double>();var last=Stopwatch.GetTimestamp();var timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(5)};timer.Tick+=(_,_)=>{gaps.Add(Stopwatch.GetElapsedTime(last).TotalMilliseconds);last=Stopwatch.GetTimestamp();};timer.Start();var watch=Stopwatch.StartNew();vm.P3QueryScope=AssetQueryScope.AllAssets;await Scope(vm);var refreshed=watch.Elapsed.TotalMilliseconds;await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);watch.Stop();timer.Stop();if(vm.P2QueryTotalCount!=4999)throw new InvalidDataException("Wrong all-library count");foreach(var x in vm.SystemCollections)if(initialCounts[x.Collection]!=x.Count)throw new InvalidDataException("Scope changed global count");samples.Add(new{cycle=i,state=i==0?"priming":"warm",refreshMs=refreshed,idleMs=watch.Elapsed.TotalMilliseconds,dispatcherGapsMs=gaps});}
 await File.WriteAllTextAsync(Path.Combine(root,"ui-measurements.json"),JsonSerializer.Serialize(new{database=db,samples,queries,timings,window="NOT_SHOWN",physicalInput="NOT_RUN",authority="Actual viewmodel P3QueryScope refresh awaited plus dispatcher idle; not desktop latency"},new JsonSerializerOptions{WriteIndented=true}));}
 static Task Scope(AssetLibraryViewModel vm)=>(Task)typeof(AssetLibraryViewModel).GetField("_p3ScopeRefreshTask",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(vm)!;
}
