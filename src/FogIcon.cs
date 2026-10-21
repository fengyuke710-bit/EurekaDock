using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace EurekaDock;

// Blur is prepared once. At the edges we interpolate premultiplied pixels and
// apply a viewport-anchored alpha curve to all four channels together.
internal sealed class FogIcon {
 internal readonly int Width,Height,Padding;
 readonly byte[][] levels=new byte[4][];
 internal readonly BitmapSource Sharp;
 readonly BitmapSource source;
 IconReflection? reflection;
 internal IconReflection Reflection=>reflection??=new IconReflection(source);
 internal FogIcon(BitmapSource source,double blurPixels) {
  this.source=source;
  var input=new FormatConvertedBitmap(source,PixelFormats.Pbgra32,null,0);
  Padding=(int)Math.Ceiling(blurPixels*3)+2;
  Width=input.PixelWidth+Padding*2;Height=input.PixelHeight+Padding*2;
  var original=new byte[input.PixelWidth*input.PixelHeight*4];input.CopyPixels(original,input.PixelWidth*4,0);
  levels[0]=new byte[Width*Height*4];
  for(int y=0;y<input.PixelHeight;y++)Buffer.BlockCopy(original,y*input.PixelWidth*4,levels[0],((y+Padding)*Width+Padding)*4,input.PixelWidth*4);
  // Keep RGB <= alpha even for malformed Shell/custom bitmap edges.
  for(int i=0;i<levels[0].Length;i+=4)for(int c=0;c<3;c++)levels[0][i+c]=Math.Min(levels[0][i+c],levels[0][i+3]);
  for(int i=1;i<4;i++)levels[i]=Blur(levels[0],Width,Height,blurPixels*i/3);
  Sharp=BitmapSource.Create(Width,Height,96,96,PixelFormats.Pbgra32,null,levels[0],Width*4);Sharp.Freeze();
 }
 internal static byte[] Blur(byte[] src,int width,int height,double sigma) {
  if(sigma<.1)return src;
  int radius=(int)Math.Ceiling(3*sigma);var weights=new double[radius*2+1];double sum=0;
  for(int k=-radius;k<=radius;k++){weights[k+radius]=Math.Exp(-k*k/(2*sigma*sigma));sum+=weights[k+radius];}
  for(int k=0;k<weights.Length;k++)weights[k]/=sum;
  var temp=new float[src.Length];var dst=new byte[src.Length];
  for(int y=0;y<height;y++)for(int x=0;x<width;x++)for(int c=0;c<4;c++) {
   double v=0;for(int k=-radius;k<=radius;k++)if(x+k>=0&&x+k<width)v+=src[(y*width+x+k)*4+c]*weights[k+radius];
   temp[(y*width+x)*4+c]=(float)v;
  }
  for(int y=0;y<height;y++)for(int x=0;x<width;x++)for(int c=0;c<4;c++) {
   double v=0;for(int k=-radius;k<=radius;k++)if(y+k>=0&&y+k<height)v+=temp[((y+k)*width+x)*4+c]*weights[k+radius];
   dst[(y*width+x)*4+c]=(byte)Math.Clamp(Math.Round(v),0,255);
  }
  return dst;
 }
 internal Rect Padded(Rect icon) {
  double sx=icon.Width/(Width-2*Padding),sy=icon.Height/(Height-2*Padding);
  return new Rect(icon.X-Padding*sx,icon.Y-Padding*sy,Width*sx,Height*sy);
 }
 internal static double Fade(double distance,double width) {
  if(width<=0)return 1;
  double t=Math.Clamp(distance/width,0,1);
  return t*t*t*(t*(t*6-15)+10);
 }
 internal void Compose(byte[] output,Rect rect,double viewport,double fadeWidth,double? rightWidth=null) {
  double center=(rect.Left+rect.Right)/2;
  double mist=1-Math.Min(Fade(center,fadeWidth),Fade(viewport-center,rightWidth??fadeWidth));
  double level=mist*3;int lower=Math.Min(2,(int)level);double blend=level-lower;
  var a=levels[lower];var b=levels[lower+1];
  for(int x=0;x<Width;x++) {
   double screenX=rect.Left+(x+.5)*rect.Width/Width;
   double opacity=Math.Min(Fade(screenX,fadeWidth),Fade(viewport-screenX,rightWidth??fadeWidth));
   for(int y=0;y<Height;y++) {
    int i=(y*Width+x)*4;
    for(int c=0;c<4;c++)output[i+c]=(byte)Math.Clamp(Math.Round((a[i+c]+(b[i+c]-a[i+c])*blend)*opacity),0,255);
   }
  }
 }
}
internal sealed class FogFrame {
 readonly FogIcon icon;
 readonly byte[] pixels;
 readonly WriteableBitmap bitmap;
 internal FogFrame(FogIcon icon) {this.icon=icon;pixels=new byte[icon.Width*icon.Height*4];bitmap=new WriteableBitmap(icon.Width,icon.Height,96,96,PixelFormats.Pbgra32,null);}
 internal bool Matches(FogIcon candidate)=>ReferenceEquals(icon,candidate);
 internal void Draw(DrawingContext dc,Rect rect,double viewport,double fade,double? rightFade=null) {
  icon.Compose(pixels,rect,viewport,fade,rightFade);
  bitmap.WritePixels(new Int32Rect(0,0,icon.Width,icon.Height),pixels,icon.Width*4,0);
  dc.DrawImage(bitmap,rect);
 }
}

