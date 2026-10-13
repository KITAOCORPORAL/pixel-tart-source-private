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
        var menu = new ContextMenu { PlacementTarget = button };
        foreach(var plan in _vm.PlanningProjects)
        {
            var item=new MenuItem{Header=plan.Name};item.Click+=async(_,_)=>{if(await FlushDocumentAsync())await _vm.OpenProjectSafelyAsync(plan.ProjectId);};menu.Items.Add(item);
        }
        button.ContextMenu=menu;menu.IsOpen=true;
    }
    private void RenderResources(string page)
    {
        if (_vm is null) return;
        Heading(DocumentContent, page); var type = ResourcePages[page];
        var tools = new WrapPanel();
        tools.Children.Add(Action("新增", () => EditPlanningResource((PlanningResource)Activator.CreateInstance(type)!)));
        tools.Children.Add(AsyncAction("撤销上次策划操作", async () => { if (await _vm.UndoPlanningAsync()) Render(); }));
        DocumentContent.Children.Add(tools);
        if (page == "设备") DocumentContent.Children.Add(Text("工具箱尚无设备目录数据源；不提供假导入按钮。现可手工登记，来源目录+来源ID导入接口已定义。",12));
        if (page == "场地") DocumentContent.Children.Add(Text("地图、日落、潮汐未连接自动服务；仅保存地点、日期、时区、手动值与来源。",12));
        if (page == "预算")
        {
            DocumentContent.Children.Add(AsyncAction("读取项目实际支出（只读）", async () => { await _vm.RefreshPlanningExpensesAsync(); Render(); }));
            DocumentContent.Children.Add(Text(_vm.ResourceStatus,12));
            foreach(var expense in _vm.PlanningExpenses) DocumentContent.Children.Add(Text($"{expense.OccurredOn} · {expense.Note} · {expense.AmountMinor / (decimal)Math.Pow(10,expense.CurrencyScale)} {expense.CurrencyCode} · {expense.PaymentStatus}",12));
        }
        if (page == "灯位图")
        {
            var canvas = new Canvas { Height=320, Background=Brush("SurfaceSecondaryBrush"), ClipToBounds=true };
            AutomationProperties.SetName(canvas,"灯位俯视图");
            canvas.SizeChanged += (_,_) => DrawPlanningLights(canvas);
            canvas.MouseLeftButtonDown += (_,e) => { if(e.Source != canvas)return; var pt=e.GetPosition(canvas); EditPlanningResource(new PlanningLight { X=Math.Clamp(pt.X/Math.Max(1,canvas.ActualWidth),0,1),Y=Math.Clamp(pt.Y/320,0,1) }); };
            DocumentContent.Children.Add(Text("点击空白放置灯具；点击灯具编辑。坐标与方向实际保存，非模拟拖动。",12));DocumentContent.Children.Add(canvas);
        }
        foreach(var item in _vm.ResourceCatalog.Items.Where(i=>i.GetType()==type))
        {
            var row=new WrapPanel { Margin=new Thickness(0,8,0,8) };row.Children.Add(Text(item.Name+(item.Archived?"（归档）":""),16));
            row.Children.Add(Action("编辑",()=>EditPlanningResource(item)));
            row.Children.Add(AsyncAction("复制",async()=>{if(await _vm.CopyResourceAsync(item.Id))Render();}));
            row.Children.Add(AsyncAction(item.Archived?"恢复":"归档",async()=>{if(await _vm.ArchiveResourceAsync(item.Id,!item.Archived))Render();}));
            DocumentContent.Children.Add(row);
        }
        if(page=="灯位图") { Heading(DocumentContent,"灯光参考与已有画布快照"); foreach(var reference in ReferencesFor("灯光图")) DocumentContent.Children.Add(ImageTile(reference,180,true)); foreach(var revision in _vm.DocumentCanvasRevisions)DocumentContent.Children.Add(Text($"画布版本 {revision.RevisionId} · {revision.CapturedAt}",12)); }
        if(page=="服化道与道具") foreach(var reference in ReferencesFor("服化道"))DocumentContent.Children.Add(ImageTile(reference,160,true));
    }
    private void DrawPlanningLights(Canvas canvas)
    {
        canvas.Children.Clear();if(_vm is null)return;
        foreach(var light in _vm.ResourceCatalog.Items.OfType<PlanningLight>().Where(l=>!l.Archived))
        {
            var button=Action($"◉ {light.Name} {light.PowerPercent}%",()=>EditPlanningResource(light));button.RenderTransform=new RotateTransform(light.DirectionDegrees);
            Canvas.SetLeft(button,light.X*Math.Max(1,canvas.ActualWidth-110));Canvas.SetTop(button,light.Y*280);canvas.Children.Add(button);
        }
    }
    private void EditPlanningResource(PlanningResource value)
    {
        if(_vm is null)return;
        ModalContent.Children.Clear();Heading(ModalContent,"编辑："+value.Name);
        var fields=new Dictionary<PropertyInfo,TextBox>(); var form=new StackPanel();
        foreach(var property in value.GetType().GetProperties().Where(p=>ResourceLabels.ContainsKey(p.Name)))
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
                if(copy is PlanningLight light)copy=light with{CanvasRevisionId=(snapshot.SelectedItem as PlanningCanvasRevision)?.RevisionId};
                if(await _vm.SaveResourceAsync(copy)){ModalOverlay.Visibility=Visibility.Collapsed;Render();}else status.Text=_vm.ResourceStatus+" "+_vm.SaveStatus;
            }catch(Exception e) when(e is FormatException or OverflowException or ArgumentException){status.Text="输入无效："+e.Message;}
        },"PrimaryButton"));
        ModalContent.Children.Add(Action("取消",()=>ModalOverlay.Visibility=Visibility.Collapsed));ModalOverlay.Visibility=Visibility.Visible;FocusOverlay(ModalContent);
    }
}
