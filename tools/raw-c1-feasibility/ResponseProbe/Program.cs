using System.Text.Json;
using System.Security.Cryptography;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
using RAWSelectionAssistant.Core.Services.Projects;
using Sdcb.LibRaw;
if(args.Length<2)throw new ArgumentException("Usage: ResponseProbe samples.json new-output-directory");
var manifest=JsonDocument.Parse(File.ReadAllText(args[0]));var outDir=Path.GetFullPath(args[1]);
if(Directory.Exists(outDir))throw new IOException("Fresh output directory required (no overwrite)");
var samples=manifest.RootElement.GetProperty("samples").EnumerateArray().ToArray();
foreach(var s in samples){var source=Path.GetFullPath(s.GetProperty("path").GetString()!);var parent=Path.GetDirectoryName(source)!;if(outDir.Equals(parent,StringComparison.OrdinalIgnoreCase)||outDir.StartsWith(parent+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("Refuse outputs beside/below source");}
Directory.CreateDirectory(outDir);var results=new List<object>();
foreach(var s in samples){string path=s.GetProperty("path").GetString()!,id=s.GetProperty("sampleId").GetString()!;Console.WriteLine(id+" "+Path.GetFileName(path));var f=new FileInfo(path);var beforeSize=f.Length;var beforeTime=f.LastWriteTimeUtc;
 string hash;using(var stream=File.OpenRead(path))hash=Convert.ToHexString(SHA256.HashData(stream));
 string thumbnailStatus="not_requested";
 // Camera thumbnails are not used: neither full-depth measurements nor processed output.

 try{
 var master=await new ProfessionalRawDecoder().DecodeAsync(path,new());using(master.Image){
 var contracts=JsonSerializer.SerializeToElement(master.Contract);
 object? fullExport=null;
 if(args.Length==3){var profile=File.ReadAllBytes(args[2]);if(Convert.ToHexString(SHA256.HashData(profile))!="A92FBB73439E5F5D390F7ED96336CC399EE3285838F9D441645185230D1E7479")throw new InvalidDataException("Pilot requires frozen known sRGB profile; do not relabel output");var destination=Path.Combine(outDir,id+"-PV4-EV0-full-srgb.tif");var export=await RAWSelectionAssistant.Core.Services.Export.AtomicTiffWriter.WriteRowsAsync(destination,master.Image.Width,master.Image.Height,(y,row)=>ProfessionalRawPipeline.FillRgb16Row(master,new(),y,row),new(IccProfile:profile,Software:"Guangjing feasibility PV4 research export",Orientation:master.Image.Orientation),overwrite:false);fullExport=new{destination,export,warning="Production row math + core writer; not WPF application export; encoded sRGB not wide gamut"};}
 var cases=new List<object>();
 foreach(double ev in new[]{-2d,-1d,0d,1d,2d}){
  var definition=new ProfessionalRawDefinition(ExposureEv:ev);using var image=ProfessionalRawPipeline.RenderPreview(master,definition,720);
  var data=image.Rgb32.ToArray();var bytes=new byte[data.Length*2];long zeros=0,high=0;double sum=0;
  for(int i=0;i<data.Length;i++){ushort q=(ushort)Math.Clamp(Math.Round(data[i]*65535d),0,65535);System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(i*2),q);if(q==0)zeros++;if(q==65535)high++;sum+=data[i];}
  var name=id+"-EV"+ev.ToString("+0;-0;0",System.Globalization.CultureInfo.InvariantCulture)+".rgb16le";File.WriteAllBytes(Path.Combine(outDir,name),bytes);
  cases.Add(new{ev,width=image.Width,height=image.Height,orientation=image.Orientation,space="encoded sRGB after production PV4 display transform",file=name,sha256=Convert.ToHexString(SHA256.HashData(bytes)),meanEncodedChannel=sum/data.Length,zeroChannelFraction=zeros/(double)data.Length,maximumChannelFraction=high/(double)data.Length});
 }
 results.Add(new{id,path,bytes=beforeSize,sourceSha256=hash,sourceLastWriteUtc=beforeTime,sourceSizeAndMtimeUnchanged=new FileInfo(path).Length==beforeSize&&File.GetLastWriteTimeUtc(path)==beforeTime,status="decoded",thumbnailStatus,contract=contracts,fullExport,cases,warning="720px production preview, not full-size export/Capture One; aggregate encoded stats not color accuracy"});
 }
 }catch(Exception ex){results.Add(new{id,path,bytes=beforeSize,sourceSha256=hash,status="failed",thumbnailStatus,error=ex.ToString()});}
 File.WriteAllText(Path.Combine(outDir,"response.json"),JsonSerializer.Serialize(new{sourceManifest=Path.GetFullPath(args[0]),libraw=RawContext.Version,results},new JsonSerializerOptions{WriteIndented=true}));
 GC.Collect();
}
