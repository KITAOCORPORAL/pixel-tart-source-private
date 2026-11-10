using System.IO;
using System.Text.Json;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
using RAWSelectionAssistant.Services.Diagnostics;
namespace RAWSelectionAssistant.WpfTests;
[TestClass]public sealed class XTransStableDiagnosticTests
{
 private static RawDecodedImage Image(ushort value)=>RawDecodedImage.FromRgb48(4,3,24,Enumerable.Repeat(value,36).ToArray(),new("Synthetic","No RAW",null,1,"sRGB"));
 [TestMethod]public void CandidateAndDefaultCacheCannotShareEntriesAndOffRouteRetainsDefault()
 {
  var root=Path.Combine(Path.GetTempPath(),"xtrans-cache-"+Guid.NewGuid().ToString("N"));var cache=new DiagnosticRawRgbCache(root);var options=new RawToJpegOptions(AutoRotate:false,DecodeMode:RawDecodeMode.ProfessionalDecode);var source=new string('A',64);var candidate=XTransStableDiagnostic.CandidateId;var original="ExistingDefault/"+new string('B',64);
  cache.Write(source,candidate,options,Image(100));Assert.IsNull(cache.Read(source,original,options));cache.Write(source,original,options,Image(200));Assert.AreEqual((ushort)200,cache.Read(source,original,options)!.Rgb48Pixels![0]);Assert.AreEqual((ushort)100,cache.Read(source,candidate,options)!.Rgb48Pixels![0]);Assert.IsNull(cache.Read(new string('C',64),candidate,options));Assert.IsNull(cache.Read(source,candidate,options with{UseCameraWhiteBalance=false}));
 }
 [TestMethod]public void InvalidCacheIsRejectedAndReplacementPreservesHistoryWithoutOverwritingPayload()
 {
  var root=Path.Combine(Path.GetTempPath(),"xtrans-cache-invalid-"+Guid.NewGuid().ToString("N"));var trace=new List<string>();var c=new DiagnosticRawRgbCache(root,(k,_)=>trace.Add(k));var o=new RawToJpegOptions(AutoRotate:false,DecodeMode:RawDecodeMode.ProfessionalDecode);var hash=new string('A',64);var id=XTransStableDiagnostic.CandidateId;c.Write(hash,id,o,Image(321));var index=c.IndexPath(DiagnosticRawRgbCache.Key(hash,id,o));var original=File.ReadAllText(index);var e=JsonSerializer.Deserialize<DiagnosticRawRgbCache.Entry>(original)!;File.WriteAllText(index,JsonSerializer.Serialize(e with{PixelSha=new string('0',64)}));Assert.IsNull(c.Read(hash,id,o));CollectionAssert.Contains(trace,"CacheRejected");c.Write(hash,id,o,Image(321));Assert.AreEqual((ushort)321,c.Read(hash,id,o)!.Rgb48Pixels![0]);Assert.AreEqual(2,Directory.GetFiles(root,"*.rgb48").Length);Assert.AreEqual(1,Directory.GetFiles(root,"*.previous-*.json").Length);Assert.IsTrue(File.Exists(Path.Combine(root,e.PixelFile)));
 }
 [TestMethod]public void DuplicateKeysAndPathTraversalAreRejected()
 {
  Assert.Throws<InvalidDataException>(()=>DiagnosticRawRgbCache.RejectDuplicateKeys(System.Text.Encoding.UTF8.GetBytes("{\"key\":1,\"key\":2}")));var c=new DiagnosticRawRgbCache(Path.Combine(Path.GetTempPath(),"xtrans-owned-"+Guid.NewGuid().ToString("N")));Assert.Throws<InvalidDataException>(()=>c.IndexPath("../outside"));
 }
 [TestMethod]public async Task LegacyProjectRecipeRoundtripRemainsSchema1AndDiagnosticMarkerIsExplicit()
 {
  var root=Path.Combine(Path.GetTempPath(),"xtrans-recipe-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);var id=Guid.NewGuid();var stack=new ColorAdjustmentStack([new(Guid.NewGuid(),ColorStudioNodeType.Preset,"原图")]);var target=new ColorStudioSessionTarget(id,Path.Combine(root,"synthetic.raf"),null,true,null,stack,null);var doc=new ColorStudioSessionDocument([target],id);var file=Path.Combine(root,"old.ptstudio.json");await ColorStudioSessionStore.SaveAsync(file,doc);Assert.IsFalse(File.ReadAllText(file).Contains("DiagnosticRawDecoderIdentity"));var read=await ColorStudioSessionStore.LoadAsync(file);Assert.IsNull(read.DiagnosticRawDecoderIdentity);Assert.AreEqual(1,read.Version);Assert.AreEqual(doc.Targets[0].Stack!.ProcessingVersion,read.Targets[0].Stack!.ProcessingVersion);var tagged=file+".candidate";await ColorStudioSessionStore.SaveAsync(tagged,read with{DiagnosticRawDecoderIdentity=XTransStableDiagnostic.CandidateId});var restored=await ColorStudioSessionStore.LoadAsync(tagged);Assert.AreEqual(XTransStableDiagnostic.CandidateId,restored.DiagnosticRawDecoderIdentity);Assert.AreEqual(read.Targets[0].Stack!.ProcessingVersion,restored.Targets[0].Stack!.ProcessingVersion);
 }
 [TestMethod]public void FormalBuildCannotActivateCandidateFromEnvironmentAlone()
 {
  if(XTransStableDiagnostic.Compiled)return;var old=Environment.GetEnvironmentVariable("PIXEL_TART_XTRANS_STABLE_V1");try{Environment.SetEnvironmentVariable("PIXEL_TART_XTRANS_STABLE_V1","1");Assert.IsFalse(XTransStableDiagnostic.Enabled);Assert.IsInstanceOfType<LibRawDecoder>(XTransStableDiagnostic.Create());}finally{Environment.SetEnvironmentVariable("PIXEL_TART_XTRANS_STABLE_V1",old);}
 }
}
