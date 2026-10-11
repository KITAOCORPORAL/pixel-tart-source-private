using System.Windows.Input;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
namespace RAWSelectionAssistant.Views;
public sealed class PlanningRichEditor : DockPanel
{
    private readonly RichTextBox _editor=new(){MinHeight=420,AcceptsTab=true,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
    public PlanningRichEditor(string? data,string text,Action<string,string> save,IEnumerable<string> canvasImages)
    {
        var tools=new WrapPanel();SetDock(tools,Dock.Top);Children.Add(tools);Children.Add(_editor);
        if(data is not null){using var input=new MemoryStream(Convert.FromBase64String(data));new TextRange(_editor.Document.ContentStart,_editor.Document.ContentEnd).Load(input,DataFormats.Rtf);}
        else _editor.Document=new FlowDocument(new Paragraph(new Run(text)));
        _editor.Document.PageWidth=794;_editor.Document.PagePadding=new Thickness(48);
        void Add(string label,Action action){var button=new Button{Content=label,Margin=new Thickness(2),Padding=new Thickness(6,3,6,3)};button.Click+=(_,_)=>{try {action();_editor.Focus();}catch(Exception ex) when(ex is IOException or NotSupportedException or System.Xml.XmlException or InvalidDataException){ThemedMessageDialog.Show(Window.GetWindow(this),"文档操作未完成",ex.Message,ThemedMessageKind.Error);}};tools.Children.Add(button);}
        void Command(string label,System.Windows.Input.RoutedUICommand command)=>Add(label,()=>command.Execute(null,_editor));
        Command("加粗",EditingCommands.ToggleBold);Command("斜体",EditingCommands.ToggleItalic);Command("下划线",EditingCommands.ToggleUnderline);
        Add("正文",()=>{_editor.Selection.ApplyPropertyValue(TextElement.FontSizeProperty,14d);_editor.Selection.ApplyPropertyValue(TextElement.FontWeightProperty,FontWeights.Normal);});
        Add("文字颜色",()=>_editor.Selection.ApplyPropertyValue(TextElement.ForegroundProperty,Brushes.DarkBlue));
        Add("标题",()=>{_editor.Selection.ApplyPropertyValue(TextElement.FontSizeProperty,24d);_editor.Selection.ApplyPropertyValue(TextElement.FontWeightProperty,FontWeights.Bold);});
        Command("左对齐",EditingCommands.AlignLeft);Command("居中",EditingCommands.AlignCenter);Command("右对齐",EditingCommands.AlignRight);Command("列表",EditingCommands.ToggleBullets);
        Command("撤销",ApplicationCommands.Undo);Command("重做",ApplicationCommands.Redo);
        Add("表格",()=>{_editor.BeginChange();try{var table=new Table();var group=new TableRowGroup();table.RowGroups.Add(group);for(var r=0;r<3;r++){var row=new TableRow();group.Rows.Add(row);for(var c=0;c<3;c++)row.Cells.Add(new TableCell(new Paragraph(new Run("单元格"))){BorderBrush=Brushes.Gray,BorderThickness=new Thickness(1),Padding=new Thickness(4)});}_editor.Document.Blocks.Add(table);}finally{_editor.EndChange();}});
        void Image(byte[] bytes){_editor.BeginChange();try{using var stream=new MemoryStream(bytes);var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();_editor.Document.Blocks.Add(new BlockUIContainer(new Image{Source=bitmap,Width=480,Stretch=Stretch.Uniform}));}finally{_editor.EndChange();}}
        Add("图片",()=>{var dialog=new OpenFileDialog{Filter="图片|*.png;*.jpg;*.jpeg"};if(dialog.ShowDialog()==true)Image(File.ReadAllBytes(dialog.FileName));});
        Add("画布快照",()=>{var last=canvasImages.LastOrDefault();if(last is not null)Image(Convert.FromBase64String(last));else ThemedMessageDialog.Show(Window.GetWindow(this),"画布快照","请先在情绪板插入或更新画布版本。",ThemedMessageKind.Information);});
        Add("分页预览",()=>{var viewer=new FlowDocumentPageViewer{Document=Clone()};new Window{Title="Kitao Studio 光镜 · 分页预览",Content=viewer,Width=900,Height=760,Owner=Window.GetWindow(this)}.Show();});
        Add("打印 / PDF",()=>{var dialog=new PrintDialog();if(dialog.ShowDialog()==true)dialog.PrintDocument(((IDocumentPaginatorSource)Clone()).DocumentPaginator,"Kitao Studio 光镜 策划");});
        Add("导出 DOCX",()=>{var dialog=new SaveFileDialog{Filter="DOCX|*.docx"};if(dialog.ShowDialog()==true)Services.PlanningDocxAdapter.Export(_editor.Document,dialog.FileName);});
        Add("导入 DOCX",()=>{var dialog=new OpenFileDialog{Filter="DOCX|*.docx"};if(dialog.ShowDialog()!=true)return;var result=Services.PlanningDocxAdapter.Import(dialog.FileName);if(result.Losses.Count>0&&!ThemedMessageDialog.Show(Window.GetWindow(this),"导入损失报告",string.Join("\n",result.Losses)+"\n是否导入支持的内容？原文件不会修改。",ThemedMessageKind.Question,true))return;_editor.Document=result.Document;});
        _editor.TextChanged+=(_,_)=>{using var stream=new MemoryStream();new TextRange(_editor.Document.ContentStart,_editor.Document.ContentEnd).Save(stream,DataFormats.Rtf);save(Convert.ToBase64String(stream.ToArray()),new TextRange(_editor.Document.ContentStart,_editor.Document.ContentEnd).Text);};
    }
    private FlowDocument Clone(){using var stream=new MemoryStream();new TextRange(_editor.Document.ContentStart,_editor.Document.ContentEnd).Save(stream,DataFormats.Rtf);stream.Position=0;var doc=new FlowDocument();new TextRange(doc.ContentStart,doc.ContentEnd).Load(stream,DataFormats.Rtf);doc.PageWidth=794;doc.PageHeight=1123;doc.PagePadding=new Thickness(48);return doc;}
}
