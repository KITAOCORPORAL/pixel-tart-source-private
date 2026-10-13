using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Views;
public partial class PlanningCenterView
{
    private void ConfigurePlanningTable(DataGrid table)
    {
        table.RowHeight=38;table.ColumnHeaderHeight=34;table.HeadersVisibility=DataGridHeadersVisibility.Column;
        table.GridLinesVisibility=DataGridGridLinesVisibility.Horizontal;table.HorizontalGridLinesBrush=Brush("DividerBrush");
        table.Background=Brush("SurfacePrimaryBrush");table.BorderBrush=Brush("DividerBrush");table.BorderThickness=new Thickness(1);
        table.AlternatingRowBackground=Brush("SurfaceSecondaryBrush");table.FontSize=12;table.MinColumnWidth=70;
        ScrollViewer.SetHorizontalScrollBarVisibility(table,ScrollBarVisibility.Visible);
    }
    private void RenderR5ResourceTable(Type type)
    {
        if(_vm is null)return;
        var table=new DataGrid{AutoGenerateColumns=false,IsReadOnly=true,CanUserAddRows=false,CanUserDeleteRows=false,SelectionMode=DataGridSelectionMode.Single,MinHeight=190,MaxHeight=450,ItemsSource=_vm.ResourceCatalog.Items.Where(i=>i.GetType()==type).ToArray()};ConfigurePlanningTable(table);
        System.Windows.Automation.AutomationProperties.SetName(table,"摄影资源清单");
        table.Columns.Add(new DataGridTextColumn{Header="名称",Binding=new Binding("Name"),Width=new DataGridLength(1,DataGridLengthUnitType.Star),MinWidth=150});
        string[] columns=type==typeof(PlanningProp)?["Category","Quantity","Preparation","Source"]:type==typeof(PlanningGearItem)?["Model","Quantity","SourceCatalog"]:type==typeof(PlanningCrewMember)?["Role","Contact","ArrivalLocal"]:type==typeof(PlanningVenue)?["Address","Contact","AvailableHours","Permit"]:type==typeof(PlanningScheduleSlot)?["StartLocal","EndLocal","Location","Owner"]:type==typeof(PlanningBudgetLine)?["Planned","Currency"]:["X","Y","DirectionDegrees","PowerPercent","Modifier"];
        foreach(var key in columns)table.Columns.Add(new DataGridTextColumn{Header=ResourceLabels.GetValueOrDefault(key,key=="SourceCatalog"?"来源目录":key),Binding=new Binding(key),Width=110});
        table.Columns.Add(new DataGridCheckBoxColumn{Header="归档",Binding=new Binding("Archived"),Width=70});
        void Select(){if(table.SelectedItem is PlanningResource item)ShowResourceInspector(item);}
        table.SelectionChanged+=(_,_)=>Select();table.MouseDoubleClick+=(_,_)=>{if(table.SelectedItem is PlanningResource item)EditPlanningResource(item);};
        table.KeyDown+=(_,e)=>{if(e.Key==Key.Enter&&table.SelectedItem is PlanningResource item){EditPlanningResource(item);e.Handled=true;}};
        DocumentContent.Children.Add(table);DocumentContent.Children.Add(Text("选择一行查看检查器；双击或 Enter 编辑。",11));
    }
    private void ShowResourceInspector(PlanningResource item)
    {
        RenderR5Inspector();var panel=new StackPanel();panel.Children.Add(Text(item.Name,16,true));
        foreach(var property in item.GetType().GetProperties().Where(p=>ResourceLabels.ContainsKey(p.Name)&&p.Name!="ShotIds"))panel.Children.Add(Text(ResourceLabels[property.Name]+"："+property.GetValue(item),12));
        panel.Children.Add(Text($"关联镜头 {item.ShotIds.Length}",12));panel.Children.Add(Action("编辑当前对象",()=>EditPlanningResource(item)));
        panel.Children.Add(AsyncAction("复制",async()=>{if(await _vm!.CopyResourceAsync(item.Id))Render();}));
        panel.Children.Add(AsyncAction(item.Archived?"恢复":"归档",async()=>{if(await _vm!.ArchiveResourceAsync(item.Id,!item.Archived))Render();}));
        AddInspectorGroup("当前对象",panel);
        if(InspectorIsDrawer)OpenPlanningDock(PlanningInspectorPane,true);
    }
}
