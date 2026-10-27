namespace RAWSelectionAssistant.Core.Services.Projects;
public static class StudioMaskCoordinates
{
    public static StudioMaskPoint? FromViewport(double x,double y,double left,double top,double width,double height,InstantPaperLayout? crop=null,double zoom=1,double centerX=.5,double centerY=.5)
    {
        if(!double.IsFinite(zoom+centerX+centerY)||zoom<=0)throw new ArgumentException("Invalid crop zoom/center");
        if(!double.IsFinite(x+y+left+top+width+height)||width<=0||height<=0||x<left||x>=left+width||y<top||y>=top+height)return null;var u=(x-left)/width;var v=(y-top)/height;
        if(crop is not{} layout)return new(u,v);var px=u*layout.Width;var py=v*layout.Height;var sourceX=layout.SourceX+(px-layout.Left)/layout.Scale;var sourceY=layout.SourceY+(py-layout.Top)/layout.Scale;
        if(px<layout.Left||py<layout.Top||px>=layout.Left+layout.WindowWidth||py>=layout.Top+layout.WindowHeight)return null;
        sourceX=centerX*layout.SourceWidth+(sourceX-centerX*layout.SourceWidth)/zoom;sourceY=centerY*layout.SourceHeight+(sourceY-centerY*layout.SourceHeight)/zoom;
        if(sourceX<0||sourceY<0||sourceX>=layout.SourceWidth||sourceY>=layout.SourceHeight)return null;return new(sourceX/layout.SourceWidth,sourceY/layout.SourceHeight);
    }
}
