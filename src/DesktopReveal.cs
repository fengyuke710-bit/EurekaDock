using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace EurekaDock;

// Scale the HWND bounds as well as the drawing: overshoot is never clipped by
// the old window rectangle, and all desktop pieces share one screen-space pivot.
internal sealed class DesktopReveal:IDisposable {
 [DllImport("user32.dll",SetLastError=true)]
 [return:MarshalAs(UnmanagedType.Bool)]
 static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int width,int height,uint flags);
 sealed class Piece {
  internal readonly Window Window;
  internal readonly FrameworkElement Content;
  readonly Canvas host=new();
  readonly ScaleTransform transform=new();
  readonly Transform oldTransform;
  readonly double oldWidth,oldHeight,oldOpacity;
  readonly bool oldHitTest;
  readonly IntPtr hwnd;
  readonly DpiScale dpi;
  internal readonly Rect Bounds;
  internal Piece(Window window){
   Window=window;window.UpdateLayout();Bounds=new Rect(window.Left,window.Top,window.Width,window.Height);
   hwnd=new WindowInteropHelper(window).Handle;dpi=VisualTreeHelper.GetDpi(window);
   Content=(FrameworkElement)window.Content;
   oldWidth=Content.Width;oldHeight=Content.Height;oldTransform=Content.RenderTransform;
   oldOpacity=window.Opacity;oldHitTest=window.IsHitTestVisible;
   window.Content=null;Content.Width=Bounds.Width;Content.Height=Bounds.Height;Content.RenderTransform=transform;
   host.Children.Add(Content);window.Content=host;window.IsHitTestVisible=false;
  }
  internal void Apply(Point pivot,double scale,double opacity){
   transform.ScaleX=transform.ScaleY=scale;
   double left=pivot.X+(Bounds.Left-pivot.X)*scale,top=pivot.Y+(Bounds.Top-pivot.Y)*scale;
   // Update all four native bounds together instead of issuing four nested
   // synchronous WPF resize/render passes per frame.
   SetWindowPos(hwnd,IntPtr.Zero,(int)Math.Round(left*dpi.DpiScaleX),(int)Math.Round(top*dpi.DpiScaleY),
    Math.Max(1,(int)Math.Round(Bounds.Width*scale*dpi.DpiScaleX)),Math.Max(1,(int)Math.Round(Bounds.Height*scale*dpi.DpiScaleY)),0x0214);
   Window.Opacity=opacity;
  }
  internal void Restore(){
   // WPF's Width/Height property may still equal its original requested value
   // after native resizing; explicitly restore the physical HWND bounds too.
   SetWindowPos(hwnd,IntPtr.Zero,(int)Math.Round(Bounds.Left*dpi.DpiScaleX),(int)Math.Round(Bounds.Top*dpi.DpiScaleY),
    (int)Math.Round(Bounds.Width*dpi.DpiScaleX),(int)Math.Round(Bounds.Height*dpi.DpiScaleY),0x0214);
   Window.Left=Bounds.Left;Window.Top=Bounds.Top;Window.Width=Bounds.Width;Window.Height=Bounds.Height;
   host.Children.Remove(Content);Window.Content=Content;
   Content.Width=oldWidth;Content.Height=oldHeight;Content.RenderTransform=oldTransform;
   Window.Opacity=oldOpacity==0?1:oldOpacity;Window.IsHitTestVisible=oldHitTest;
  }
 }
 readonly DockWindow dock;
 readonly List<Piece> pieces=new();
 readonly Stopwatch clock=new();
 readonly DispatcherTimer timer=new(DispatcherPriority.Normal){Interval=TimeSpan.FromMilliseconds(1000.0/120)};
 Point pivot;
 double last,velocity;
 bool updating;
 internal double Scale {get;private set;}=.02;
 internal bool Animating {get;private set;}
 internal DesktopReveal(DockWindow window){dock=window;timer.Tick+=Frame;}
 internal void Start(){
  if(Animating||!dock.IsVisible)return;dock.Revealing=true;
  pieces.Add(new Piece(dock));
  if(dock.Music?.IsVisible==true)pieces.Add(new Piece(dock.Music));
  var bounds=pieces[0].Bounds;
  pivot=new Point(bounds.Left+bounds.Width/2,bounds.Bottom-8);
  Scale=.02;velocity=0;last=0;Animating=true;clock.Restart();Apply();
  // Rendering also fires synchronously during HWND resize, so it is not a
  // reliable display-clock event for this short bounds animation.
  App.Store.Changed+=Cancel;timer.Start();
 }
 void Frame(object? s,EventArgs e){
  // Resizing a layered HWND can synchronously raise Rendering again.
  if(!Animating||updating)return;updating=true;
  try{
  double now=clock.Elapsed.TotalSeconds,dt=Math.Clamp(now-last,0,.05);last=now;
  int count=Math.Max(1,(int)Math.Ceiling(dt/.004));double step=dt/count;
  for(int i=0;i<count;i++){velocity+=(200*(1-Scale)-20*velocity)*step;Scale+=velocity*step;}
  Apply();
  if(Math.Abs(Scale-1)<.0005&&Math.Abs(velocity)<.01)Dispose();
  }finally{updating=false;}
 }
 void Apply(){
  double opacity=Math.Clamp((Scale-.02)/.45,0,1);
  for(int i=0;i<pieces.Count&&Animating;i++)pieces[i].Apply(pivot,Scale,opacity);
 }
 void Cancel()=>Dispose();
 public void Dispose(){
  Animating=false;
  timer.Stop();timer.Tick-=Frame;App.Store.Changed-=Cancel;
  foreach(var piece in pieces)piece.Restore();pieces.Clear();
  Animating=false;Scale=1;clock.Stop();dock.Revealing=false;
 }
}
