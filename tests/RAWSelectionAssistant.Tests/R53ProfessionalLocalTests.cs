using System.Text.Json;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
namespace RAWSelectionAssistant.Tests;
[TestClass]public sealed class R53ProfessionalLocalTests
{
 public static ProfessionalRawMaster Master(int w=24,int h=16,ushort orientation=1){var d=new ProfessionalRawDefinition();return new(new HighBitDepthImageBuffer(w,h,Enumerable.Range(0,w*h*3).Select(i=>.02f+i%29*.025f).ToArray(),"16",ProfessionalRawPipeline.WorkingSpace,orientation),new("test","test","synthetic",new string('a',64),d.DecodeKey,[1,1,1,1],[1,1,1,1],[1,0,0,0,0,1,0,0,0,0,1,0],65535));}
 static ColorAdjustmentStack Stack()=>new([new(Guid.NewGuid(),ColorStudioNodeType.BasicTone,"local",NumericParameters:new Dictionary<string,double>{{"exposure",.5}}){CompositeVersion=1,Opacity=.6,Blend=StudioLayerBlend.Screen,LocalMask=new([new([new(.2,.3),new(.7,.6)],.18,.5,.7,.9)])}],ProcessingVersion:2);
 [TestMethod]public void LegacyDefinitionUnchangedExplicitExtensionMatchesSharedSrgbStack()
 {
  var master=Master();var old=new ProfessionalRawDefinition();using var legacy=ProfessionalRawPipeline.Render(master,old);var d=old with{LocalEdits=new(Stack())};using var actual=ProfessionalRawPipeline.Render(master,d);var analysis=ColorStudioProcessingAnalysis.Create(legacy.ToVisualRgb24(),d.LocalEdits.Stack,null);using var expected=new ColorStudioRenderPipeline().Render(legacy,analysis,null,d.LocalEdits.Stack,captureNodeDiagnostics:false).ProcessingPixels!;for(int i=0;i<expected.Rgb32.Length;i++)Assert.AreEqual(expected.Rgb32.Span[i],actual.Rgb32.Span[i],1e-6);CollectionAssert.AreEqual(legacy.Rgb32.ToArray(),ProfessionalRawPipeline.Render(master,old).Rgb32.ToArray());Assert.IsFalse(JsonSerializer.Serialize(old).Contains("LocalEdits"));Assert.AreEqual(old.DecodeKey,d.DecodeKey);
  var frozen=d with{LocalEdits=d.LocalEdits.Freeze()};Assert.AreNotSame(d.LocalEdits.Stack.Nodes[0].LocalMask,frozen.LocalEdits.Stack.Nodes[0].LocalMask);var back=JsonSerializer.Deserialize<ProfessionalRawDefinition>(JsonSerializer.Serialize(d))!;CollectionAssert.AreEqual(actual.Rgb32.ToArray(),ProfessionalRawPipeline.Render(master,back).Rgb32.ToArray());
 }
 [TestMethod]public void AllExifOrientationsGeometryCropTurnAndRowsKeepSameSourcePosition()
 {
  for(ushort orientation=1;orientation<=8;orientation++){
   var master=Master(24,16,orientation);var old=new ProfessionalRawDefinition();var d=old with{LocalEdits=new(Stack()),Geometry=new(.125,.125,.75,.75,1,true)};using var full=ProfessionalRawPipeline.Render(master,d);var size=ProfessionalRawPipeline.OutputSize(master,d);Assert.AreEqual(size.Width,full.Width);Assert.AreEqual(1,full.Orientation);var bytes=new byte[full.Width*6];for(int y=0;y<full.Height;y++){ProfessionalRawPipeline.FillOutputRgb16Row(master,d,y,bytes);for(int x=0;x<full.Width*3;x++)Assert.AreEqual(full.ToRgb48()[y*full.Width*3+x],System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(x*2)));}
   var upright=ProfessionalRawPipeline.OrientedSize(master);using var uncropped=ProfessionalRawPipeline.Render(master,old with{LocalEdits=d.LocalEdits});for(int y=0;y<full.Height;y++)for(int x=0;x<full.Width;x++){var p=d.Geometry.Source((x+.5)/full.Width,(y+.5)/full.Height);int sx=Math.Min(upright.Width-1,(int)(p.X*upright.Width)),sy=Math.Min(upright.Height-1,(int)(p.Y*upright.Height));for(int c=0;c<3;c++)Assert.AreEqual(uncropped.Rgb32.Span[(sy*upright.Width+sx)*3+c],full.Rgb32.Span[(y*full.Width+x)*3+c],1e-6,"Same sampled source pixel independent of crop/turn");}
  }
 }
 [TestMethod]public void PreviewIsAreaAverageOfFullEffectsAndExtendedCompositeDomainIsExplicit()
 {
  var m=Master(60,40);var d=new ProfessionalRawDefinition{LocalEdits=new(Stack())};using var full=ProfessionalRawPipeline.Render(m,d);using var preview=ProfessionalRawPipeline.RenderPreview(m,d,30);for(int y=0;y<preview.Height;y++)for(int x=0;x<preview.Width;x++)for(int c=0;c<3;c++){double sum=0;for(int dy=0;dy<2;dy++)for(int dx=0;dx<2;dx++){var v=full.Rgb32.Span[((y*2+dy)*60+x*2+dx)*3+c];sum+=v<=.04045?v/12.92:Math.Pow((v+.055)/1.055,2.4);}sum/=4;var expected=sum<=.0031308?sum*12.92:1.055*Math.Pow(sum,1/2.4)-.055;Assert.AreEqual(expected,preview.Rgb32.Span[(y*preview.Width+x)*3+c],1e-6);}
  using var a=new HighBitDepthImageBuffer(1,1,new float[]{-.2f,.5f,2});using var b=new HighBitDepthImageBuffer(1,1,new float[]{-.5f,1.4f,3});var n=new ColorAdjustmentStackNode(Guid.NewGuid(),ColorStudioNodeType.BasicTone,"float"){CompositeVersion=1,Opacity=.5};using var result=StudioLayerComposite.Apply(a,b,n);Assert.IsTrue(result.Rgb32.Span[0]<0);Assert.IsTrue(result.Rgb32.Span[2]>1);Assert.ThrowsExactly<NotSupportedException>(()=>(d with{InstantFilm=new(true)}).Validate());
 }
 [TestMethod]public void BvhPreservesV1ExactOverlapEraseInverseSemantics()
 {
  var strokes=Enumerable.Range(0,12).Select(n=>new StudioMaskStroke(Enumerable.Range(0,100).Select(i=>new StudioMaskPoint(i/99d,.5+.3*Math.Sin(i*.1+n))).ToArray(),.08,.5,.6,.8,n%4==3)).ToArray();var mask=new StudioLocalMask(strokes,true);var sample=mask.Compile(1.5);for(int y=0;y<30;y++)for(int x=0;x<40;x++){double px=(x+.5)/40,py=(y+.5)/30;Assert.AreEqual(Brute(mask,px,py,1.5),sample(px,py),1e-12);}
 }
 static double Brute(StudioLocalMask mask,double x,double y,double aspect){double sx=aspect>=1?aspect:1,sy=aspect>=1?1:1/aspect,w=0;foreach(var s in mask.Strokes){double distance=double.MaxValue;for(int i=0;i<s.Points.Count;i++){var a=s.Points[Math.Max(0,i-1)];var b=s.Points[i];double ax=a.X*sx,ay=a.Y*sy,dx=(b.X-a.X)*sx,dy=(b.Y-a.Y)*sy,l=dx*dx+dy*dy,t=l==0?0:Math.Clamp(((x*sx-ax)*dx+(y*sy-ay)*dy)/l,0,1);distance=Math.Min(distance,Math.Sqrt(Math.Pow(x*sx-ax-t*dx,2)+Math.Pow(y*sy-ay-t*dy,2)));}if(distance>=s.Radius)continue;var inner=s.Radius*(1-s.Feather);var c=distance<=inner?1:(s.Radius-distance)/Math.Max(1e-12,s.Radius-inner);var a2=c*s.Flow;w=s.Erase?w*(1-a2):w+Math.Max(0,s.Density-w)*a2;}return mask.Inverted?1-w:w;}
}
