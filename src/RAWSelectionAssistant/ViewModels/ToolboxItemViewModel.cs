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
    public FeatureAvailability Availability => Definition.Availability;
    public string MaturityLabel => Availability switch
    {
        FeatureAvailability.Preview => "预览功能",
        FeatureAvailability.ComingSoon => "即将推出",
        FeatureAvailability.Hidden => "暂不显示",
        _ => "正式可用"
    };

    public bool IsPinned
    {
        get => _isPinned;
        private set
        {
            if (!SetProperty(ref _isPinned, value)) return;
            OnPropertyChanged(nameof(PinGlyph));
            OnPropertyChanged(nameof(PinToolTip));
            OnPropertyChanged(nameof(PinAutomationName));
            OnPropertyChanged(nameof(PinStateLabel));
            OnPropertyChanged(nameof(PinActionLabel));
        }
    }

    public string PinGlyph => IsPinned ? "●" : "○";
    public string PinStateLabel => IsPinned ? "已固定" : "未固定";
    public string PinActionLabel => IsPinned ? "●  已固定" : "○";
    public string PinToolTip => IsPinned ? $"从工作台快捷区取消固定{DisplayName}" : $"固定{DisplayName}到工作台快捷区";
    public string PinAutomationName => $"{DisplayName}，{PinStateLabel}，点击后{(IsPinned ? "取消固定" : "固定")}";

    public void SetPinned(bool value) => IsPinned = value;
}
