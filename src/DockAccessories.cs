using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
namespace EurekaDock;
internal sealed class DockAccessories : FrameworkElement {
 internal static readonly DockItem RecycleBin=new() {Name="回收站",Target="::{645FF040-5081-101B-9F08-00AA002F954E}"};
 readonly PopSpring[] buttons={new(1),new(1),new(1)};
 readonly HoverResponse[] hovers={new(),new(),new()};
 readonly Stopwatch clock=Stopwatch.StartNew();
 readonly DispatcherTimer stateTimer=new(){Interval=TimeSpan.FromSeconds(2)};
 static readonly Geometry gear=MakeGear();
 static readonly Geometry openEye=Geometry.Parse("M -8,0 C -4,-6 4,-6 8,0 C 4,6 -4,6 -8,0");
 static readonly Geometry closedEye=Geometry.Parse("M -8,-2 C -4,4 4,4 8,-2 M -5,1 L -7,3 M 0,2.5 L 0,5 M 5,1 L 7,3");
 internal static double Smooth(double x){x=Math.Clamp(x,0,1);return x*x*(3-2*x);}
 internal static Geometry ArrowShape(double reveal){
  double bend=1-Smooth(reveal/.24),height=8+2*Smooth((reveal-.12)/.25);
  var g=new StreamGeometry();using(var c=g.Open()){c.BeginFigure(new Point(5*bend,-height),false,false);c.LineTo(new Point(-5*bend,0),true,false);c.LineTo(new Point(5*bend,height),true,false);}g.Freeze();return g;
 }
 bool rendering,backgroundSuspended;
 double lastFrame;
 Point? pointer;
 int pressed=-1;
 Point down;
 Configuration C=>App.Store.Config;
 DockWindow Owner=>(DockWindow)Window.GetWindow(this);
 internal bool Animating=>rendering;
 internal bool DesktopVisible {get;private set;}=true;
 // Two right-hand controls fit inside the icon-height band, including hover.
 internal double Unit=>C.IconSize/48;
 internal Point Center(int index)=>new(ActualWidth-(index==2?43:17)*Unit,ActualHeight-12-C.IconSize/2+(index==2?0:(index==0?-1:1)*11.5*Unit));
 internal Rect HitBounds(int index){var p=Center(index);return new Rect(p.X-(index==2?19:12)*Unit,p.Y-11.5*Unit,(index==2?33:24)*Unit,23*Unit);}
 internal double DotX=>Center(2).X;
 internal double DotY(int index)=>Center(index).Y;
 internal int HitAt(Point p){for(int i=0;i<3;i++)if(HitBounds(i).Contains(p))return i;return -1;}
 internal double HoverScale(int index)=>1+hovers[index].Value;
 internal void HoverAt(Point? p){pointer=p;Wake();}
 public DockAccessories(){
  ClipToBounds=true;Focusable=false;
  ToolTipService.SetInitialShowDelay(this,500);
  stateTimer.Tick+=(s,e)=>{if(IsVisible)SyncDesktopState();};
  Loaded+=(s,e)=>{if(!backgroundSuspended){SyncDesktopState();stateTimer.Start();}};
  Unloaded+=(s,e)=>stateTimer.Stop();
  MouseEnter+=(s,e)=>{SyncDesktopState();HoverAt(e.GetPosition(this));};
  MouseMove+=(s,e)=>{var p=e.GetPosition(this);HoverAt(p);ToolTip=HitAt(p) switch {0=>L.T("打开设置"),1=>DesktopVisible?L.T("隐藏桌面图标"):L.T("显示桌面图标"),2=>L.T("展开 / 收起快捷栏"),_=>null};};
  MouseLeave+=(s,e)=>HoverAt(null);
  MouseLeftButtonDown+=(s,e)=>{
   if(Owner.MenuOpen)return;
   down=e.GetPosition(this);pressed=HitAt(down);if(pressed<0)return;
   buttons[pressed].SetTarget(.78);CaptureMouse();Wake();e.Handled=true;
  };
  MouseLeftButtonUp+=(s,e)=>{
   if(pressed<0)return;
   int hit=pressed;pressed=-1;ReleaseMouseCapture();buttons[hit].SetTarget(1);
   if(buttons[hit].Value>.95)buttons[hit].Kick(-4);
   var p=e.GetPosition(this);bool click=(p-down).Length<7&&HitAt(p)==hit;
   Wake();if(click)Activate(hit);e.Handled=true;
  };
  LostMouseCapture+=(s,e)=>{if(pressed>=0)buttons[pressed].SetTarget(1);pressed=-1;Wake();};
 }
 internal void SyncDesktopState(){int result=Native.DesktopIcons(-1);if(result>=0&&DesktopVisible!=(result==1)){DesktopVisible=result==1;InvalidateVisual();}}
 internal void Activate(int index){
  if(Owner.MenuOpen)return;
  try{
   if(index==0)((App)Application.Current).OpenSettings();
   else if(index==1){int result=Native.DesktopIcons(2);if(result<0)throw new System.Runtime.InteropServices.COMException(L.T("当前桌面无法切换图标显示"),result);DesktopVisible=result==1;InvalidateVisual();App.Store.Log("Desktop icons visible="+result);}
   else if(index==2)Owner.ToggleQuickBar();
  }catch(Exception ex){App.Store.Log("Dock control: "+ex);MessageBox.Show(ex.Message,"EurekaDock");}
 }
 internal void Reload(){Wake();InvalidateVisual();}
 internal void RefreshHover()=>HoverAt(IsMouseOver?Mouse.GetPosition(this):null);
 internal void SetBackgroundSuspended(bool value){backgroundSuspended=value;if(value){stateTimer.Stop();pointer=null;pressed=-1;ReleaseMouseCapture();CompositionTarget.Rendering-=Frame;rendering=false;}else{stateTimer.Start();RefreshHover();}}
 void Wake(){if(rendering||backgroundSuspended)return;rendering=true;lastFrame=clock.Elapsed.TotalSeconds;CompositionTarget.Rendering+=Frame;}
 void Frame(object? sender,EventArgs e){
  double now=clock.Elapsed.TotalSeconds,dt=now-lastFrame;lastFrame=now;bool moving=false;
  for(int i=0;i<3;i++){
   double proximity=pointer.HasValue?Math.Exp(-(pointer.Value-Center(i)).LengthSquared/(2*Math.Pow(16*Unit,2))):0;
   hovers[i].Step(.14*proximity,dt);buttons[i].Step(dt);moving|=hovers[i].Moving||buttons[i].Moving;
  }
  InvalidateVisual();if(!moving){CompositionTarget.Rendering-=Frame;rendering=false;}
 }
 static Geometry MakeGear(){
  var g=new StreamGeometry();using(var c=g.Open()){
   for(int i=0;i<=128;i++){double a=i*Math.PI*2/128,r=6.5+.85*Math.Cos(a*8);var p=new Point(r*Math.Cos(a),r*Math.Sin(a));if(i==0)c.BeginFigure(p,false,true);else c.LineTo(p,true,true);}
  }g.Freeze();return g;
 }
 protected override void OnRender(DrawingContext dc){
  base.OnRender(dc);
  var hitBrush=new SolidColorBrush(Color.FromArgb(1,0,0,0));hitBrush.Freeze();
  for(int i=0;i<3;i++){
   var center=Center(i);dc.DrawRoundedRectangle(hitBrush,null,HitBounds(i),4*Unit,4*Unit);
   double scale=Unit*buttons[i].Value*HoverScale(i),light=Math.Clamp(hovers[i].Value/.14,0,1);
   byte top=(byte)(186+light*36),bottom=(byte)(139+light*35);
   var brush=new LinearGradientBrush(Color.FromRgb(top,top,top),Color.FromRgb(bottom,bottom,bottom),90);
   double reveal=i==2?Owner.QuickReveal:0;
   var pen=new Pen(brush,1.65+(i==2?3*Smooth((reveal-.18)/.24):0)){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};
   if(i==2)dc.PushOpacity(Owner.QuickMorphVisible?0:1);
   dc.PushTransform(new TranslateTransform(center.X,center.Y));dc.PushTransform(new ScaleTransform(scale,scale));
   var shape=i==0?gear:i==1?(DesktopVisible?openEye:closedEye):ArrowShape(reveal);
   var shadow=new Pen(new SolidColorBrush(Color.FromArgb(65,0,0,0)),2.5){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};
   dc.PushTransform(new TranslateTransform(0,.6));dc.DrawGeometry(null,shadow,shape);dc.Pop();
   dc.DrawGeometry(null,pen,shape);
   if(i==0)dc.DrawEllipse(null,pen,new Point(),2.45,2.45);
   if(i==1&&DesktopVisible)dc.DrawEllipse(null,pen,new Point(),1.8,1.8);
   dc.Pop();dc.Pop();
   if(i==2)dc.Pop();
  }
 }
 internal void Dispose(){SetBackgroundSuspended(true);}
}
