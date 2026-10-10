using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class PlanningDocumentTextEditingTests
{
    [TestMethod]
    public void BoldAndItalicWrapSelectionAndBoldCanToggleOff()
    {
        var bold = PlanningDocumentTextEditing.Apply("alpha beta", 6, 4, PlanningTextCommand.Bold);
        Assert.AreEqual("alpha **beta**", bold.Text); Assert.AreEqual(8, bold.SelectionStart); Assert.AreEqual(4, bold.SelectionLength);
        var unwrapped = PlanningDocumentTextEditing.Apply(bold.Text, 6, 8, PlanningTextCommand.Bold);
        Assert.AreEqual("alpha beta", unwrapped.Text); Assert.AreEqual(6, unwrapped.SelectionStart); Assert.AreEqual(4, unwrapped.SelectionLength);
        Assert.AreEqual("*alpha*", PlanningDocumentTextEditing.Apply("alpha", 0, 5, PlanningTextCommand.Italic).Text);
    }

    [TestMethod]
    public void HeadingsListsAndQuotesApplyToCompleteSelectedLines()
    {
        var heading = PlanningDocumentTextEditing.Apply("intro\nold\nnext\noutro", 7, 8, PlanningTextCommand.Heading2);
        Assert.AreEqual("intro\n## old\n## next\noutro", heading.Text);
        var list = PlanningDocumentTextEditing.Apply("one\ntwo", 0, 7, PlanningTextCommand.NumberedList);
        Assert.AreEqual("1. one\n2. two", list.Text);
        var relisted = PlanningDocumentTextEditing.Apply(list.Text, 0, list.Text.Length, PlanningTextCommand.BulletedList);
        Assert.AreEqual("- one\n- two", relisted.Text);
        Assert.AreEqual("> one\n> two", PlanningDocumentTextEditing.Apply("one\ntwo", 0, 7, PlanningTextCommand.Quote).Text);
    }

    [TestMethod]
    public void EmptySelectionProducesStableCaretAndDividerDoesNotDeleteAdjacentText()
    {
        var bold = PlanningDocumentTextEditing.Apply("text", 2, 0, PlanningTextCommand.Bold);
        Assert.AreEqual("te****xt", bold.Text); Assert.AreEqual(4, bold.SelectionStart); Assert.AreEqual(0, bold.SelectionLength);
        var divider = PlanningDocumentTextEditing.Apply("beforeafter", 6, 0, PlanningTextCommand.Divider);
        Assert.AreEqual("before\r\n---\r\nafter", divider.Text);
    }

    [TestMethod]
    public void InvalidSelectionIsClampedInsteadOfThrowing()
    {
        var result = PlanningDocumentTextEditing.Apply("abc", -10, 999, PlanningTextCommand.Bold);
        Assert.AreEqual("**abc**", result.Text);
    }
}
