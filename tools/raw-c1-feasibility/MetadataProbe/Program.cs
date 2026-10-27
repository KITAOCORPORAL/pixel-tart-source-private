using System.Text.Json;
using MetadataExtractor;
using Sdcb.LibRaw;
if(args.Length!=2)throw new ArgumentException("Usage: MetadataProbe explicit-RAW-root output.json");
var root=Path.GetFullPath(args[0]);var output=Path.GetFullPath(args[1]);
if(!System.IO.Directory.Exists(root)||Path.GetPathRoot(root)==root)throw new ArgumentException("Refuse absent root/whole volume");
if(output.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Output must not be in RAW root");
var extensions=new HashSet<string>(".arw .cr2 .cr3 .raf .nef .nrw .dng .orf .rw2 .pef .srw .x3f .3fr .mos .mrw .mdc .kdc .raw .srf .sr2 .tif .tiff".Split(' '),StringComparer.OrdinalIgnoreCase);
var rows=new List<object>();
// Bounded to named root and one camera subdirectory; no archives, junctions or symlinks.
var opts=new EnumerationOptions{RecurseSubdirectories=true,MaxRecursionDepth=1,AttributesToSkip=FileAttributes.ReparsePoint,IgnoreInaccessible=false};
foreach(var file in System.IO.Directory.EnumerateFiles(root,"*",opts).Where(f=>extensions.Contains(Path.GetExtension(f))).Order(StringComparer.OrdinalIgnoreCase)){
 var f=new FileInfo(file);string make="unknown",model="unknown",status="unknown",error="";int width=0,height=0;object? iso=null;uint? filters=null;int? colors=null;
 try{using var c=RawContext.OpenFile(file);make=c.ImageParams.Make.Trim();model=c.ImageParams.Model.Trim();width=c.RawWidth;height=c.RawHeight;filters=c.ImageParams.Filters;colors=c.ImageParams.Colors;var p=(object)c.ImageOtherParams;iso=p.GetType().GetField("IsoSpeed")?.GetValue(p)??p.GetType().GetProperty("IsoSpeed")?.GetValue(p);status="opened_no_unpack";}catch(Exception ex){status="open_failed";error=ex.GetType().Name+": "+ex.Message;}
 var tags=new List<object>();var compression=new List<string>();var bits=new List<string>();
 try{foreach(var d in ImageMetadataReader.ReadMetadata(file))foreach(var t in d.Tags){if(t.Name.Contains("Compression",StringComparison.OrdinalIgnoreCase))compression.Add(d.Name+"/"+t.Name+": "+t.Description);if(t.Name.Contains("Bits Per Sample",StringComparison.OrdinalIgnoreCase)||t.Name.Contains("Bit Depth",StringComparison.OrdinalIgnoreCase))bits.Add(d.Name+"/"+t.Name+": "+t.Description);if(new[]{"Make","Model","ISO Speed Ratings","Orientation","Compression","Bits Per Sample","Image Width","Image Height"}.Contains(t.Name))tags.Add(new{directory=d.Name,t.Type,t.Name,t.Description});}}catch(Exception ex){tags.Add(new{metadataError=ex.Message});}
 rows.Add(new{path=f.FullName,bytes=f.Length,modifiedUtc=f.LastWriteTimeUtc,make,model,metadataMake=tags.OfType<object>().Select(t=>JsonSerializer.SerializeToElement(t)).Where(t=>t.TryGetProperty("Name",out var n)&&n.GetString()=="Make").Select(t=>t.GetProperty("Description").GetString()).FirstOrDefault()??"unknown",metadataFullModel=tags.OfType<object>().Select(t=>JsonSerializer.SerializeToElement(t)).Where(t=>t.TryGetProperty("Name",out var n)&&n.GetString()=="Model").Select(t=>t.GetProperty("Description").GetString()).FirstOrDefault()??"unknown",rawType=Path.GetExtension(file).ToUpperInvariant(),rawCompression="unknown",compressionTagObservations=compression.Count==0?new[]{"unknown"}:compression.ToArray(),rawBitDepth="unknown",bitDepthTagsNotSensorAuthority=bits,iso=iso is null||Convert.ToDouble(iso)==0?"unknown":iso,rawWidth=width,rawHeight=height,filters,colors,status,error,tags});
}
System.IO.Directory.CreateDirectory(Path.GetDirectoryName(output)!);
File.WriteAllText(output,JsonSerializer.Serialize(new{root,scope="root plus one subdirectory; read metadata/open only; no unpack; no source write; no bulk hashing",libraw=RawContext.Version,rows},new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));Console.WriteLine($"Inventory {rows.Count}: {output}");
