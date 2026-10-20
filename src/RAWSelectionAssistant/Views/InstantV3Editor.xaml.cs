using System.Windows;using System.Windows.Input;using RAWSelectionAssistant.ViewModels;
namespace RAWSelectionAssistant.Views;
public partial class InstantV3Editor:System.Windows.Controls.UserControl
{
 public static readonly DependencyProperty SectionProperty=DependencyProperty.Register(nameof(Section),typeof(string),typeof(InstantV3Editor),new PropertyMetadata("Full",(d,_)=>((InstantV3Editor)d).RefreshSections()));
 public string Section{get=>(string)GetValue(SectionProperty);set=>SetValue(SectionProperty,value);}
 public InstantV3Editor(){InitializeComponent();RefreshSections();}
 private void RefreshSections(){if(PickerArea is null||AdvancedArea is null||FullCommonArea is null)return;FullCommonArea.Visibility=Section=="Full"?Visibility.Visible:Visibility.Collapsed;PickerArea.Visibility=Section=="Advanced"?Visibility.Collapsed:Visibility.Visible;AdvancedArea.Visibility=Section=="Picker"?Visibility.Collapsed:Visibility.Visible;}
 private void PaperClicked(object sender,MouseButtonEventArgs e){if(DataContext is ReferenceColorWorkspaceViewModel vm)vm.RequestLightPrintPreview();}
}
