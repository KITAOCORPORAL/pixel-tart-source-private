using System.ComponentModel;
using System.Windows;using System.Windows.Input;using RAWSelectionAssistant.ViewModels;
namespace RAWSelectionAssistant.Views;
public partial class InstantV3Editor:System.Windows.Controls.UserControl
{
 public static readonly DependencyProperty SectionProperty=DependencyProperty.Register(nameof(Section),typeof(string),typeof(InstantV3Editor),new PropertyMetadata("Full",(d,_)=>((InstantV3Editor)d).RefreshSections()));
 public string Section{get=>(string)GetValue(SectionProperty);set=>SetValue(SectionProperty,value);}
 public InstantV3Editor(){InitializeComponent();RefreshSections();DataContextChanged+=(_,e)=>{if(e.OldValue is ReferenceColorWorkspaceViewModel old)PropertyChangedEventManager.RemoveHandler(old,ResourcesChanged,string.Empty);if(e.NewValue is ReferenceColorWorkspaceViewModel next)PropertyChangedEventManager.AddHandler(next,ResourcesChanged,string.Empty);ApplyFilter();};}
 private void ResourcesChanged(object? sender,PropertyChangedEventArgs e){if(e.PropertyName is nameof(ReferenceColorWorkspaceViewModel.PhotographerPapers) or nameof(ReferenceColorWorkspaceViewModel.FilteredFilmPapers))ApplyFilter();}
 private bool applyingResourceFilter;
 public void SetResourceFilter(string search,string filter){if(DataContext is ReferenceColorWorkspaceViewModel vm){vm.SetFilmResourceFilter(search,filter);ApplyFilter();}}
 private void ApplyFilter(){if(DataContext is not ReferenceColorWorkspaceViewModel vm)return;applyingResourceFilter=true;try{var selected=vm.SelectedPhotographerPaperId;var view=System.Windows.Data.CollectionViewSource.GetDefaultView(vm.FilteredFilmPapers);view.GroupDescriptions.Clear();view.GroupDescriptions.Add(new System.Windows.Data.PropertyGroupDescription("Group"));PaperChoices.ItemsSource=view;PaperChoices.SelectedValue=selected;}finally{applyingResourceFilter=false;}}
 private void PaperSelectionChanged(object sender,System.Windows.Controls.SelectionChangedEventArgs e){if(!applyingResourceFilter&&DataContext is ReferenceColorWorkspaceViewModel vm&&e.AddedItems.OfType<PhotographerPaper>().FirstOrDefault() is{} paper){vm.RecordR53Resource("paper:"+paper.ProfileId);}}
 private void FavoriteLoaded(object sender,RoutedEventArgs e){if(DataContext is ReferenceColorWorkspaceViewModel vm&&sender is System.Windows.Controls.Button{Tag:string id} button)button.Content=vm.IsFilmResourceFavorite("paper:"+id)?"★":"☆";}
 private void ToggleFavorite(object sender,RoutedEventArgs e){if(DataContext is ReferenceColorWorkspaceViewModel vm&&sender is FrameworkElement{Tag:string id}){vm.FavoriteFilmResource("paper:"+id);if(sender is System.Windows.Controls.Button button)button.Content=vm.IsFilmResourceFavorite("paper:"+id)?"★":"☆";ApplyFilter();}}
 private void RefreshSections(){if(PickerArea is null||AdvancedArea is null||FullCommonArea is null)return;FullCommonArea.Visibility=Section=="Full"?Visibility.Visible:Visibility.Collapsed;PickerArea.Visibility=Section=="Advanced"?Visibility.Collapsed:Visibility.Visible;AdvancedArea.Visibility=Section=="Picker"?Visibility.Collapsed:Visibility.Visible;}
 private void PaperClicked(object sender,MouseButtonEventArgs e){if(DataContext is ReferenceColorWorkspaceViewModel vm){if(PaperChoices.SelectedValue is string id)vm.SelectFilmPaper(id);ApplyFilter();}}
}
