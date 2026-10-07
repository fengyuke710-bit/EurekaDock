using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
namespace EurekaDock;
internal sealed class MusicBars:FrameworkElement{
 readonly double[] target=new double[36],level=new double[36];readonly Stopwatch time=Stopwatch.StartNew();bool rendering;double last,angle,phase;
 TrackInfo track=TrackInfo.Empty,displayTrack=TrackInfo.Empty;bool suppressTrackInfo;double reveal=1,revealVelocity;
 readonly LinearGradientBrush?[] fills=new LinearGradientBrush?[36];
 readonly DrawingGroup scene=new(),albumLayer=new(),textLayer=new();
 readonly ScaleTransform albumScale=new(),textScale=new();
 readonly RotateTransform recordRotation=new();
 readonly RectangleGeometry?[] barShapes=new RectangleGeometry?[36];
 readonly GeometryDrawing?[] barDrawings=new GeometryDrawing?[36];
 Color[]? palette;bool sceneValid;double sceneWidth,sceneHeight;
 readonly SolidColorBrush titleBrush=new(),artistBrush=new();
 FormattedText? titleLayout,artistLayout;
 string layoutTitle="",layoutArtist="",layoutLanguage="";
 double layoutWidth,layoutTitleSize,layoutArtistSize,layoutDpi;
 ImageSource? cachedArtwork;ImageBrush? artworkBrush;
 static T Frozen<T>(T value) where T:Freezable{value.Freeze();return value;}
 internal double BaseCoverSize=>Math.Max(48,App.Store.Config.IconSize);
 internal double Scale=>App.Store.Config.MusicBarScale;
 internal double CoverSize=>BaseCoverSize*Scale;
 internal double HorizontalPadding=>(CoverSize*1.5+14*Scale)*.3+4;
 internal double Baseline=>ActualHeight-6;
 internal bool Animating=>rendering;
 internal double Angle=>angle;
 internal TrackInfo Track=>track;
 internal string TitleText=>displayTrack.Title.Trim();
 internal Color TrackTextColor=>WaveColors.Sample(App.Store.Config,-phase*.10,null,true);
 internal double BandLevel(int index)=>level[index];
 internal double BarHeight(int i)=>3*Scale+Math.Clamp(level[i]/2.05,0,1)*(CoverSize/2*(1+.5*(1-Math.Clamp(reveal,0,1)))-3*Scale);
 internal void SetTrack(TrackInfo value){track=value;if(value.Available&&displayTrack!=value){displayTrack=value;sceneValid=false;InvalidateVisual();}if(track.Available)Wake();}
 // Prime the frozen reveal without enrolling the record or waveform in rendering.
 internal void SetStaticTrack(TrackInfo value,bool hidden){track=displayTrack=value;suppressTrackInfo=hidden;reveal=hidden?0:1;revealVelocity=0;sceneValid=false;InvalidateVisual();}
 internal bool TrackInfoHidden=>suppressTrackInfo;
 internal double TrackReveal=>reveal;
 internal void SuppressTrackInfo(bool value){if(suppressTrackInfo==value)return;suppressTrackInfo=value;if(value&&Math.Abs(revealVelocity)<.02&&reveal>.99)revealVelocity=3.6;Wake();}
 internal void Feed(float[] incoming){bool changed=false;for(int i=0;i<36;i++){double start=i*48.0/36,end=(i+1)*48.0/36,power=0;for(int k=(int)start;k<Math.Ceiling(end);k++){double weight=Math.Max(0,Math.Min(end,k+1)-Math.Max(start,k));power+=incoming[k]*incoming[k]*weight;}double magnitude=Math.Max(0,Math.Sqrt(power/(end-start))-.00035);double position=i/35.0,balance=Math.Clamp(position/.65,0,1),frequencyGain=1+.55*position*position*(3-2*position)+.12*balance*balance*(3-2*balance);double next=VolumeResponse(magnitude*frequencyGain,App.Store.Config.MusicSensitivity);changed|=Math.Abs(next-target[i])>.0001;target[i]=next;}if(changed)Wake();}
 internal static double VolumeResponse(double magnitude,double sensitivity){double x=Math.Clamp(magnitude*sensitivity*4,0,1);return 2.05*(.19*x+.81*x*x);}
 internal void Refresh(){sceneValid=false;InvalidateVisual();if(track.Available||Math.Abs(reveal-(suppressTrackInfo?0:1))>.0001)Wake();}
 void Wake(){if(rendering)return;rendering=true;last=time.Elapsed.TotalSeconds;CompositionTarget.Rendering+=Frame;}
 void Frame(object? sender,EventArgs e){
  double now=time.Elapsed.TotalSeconds,dt=Math.Clamp(now-last,0,.05);last=now;bool moving=false;
  // Keep position and velocity across reversals; small integration steps make
  // the spring stable at both low and high display refresh rates.
  double goal=suppressTrackInfo?0:1;
  int steps=Math.Max(1,(int)Math.Ceiling(dt/.008));double step=dt/steps;
  for(int i=0;i<steps;i++){revealVelocity+=(200*(goal-reveal)-15*revealVelocity)*step;reveal+=revealVelocity*step;}
  if(Math.Abs(reveal-goal)<.0002&&Math.Abs(revealVelocity)<.002){reveal=goal;revealVelocity=0;}
  moving|=reveal!=goal||revealVelocity!=0;
  for(int i=0;i<36;i++){level[i]+=(target[i]-level[i])*(1-Math.Exp(-(target[i]>level[i]?90:26)*dt));if(Math.Abs(level[i]-target[i])<.0001)level[i]=target[i];moving|=level[i]!=target[i];}
  if(displayTrack.Available&&reveal>.001)angle=(angle+dt*9)%360;phase+=dt*1.5;
  if(sceneValid)UpdateScene();else InvalidateVisual();
  if(!moving&&(!track.Available||reveal==0)){CompositionTarget.Rendering-=Frame;rendering=false;}
 }
 internal void Stop(){CompositionTarget.Rendering-=Frame;rendering=false;Array.Clear(target);Array.Clear(level);track=displayTrack=TrackInfo.Empty;reveal=suppressTrackInfo?0:1;revealVelocity=0;scene.Children.Clear();albumLayer.Children.Clear();textLayer.Children.Clear();Array.Clear(barShapes);Array.Clear(barDrawings);Array.Clear(fills);cachedArtwork=null;artworkBrush=null;titleLayout=artistLayout=null;sceneValid=false;InvalidateVisual();}
 internal void Suspend(){CompositionTarget.Rendering-=Frame;rendering=false;}
 void Artwork(DrawingContext dc,Rect rect){
  if(!ReferenceEquals(cachedArtwork,displayTrack.Artwork)){cachedArtwork=displayTrack.Artwork;artworkBrush=cachedArtwork==null?null:Frozen(new ImageBrush(cachedArtwork){Stretch=Stretch.UniformToFill});}
  if(artworkBrush!=null)dc.DrawRectangle(artworkBrush,null,rect);
  else{dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(80,89,104),Color.FromRgb(41,48,62),90),null,rect);var pen=new Pen(new SolidColorBrush(Color.FromArgb(160,209,216,229)),1.5);dc.DrawEllipse(null,pen,new Point(rect.X+rect.Width/2,rect.Y+rect.Height/2),rect.Width*.16,rect.Height*.16);}
 }
 void TrackText(DrawingContext dc,double x,double y,double width,double titleSize,double artistSize,Color color){
  if(width<8)return;
  string title=TitleText.Length>0?TitleText:L.P("等待歌曲信息","Waiting for track info"),
   artist=displayTrack.DisplayArtist.Length>0?displayTrack.DisplayArtist:displayTrack.Available?L.P("未知作者","Unknown artist"):L.P("播放音乐后自动显示","Appears when your player shares it");
  double dpi=VisualTreeHelper.GetDpi(this).PixelsPerDip;string language=L.Culture.Name;
  if(titleLayout==null||layoutTitle!=title||layoutArtist!=artist||layoutWidth!=width||layoutTitleSize!=titleSize||layoutArtistSize!=artistSize||layoutDpi!=dpi||layoutLanguage!=language){
   layoutTitle=title;layoutArtist=artist;layoutWidth=width;layoutTitleSize=titleSize;layoutArtistSize=artistSize;layoutDpi=dpi;layoutLanguage=language;
   titleLayout=new FormattedText(title,L.Culture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),titleSize,titleBrush,dpi){MaxTextWidth=width,MaxLineCount=1,Trimming=TextTrimming.CharacterEllipsis};
   artistLayout=new FormattedText(artist,L.Culture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),artistSize,artistBrush,dpi){MaxTextWidth=width,MaxLineCount=1,Trimming=TextTrimming.CharacterEllipsis};
  }
  if(titleBrush.Color!=color)titleBrush.Color=color;
  var artistColor=Color.FromArgb(214,color.R,color.G,color.B);if(artistBrush.Color!=artistColor)artistBrush.Color=artistColor;
  dc.DrawText(titleLayout,new Point(x,y));dc.DrawText(artistLayout!,new Point(x,y+titleSize+3*Scale));
 }
 // Static content is recorded only for layout, metadata or settings changes.
 // The compositor receives per-frame property changes on the retained graph.
 protected override void OnRender(DrawingContext dc){
  base.OnRender(dc);if(ActualWidth<10||ActualHeight<10)return;
  if(!sceneValid||sceneWidth!=ActualWidth||sceneHeight!=ActualHeight)BuildScene();dc.DrawDrawing(scene);
 }
 void BuildScene(){
  double cover=CoverSize,top=Baseline-cover,discRadius=cover/2;
  scene.Children.Clear();scene.Transform=new TranslateTransform(HorizontalPadding,0);
  albumScale.CenterX=cover*.75;albumScale.CenterY=top+cover/2;albumLayer.Transform=albumScale;
  recordRotation.CenterX=cover;recordRotation.CenterY=top+cover/2;
  using(var dc=albumLayer.Open()){
   var center=new Point(cover,top+cover/2);dc.PushTransform(recordRotation);
   dc.DrawEllipse(new RadialGradientBrush(Color.FromRgb(45,46,49),Color.FromRgb(13,14,17)),new Pen(new SolidColorBrush(Color.FromArgb(100,125,130,140)),.8),center,discRadius,discRadius);
   for(int i=0;i<7;i++)dc.DrawEllipse(null,new Pen(new SolidColorBrush(Color.FromArgb(24,180,185,193)),.6),center,discRadius*(.42+i*.075),discRadius*(.42+i*.075));
   double label=cover*.45;dc.PushClip(new EllipseGeometry(center,label/2,label/2));Artwork(dc,new Rect(center.X-label/2,center.Y-label/2,label,label));dc.Pop();
   dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(17,18,21)),null,center,1.4,1.4);
   dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(60,183,190,204)),1),new Point(center.X+discRadius*.58,center.Y),new Point(center.X+discRadius*.9,center.Y));dc.Pop();
   dc.PushClip(new RectangleGeometry(new Rect(0,top,cover,cover),3,3));Artwork(dc,new Rect(0,top,cover,cover));dc.Pop();
   dc.DrawRoundedRectangle(null,new Pen(new SolidColorBrush(Color.FromArgb(80,225,230,239)),.8),new Rect(.4,top+.4,cover-.8,cover-.8),3,3);
  }
  scene.Children.Add(albumLayer);
  for(int i=0;i<36;i++){
   var shape=barShapes[i]=new RectangleGeometry();
   var drawing=barDrawings[i]=new GeometryDrawing(null,null,shape);scene.Children.Add(drawing);
  }
  double labelStart=cover*1.5+14*Scale;
  double titleSize=Math.Clamp(BaseCoverSize*.18,10,18)*Scale,artistSize=Math.Clamp(BaseCoverSize*.15,9,14)*Scale;
  textScale.CenterX=labelStart;textScale.CenterY=top+(titleSize+artistSize)/2;textLayer.Transform=textScale;
  using(var dc=textLayer.Open())TrackText(dc,labelStart,top,Math.Max(24,ActualWidth-2*HorizontalPadding-labelStart-2),titleSize,artistSize,TrackTextColor);
  scene.Children.Add(textLayer);
  palette=App.Store.Config.BarColorMode=="custom"?App.Store.Config.BarGradient.ConvertAll(WaveColors.Parse).ToArray():null;
  sceneWidth=ActualWidth;sceneHeight=ActualHeight;sceneValid=true;UpdateScene();
 }
 void UpdateScene(){
  double size=Math.Max(0,reveal),visible=Math.Clamp((size-.025)/.45,0,1);
  albumScale.ScaleX=albumScale.ScaleY=textScale.ScaleX=textScale.ScaleY=size;
  albumLayer.Opacity=textLayer.Opacity=visible;recordRotation.Angle=angle;
  double start=(CoverSize*1.5+14*Scale)*reveal,width=Math.Max(24,ActualWidth-2*HorizontalPadding-start-2),pitch=width/36,barWidth=Math.Max(1,pitch*.63);
  var config=App.Store.Config;
  for(int i=0;i<36;i++){
   var shape=barShapes[i]!;double height=BarHeight(i);
   var rect=new Rect(start+i*pitch+(pitch-barWidth)/2,Baseline-height,barWidth,height);
   if(shape.Rect!=rect)shape.Rect=rect;
   double radius=Math.Min(1.6,barWidth/3);if(shape.RadiusX!=radius)shape.RadiusX=radius;if(shape.RadiusY!=1.2)shape.RadiusY=1.2;
   var tint=WaveColors.Sample(config,i/18.0-phase*.10,palette,true);
   byte Blend(byte a,byte b,double fraction)=>(byte)Math.Round(a+(b-a)*fraction);
   var top=Color.FromArgb(235,Blend(tint.R,246,.38),Blend(tint.G,248,.38),Blend(tint.B,251,.38));
   var bottom=Color.FromArgb(185,Blend(tint.R,111,.28),Blend(tint.G,126,.28),Blend(tint.B,146,.28));
   var fill=fills[i];if(fill==null||fill.GradientStops[0].Color!=top||fill.GradientStops[1].Color!=bottom){
    fills[i]=fill=Frozen(new LinearGradientBrush(top,bottom,90));barDrawings[i]!.Brush=fill;
   }else if(barDrawings[i]!.Brush!=fill)barDrawings[i]!.Brush=fill;
  }
  var color=TrackTextColor;if(titleBrush.Color!=color)titleBrush.Color=color;
  var artistColor=Color.FromArgb(214,color.R,color.G,color.B);if(artistBrush.Color!=artistColor)artistBrush.Color=artistColor;
 }
}
