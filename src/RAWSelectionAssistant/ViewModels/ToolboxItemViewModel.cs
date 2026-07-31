using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Utilities;

namespace RAWSelectionAssistant.ViewModels;

public sealed class ToolboxItemViewModel : ObservableObject
{
    private bool _isPinned;

    public ToolboxItemViewModel(ToolDefinition definition)
    {
        Definition = definition;
    }

    public ToolDefinition Definition { get; }
    public string Id => Definition.SettingsId;
    public string DisplayName => Definition.DisplayName;
    public string Description => Definition.Description;
    public string IconResourceKey => Definition.IconResourceKey;
    public string TargetPageKey => Definition.TargetPageKey;
    public bool CanPin => Definition.CanPin;
    public bool IsAvailable => Definition.IsAvailable;
    public string MaturityLabel => Definition.Maturity == ToolMaturity.Preview ? "预览" : "可用";

    public bool IsPinned
    {
        get => _isPinned;
        private set
        {
            if (!SetProperty(ref _isPinned, value)) return;
            OnPropertyChanged(nameof(PinActionLabel));
            OnPropertyChanged(nameof(PinAutomationName));
        }
    }

    public string PinActionLabel => IsPinned ? "取消固定" : "固定";
    public string PinAutomationName => IsPinned ? $"取消固定{DisplayName}" : $"固定{DisplayName}";

    public void SetPinned(bool value) => IsPinned = value;
}
