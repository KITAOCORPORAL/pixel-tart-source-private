namespace RAWSelectionAssistant.Core.Services.Projects;
/// <summary>Bounded display overlay only. Exact V1 mask sampling; at most four row workers.</summary>
public static class StudioMaskRaster
{
 public static byte[] Alpha(StudioLocalMask mask,double aspect,int width,int height,CancellationToken token=default,Func<double,double,(double X,double Y,bool Valid)>? map=null)
 {
  if(width<1||height<1||width>768||height>768)throw new ArgumentOutOfRangeException(nameof(width));var sample=mask.Compile(aspect,token);var bytes=new byte[width*height*4];Parallel.For(0,height,new ParallelOptions{CancellationToken=token,MaxDegreeOfParallelism=4},y=>{for(int x=0;x<width;x++){var p=map?.Invoke((x+.5)/width,(y+.5)/height)??((x+.5)/width,(y+.5)/height,true);byte a=!p.Item3?(byte)0:(byte)Math.Clamp(Math.Round(sample(p.Item1,p.Item2)*128),0,128);int i=(y*width+x)*4;bytes[i+2]=bytes[i+3]=a;}});return bytes;
 }
}
