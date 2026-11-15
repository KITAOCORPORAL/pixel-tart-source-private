using RAWSelectionAssistant.Core.Services.OnlineSelection;
namespace RAWSelectionAssistant.Tests;
[TestClass]
public sealed class SelectionWorkflowSourcePolicyTests
{
    private static string Root=>Path.Combine(Path.GetTempPath(),"Guangjing-SourcePolicy");
    [TestMethod] public void ExistingFreeProjectKeepsOneOriginalSourceAfterSelectionReturn()
    {
        var original=Path.Combine(Root,"originals");var prepared=Path.Combine(Root,"Prepared","project");var rendered=Path.Combine(prepared,"attempt");
        var result=SelectionWorkflowSourcePolicy.Resolve([original,rendered],[rendered],prepared);
        CollectionAssert.AreEqual(new[]{original},result); // Re-scanning still satisfies the existing one-source free limit.
    }
    [TestMethod] public void StandaloneOnlineSelectionCanStillEstablishItsInitialSource()
    {
        var rendered=Path.Combine(Root,"rendered");var result=SelectionWorkflowSourcePolicy.Resolve([],[rendered],Path.Combine(Root,"Prepared","project"));
        CollectionAssert.AreEqual(new[]{rendered},result);
    }
    [TestMethod] public void RepeatedReturnDoesNotAddManagedSourceAndPreservesOtherUserSources()
    {
        var a=Path.Combine(Root,"source-a");var b=Path.Combine(Root,"source-b");var managed=Path.Combine(Root,"Prepared","project");
        var once=SelectionWorkflowSourcePolicy.Resolve([a,b],[Path.Combine(managed,"attempt")],managed);
        var twice=SelectionWorkflowSourcePolicy.Resolve(once,[Path.Combine(managed,"another")],managed);
        CollectionAssert.AreEqual(new[]{a,b},twice);
    }
    [TestMethod] public void SimilarUserPathOutsideManagedProjectBoundaryIsNotRemoved()
    {
        var managed=Path.Combine(Root,"Prepared","project");var user=managed+"-user";
        CollectionAssert.AreEqual(new[]{user},SelectionWorkflowSourcePolicy.Resolve([user],[],managed));
    }
}
