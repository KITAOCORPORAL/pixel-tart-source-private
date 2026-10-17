using System.IO;using System.Xml.Linq;using System.Windows;using System.Windows.Media;using System.Windows.Media.Imaging;using RAWSelectionAssistant.Views;
internal static class PhotoGradeVisualAudit
{
 public static void Run(string root,string output)
 {
  if(File.Exists(output))throw new IOException("New synthetic visual output");Exception? failure=null;var thread=new Thread(()=>
  {
   try{var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};var doc=XDocument.Load(Path.Combine(root,"src/RAWSelectionAssistant/App.xaml"));foreach(var attribute in doc.Descendants().Attributes("Source"))app.Resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(new Uri("/KitaoPhotoSelector;component/"+attribute.Value,UriKind.Relative)));
   var view=new PhotoGradePageView();view.Measure(new Size(1400,850));view.Arrange(new Rect(0,0,1400,850));view.UpdateLayout();var bitmap=new RenderTargetBitmap(1400,850,96,96,PixelFormats.Pbgra32);bitmap.Render(view);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);using var fileOut=File.Create(output);encoder.Save(fileOut);}catch(Exception e){failure=e;}
  });thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();if(failure is not null)throw failure;
 }
}
