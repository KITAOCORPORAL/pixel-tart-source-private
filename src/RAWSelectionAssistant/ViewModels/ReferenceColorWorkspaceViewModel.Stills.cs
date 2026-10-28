using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.ViewModels;
public sealed partial class ReferenceColorWorkspaceViewModel
{
    public IReadOnlyList<StudioStillSnapshot> R54Stills=>Layout.R54Stills;
    public bool CaptureR54Still()
    {
        var image=Editor.MatchedImage??Editor.SourceImage;if(image is null){StatusText="请先加载并完成当前照片预览。";return false;}
        if(!Editor.IsSettled){StatusText="请等待当前成品预览完成再抓取剧照。";return false;}
        var scale=Math.Min(1,1200d/Math.Max(image.PixelWidth,image.PixelHeight));var proxy=new TransformedBitmap(image,new System.Windows.Media.ScaleTransform(scale,scale));proxy.Freeze();
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(proxy));using var stream=new MemoryStream();png.Save(stream);if(stream.Length>8*1024*1024){StatusText="剧照超过8MB上限，未保存。";return false;}
        var still=new StudioStillSnapshot(Guid.NewGuid(),$"剧照 {DateTime.Now:HH:mm:ss}",Convert.ToBase64String(stream.ToArray()),Editor.AdjustmentStack.Nodes.Count>0?Editor.AdjustmentStack.DeepClone():null);
        Layout=Layout with{R54Stills=Layout.R54Stills.Append(still).TakeLast(8).ToArray()};OnPropertyChanged(nameof(R54Stills));NotifyR53Edit();StatusText="剧照已抓取，随项目保存（最多8张）。";return true;
    }
    public static BitmapSource R54StillImage(StudioStillSnapshot still)
    {
        using var stream=new MemoryStream(Convert.FromBase64String(still.PngBase64));var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.StreamSource=stream;image.EndInit();image.Freeze();return image;
    }
    public void ApplyR54Still(StudioStillSnapshot still)
    {
        if(still.Stack is null){StatusText="此剧照只含成品图，无可复用调整栈。";return;}
        if(LocalOutputEditing||ActiveTarget?.RawDefinition is not null){StatusText="当前输入域不同，未跨域套用剧照参数。";return;}
        Editor.SelectedColorScheme=new(still.Id,still.Name,still.Stack,DateTimeOffset.UtcNow);Editor.ApplyColorSchemeCommand.Execute(null);
    }
}
