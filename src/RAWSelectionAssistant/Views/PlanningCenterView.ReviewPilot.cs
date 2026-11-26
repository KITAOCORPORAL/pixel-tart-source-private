#if PLANNING_REVIEW_PILOT
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Windows.Automation;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace RAWSelectionAssistant.Views;

public partial class PlanningCenterView
{
    sealed record ReviewBlock(Guid Id, string Kind, string Text, int Version, bool Removed, string? Hash);
    sealed record ReviewPage(Guid Project, Guid Document, long Revision, string Title, Guid[] Order, ReviewBlock[] Blocks);
    sealed record ReviewImage(string Mime,string Data);
    readonly JsonSerializerOptions reviewJson = new(JsonSerializerDefaults.Web);
    WebView2? reviewWeb;
    Button? nativeToggle;
    ReviewPage? nativePage;
    bool nativeOpen;
    sealed record ReviewLaunch(string Url, string PeerSha256, string Profile, string CredentialFile);
    internal async Task OpenReviewCopyAsync(string configuration)
    {
        var info = JsonSerializer.Deserialize<ReviewLaunch>(await File.ReadAllTextAsync(configuration), new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? throw new InvalidDataException("审稿配置无效");
        var uri = new Uri(info.Url); if (uri.Scheme != "https" || !uri.IsLoopback) throw new InvalidDataException("本机审稿入口需要 HTTPS 回环地址");
        var web = reviewWeb = new WebView2();
        DocumentListPane.Visibility=PlanningListSplitter.Visibility=PlanningInspectorPane.Visibility=PlanningInspectorSplitter.Visibility=ContentNavigationBorder.Visibility=Visibility.Collapsed;
        DocumentListColumn.Width=SectionOutlineColumn.Width=PlanningInspectorColumn.Width=new GridLength(0);
        PlanningLoadingOverlay.Visibility=Visibility.Collapsed;EditorHeader.Visibility=Visibility.Collapsed;
        DocumentScroll.Visibility=Visibility.Collapsed;
        var dock=new DockPanel(); Grid.SetColumnSpan(dock,3);Grid.SetRowSpan(dock,3);PlanningBodyGrid.Children.Add(dock);
        var strip=new DockPanel {Margin=new Thickness(12,4,12,4)};DockPanel.SetDock(strip,Dock.Top);dock.Children.Add(strip);
        nativeToggle=new Button {Content="原编辑器选区",ToolTip="用原策划正文、分镜卡或图片选区发起批注"};AutomationProperties.SetName(nativeToggle,"原编辑器选区");DockPanel.SetDock(nativeToggle,Dock.Right);strip.Children.Add(nativeToggle);
        strip.Children.Add(Text("光镜　/　策划中心　·　审稿试点",13,true));dock.Children.Add(web);
        var environment = await CoreWebView2Environment.CreateAsync(null,info.Profile);
        await web.EnsureCoreWebView2Async(environment);
        web.CoreWebView2.Settings.AreDevToolsEnabled=false; web.CoreWebView2.Settings.IsWebMessageEnabled=false;
        bool Same(string url) => Uri.TryCreate(url,UriKind.Absolute,out var target) && target.GetLeftPart(UriPartial.Authority)==uri.GetLeftPart(UriPartial.Authority);
        web.CoreWebView2.NavigationStarting+=(_,args)=> {if(!Same(args.Uri)) args.Cancel=true;};
        web.CoreWebView2.NewWindowRequested+=(_,args)=>args.Handled=true;
        web.CoreWebView2.ServerCertificateErrorDetected+=(_,args)=>
        {
            using var peer=X509Certificate2.CreateFromPem(args.ServerCertificate.ToPemEncoding());
            args.Action=Same(args.RequestUri) && DateTime.UtcNow>=peer.NotBefore.ToUniversalTime() && DateTime.UtcNow<=peer.NotAfter.ToUniversalTime() && string.Equals(peer.GetCertHashString(HashAlgorithmName.SHA256),info.PeerSha256,StringComparison.OrdinalIgnoreCase) ? CoreWebView2ServerCertificateErrorAction.AlwaysAllow : CoreWebView2ServerCertificateErrorAction.Cancel;
        };
        web.Source=new UriBuilder(uri){Fragment="code="+Uri.EscapeDataString((await File.ReadAllTextAsync(info.CredentialFile)).Trim())}.Uri;
        nativeToggle.Click+=async (_,_)=>{try{if(nativeOpen){ShowDiscussion();return;}var current=await web.ExecuteScriptAsync("window.reviewNative?.snapshot() ?? null");nativePage=JsonSerializer.Deserialize<ReviewPage>(current,reviewJson)??throw new InvalidOperationException("请先登录并打开策划");nativeOpen=true;nativeToggle.Content="返回讨论";web.Visibility=Visibility.Collapsed;DocumentScroll.Visibility=Visibility.Visible;Grid.SetRow(dock,0);Grid.SetRowSpan(dock,1);RenderNativePage();}catch(Exception ex){MessageBox.Show(Window.GetWindow(this),ex.Message,"原策划选区");}};
        void ShowDiscussion(){nativeOpen=false;DocumentScroll.Visibility=Visibility.Collapsed;web.Visibility=Visibility.Visible;Grid.SetRowSpan(dock,3);nativeToggle.Content="原编辑器选区";}
        async Task Discuss(object point){var snapshot=nativePage!;ShowDiscussion();var packet=JsonSerializer.Serialize(new{project=snapshot.Project,document=snapshot.Document,point},reviewJson);var result=await web.ExecuteScriptAsync($"window.reviewNative.anchor({packet})");if(result!="true")MessageBox.Show(Window.GetWindow(this),"原内容已变化，请重新选取位置。","刷新选区");}
        void RenderNativePage()
        {
            DocumentContent.Children.Clear();var data=nativePage!;Heading(DocumentContent,data.Title);DocumentContent.Children.Add(Text("从原稿选中文字、分镜或图片区域，进入右侧讨论。",13));
            foreach(var block in data.Blocks.Where(b=>!b.Removed&&b.Kind=="text"))
            {
                var editor=new PlanningRichEditor(null,block.Text,(_,_,_)=>{},[]);var box=editor.Children.OfType<RichTextBox>().Single();
                editor.Children.OfType<WrapPanel>().First().Visibility=Visibility.Collapsed;box.IsReadOnly=true;box.MinHeight=64;box.MaxHeight=140;box.IsReadOnlyCaretVisible=true;box.Document.PagePadding=new Thickness(6);
                var line=box.Document.Blocks.FirstBlock!;DocumentContent.Children.Add(editor);
                var annotate=Action("批注所选文字",async()=>{var quote=box.Selection.Text;if(quote.Length==0)return;var start=new TextRange(line.ContentStart,box.Selection.Start).Text.Length;await Discuss(new{target=block.Id,start,quote,version=block.Version});});annotate.Focusable=false;DocumentContent.Children.Add(annotate);
            }
            Heading(DocumentContent,"镜头清单与分镜");foreach(var shotId in data.Order){var block=data.Blocks.Single(b=>b.Id==shotId);var card=new PlanningShotRow{Child=Text($"{Array.IndexOf(data.Order,shotId)+1:00}　{block.Text}",17,true),Padding=new Thickness(12),Margin=new Thickness(0,3,0,3),Background=Brush("SurfaceSecondaryBrush"),Focusable=true};AutomationProperties.SetName(card,"批注分镜："+block.Text);async void Open(){await Discuss(new{target=block.Id,version=block.Version});}card.OpenRequested=Open;card.MouseLeftButtonUp+=(_,_)=>Open();card.KeyDown+=(_,args)=>{if(args.Key==Key.Enter)Open();};DocumentContent.Children.Add(card);}
            Heading(DocumentContent,"参考图 · 框选区域");foreach(var block in data.Blocks.Where(b=>b.Kind=="image"&&!b.Removed)){var tile=new PlanningReferenceTile();tile.Children.Add(Text(block.Text,15,true));var image=new Image{Width=350,Stretch=Stretch.Uniform};var surface=new Grid{HorizontalAlignment=HorizontalAlignment.Left};surface.Children.Add(image);tile.Children.Add(surface);DocumentContent.Children.Add(tile);_ = LoadImageAsync();
                async Task LoadImageAsync(){try{var bytes=await NativeImageAsync(block.Id);if(nativePage!=data||!nativeOpen)return;using var stream=new MemoryStream(bytes);var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.DecodePixelWidth=800;bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();image.Source=bitmap;}catch(Exception ex){tile.Children.Add(Text(ex.Message));}}
                Point? start=null;var outline=new System.Windows.Shapes.Rectangle{Stroke=Brushes.Gold,StrokeThickness=2,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,IsHitTestVisible=false,Visibility=Visibility.Collapsed};surface.Children.Add(outline);
                surface.PreviewMouseLeftButtonDown+=(_,args)=>{if(image.Source==null)return;start=args.GetPosition(surface);surface.CaptureMouse();args.Handled=true;};
                double[] Rect(Point point){var origin=start!.Value;double x=Math.Clamp(point.X,0,surface.ActualWidth),y=Math.Clamp(point.Y,0,surface.ActualHeight);return [Math.Min(origin.X,x),Math.Min(origin.Y,y),Math.Abs(origin.X-x),Math.Abs(origin.Y-y)];}
                surface.MouseMove+=(_,args)=>{if(start==null)return;var r=Rect(args.GetPosition(surface));outline.Margin=new Thickness(r[0],r[1],0,0);outline.Width=r[2];outline.Height=r[3];outline.Visibility=Visibility.Visible;};
                surface.PreviewMouseLeftButtonUp+=async(_,args)=>{if(start==null)return;var r=Rect(args.GetPosition(surface));start=null;surface.ReleaseMouseCapture();args.Handled=true;if(r[2]<3||r[3]<3)return;await Discuss(new{target=block.Id,hash=block.Hash,version=block.Version,rect=new[]{r[0]/surface.ActualWidth,r[1]/surface.ActualHeight,r[2]/surface.ActualWidth,r[3]/surface.ActualHeight}});};
            }
        }
        Window.GetWindow(this)!.Closed+=(_,_)=>web.Dispose();
    }
    async Task<byte[]> NativeImageAsync(Guid id)
    {
        var json=await reviewWeb!.CoreWebView2.CallDevToolsProtocolMethodAsync("Runtime.evaluate",JsonSerializer.Serialize(new {expression=$"window.reviewNative.photo('{id:D}')",awaitPromise=true,returnByValue=true},reviewJson));using var response=JsonDocument.Parse(json);
        var payload=response.RootElement.GetProperty("result").GetProperty("value").Deserialize<ReviewImage>(reviewJson)??throw new InvalidDataException("未取得项目图片");return Convert.FromBase64String(payload.Data);
    }
}
#endif
