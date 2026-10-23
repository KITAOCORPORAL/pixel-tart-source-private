using System.IO;
using System.Security.Cryptography;
using System.Buffers.Binary;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Services;

/// <summary>Summary-export-only reader. Never resolves a library original or an arbitrary external file.</summary>
public static class PlanningSummaryOwnedImage
{
    public const long MaximumBytes=64L*1024*1024;
    public const long MaximumPixels=40_000_000;
    public static Image Read(PlanningDocumentImage meta,Guid documentId,double contentWidth,double contentHeight)
    {
        meta.Validate();
        if(meta.DocumentId!=documentId)throw new InvalidDataException("图片不属于当前策划文档。");
        if(string.IsNullOrWhiteSpace(meta.StorageRoot))throw new InvalidDataException("自有图片未绑定当前项目存储根。");
        var root=Path.GetFullPath(meta.StorageRoot);var expected=Path.GetFullPath(Path.Combine(root,PlanningImageStorage.RelativePath(documentId,meta.DocumentImageId)));
        var stored=meta.CopyPath.Replace('\\','/');
        if(!stored.Equals(PlanningImageStorage.RelativePath(documentId,meta.DocumentImageId),StringComparison.OrdinalIgnoreCase)&&
           !(Path.IsPathFullyQualified(meta.CopyPath)&&Path.GetFullPath(meta.CopyPath).Equals(expected,StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("图片路径不属于当前项目的规范自有副本；未读取外部原件。");
        PlanningImageStorage.RejectLinks(root,expected);
        // A read handle denies concurrent writes/deletes; hash and decode use the same bounded bytes.
        using var stream=new FileStream(expected,FileMode.Open,FileAccess.Read,FileShare.Read);
        if(stream.Length<33||stream.Length>MaximumBytes)throw new InvalidDataException("自有图片大小不支持（允许33字节至64MB）。");
        var data=new byte[checked((int)stream.Length)];stream.ReadExactly(data);
        if(!Convert.ToHexString(SHA256.HashData(data)).Equals(meta.CopyHash,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("自有副本已变化（SHA-256不符）；未嵌入。");
        var image=Decode(data,meta.Width,meta.Height,contentWidth,contentHeight,meta.Title+"\n"+meta.Caption); image.Tag=meta; return image;
    }
    public static Image ReadImported(PlanningProjectCopy copy, Guid projectId, Guid documentId, double contentWidth, double contentHeight)
    {
        if(copy.Kind!="reference" || copy.ProjectId!=projectId || copy.DocumentId!=documentId)throw new InvalidDataException("自有参考与汇总项目/文档身份不匹配。");
        var bitmap=PlanningOwnedReferenceImage.ReadBounded(copy,projectId,documentId,copy.ReferenceId,PlanningOwnedReferenceImage.SummaryEdge);
        var maxWidth=Math.Max(1,Math.Min(520,contentWidth));var maxHeight=Math.Max(1,Math.Min(360,contentHeight*.42));var scale=Math.Min(1,Math.Min(maxWidth/bitmap.PixelWidth,maxHeight/bitmap.PixelHeight));
        return new Image{Source=bitmap,Width=bitmap.PixelWidth*scale,Height=bitmap.PixelHeight*scale,Stretch=Stretch.Uniform,ToolTip=Path.GetFileName(copy.OriginalPath)};
    }

    private static Image Decode(byte[] data,double savedWidth,double savedHeight,double contentWidth,double contentHeight,string title)
    {
        if(!data.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10})||!data.AsSpan(12,4).SequenceEqual("IHDR"u8))throw new NotSupportedException("自有副本不是已支持的PNG格式；未尝试其他文件。");
        var width=BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(16,4));var height=BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(20,4));
        if(width==0||height==0||width>16384||height>16384||(long)width*height>MaximumPixels)throw new InvalidDataException("图片像素尺寸超过汇总安全范围（最长边16384且总像素不超过4000万）。");
        using var bytes=new MemoryStream(data,false);var decoder=BitmapDecoder.Create(bytes,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);
        if(decoder.Frames.Count!=1)throw new NotSupportedException("多帧图片不在当前汇总范围。");
        var bitmap=decoder.Frames[0];if(bitmap.PixelWidth!=width||bitmap.PixelHeight!=height)throw new InvalidDataException("图片头与解码尺寸不一致。");bitmap.Freeze();
        var maxWidth=Math.Max(1,Math.Min(savedWidth,Math.Min(520,contentWidth)));var maxHeight=Math.Max(1,Math.Min(savedHeight,Math.Min(360,contentHeight*.42)));
        var scale=Math.Min(1,Math.Min(maxWidth/bitmap.PixelWidth,maxHeight/bitmap.PixelHeight));
        return new Image{Source=bitmap,Width=bitmap.PixelWidth*scale,Height=bitmap.PixelHeight*scale,Stretch=Stretch.Uniform,ToolTip=title};
    }
    public static string SourceLabel(PlanningDocumentImage meta)=>meta.Source is{} source
        ?$"来源：项目自有历史副本；文档ID {meta.DocumentId:D}；图片ID {meta.DocumentImageId:D}；原库ID {source.LibraryId:D}；素材ID {source.AssetId:D}；原素材Hash {source.ContentHash}；副本Hash {meta.CopyHash}。未读取库当前原件，不代表项目完整跨机便携。"
        :$"来源：项目自有图片；文档ID {meta.DocumentId:D}；图片ID {meta.DocumentImageId:D}；副本Hash {meta.CopyHash}。不代表外链/附件已打包。";
}
