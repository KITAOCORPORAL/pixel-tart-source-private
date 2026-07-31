using System.Collections.ObjectModel;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Utilities;

namespace RAWSelectionAssistant.ViewModels;

public sealed class OrganizePhotosViewModel : ObservableObject
{
    public ObservableCollection<PhotoGroup> Groups { get; } = [];
    public string PageTitle => ToolRegistry.Get(ToolId.PhotoOrganize).DisplayName;
    public string CapabilityName => "分组整理";
    public string SourcePath => string.Empty;
}

public sealed class CollageViewModel : ObservableObject
{
    private string _selectedTemplateCategory = "2 张";
    private string _backgroundColor = "#18191C";

    public string PageTitle => ToolRegistry.Get(ToolId.Collage).DisplayName;
    public IReadOnlyList<string> TemplateCategories { get; } = ["2 张", "3 张", "4 张", "5 张", "6 张"];
    public IReadOnlyList<string> TemplateOptions { get; } = ["左右布局", "上下布局", "竖向排列", "网格布局", "主图与副图"];

    public string SelectedTemplateCategory
    {
        get => _selectedTemplateCategory;
        set => SetProperty(ref _selectedTemplateCategory, value);
    }

    public string BackgroundColor
    {
        get => _backgroundColor;
        set => SetProperty(ref _backgroundColor, value);
    }
}
