using RAWSelectionAssistant.Core.Services.Color;
namespace RAWSelectionAssistant.Core.Services.Projects;
public enum CandidateSignalDomain { CameraLinear, SceneLinear, DisplayEncoded }
public sealed record CandidateInputContract(CandidateSignalDomain Domain,WorkingColorSpace Space,bool WhiteBalanceApplied,bool? HighlightsRendered,string Provenance);
public static class CandidateColorBoundary
{
    public static CandidateInputContract ExistingLibRawOutput(bool cameraWhiteBalance) => new(CandidateSignalDomain.DisplayEncoded,WorkingColorSpace.SRgb,true,null,cameraWhiteBalance?"LibRaw DcrawProcess camera WB + camera matrix + sRGB; highlight/gamma defaults not independently characterized":"LibRaw DcrawProcess auto WB + camera matrix + sRGB; highlight/gamma defaults not independently characterized");
    public static ColorVector3 ToWorking(ColorVector3 input,CandidateInputContract contract,ColorMatrix3x3? cameraToAces=null)
    {
        if(!double.IsFinite(input.X)||!double.IsFinite(input.Y)||!double.IsFinite(input.Z))throw new ArgumentException("颜色输入必须有限。");
        if(contract.Domain==CandidateSignalDomain.CameraLinear)
        {
            if(cameraToAces is null)throw new NotSupportedException("缺少相机输入配置；不得假设相机 RGB 为 sRGB。");
            return cameraToAces.Value.Transform(input);
        }
        if(contract.Domain==CandidateSignalDomain.DisplayEncoded)
        {
            if(contract.Space!=WorkingColorSpace.SRgb)throw new NotSupportedException("编码图像需要明确 ICC 转换。");
            double Decode(double x)=>x<=.04045?x/12.92:Math.Pow((x+.055)/1.055,2.4);
            return LinearRgbColorScience.Convert(new(Decode(input.X),Decode(input.Y),Decode(input.Z)),WorkingColorSpace.LinearSRgb,WorkingColorSpace.AcesCg);
        }
        return LinearRgbColorScience.Convert(input,contract.Space,WorkingColorSpace.AcesCg);
    }
    public static ColorVector3 ApplyRawWhiteBalance(ColorVector3 input,CandidateInputContract contract,ColorVector3 gains)
    {
        if(contract.WhiteBalanceApplied || contract.Domain==CandidateSignalDomain.DisplayEncoded)throw new InvalidOperationException("已应用白平衡或编码图像不能再次按 RAW 增益处理。");
        if(!double.IsFinite(gains.X)||!double.IsFinite(gains.Y)||!double.IsFinite(gains.Z)||gains.X<=0||gains.Y<=0||gains.Z<=0)throw new ArgumentException("白平衡增益无效。");
        return new(input.X*gains.X,input.Y*gains.Y,input.Z*gains.Z);
    }
    // Shared preview/export boundary. Hard clip is explicit baseline, not a professional tone mapper.
    public static ColorVector3 ToSrgbOutput(ColorVector3 aces)
    {
        var linear=LinearRgbColorScience.Convert(aces,WorkingColorSpace.AcesCg,WorkingColorSpace.LinearSRgb);
        double Encode(double x){x=Math.Clamp(x,0,1);return x<=.0031308?12.92*x:1.055*Math.Pow(x,1/2.4)-.055;}
        return new(Encode(linear.X),Encode(linear.Y),Encode(linear.Z));
    }
}
