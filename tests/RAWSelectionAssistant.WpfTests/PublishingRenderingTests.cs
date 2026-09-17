using System.Security.Cryptography;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Publishing;
using RAWSelectionAssistant.Services.Publishing;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class PublishingRenderingTests
{
    [TestMethod] public async Task PublishingCompressionTests(){using var temp=new TemporaryDirectory();var source=CreateImage(temp.File("source.png"),800,600);var output=temp.File("out.jpg");await new WpfPublishingRenderer().RenderAsync(source,output,new(new(true,PublishingSizeMode.LongestEdge,400),false,[]));var size=ReadSize(output);Assert.AreEqual((400,300),size);}
    [TestMethod] public async Task PublishingImageWatermarkTests(){using var temp=new TemporaryDirectory();var source=CreateImage(temp.File("source.png"),640,480);var logo=CreateLogo(temp.File("logo.png"));var output=temp.File("out.png");await new WpfPublishingRenderer().RenderAsync(source,output,new(new(false),true,[new(Guid.NewGuid(),WatermarkLayerType.Image,ImagePath:logo,WidthPercent:20,Opacity:.8)],PublishingOutputFormat.Png));Assert.IsGreaterThan(new FileInfo(logo).Length,new FileInfo(output).Length);}
    [TestMethod] public async Task PublishingTextWatermarkTests(){using var temp=new TemporaryDirectory();var source=CreateImage(temp.File("source.png"),640,480);var plain=temp.File("plain.png");var marked=temp.File("marked.png");var renderer=new WpfPublishingRenderer();await renderer.RenderAsync(source,plain,new(new(false),false,[],PublishingOutputFormat.Png));await renderer.RenderAsync(source,marked,new(new(false),true,[new(Guid.NewGuid(),WatermarkLayerType.Text,Text:"Kitao Soma",Position:WatermarkPosition.Center)],PublishingOutputFormat.Png));CollectionAssert.AreNotEqual(SHA256.HashData(File.ReadAllBytes(plain)),SHA256.HashData(File.ReadAllBytes(marked)));}
    [TestMethod] public async Task PublishingHslTests(){using var temp=new TemporaryDirectory();var source=CreateImage(temp.File("source.png"),640,480);var logo=CreateLogo(temp.File("logo.png"));var normal=temp.File("normal.png");var adjusted=temp.File("adjusted.png");var renderer=new WpfPublishingRenderer();await renderer.RenderAsync(source,normal,new(new(false),true,[new(Guid.NewGuid(),WatermarkLayerType.Image,ImagePath:logo)],PublishingOutputFormat.Png));await renderer.RenderAsync(source,adjusted,new(new(false),true,[new(Guid.NewGuid(),WatermarkLayerType.Image,ImagePath:logo,ColorAdjustments:new(90,.2,.1,true))],PublishingOutputFormat.Png));CollectionAssert.AreNotEqual(SHA256.HashData(File.ReadAllBytes(normal)),SHA256.HashData(File.ReadAllBytes(adjusted)));}
    [TestMethod] public async Task PublishingNoCompressionWatermarkTests(){using var temp=new TemporaryDirectory();var source=CreateImage(temp.File("source.png"),613,417);var output=temp.File("out.jpg");await new WpfPublishingRenderer().RenderAsync(source,output,new(new(false,PublishingSizeMode.Original),true,[new(Guid.NewGuid(),WatermarkLayerType.Text,Text:"Mark")]));Assert.AreEqual((613,417),ReadSize(output));}
    private static string CreateImage(string path,int width,int height){var pixels=new byte[width*height*4];for(var i=0;i<pixels.Length;i+=4){pixels[i]=40;pixels[i+1]=80;pixels[i+2]=150;pixels[i+3]=255;}Save(path,BitmapSource.Create(width,height,96,96,PixelFormats.Bgra32,null,pixels,width*4));return path;}
    private static string CreateLogo(string path){var pixels=new byte[80*30*4];for(var i=0;i<pixels.Length;i+=4){pixels[i]=20;pixels[i+1]=210;pixels[i+2]=240;pixels[i+3]=(byte)(i%255);}Save(path,BitmapSource.Create(80,30,96,96,PixelFormats.Bgra32,null,pixels,80*4));return path;}
    private static void Save(string path,BitmapSource bitmap){var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(path);encoder.Save(stream);}
    private static (int,int) ReadSize(string path){var decoder=BitmapDecoder.Create(new Uri(path),BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);return(decoder.Frames[0].PixelWidth,decoder.Frames[0].PixelHeight);}
    private sealed class TemporaryDirectory:IDisposable{public TemporaryDirectory(){Path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"PixelTart-Publishing",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path);}public string Path{get;}public string File(string name)=>System.IO.Path.Combine(Path,name);public void Dispose(){try{Directory.Delete(Path,true);}catch{}}}
}
