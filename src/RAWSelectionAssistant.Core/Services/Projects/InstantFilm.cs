using RAWSelectionAssistant.Core.Services.Color;
namespace RAWSelectionAssistant.Core.Services.Projects;

public sealed record InstantFilmProfile(string Id,string Name,string ChemicalGeneration,string Direction,string Evidence="CreativeApproximationV1");
public static class InstantFilmProfiles
{
    public static IReadOnlyList<InstantFilmProfile> Available {get;}=[new("itype-color","i-Type Color","ModernIntegralCreative","Color"),new("itype-bw","i-Type B&W","ModernIntegralCreative","BW"),new("600-color","600 Color","ModernIntegralCreative","Color"),new("600-bw","600 B&W","ModernIntegralCreative","BW"),new("sx70-color","SX-70 Color","ModernIntegralCreative","Color"),new("sx70-bw","SX-70 B&W","ModernIntegralCreative","BW"),new("reclaimed-blue600","Reclaimed Blue600","ReclaimedCreative","WhiteBlue"),new("blue-duochrome600","Blue Duochrome600","DuochromeCreative","BlackBlue")];
    public static IReadOnlyList<string> ResearchOnly {get;}=["reclaimed-green600","go","8x10","time-zero","spectra","type55","type665","type669"];
}
public sealed record InstantDefect(string Kind,double X,double Y,double Radius,double Strength,double Period=0);
public sealed record InstantRandomSnapshot(ulong RootSeed,string PrngVersion,string ProfileId,int ProfileVersion,string Tier,double ColorShift,double SpreadScale,double TextureScale,IReadOnlyList<InstantDefect> Geometry);
public sealed record InstantFilmDefinition(bool Enabled=false,string FilmId="itype-color",string ChemicalGeneration="ModernIntegralCreative",int ProfileVersion=1,int AlgorithmVersion=1,double Amount=1,bool PaperFrame=true,double Border=.04,double BottomBorder=.16,double Uneven=.15,double Unspread=.12,double Roller=.10,double ArtLightLeak=0,double EnvironmentCelsius=21,bool DiceEnabled=false,string Tier="稳定出片",InstantRandomSnapshot? Snapshot=null)
{
    public string PaperColor {get;init;}="Warm";
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool ImageEffectsDisabled {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public InstantPaperGeometry? PaperGeometry {get;init;}
    public InstantFilmDefinition Validate()
    {
        if(PaperColor is not("Warm" or "Neutral" or "Cool"))throw new InvalidDataException("未知纸框色彩。");
        if(AlgorithmVersion is not (1 or 2)||ProfileVersion!=1||InstantFilmProfiles.Available.FirstOrDefault(x=>x.Id==FilmId) is not {} profile||profile.ChemicalGeneration!=ChemicalGeneration)throw new InvalidDataException("未知即时胶片定义/世代；研究中相纸不可作为可用预设。");
        if(AlgorithmVersion==1&&(ImageEffectsDisabled||PaperGeometry is not null))throw new InvalidDataException("V2 controls require explicit migration.");
        PaperGeometry?.Validate();
        foreach(var value in new[]{Amount,Uneven,Unspread,Roller,ArtLightLeak})if(!double.IsFinite(value)||value is <0 or >1)throw new InvalidDataException("即时胶片参数越界。");
        if(!double.IsFinite(Border)||Border is <0 or >.2||!double.IsFinite(BottomBorder)||BottomBorder is <0 or >.4||!double.IsFinite(EnvironmentCelsius)||EnvironmentCelsius is <0 or >40||Tier is not("稳定出片" or "轻微偶然" or "实验胶片"))throw new InvalidDataException("相纸/温度/骰子参数无效。");
        if(DiceEnabled&&(Snapshot is null||Snapshot.PrngVersion!="SplitMix64SubstreamsV1"||Snapshot.ProfileId!=FilmId||Snapshot.ProfileVersion!=ProfileVersion||Snapshot.Tier!=Tier))throw new InvalidDataException("骰子必须有与相纸版本一致的冻结快照。");
        if(Snapshot is {} s&&(s.Geometry is null||s.Geometry.Count>32||!double.IsFinite(s.ColorShift)||Math.Abs(s.ColorShift)>.2||!double.IsFinite(s.SpreadScale)||s.SpreadScale is <0 or >2||!double.IsFinite(s.TextureScale)||s.TextureScale is <0 or >2||s.Geometry.Any(g=>g.Kind is not("Spread" or "Corner" or "Roller")||new[]{g.X,g.Y,g.Radius,g.Strength,g.Period}.Any(v=>!double.IsFinite(v))||g.X is <0 or >1||g.Y is <0 or >1||g.Radius is <=0 or >1||g.Strength is <0 or >1||g.Period is <0 or >1||(g.Kind=="Roller"&&g.Period<=0))))throw new InvalidDataException("随机快照几何非法。");
        return this;
    }
}
public static class InstantFilmRandom
{
    public static InstantRandomSnapshot Roll(InstantFilmDefinition definition,ulong seed)
    {
        definition=definition with{DiceEnabled=false,Snapshot=null};definition.Validate();var level=definition.Tier switch{"稳定出片"=>.25,"轻微偶然"=>.6,_=>1d};
        var color=new Generator(seed^0x434F4C4F52UL);var spread=new Generator(seed^0x535052454144UL);var roller=new Generator(seed^0x524F4C4C4552UL);var texture=new Generator(seed^0x54455854555245UL);
        var defects=new List<InstantDefect>();for(var i=0;i<3;i++)defects.Add(new("Spread",spread.Next(),spread.Next(),.1+.3*spread.Next(),level*(.2+.6*spread.Next())));
        defects.Add(new("Corner",spread.Next()<.5?0:1,spread.Next()<.5?0:1,.03+.12*spread.Next(),level));
        defects.Add(new("Roller",roller.Next(),roller.Next(),.005+.015*roller.Next(),level*.7,.06+.10*roller.Next()));
        return new(seed,"SplitMix64SubstreamsV1",definition.FilmId,definition.ProfileVersion,definition.Tier,(color.Next()-.5)*.06*level,.5+spread.Next()*level,.5+texture.Next()*level,defects);
    }
    private sealed class Generator(ulong seed){private ulong state=seed;public double Next(){state+=0x9E3779B97F4A7C15UL;var z=state;z=(z^(z>>30))*0xBF58476D1CE4E5B9UL;z=(z^(z>>27))*0x94D049BB133111EBUL;return((z^(z>>31))>>11)*(1d/9007199254740992d);}}
}
public static class InstantFilmPipeline
{
    // All inputs/outputs are display-referred sRGB float; RAW WB/camera conversion must have already run exactly once.
    // Pointwise analytic normalized geometry: halo=0, no neighboring arrays, identical across tiles and output scales.
    public static InstantFilmDefinition Freeze(InstantFilmDefinition definition)
    {
        definition.Validate();
        return definition with { Snapshot=definition.Snapshot is {} s ? s with { Geometry=Array.AsReadOnly(s.Geometry.ToArray()) } : null };
    }
    public static ColorVector3 Apply(ColorVector3 srgb,double x,double y,InstantFilmDefinition definition)
    {
        if(!definition.Enabled||definition.AlgorithmVersion==2&&definition.ImageEffectsDisabled)return srgb;
        var direction=definition.FilmId switch{"itype-bw" or "600-bw" or "sx70-bw"=>"BW","reclaimed-blue600"=>"WhiteBlue","blue-duochrome600"=>"BlackBlue",_=>"Color"};var l=.2126*srgb.X+.7152*srgb.Y+.0722*srgb.Z;
        var tone=Math.Clamp(.035+.93*(l*l*(3-2*l)),0,1);ColorVector3 value;
        if(direction=="BW")value=new(tone*.99,tone,tone*.98);
        else if(direction=="WhiteBlue")value=new(.10+.83*tone,.23+.72*tone,.48+.49*tone);
        else if(direction=="BlackBlue")value=new(.012+.12*tone,.018+.34*tone,.03+.77*tone);
        else{var warmth=definition.FilmId.StartsWith("sx70")?.025:.012;value=new(.04+.90*srgb.X+warmth,.035+.90*srgb.Y,.05+.86*srgb.Z-warmth);}
        var random=definition.DiceEnabled?definition.Snapshot:null;var variation=0d;var loss=0d;
        if(random is not null)foreach(var g in random.Geometry){var dx=x-g.X;var dy=y-g.Y;var d=dx*dx+dy*dy;if(g.Kind=="Spread")variation+=Math.Exp(-d/(g.Radius*g.Radius))*g.Strength*definition.Uneven*.12*random.SpreadScale;else if(g.Kind=="Corner")loss=Math.Max(loss,Math.Exp(-d/(g.Radius*g.Radius))*g.Strength*definition.Unspread);else{var phase=Math.Abs((y-g.Y)/g.Period-Math.Round((y-g.Y)/g.Period))*g.Period;loss+=Math.Exp(-(phase*phase+dx*dx)/(g.Radius*g.Radius))*g.Strength*definition.Roller;}}
        else{variation=definition.Uneven*.03*Math.Sin(9*x+2*y)*Math.Cos(7*y);loss=definition.Unspread*.15*Math.Exp(-(x*x+y*y)/.012)+definition.Roller*.03*Math.Pow(Math.Cos(80*y),16);}
        var texture=(random?.TextureScale??1)*.002*Math.Sin(x*1931)*Math.Sin(y*1777);var cold=(21-definition.EnvironmentCelsius)*.0007;var shift=random?.ColorShift??0;
        var leak=definition.ArtLightLeak*Math.Exp(-x*18)*.18;double Channel(double original,double styled,double tint)=>Math.Clamp(original+(styled-original)*definition.Amount+variation+texture+tint+loss*(.86-styled),0,1);
        return new(Channel(srgb.X,value.X,shift-cold+leak),Channel(srgb.Y,value.Y,0),Channel(srgb.Z,value.Z,-shift+cold));
    }
    public static (int Width,int Height,int Margin,int Bottom) Layout(int width,int height,InstantFilmDefinition? definition)
    {var layout=InstantPaperLayout.Create(width,height,definition);return(layout.Width,layout.Height,layout.Left,layout.Bottom);}
    public static ColorVector3 Paper(double x,double y,InstantFilmDefinition? definition=null)=>definition?.PaperColor switch{"Neutral"=>new(.94-.008*y,.94-.008*y,.94-.008*y),"Cool"=>new(.89-.008*y,.93-.008*x,.96),_=>new(.94-.015*y,.925-.008*x,.875+.01*y)};
}
