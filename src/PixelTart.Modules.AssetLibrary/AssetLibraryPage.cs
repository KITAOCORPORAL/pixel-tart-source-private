using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace PixelTart.Modules.AssetLibrary;

public sealed class AssetLibraryPage : UserControl
{
    public AssetLibraryPage()
    {
        AutomationProperties.SetAutomationId(this, "AssetLibraryPage");
        AutomationProperties.SetName(this, "素材库");
        Content = new Border
        {
            Padding = new Thickness(28),
            Background = Brushes.Transparent,
            Child = new TextBlock { Text = "素材库", FontSize = 28, FontWeight = FontWeights.SemiBold }
        };
    }
}
