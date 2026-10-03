using System.Windows;
using System.Windows.Controls;

namespace PixelTart.Modules.AssetLibrary;

public partial class AssetSmartFolderEditorView : UserControl
{
    public AssetSmartFolderEditorView() => InitializeComponent();
    private async void DeleteDefinition_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not AssetLibraryViewModel model || !model.P3SmartFolderIsEditing) return;
        if (MessageBox.Show(Window.GetWindow(this), $"删除智能文件夹“{model.P3SmartFolderName}”？\n仅删除查询定义和未保存的条件，不删除照片或源文件。此操作不能撤销。",
            "删除智能文件夹", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;
        await model.DeleteP3SmartFolderDefinitionAsync();
    }
}
