using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace EurekaDock;

// Keep the detector and the IPC HWND independent of all rendered windows.
// A closed Dock must have no strong reference left in App or this controller.
internal sealed class DesktopLifecycle:IDisposable {
 readonly App app;
 readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(500)};
 readonly DispatcherTimer reclaim=new(){Interval=TimeSpan.FromMilliseconds(300)};
 HwndSource? signal;
 IntPtr hook;
 Native.WinEvent? callback;
 DesktopReveal? reveal;
 bool disposed,queued,hidden,started,hasCreated,reclaimRequested;
 internal int ReclaimCount {get;private set;}
 double? carouselPosition;
 // Keep only the small frozen artwork DTO, never a closed window or worker.
 TrackInfo revealTrack=TrackInfo.Empty;
 bool revealTrackHidden;
 internal Func<bool> Covered=FullscreenGuard.IsBackgroundCovered;
 internal bool Suspended {get;private set;}
 internal WeakReference? LastClosed {get;private set;}
 internal bool Revealing=>reveal?.Animating==true;
 internal double RevealScale=>reveal?.Scale??1;
 internal Rect RevealBounds=>reveal?.Bounds??Rect.Empty;
 internal double RevealAlignmentError=>reveal?.AlignmentError??0;

 internal DesktopLifecycle(App owner){
  app=owner;
  timer.Tick+=(s,e)=>Refresh();
  reclaim.Tick+=(s,e)=>{
   reclaim.Stop();if(disposed||app.Dock!=null)return;
   GC.Collect(GC.MaxGeneration,GCCollectionMode.Forced,true,true);
   GC.WaitForPendingFinalizers();
   BackgroundMemory.ReleaseIdlePages();
   ReclaimCount++;
   App.Store.Log("Desktop lifecycle: closed-window resources reclaimed");
  };
 }
 internal void Start(){
  if(started)return;started=true;
  var parameters=new HwndSourceParameters("EurekaDock"){
   WindowStyle=unchecked((int)0x80000000),ExtendedWindowStyle=0x08000080,
   Width=0,Height=0
  };
  signal=new HwndSource(parameters);signal.AddHook(Messages);
  callback=(h,ev,w,o,c,t,ms)=>QueueRefresh();
  hook=Native.SetWinEventHook(3,3,IntPtr.Zero,callback,0,0,0);
  App.Store.Changed+=Reload;timer.Start();Refresh();
 }
 IntPtr Messages(IntPtr h,int message,IntPtr w,IntPtr l,ref bool handled){
  if(message==0x8001){handled=true;app.Dispatcher.BeginInvoke(()=>app.Shutdown());}
  if(message==0x8002){handled=true;app.Dispatcher.BeginInvoke(app.OpenSettings);}
  return IntPtr.Zero;
 }
 void QueueRefresh(){
  if(disposed||queued)return;queued=true;
  app.Dispatcher.BeginInvoke(()=>{queued=false;Refresh();});
 }
 internal void SetHidden(bool value){hidden=value;Refresh();}
 internal void Refresh(){
  if(disposed)return;
  bool covered=App.Store.Config.SuspendWhenCovered&&Covered();
  bool shouldClose=covered||hidden||(!App.Store.Config.DockEnabled&&!App.Store.Config.MusicEnabled);
  if(shouldClose){
   if(app.Dock?.MenuOpen==true)return;
   Suspended=covered;
   if(app.Dock!=null){
    CloseDock();
    App.Store.Log("Desktop lifecycle: windows closed; reason="+(covered?"fullscreen/maximized":hidden?"tray hidden":"disabled"));
   }
   // Also release startup pages when the app starts under an already-covered
   // desktop. Request once per suspension, never on every detector tick.
   if(!reclaimRequested){reclaimRequested=true;System.Runtime.GCSettings.LargeObjectHeapCompactionMode=System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;reclaim.Stop();reclaim.Start();}
   return;
  }
  bool animate=hasCreated||Suspended;Suspended=false;
  if(app.Dock!=null)return;
  reclaim.Stop();reclaimRequested=false;
  // Opacity zero prevents an unanimated first frame when recreating the HWNDs.
  var dock=new DockWindow(animate);app.Dock=dock;
  dock.Show();dock.UpdateLayout();
  if(carouselPosition.HasValue){
   var motion=dock.Surface.Motion;
   double position=App.Store.Config.InfiniteCarousel?carouselPosition.Value:Math.Clamp(carouselPosition.Value,motion.Minimum,motion.Maximum);
   motion.Position=motion.Target=position;motion.Velocity=0;dock.Surface.InvalidateVisual();
  }
  if(animate){
   dock.Music?.PrimeRevealTrack(revealTrack,revealTrackHidden);
   reveal=new DesktopReveal(dock);reveal.Start();
   App.Store.Log("Desktop lifecycle: windows recreated; unified reveal started");
  }
  hasCreated=true;
 }
 [MethodImpl(MethodImplOptions.NoInlining)]
 void CloseDock(){
  reveal?.Dispose();reveal=null;
  var dock=app.Dock;app.Dock=null!;
  if(dock==null)return;
  carouselPosition=dock.Surface.Motion.Position;
  revealTrack=App.Store.Config.MusicStyle=="bars"?dock.Music?.Bars.Track??TrackInfo.Empty:TrackInfo.Empty;
  revealTrackHidden=App.Store.Config.MusicStyle=="bars"&&dock.Music?.TrackInfoHidden==true;
  LastClosed=new WeakReference(dock);
  if(ReferenceEquals(app.MainWindow,dock))app.MainWindow=null;
  dock.Close();
 }
 internal void Reload(){reveal?.Complete();reveal?.Dispose();reveal=null;Refresh();app.Dock?.Reload();}
 public void Dispose(){
  if(disposed)return;disposed=true;
  timer.Stop();reclaim.Stop();App.Store.Changed-=Reload;
  if(hook!=IntPtr.Zero){Native.UnhookWinEvent(hook);hook=IntPtr.Zero;}
  signal?.Dispose();signal=null;callback=null;
  CloseDock();
 }
}
