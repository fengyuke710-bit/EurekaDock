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
 internal static void RunCoverageWindow(string phasePath){
  var window=new Window{Title="EurekaDock fullscreen verification",Width=720,Height=420,WindowStartupLocation=WindowStartupLocation.CenterScreen};
  int stage=0;var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(1800)};
  window.Show();window.Activate();window.WindowState=WindowState.Maximized;
  Application.Current.Dispatcher.BeginInvoke(()=>File.WriteAllText(phasePath,"maximized"),DispatcherPriority.ApplicationIdle);
  timer.Tick+=(s,e)=>{
   if(++stage==1){window.WindowState=WindowState.Normal;window.Width=720;window.Height=420;window.Activate();File.WriteAllText(phasePath,"normal");}
   else if(stage==2){
    window.WindowStyle=WindowStyle.None;window.ResizeMode=ResizeMode.NoResize;
    var dpi=System.Windows.Media.VisualTreeHelper.GetDpi(window);var screen=System.Windows.Forms.Screen.PrimaryScreen!.Bounds;
    window.Left=screen.Left/dpi.DpiScaleX;window.Top=screen.Top/dpi.DpiScaleY;window.Width=screen.Width/dpi.DpiScaleX;window.Height=screen.Height/dpi.DpiScaleY;
    window.Activate();File.WriteAllText(phasePath,"fullscreen");
   }else{timer.Stop();window.Close();File.WriteAllText(phasePath,"finished");Application.Current.Shutdown();}
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
  fullscreen=false;Pump(900);Check(!music.FullscreenPaused&&music.Bars.Animating&&music.Bars.Angle>angle,"leaving fullscreen restarts the bars and record");
  store.Config.MusicStyle="line";store.Save();Pump(150);
  fullscreen=true;Pump(900);Check(music.FullscreenPaused&&!music.Line.Animating,"fullscreen also suspends the linear wave");
  fullscreen=false;Pump(900);Check(!music.FullscreenPaused&&music.IsVisible,"linear wave resumes after fullscreen");
  app.Dock.Close();app.Dock=null!;app.MainWindow=null;
  bool background=false;using var desktop=new DesktopLifecycle(app){Covered=()=>background};desktop.Start();Pump(350);
  Check(app.Dock!=null&&app.Dock.Surface.CachedIconCount>0,"lifecycle creates configured desktop windows");
  app.Dock!.Surface.Motion.Position=app.Dock.Surface.Motion.Target=app.Dock.Surface.Motion.Minimum;
  double savedPosition=app.Dock.Surface.Motion.Position;
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
  background=false;desktop.Refresh();
  Check(app.Dock!=null&&desktop.Revealing&&desktop.RevealScale<.1,"leaving fullscreen creates new windows starting at a tiny unified scale");
  var frames=new List<double>();for(int i=0;i<40;i++){Pump(20);frames.Add(desktop.RevealScale);}
  File.WriteAllText(Path.Combine(store.Root,"reveal-frames.json"),JsonSerializer.Serialize(frames));
  Check(frames.Max()>1.005&&frames.Max()<1.1,"unified reveal gently overshoots and rebounds; observed maximum="+frames.Max());
  Pump(550);Check(!desktop.Revealing&&app.Dock!.Opacity==1&&app.Dock.IsHitTestVisible,"reveal settles and releases frame rendering with input restored");
  Check(app.Dock!.IsVisible&&app.Dock.Surface.CachedIconCount>0&&Math.Abs(app.Dock.Surface.Motion.Position-savedPosition)<.001,"recreation restores icon cache and carousel position");
  Check(Application.Current.Windows.OfType<MusicWindow>().Any(w=>w.IsVisible&&w.Opacity==1&&!w.IsHitTestVisible),"music joins the unified reveal and restores noninteractive behavior");
  background=true;desktop.Refresh();Pump(50);background=false;desktop.Refresh();Pump(80);background=true;desktop.Refresh();Pump(650);
  Check(app.Dock==null&&!desktop.Revealing&&!Application.Current.Windows.OfType<MusicWindow>().Any(),"rapid fullscreen changes safely cancel an in-progress reveal");
  desktop.SetHidden(true);background=false;desktop.Refresh();Pump(150);
  Check(app.Dock==null,"tray hide remains respected after leaving fullscreen");
  desktop.SetHidden(false);Pump(1350);Check(app.Dock?.IsVisible==true,"tray restore recreates the desktop display");
  store.Config.DockEnabled=false;store.Config.MusicEnabled=false;store.Save();Pump(150);
  Check(app.Dock==null,"disabling both components destroys their windows");
  store.Config.MusicEnabled=true;store.Config.DockEnabled=false;store.Save();Pump(1350);
  Check(app.Dock?.Surface.Visibility==Visibility.Hidden&&app.Dock.Music?.IsVisible==true,"recreation respects music-only mode");
  store.Config.DockEnabled=true;store.Save();Pump(150);
  // Real foreground windows from a separate process exercise Win32 detection,
  // not only an injected predicate or geometry-only assertions.
  desktop.Covered=FullscreenGuard.IsBackgroundCovered;
  string phasePath=Path.Combine(store.Root,"coverage-phase.txt");
  using var helper=Process.Start(new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,ArgumentList={"--coverage-window-probe","--data="+Path.Combine(store.Root,"coverage-helper"),"--coverage-phase="+phasePath}})!;
  void WaitPhase(string phase){var timeout=Stopwatch.StartNew();while(timeout.ElapsedMilliseconds<6000){Pump(40);if(File.Exists(phasePath)&&File.ReadAllText(phasePath)==phase){Pump(120);return;}}throw new Exception("Coverage window phase timed out: "+phase);}
  try{
   WaitPhase("maximized");desktop.Refresh();Check(app.Dock==null&&desktop.Suspended,"actual maximized foreground window closes the Dock");
   WaitPhase("normal");desktop.Refresh();Check(app.Dock!=null&&!desktop.Suspended,"actual unmaximized foreground window recreates the Dock");
   WaitPhase("fullscreen");desktop.Refresh();Check(app.Dock==null&&desktop.Suspended,"actual borderless fullscreen foreground window closes the Dock");
   WaitPhase("finished");desktop.Covered=()=>false;desktop.Refresh();Pump(1350);
   Check(app.Dock?.IsVisible==true&&!desktop.Revealing,"display recovers after real foreground coverage ends");
  }finally{if(!helper.HasExited)helper.Kill();}
  desktop.Dispose();  File.WriteAllText(Path.Combine(store.Root,"fullscreen-results.json"),JsonSerializer.Serialize(checks,new JsonSerializerOptions{WriteIndented=true}));
 }
}

