namespace RAWSelectionAssistant.ViewModels;

public sealed class PageChangedEventArgs(string previousPage, string currentPage) : EventArgs
{
    public string PreviousPage { get; } = previousPage;
    public string CurrentPage { get; } = currentPage;
}
