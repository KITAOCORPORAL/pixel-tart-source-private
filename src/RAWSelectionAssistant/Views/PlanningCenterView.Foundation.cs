using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.Views;

public partial class PlanningCenterView
{
    private DataGrid? _shotTable;
    private void RenderSimpleOverview()
    {
        if (_vm is null) return;
        var metrics=new System.Windows.Controls.Primitives.UniformGrid { Columns=4,Margin=new Thickness(0,0,0,16) };
        foreach(var metric in new[]{("进度",_vm.ProgressText),("时长",_vm.EstimatedTimeText),("素材",_vm.CapturedAssetText),("资料",$"参考 {_vm.AllProjectReferences.Count} · 镜头 {_vm.Shots.Count}")})
        {var content=new StackPanel();content.Children.Add(Text(metric.Item1,11));content.Children.Add(Text(metric.Item2,15,true));metrics.Children.Add(R5Card(content));}DocumentContent.Children.Add(metrics);
        var body=new StackPanel();body.Children.Add(Text("创作策划 · "+_vm.DocumentTitle,18,true));
        var fields=new System.Windows.Controls.Primitives.UniformGrid { Columns=2 };
        foreach(var (label,property) in new[]{("拍摄目标",nameof(_vm.ShootGoal)),("视觉关键词",nameof(_vm.Keywords)),("客户要求",nameof(_vm.ClientRequirements)),("必拍内容",nameof(_vm.MustCapture)),("注意事项",nameof(_vm.PlanningNotes)),("交付用途",nameof(_vm.OutputPurpose))})
        {var cell=new StackPanel{Margin=new Thickness(0,10,12,0)};cell.Children.Add(Text(label,11));var editor=Editor(property,2);editor.MinHeight=54;editor.Margin=new Thickness(0,4,0,4);cell.Children.Add(editor);fields.Children.Add(cell);}body.Children.Add(fields);DocumentContent.Children.Add(R5Card(body));
        var summaries=new System.Windows.Controls.Primitives.UniformGrid{Columns=2};
        var shots=new StackPanel();shots.Children.Add(Text("镜头摘要",16,true));foreach(var shot in _vm.Shots.Take(4))shots.Children.Add(Text($"{shot.Order+1:00}  {shot.Name} · {shot.Scene}",12));shots.Children.Add(AsyncAction("查看镜头清单",async()=>{if(await FlushDocumentAsync())await _vm.SelectContentSafelyAsync("镜头清单");}));summaries.Children.Add(R5Card(shots));
        var resources=new StackPanel();resources.Children.Add(Text("服化道与灯位",16,true));resources.Children.Add(Text($"道具 {_vm.ResourceCatalog.Items.OfType<RAWSelectionAssistant.Core.Services.Projects.PlanningProp>().Count(i=>!i.Archived)} · 灯具 {_vm.ResourceCatalog.Items.OfType<RAWSelectionAssistant.Core.Services.Projects.PlanningLight>().Count(i=>!i.Archived)}",13));resources.Children.Add(AsyncAction("编辑灯位图",async()=>{if(await FlushDocumentAsync())await _vm.SelectContentSafelyAsync("灯位图");}));summaries.Children.Add(R5Card(resources));DocumentContent.Children.Add(summaries);
    }
    private Border R5Card(UIElement child)=>new(){Child=child,Background=Brush("SurfaceSecondaryBrush"),BorderBrush=Brush("DividerBrush"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8),Padding=new Thickness(16),Margin=new Thickness(0,0,10,14)};
    private void RenderEditableShotTable()
    {
        if (_vm is null) return;
        Heading(DocumentContent, "镜头清单");
        DocumentContent.Children.Add(Text("双击单元格编辑；Esc 取消当前行修改。自定义列移除后保留数据，可恢复。", 12));
        _shotTable = new DataGrid { AutoGenerateColumns = false, CanUserAddRows = false, CanUserDeleteRows = false,
            CanUserSortColumns = false, CanUserReorderColumns = false, FrozenColumnCount = 1, MinHeight = 180, MaxHeight = 560,
            ItemsSource = _vm.Shots.OrderBy(s => s.Order).Select(s => CreatePlanningShotRow(s)).ToArray() };
        ConfigurePlanningTable(_shotTable);
        _shotTable.RowHeight=double.NaN;_shotTable.MinRowHeight=76;
        var display=new Style(typeof(TextBlock));display.Setters.Add(new Setter(TextBlock.TextWrappingProperty,TextWrapping.Wrap));display.Setters.Add(new Setter(FrameworkElement.MaxHeightProperty,78d));display.Setters.Add(new Setter(FrameworkElement.ToolTipProperty,new Binding("Text"){RelativeSource=new RelativeSource(RelativeSourceMode.Self)}));
        var editing=new Style(typeof(TextBox));editing.Setters.Add(new Setter(TextBox.AcceptsReturnProperty,true));editing.Setters.Add(new Setter(TextBox.TextWrappingProperty,TextWrapping.Wrap));editing.Setters.Add(new Setter(FrameworkElement.MaxHeightProperty,160d));editing.Setters.Add(new Setter(ScrollViewer.VerticalScrollBarVisibilityProperty,ScrollBarVisibility.Auto));
        AutomationProperties.SetName(_shotTable, "可编辑镜头清单"); AutomationProperties.SetAutomationId(_shotTable, "PlanningEditableShotTable");
        _shotTable.SelectionChanged += (_, _) => { if (_shotTable.SelectedItem is PlanningShotEditRow row) { _inspectorCapture?.Invoke(); _vm.SelectedShot = _vm.Shots.FirstOrDefault(s => s.ShotId == row.ShotId); RenderR5Inspector(); } };
        void Column(string title, string key, bool readOnly = false, double width = 130)
        {
            _shotTable.Columns.Add(new DataGridTextColumn { Header = title, IsReadOnly = readOnly, Width = width, ElementStyle=display, EditingElementStyle=editing,
                Binding = new Binding("[" + key + "]") { Mode = readOnly ? BindingMode.OneWay : BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.LostFocus } });
        }
        Column("镜号", "Order", true, 64);
        _shotTable.Columns.Add(new DataGridComboBoxColumn { Header="状态",Width=90,ItemsSource=PlanningShotEditRow.StatusLabels,SelectedItemBinding=new Binding("[Status]"){Mode=BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged}});
        _shotTable.FrozenColumnCount=2;
        var imageFactory=new FrameworkElementFactory(typeof(Image));imageFactory.SetValue(Image.HeightProperty,30d);imageFactory.SetValue(Image.WidthProperty,48d);imageFactory.SetValue(Image.StretchProperty,System.Windows.Media.Stretch.Uniform);imageFactory.SetBinding(Image.SourceProperty,new Binding(nameof(PlanningShotEditRow.ReferencePreviewPath)));imageFactory.SetBinding(FrameworkElement.ToolTipProperty,new Binding(nameof(PlanningShotEditRow.ReferenceLabel)));
        _shotTable.Columns.Add(new DataGridTemplateColumn{Header="参考",Width=74,IsReadOnly=true,CellTemplate=new DataTemplate{VisualTree=imageFactory}});
        Column("名称", "Name", width:240); Column("场景", "Scene"); Column("景别", "ShotSize", width: 90);
        Column("机位 / 镜头", "CameraAndLens"); Column("人物动作", "Action", width:280); Column("道具", "Props"); Column("灯光", "Lighting");
        Column("预计分钟", "EstimatedMinutes", width: 85);
        DocumentContent.Children.Add(Text("可横向滚动；镜号与状态保持可见。参考缩略图来自本镜头已关联资料。",11));
        Column("备注", "Remarks");
        foreach (var column in _vm.ShotColumns) Column(column.Name, "custom:" + column.Id.ToString("N"));
        DocumentContent.Children.Add(_shotTable);
        var actions = new WrapPanel();
        void Command(string title, RAWSelectionAssistant.Utilities.AsyncRelayCommand command)
        { actions.Children.Add(AsyncAction(title, async () => { if (!await FlushDocumentAsync()) return; await command.ExecuteAsync(null); Render(); })); }
        Command("新建镜头", _vm.NewShotCommand); Command("上移", _vm.MoveShotUpCommand); Command("下移", _vm.MoveShotDownCommand);
        Command("复制镜头", _vm.DuplicateShotCommand); Command("归档镜头", _vm.ArchiveShotCommand);
        actions.Children.Add(AsyncAction("保存", async () => { if (await FlushDocumentAsync()) Render(); }));
        DocumentContent.Children.Add(actions);
        var columns = new WrapPanel(); var name = new TextBox { Width = 150, Margin = new Thickness(4), ToolTip = "新列名称" };
        AutomationProperties.SetName(name, "自定义列名称"); columns.Children.Add(name);
        columns.Children.Add(AsyncAction("添加列", async () => { if (await FlushDocumentAsync() && await _vm.AddShotColumnAsync(name.Text)) Render(); }));
        foreach (var column in _vm.ShotColumns)
            columns.Children.Add(AsyncAction("移除列：" + column.Name, async () => { if (await FlushDocumentAsync() && await _vm.SetShotColumnArchivedAsync(column.Id, true)) Render(); }));
        foreach (var column in _vm.ArchivedShotColumns)
            columns.Children.Add(AsyncAction("恢复列：" + column.Name, async () => { if (await FlushDocumentAsync() && await _vm.SetShotColumnArchivedAsync(column.Id, false)) Render(); }));
        DocumentContent.Children.Add(columns);
    }
    private PlanningShotEditRow CreatePlanningShotRow(RAWSelectionAssistant.Core.Services.Projects.ProjectShot shot)
    {
        var ids=shot.References.Select(r=>r.ReferenceId).ToHashSet();var reference=_vm!.AllProjectReferences.FirstOrDefault(r=>ids.Contains(r.Id));
        var owner=_vm;var revision=_documentRenderRevision;var generation=owner.EditorGeneration;
        return new(owner.ShotDraft(shot.ShotId),value=>{if(generation==owner.EditorGeneration&&revision==_documentRenderRevision)owner.StageShot(value);},()=>owner.ShotDraft(shot.ShotId)){ReferencePreviewPath=reference?.PreviewPath,ReferenceLabel=reference?.Title??"未关联参考"};
    }
    private void FlushSimpleEditors()
    {
        foreach(var capture in _structureDraftCaptures.ToArray())capture();
        if (_shotTable?.CommitEdit(DataGridEditingUnit.Cell, true) == false) throw new InvalidOperationException("镜头单元格尚未提交。");
        if (_shotTable?.CommitEdit(DataGridEditingUnit.Row, true) == false) throw new InvalidOperationException("镜头行尚未提交。");
        _inspectorCapture?.Invoke();
        foreach (var editor in DocumentContent.Children.OfType<PlanningRichEditor>()) editor.FlushPending();
    }
}
