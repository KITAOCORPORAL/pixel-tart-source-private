using System.Windows;using System.Windows.Media;using System.Windows.Media.Imaging;using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Views;
public partial class PhotoGradePageView
{
 // Invert only the existing versioned encoded-sRGB relative white-balance control.
 // No RAW camera Kelvin or new image transform; one shared editor transaction.
 private void SampleR55WhiteBalance(Point point)
 {
  if(model is null)return;var editor=model.Editor;if(editor.ReadOnlyTarget||editor.SelectedAdjustmentNode is not{Type:ColorStudioNodeType.WhiteBalance,IsLocked:false} node||node.WideToolVersion==2){ToolHint.Text="请选择可编辑的兼容白平衡节点";ToolHint.Visibility=Visibility.Visible;return;}
  var mapped=ColorStudioSampleMapping.Map(point,PhotoViewport.RenderSize,editor.EffectiveViewMode,editor.SplitPosition,editor.SourceImage,editor.MatchedImage,PhotoViewport.State);if(mapped is not{} sample)return;var (image,x,y)=sample;var format=new FormatConvertedBitmap(image,PixelFormats.Rgb24,null,0);var data=new byte[3];format.CopyPixels(new Int32Rect(x,y,1,1),data,3,0);double Linear(byte value){double v=value/255d;return v<=.04045?v/12.92:Math.Pow((v+.055)/1.055,2.4);}var r=Linear(data[0]);var g=Linear(data[1]);var b=Linear(data[2]);if(Math.Min(r,Math.Min(g,b))<.001||Math.Max(data[0],Math.Max(data[1],data[2]))>=253){ToolHint.Text="取样过暗或已裁切，请选中性中间调";ToolHint.Visibility=Visibility.Visible;return;}
  var parameters=ColorStudioToolCatalog.GetParameters(ColorStudioNodeType.WhiteBalance);var t=parameters.Single(p=>p.Key=="temperature");var tint=parameters.Single(p=>p.Key=="tint");var warm=.5*Math.Log(b/r);var green=Math.Log(g/Math.Sqrt(r*b))/1.5;editor.BeginEditTransaction();editor.SetToolParameter(t,editor.ToolValue(t)+warm/.35*100);editor.SetToolParameter(tint,editor.ToolValue(tint)+green/.25*100);editor.CommitEditTransaction();activeTool="选择";ToolLabel.Text=activeTool;PhotoViewport.Cursor=System.Windows.Input.Cursors.Arrow;
 }
}
