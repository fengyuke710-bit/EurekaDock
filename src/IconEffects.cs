using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace EurekaDock;

internal static class IconEffects {
 internal const double BounceDuration=.9;
 internal static double Headroom(Configuration c,double dpi=1)=>c.IconClickFeedback=="bounce"?Math.Ceiling(c.IconSize*.65*dpi)/dpi:0;
 internal static double ReflectionSpace(Configuration c,double dpi=1)=>c.IconDisplayEffect=="reflection"?Math.Ceiling((c.IconSize*c.HoverScale*.52+4+(c.IconClickFeedback=="bounce"?c.IconSize*.25:0))*dpi)/dpi:0;
 internal static double Bounce(double elapsed){
  if(elapsed<0||elapsed>=BounceDuration)return 0;
  double duration,amplitude;
  if(elapsed<.42){duration=.42;amplitude=1;}
  else if(elapsed<.70){elapsed-=.42;duration=.28;amplitude=.38;}
  else{elapsed-=.70;duration=.20;amplitude=.12;}
  return amplitude*Math.Pow(Math.Max(0,Math.Sin(Math.PI*elapsed/duration)),1.4);
 }
 internal static Rect ReflectionBounds(Rect icon,double lift=0)=>new(icon.X-icon.Width*.11,icon.Bottom+2+lift*.45,icon.Width*1.22,icon.Height*.46);
}

// Perspective and progressive blur are baked once into premultiplied pixels.
// The upper edge is narrow; the lower edge widens towards the viewer. Reflections
// fade in all RGBA channels together, so saturated app colors keep their hue.
internal sealed class IconReflection {
 internal BitmapSource Image {get;}
 internal IconReflection(BitmapSource source){
  double ratio=Math.Min(1,96.0/Math.Max(source.PixelWidth,source.PixelHeight));
  BitmapSource scaled=ratio<1?new TransformedBitmap(source,new ScaleTransform(ratio,ratio)):source;
  var input=new FormatConvertedBitmap(scaled,PixelFormats.Pbgra32,null,0);
  int w=input.PixelWidth,h=input.PixelHeight;var original=new byte[w*h*4];input.CopyPixels(original,w*4,0);
  for(int i=0;i<original.Length;i+=4)for(int c=0;c<3;c++)original[i+c]=Math.Min(original[i+c],original[i+3]);
  var levels=new byte[4][];levels[0]=original;
  for(int i=1;i<4;i++)levels[i]=FogIcon.Blur(original,w,h,w*(.007+.014*i));
  int width=(int)Math.Ceiling(w*1.22),height=Math.Max(2,(int)Math.Ceiling(h*.46));var pixels=new byte[width*height*4];
  double Sample(byte[] data,double x,double y,int channel){
   int x0=(int)Math.Floor(x),y0=(int)Math.Floor(y);double tx=x-x0,ty=y-y0;
   double P(int px,int py)=>px<0||px>=w||py<0||py>=h?0:data[(py*w+px)*4+channel];
   return (P(x0,y0)*(1-tx)+P(x0+1,y0)*tx)*(1-ty)+(P(x0,y0+1)*(1-tx)+P(x0+1,y0+1)*tx)*ty;
  }
  for(int y=0;y<height;y++){
   double t=y/(double)(height-1),rowWidth=w*(1+.18*t),v=(1-t/(1.25-.25*t))*(h-1),blur=t*3,alpha=.34*Math.Pow(1-t,1.8);
   int lower=Math.Min(2,(int)blur);double mix=blur-lower;
   for(int x=0;x<width;x++){
    double u=((x+.5-width/2.0)/rowWidth+.5)*w-.5;
    for(int c=0;c<4;c++){double a=Sample(levels[lower],u,v,c),b=Sample(levels[lower+1],u,v,c);pixels[(y*width+x)*4+c]=(byte)Math.Clamp(Math.Round((a+(b-a)*mix)*alpha),0,255);}
   }
  }
  Image=BitmapSource.Create(width,height,96,96,PixelFormats.Pbgra32,null,pixels,width*4);Image.Freeze();
 }
 internal void Draw(DrawingContext dc,Rect icon,double lift=0){
  dc.PushOpacity(Math.Clamp(1-lift/Math.Max(1,icon.Height)*.6,.35,1));
  dc.DrawImage(Image,IconEffects.ReflectionBounds(icon,lift));dc.Pop();
 }
}

// The Quick Panel drives this element from its existing frame loop. No separate
// timer/render subscription is held by a static icon.
internal sealed class ApplicationIcon:FrameworkElement {
 readonly ImageSource? source;readonly IconReflection? reflection;
 double lift;
 internal ApplicationIcon(ImageSource? value,double size,bool mirror){source=value;Width=Height=size;if(mirror&&value is BitmapSource bitmap)reflection=new IconReflection(bitmap);}
 internal void Animate(double amount){double next=amount*Width*.55;if(next==lift)return;lift=next;InvalidateVisual();}
 protected override void OnRender(DrawingContext dc){
  base.OnRender(dc);var rect=new Rect(0,-lift,Width,Height);
  reflection?.Draw(dc,new Rect(0,0,Width,Height),lift);
  if(source!=null)dc.DrawImage(source,rect);
 }
}
