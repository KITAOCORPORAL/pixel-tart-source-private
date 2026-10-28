using RAWSelectionAssistant.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Views;
public partial class PhotoGradePageView
{
    private CancellationTokenSource? r53PresetCancellation;
    private long r53PresetRevision;
    private async void RenderR53PresetCards(Panel target)
    {
        r53PresetCancellation?.Cancel();var cancellation=new CancellationTokenSource();r53PresetCancellation=cancellation;var token=cancellation.Token;var revision=++r53PresetRevision;
        if(model is not{} workspace){cancellation.Dispose();return;}
        try
        {
        var asset=workspace.ActiveTarget?.Id;var variant=workspace.ActiveTarget?.ActiveEditVersionId;var source=workspace.LayerThumbnailSource??workspace.Editor.SourceImage;
        var cards=new List<(string Name,ColorAdjustmentStack Stack,Action Apply)>();
        foreach(var (name,type,key,value) in new[]{("提亮 +0.3 EV",ColorStudioNodeType.BasicTone,"exposure",.3),("柔和对比",ColorStudioNodeType.BasicTone,"contrast",-15),("暖色",ColorStudioNodeType.WhiteBalance,"temperature",8),("自然饱和",ColorStudioNodeType.BasicTone,"vibrance",15)})
        {
            var node=new ColorAdjustmentStackNode(Guid.NewGuid(),type,name,NumericParameters:new Dictionary<string,double>{{key,value}});
            cards.Add((name,new([node]),()=>{workspace.Editor.AddAdjustmentNodeCommand.Execute(type.ToString());var parameter=ColorStudioToolCatalog.GetParameters(type).FirstOrDefault(p=>p.Key==key);if(parameter is not null)workspace.Editor.SetToolParameter(parameter,value);}));
        }
        foreach(var scheme in workspace.Editor.ColorSchemes.Take(128))cards.Add((scheme.Name,scheme.Stack,()=>{workspace.Editor.SelectedColorScheme=scheme;workspace.Editor.RequestApplySchemeCommand.Execute(null);}));
        var grid=new System.Windows.Controls.Primitives.UniformGrid{Columns=2,Width=244,Margin=new Thickness(2)};target.Children.Add(grid);
        foreach(var card in cards)
        {
            if(!string.IsNullOrWhiteSpace(r53Search)&&!card.Name.Contains(r53Search,StringComparison.CurrentCultureIgnoreCase)||r53FavoritesOnly&&!workspace.R53FavoriteResources.Contains(card.Name)||r53RecentOnly&&!workspace.R53RecentResources.Contains(card.Name))continue;
            var image=new Image{Height=88,Width=108,Stretch=Stretch.UniformToFill};var content=new StackPanel();content.Children.Add(image);content.Children.Add(new TextBlock{Text=card.Name,TextTrimming=TextTrimming.CharacterEllipsis,Margin=new Thickness(0,3,0,0)});
            var cell=new StackPanel{Width=118,Margin=new Thickness(1,3,1,5)};var choose=new Button{Content=content,Height=112,Padding=new Thickness(2),ToolTip=card.Name};choose.Click+=(_,_)=>{card.Apply();workspace.RecordR53Resource(card.Name);};cell.Children.Add(choose);
            var favorite=new Button{Content=new DarkroomIcon{Kind="star"},Height=20,Width=24,HorizontalAlignment=HorizontalAlignment.Right,ToolTip="收藏 "+card.Name};favorite.Click+=(_,_)=>{workspace.ToggleR53Favorite(card.Name);RenderR53Resources();};cell.Children.Add(favorite);grid.Children.Add(cell);
            // Raster gallery uses the unchanged production bitmap renderer at <=192 px.
            // RAW/document-working-domain cards are explicitly unavailable until their matching adapter exists.
            if(source is null||workspace.IsProfessionalRaw){choose.IsEnabled=false;choose.ToolTip="当前输入尚无该预设的同域缩略适配；请使用右侧已支持工具";continue;}
            try
            {
                var scale=Math.Min(1,192d/Math.Max(source.PixelWidth,source.PixelHeight));var small=new TransformedBitmap(source,new ScaleTransform(scale,scale));small.Freeze();var look=workspace.Editor.SelectedLook;
                var previewStack=workspace.Editor.AdjustmentStack with{Nodes=workspace.Editor.AdjustmentStack.Nodes.Concat(card.Stack.Nodes).Take(64).ToArray()};
                var bitmap=await Task.Run(()=>ColorStudioBitmapRenderer.Render(small,previewStack,look,token),token);
                if(token.IsCancellationRequested||revision!=r53PresetRevision||workspace.ActiveTarget?.Id!=asset||workspace.ActiveTarget?.ActiveEditVersionId!=variant)return;image.Source=bitmap;
            }
            catch(OperationCanceledException){return;}
            catch(Exception error)when(error is InvalidOperationException or ArgumentException or NotSupportedException or System.IO.IOException or System.Runtime.InteropServices.COMException){choose.IsEnabled=false;choose.ToolTip="缩略图失败："+error.Message;}
        }
        if(r53ResourceTab=="滤镜"){var existing=new Button{Content="胶片风格与已有方案…",ToolTip="打开现有胶片风格处理链"};existing.Click+=(_,_)=>{EditModes.SelectedIndex=3;OpenR53Panel();};target.Children.Add(existing);}
        }
        finally{if(ReferenceEquals(r53PresetCancellation,cancellation))r53PresetCancellation=null;cancellation.Dispose();}
    }
}
