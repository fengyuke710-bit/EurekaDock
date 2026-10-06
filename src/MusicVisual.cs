using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
namespace EurekaDock;
internal sealed class MusicAudio:IDisposable {
 [DllImport("EurekaDock.Native.dll")] static extern int MusicOpen(out IntPtr handle);
 [DllImport("EurekaDock.Native.dll")] static extern int MusicReadSpectrum(IntPtr handle,[Out] float[] bands);
 [DllImport("EurekaDock.Native.dll")] static extern int MusicHasOtherAudio(IntPtr handle);
 [DllImport("EurekaDock.Native.dll")] static extern void MusicClose(IntPtr handle);
 readonly ManualResetEvent stop=new(false);readonly Thread thread;
 readonly Action<float[]> consume;readonly Action<bool>? activity;int disposed;
 internal static string Status {get;private set;}="已关闭";
 internal static event Action? StatusChanged;
 static void SetStatus(string text){if(Status==text)return;Status=text;StatusChanged?.Invoke();}
 internal MusicAudio(Action<float[]> consumer,Action<bool>? activityChanged=null){consume=consumer;activity=activityChanged;thread=new Thread(Run){IsBackground=true,Name="EurekaDock system audio"};thread.Start();}
 void Run(){IntPtr handle=IntPtr.Zero;int previousError=0;var bands=new float[48];var sent=new float[48];long nextActivityCheck=0;
  try{while(!stop.WaitOne(0)){
   if(handle==IntPtr.Zero){int result=MusicOpen(out handle);if(result<0){SetStatus("等待可用播放设备（"+result.ToString("X8")+"）");if(previousError!=result){App.Store.Log("Music loopback: 0x"+result.ToString("X8"));previousError=result;}consume(new float[48]);if(stop.WaitOne(1500))break;continue;}SetStatus("已连接系统默认播放设备");previousError=0;}
   int hr=MusicReadSpectrum(handle,bands);if(hr<0){MusicClose(handle);handle=IntPtr.Zero;consume(new float[48]);activity?.Invoke(false);if(stop.WaitOne(500))break;continue;}bool changed=false;for(int i=0;i<48;i++)changed|=Math.Abs(bands[i]-sent[i])>.00001f;if(changed){consume((float[])bands.Clone());Array.Copy(bands,sent,48);}if(activity!=null&&Environment.TickCount64>=nextActivityCheck){int active=MusicHasOtherAudio(handle);if(active>=0)activity(active!=0);nextActivityCheck=Environment.TickCount64+250;}if(stop.WaitOne(16))break;
  }}catch(Exception ex){App.Store.Log("Music capture: "+ex);SetStatus("音频采集暂不可用，请重新开启");consume(new float[48]);}
  finally{if(handle!=IntPtr.Zero)MusicClose(handle);SetStatus("已关闭");}
 }
 public void Dispose(){if(Interlocked.Exchange(ref disposed,1)!=0)return;stop.Set();if(thread.Join(1500))stop.Dispose();}
}
internal sealed class MusicWindow:Window {
 readonly DockWindow dock;internal readonly MusicLine Line=new();internal readonly MusicBars Bars=new();readonly DispatcherTimer fullscreenTimer=new(){Interval=TimeSpan.FromMilliseconds(700)};MusicAudio? audio;MediaInfoReader? media;TrackInfo remembered=TrackInfo.Empty;bool suppressedTrackInfo,disposed;int otherAudioChecks;
 internal Func<bool> FullscreenCheck=FullscreenGuard.IsActive;
 internal bool FullscreenPaused {get;private set;}
 internal bool WorkersRunning=>audio!=null||media!=null;
 internal bool TrackInfoHidden=>suppressedTrackInfo;
 internal void SuppressForOtherAudio(){suppressedTrackInfo=true;remembered=TrackInfo.Empty;Bars.SetTrack(TrackInfo.Empty);Bars.SuppressTrackInfo(true);}
 internal void PrimeRevealTrack(TrackInfo value,bool hidden){
  if(disposed||!dock.Revealing||App.Store.Config.MusicStyle!="bars")return;
  suppressedTrackInfo=hidden;remembered=hidden?TrackInfo.Empty:value;Bars.SetStaticTrack(remembered,hidden);
 }
 public MusicWindow(DockWindow owner){dock=owner;Owner=owner;Content=Line;WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=null;ShowInTaskbar=false;ShowActivated=false;ResizeMode=ResizeMode.NoResize;IsHitTestVisible=false;Topmost=false;
  SourceInitialized+=(s,e)=>{var h=new WindowInteropHelper(this).Handle;Native.SetWindowLongPtr(h,-20,new IntPtr(Native.GetWindowLongPtr(h,-20).ToInt64()|0x08000000L|0x80L|0x20L));};
  fullscreenTimer.Tick+=(s,e)=>{if(IsVisible&&App.Store.Config.MusicEnabled){bool pause=FullscreenCheck();if(pause!=FullscreenPaused)Sync(true);}};
  Closed+=(s,e)=>{disposed=true;fullscreenTimer.Stop();audio?.Dispose();audio=null;media?.Dispose();media=null;Line.Stop();Bars.Stop();};
 }
 internal void Sync(bool visible){if(disposed)return;var c=App.Store.Config;
  if(!visible||!c.MusicEnabled){fullscreenTimer.Stop();FullscreenPaused=false;audio?.Dispose();audio=null;media?.Dispose();media=null;Line.Stop();Bars.Stop();Hide();return;}
  bool bars=c.MusicStyle=="bars";object view=bars?Bars:Line;if(Content!=view){Line.Stop();Bars.Stop();Content=view;}
  var dpi=VisualTreeHelper.GetDpi(dock);var screen=System.Windows.Forms.Screen.PrimaryScreen!.Bounds;double screenWidth=screen.Width/dpi.DpiScaleX,screenHeight=screen.Height/dpi.DpiScaleY;
  Width=Math.Clamp(screenWidth*c.WidthFraction*c.MusicLength+(bars?(Bars.CoverSize-Bars.BaseCoverSize)*1.5+14*(Bars.Scale-1):0),bars?Bars.CoverSize*1.5+100:32,screenWidth-8);Height=bars?Bars.CoverSize+12:c.MusicAmplitude*2.6+12;
  if(bars)Width+=2*Bars.HorizontalPadding;
  double baseline=bars?Height-6:Height-c.MusicAmplitude*.35-6;
  Left=screen.X/dpi.DpiScaleX+(screenWidth-Width)/2;Top=Math.Clamp(dock.MusicAnchorTop-c.MusicOffset-baseline,screen.Y/dpi.DpiScaleY,screen.Y/dpi.DpiScaleY+screenHeight-Height);
  if(dock.Revealing){
   Opacity=0;Show();fullscreenTimer.Stop();FullscreenPaused=false;
   audio?.Dispose();audio=null;media?.Dispose();media=null;Line.Suspend();Bars.Suspend();return;
  }
  Show();fullscreenTimer.Start();FullscreenPaused=FullscreenCheck();
  if(FullscreenPaused){audio?.Dispose();audio=null;media?.Dispose();media=null;Line.Suspend();Bars.Suspend();return;}
  if(bars){Bars.SuppressTrackInfo(suppressedTrackInfo);if(!suppressedTrackInfo&&!Bars.Track.Available&&remembered.Available)Bars.SetTrack(remembered);Bars.Refresh();media??=new MediaInfoReader(track=>{if(!disposed)Dispatcher.BeginInvoke(()=>{if(media!=null&&!disposed&&IsVisible&&App.Store.Config.MusicStyle=="bars"){if(track.Playing){suppressedTrackInfo=false;otherAudioChecks=0;Bars.SuppressTrackInfo(false);}if(suppressedTrackInfo)return;if(track.Available)remembered=track;Bars.SetTrack(track.Available?track:remembered);}});});}else{media?.Dispose();media=null;Line.Refresh();}
  audio??=new MusicAudio(bands=>{if(!disposed)Dispatcher.BeginInvoke(()=>{if(audio!=null&&!disposed&&IsVisible){if(App.Store.Config.MusicStyle=="bars")Bars.Feed(bands);else Line.Feed(bands);}});},active=>{if(!disposed)Dispatcher.BeginInvoke(()=>{if(audio==null||disposed||!IsVisible||App.Store.Config.MusicStyle!="bars")return;if(Bars.Track.Playing){otherAudioChecks=0;return;}otherAudioChecks=active?otherAudioChecks+1:0;if(otherAudioChecks<2||suppressedTrackInfo)return;SuppressForOtherAudio();});});
 }
}
internal sealed class MusicLine:FrameworkElement {
 readonly double[] target=new double[24],level=new double[24];readonly Stopwatch time=Stopwatch.StartNew();double last,phase;bool rendering;
 internal bool Animating=>rendering;internal double Energy {get{double sum=0;foreach(double x in level)sum+=x;return sum;}}
 internal double BandLevel(int index)=>level[index];
 internal void Feed(float[] incoming){
  // Rainmeter-style band envelopes: absolute level, short attack/decay, no peak
  // selection, normalization or layered moving lobes to hold the line up.
  bool changed=false;
  for(int i=0;i<24;i++){
   double power=(incoming[i*2]*incoming[i*2]+incoming[i*2+1]*incoming[i*2+1])*.5;
   double magnitude=Math.Max(0,Math.Sqrt(power)-.00035);
   double position=i/23.0,balance=Math.Clamp(position/.65,0,1),frequencyGain=1+.48*position*position*(3-2*position)+.12*balance*balance*(3-2*balance);
   double next=VolumeResponse(magnitude*frequencyGain,App.Store.Config.MusicSensitivity);
   changed|=Math.Abs(next-target[i])>.0001;target[i]=next;
  }
  if(changed||Energy>.001)Wake();
 }
 // Convex gain through the usable range: quiet changes gently, louder changes faster.
 internal static double VolumeResponse(double magnitude,double sensitivity){double x=Math.Clamp(magnitude*sensitivity*4,0,1);return 2.05*(.14*x+.86*x*x);}
 internal double Baseline=>ActualHeight-App.Store.Config.MusicAmplitude*.35-6;
 double Curve(double x){
  double p=x*25-1;int i=(int)Math.Floor(p);double t=p-i;
  double V(int index)=>index<0||index>=24?0:level[index];
  // Uniform cubic B-spline: continuous first and second derivatives at joins.
  return (V(i-1)*Math.Pow(1-t,3)+V(i)*(3*t*t*t-6*t*t+4)+V(i+1)*(-3*t*t*t+3*t*t+3*t+1)+V(i+2)*t*t*t)/6;
 }
 internal double SymmetricDisplacement(double distance){
  double d=Math.Clamp(Math.Abs(distance),0,1),source=.05+.95*d;
  double local=Math.Max(Math.Max(level[0],level[1]*.95),Math.Max(level[2]*.8,level[3]*.55)),largest=0;foreach(double v in level)largest=Math.Max(largest,v);
  double g=Math.Clamp((local-.15)/.45,0,1);g=g*g*(3-2*g);
  double otherScale=largest>0?1-g*(1-Math.Min(1,local/largest)):1;
  double surrounding=(Curve(source)-.055*(Curve(source-.065)+Curve(source+.065)))*otherScale;
  // Gather nearby bands into a rounded central crest. Remove their broad
  // shoulder before adding the narrower crest, rather than blending two
  // similarly high shapes into a flat cap. Both kernels have zero axis slope.
  double centerWeight=Math.Exp(-d*d/(2*.075*.075)),centerPeak=local;
  double gatheredWeight=Math.Exp(-d*d/(2*.15*.15));
  double edge=Math.Clamp((1-d)/.08,0,1);edge=edge*edge*(3-2*edge);
  return (surrounding*(1-.55*gatheredWeight)*(1-centerWeight)+centerPeak*centerWeight)*edge;
 }
 internal double Displacement(double x)=>SymmetricDisplacement((x-.5)*2);
 internal void Refresh(){InvalidateVisual();if(Energy>.001)Wake();}
 void Wake(){if(rendering)return;rendering=true;last=time.Elapsed.TotalSeconds;CompositionTarget.Rendering+=Frame;}
 void Frame(object? s,EventArgs e){double now=time.Elapsed.TotalSeconds,dt=Math.Clamp(now-last,0,.05);last=now;for(int i=0;i<24;i++){double rate=target[i]>level[i]?90:26;level[i]+=(target[i]-level[i])*(1-Math.Exp(-rate*dt));if(Math.Abs(level[i])<.0001&&target[i]==0)level[i]=0;}phase+=dt*1.5;InvalidateVisual();if(Energy<.001&&Array.TrueForAll(target,x=>x==0)){Array.Clear(level);CompositionTarget.Rendering-=Frame;rendering=false;InvalidateVisual();}}
 internal void Stop(){CompositionTarget.Rendering-=Frame;rendering=false;Array.Clear(target);Array.Clear(level);InvalidateVisual();}
 internal void Suspend(){CompositionTarget.Rendering-=Frame;rendering=false;}
 protected override void OnRender(DrawingContext dc){base.OnRender(dc);if(ActualWidth<2)return;double center=Baseline,amplitude=App.Store.Config.MusicAmplitude;
  var geometry=new StreamGeometry();using(var g=geometry.Open()){
   for(int i=0;i<=392;i++){double x=i/392.0;var point=new Point(x*ActualWidth,center-amplitude*Displacement(x));if(i==0)g.BeginFigure(point,false,false);else g.LineTo(point,true,false);}
  }geometry.Freeze();
  var brush=new LinearGradientBrush{StartPoint=new Point(0,0),EndPoint=new Point(1,0)};
  var palette=App.Store.Config.MusicColorMode=="custom"?App.Store.Config.MusicGradient.ConvertAll(WaveColors.Parse).ToArray():null;
  int samples=App.Store.Config.MusicColorMode=="original"?12:120;
  for(int i=0;i<=samples;i++){double x=i/(double)samples;var color=WaveColors.Sample(App.Store.Config,x*2-phase*.10,palette);color.A=(byte)(150+Math.Min(1,Energy)*70);brush.GradientStops.Add(new GradientStop(color,x));}brush.Freeze();
  dc.PushOpacity(.13);dc.DrawGeometry(null,new Pen(brush,6),geometry);dc.Pop();var pen=new Pen(brush,1.8){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};pen.Freeze();dc.DrawGeometry(null,pen,geometry);
 }
}








