using System.IO;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Services;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class CollageExportTests
{
    [TestMethod]
    public Task ExportJpg_UsesOriginalFilesAndPreservesSources() => RunSta(async () =>
    {
        using var temp=new TempDirectory();
        var sources=Enumerable.Range(1,4).Select(i=>CreatePng(temp.Combine($"source-{i}.png"),(byte)(i*45))).ToArray();
        var originals=sources.ToDictionary(x=>x,x=>File.ReadAllBytes(x));
        var project=Project(sources,"4-grid","JPG");
        var result=await new CollageExportService().ExportAsync(project,temp.Combine("collage.jpg"));
        Assert.IsTrue(File.Exists(result.OutputPath));
        Assert.IsGreaterThan(0,new FileInfo(result.OutputPath).Length);
        foreach(var source in sources)CollectionAssert.AreEqual(originals[source],File.ReadAllBytes(source));
    });

    [TestMethod]
    public Task ExportPng_SupportsTransparentBackground() => RunSta(async () =>
    {
        using var temp=new TempDirectory();
        var source=CreatePng(temp.Combine("source.png"),120);
        var project=Project([source],"2-left-right","PNG");
        project.Export.TransparentBackground=true;
        var result=await new CollageExportService().ExportAsync(project,temp.Combine("collage.png"));
        Assert.IsTrue(File.Exists(result.OutputPath));
        Assert.AreEqual(".png",Path.GetExtension(result.OutputPath));
    });

    [TestMethod]
    public Task ExportConflict_AutoNumbersAndDoesNotOverwrite() => RunSta(async () =>
    {
        using var temp=new TempDirectory();
        var source=CreatePng(temp.Combine("source.png"),180);
        var existing=temp.Combine("collage.jpg");
        await File.WriteAllBytesAsync(existing,[7,8,9]);
        var result=await new CollageExportService().ExportAsync(Project([source],"2-left-right","JPG"),existing);
        CollectionAssert.AreEqual(new byte[]{7,8,9},File.ReadAllBytes(existing));
        StringAssert.EndsWith(result.OutputPath,"collage_2.jpg");
    });

    private static CollageProject Project(IReadOnlyList<string> sources,string template,string format)
    {
        var project=new CollageProject{TemplateId=template};
        project.Export.Format=format;project.Export.PixelWidth=640;project.Export.PixelHeight=480;
        for(var i=0;i<sources.Count;i++)project.Images.Add(new CollageImageState{SourcePath=sources[i],SlotId=(i+1).ToString()});
        return project;
    }

    private static string CreatePng(string path,byte value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var pixels=Enumerable.Repeat(value,64*64*4).ToArray();
        var bitmap=BitmapSource.Create(64,64,96,96,PixelFormats.Bgra32,null,pixels,64*4);
        using var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));encoder.Save(stream);
        return path;
    }

    private static Task RunSta(Func<Task> action)
    {
        var completion=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread=new Thread(async()=>{try{await action();completion.SetResult();}catch(Exception ex){completion.SetException(ex);}});
        thread.SetApartmentState(ApartmentState.STA);thread.Start();return completion.Task;
    }

    private sealed class TempDirectory:IDisposable
    {
        public TempDirectory(){Path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"PixelTart.WpfTests",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path);}
        public string Path{get;} public string Combine(params string[] parts)=>System.IO.Path.Combine([Path,..parts]);
        public void Dispose(){try{Directory.Delete(Path,true);}catch{}}
    }
}
