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
        Heading(DocumentContent, "项目概览");
        DocumentContent.Children.Add(Text($"{_vm.DocumentTitle} · {_vm.DocumentDate} · {_vm.ProgressText}", 16));
        foreach (var (label, property) in new[] { ("标题", nameof(_vm.DocumentTitle)), ("地点", nameof(_vm.DocumentLocation)),
            ("人员", nameof(_vm.DocumentPeople)), ("拍摄目标", nameof(_vm.ShootGoal)), ("视觉关键词", nameof(_vm.Keywords)),
            ("客户要求", nameof(_vm.ClientRequirements)), ("必拍内容", nameof(_vm.MustCapture)),
            ("注意事项", nameof(_vm.PlanningNotes)), ("交付用途", nameof(_vm.OutputPurpose)) })
        { DocumentContent.Children.Add(Text(label, 12)); DocumentContent.Children.Add(Editor(property, 2)); }
        DocumentContent.Children.Add(AsyncAction("保存", async () => { await FlushDocumentAsync(); }));
    }
    private void RenderEditableShotTable()
    {
        if (_vm is null) return;
        Heading(DocumentContent, "镜头清单");
        DocumentContent.Children.Add(Text("双击单元格编辑；Esc 取消当前行修改。自定义列移除后保留数据，可恢复。", 12));
        _shotTable = new DataGrid { AutoGenerateColumns = false, CanUserAddRows = false, CanUserDeleteRows = false,
            CanUserSortColumns = false, CanUserReorderColumns = false, FrozenColumnCount = 1, MinHeight = 180, MaxHeight = 560,
            ItemsSource = _vm.Shots.OrderBy(s => s.Order).Select(s => new PlanningShotEditRow(_vm.ShotDraft(s.ShotId), _vm.StageShot)).ToArray() };
        AutomationProperties.SetName(_shotTable, "可编辑镜头清单"); AutomationProperties.SetAutomationId(_shotTable, "PlanningEditableShotTable");
        _shotTable.SelectionChanged += (_, _) => { if (_shotTable.SelectedItem is PlanningShotEditRow row) _vm.SelectedShot = _vm.Shots.FirstOrDefault(s => s.ShotId == row.ShotId); };
        void Column(string title, string key, bool readOnly = false, double width = 130)
        {
            _shotTable.Columns.Add(new DataGridTextColumn { Header = title, IsReadOnly = readOnly, Width = width,
                Binding = new Binding("[" + key + "]") { Mode = readOnly ? BindingMode.OneWay : BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.LostFocus } });
        }
        Column("镜号", "Order", true, 64); Column("名称", "Name"); Column("场景", "Scene"); Column("景别", "ShotSize", width: 90);
        Column("机位 / 镜头", "CameraAndLens"); Column("人物动作", "Action"); Column("道具", "Props"); Column("灯光", "Lighting");
        Column("预计分钟", "EstimatedMinutes", width: 85);
        _shotTable.Columns.Add(new DataGridComboBoxColumn { Header = "状态", Width = 90, ItemsSource = PlanningShotEditRow.StatusLabels,
            SelectedItemBinding = new Binding("[Status]") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged } });
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
    private void FlushSimpleEditors()
    {
        if (_shotTable?.CommitEdit(DataGridEditingUnit.Cell, true) == false) throw new InvalidOperationException("镜头单元格尚未提交。");
        if (_shotTable?.CommitEdit(DataGridEditingUnit.Row, true) == false) throw new InvalidOperationException("镜头行尚未提交。");
        foreach (var editor in DocumentContent.Children.OfType<PlanningRichEditor>()) editor.FlushPending();
    }
}
