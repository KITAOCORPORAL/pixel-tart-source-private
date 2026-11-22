using System.IO;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.ViewModels;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass]
public sealed class SelectionRejectionVisibilityTests
{
    [TestMethod]public Task RejectedSendUsesVisibleExistingDialogBeforeAnyRenderOrHandoff()=>RunSta(async()=>
    {
        var dialogs=new Dialogs();using var vm=new ReferenceColorWorkspaceViewModel(dialogs);vm.BindWorkflowProject(Guid.NewGuid());
        vm.Targets.Add(new ReferenceTargetItem(Path.Combine(Path.GetTempPath(),"not-read-on-rejected-send.jpg")){IsSelected=true});
        var handoffs=0;vm.OpenOnlineSelection=_=>{handoffs++;return Task.FromResult(true);};vm.ValidateOnlineSelection=_=>Task.FromResult<string?>("请填写项目名");
        await vm.PrepareOnlineSelectionCommand.ExecuteAsync(null);
        CollectionAssert.AreEqual(new[]{"请填写项目名"},dialogs.Messages);Assert.AreEqual(0,handoffs);Assert.AreEqual("请填写项目名",vm.StatusText);
    });
    private sealed class Dialogs:IDialogService
    {
        public List<string> Messages {get;}=[];public void ShowInfo(string m)=>Messages.Add(m);public void ShowError(string m)=>throw new IOException(m);
        public string? ChooseFolder(string t,string? d=null)=>null;public IReadOnlyList<string> ChooseFiles(string t,string f,bool m=true)=>[];
        public string? ChooseSaveFile(string t,string f,string e,string? n=null)=>null;public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> ids)=>null;
        public bool Confirm(string m,string t)=>false;public HelpAction ShowHelp()=>HelpAction.None;public void ShowFeedback(){}public RawFileEntry? ChooseRawCandidate(IReadOnlyList<RawFileEntry> c)=>null;
        public bool ShowMediaDetails(MediaSelectionItem i,bool a)=>false;public void RevealFile(string p){}
    }
}
