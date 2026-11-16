using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
if(args.Length is not (2 or 4))throw new ArgumentException("Usage: CompareTiffs input.tif report.json [first.tif reopen.tif]");
var input=Probe(args[0]);
if(args.Length==2){Save(args[1],new{authority="Read-only WIC row probe; no RAW decoder references",input});return;}
var first=Probe(args[2]);var reopened=Probe(args[3]);
var checks=new Dictionary<string,bool>{
 ["allOutputStoredChannelsMatch"]=first.StoredPixelSha256==reopened.StoredPixelSha256&&first.Format==reopened.Format,
 ["allOutputRgb48RowsMatch"]=first.Rgb48PixelSha256==reopened.Rgb48PixelSha256,
 ["uneditedOutputMatchesInput"]=first.Rgb48PixelSha256==input.Rgb48PixelSha256,
 ["dimensionsMatch"]=first.Width==reopened.Width&&first.Height==reopened.Height&&first.Width==input.Width&&first.Height==input.Height,
 ["sixteenBitChannels"]=input.ChannelBits==16&&first.ChannelBits==16&&reopened.ChannelBits==16,
 ["orientationOne"]=input.Orientation==1&&first.Orientation==1&&reopened.Orientation==1,
 ["iccPreserved"]=JsonSerializer.Serialize(input.Icc)==JsonSerializer.Serialize(first.Icc)&&JsonSerializer.Serialize(first.Icc)==JsonSerializer.Serialize(reopened.Icc)
};
Save(args[1],new{status=checks.Values.All(x=>x)?"PASS_FULL_ROW_PIXEL_AND_METADATA_COMPARISON":"FAIL",authority="Actual files read row by row; hashes include every stored channel and normalized RGB48 sample; does not attest UI operations",checks,input,first,reopened});
if(checks.Values.Any(x=>!x))Environment.ExitCode=1;
static void Save(string path,object value)=>File.WriteAllText(path,JsonSerializer.Serialize(value,new JsonSerializerOptions{WriteIndented=true}));
static TiffProbe Probe(string path)
{
 using var file=File.OpenRead(path);var fileSha=Convert.ToHexString(SHA256.HashData(file));file.Position=0;
 var decoder=BitmapDecoder.Create(file,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.None);var frame=decoder.Frames[0];
 if(frame.Format!=PixelFormats.Rgb48&&frame.Format!=PixelFormats.Rgba64)throw new InvalidDataException("Expected RGB48 or RGBA64: "+frame.Format);
 int orientation=1;if(frame.Metadata is BitmapMetadata m&&m.ContainsQuery("/ifd/{ushort=274}"))orientation=Convert.ToInt32(m.GetQuery("/ifd/{ushort=274}"));
 var icc=(frame.ColorContexts??[]).Select(c=>{using var s=c.OpenProfileStream();using var b=new MemoryStream();s.CopyTo(b);return new Profile(b.Length,Convert.ToHexString(SHA256.HashData(b.ToArray())));}).ToArray();
 using var stored=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);using var rgb=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
 var storedStride=checked((frame.PixelWidth*frame.Format.BitsPerPixel+7)/8);var storedRow=new byte[storedStride];var converted=new FormatConvertedBitmap(frame,PixelFormats.Rgb48,null,0);var rgbRow=new byte[checked(frame.PixelWidth*6)];
 for(int y=0;y<frame.PixelHeight;y++){var rect=new Int32Rect(0,y,frame.PixelWidth,1);frame.CopyPixels(rect,storedRow,storedStride,0);stored.AppendData(storedRow);converted.CopyPixels(rect,rgbRow,rgbRow.Length,0);rgb.AppendData(rgbRow);}
 return new(Path.GetFullPath(path),fileSha,frame.PixelWidth,frame.PixelHeight,frame.Format.ToString(),frame.Format.BitsPerPixel,16,orientation,frame.PixelHeight,Convert.ToHexString(stored.GetHashAndReset()),Convert.ToHexString(rgb.GetHashAndReset()),icc);
}
record Profile(long Bytes,string Sha256);
record TiffProbe(string Path,string FileSha256,int Width,int Height,string Format,int BitsPerPixel,int ChannelBits,int Orientation,int RowsRead,string StoredPixelSha256,string Rgb48PixelSha256,Profile[] Icc);
