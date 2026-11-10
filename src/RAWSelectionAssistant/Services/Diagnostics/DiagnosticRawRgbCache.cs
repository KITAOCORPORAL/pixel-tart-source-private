using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RAWSelectionAssistant.Core.Models;
namespace RAWSelectionAssistant.Services.Diagnostics;
public sealed class DiagnosticRawRgbCache(string root,Action<string,object>? trace=null)
{
 public sealed record Entry(int Version,string Key,string SourceSha,string DecoderIdentity,string OptionsSha,int Width,int Height,string PixelFile,string PixelSha,RawImageMetadata Metadata);
 private static readonly JsonSerializerOptions Json=new(){WriteIndented=true};
 public string Root {get;}=Path.GetFullPath(root);
 public static string Key(string sourceSha,string decoderIdentity,RawToJpegOptions options)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("DiagnosticRgb48CacheV1|"+sourceSha+"|"+decoderIdentity+"|"+JsonSerializer.Serialize(options)))).ToLowerInvariant();
 private string Owned(string name){if(name!=Path.GetFileName(name))throw new InvalidDataException("Cache path escaped root");var p=Path.GetFullPath(Path.Combine(Root,name));if(!p.StartsWith(Root.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Cache path escaped root");return p;}
 public string IndexPath(string key)=>Owned(key+".json");
 private static string OptionsHash(RawToJpegOptions o)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(o))));
 public RawDecodedImage? Read(string source,string identity,RawToJpegOptions options,CancellationToken token=default)
 {
  var key=Key(source,identity,options);var index=IndexPath(key);if(!File.Exists(index)){trace?.Invoke("CacheMiss",new{key,identity,reason="Absent"});return null;}
  try{
   if(new FileInfo(index).Length>65536)throw new InvalidDataException("Cache metadata exceeds64KiB");var bytes=File.ReadAllBytes(index);RejectDuplicateKeys(bytes);var e=JsonSerializer.Deserialize<Entry>(bytes)!;
   if(e.Version!=1||e.Key!=key||e.SourceSha!=source||e.DecoderIdentity!=identity||e.OptionsSha!=OptionsHash(options)||e.Width<1||e.Height<1||e.Width>30000||e.Height>30000||e.Metadata.ColorSpace!="sRGB"||e.Metadata.Orientation is <1 or >8||!System.Text.RegularExpressions.Regex.IsMatch(e.PixelFile,"^[0-9a-f]{32}\\.rgb48$"))throw new InvalidDataException("Cache identity/schema mismatch");
   var file=Owned(e.PixelFile);var count=checked(e.Width*e.Height*3);if(new FileInfo(file).Length!=checked((long)count*2))throw new InvalidDataException("Cache length mismatch");if((long)count*2>268435456)throw new InvalidDataException("Cache RGB48 exceeds master admission");var pixels=new ushort[count];using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);using var f=File.OpenRead(file);var block=new byte[1048576];int offset=0;while(offset<count*2){token.ThrowIfCancellationRequested();var n=Math.Min(block.Length,count*2-offset);f.ReadExactly(block.AsSpan(0,n));hash.AppendData(block,0,n);Buffer.BlockCopy(block,0,pixels,offset,n);offset+=n;}if(Convert.ToHexString(hash.GetHashAndReset())!=e.PixelSha)throw new InvalidDataException("Cache pixel SHA mismatch");trace?.Invoke("CacheHit",new{key,identity,index,pixelSha=e.PixelSha});return RawDecodedImage.FromRgb48(e.Width,e.Height,e.Width*6,pixels,e.Metadata);
  }catch(OperationCanceledException){throw;}catch(Exception ex)when(ex is IOException or JsonException or InvalidDataException or OverflowException or ArgumentException){trace?.Invoke("CacheRejected",new{key,identity,index,reason=ex.Message});return null;}
 }
 public void Write(string source,string identity,RawToJpegOptions options,RawDecodedImage image,CancellationToken token=default)
 {
  if(!image.IsProfessionalPrecision||image.Channels!=3||image.BitsPerChannel!=16||image.Rgb48Pixels!.Length!=checked(image.Width*image.Height*3))throw new InvalidDataException("Diagnostic cache accepts exact RGB48 only");Directory.CreateDirectory(Root);var key=Key(source,identity,options);var pixelName=Guid.NewGuid().ToString("N")+".rgb48";var file=Owned(pixelName);using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);using(var f=new FileStream(file,FileMode.CreateNew)){var span=System.Runtime.InteropServices.MemoryMarshal.AsBytes(image.Rgb48Pixels.AsSpan());for(int at=0;at<span.Length;at+=1048576){token.ThrowIfCancellationRequested();var block=span.Slice(at,Math.Min(1048576,span.Length-at));hash.AppendData(block);f.Write(block);}f.Flush(true);}
  var e=new Entry(1,key,source,identity,OptionsHash(options),image.Width,image.Height,pixelName,Convert.ToHexString(hash.GetHashAndReset()),image.Metadata);var temp=Owned(Guid.NewGuid().ToString("N")+".json");File.WriteAllBytes(temp,JsonSerializer.SerializeToUtf8Bytes(e,Json));var index=IndexPath(key);if(File.Exists(index))File.Copy(index,Owned(key+".previous-"+Guid.NewGuid().ToString("N")+".json"),false);File.Move(temp,index,true);trace?.Invoke("CacheStored",new{key,identity,index,pixelSha=e.PixelSha});
 }
 public static void RejectDuplicateKeys(ReadOnlySpan<byte> bytes){var r=new Utf8JsonReader(bytes);var objects=new Stack<HashSet<string>>();while(r.Read()){if(r.TokenType==JsonTokenType.StartObject)objects.Push(new(StringComparer.Ordinal));else if(r.TokenType==JsonTokenType.EndObject)objects.Pop();else if(r.TokenType==JsonTokenType.PropertyName&&!objects.Peek().Add(r.GetString()!))throw new InvalidDataException("Duplicate JSON property");}}
}
