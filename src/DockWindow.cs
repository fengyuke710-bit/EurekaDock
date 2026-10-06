using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace EurekaDock;
internal sealed class DockWindow : Window {
 internal readonly DockSurface Surface=new();
 internal readonly DockAccessories Accessories=new();
 readonly ColumnDefinition accessoryColumn=new();
 IntPtr hwnd,hook;
 Native.WinEvent? callback;
 readonly DispatcherTimer attachRetry=new() {Interval=TimeSpan.FromSeconds(1)};
 bool closing;
 bool initialLayout;
 int retryCount;
 bool hidden,menuOpen,attached;
 double anchorTop,baseLeft,baseWidth,baseHeight,sideWidth;
 QuickBarWindow? quickBar;
 MusicWindow? music;
 void SyncMusic(){if(closing)return;if(App.Store.Config.MusicEnabled)music??=new MusicWindow(this);music?.Sync(IsVisible);}
 internal MusicWindow? Music=>music;
 internal bool IsClosing=>closing;
 internal bool Revealing;
 internal double MusicAnchorTop=>anchorTop;
 internal void PreviewMusicColor(){music?.Line.Refresh();music?.Bars.Refresh();}
 internal double QuickReveal=>quickBar?.Reveal??0;
 internal bool QuickMorphVisible=>quickBar?.IsVisible==true;
 internal void ToggleQuickBar() {quickBar??=new QuickBarWindow(this);quickBar.Toggle();}
 internal bool DesktopAttached=>attached;
 internal bool MenuOpen {get=>menuOpen;set {menuOpen=value; if(!value) {RefreshVisibility();Surface.RefreshHover();Accessories.RefreshHover();}}}
 public DockWindow(bool prepareReveal=false) {
  if(prepareReveal)BeginReveal();
  Title="EurekaDock"; WindowStyle=WindowStyle.None; AllowsTransparency=true; Background=Brushes.Transparent;
  ShowInTaskbar=false; ShowActivated=false; ResizeMode=ResizeMode.NoResize; Topmost=false;
  var layout=new Grid();layout.ColumnDefinitions.Add(accessoryColumn);layout.ColumnDefinitions.Add(new ColumnDefinition());
  Grid.SetColumn(Surface,1);layout.Children.Add(Accessories);layout.Children.Add(Surface);Content=layout;
  attachRetry.Tick+=(s,e)=> {TryAttach();RefreshVisibility();if(attached||++retryCount>=60)attachRetry.Stop();};
  SourceInitialized+=(s,e)=> {
   hwnd=new WindowInteropHelper(this).Handle;
   long style=Native.GetWindowLongPtr(hwnd,-20).ToInt64();
   Native.SetWindowLongPtr(hwnd,-20,new IntPtr(style|0x08000000L|0x80L));
   HwndSource.FromHwnd(hwnd)?.AddHook(Messages);
   callback=(h,ev,w,o,c,t,ms)=>Dispatcher.BeginInvoke(RefreshVisibility);
   hook=Native.SetWinEventHook(3,3,IntPtr.Zero,callback,0,0,0);
   App.Store.Log("Desktop host="+Native.DesktopHost()+"; foreground hook="+hook);
   TryAttach();
   if(!attached)attachRetry.Start();
  };
  Loaded+=(s,e)=>RefreshVisibility();
  IsVisibleChanged+=(s,e)=>SyncMusic();
  App.Store.Changed+=Reload;
  Microsoft.Win32.SystemEvents.DisplaySettingsChanged+=DisplayChanged;
  Closing+=(s,e)=> {closing=true;music?.Close();music=null;quickBar?.Close();quickBar=null;attachRetry.Stop();if(attached) {Native.DetachDesktop(hwnd);attached=false;}};
  Closed+=(s,e)=> { App.Store.Changed-=Reload; Microsoft.Win32.SystemEvents.DisplaySettingsChanged-=DisplayChanged; if(hook!=IntPtr.Zero) Native.UnhookWinEvent(hook); Surface.Dispose(); Accessories.Dispose(); };
  Reload();
 }
 void DisplayChanged(object? s,EventArgs e)=>Dispatcher.BeginInvoke(Reload);
 IntPtr Messages(IntPtr h,int msg,IntPtr w,IntPtr l,ref bool handled) {
  if(msg==0x8001) {handled=true;Dispatcher.BeginInvoke(()=>Application.Current.Shutdown());}
  if(msg==0x8002) {handled=true;Dispatcher.BeginInvoke(()=>((App)Application.Current).OpenSettings());}
  if(msg==0x21) {handled=true; return new IntPtr(3);}
  if(msg==0x20e) { if(!Revealing)Surface.Scroll((short)((w.ToInt64()>>16)&0xffff)/120.0,false); handled=true; }
  if(msg==0x2e0) Dispatcher.BeginInvoke(Reload);
  return IntPtr.Zero;
 }
 internal void SetHidden(bool value) {hidden=value;RefreshVisibility();}
 void TryAttach() {
  if(attached)return;
  int result=Native.AttachDesktop(hwnd);attached=result==0;
  if(attached) {bool wasWaiting=attachRetry.IsEnabled;attachRetry.Stop();if(wasWaiting)Dispatcher.BeginInvoke(Reload);}
  App.Store.Log("Desktop attachment="+result);
  if(!attached) Native.PlaceAtBottom(hwnd);
 }
 void RefreshVisibility() {
  if(closing||(Revealing&&initialLayout)||menuOpen||hwnd==IntPtr.Zero) return;
  if(!attached&&Native.IsDesktopForeground()) TryAttach();
  var c=App.Store.Config;
  bool show=!hidden&&(c.DockEnabled||c.MusicEnabled)&&(attached||Native.IsDesktopForeground()||Native.GetForegroundWindow()==hwnd);
  if(!show){quickBar?.Dismiss();if(IsVisible)Hide();else music?.Sync(false);return;}
  Surface.Visibility=c.DockEnabled?Visibility.Visible:Visibility.Hidden;
  Accessories.Visibility=c.ShowAccessories?Visibility.Visible:Visibility.Hidden;
  if(!c.ShowAccessories)quickBar?.Dismiss();
  accessoryColumn.Width=new GridLength(c.ShowAccessories?sideWidth:0);
  Width=baseWidth+(c.ShowAccessories?sideWidth:0);Height=baseHeight;
  Left=baseLeft-(c.ShowAccessories?sideWidth:0);Top=anchorTop;
  if(!IsVisible)Show();Native.PlaceAtBottom(hwnd);
  SyncMusic();
  if(!c.DockEnabled&&music?.IsVisible==true&&c.ShowAccessories){Left=music.Left+(c.MusicStyle=="bars"?music.Bars.HorizontalPadding:0)-sideWidth-10;Top=music.Top+music.Height-Height;}
  initialLayout=true;
 }
 internal void BeginReveal(){
  Revealing=true;Opacity=0;IsHitTestVisible=false;
  Surface.SetRevealPaused(true);Accessories.SetBackgroundSuspended(true);
  Accessories.SyncDesktopState();
  music?.Sync(IsVisible);
 }
 internal void CompleteReveal(){
  if(closing)return;
  Revealing=false;Opacity=1;IsHitTestVisible=true;
  Surface.SetRevealPaused(false);Accessories.SetBackgroundSuspended(false);
  if(music!=null)music.Opacity=1;
  RefreshVisibility();
 }
 internal void Reload() {
  if(closing)return;
  quickBar?.Dismiss();
  var c=App.Store.Config;
  var screen=System.Windows.Forms.Screen.PrimaryScreen!.Bounds;
  var dpi=VisualTreeHelper.GetDpi(this);
  double screenWidth=screen.Width/dpi.DpiScaleX, screenHeight=screen.Height/dpi.DpiScaleY;
  baseWidth=screenWidth*c.WidthFraction;sideWidth=c.IconSize*1.6+38;baseHeight=c.IconSize*1.7+30;
  baseLeft=screen.X/dpi.DpiScaleX+(screenWidth-baseWidth)/2;
  anchorTop=screen.Y/dpi.DpiScaleY+screenHeight*c.VerticalFraction-baseHeight;
  Surface.Reload();
  Accessories.Reload();
  RefreshVisibility();
 }
 internal void OpenMenu(DockItem item,Point local) {
  menuOpen=true; Surface.CancelDrag();
  try { var p=Surface.PointToScreen(local); int result=Native.ShellMenu(hwnd,item.Target,(int)p.X,(int)p.Y,false); if(result<0) throw new System.Runtime.InteropServices.COMException(L.T("Shell 菜单失败"),result); }
  catch(Exception ex) { App.Store.Log("Shell menu: "+ex); MessageBox.Show(ex.Message,"EurekaDock"); }
  finally {MenuOpen=false;}
 }
 internal void Launch(DockItem item) {
  if(!Native.LaunchTarget(item.Target,item.Arguments,hwnd)) { App.Store.Log("Launch failed: "+item.Target); MessageBox.Show(L.T("无法启动目标，请在设置中检查路径。"),"EurekaDock"); }
 }
}
internal sealed class DockSurface : FrameworkElement {
 internal readonly Carousel Motion=new();
 readonly Dictionary<Guid,FogIcon?> icons=new();
 readonly Dictionary<int,FogFrame> fogFrames=new();
 readonly Dictionary<int,HoverResponse> hoverResponses=new();
 readonly Dictionary<string,FogIcon?> iconCache=new();
 readonly DrawingGroup scene=new();
 readonly List<(Rect rect,DockItem item)> hits=new();
 readonly Stopwatch time=Stopwatch.StartNew();
 readonly DispatcherTimer hold=new();
 List<DockItem> items=new();
 bool rendering,pressed,dragged,backgroundSuspended,resourcesReleased,revealPaused;
 internal bool BackgroundAnimating=>rendering;
 internal int CachedIconCount=>iconCache.Count;
 double lastFrame,downX,startPosition,lastX,lastMove,dragVelocity;
 double? hover;
 DockItem? pressedItem;
 Configuration C=>App.Store.Config;
 DockWindow Owner=>(DockWindow)Window.GetWindow(this);
 double Pitch=>C.IconSize+C.Gap;
 public DockSurface() {
  ClipToBounds=true; Focusable=false;
  hold.Tick+=(s,e)=> {hold.Stop();if(pressed) BeginDrag();};
  MouseWheel+=(s,e)=> {Scroll(-e.Delta/120.0);e.Handled=true;};
  MouseMove+=Move;
  MouseEnter+=(s,e)=>RefreshHover();
  MouseLeave+=(s,e)=> {hover=null;Wake();};
  MouseLeftButtonDown+=Down;
  MouseLeftButtonUp+=Up;
  MouseRightButtonUp+=(s,e)=> {if(!pressed) {var p=e.GetPosition(this); var item=Hit(p);if(item!=null) Owner.OpenMenu(item,p);} e.Handled=true;};
  LostMouseCapture+=(s,e)=> {if(pressed) CancelDrag();};
  SizeChanged+=(s,e)=>{ConfigureMotion();InvalidateVisual();Wake();};
 }
 internal void Reload() {
  if(backgroundSuspended){ReleaseBackgroundResources();return;}
  resourcesReleased=false;
  if(pressed)CancelDrag();
  items=C.Items.Where(i=>i.Available&&!i.Hidden).ToList(); icons.Clear();ConfigureMotion();
  int size=(int)Math.Ceiling(C.IconSize*1.5*VisualTreeHelper.GetDpi(this).DpiScaleX);
  var active=new HashSet<string>();
  foreach(var item in items) {
   double blurPixels=C.Blur*size/C.IconSize;
   string key=item.Target+"|"+item.Icon+"|"+size+"|"+blurPixels;
   active.Add(key);
   if(!iconCache.TryGetValue(key,out var icon)) {var source=Native.Icon(item,size) as BitmapSource;icon=source==null?null:new FogIcon(source,blurPixels);iconCache[key]=icon;}
   icons[item.Id]=icon;
  }
  foreach(var key in iconCache.Keys.Where(k=>!active.Contains(k)).ToArray())iconCache.Remove(key);
  fogFrames.Clear();
  InvalidateVisual();Wake();
 }
 void ConfigureMotion()=>Motion.Configure(C.InfiniteCarousel,items.Count,Math.Max(0,(ActualWidth-C.IconSize-4)/(2*Pitch)));
 internal void Scroll(double slots,bool vertical=true) {if(revealPaused||Owner.MenuOpen||pressed) return;Motion.Wheel(vertical&&C.ReverseWheel?-slots:slots);Wake();}
 internal void RefreshHover() {hover=IsMouseOver?Mouse.GetPosition(this).X:null;Wake();}
 internal void SetBackgroundSuspended(bool value){
  backgroundSuspended=value;
  if(value){CancelDrag();hover=null;hoverResponses.Clear();CompositionTarget.Rendering-=Frame;rendering=false;hold.Stop();}
  else {if(resourcesReleased)Reload();RefreshHover();}
 }
 internal void ReleaseBackgroundResources(){scene.Children.Clear();icons.Clear();iconCache.Clear();fogFrames.Clear();hits.Clear();resourcesReleased=true;InvalidateVisual();}
 internal void SetRevealPaused(bool value){
  revealPaused=value;
  if(value){CancelDrag();hover=null;hoverResponses.Clear();CompositionTarget.Rendering-=Frame;rendering=false;hold.Stop();}
  else RefreshHover();
  InvalidateVisual();
 }
 void Wake() {if(rendering||backgroundSuspended||revealPaused)return;lastFrame=time.Elapsed.TotalSeconds;rendering=true;CompositionTarget.Rendering+=Frame;}
 void Frame(object? s,EventArgs e) {
  double now=time.Elapsed.TotalSeconds,dt=now-lastFrame;
  Motion.Step(dt,C.Spring,C.Damping);lastFrame=now;
  bool hoverMoving=false;
  int slots=(int)Math.Ceiling(ActualWidth/Pitch/2)+2,baseSlot=(int)Math.Floor(-Motion.VisualPosition);
  // Track each icon's current position even while the carousel is moving.
  // Keep the response state across settling so there is no restore transition.
  bool enabled=hover.HasValue&&!pressed&&!Owner.MenuOpen;
  for(int j=baseSlot-slots;j<=baseSlot+slots;j++) {
   if(!hoverResponses.TryGetValue(j,out var response))hoverResponses[j]=response=new HoverResponse();
   double target=0;
   if(enabled&&(C.InfiniteCarousel||(j>=0&&j<items.Count))) {double x=ActualWidth/2+(j+Motion.VisualPosition)*Pitch,d=(x-hover!.Value)/(Pitch*1.1);target=(C.HoverScale-1)*Math.Exp(-d*d*1.6);}
   response.Step(target,dt);hoverMoving|=response.Moving;
  }
  foreach(int j in hoverResponses.Keys.Where(j=>j<baseSlot-slots||j>baseSlot+slots).ToArray())hoverResponses.Remove(j);
  InvalidateVisual();
  if((!Motion.Moving||Motion.Dragging)&&!hoverMoving) {CompositionTarget.Rendering-=Frame;rendering=false;}
 }
 DockItem? Hit(Point p)=>hits.LastOrDefault(h=>h.rect.Contains(p)).item;
 void Down(object s,MouseButtonEventArgs e) {
  if(Owner.MenuOpen)return;
  var p=e.GetPosition(this); pressed=true;dragged=false;pressedItem=Hit(p); downX=lastX=p.X;startPosition=Motion.Position;
  lastMove=time.Elapsed.TotalSeconds;dragVelocity=0;Motion.Target=Motion.Position;Motion.Velocity=0;
  Wake();CaptureMouse();hold.Interval=TimeSpan.FromMilliseconds(C.HoldMilliseconds);hold.Start(); e.Handled=true;
 }
 void BeginDrag() {if(dragged)return;dragged=true;Motion.Dragging=true;pressedItem=null;hold.Stop();Wake();}
 void Move(object s,MouseEventArgs e) {
  var p=e.GetPosition(this);hover=p.X;
  if(pressed) {
   if(Math.Abs(p.X-downX)>C.DragThreshold) BeginDrag();
   if(dragged) {
    double now=time.Elapsed.TotalSeconds,dt=now-lastMove;
    double previous=Motion.Position;Motion.DragBy((p.X-lastX)/Pitch);
    if(dt>.001) dragVelocity=.55*dragVelocity+.45*((Motion.Position-previous)/dt);
    lastX=p.X;lastMove=now;
   }
  }
  InvalidateVisual();Wake();
 }
 void Up(object s,MouseButtonEventArgs e) {
  if(!pressed)return;
  var item=pressedItem;bool wasDrag=dragged;pressed=false;hold.Stop();ReleaseMouseCapture();
  if(wasDrag) {Motion.Release(time.Elapsed.TotalSeconds-lastMove>.1?0:dragVelocity);Wake();}
  else {Motion.Release(0);Wake(); if(item!=null&&Hit(e.GetPosition(this))?.Id==item.Id) Owner.Launch(item);}
  dragged=false;e.Handled=true;
 }
 internal void CancelDrag() {pressed=false;dragged=false;hold.Stop();ReleaseMouseCapture();Motion.Release(0);Wake();}
 protected override void OnRender(DrawingContext destination) {
  base.OnRender(destination);destination.DrawDrawing(scene);using var dc=scene.Open();hits.Clear();
  if(backgroundSuspended)return;
  // One alpha step enables wheel/drag in spaces between icons on a layered HWND.
  dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(1,0,0,0)),null,new Rect(RenderSize));
  if(items.Count==0)return;
  int slots=(int)Math.Ceiling(ActualWidth/Pitch/2)+2,baseSlot=(int)Math.Floor(-Motion.VisualPosition);
  foreach(int j in fogFrames.Keys.Where(j=>j<baseSlot-slots||j>baseSlot+slots).ToArray())fogFrames.Remove(j);
  for(int j=baseSlot-slots;j<=baseSlot+slots;j++) {
   if(!C.InfiniteCarousel&&(j<0||j>=items.Count))continue;
   var item=items[C.InfiniteCarousel?Carousel.Wrap(j,items.Count):j];
   double x=ActualWidth/2+(j+Motion.VisualPosition)*Pitch;
   double scale=1+(hoverResponses.TryGetValue(j,out var response)?response.Value:0);
   double size=C.IconSize*scale, bottom=ActualHeight-12;
   if(!C.InfiniteCarousel&&j==0&&Math.Abs(Motion.Position-Motion.Maximum)<.001)x=Math.Max(x,size/2+2);
   if(!C.InfiniteCarousel&&j==items.Count-1&&Math.Abs(Motion.Position-Motion.Minimum)<.001)x=Math.Min(x,ActualWidth-size/2-2);
   var rect=new Rect(x-size/2,bottom-size,size,size);
   double leftFade=C.InfiniteCarousel?C.FadeWidth:C.FadeWidth*FogIcon.Fade(Motion.Maximum-Motion.Position,.75),rightFade=C.InfiniteCarousel?C.FadeWidth:C.FadeWidth*FogIcon.Fade(Motion.Position-Motion.Minimum,.75);
   if(icons.TryGetValue(item.Id,out var icon)&&icon!=null) {
    var padded=icon.Padded(rect);
    if(padded.Right<0||padded.Left>ActualWidth)continue;
    if((leftFade<=0||padded.Left>=leftFade)&&(rightFade<=0||padded.Right<=ActualWidth-rightFade))dc.DrawImage(icon.Sharp,padded);
    else {
     if(!fogFrames.TryGetValue(j,out var frame)||!frame.Matches(icon))fogFrames[j]=frame=new FogFrame(icon);
     frame.Draw(dc,padded,ActualWidth,leftFade,rightFade);
    }
   }
   else {dc.DrawRoundedRectangle(Brushes.SlateGray,null,rect,12,12);var text=new FormattedText(item.Name.Length>0?item.Name[..1]:"?",System.Globalization.CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),size*.45,Brushes.White,VisualTreeHelper.GetDpi(this).PixelsPerDip);dc.DrawText(text,new Point(rect.X+(size-text.Width)/2,rect.Y+(size-text.Height)/2));}
   hits.Add((rect,item));
  }
 }
 public void Dispose() {SetBackgroundSuspended(true);ReleaseBackgroundResources();items.Clear();}
}




