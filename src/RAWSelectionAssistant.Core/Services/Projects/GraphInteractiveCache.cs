using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RAWSelectionAssistant.Core.Services.Color;
namespace RAWSelectionAssistant.Core.Services.Projects;
/// <summary>Only800-edge interactive RGB outputs. Existing leased cache owns <=8x800²xRGBf32
/// (<64MiB); dependency content keys include input/profile/geometry and upstream signatures.</summary>
public sealed class GraphInteractiveCache:IDisposable
{
 private readonly LeasedPreviewBufferCache buffers=new(8);public long Hits{get;private set;}public long Misses{get;private set;}
 public long Generation=>buffers.Generation;
 public static string Key(string context,ColorAdjustmentStackNode node,IEnumerable<string> parents)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(context+"|"+JsonSerializer.Serialize(node)+"|"+string.Join("|",parents))));
 public LeasedPreviewBufferCache.Lease? Acquire(string key){var value=buffers.Acquire(key);if(value is null)Misses++;else Hits++;return value;}
 public void Store(string key,HighBitDepthImageBuffer value,long generation){if(Math.Max(value.Width,value.Height)>800)return;var copy=new HighBitDepthImageBuffer(value.Width,value.Height,value.Rgb32.ToArray(),value.SourceBitDepth,value.WorkingColorSpace,value.Orientation,value.Metadata);using var held=buffers.StoreAndAcquire(key,copy,generation);}
 public void Clear()=>buffers.Clear();public void Dispose()=>buffers.Dispose();
}
