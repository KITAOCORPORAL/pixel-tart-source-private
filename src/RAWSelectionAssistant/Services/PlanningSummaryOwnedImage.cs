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
    public const int MaximumSnapshotBase64Characters=32*1024*1024;
    public const int MaximumSnapshotBytes=24*1024*1024;
    public const long MaximumSnapshotPixels=4_194_304;
    public static Image ReadSnapshot(PlanningCanvasRevision revision,Guid projectId,double contentWidth,double contentHeight)
    {
        if(revision.RevisionId==Guid.Empty||revision.CanvasId==Guid.Empty||revision.EditableSnapshot?.CanvasId!=revision.CanvasId||revision.EditableSnapshot.ProjectId is Guid owner&&owner!=projectId)
            throw new InvalidDataException("快照身份与当前策划不匹配。");
        var encoded=revision.PreviewPngBase64;
        if(string.IsNullOrWhiteSpace(encoded))throw new InvalidDataException("未保存PNG预览。");
        if(encoded.Length>MaximumSnapshotBase64Characters)throw new InvalidDataException("PNG编码超过32MiB字符预算。");
        byte[] data;
        try { data=Convert.FromBase64String(encoded); }
        catch(FormatException error){throw new InvalidDataException("PNG编码已损坏，无法读取该固定版本。",error);}
        if(data.Length<33||data.Length>MaximumSnapshotBytes)throw new InvalidDataException("PNG编码字节不在33字节至24MiB范围内。");
        if(!data.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10})||!data.AsSpan(12,4).SequenceEqual("IHDR"u8))throw new NotSupportedException("保存的预览不是支持的PNG。");
        var width=BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(16,4));var height=BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(20,4));
        if(width==0||height==0||width>16384||height>16384||(long)width*height>MaximumSnapshotPixels)
            throw new InvalidDataException("快照超过单图4194304像素预算（解码工作估算64MiB，不含编码字节）。");
        if(revision.PreviewWidth is int savedWidth&&savedWidth!=width||revision.PreviewHeight is int savedHeight&&savedHeight!=height)
            throw new InvalidDataException("保存的PNG尺寸与版本记录不一致。");
        if(data[24]!=8)throw new NotSupportedException("固定快照阅读导出仅支持8位PNG，不自动降低精度。");
        var position=8;
        while(position<=data.Length-12)
        {
            var length=BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(position,4));
            if(length>data.Length-position-12)throw new InvalidDataException("PNG数据块不完整。");
            if(data.AsSpan(position+4,4).SequenceEqual("acTL"u8))throw new NotSupportedException("动画PNG不在固定快照阅读范围内。");
            position=checked(position+(int)length+12);
        }
        return Decode(data,width,height,contentWidth,contentHeight,"已保存画布固定快照");
    }
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
