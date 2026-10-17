using System.IO;using System.Reflection;using System.Text.Json;using System.Windows.Media;using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Services;using RAWSelectionAssistant.Core.Services.Export;using RAWSelectionAssistant.Core.Services.Projects;
internal static class InstantRasterAudit
{
 public static void Run(string input)
 {
  var report=Path.Combine(input,"RASTER_RESULTS.json");if(File.Exists(report))throw new IOException("Fresh result required");
  var rows=new List<object>();var method=typeof(StudioQuickExport).Assembly.GetType("RAWSelectionAssistant.Services.ProfessionalWicRowEncoder")!.GetMethod("WriteRows",BindingFlags.Static|BindingFlags.NonPublic)!;
  foreach(var file in Directory.EnumerateFiles(input,"*.private.tif",SearchOption.AllDirectories).Where(p=>!p.EndsWith("frozen.private.tif")))
  {
   TiffStreamValidation expected;using(var f=File.OpenRead(file))expected=TiffStreamValidator.Validate(f);
   foreach(bool png in new[]{true,false})
   {
    var output=file+(png?".png":".jpg");if(File.Exists(output))throw new IOException("Raster exists");var timer=System.Diagnostics.Stopwatch.StartNew();using var budget=StudioGlobalResources.ReserveWork(expected.Width*18L);
    using(var source=File.OpenRead(file))
    {
     var offset=source.Length-expected.PixelBytes;var rgb16=new byte[expected.Width*6];
     Action<int,byte[]> fill=(y,row)=>{source.Position=offset+(long)y*rgb16.Length;source.ReadExactly(rgb16);if(png)rgb16.CopyTo(row,0);else for(int x=0;x<expected.Width;x++)for(int c=0;c<3;c++)row[x*3+2-c]=(byte)Math.Clamp(Math.Round(System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(rgb16.AsSpan((x*3+c)*2))/257d),0,255);};
     method.Invoke(null,[output,expected.Width,expected.Height,95,fill,CancellationToken.None,png,true,96]);
    }
    using var stream=File.OpenRead(output);var frame=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat|BitmapCreateOptions.IgnoreColorProfile,BitmapCacheOption.OnDemand).Frames[0];
    if(frame.PixelWidth!=expected.Width||frame.PixelHeight!=expected.Height||frame.ColorContexts is not{Count:>0})throw new InvalidDataException("Raster geometry/profile");
    using(var profile=frame.ColorContexts[0].OpenProfileStream()){using var m=new MemoryStream();profile.CopyTo(m);if(!m.ToArray().SequenceEqual(expected.IccProfile))throw new InvalidDataException("ICC differs");}
    var bitmap=new FormatConvertedBitmap(frame,png?PixelFormats.Rgb48:PixelFormats.Rgb24,null,0);var actual=new byte[expected.Width*3*(png?2:1)];var wanted=new byte[expected.Width*6];long total=0;double squares=0;int max=0;
    using(var source=File.OpenRead(file))
    {source.Position=source.Length-expected.PixelBytes;for(int y=0;y<expected.Height;y++){source.ReadExactly(wanted);bitmap.CopyPixels(new System.Windows.Int32Rect(0,y,expected.Width,1),actual,actual.Length,0);for(int x=0;x<expected.Width*3;x++){int v=System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(wanted.AsSpan(x*2));int a=png?System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(actual.AsSpan(x*2)):actual[x];int b=png?v:(int)Math.Round(v/257d);int delta=Math.Abs(a-b);total+=delta;squares+=(double)delta*delta;max=Math.Max(max,delta);}}}
    double n=(double)expected.Width*expected.Height*3,mae=total/n,rmse=Math.Sqrt(squares/n);if(png&&max!=0||!png&&(mae>5||rmse>15))throw new InvalidDataException("Raster pixel gate failed");
    rows.Add(new{input=Path.GetFileName(Path.GetDirectoryName(file)),profile=Path.GetFileName(file),format=png?"PNG16":"JPEG95-444",width=expected.Width,height=expected.Height,iccExact=true,pixelMaximum=max,mae,rmse,ms=timer.Elapsed.TotalMilliseconds,scope="Research candidate streamed TIFF to same product WIC row encoder; not V3 product UI",outputBytes=new FileInfo(output).Length});
    File.WriteAllText(report,JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
   }
  }
 }
}
