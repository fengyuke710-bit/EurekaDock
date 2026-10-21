using System;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;

namespace EurekaDock;

internal static class FullscreenProbes {
 static IEnumerable<DependencyObject> VisualChildren(DependencyObject item){yield return item;for(int i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(item);i++)foreach(var child in VisualChildren(System.Windows.Media.VisualTreeHelper.GetChild(item,i)))yield return child;}
 [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
 static void PrimeArtwork(App app,System.Windows.Media.ImageSource artwork){app.Dock.Music!.Bars.SetTrack(new TrackInfo("Reveal artwork","Artist",artwork,true));}
 static int SnapshotArtworkPixels(string path){
  var window=Application.Current.Windows.OfType<Window>().Single(w=>w.Title=="EurekaDock reveal");
  var image=((System.Windows.Controls.Canvas)window.Content).Children.OfType<System.Windows.Controls.Image>().Single();
  var visual=new System.Windows.Media.DrawingVisual();
  using(var dc=visual.RenderOpen())dc.DrawImage(image.Source,new Rect(0,0,image.Width,image.Height));
  var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(image.Width),(int)Math.Ceiling(image.Height),96,96,System.Windows.Media.PixelFormats.Pbgra32);
  bitmap.Render(visual);var pixels=new byte[bitmap.PixelWidth*bitmap.PixelHeight*4];bitmap.CopyPixels(pixels,bitmap.PixelWidth*4,0);
  int matches=0;for(int i=0;i<pixels.Length;i+=4)if(pixels[i]==40&&pixels[i+1]==90&&pixels[i+2]==170&&pixels[i+3]==255)matches++;
  var png=new System.Windows.Media.Imaging.PngBitmapEncoder();png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));using var file=File.Create(path);png.Save(file);
  return matches;
 }
 static string WaveFingerprint(){
  var window=Application.Current.Windows.OfType<Window>().Single(w=>w.Title=="EurekaDock reveal");
  var image=((System.Windows.Controls.Canvas)window.Content).Children.OfType<System.Windows.Controls.Image>().Single();
  var visual=new System.Windows.Media.DrawingVisual();using(var dc=visual.RenderOpen())dc.DrawImage(image.Source,new Rect(0,0,image.Width,image.Height));
  var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(image.Width),(int)Math.Ceiling(image.Height),96,96,System.Windows.Media.PixelFormats.Pbgra32);bitmap.Render(visual);
  var crop=new System.Windows.Media.Imaging.CroppedBitmap(bitmap,new Int32Rect(bitmap.PixelWidth/2,0,bitmap.PixelWidth-bitmap.PixelWidth/2,bitmap.PixelHeight));
  var pixels=new byte[crop.PixelWidth*crop.PixelHeight*4];crop.CopyPixels(pixels,crop.PixelWidth*4,0);return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pixels));
 }
 static System.Media.SoundPlayer RevealTone(string root){
  string path=Path.Combine(root,"reveal-test-tone.wav");const int rate=48000,frames=48000;
  using(var writer=new BinaryWriter(File.Create(path))){writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+frames*2);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(rate);writer.Write(rate*2);writer.Write((short)2);writer.Write((short)16);writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(frames*2);for(int i=0;i<frames;i++)writer.Write((short)(Math.Sin(i*2*Math.PI*1200/rate)*1400));}
  var player=new System.Media.SoundPlayer(path);player.Load();return player;
 }
 internal static void RunCoverageWindow(string phasePath){
  var window=new Window{Title="EurekaDock fullscreen verification",Width=720,Height=420,WindowStartupLocation=WindowStartupLocation.CenterScreen};
  Window? utility=null;
  int stage=0;var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(1800)};
  window.Show();window.Activate();window.WindowState=WindowState.Maximized;
  Application.Current.Dispatcher.BeginInvoke(()=>File.WriteAllText(phasePath,"maximized"),DispatcherPriority.ApplicationIdle);
  timer.Tick+=(s,e)=>{
   if(++stage==1){utility=new Window{Title="EurekaDock small foreground verification",Width=500,Height=320,WindowStartupLocation=WindowStartupLocation.CenterScreen};utility.Show();utility.Activate();File.WriteAllText(phasePath,"maximized-overlay");}
   else if(stage==2){utility?.Close();utility=null;window.WindowState=WindowState.Normal;window.Width=720;window.Height=420;window.Activate();File.WriteAllText(phasePath,"normal");}
   else if(stage==3){
    window.WindowStyle=WindowStyle.None;window.ResizeMode=ResizeMode.NoResize;
    var dpi=System.Windows.Media.VisualTreeHelper.GetDpi(window);var work=System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea;
    window.Left=work.Left/dpi.DpiScaleX;window.Top=work.Top/dpi.DpiScaleY;window.Width=work.Width/dpi.DpiScaleX;window.Height=work.Height/dpi.DpiScaleY;
    window.Activate();File.WriteAllText(phasePath,"work-area");
   }
   else if(stage==4){
    window.WindowStyle=WindowStyle.None;window.ResizeMode=ResizeMode.NoResize;
    var dpi=System.Windows.Media.VisualTreeHelper.GetDpi(window);var screen=System.Windows.Forms.Screen.PrimaryScreen!.Bounds;
    window.Left=screen.Left/dpi.DpiScaleX;window.Top=screen.Top/dpi.DpiScaleY;window.Width=screen.Width/dpi.DpiScaleX;window.Height=screen.Height/dpi.DpiScaleY;
    window.Activate();File.WriteAllText(phasePath,"fullscreen");
   }else if(stage==5){utility=new Window{Title="EurekaDock small foreground verification",Width=500,Height=320,WindowStartupLocation=WindowStartupLocation.CenterScreen};utility.Show();utility.Activate();File.WriteAllText(phasePath,"fullscreen-overlay");}
   else if(stage==6){utility?.Close();utility=null;window.WindowState=WindowState.Minimized;File.WriteAllText(phasePath,"minimized");}
   else{timer.Stop();utility?.Close();window.Close();File.WriteAllText(phasePath,"finished");Application.Current.Shutdown();}
  };timer.Start();
 }
 static void Pump(int milliseconds){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(milliseconds)};timer.Tick+=(s,e)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
 internal static void Run(Store store){
  var checks=new List<string>();void Check(bool ok,string name){if(!ok)throw new Exception(name);checks.Add("PASS "+name);}
  var screen=new Rectangle(0,0,2560,1440);
  Check(FullscreenGuard.CoversMonitor(screen,screen,false),"borderless monitor-sized window is fullscreen");
  Check(!FullscreenGuard.CoversMonitor(new Rectangle(0,0,2560,1400),screen,false),"ordinary work-area window is not fullscreen");
  Check(!FullscreenGuard.CoversMonitor(screen,screen,true),"maximized window is not treated as fullscreen");
  Check(FullscreenGuard.CoversWorkArea(new Rectangle(0,0,2560,1400),new Rectangle(0,0,2560,1400),true),"maximized window covering work area triggers background sleep");
  Check(!FullscreenGuard.CoversWorkArea(new Rectangle(0,0,1600,1000),new Rectangle(0,0,2560,1400),false),"ordinary smaller window keeps desktop active");
  store.Load();store.Config.MusicEnabled=true;store.Config.MusicStyle="bars";store.Save();
  for(int i=0;i<20;i++){string folder=Path.Combine(store.Root,"probe-icon-"+i);Directory.CreateDirectory(folder);store.Config.Items.Add(new DockItem{Name="Probe folder "+i,Target=folder});}
  var app=(App)Application.Current;app.Dock=new DockWindow();app.Dock.Show();Pump(250);
  var music=Application.Current.Windows.OfType<MusicWindow>().Single();bool fullscreen=false;music.FullscreenCheck=()=>fullscreen;
  music.Bars.SetTrack(new TrackInfo("Fullscreen test","Artist",null,true));Pump(150);
  fullscreen=true;Pump(900);double angle=music.Bars.Angle;Pump(150);
  Check(music.FullscreenPaused&&!music.Bars.Animating&&Math.Abs(music.Bars.Angle-angle)<.001,"fullscreen stops bar animation and record rotation");
  Check(MusicAudio.Status=="已关闭"&&music.Bars.Track.Title=="Fullscreen test","fullscreen releases audio capture while retaining track details");
  store.Config.SuspendWhenCovered=false;music.Sync(true);Pump(150);
  Check(!music.FullscreenPaused&&music.WorkersRunning&&music.Bars.Animating,"disabling coverage suspension also bypasses the visualizer's fullscreen pause");
  store.Config.SuspendWhenCovered=true;music.Sync(true);
  fullscreen=false;Pump(900);Check(!music.FullscreenPaused&&music.Bars.Animating&&music.Bars.Angle>angle,"leaving fullscreen restarts the bars and record");
  store.Config.MusicStyle="line";store.Save();Pump(150);
  fullscreen=true;Pump(900);Check(music.FullscreenPaused&&!music.Line.Animating,"fullscreen also suspends the linear wave");
  fullscreen=false;Pump(900);Check(!music.FullscreenPaused&&music.IsVisible,"linear wave resumes after fullscreen");
  app.Dock.Close();app.Dock=null!;app.MainWindow=null;
  store.Config.MusicStyle="bars";store.Config.InfiniteCarousel=false;store.Save();
  bool background=true;using var desktop=new DesktopLifecycle(app){Covered=()=>background};desktop.Start();Pump(700);
  Check(app.Dock==null&&desktop.Suspended&&desktop.ReclaimCount==1,"startup under a covered desktop creates no widgets and reclaims startup pages");
  Pump(700);Check(desktop.ReclaimCount==1,"suspended polling does not repeatedly force GC or trim memory");
  background=false;desktop.Refresh();Pump(1100);
  Check(app.Dock!=null&&app.Dock.Surface.CachedIconCount>0,"lifecycle creates configured desktop windows");
  app.Dock!.Surface.Motion.Position=app.Dock.Surface.Motion.Target=Math.Min(app.Dock.Surface.Motion.Maximum,app.Dock.Surface.Motion.Minimum+.37);
  double savedPosition=app.Dock.Surface.Motion.Position;
  var artwork=System.Windows.Media.Imaging.BitmapSource.Create(2,2,96,96,System.Windows.Media.PixelFormats.Bgra32,null,
   new byte[]{40,90,170,255,40,90,170,255,40,90,170,255,40,90,170,255},8);artwork.Freeze();
  PrimeArtwork(app,artwork);
  using var process=Process.GetCurrentProcess();process.Refresh();long activeWorkingSet=process.WorkingSet64,activePrivate=process.PrivateMemorySize64;
  background=true;desktop.Refresh();
  Check(desktop.Suspended&&app.Dock==null&&!Application.Current.Windows.OfType<DockWindow>().Any()&&!Application.Current.Windows.OfType<MusicWindow>().Any(),"fullscreen immediately closes Dock and music HWNDs without a 15-second wait");
  Check(MusicAudio.Status=="已关闭","closing windows stops the audio worker");
  Native.OpenExistingSettings();Pump(150);
  Check(Application.Current.Windows.OfType<SettingsWindow>().Any(w=>w.IsVisible)&&app.Dock==null,"settings IPC still works while all desktop windows are closed");
  app.ReloadDock();foreach(var settings in Application.Current.Windows.OfType<SettingsWindow>().ToArray())settings.Close();
  Pump(900);GC.Collect();GC.WaitForPendingFinalizers();Pump(150);GC.Collect();
  Check(desktop.LastClosed?.IsAlive==false,"closed Dock graph is collectible rather than hidden or retained");
  process.Refresh();var cpuBefore=process.TotalProcessorTime;var measurement=Stopwatch.StartNew();Pump(1500);process.Refresh();
  var metrics=new {WorkingSetActiveMB=activeWorkingSet/1048576.0,WorkingSetSuspendedMB=process.WorkingSet64/1048576.0,PrivateActiveMB=activePrivate/1048576.0,PrivateSuspendedMB=process.PrivateMemorySize64/1048576.0,SuspendedCpuPercent=(process.TotalProcessorTime-cpuBefore).TotalSeconds/measurement.Elapsed.TotalSeconds/Environment.ProcessorCount*100};
  File.WriteAllText(Path.Combine(store.Root,"background-metrics.json"),JsonSerializer.Serialize(metrics,new JsonSerializerOptions{WriteIndented=true}));
  using var tone=RevealTone(store.Root);
  background=false;desktop.Refresh();
  Check(app.Dock!=null&&desktop.Revealing&&desktop.RevealScale<.1,"leaving fullscreen creates new windows starting at a tiny unified scale");
  Rect dockRect=new(app.Dock!.Left,app.Dock.Top,app.Dock.Width,app.Dock.Height),overlayRect=desktop.RevealBounds;
  var restoredMusic=app.Dock.Music!;Rect musicRect=new(restoredMusic.Left,restoredMusic.Top,restoredMusic.Width,restoredMusic.Height);
  Check(restoredMusic.Bars.TrackReveal==0&&!restoredMusic.Bars.Track.Available&&restoredMusic.CaptureRunning&&restoredMusic.MetadataRunning,
   "reveal starts expanded with live audio and a fresh session check instead of stale cached artwork");
  Check(desktop.RevealAlignmentError<.001,"snapshot and live client origins align to the same physical pixels");
  Check(SnapshotArtworkPixels(Path.Combine(store.Root,"reveal-session-pending.png"))==0,"unverified cached artwork is absent while a fresh session is pending");
  string quietFrame=WaveFingerprint();tone.PlayLooping();
  bool fixedBounds=true,idleUntilComplete=true,waveResponded=false,pixelsChanged=false;
  var frames=new List<double>();for(int i=0;i<40;i++){
   Pump(20);frames.Add(desktop.RevealScale);
   if(desktop.Revealing){
    fixedBounds&=desktop.RevealAlignmentError<.001&&desktop.RevealBounds==overlayRect&&new Rect(app.Dock.Left,app.Dock.Top,app.Dock.Width,app.Dock.Height)==dockRect&&new Rect(restoredMusic.Left,restoredMusic.Top,restoredMusic.Width,restoredMusic.Height)==musicRect;
    idleUntilComplete&=!app.Dock.Surface.BackgroundAnimating&&!app.Dock.Accessories.Animating&&restoredMusic.CaptureRunning&&restoredMusic.MetadataRunning&&app.Dock.Opacity==0&&restoredMusic.Opacity==0&&!app.Dock.IsHitTestVisible;
    if(i>=8){waveResponded|=Enumerable.Range(0,36).Any(n=>restoredMusic.Bars.BandLevel(n)>.0001);pixelsChanged|=WaveFingerprint()!=quietFrame;}
   }
  }
  Check(fixedBounds,"Dock, music and single snapshot HWND bounds stay fixed throughout reveal");
  Check(idleUntilComplete,"carousel, hover and controls stay paused while live audio and session checks run during reveal");
  Check(waveResponded,"real loopback audio moves bar levels before reveal completes");
  Check(pixelsChanged,"live music brush changes the visible composite pixels during reveal");
  tone.Stop();
  File.WriteAllText(Path.Combine(store.Root,"reveal-frames.json"),JsonSerializer.Serialize(frames));
  Check(frames.Max()>1.005&&frames.Max()<1.1,"unified reveal gently overshoots and rebounds; observed maximum="+frames.Max());
  Pump(550);Check(!desktop.Revealing&&app.Dock!.Opacity==1&&app.Dock.IsHitTestVisible,"reveal settles and releases frame rendering with input restored");
  Check(app.Dock!.IsVisible&&app.Dock.Surface.CachedIconCount>0&&Math.Abs(app.Dock.Surface.Motion.Position-savedPosition)<.001,"recreation restores icon cache and carousel position");
  Check(Application.Current.Windows.OfType<MusicWindow>().Any(w=>w.IsVisible&&w.Opacity==1&&!w.IsHitTestVisible),"music joins the unified reveal and restores noninteractive behavior");
  Check(restoredMusic.CaptureRunning&&restoredMusic.MetadataRunning&&!Application.Current.Windows.OfType<Window>().Any(w=>w.Title=="EurekaDock reveal"),"completion removes overlay without interrupting audio or session checks");
  Check(Math.Abs(app.Dock.Surface.Motion.Target-savedPosition)<.001,"reveal handoff does not snap a restored fractional carousel position to another slot");
  restoredMusic.Bars.SetTrack(TrackInfo.Empty);background=true;desktop.Refresh();background=false;desktop.Refresh();
  var hiddenMusic=app.Dock!.Music!;
  Check(hiddenMusic.TrackInfoHidden&&hiddenMusic.Bars.TrackInfoHidden&&hiddenMusic.Bars.TrackReveal==0&&!hiddenMusic.Bars.Track.Available&&hiddenMusic.CaptureRunning&&hiddenMusic.MetadataRunning,
   "a previously hidden cover returns directly as the expanded waveform-only layout");
  Check(SnapshotArtworkPixels(Path.Combine(store.Root,"reveal-without-artwork.png"))==0,"waveform-only snapshot contains no stale album pixels");
  Pump(150);Check(desktop.Revealing&&(hiddenMusic.Bars.TrackReveal==0||
   (hiddenMusic.Bars.Track.Available&&!ReferenceEquals(hiddenMusic.Bars.Track.Artwork,artwork))),
   "waveform-only reveal can open fresh session artwork without flashing a discarded cached cover");
  background=true;desktop.Refresh();Pump(50);background=false;desktop.Refresh();Pump(80);background=true;desktop.Refresh();Pump(650);
  Check(app.Dock==null&&!desktop.Revealing&&!Application.Current.Windows.OfType<MusicWindow>().Any(),"rapid fullscreen changes safely cancel an in-progress reveal");
  Check(MusicAudio.Status=="已关闭","cancelling reveal releases its live audio worker");
  store.Config.MusicStyle="line";store.Save();tone.PlayLooping();background=false;desktop.Refresh();Pump(350);
  var liveLine=app.Dock!.Music!;
  Check(desktop.Revealing&&liveLine.CaptureRunning&&!liveLine.MetadataRunning&&liveLine.Line.Energy>.0001,"linear wave also responds to real music before popup completion");
  background=true;desktop.Refresh();tone.Stop();store.Config.MusicStyle="bars";store.Save();
  desktop.SetHidden(true);background=false;desktop.Refresh();Pump(150);
  Check(app.Dock==null,"tray hide remains respected after leaving fullscreen");
  desktop.SetHidden(false);Pump(1350);Check(app.Dock?.IsVisible==true,"tray restore recreates the desktop display");
  store.Config.DockEnabled=false;store.Config.MusicEnabled=false;store.Save();Pump(150);
  Check(app.Dock==null,"disabling both components destroys their windows");
  store.Config.MusicEnabled=true;store.Config.DockEnabled=false;store.Save();Pump(1350);
  Check(app.Dock?.Surface.Visibility==Visibility.Hidden&&app.Dock.Music?.IsVisible==true,"recreation respects music-only mode");
  store.Config.DockEnabled=true;store.Save();Pump(150);
  background=true;desktop.Refresh();
  var switchSettings=new SettingsWindow();switchSettings.Show();switchSettings.UpdateLayout();
  System.Windows.Controls.CheckBox SuspendToggle()=>VisualChildren(switchSettings).OfType<System.Windows.Controls.CheckBox>().Single(c=>c.Name=="SuspendWhenCoveredToggle");
  var toggle=SuspendToggle();Check(toggle.IsChecked==true&&toggle.Content.ToString()!.Contains("全屏"),"General exposes the enabled coverage suspension switch in Chinese");
  toggle.IsChecked=false;toggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));Pump(1100);
  Check(!store.Config.SuspendWhenCovered&&app.Dock?.IsVisible==true&&!desktop.Suspended,"turning off the settings switch immediately restores widgets even while coverage remains active");
  var reloaded=new Store(store.Root);reloaded.Load();Check(!reloaded.Config.SuspendWhenCovered&&reloaded.Config.Items.Count==store.Config.Items.Count,"switch state persists without changing the configured apps");
  switchSettings.ChangeLanguage("en");switchSettings.UpdateLayout();
  Check(SuspendToggle().Content.ToString()!.Contains("fullscreen")&&SuspendToggle().IsChecked==false,"English General uses the same persisted switch");
  toggle=SuspendToggle();toggle.IsChecked=true;toggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));Pump(80);
  Check(store.Config.SuspendWhenCovered&&app.Dock==null&&desktop.Suspended&&MusicAudio.Status=="已关闭","turning the switch back on closes covered windows and stops music workers immediately");
  switchSettings.Close();if(ReferenceEquals(app.MainWindow,switchSettings))app.MainWindow=null;
  background=false;desktop.Refresh();Pump(1100);Check(app.Dock?.IsVisible==true&&!desktop.Revealing,"enabled suspension restores the unified popup after coverage ends");
  // Real foreground windows from a separate process exercise Win32 detection,
  // not only an injected predicate or geometry-only assertions.
  string phasePath=Path.Combine(store.Root,"coverage-phase.txt");
  using var helper=Process.Start(new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,ArgumentList={"--coverage-window-probe","--data="+Path.Combine(store.Root,"coverage-helper"),"--coverage-phase="+phasePath}})!;
  desktop.Covered=()=>FullscreenGuard.IsProcessCovered((uint)helper.Id);
  void WaitPhase(string phase){var timeout=Stopwatch.StartNew();while(timeout.ElapsedMilliseconds<6000){Pump(40);if(File.Exists(phasePath)&&File.ReadAllText(phasePath)==phase){Pump(120);return;}}throw new Exception("Coverage window phase timed out: "+phase);}
  try{
   WaitPhase("maximized");desktop.Refresh();Check(app.Dock==null&&desktop.Suspended,"actual maximized foreground window closes the Dock");
   WaitPhase("maximized-overlay");desktop.Refresh();Check(app.Dock==null&&desktop.Suspended&&MusicAudio.Status=="已关闭","a small foreground utility over a maximized app keeps Dock and audio suspended");
   WaitPhase("normal");desktop.Refresh();Check(app.Dock!=null&&!desktop.Suspended,"actual unmaximized foreground window recreates the Dock");
   WaitPhase("work-area");desktop.Refresh();Check(app.Dock==null&&desktop.Suspended,"a custom-titlebar app filling the work area suspends widgets without a maximize flag");
   WaitPhase("fullscreen");desktop.Refresh();Check(app.Dock==null&&desktop.Suspended,"actual borderless fullscreen foreground window closes the Dock");
   WaitPhase("fullscreen-overlay");desktop.Refresh();Check(app.Dock==null&&desktop.Suspended&&MusicAudio.Status=="已关闭","a small foreground utility over a fullscreen app does not wake the Dock");
   WaitPhase("minimized");desktop.Refresh();Check(app.Dock!=null&&!desktop.Suspended,"minimized fullscreen windows no longer count as desktop coverage");
   WaitPhase("finished");desktop.Covered=()=>false;desktop.Refresh();Pump(1350);
   Check(app.Dock?.IsVisible==true&&!desktop.Revealing,"display recovers after real foreground coverage ends");
  }finally{if(!helper.HasExited)helper.Kill();}
  desktop.Dispose();  File.WriteAllText(Path.Combine(store.Root,"fullscreen-results.json"),JsonSerializer.Serialize(checks,new JsonSerializerOptions{WriteIndented=true}));
 }
}

