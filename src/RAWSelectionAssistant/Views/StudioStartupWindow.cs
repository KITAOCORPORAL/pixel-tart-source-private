using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
namespace RAWSelectionAssistant.Views;
public sealed class StudioStartupWindow : Window
{
    private readonly TextBlock _status=new(){Foreground=Brushes.LightGray,Text="初始化…",HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,12,0,0)};
    private readonly ParticleView _particles=new();
    public StudioStartupWindow()
    {
        Title="Kitao Studio 光镜";Width=520;Height=320;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;WindowStartupLocation=WindowStartupLocation.CenterScreen;Background=Brushes.Black;ShowInTaskbar=false;
        var root=new Grid();Content=root;root.Children.Add(_particles);
        var labels=new StackPanel{VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(24)};
        labels.Children.Add(new TextBlock{Text="Kitao Studio",FontSize=30,Foreground=Brushes.White,HorizontalAlignment=HorizontalAlignment.Center});
        labels.Children.Add(new TextBlock{Text="光镜",FontSize=18,Foreground=Brushes.White,HorizontalAlignment=HorizontalAlignment.Center});labels.Children.Add(_status);root.Children.Add(labels);
        Closed+=(_,_)=>_particles.Stop();
    }
    public void Report(string stage)=>_status.Text=stage switch { "04 Database and composition"=>"正在打开数据与服务…","05 Services and modules"=>"正在加载模块…","03 Settings"=>"正在读取设置…","06 Theme"=>"正在应用外观…","08 Workspace"=>"正在准备工作区…","09 Restore Session"=>"正在恢复会话…","07 MainWindow"=>"正在准备主窗口…",_=>"正在初始化…" };
    public void Complete()
    {
        _particles.Stop();
        if(!SystemParameters.ClientAreaAnimation || Environment.GetEnvironmentVariable("KITAO_REDUCE_MOTION")=="1"){Close();return;}
        var fade=new DoubleAnimation(0,TimeSpan.FromMilliseconds(150));fade.Completed+=(_,_)=>Close();BeginAnimation(OpacityProperty,fade);
    }
    private sealed class ParticleView : FrameworkElement
    {
        private readonly Stopwatch _clock=Stopwatch.StartNew();private bool _running;
        public ParticleView(){Loaded+=(_,_)=>{if(SystemParameters.ClientAreaAnimation&&Environment.GetEnvironmentVariable("KITAO_REDUCE_MOTION")!="1"){CompositionTarget.Rendering+=Frame;_running=true;}};Unloaded+=(_,_)=>Stop();}
        private void Frame(object? sender,EventArgs e)=>InvalidateVisual();
        public void Stop(){if(_running)CompositionTarget.Rendering-=Frame;_running=false;_clock.Stop();}
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);var t=_running?_clock.Elapsed.TotalSeconds:0;
            for(var i=0;i<48;i++)
            {
                var angle=i*2.399963;var radius=25+70*(.5+.5*Math.Sin(i*1.7+t*.18));
                var x=ActualWidth/2+Math.Cos(angle+t*.08)*radius;var y=100+Math.Sin(angle+t*.08)*radius*.65;
                var geometry=new StreamGeometry();using(var c=geometry.Open()){c.BeginFigure(new Point(x,y-4),true,true);c.LineTo(new Point(x-4,y+3),true,false);c.LineTo(new Point(x+4,y+3),true,false);}
                dc.DrawGeometry(new SolidColorBrush(Color.FromRgb((byte)(80+i*43%175),(byte)(80+i*79%175),(byte)(80+i*113%175))),null,geometry);
            }
        }
    }
}
