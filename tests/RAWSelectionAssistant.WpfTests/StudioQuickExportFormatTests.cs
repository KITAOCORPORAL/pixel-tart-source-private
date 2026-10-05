using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Views;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class StudioQuickExportFormatTests
{
    [TestMethod]
    public Task JpegAndTiffExifDirectionsAreNormalizedOnceWithoutLosingPrecisionOrAlpha() => RunSta(()=>
    {
        var root=Path.Combine(Path.GetTempPath(),"PixelTart-Orientation-"+Guid.NewGuid());Directory.CreateDirectory(root);
        try
        {
            const int width=48,height=32;
            var rgba=new ushort[width*height*4];
            for(var y=0;y<height;y++)for(var x=0;x<width;x++)
            {
                var offset=(y*width+x)*4;
                rgba[offset]=(ushort)(1001+x*997);rgba[offset+1]=(ushort)(2003+y*1703);
                rgba[offset+2]=(ushort)(3007+x*503+y*307);rgba[offset+3]=(ushort)(10009+x*613+y*719);
            }
            var input=BitmapSource.Create(width,height,96,96,PixelFormats.Rgba64,null,rgba,width*8);
            foreach(var extension in new[]{"jpg","tiff"})for(ushort orientation=1;orientation<=8;orientation++)
            {
                var path=Path.Combine(root,$"input-{orientation}.{extension}");
                BitmapEncoder encoder=extension=="jpg"?new JpegBitmapEncoder{QualityLevel=95}:new TiffBitmapEncoder{Compression=TiffCompressOption.Zip};
                var metadata=new BitmapMetadata(extension);var query=extension=="jpg"?"/app1/ifd/{ushort=274}":"/ifd/{ushort=274}";
                metadata.SetQuery(query,orientation);
                BitmapSource source=extension=="jpg"?new FormatConvertedBitmap(input,PixelFormats.Bgr24,null,0):input;
                encoder.Frames.Add(BitmapFrame.Create(source,null,metadata,new System.Collections.ObjectModel.ReadOnlyCollection<ColorContext>([new ColorContext(PixelFormats.Bgra32)])));
                using(var file=File.Create(path))encoder.Save(file);
                // The codec's untransformed decoded raster is the oracle. This also
                // avoids claiming JPEG's lossy pixels equal the pre-encoding input.
                using var stream=File.OpenRead(path);
                var raw=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames[0];
                Assert.AreEqual(width,raw.PixelWidth);Assert.AreEqual(height,raw.PixelHeight);
                var decoded=ReadRgba64(raw);if(extension=="tiff")CollectionAssert.AreEqual(rgba,decoded,"TIFF fixture preserves all 16-bit channels.");
                var loaded=StudioQuickExport.Load(path);var output=ReadRgba64(loaded);
                var outputWidth=orientation>=5?height:width;var outputHeight=orientation>=5?width:height;
                Assert.AreEqual(outputWidth,loaded.PixelWidth,$"{extension} orientation {orientation}");Assert.AreEqual(outputHeight,loaded.PixelHeight);
                for(var y=0;y<outputHeight;y++)for(var x=0;x<outputWidth;x++)
                {
                    var (sx,sy)=orientation switch
                    {
                        2=>(width-1-x,y),3=>(width-1-x,height-1-y),4=>(x,height-1-y),
                        5=>(y,x),6=>(y,height-1-x),7=>(width-1-y,height-1-x),8=>(width-1-y,x),_=>(x,y)
                    };
                    for(var channel=0;channel<4;channel++)Assert.AreEqual(decoded[(sy*width+sx)*4+channel],output[(y*outputWidth+x)*4+channel],$"{extension} orientation {orientation}, pixel ({x},{y}), channel {channel}");
                }
                Assert.IsTrue(loaded.IsFrozen);
                // Lossless export/reload must not rotate a second time; the exported
                // metadata explicitly says its already-normalized raster is upright.
                var roundtrip=Path.Combine(root,$"roundtrip-{extension}-{orientation}.tif");
                StudioQuickExport.Encode(loaded,path,roundtrip,StudioExportFormat.Tiff);
                using var roundtripStream=File.OpenRead(roundtrip);
                var exported=BitmapDecoder.Create(roundtripStream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames[0];
                Assert.AreEqual((ushort)1,((BitmapMetadata)exported.Metadata).GetQuery("/ifd/{ushort=274}"));
                Assert.IsNotNull(exported.ColorContexts);Assert.IsGreaterThan(0,exported.ColorContexts.Count);
                var reloaded=StudioQuickExport.Load(roundtrip);Assert.AreEqual(outputWidth,reloaded.PixelWidth);Assert.AreEqual(outputHeight,reloaded.PixelHeight);CollectionAssert.AreEqual(output,ReadRgba64(reloaded));
                var jpeg=Path.Combine(root,$"upright-{extension}-{orientation}.jpg");StudioQuickExport.Encode(loaded,path,jpeg,StudioExportFormat.Jpeg);
                using var jpegStream=File.OpenRead(jpeg);var jpegFrame=BitmapDecoder.Create(jpegStream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames[0];
                Assert.AreEqual((ushort)1,((BitmapMetadata)jpegFrame.Metadata).GetQuery("/app1/ifd/{ushort=274}"));
                var jpegLoaded=StudioQuickExport.Load(jpeg);Assert.AreEqual(outputWidth,jpegLoaded.PixelWidth);Assert.AreEqual(outputHeight,jpegLoaded.PixelHeight);CollectionAssert.AreEqual(ReadRgba64(jpegFrame),ReadRgba64(jpegLoaded));
            }
        }
        finally{Directory.Delete(root,true);}
        return Task.CompletedTask;
    });
    private static ushort[] ReadRgba64(BitmapSource source)
    {
        var pixels=new ushort[source.PixelWidth*source.PixelHeight*4];new FormatConvertedBitmap(source,PixelFormats.Rgba64,null,0).CopyPixels(pixels,source.PixelWidth*8,0);return pixels;
    }
    [TestMethod]
    public Task ChosenEncodersPreservePrecisionAlphaAndEmbedSrgb() => RunSta(()=>
    {
        var root=Path.Combine(Path.GetTempPath(),"PixelTart-Format-"+Guid.NewGuid());Directory.CreateDirectory(root);
        try
        {
            ushort[] rgba=[10001,22002,33003,65535,40004,50005,60006,32768];
            var image=BitmapSource.Create(2,1,96,96,PixelFormats.Rgba64,null,rgba,16);
            var oriented=StudioQuickExport.ApplyOrientation(image,6);Assert.AreEqual(1,oriented.PixelWidth);Assert.AreEqual(2,oriented.PixelHeight);
            foreach(var format in new[]{StudioExportFormat.Png,StudioExportFormat.Tiff})
            {
                var path=Path.Combine(root,"source"+StudioQuickExport.Extension("input.jpg",format));StudioQuickExport.Encode(image,"input.jpg",path,format);
                using var stream=File.OpenRead(path);var frame=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames[0];
                Assert.IsGreaterThan(32,frame.Format.BitsPerPixel);Assert.IsNotNull(frame.ColorContexts);Assert.IsGreaterThan(0,frame.ColorContexts.Count);
                var read=new ushort[8];new FormatConvertedBitmap(frame,PixelFormats.Rgba64,null,0).CopyPixels(read,16,0);CollectionAssert.AreEqual(rgba,read);
            }
            var transparent=BitmapSource.Create(16,16,96,96,PixelFormats.Bgra32,null,new byte[16*16*4],64);
            var jpeg=Path.Combine(root,"alpha.jpg");StudioQuickExport.Encode(transparent,"input.png",jpeg,StudioExportFormat.Jpeg);
            var decoded=StudioQuickExport.Load(jpeg);var rgb=new byte[16*16*3];new FormatConvertedBitmap(decoded,PixelFormats.Bgr24,null,0).CopyPixels(rgb,48,0);
            Assert.IsTrue(rgb.All(x=>x>=253),"JPEG alpha is explicitly composited on white.");
            Assert.AreEqual(".tif",StudioQuickExport.Extension("camera.cr3",StudioExportFormat.Source));Assert.AreEqual(".png",StudioQuickExport.Extension("camera.cr3",StudioExportFormat.Png));
            Assert.ThrowsExactly<OperationCanceledException>(()=>StudioQuickExport.Encode(image,"input.png",Path.Combine(root,"cancel.png"),StudioExportFormat.Png,new(true)));
            Assert.IsFalse(File.Exists(Path.Combine(root,"cancel.png")));
        }
        finally{Directory.Delete(root,true);}
        return Task.CompletedTask;
    });
    [TestMethod]
    public Task RawToPngAndJpegReuseOneFrozenMasterAndCleanupIntermediates() => RunSta(async()=>
    {
        var root=Path.Combine(Path.GetTempPath(),"PixelTart-Format-Raw-"+Guid.NewGuid());Directory.CreateDirectory(root);
        try
        {
            var source=Path.Combine(root,"synthetic.cr3");File.WriteAllText(source,"synthetic decoder contract, not a real camera file");var decoder=new Decoder();
            using var workspace=new ReferenceColorWorkspaceViewModel(new Dialogs(root,source),rawDecoder:decoder);
            await workspace.ChooseTargetCommand.ExecuteAsync(null);
            workspace.QuickExportFormat=StudioExportFormat.Png;await workspace.ExportSelectedCommand.ExecuteAsync(null);
            var item=workspace.Targets[0];Assert.AreEqual(ReferenceExportStatus.Succeeded,item.ExportStatus);StringAssert.EndsWith(item.OutputPath!,".png");Assert.IsGreaterThan(32,StudioQuickExport.Load(item.OutputPath!).Format.BitsPerPixel);
            workspace.QuickExportFormat=StudioExportFormat.Jpeg;await workspace.ExportSelectedCommand.ExecuteAsync(null);
            Assert.AreEqual(ReferenceExportStatus.Succeeded,item.ExportStatus);StringAssert.EndsWith(item.OutputPath!,".jpg");Assert.AreEqual(1,decoder.Count);
            Assert.IsEmpty(Directory.GetFiles(root,"*.tmp*"));Assert.IsEmpty(Directory.GetFiles(root,"*.tif"));
            workspace.QuickExportFormat=StudioExportFormat.Source;await workspace.ExportSelectedCommand.ExecuteAsync(null);
            Assert.AreEqual(ReferenceExportStatus.Succeeded,item.ExportStatus);
            using var tiff=File.OpenRead(item.OutputPath!);var frame=BitmapDecoder.Create(tiff,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames[0];
            Assert.IsNotNull(frame.ColorContexts);Assert.IsGreaterThan(0,frame.ColorContexts.Count,"RAW TIFF output must embed its declared sRGB profile.");
        }
        finally{Directory.Delete(root,true);}
    });
    [TestMethod]
    public Task SubclassSelectorsUseTheActualDarkComboBoxTemplate() => RunSta(()=>
    {
        EnsureTestApplication();var language=new StudioLanguageSelector();var format=new StudioExportFormatSelector();
        var style=(Style)Application.Current.FindResource(typeof(ComboBox));
        Assert.AreSame(style,language.Style);Assert.AreSame(style,format.Style);
        language.ApplyTemplate();format.ApplyTemplate();Assert.IsNotNull(language.Template.FindName("InputBorder",language));Assert.IsNotNull(format.Template.FindName("InputBorder",format));
        Assert.AreNotEqual(Brushes.White,language.Background);Assert.HasCount(4,format.Items);
        return Task.CompletedTask;
    });
    private sealed class Decoder:IRawDecoder
    {
        public int Count{get;private set;}
        public RawDecoderCapability GetCapability()=>throw new NotSupportedException();
        public Task<RawDecodedImage> DecodeAsync(string path,RawToJpegOptions options,CancellationToken token=default)
        {Count++;return Task.FromResult(RawDecodedImage.FromRgb48(8,8,48,Enumerable.Range(0,64).SelectMany(i=>new ushort[]{(ushort)(17001+i),28003,39005}).ToArray(),new("Test","Synthetic",null,1,"sRGB")));}
    }
    private sealed class Dialogs(string root,string source):IDialogService
    {
        public string? ChooseFolder(string title,string? initialDirectory=null)=>root;public IReadOnlyList<string> ChooseFiles(string title,string filter,bool multiselect=true)=>[source];
        public string? ChooseSaveFile(string title,string filter,string defaultExtension,string? suggestedFileName=null)=>null;public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> currentToolIds)=>currentToolIds;
        public void ShowInfo(string message){}public void ShowError(string message){}public bool Confirm(string message,string title)=>false;public HelpAction ShowHelp()=>HelpAction.None;public void ShowFeedback(){}
        public RawFileEntry? ChooseRawCandidate(IReadOnlyList<RawFileEntry> candidates)=>null;public bool ShowMediaDetails(MediaSelectionItem item,bool showAdvancedDetails)=>false;public void RevealFile(string path){}
    }
}
