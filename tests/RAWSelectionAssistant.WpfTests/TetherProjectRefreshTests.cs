using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.ViewModels;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass]
public sealed class TetherProjectRefreshTests
{
 static TetherCaptureViewModel Vm(IProjectRepository projects)=>new(null!,null!,null!,null!,null!,projectRepository:projects);
 static PhotoProjectRecord Project(string name)=>new(){Id=Guid.NewGuid(),Name=name};
 [TestMethod]public Task ActivationSeesNewlySavedProjectAndPreservesSelection()=>RunSta(async()=>
 {
  var repo=new Projects();var vm=Vm(repo);try
  {
   vm.OnActivated();await vm.ProjectRefreshWork;Assert.AreEqual(1,vm.ProjectOptions.Count);vm.OnDeactivated();var first=Project("named-test");repo.Items.Add(first);vm.OnActivated();await vm.ProjectRefreshWork;Assert.AreEqual(2,vm.ProjectOptions.Count);vm.SelectedProject=vm.ProjectOptions.Single(x=>x.Id==first.Id);vm.OnDeactivated();repo.Items.Add(Project("another"));vm.OnActivated();await vm.ProjectRefreshWork;Assert.AreEqual(first.Id,vm.SelectedProject!.Id);Assert.AreEqual(3,vm.ProjectOptions.Count);
  }finally{await vm.DisposeAsync();}
 });
 [TestMethod]public Task LateRefreshCannotPublishIntoInactivePage()=>RunSta(async()=>
 {
  var repo=new Projects();var vm=Vm(repo);try
  {
   vm.OnActivated();await vm.ProjectRefreshWork;vm.OnDeactivated();repo.Pending=new(TaskCreationOptions.RunContinuationsAsynchronously);vm.OnActivated();var work=vm.ProjectRefreshWork;vm.OnDeactivated();repo.Pending.SetResult([Project("late")]);await work;Assert.AreEqual(1,vm.ProjectOptions.Count);
  }finally{await vm.DisposeAsync();}
 });
 [TestMethod]public Task RunningSessionSelectionSurvivesArchivedEntryRefresh()=>RunSta(async()=>
 {
  var repo=new Projects();var item=Project("active");repo.Items.Add(item);var vm=Vm(repo);try
  {
   vm.OnActivated();await vm.ProjectRefreshWork;vm.SelectedProject=vm.ProjectOptions.Single(x=>x.Id==item.Id);vm.ApplyReviewState("connected",[]);Assert.IsTrue(vm.IsRunning);vm.OnDeactivated();repo.Items.Clear();vm.OnActivated();await vm.ProjectRefreshWork;Assert.AreEqual(item.Id,vm.SelectedProject!.Id);Assert.IsTrue(vm.ProjectOptions.Any(x=>x.Id==item.Id));
  }finally{await vm.DisposeAsync();}
 });
 sealed class Projects:IProjectRepository
 {
  public List<PhotoProjectRecord> Items=[];public TaskCompletionSource<IReadOnlyList<PhotoProjectRecord>>? Pending;
  public Task UpsertAsync(PhotoProjectRecord p,CancellationToken t=default){Items.Add(p);return Task.CompletedTask;}
  public Task<IReadOnlyList<PhotoProjectRecord>> ListAsync(CancellationToken t=default)=>Pending?.Task??Task.FromResult<IReadOnlyList<PhotoProjectRecord>>(Items.ToArray());
 }
}
