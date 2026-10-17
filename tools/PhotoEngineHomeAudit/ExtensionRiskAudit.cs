using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Kitao.InstantFilm.Research;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Services;

internal static class ExtensionRiskAudit
{
    public static async Task Run(string output)
    {
        if(Directory.Exists(output))throw new IOException("Fresh synthetic risk output required");
        Directory.CreateDirectory(output);
        var rows=new List<object>();
        var basis=new ProfessionalRawDefinition(5,"Rendered",InputConfiguration:"RenderedSrgbFloatV1",DisplayTransform:"RenderedSrgbIdentityV1");
        foreach(var dimensions in new[]{(Width:97,Height:73),(Width:769,Height:577)})
        {
            var (w,h)=dimensions;var data=new float[w*h*3];
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {
                int i=(y*w+x)*3;data[i]=x/(float)(w-1);data[i+1]=y/(float)(h-1);
                data[i+2]=(x+y)%7==0?1:(x/13+y/11)%2==0?.08f:.85f;
            }
            using var image=new HighBitDepthImageBuffer(w,h,data,"32f","sRGB",1);
            var hash=Convert.ToHexString(SHA256.HashData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(image.Rgb32.Span)));
            var master=new ProfessionalRawMaster(image,new("SyntheticRisk","Synthetic","Diagnostic",hash,basis.DecodeKey,[],[],[],65535,OutputSpace:"sRGB",WhiteBalanceApplied:false,CameraMatrixApplied:false,Gamma:"sRGB"));
            var second=Path.Combine(output,"second-"+w+".png");
            StudioQuickExport.Encode(BitmapSource.Create(71,53,96,96,PixelFormats.Rgb24,null,Enumerable.Range(0,71*53).SelectMany(i=>new[]{(byte)(i%256),(byte)(255-i%256),(byte)130}).ToArray(),71*3),second,second,StudioExportFormat.Png);
            SecondExposureDecoder.EnsureRegistered();
            var multiple=SecondExposureDecoder.Describe(second) with{Weight=.4,Zoom=1.23,CropX=.2,CropY=.8};
            var original=CandidateRecipe.Create("modern-itype-v3",20261017) with{Creative=new(Version:2),Paper=new(Version:3),Defects=new(Version:2,Geometry:new())};
            var cases=new Dictionary<string,CandidateRecipe>
            {
                ["off"]=original with{Amount=0},
                ["usual"]=original,
                ["womb-optics-emulsion"]=original with{Creative=new(Version:2,Stock:"Womb01351.Stock0"),Optics=new(DetailMix:.8,HighlightDiffusion:.7),Emulsion=new(Microstructure:.03,ExpiredAging:.7)},
                ["circle-zoom-rims"]=original with{Paper=new(Version:3,Window:"Circle",Zoom:1.37,WindowHighlight:1,WindowPressed:1)},
                ["reflection-highlights"]=original with{Paper=new(Version:3,Reflection:1,ReflectionAngle:35,ReflectionWidth:.15)},
                ["crystal-roller-preview"]=original with{Defects=new(Version:2,Kind:"Bands",Amount:1,Crystal:1,Geometry:new(CenterX:.35,CenterY:.7,AngleDegrees:17,Scale:.8))},
                ["bw-temperature-drift-defects"]=CandidateRecipe.Create("modern-bw-v3",20261017) with{Creative=new(Version:2,TemperatureAppearance:1,FrameDrift:1),Paper=new(Version:3),Defects=new(Version:2,Kind:"Fog",Amount:1,Leak:1,Geometry:new())},
                ["multiple-mix-film"]=original with{MultipleInput=multiple},
                ["multiple-sum-film"]=original with{MultipleInput=multiple with{Mode="ExposureSum",Weight=1}},
                ["extreme"]=original with{Response=original.Response with{ExposureEv=2,Toe=.3,Shoulder=2,ApparentDensity=2},Creative=new(Version:2,Stock:"Womb01351.Stock5",Fog:1,Fade:1,LensVignette:1,DevelopmentEdge:1),Paper=new(Version:3,Pattern:"Foil",Emboss:1,EdgeLine:1,Reflection:1,Zoom:4),Defects=new(Version:2,Kind:"Islands",Amount:1,Crystal:1,Leak:1,Geometry:new(CenterX:.1,CenterY:.9,AngleDegrees:90,Scale:4))}
            };
            foreach(var item in cases)
            {
                var watch=Stopwatch.StartNew();var recipe=item.Value.Freeze();var legacy=InstantFilmProfiles.Available.Single(x=>x.Id==recipe.Profile.LegacyId);
                var definition=basis with{InstantFilm=new(true,legacy.Id,legacy.ChemicalGeneration,AlgorithmVersion:3){V3=recipe}};
                var held=StudioGlobalResources.AccountedBytes;
                var folder=Path.Combine(output,w+"-"+item.Key);Directory.CreateDirectory(folder);
                using(var session=InstantV3ProductPipeline.Open(master,definition))
                using(var full=session.RenderRegion(0,0,session.Layout.Width,session.Layout.Height))
                using(var borrow=full.Borrow())
                using(var preview=session.RenderPreview(128))
                using(var pb=preview.Borrow())
                {
                    var pixels=borrow.Image.Rgb32.ToArray();if(pixels.Any(x=>!float.IsFinite(x)||x<0||x>1))throw new InvalidDataException("Non-finite/out-of-range risk output");
                    // NeutralBW applies to the photo, not the independently warm paper base.
                    if(recipe.Profile.Palette=="NeutralBW")for(int y=session.Layout.Top+2;y<session.Layout.Top+session.Layout.WindowHeight-2;y++)for(int x=session.Layout.Left+2;x<session.Layout.Left+session.Layout.WindowWidth-2;x++)
                    {int i=(y*session.Layout.Width+x)*3;if(pixels[i]!=pixels[i+1]||pixels[i]!=pixels[i+2])throw new InvalidDataException("BW photo neutrality");}
                    session.StreamStrips((top,strip)=>{for(int y=0;y<strip.Height;y++)for(int x=0;x<strip.Width*3;x++)if(pixels[(top+y)*strip.Width*3+x]!=strip.Rgb32.Span[y*strip.Width*3+x])throw new InvalidDataException("Strip boundary mismatch");},7);
                    void Save(HighBitDepthImageBuffer buffer,string name){var bitmap=RawDisplayBitmapAdapter.ToBitmap(buffer.ToVisualRgb24());var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(Path.Combine(folder,name));encoder.Save(stream);}
                    Save(borrow.Image,"full.png");Save(pb.Image,"preview.png");
                    var pixelHash=Convert.ToHexString(SHA256.HashData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(borrow.Image.Rgb32.Span)));
                    File.WriteAllText(Path.Combine(folder,"recipe.json"),JsonSerializer.Serialize(recipe,new JsonSerializerOptions{WriteIndented=true}));
                    rows.Add(new{caseId=item.Key,inputWidth=w,inputHeight=h,inputSha256=hash,recipe,fullWidth=borrow.Image.Width,fullHeight=borrow.Image.Height,pixelHash,stripHeight=7,stripExactlyMatches=true,previewEdge=128,milliseconds=watch.Elapsed.TotalMilliseconds,scope="Synthetic full-size diagnostic and exact strips; visual review separate; not physical film"});
                }
                if(StudioGlobalResources.AccountedBytes!=held)throw new InvalidDataException("Risk resources retained");
                if(item.Key.StartsWith("multiple-"))foreach(var format in new[]{StudioExportFormat.Tiff,StudioExportFormat.Png,StudioExportFormat.Jpeg})
                    await ProfessionalRawExport.WriteAsync(master,definition,Path.Combine(folder,"direct"+StudioQuickExport.Extension("x.png",format)),format);
                File.WriteAllText(Path.Combine(output,"RISK_RESULTS.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
            }
        }
    }
}
