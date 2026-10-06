using System;
using System.IO;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Text.Json;
using System.Linq;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace EurekaDock;
internal static class Probes {
 internal static void Smoke(Store store) {
  store.Load();store.Config.Items.Clear();
  foreach(var path in new[]{Environment.GetFolderPath(Environment.SpecialFolder.Windows)+@"\explorer.exe",Environment.GetFolderPath(Environment.SpecialFolder.Windows)+@"\System32\cmd.exe",Environment.GetFolderPath(Environment.SpecialFolder.Windows)+@"\System32\notepad.exe",Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),@"shell:AppsFolder"}) store.Config.Items.Add(new DockItem {Target=path,Name=Path.GetFileName(path)});
  store.Save();
  var app=(App)Application.Current;app.Dock=new DockWindow();app.Dock.Show();
  var settings=new SettingsWindow();settings.Show();
  var timer=new DispatcherTimer {Interval=TimeSpan.FromSeconds(3)};
  timer.Tick+=(s,e)=> {
   timer.Stop();
   try {
    static void Capture(FrameworkElement element,string path) {element.UpdateLayout();var image=new RenderTargetBitmap((int)element.ActualWidth,(int)element.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(element);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using var file=File.Create(path);png.Save(file);}
    Capture(settings,Path.Combine(store.Root,"settings.png"));Capture(app.Dock.Surface,Path.Combine(store.Root,"dock.png"));
    var screen=System.Windows.Forms.Screen.PrimaryScreen!.Bounds;
    File.WriteAllText(Path.Combine(store.Root,"ui-smoke.txt"),$"Settings loaded; Dock attached={app.Dock.DesktopAttached}; screen={screen}; DPI={VisualTreeHelper.GetDpi(app.Dock).DpiScaleX}; dock={app.Dock.Width}x{app.Dock.Height}; position={app.Dock.Left},{app.Dock.Top}");
    settings.Close();app.Shutdown(0);
   } catch(Exception ex) {File.WriteAllText(Path.Combine(store.Root,"ui-failure.txt"),ex.ToString());settings.Close();app.Shutdown(1);}
  };timer.Start();
 }
 internal static void Run(Store store) {
  var results=new List<string>();
  void Check(bool ok,string name) {results.Add((ok?"PASS ":"FAIL ")+name);if(!ok)throw new Exception(name);}
  store.Load();
  var finite=new Carousel();finite.Configure(false,5);finite.Wheel(-100);Check(finite.Target==-4,"finite wheel clamps at last item");
  for(int i=0;i<480;i++){finite.Step(1.0/240);CheckRange();}
  void CheckRange(){if(finite.Position < -4||finite.Position>0)throw new Exception("finite motion escaped bounds");}
  Check(finite.Position==-4&&!finite.Moving,"finite animation settles within bounds");
  finite.Wheel(100);Check(finite.Target==0,"finite wheel clamps at first item");
  finite.Dragging=true;finite.DragBy(100);Check(finite.Position==0,"drag cannot cross first item");finite.DragBy(-.25);Check(finite.Position==-.25,"drag reverses immediately after boundary");
  finite.DragBy(-100);finite.Release(-18);for(int i=0;i<480;i++)finite.Step(1.0/240);Check(finite.Position==-4&&!finite.Moving,"release inertia cannot cross last item");
  finite.Configure(false,1);finite.Wheel(-100);finite.Release(18);finite.Step(.05);Check(finite.Position==0&&finite.Target==0&&!finite.Moving,"single item remains stationary");
  finite.Configure(false,0);finite.Wheel(100);finite.Step(.05);Check(finite.Position==0,"empty finite carousel remains stationary");
  finite.Configure(true,5);finite.Wheel(20);Check(finite.Target==20,"switching back restores unlimited wheel targets");
  finite.Position=1002;finite.Target=1002;finite.Configure(false,5);Check(finite.Position==-3&&finite.Target==-3,"mode switch preserves centered item across repeated cycles");
  var hover=new HoverResponse();hover.Step(.25,1.0/144);
  Check(hover.Value>0&&hover.Value<.01,"hover entry starts gradually");
  for(int i=0;i<144;i++)hover.Step(.25,1.0/144);
  Check(hover.Value==.25&&!hover.Moving,"hover settles and permits idle");
  hover.Step(0,1.0/144);Check(hover.Value>.24&&hover.Value<.25,"hover suppression fades rather than jumps");
  for(int i=0;i<144;i++)hover.Step(0,1.0/144);
  hover.Step(.25,1.0/144);Check(hover.Value>0&&hover.Value<.01,"hover restoration after input suppression is gradual");
  var hover60=new HoverResponse();var hover240=new HoverResponse();
  for(int i=0;i<12;i++)hover60.Step(.25,1.0/60);
  for(int i=0;i<48;i++)hover240.Step(.25,1.0/240);
  Check(Math.Abs(hover60.Value-hover240.Value)<1e-10,"hover timing independent of refresh rate");
  var reversing=new HoverResponse();for(int i=0;i<8;i++)reversing.Step(.25,1.0/144);
  double beforeReverse=reversing.Value;reversing.Step(0,1.0/144);
  Check(Math.Abs(reversing.Value-beforeReverse)<.025,"hover interrupted entry retains continuity");
  var m=new Carousel();m.Wheel(12);for(int i=0;i<1500;i++)m.Step(1.0/144);
  Check(m.Position==12&&m.Velocity==0,"12 wheel ticks accumulate and settle");
  m.Wheel(8);for(int i=0;i<12;i++)m.Step(1.0/144);double v=m.Velocity;m.Wheel(-16);m.Step(1.0/144);
  Check(v>0&&m.Velocity>0&&m.Velocity<v,"reverse input first decelerates");
  for(int i=0;i<1500;i++)m.Step(1.0/144);
  Check(m.Position==4,"reverse settles on cumulative target");
  m.Dragging=true;m.Position=3.4;m.Target=3.4;m.Wheel(3);Check(m.Target==3.4,"wheel ignored during drag");
  m.Release(7);for(int i=0;i<1500;i++)m.Step(1.0/144);Check(m.Position==5,"drag inertia snaps to slot");
  Check(Carousel.Wrap(-1,9)==8,"negative wrap");
  var precision=new Carousel();for(int i=0;i<10;i++)precision.Wheel(.1);Check(precision.Target==1,"precision wheel deltas accumulate into exact slots");
  foreach(double hz in new[]{60d,144d,240d}) {var f=new Carousel();f.Wheel(7);for(int i=0;i<10*hz;i++)f.Step(1/hz);Check(f.Position==7,"settles at "+hz+" Hz");}
  store.Config.IconSize=64;store.Save();store.Save();
  File.WriteAllText(Path.Combine(store.Root,"config.json"),"{invalid");store.Load();
  Check(store.Config.IconSize==64&&Directory.GetFiles(store.Root,"*.bad-*").Length==1,"corrupt primary recovers backup and preserves original");
  File.WriteAllText(Path.Combine(store.Root,"config.json"),"{invalid");File.WriteAllText(Path.Combine(store.Root,"config.backup.json"),"{invalid");store.Load();
  Check(store.Config.IconSize==new Configuration().IconSize&&Directory.GetFiles(store.Root,"*.bad-*").Length==3,"both corrupt recover defaults and preserve originals");
  Check(!string.IsNullOrWhiteSpace(store.Verify()),"recovered primary and backup valid");
  using var window=new ProbeWindow();
  var h=window.Handle;
  results.Add("INFO WPF rendering tier "+(RenderCapability.Tier>>16));
  results.Add("INFO desktop host "+Native.DesktopHost());
  results.Add("INFO foreground "+Native.GetForegroundWindow());
  results.Add("INFO Dock DPI mode "+Native.WindowDpiMode(h)+"; desktop DPI mode "+Native.WindowDpiMode(Native.DesktopHost()));
  int attach=Native.AttachDesktop(h);
  results.Add("INFO desktop attachment result "+attach);
  if(attach==0) Native.DetachDesktop(h);
  string file=Path.Combine(store.Root,"shell-test.txt");File.WriteAllText(file,"EurekaDock test");
  string link=Path.Combine(store.Root,"test.lnk");Check(Native.CreateTestLink(Environment.ProcessPath!,link)==0,"create isolated test shortcut");
  var installed=new List<DockItem>();int appCount=Native.EnumerateApps((name,path)=>installed.Add(new DockItem {Name=name,Target=path}));
  Check(appCount>0,"enumerate installed Windows applications ("+appCount+")");
  var packaged=installed.FirstOrDefault(x=>x.Target.Contains('!'));
  var paths=new List<string>{file,store.Root,Environment.GetFolderPath(Environment.SpecialFolder.Windows)+@"\explorer.exe",link,@"shell:AppsFolder"};
  if(packaged!=null)paths.Add(packaged.Target);
  foreach(var path in paths) {
   int count=Native.ShellMenu(h,path,0,0,true);Check(count>0,"Shell menu query: "+path+" ("+count+")");
   IntPtr icon=Native.ReadIcon(path,64);Check(icon!=IntPtr.Zero,"Shell icon: "+path);if(icon!=IntPtr.Zero)Native.DeleteObject(icon);
  }
  string marker=Path.Combine(store.Root,"launched.txt");
  Check(Native.LaunchTarget(link,"--launch-marker=\""+marker+"\"",h),"Shell launch shortcut accepted");
  for(int i=0;i<50&&!File.Exists(marker);i++)System.Threading.Thread.Sleep(100);
  Check(File.Exists(marker),"launched target received arguments and exited");
  File.WriteAllText(Path.Combine(store.Root,"probe-results.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions {WriteIndented=true}));
 }
 sealed class ProbeWindow : IDisposable {
  readonly Window window=new() {Width=300,Height=100,WindowStyle=WindowStyle.None,AllowsTransparency=true,Background=Brushes.Transparent,ShowInTaskbar=false};
  public IntPtr Handle=>new WindowInteropHelper(window).EnsureHandle();
  public void Dispose()=>window.Close();
 }
}

