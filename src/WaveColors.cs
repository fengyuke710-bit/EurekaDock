using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace EurekaDock;
internal static class WaveColors{
 internal static Color Parse(string hex)=>(Color)ColorConverter.ConvertFromString(hex);
 internal static string Hex(Color c)=>$"#{c.R:X2}{c.G:X2}{c.B:X2}";
 internal static bool TryHex(string? value,out string hex){
  hex="";string text=value?.Trim()??"";if(text.StartsWith('#'))text=text[1..];
  if(text.Length!=6||!int.TryParse(text,System.Globalization.NumberStyles.HexNumber,System.Globalization.CultureInfo.InvariantCulture,out _))return false;
  hex="#"+text.ToUpperInvariant();return true;
 }
 internal static Color Hsv(double hue,double saturation,double value){
  hue=(hue%360+360)%360;double c=value*saturation,x=c*(1-Math.Abs(hue/60%2-1)),m=value-c;
  (double r,double g,double b)=hue switch{<60=>(c,x,0d),<120=>(x,c,0d),<180=>(0d,c,x),<240=>(0d,x,c),<300=>(x,0d,c),_=>(c,0d,x)};
  return Color.FromRgb((byte)Math.Round((r+m)*255),(byte)Math.Round((g+m)*255),(byte)Math.Round((b+m)*255));
 }
 internal static (double h,double s,double v) ToHsv(Color c){
  double r=c.R/255d,g=c.G/255d,b=c.B/255d,max=Math.Max(r,Math.Max(g,b)),min=Math.Min(r,Math.Min(g,b)),d=max-min;
  double h=d==0?0:max==r?60*((g-b)/d%6):max==g?60*((b-r)/d+2):60*((r-g)/d+4);
  return ((h+360)%360,max==0?0:d/max,max);
 }
 internal static Color Sample(Configuration c,double phase,Color[]? palette=null,bool bars=false){
  string mode=bars?c.BarColorMode:c.MusicColorMode,color=bars?c.BarColor:c.MusicColor;
  var gradient=bars?c.BarGradient:c.MusicGradient;
  if(mode=="solid")return Parse(color);
  if(mode=="original"){
   byte Channel(double center,double spread,double offset)=>(byte)Math.Clamp(center+spread*Math.Sin(phase*Math.PI*2+offset),0,255);
   return Color.FromRgb(Channel(170,22,0),Channel(183,16,2),Channel(200,20,4));
  }
  double p=(phase-Math.Floor(phase))*gradient.Count;int index=(int)p;double t=p-index;t=t*t*(3-2*t);
  Color a=palette?[index]??Parse(gradient[index]),b=palette?[(index+1)%gradient.Count]??Parse(gradient[(index+1)%gradient.Count]);
  byte Mix(byte x,byte y)=>(byte)Math.Round(x+(y-x)*t);
  return Color.FromRgb(Mix(a.R,b.R),Mix(a.G,b.G),Mix(a.B,b.B));
 }
}
internal sealed class ColorWheel:FrameworkElement{
 const int Pixels=200;static readonly BitmapSource disk=MakeDisk();
 double hue,saturation,value=1;
 internal event Action<Color>? Changed;
 internal Color Selected=>WaveColors.Hsv(hue,saturation,value);
 internal void SetColor(Color color){(hue,saturation,value)=WaveColors.ToHsv(color);InvalidateVisual();}
 internal void Brightness(double v){value=Math.Clamp(v,0,1);Changed?.Invoke(Selected);InvalidateVisual();}
 internal double Value=>value;
 internal ColorWheel(){Width=Height=200;Cursor=Cursors.Cross;Focusable=false;
  MouseLeftButtonDown+=(s,e)=>{CaptureMouse();Choose(e.GetPosition(this));e.Handled=true;};
  MouseMove+=(s,e)=>{if(IsMouseCaptured&&e.LeftButton==MouseButtonState.Pressed)Choose(e.GetPosition(this));};
  MouseLeftButtonUp+=(s,e)=>{if(IsMouseCaptured){Choose(e.GetPosition(this));ReleaseMouseCapture();e.Handled=true;}};
 }
 void Choose(Point p){double dx=p.X-100,dy=p.Y-100;hue=(Math.Atan2(dy,dx)*180/Math.PI+360)%360;saturation=Math.Clamp(Math.Sqrt(dx*dx+dy*dy)/96,0,1);Changed?.Invoke(Selected);InvalidateVisual();}
 static BitmapSource MakeDisk(){
  var pixels=new byte[Pixels*Pixels*4];
  for(int y=0;y<Pixels;y++)for(int x=0;x<Pixels;x++){double dx=x-99.5,dy=y-99.5,r=Math.Sqrt(dx*dx+dy*dy);if(r>96)continue;var c=WaveColors.Hsv(Math.Atan2(dy,dx)*180/Math.PI,r/96,1);int i=(y*Pixels+x)*4;pixels[i]=c.B;pixels[i+1]=c.G;pixels[i+2]=c.R;pixels[i+3]=255;}
  var bitmap=BitmapSource.Create(Pixels,Pixels,96,96,PixelFormats.Bgra32,null,pixels,Pixels*4);bitmap.Freeze();return bitmap;
 }
 protected override void OnRender(DrawingContext dc){
  dc.DrawImage(disk,new Rect(0,0,200,200));dc.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)((1-value)*255),0,0,0)),null,new Point(100,100),96,96);
  var point=new Point(100+96*saturation*Math.Cos(hue*Math.PI/180),100+96*saturation*Math.Sin(hue*Math.PI/180));
  dc.DrawEllipse(null,new Pen(Brushes.Black,3),point,5,5);dc.DrawEllipse(null,new Pen(Brushes.White,1.5),point,5,5);
 }
}
