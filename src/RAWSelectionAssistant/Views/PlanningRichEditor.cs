using System.Windows.Threading;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Services;
using System.Windows.Input;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
namespace RAWSelectionAssistant.Views;
public sealed partial class PlanningRichEditor : DockPanel
{
    private readonly RichTextBox _editor=new(){MinHeight=420,AcceptsTab=true,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
    private PlanningPageLayout _page=new();
    private IReadOnlyList<string> _losses=[];
    private Action? _persist;
    private RichDocumentHistory _history=null!;
    private readonly DispatcherTimer _saveTimer=new(){Interval=TimeSpan.FromMilliseconds(350)};
    public PlanningRichEditor(string? data,string text,Action<string,string,PlanningRichLayout> save,IEnumerable<PlanningCanvasRevision> canvasImages,PlanningRichLayout? layout=null)
    {
        var tools=new WrapPanel();SetDock(tools,Dock.Top);Children.Add(tools);Children.Add(_editor);
        if(data is not null){using var input=new MemoryStream(Convert.FromBase64String(data));new TextRange(_editor.Document.ContentStart,_editor.Document.ContentEnd).Load(input,DataFormats.Rtf);}
        else _editor.Document=new FlowDocument(new Paragraph(new Run(text)));
        _page=layout?.Page??new();_losses=layout?.ImportLosses??[];PlanningDocumentLayout.Apply(_editor.Document,layout);
        _editor.IsUndoEnabled=false;
        _editor.PreviewKeyDown+=(_,e)=>{if(Keyboard.Modifiers.HasFlag(ModifierKeys.Control)&&e.Key is Key.F or Key.H){OpenFind(e.Key==Key.H);e.Handled=true;}};
        _editor.PreviewKeyDown+=(_,e)=>{if(Keyboard.Modifiers.HasFlag(ModifierKeys.Control)&&e.Key is Key.Z or Key.Y){if(e.Key==Key.Y||Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))_history.Redo();else _history.Undo();_persist?.Invoke();e.Handled=true;}};_history=new(_editor,()=>PlanningDocumentLayout.Capture(_editor.Document,_page,_losses),value=>{_page=value.Page;_losses=value.ImportLosses;});_history.Initialize();
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Undo,(_,e)=>{_history.Undo();_persist?.Invoke();e.Handled=true;},(_,e)=>{e.CanExecute=true;e.Handled=true;}));
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Redo,(_,e)=>{_history.Redo();_persist?.Invoke();e.Handled=true;},(_,e)=>{e.CanExecute=true;e.Handled=true;}));
        void Add(string label,Action action){var button=new Button{Content=label,Margin=new Thickness(2),Padding=new Thickness(6,3,6,3)};button.Click+=(_,_)=>{try {if(label is "撤销" or "重做")action();else _history.Execute(action);_persist?.Invoke();_editor.Focus();}catch(Exception ex) when(ex is IOException or NotSupportedException or System.Xml.XmlException or InvalidDataException){ThemedMessageDialog.Show(Window.GetWindow(this),"文档操作未完成",ex.Message,ThemedMessageKind.Error);}};tools.Children.Add(button);}
        void Command(string label,System.Windows.Input.RoutedUICommand command)=>Add(label,()=>command.Execute(null,_editor));
        Command("加粗",EditingCommands.ToggleBold);Command("斜体",EditingCommands.ToggleItalic);Command("下划线",EditingCommands.ToggleUnderline);
        void Style(string style){if(_editor.CaretPosition.Paragraph is not {} p)return;p.Tag=new PlanningBlockSemantic(0,style,p.BreakPageBefore);p.FontSize=style switch{"Heading1"=>26,"Heading2"=>21,"Heading3"=>17,_=>14};p.FontWeight=style=="Body"?FontWeights.Normal:FontWeights.Bold;}
        Add("正文",()=>Style("Body"));Add("标题1",()=>Style("Heading1"));Add("标题2",()=>Style("Heading2"));Add("标题3",()=>Style("Heading3"));
        var outline=new ComboBox{Width=130,ToolTip="标题大纲"};tools.Children.Add(outline);
        outline.DropDownOpened+=(_,_)=>outline.ItemsSource=_editor.Document.Blocks.OfType<Paragraph>().Where(p=>(p.Tag as PlanningBlockSemantic)?.Style.StartsWith("Heading")==true).Select(p=>new{Text=new TextRange(p.ContentStart,p.ContentEnd).Text.Trim(),Paragraph=p}).ToArray();outline.DisplayMemberPath="Text";
        outline.SelectionChanged+=(_,_)=>{if(outline.SelectedItem is {} selected && selected.GetType().GetProperty("Paragraph")?.GetValue(selected) is Paragraph p){_editor.CaretPosition=p.ContentStart;p.BringIntoView();}};
        Add("分页",()=>{if(_editor.CaretPosition.Paragraph is {} p)p.BreakPageBefore=!p.BreakPageBefore;});
        Add("横/竖页",()=>{_page=_page with{Width=_page.Height,Height=_page.Width};PlanningDocumentLayout.Apply(_editor.Document,PlanningDocumentLayout.Capture(_editor.Document,_page,_losses));});
        Add("A4/Letter",()=>{_page=_page.Width==794?new(816,1056):new();PlanningDocumentLayout.Apply(_editor.Document,PlanningDocumentLayout.Capture(_editor.Document,_page,_losses));});
        Add("页边距",()=>{var margin=_page.Left==48?72:48;_page=_page with{Left=margin,Top=margin,Right=margin,Bottom=margin};PlanningDocumentLayout.Apply(_editor.Document,PlanningDocumentLayout.Capture(_editor.Document,_page,_losses));});        Command("左对齐",EditingCommands.AlignLeft);Command("居中",EditingCommands.AlignCenter);Command("右对齐",EditingCommands.AlignRight);Command("列表",EditingCommands.ToggleBullets);
        Add("撤销",_history.Undo);Add("重做",_history.Redo);
        Add("表格",()=>{_editor.BeginChange();try{var table=new Table();var group=new TableRowGroup();table.RowGroups.Add(group);for(var r=0;r<3;r++){var row=new TableRow();group.Rows.Add(row);for(var c=0;c<3;c++)row.Cells.Add(new TableCell(new Paragraph(new Run("单元格"))){BorderBrush=Brushes.Gray,BorderThickness=new Thickness(1),Padding=new Thickness(4)});}_editor.Document.Blocks.Add(table);}finally{_editor.EndChange();}});
        TableCell? Cell(){DependencyObject? item=_editor.CaretPosition.Parent;while(item is not null){if(item is TableCell c)return c;item=(item as FrameworkContentElement)?.Parent;}return null;}
        Add("合并单元格",()=>{var cell=Cell();var table=PlanningTableEditing.TableOf(cell)??throw new InvalidDataException("请先选择表格单元格。");
            var chosen=PlanningTableEditing.Map(table).Where(p=>_editor.Selection.Start.CompareTo(p.Cell.ContentEnd)<0&&_editor.Selection.End.CompareTo(p.Cell.ContentStart)>0).Select(p=>p.Cell).ToArray();
            try{PlanningTableEditing.Merge(table,chosen);}catch(InvalidOperationException e){throw new InvalidDataException(e.Message,e);}});
        Add("拆分单元格",()=>{if(Cell() is {} cell&&PlanningTableEditing.TableOf(cell) is {} table)PlanningTableEditing.Split(table,cell);});
        Add("重复表头",()=>{if(PlanningTableEditing.TableOf(Cell()) is {} table){var semantic=table.Tag as PlanningBlockSemantic??new(0);table.Tag=semantic with{RepeatHeader=!semantic.RepeatHeader};}});
        Add("加行",()=>{if(Cell()?.Parent is TableRow row && row.Parent is TableRowGroup group){var added=new TableRow();foreach(var c in row.Cells)added.Cells.Add(new TableCell(new Paragraph(new Run(""))));group.Rows.Insert(group.Rows.IndexOf(row)+1,added);}});
        Add("删行",()=>{if(Cell()?.Parent is TableRow row && row.Parent is TableRowGroup group && group.Rows.Count>1)group.Rows.Remove(row);});
        Add("加列",()=>{if(Cell()?.Parent is TableRow row && row.Parent is TableRowGroup group)foreach(var r in group.Rows)r.Cells.Add(new TableCell(new Paragraph(new Run(""))));});
        Add("删列",()=>{if(Cell() is {} cell && cell.Parent is TableRow row && row.Parent is TableRowGroup group){var index=row.Cells.IndexOf(cell);if(row.Cells.Count>1)foreach(var r in group.Rows)if(r.Cells.Count>index)r.Cells.RemoveAt(index);}});
        void Numeric(string label,double initial,double min,double max,Action<double> commit)
        {
            tools.Children.Add(new TextBlock{Text=label,VerticalAlignment=VerticalAlignment.Center});var box=new TextBox{Tag="PrecisionInput",Text=initial.ToString(System.Globalization.CultureInfo.CurrentCulture),Width=62,ToolTip=$"毫米，范围 {min}–{max}；Enter/失焦提交，Esc 取消"};tools.Children.Add(box);var committed=box.Text;
            void Apply(){if(box.Text==committed)return;if(!double.TryParse(box.Text,out var value)||!double.IsFinite(value)||value<min||value>max){box.ToolTip=$"无效数值：请输入 {min}–{max} 毫米";box.BorderBrush=Brushes.OrangeRed;return;}try{_history.Execute(()=>commit(value));}catch(ArgumentException e){box.ToolTip=e.Message;box.BorderBrush=Brushes.OrangeRed;return;}committed=box.Text;box.ClearValue(Control.BorderBrushProperty);_persist?.Invoke();}
            box.KeyDown+=(_,e)=>{if(e.Key==Key.Enter){Apply();e.Handled=true;}if(e.Key==Key.Escape){box.Text=committed;box.ClearValue(Control.BorderBrushProperty);e.Handled=true;}};box.LostKeyboardFocus+=(_,_)=>Apply();
        }
        Numeric("列宽 mm",40,5,250,value=>{if(Cell() is {} cell&&cell.Parent is TableRow row&&row.Parent is TableRowGroup group&&group.Parent is Table table){while(table.Columns.Count<row.Cells.Count)table.Columns.Add(new TableColumn());table.Columns[row.Cells.IndexOf(cell)].Width=new GridLength(value*96/25.4);}});
        foreach(var side in new[]{"左","上","右","下"}){var captured=side;Numeric(side+"边距 mm",12.7,0,80,value=>{var dip=value*96/25.4;_page=captured switch{"左"=>_page with{Left=dip},"上"=>_page with{Top=dip},"右"=>_page with{Right=dip},_=>_page with{Bottom=dip}};PlanningDocumentLayout.Apply(_editor.Document,PlanningDocumentLayout.Capture(_editor.Document,_page,_losses));});}        Add("列宽",()=>{if(Cell() is {} cell && cell.Parent is TableRow row && row.Parent is TableRowGroup group && group.Parent is Table table){while(table.Columns.Count<row.Cells.Count)table.Columns.Add(new TableColumn{Width=new GridLength(120)});var column=table.Columns[row.Cells.IndexOf(cell)];column.Width=new GridLength(column.Width.Value==120?180:120);}});
        Add("表格对齐",()=>{if(Cell() is {} cell)cell.TextAlignment=cell.TextAlignment==TextAlignment.Left?TextAlignment.Center:TextAlignment.Left;});
        Add("边框",()=>{if(Cell() is {} cell){cell.BorderBrush=Brushes.Gray;cell.BorderThickness=cell.BorderThickness.Left==0?new Thickness(1):new Thickness(0);}});
        void Image(byte[] bytes){_editor.BeginChange();try{using var stream=new MemoryStream(bytes);var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();_editor.Document.Blocks.Add(new BlockUIContainer(new Image{Source=bitmap,Width=480,Stretch=Stretch.Uniform}));}finally{_editor.EndChange();}}
        Add("图片",()=>{var dialog=new OpenFileDialog{Filter="图片|*.png;*.jpg;*.jpeg"};if(dialog.ShowDialog()==true)Image(File.ReadAllBytes(dialog.FileName));});
        var canvasPicker=new ComboBox{Width=140,ItemsSource=canvasImages.Where(v=>v.PreviewPngBase64 is not null).GroupBy(v=>v.CanvasId).Select(g=>g.Last()).ToArray(),DisplayMemberPath="EditableSnapshot.Name",ToolTip="选择需插入或更新的画布"};tools.Children.Add(canvasPicker);canvasPicker.SelectedIndex=0;
        Add("画布快照",()=>{var last=canvasPicker.SelectedItem as PlanningCanvasRevision;if(last is not null){var existing=_editor.Document.Blocks.FirstOrDefault(b=>(b.Tag as PlanningBlockSemantic)?.CanvasId==last.CanvasId);Image(Convert.FromBase64String(last.PreviewPngBase64!));var inserted=_editor.Document.Blocks.LastBlock!;inserted.Tag=new PlanningBlockSemantic(0,CanvasId:last.CanvasId,CanvasRevisionId:last.RevisionId);if(existing is not null){_editor.Document.Blocks.Remove(inserted);_editor.Document.Blocks.InsertBefore(existing,inserted);_editor.Document.Blocks.Remove(existing);}}else ThemedMessageDialog.Show(Window.GetWindow(this),"画布快照","请先在情绪板插入或更新画布版本。",ThemedMessageKind.Information);});
        Add("查找",()=>OpenFind(false));Add("替换",()=>OpenFind(true));
        Add("分页预览",()=>OpenPagedPreview());
        Add("打印 / PDF",()=>{var dialog=new PrintDialog();if(dialog.ShowDialog()==true)dialog.PrintDocument(((IDocumentPaginatorSource)Clone()).DocumentPaginator,"Kitao Studio 光镜 策划");});
        Add("导出 DOCX",()=>{var dialog=new SaveFileDialog{Filter="DOCX|*.docx"};if(dialog.ShowDialog()==true)Services.PlanningDocxAdapter.Export(_editor.Document,dialog.FileName);});
        Add("导入 DOCX",()=>{var dialog=new OpenFileDialog{Filter="DOCX|*.docx"};if(dialog.ShowDialog()!=true)return;var result=Services.PlanningDocxAdapter.Import(dialog.FileName);if(result.Losses.Count>0&&!ThemedMessageDialog.Show(Window.GetWindow(this),"导入损失报告",string.Join("\n",result.Losses)+"\n是否导入支持的内容？原文件不会修改。",ThemedMessageKind.Question,true))return;_losses=result.Losses;_editor.Document=result.Document;_page=new(double.IsFinite(result.Document.PageWidth)?result.Document.PageWidth:794,double.IsFinite(result.Document.PageHeight)?result.Document.PageHeight:1123,result.Document.PagePadding.Left,result.Document.PagePadding.Top,result.Document.PagePadding.Right,result.Document.PagePadding.Bottom);PlanningDocumentLayout.Apply(_editor.Document,PlanningDocumentLayout.Capture(_editor.Document,_page,_losses));});
        _persist=()=>{using var stream=new MemoryStream();new TextRange(_editor.Document.ContentStart,_editor.Document.ContentEnd).Save(stream,DataFormats.Rtf);save(Convert.ToBase64String(stream.ToArray()),new TextRange(_editor.Document.ContentStart,_editor.Document.ContentEnd).Text,PlanningDocumentLayout.Capture(_editor.Document,_page,_losses));};
        _saveTimer.Tick+=(_,_)=>{_saveTimer.Stop();_history.Checkpoint();_persist?.Invoke();};
        _editor.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Enter)_history.Checkpoint();};
        _editor.TextChanged+=(_,_)=>{if(_persist is null||_history.Restoring)return;_saveTimer.Stop();_saveTimer.Start();};
        Unloaded+=(_,_)=>{_saveTimer.Stop();_persist?.Invoke();};
    }
    public void ApplyPlanningPaperAppearance()
    {
        if(Children.OfType<WrapPanel>().FirstOrDefault() is {} tools)
        {
            var pages=new Dictionary<string,WrapPanel>{{"开始",new()},{"插入",new()},{"布局",new()},{"文档",new()}};
            var controls=tools.Children.Cast<UIElement>().ToArray();tools.Children.Clear();Children.Remove(tools);
            var tabs=new TabControl{Margin=new Thickness(0,6,0,10),Background=new SolidColorBrush(Color.FromRgb(35,35,39)),BorderBrush=new SolidColorBrush(Color.FromRgb(52,52,56))};
            var style=(Style)System.Windows.Markup.XamlReader.Parse("<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='TabItem'><Setter Property='Foreground' Value='#E7E6E2'/><Setter Property='Background' Value='#232327'/><Setter Property='Padding' Value='12,5'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='TabItem'><Border x:Name='tab' Background='{TemplateBinding Background}' BorderBrush='#343438' BorderThickness='0,0,0,2' Padding='{TemplateBinding Padding}'><ContentPresenter ContentSource='Header'/></Border><ControlTemplate.Triggers><Trigger Property='IsSelected' Value='True'><Setter TargetName='tab' Property='BorderBrush' Value='#9CC4BA'/><Setter Property='Background' Value='#303C39'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='tab' Property='BorderThickness' Value='1'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>");tabs.ItemContainerStyle=style;SetDock(tabs,Dock.Top);
            string group="开始";
            foreach(var control in controls)
            {
                if(control is Button {Content:string label})group=label switch
                {
                    "图片" or "画布快照" or "表格"=>"插入",
                    "分页" or "横/竖页" or "A4/Letter" or "页边距" or "加行" or "删行" or "加列" or "删列" or "列宽" or "表格对齐" or "边框"=>"布局",
                    "分页预览" or "打印 / PDF" or "导出 DOCX" or "导入 DOCX"=>"文档",_=>"开始"
                };
                pages[group].Children.Add(control);
            }
            foreach(var pair in pages)tabs.Items.Add(new TabItem{Header=pair.Key,Content=new ScrollViewer{Content=pair.Value,MaxHeight=120,VerticalScrollBarVisibility=ScrollBarVisibility.Auto}});
            Children.Insert(0,tabs);
        }
        _editor.Background=new SolidColorBrush(Color.FromRgb(249,248,244));_editor.Foreground=Brushes.Black;
        _editor.BorderBrush=new SolidColorBrush(Color.FromRgb(70,70,74));_editor.BorderThickness=new Thickness(1);
        _editor.Padding=new Thickness(28);_editor.MinHeight=420;_editor.MaxHeight=640;_editor.MaxWidth=794;_editor.HorizontalAlignment=HorizontalAlignment.Center;
        _editor.HorizontalScrollBarVisibility=ScrollBarVisibility.Auto;
        var zoomBar=new WrapPanel{Margin=new Thickness(0,4,0,8)};SetDock(zoomBar,Dock.Top);zoomBar.Children.Add(new TextBlock{Text="页面缩放",VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(4)});
        var zoomChoice=new ComboBox{ItemsSource=new[]{50,75,100,125,150,175,200},SelectedItem=100,Width=76,Margin=new Thickness(4)};System.Windows.Automation.AutomationProperties.SetName(zoomChoice,"页面缩放百分比");_zoomChoice=zoomChoice;zoomBar.Children.Add(zoomChoice);zoomChoice.DropDownOpened+=(_,_)=>zoomChoice.SelectedItem=(int)_previewZoom;zoomChoice.SelectionChanged+=(_,_)=>{if(zoomChoice.SelectedItem is int value&&value!=_previewZoom){SetPageZoom(value);PageZoomChanged?.Invoke(value);}};Children.Insert(0,zoomBar);
    }
    private double _previewZoom=100;
    private ComboBox? _zoomChoice;
    public double PageZoom=>_previewZoom;
    public void SetPageZoom(double value){_previewZoom=Math.Clamp(value,50,200);_editor.LayoutTransform=new ScaleTransform(_previewZoom/100,_previewZoom/100);if(_zoomChoice is not null)_zoomChoice.SelectedItem=(int)_previewZoom;}
    public event Action<double>? PageZoomChanged;
    internal FlowDocumentPageViewer CreatePagedViewer()
    {
        var document=Clone();document.Background=Brushes.White;document.Foreground=Brushes.Black;
        return new(){Document=document,Zoom=_previewZoom,Background=Brushes.White,Foreground=Brushes.Black};
    }
    internal DockPanel CreatePagedPreview()
    {
        FlushPending();var viewer=CreatePagedViewer();var panel=new DockPanel();var controls=new WrapPanel();SetDock(controls,Dock.Top);panel.Children.Add(controls);panel.Children.Add(viewer);
        var count=new TextBlock{Margin=new Thickness(8),VerticalAlignment=VerticalAlignment.Center};
        viewer.Background=Brushes.White;viewer.Foreground=Brushes.Black;
        void Update(){var paginator=((IDocumentPaginatorSource)viewer.Document).DocumentPaginator;paginator.ComputePageCount();count.SetBinding(TextBlock.TextProperty,new System.Windows.Data.Binding(nameof(FlowDocumentPageViewer.MasterPageNumber)){Source=viewer,StringFormat="第 {0} 页 / 共 "+paginator.PageCount+" 页"});}
        void Button(string label,Action action){var button=new Button{Content=label,Margin=new Thickness(4)};button.Click+=(_,_)=>{action();Update();};controls.Children.Add(button);}
        Button("上一页",()=>viewer.PreviousPage());Button("下一页",()=>viewer.NextPage());controls.Children.Add(count);
        var page=new TextBox{Width=48,Margin=new Thickness(4),Text="1"};controls.Children.Add(page);Button("跳转",()=>{if(int.TryParse(page.Text,out var index)&&index>0)viewer.GoToPage(index);});
        var zoom=new ComboBox{ItemsSource=new[]{50,75,100,125,150,175,200},SelectedItem=(int)_previewZoom,Width=76,Margin=new Thickness(4)};controls.Children.Add(zoom);zoom.SelectionChanged+=(_,_)=>{if(zoom.SelectedItem is int value){viewer.Zoom=value;SetPageZoom(value);PageZoomChanged?.Invoke(value);}};
        panel.Height=640;panel.Loaded+=(_,_)=>Update();
        Update();return panel;
    }
    private void OpenPagedPreview()
    {
        var window=new Window{Title="Kitao Studio 光镜 · 分页预览",Content=CreatePagedPreview(),Width=900,Height=760,Owner=Window.GetWindow(this)};window.Show();
    }
    internal void UsePlanningDocxActions(Func<Task> export,Func<Task> import,Action<Exception> error)
    {
        // Replace the generic file actions with the planning diff/recovery path.
        foreach(var panel in Children.OfType<WrapPanel>().Concat(Children.OfType<TabControl>().SelectMany(t=>t.Items.OfType<TabItem>()).Select(t=>t.Content).OfType<WrapPanel>()))
        foreach(var old in panel.Children.OfType<Button>().Where(b=>b.Content is "导出 DOCX" or "导入 DOCX").ToArray())
        {
            var action=Equals(old.Content,"导出 DOCX")?export:import;var button=new Button{Content=old.Content,Margin=old.Margin};
            button.Click+=async(_,_)=>{try{await action();}catch(Exception e){error(e);}};
            var index=panel.Children.IndexOf(old);panel.Children.Remove(old);panel.Children.Insert(index,button);
        }
    }
    public void Retire(){_saveTimer.Stop();_persist=null;}
    public void FlushPending(){_saveTimer.Stop();_history.Checkpoint();_persist?.Invoke();}
    private FlowDocument Clone(){using var stream=new MemoryStream();new TextRange(_editor.Document.ContentStart,_editor.Document.ContentEnd).Save(stream,DataFormats.Rtf);stream.Position=0;var doc=new FlowDocument();new TextRange(doc.ContentStart,doc.ContentEnd).Load(stream,DataFormats.Rtf);PlanningDocumentLayout.Apply(doc,PlanningDocumentLayout.Capture(_editor.Document,_page,_losses));return doc;}
}
