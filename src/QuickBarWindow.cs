using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
namespace EurekaDock;
internal sealed class QuickBarWindow:Window {
 readonly DockWindow dock;
 readonly Border tray=new();
 readonly QuickMorph morph=new(){IsHitTestVisible=false};
 Point anchorLocal;Rect destination;
 readonly ScaleTransform scale=new();
 readonly PopSpring spring=new();
 readonly Stopwatch clock=Stopwatch.StartNew();
 readonly List<(HoverResponse response,ScaleTransform transform)> hovers=new();
 readonly List<ApplicationIcon> artwork=new();
 readonly Dictionary<int,double> feedback=new();
 System.Windows.Controls.Primitives.UniformGrid? iconGrid;
 Point? pointer;bool pressed;
 internal double IconScale(int index)=>hovers[index].transform.ScaleX;
 internal void HoverAt(Point? point){pointer=point;Wake();}
 double last;bool rendering,opened;
 IntPtr hook;
 MouseHook? callback;
 internal bool Opened=>opened;
 internal double Reveal=>spring.Value;
 internal bool Animating=>rendering;
 [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate IntPtr MouseHook(int code,IntPtr message,IntPtr data);
 [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int id,MouseHook callback,IntPtr module,uint thread);
 [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr handle);
 [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern IntPtr GetModuleHandle(string? name);
 public QuickBarWindow(DockWindow owner) {
  dock=owner;Owner=owner;WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=null;ShowInTaskbar=false;ShowActivated=false;ResizeMode=ResizeMode.NoResize;Topmost=false;
  var canvas=new Canvas();Content=canvas;canvas.Children.Add(morph);canvas.Children.Add(tray);tray.RenderTransform=scale;
  tray.CornerRadius=new CornerRadius(14);tray.BorderThickness=new Thickness(1);tray.BorderBrush=Brushes.Transparent;
  tray.Background=Brushes.Transparent;
  tray.MouseMove+=(s,e)=>{if(iconGrid!=null)HoverAt(Mouse.GetPosition(iconGrid));};
  tray.MouseLeave+=(s,e)=>HoverAt(null);
  tray.PreviewMouseLeftButtonDown+=(s,e)=>{pressed=true;Wake();};
  tray.PreviewMouseLeftButtonUp+=(s,e)=>{pressed=false;Wake();};
  tray.LostMouseCapture+=(s,e)=>{pressed=false;Wake();};
  SourceInitialized+=(s,e)=>{var h=new WindowInteropHelper(this).Handle;Native.SetWindowLongPtr(h,-20,new IntPtr(Native.GetWindowLongPtr(h,-20).ToInt64()|0x08000000L|0x80L));};
  Closed+=(s,e)=>{StopHook();CompositionTarget.Rendering-=Frame;rendering=false;};
 }
 internal void Toggle(){if(opened){Dismiss();return;}Open();}
 internal void Open() {
  var c=App.Store.Config;double pitch=c.IconSize+c.Gap;
  var dpi=VisualTreeHelper.GetDpi(dock);var screen=System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea;
  var work=new Rect(screen.X/dpi.DpiScaleX,screen.Y/dpi.DpiScaleY,screen.Width/dpi.DpiScaleX,screen.Height/dpi.DpiScaleY);
  var point=dock.Accessories.PointToScreen(new Point(dock.Accessories.DotX,dock.Accessories.DotY(2)));
  var anchor=new Point(point.X/dpi.DpiScaleX,point.Y/dpi.DpiScaleY);
  double headroom=IconEffects.Headroom(c,dpi.DpiScaleY),reflectionSpace=IconEffects.ReflectionSpace(c,dpi.DpiScaleY);
  double extent=c.IconSize*c.HoverScale*(c.IconDisplayEffect=="reflection"?1.22:1);
  double side=Math.Max(11,(extent-pitch)/2+2),above=Math.Max(11,c.IconSize*(c.HoverScale-1)-c.Gap/2+2)+headroom,below=11+reflectionSpace;
  double width=Math.Min(c.QuickColumns*pitch+side*2+2,work.Width-32),height=Math.Min(c.QuickRows*pitch+above+below+2,work.Height-32);
  double left=Math.Clamp(anchor.X-width-18,work.Left+8,work.Right-width-8),top=Math.Clamp(anchor.Y-height+8,work.Top+8,work.Bottom-height-8);
  // Include the anchor in the HWND, then grow the tray about that exact point.
  double margin=Math.Max(width,height)*.16+8;Left=Math.Min(left,anchor.X)-margin;Top=Math.Min(top,anchor.Y)-margin;
  Width=Math.Max(left+width,anchor.X+8)-Left+margin;Height=Math.Max(top+height,anchor.Y+8)-Top+margin;
  Canvas.SetLeft(tray,left-Left);Canvas.SetTop(tray,top-Top);tray.Width=width;tray.Height=height;
  anchorLocal=new Point(anchor.X-Left,anchor.Y-Top);destination=new Rect(left-Left,top-Top,width,height);
  morph.Width=Width;morph.Height=Height;scale.CenterX=scale.CenterY=0;
  hovers.Clear();artwork.Clear();feedback.Clear();pointer=null;pressed=false;
  var grid=new System.Windows.Controls.Primitives.UniformGrid{Columns=c.QuickColumns,Rows=c.QuickRows,Width=c.QuickColumns*pitch,Height=c.QuickRows*pitch};
  iconGrid=grid;
  var presenter=new FrameworkElementFactory(typeof(ContentPresenter));presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);presenter.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);
  var hitArea=new FrameworkElementFactory(typeof(Border));hitArea.SetValue(Border.BackgroundProperty,Brushes.Transparent);hitArea.AppendChild(presenter);
  var iconTemplate=new ControlTemplate(typeof(Button)){VisualTree=hitArea};
  var visible=c.QuickItems.Where(x=>!x.Hidden).Take(c.QuickColumns*c.QuickRows).ToList();
  for(int i=0;i<c.QuickColumns*c.QuickRows;i++) {
   var slot=new Grid{Width=pitch,Height=pitch};grid.Children.Add(slot);
   if(i>=visible.Count)continue;
   var item=visible[i];var icon=Native.Icon(item,(int)Math.Ceiling(c.IconSize*c.HoverScale*dpi.DpiScaleX));
   var zoom=new ScaleTransform(1,1);hovers.Add((new HoverResponse(),zoom));
   var view=new ApplicationIcon(icon,c.IconSize,c.IconDisplayEffect=="reflection"){RenderTransform=zoom,RenderTransformOrigin=new Point(.5,1),Opacity=item.Available?1:.4};artwork.Add(view);int index=artwork.Count-1;
   var button=new Button{Template=iconTemplate,FocusVisualStyle=null,Background=Brushes.Transparent,BorderThickness=new Thickness(0),Padding=new Thickness(0),ToolTip=item.ToString(),Content=view};
   button.Click+=(s,e)=>{if(c.IconClickFeedback=="bounce")feedback[index]=clock.Elapsed.TotalSeconds;Dismiss();dock.Launch(item);};slot.Children.Add(button);
  }
  var contents=new Grid();contents.Children.Add(grid);if(c.QuickItems.Count==0)contents.Children.Add(new TextBlock{Text=L.T("在设置 → 快捷栏中添加应用"),TextWrapping=TextWrapping.Wrap,TextAlignment=TextAlignment.Center,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(10),Foreground=Brushes.DarkSlateGray});
  var paddedContents=new Border{Padding=new Thickness(side,above,side,below),Child=contents};
  var viewport=new ScrollViewer{Content=paddedContents,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
  viewport.ScrollChanged+=(s,e)=>{if(tray.IsMouseOver&&iconGrid!=null)HoverAt(Mouse.GetPosition(iconGrid));};tray.Child=viewport;
  ApplyReveal();opened=true;Show();dock.Accessories.InvalidateVisual();StartHook();if(!opened)return;spring.SetTarget(1);Wake();
 }
 void StartHook(){if(hook!=IntPtr.Zero)return;callback=OutsideClick;hook=SetWindowsHookEx(14,callback,GetModuleHandle(null),0);if(hook==IntPtr.Zero){App.Store.Log("Quickbar outside click hook failed: "+Marshal.GetLastWin32Error());Dismiss();}}
 void StopHook(){if(hook!=IntPtr.Zero){UnhookWindowsHookEx(hook);hook=IntPtr.Zero;}callback=null;}
 IntPtr OutsideClick(int code,IntPtr message,IntPtr data){
  if(code>=0&&opened&&(message.ToInt32()==0x201||message.ToInt32()==0x204||message.ToInt32()==0x207)){
   int x=Marshal.ReadInt32(data),y=Marshal.ReadInt32(data,4);
   Dispatcher.BeginInvoke(()=>{if(!opened)return;var p=tray.PointFromScreen(new Point(x,y));var dot=dock.Accessories.PointFromScreen(new Point(x,y));
    if(!new Rect(0,0,tray.ActualWidth,tray.ActualHeight).Contains(p)&&!dock.Accessories.HitBounds(2).Contains(dot))Dismiss();});
  }
  return CallNextHookEx(hook,code,message,data);
 }
 internal void Dismiss(){opened=false;pointer=null;pressed=false;StopHook();spring.SetTarget(0);if(IsVisible)Wake();}
 void Wake(){if(rendering)return;rendering=true;last=clock.Elapsed.TotalSeconds;CompositionTarget.Rendering+=Frame;}
 internal static double Expansion(double reveal){double x=Math.Max(0,(reveal-.22)/.78);return x<=1?DockAccessories.Smooth(x):x;}
 void ApplyReveal(){
  double amount=Expansion(spring.Value),unit=dock.Accessories.Unit;
  var rect=QuickMorph.Bounds(anchorLocal,destination,unit,amount);
  scale.ScaleX=rect.Width/tray.Width;scale.ScaleY=rect.Height/tray.Height;
  Canvas.SetLeft(tray,rect.Left);Canvas.SetTop(tray,rect.Top);
  tray.Opacity=1;tray.IsHitTestVisible=opened&&amount>.6;
  morph.Update(anchorLocal,rect,unit,spring.Value,amount);
  if(tray.Child!=null)tray.Child.Opacity=DockAccessories.Smooth((amount-.18)/.55);
 }
 void Frame(object? sender,EventArgs e){
  double now=clock.Elapsed.TotalSeconds,dt=now-last;spring.Step(dt*.75);last=now;
  ApplyReveal();dock.Accessories.InvalidateVisual();
  var c=App.Store.Config;double pitch=c.IconSize+c.Gap;bool moving=false;
  for(int i=0;i<hovers.Count;i++){
   double target=0;
   if(opened&&pointer.HasValue&&!pressed){double dx=((i%c.QuickColumns+.5)*pitch-pointer.Value.X)/(pitch*1.1),dy=((i/c.QuickColumns+.5)*pitch-pointer.Value.Y)/(pitch*1.1);target=(c.HoverScale-1)*Math.Exp(-(dx*dx+dy*dy)*1.6);}
   var entry=hovers[i];entry.response.Step(target,dt);entry.transform.ScaleX=entry.transform.ScaleY=1+entry.response.Value;moving|=entry.response.Moving;
   if(feedback.TryGetValue(i,out double began)){double elapsed=now-began;artwork[i].Animate(IconEffects.Bounce(elapsed));if(elapsed>=IconEffects.BounceDuration)feedback.Remove(i);else moving=true;}
  }
  if(!spring.Moving&&!moving){CompositionTarget.Rendering-=Frame;rendering=false;if(!opened){Hide();dock.Accessories.InvalidateVisual();}}
 }
}
// One painted object owns the whole transition: bent stroke, straight capsule,
// then rounded tray. It never fades out or hands its outline to another visual.
internal sealed class QuickMorph:FrameworkElement {
 Point anchor;Rect rect;double unit,reveal,amount;
 internal static Rect Bounds(Point anchor,Rect target,double unit,double amount){
  double w=1.65*unit,h=17.65*unit;
  return new Rect(anchor.X-w/2+(target.Left-anchor.X+w/2)*amount,anchor.Y-h/2+(target.Top-anchor.Y+h/2)*amount,w+(target.Width-w)*amount,h+(target.Height-h)*amount);
 }
 internal void Update(Point a,Rect r,double u,double p,double t){anchor=a;rect=r;unit=u;reveal=p;amount=t;InvalidateVisual();}
 protected override void OnRender(DrawingContext dc){
  double t=Math.Clamp(amount,0,1);
  byte Mix(double a,double b)=>(byte)Math.Round(a+(b-a)*t);
  var fill=new LinearGradientBrush(Color.FromArgb(Mix(255,155),Mix(186,223),Mix(186,226),Mix(186,231)),Color.FromArgb(Mix(255,125),139,Mix(139,146),Mix(139,156)),90);
  if(reveal<.22){
   double bend=1-DockAccessories.Smooth(reveal/.22);var g=new StreamGeometry();using(var c=g.Open()){c.BeginFigure(new Point(anchor.X+5*unit*bend,anchor.Y-8*unit),false,false);c.LineTo(new Point(anchor.X-5*unit*bend,anchor.Y),true,false);c.LineTo(new Point(anchor.X+5*unit*bend,anchor.Y+8*unit),true,false);}
   dc.DrawGeometry(null,new Pen(fill,1.65*unit){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round},g);
  }else{
   double radius=Math.Min(Math.Min(rect.Width,rect.Height)/2,14);
   dc.DrawRoundedRectangle(fill,new Pen(new SolidColorBrush(Color.FromArgb((byte)(85*t),242,244,247)),t),rect,radius,radius);
  }
 }
}

