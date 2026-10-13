using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Views;
public partial class PlanningCenterView
{
    private static readonly Dictionary<string, Type> ResourcePages = new()
    {
        ["服化道与道具"] = typeof(PlanningProp), ["设备"] = typeof(PlanningGearItem), ["人员"] = typeof(PlanningCrewMember),
        ["场地"] = typeof(PlanningVenue), ["当天时间表"] = typeof(PlanningScheduleSlot), ["预算"] = typeof(PlanningBudgetLine), ["灯位图"] = typeof(PlanningLight)
    };
    private static readonly Dictionary<string,string> ResourceLabels = new()
    {
        ["Name"]="名称", ["Notes"]="备注", ["ShotIds"]="关联镜头ID（逗号分隔）", ["Category"]="分类", ["Preparation"]="准备状态", ["Source"]="来源", ["Quantity"]="数量",
        ["Model"]="型号", ["Role"]="角色", ["Contact"]="联系方式", ["ArrivalLocal"]="到场时间", ["Address"]="地址", ["AvailableHours"]="可用时段", ["Permit"]="报备许可",
        ["Date"]="日期", ["TimeZoneId"]="时区ID", ["SunsetLocal"]="日落（手动）", ["TideLocal"]="潮汐（手动）", ["ObservationSource"]="数据来源", ["DataStatus"]="离线状态",
        ["StartLocal"]="开始时间", ["EndLocal"]="结束时间", ["Location"]="地点", ["Owner"]="负责人", ["Planned"]="预算金额", ["Currency"]="币种",
        ["X"]="横向位置 0–1", ["Y"]="纵向位置 0–1", ["DirectionDegrees"]="方向角度", ["PowerPercent"]="功率百分比", ["Modifier"]="修饰器", ["ReferencePath"]="原图引用路径"
    };
    private void PlanningPicker_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is null || sender is not Button button) return;
        OpenPlanningDock(DocumentListPane,false);
    }
    private void RenderResources(string page)
    {
        if (_vm is null) return;
        Heading(DocumentContent, page); var type = ResourcePages[page];
        var tools = new WrapPanel();
        tools.Children.Add(Action("新增", () => EditPlanningResource((PlanningResource)Activator.CreateInstance(type)!)));
        tools.Children.Add(AsyncAction("撤销上次策划操作", async () => { if (await _vm.UndoPlanningAsync()) Render(); }));
        DocumentContent.Children.Add(tools);
        if (page == "设备") DocumentContent.Children.Add(Text("手工登记设备。工具箱设备目录尚未连接，原设备资料不会被修改。",12));
        if (page == "场地") DocumentContent.Children.Add(Text("地点与日落 / 潮汐使用手动资料，请填写日期、时区和来源。",12));
        if (page == "预算")
        {
            DocumentContent.Children.Add(AsyncAction("读取项目实际支出（只读）", async () => { await _vm.RefreshPlanningExpensesAsync(); Render(); }));
            DocumentContent.Children.Add(Text(_vm.ResourceStatus,12));
            foreach(var expense in _vm.PlanningExpenses.Where(e=>e.ProjectId==_vm.ProjectId)) DocumentContent.Children.Add(Text($"{expense.OccurredOn} · {expense.Note} · {expense.AmountMinor / (decimal)Math.Pow(10,expense.CurrencyScale)} {expense.CurrencyCode} · {expense.PaymentStatus}",12));
        }
        if (page == "灯位图")
        {
            var canvas = new Canvas { Height=360, Background=Brush("SurfaceSecondaryBrush"), ClipToBounds=true };
            AutomationProperties.SetName(canvas,"灯位俯视图");
            canvas.SizeChanged += (_,_) => DrawPlanningLights(canvas);
            canvas.MouseLeftButtonDown += (_,e) => { if(e.Source != canvas)return; var pt=e.GetPosition(canvas); EditPlanningResource(new PlanningLight { X=Math.Clamp(pt.X/Math.Max(1,canvas.ActualWidth),0,1),Y=Math.Clamp(pt.Y/360,0,1) }); };
            DocumentContent.Children.Add(Text("点击空白放置灯具；点击灯具编辑位置、方向、功率与修饰器。",12));DocumentContent.Children.Add(canvas);
        }
        RenderR5ResourceTable(type);
        if(page=="灯位图") { Heading(DocumentContent,"灯光参考与已有画布快照"); foreach(var reference in ReferencesFor("灯光图")) DocumentContent.Children.Add(ImageTile(reference,180,true)); foreach(var revision in _vm.DocumentCanvasRevisions)DocumentContent.Children.Add(Text($"画布版本 {revision.RevisionId} · {revision.CapturedAt}",12)); }
        if(page=="服化道与道具") foreach(var reference in ReferencesFor("服化道"))DocumentContent.Children.Add(ImageTile(reference,160,true));
    }
    private void DrawPlanningLights(Canvas canvas)
    {
        canvas.Children.Clear();if(_vm is null)return;
        for(var x=0d;x<canvas.ActualWidth;x+=40)canvas.Children.Add(new Line{X1=x,X2=x,Y1=0,Y2=360,Stroke=Brush("DividerBrush"),StrokeThickness=.5,IsHitTestVisible=false});
        for(var y=0d;y<360;y+=40)canvas.Children.Add(new Line{X1=0,X2=canvas.ActualWidth,Y1=y,Y2=y,Stroke=Brush("DividerBrush"),StrokeThickness=.5,IsHitTestVisible=false});
        var subject=Text("主体",14,true);Canvas.SetLeft(subject,canvas.ActualWidth/2);Canvas.SetTop(subject,160);canvas.Children.Add(subject);
        foreach(var light in _vm.ResourceCatalog.Items.OfType<PlanningLight>().Where(l=>!l.Archived))
        {
            var button=Action($"◉ {light.Name} {light.PowerPercent}%",()=>EditPlanningResource(light));button.ToolTip=$"方向 {light.DirectionDegrees}° · {light.Modifier}";
            var rad=light.DirectionDegrees*Math.PI/180;var x=light.X*Math.Max(1,canvas.ActualWidth-110);var y=light.Y*280;canvas.Children.Add(new Line{X1=x+20,Y1=y+16,X2=x+20+Math.Cos(rad)*48,Y2=y+16+Math.Sin(rad)*48,Stroke=Brush("AccentBrush"),StrokeThickness=2});
            Canvas.SetLeft(button,light.X*Math.Max(1,canvas.ActualWidth-110));Canvas.SetTop(button,light.Y*280);canvas.Children.Add(button);
        }
    }
    private void EditPlanningResource(PlanningResource value)
    {
        if(_vm is null)return;
        ModalContent.Children.Clear();Heading(ModalContent,"编辑："+value.Name);
        var fields=new Dictionary<PropertyInfo,TextBox>(); var form=new StackPanel();
        foreach(var property in value.GetType().GetProperties().Where(p=>ResourceLabels.ContainsKey(p.Name) && p.Name != "ShotIds"))
        {
            form.Children.Add(Text(ResourceLabels[property.Name],12));var raw=property.GetValue(value);
            var box=new TextBox { Text=raw is Guid[] ids?string.Join(",",ids):Convert.ToString(raw,CultureInfo.InvariantCulture)??"",Margin=new Thickness(0,3,0,8),IsReadOnly=property.Name=="DataStatus" };
            AutomationProperties.SetName(box,ResourceLabels[property.Name]);form.Children.Add(box);fields[property]=box;
        }
        var related=new ListBox { SelectionMode=SelectionMode.Multiple, MaxHeight=140,ItemsSource=_vm.Shots,DisplayMemberPath="Name" };
        foreach(var shot in _vm.Shots.Where(s=>value.ShotIds.Contains(s.ShotId)))related.SelectedItems.Add(shot);
        form.Children.Add(Text("关联镜头（多选）",12));form.Children.Add(related);
        var snapshot=new ComboBox { ItemsSource=_vm.DocumentCanvasRevisions,DisplayMemberPath="RevisionId",SelectedItem=_vm.DocumentCanvasRevisions.FirstOrDefault(r=>value is PlanningLight l&&l.CanvasRevisionId==r.RevisionId) };
        if(value is PlanningLight){form.Children.Add(Text("已有画布快照引用",12));form.Children.Add(snapshot);}
        ModalContent.Children.Add(new ScrollViewer { Content=form,MaxHeight=440,VerticalScrollBarVisibility=ScrollBarVisibility.Auto });
        var status=Text("取消不会写入；原文件与原图只引用。",12);ModalContent.Children.Add(status);
        ModalContent.Children.Add(AsyncAction("保存",async()=>
        {
            try
            {
                var copy=(PlanningResource)JsonSerializer.Deserialize(JsonSerializer.Serialize(value,value.GetType()),value.GetType())!;
                foreach(var (property,box) in fields)
                {
                    object parsed=property.PropertyType==typeof(Guid[])?related.SelectedItems.Cast<ProjectShot>().Select(s=>s.ShotId).ToArray():property.PropertyType==typeof(DateOnly)?DateOnly.Parse(box.Text,CultureInfo.InvariantCulture):Convert.ChangeType(box.Text,property.PropertyType,CultureInfo.InvariantCulture);
                    property.SetValue(copy,parsed);
                }
                copy=copy with{ShotIds=related.SelectedItems.Cast<ProjectShot>().Select(s=>s.ShotId).ToArray()};
                if(copy is PlanningLight light)copy=light with{CanvasRevisionId=(snapshot.SelectedItem as PlanningCanvasRevision)?.RevisionId};
                if(await _vm.SaveResourceAsync(copy)){ModalOverlay.Visibility=Visibility.Collapsed;Render();}else status.Text=_vm.ResourceStatus+" "+_vm.SaveStatus;
            }catch(Exception e) when(e is FormatException or OverflowException or ArgumentException){status.Text="输入无效："+e.Message;}
        },"PrimaryButton"));
        ModalContent.Children.Add(Action("取消",()=>ModalOverlay.Visibility=Visibility.Collapsed));ModalOverlay.Visibility=Visibility.Visible;FocusOverlay(ModalContent);
    }
}
