using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace EurekaDock;

// One fixed, noninteractive desktop HWND presents a frozen composite. Only its
// drawing transform changes; the real Dock/music geometry and workers stay idle.
internal sealed class DesktopReveal:IDisposable {
 readonly DockWindow dock;
 readonly Stopwatch clock=new();
 readonly ScaleTransform transform=new();
 Window? overlay;
 Image? image;
 internal double Scale {get;private set;}=.02;
 internal bool Animating {get;private set;}
 internal Rect Bounds=>overlay==null?Rect.Empty:new Rect(overlay.Left,overlay.Top,overlay.Width,overlay.Height);
 internal DesktopReveal(DockWindow window){dock=window;}

 static BitmapSource Capture(Window window){
  var content=(FrameworkElement)window.Content;content.UpdateLayout();
  var dpi=VisualTreeHelper.GetDpi(window);
  var bitmap=new RenderTargetBitmap(Math.Max(1,(int)Math.Ceiling(content.ActualWidth*dpi.DpiScaleX)),
   Math.Max(1,(int)Math.Ceiling(content.ActualHeight*dpi.DpiScaleY)),96*dpi.DpiScaleX,96*dpi.DpiScaleY,PixelFormats.Pbgra32);
  bitmap.Render(content);bitmap.Freeze();return bitmap;
 }
 internal void Start(){
  if(Animating||!dock.IsVisible)return;
  dock.BeginReveal();dock.UpdateLayout();
  var dockBounds=new Rect(dock.Left,dock.Top,dock.Width,dock.Height);
  Rect union=dockBounds;
  var music=dock.Music?.IsVisible==true?dock.Music:null;
  if(music!=null)union.Union(new Rect(music.Left,music.Top,music.Width,music.Height));
  var drawing=new DrawingGroup();
  using(var dc=drawing.Open()){
   dc.DrawImage(Capture(dock),new Rect(dock.Left-union.Left,dock.Top-union.Top,dock.Width,dock.Height));
   if(music!=null)dc.DrawImage(Capture(music),new Rect(music.Left-union.Left,music.Top-union.Top,music.Width,music.Height));
  }
  drawing.Freeze();var snapshot=new DrawingImage(drawing);snapshot.Freeze();
  double marginX=union.Width*.08+4,marginY=union.Height*.12+4;
  var host=new Canvas();
  image=new Image{Source=snapshot,Width=union.Width,Height=union.Height,Stretch=Stretch.Fill,RenderTransform=transform,IsHitTestVisible=false};
  transform.CenterX=dockBounds.Left+dockBounds.Width/2-union.Left;
  transform.CenterY=dockBounds.Bottom-8-union.Top;
  Canvas.SetLeft(image,marginX);Canvas.SetTop(image,marginY);host.Children.Add(image);
  overlay=new Window{Title="EurekaDock reveal",Owner=dock,WindowStyle=WindowStyle.None,AllowsTransparency=true,
   Background=Brushes.Transparent,ShowInTaskbar=false,ShowActivated=false,ResizeMode=ResizeMode.NoResize,
   IsHitTestVisible=false,Topmost=false,Left=union.Left-marginX,Top=union.Top-marginY,
   Width=union.Width+marginX*2,Height=union.Height+marginY*2,Content=host};
  overlay.SourceInitialized+=(s,e)=>{
   var hwnd=new WindowInteropHelper(overlay).Handle;
   Native.SetWindowLongPtr(hwnd,-20,new IntPtr(Native.GetWindowLongPtr(hwnd,-20).ToInt64()|0x08000000L|0x80L|0x20L));
  };
  Scale=.02;Animating=true;clock.Restart();Apply();overlay.Show();
  CompositionTarget.Rendering+=Frame;
 }
 void Frame(object? s,EventArgs e){
  if(!Animating)return;double seconds=clock.Elapsed.TotalSeconds;
  // Analytic spring keeps timing independent of refresh rate and frame skips.
  Scale=.02+.98*(1-Math.Exp(-12*seconds)*(Math.Cos(10*seconds)+1.2*Math.Sin(10*seconds)));
  Apply();if(seconds>=.75)Complete();
 }
 void Apply(){
  transform.ScaleX=transform.ScaleY=Scale;
  if(image!=null)image.Opacity=Math.Clamp((Scale-.02)/.45,0,1);
 }
 internal void Complete(){
  if(!Animating)return;
  Scale=1;Apply();Animating=false;CompositionTarget.Rendering-=Frame;
  if(!dock.IsClosing)dock.CompleteReveal();
  CloseOverlay();clock.Stop();
 }
 void CloseOverlay(){
  if(overlay!=null){overlay.Close();overlay.Content=null;overlay=null;}
  if(image!=null){image.Source=null;image=null;}
 }
 public void Dispose(){
  Animating=false;CompositionTarget.Rendering-=Frame;clock.Stop();CloseOverlay();
 }
}
