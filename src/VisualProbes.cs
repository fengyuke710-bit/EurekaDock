using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Text.Json;
namespace EurekaDock;
internal static class VisualProbes {
 internal static void Run(Store store) {
  var checks=new List<string>();
  void Check(bool ok,string label) {if(!ok)throw new Exception(label);checks.Add("PASS "+label);}
  store.Load();
  var bytes=new byte[32*32*4];
  for(int i=0;i<bytes.Length;i+=4){bytes[i]=40;bytes[i+1]=210;bytes[i+2]=40;bytes[i+3]=255;}
  var source=BitmapSource.Create(32,32,96,96,PixelFormats.Pbgra32,null,bytes,128);
  var icon=new FogIcon(source,0);var pixels=new byte[icon.Width*icon.Height*4];var prior=new byte[pixels.Length];
  var rect=icon.Padded(new Rect(16,10,32,32));
  icon.Compose(pixels,rect,500,100);
  int p=((icon.Padding+16)*icon.Width+icon.Padding+16)*4;
  Check(pixels[p+3]>0&&pixels[p+3]<255,"fog produces partial alpha");
  // Compare in premultiplied space: unpremultiplication magnifies normal
  // one-byte rounding error at low opacity.
  Check(Math.Abs(pixels[p+1]-pixels[p+3]*210.0/255)<=1&&Math.Abs(pixels[p]-pixels[p+3]*40.0/255)<=1,"saturated green retains hue within 8-bit rounding under alpha fade");
  icon.Compose(prior,rect,500,100);rect.X+=.1;icon.Compose(pixels,rect,500,100);
  Check(pixels.Zip(prior,(a,b)=>Math.Abs(a-b)).Max()<=1,"subpixel movement has continuous alpha, no bounds-dependent jump");
  var blurred=new FogIcon(source,4);pixels=new byte[blurred.Width*blurred.Height*4];
  blurred.Compose(pixels,blurred.Padded(new Rect(-16,0,32,32)),500,100);
  Check(Enumerable.Range(0,pixels.Length/4).All(i=>pixels[i*4]<=pixels[i*4+3]&&pixels[i*4+1]<=pixels[i*4+3]&&pixels[i*4+2]<=pixels[i*4+3]),"blur and fade preserve premultiplied RGBA");
  Check(FogIcon.Fade(0,100)==0&&FogIcon.Fade(100,100)==1&&FogIcon.Fade(.1,100)<.000001,"fade reaches zero before clipping, with soft endpoints");
  store.Config.ReverseWheel=true;store.Save();store.Load();Check(store.Config.ReverseWheel,"reverse wheel setting persists");
  var weixin=new DockItem {Target=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Tencent","Weixin","Weixin.exe"),Name="Weixin"};
  var real=Native.Icon(weixin,96) as BitmapSource;
  Check(real!=null,"real Weixin icon decoded");
  checks.Add("INFO Weixin pixel format "+real!.Format);
  var realFog=new FogIcon(real,5);
  for(int step=0;step<5;step++) {
   var visual=new DrawingVisual();
   using(var dc=visual.RenderOpen()) {
    dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(25,29,34)),null,new Rect(0,0,420,160));
    var frame=new FogFrame(realFog);frame.Draw(dc,realFog.Padded(new Rect(-48+step*36,32,80,80)),420,180);
   }
   var shot=new RenderTargetBitmap(420,160,96,96,PixelFormats.Pbgra32);shot.Render(visual);
   Save(shot,Path.Combine(store.Root,"fog-"+step+".png"));
  }
  var app=(App)Application.Current;app.Dock=new DockWindow();app.Dock.Show();
  app.Dock.Surface.Scroll(1);Check(app.Dock.Surface.Motion.Target==-1,"reverse vertical wheel changes direction");
  app.Dock.Surface.Scroll(1,false);Check(app.Dock.Surface.Motion.Target==0,"horizontal scroll keeps natural direction");
  var settings=new SettingsWindow {Left=100,Top=100,WindowStartupLocation=WindowStartupLocation.Manual};settings.Show();
  var timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(800)};
  int stage=0;System.Drawing.Bitmap? baseline=null;
  var positions=new[]{new Point(1900,100),new Point(-400,100),new Point(100,-300),new Point(100,950)};
  timer.Tick+=(s,e)=> {
   try {
    if(stage==0) {
     Check(HwndSource.FromHwnd(new WindowInteropHelper(settings).Handle)!.CompositionTarget.RenderMode==RenderMode.SoftwareOnly,"settings uses per-window compatibility rendering");
     baseline=Capture(settings);baseline.Save(Path.Combine(store.Root,"settings-before.png"));
    } else if(stage<9&&stage%2==1) {settings.Left=positions[(stage-1)/2].X;settings.Top=positions[(stage-1)/2].Y;}
    else {settings.Left=100;settings.Top=100;}
    if(stage==9) {
     using var after=Capture(settings);after.Save(Path.Combine(store.Root,"settings-after.png"));
     int differences=0;
     foreach(var pt in new[]{new System.Drawing.Point(30,180),new System.Drawing.Point(400,350),new System.Drawing.Point(600,420)})if(baseline!.GetPixel(pt.X,pt.Y)!=after.GetPixel(pt.X,pt.Y))differences++;
     Check(differences==0,"settings background colors unchanged after four offscreen round trips");
     File.WriteAllText(Path.Combine(store.Root,"visual-results.json"),JsonSerializer.Serialize(checks,new JsonSerializerOptions{WriteIndented=true}));
     timer.Stop();baseline?.Dispose();settings.Close();app.Shutdown(0);
    }
    stage++;
   } catch(Exception ex) {timer.Stop();File.WriteAllText(Path.Combine(store.Root,"visual-failure.txt"),ex.ToString());baseline?.Dispose();settings.Close();app.Shutdown(1);}
  };timer.Start();
 }
 static void Save(BitmapSource image,string path) {var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using var file=File.Create(path);png.Save(file);}
 static System.Drawing.Bitmap Capture(Window window) {
  var origin=window.PointToScreen(new Point(0,0));var dpi=VisualTreeHelper.GetDpi(window);
  var bitmap=new System.Drawing.Bitmap((int)(window.ActualWidth*dpi.DpiScaleX),(int)(window.ActualHeight*dpi.DpiScaleY));
  using var graphics=System.Drawing.Graphics.FromImage(bitmap);
  graphics.CopyFromScreen((int)origin.X,(int)origin.Y,0,0,bitmap.Size);return bitmap;
 }
}
