using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace EurekaDock;
internal static class AccessoryProbes {
 internal static void Run(Store store) {
  var results=new List<string>();
  void Check(bool ok,string name){results.Add((ok?"PASS ":"FAIL ")+name);if(!ok)throw new Exception(name);}
  Check(DockAccessories.ArrowShape(0).Bounds.Width==10&&DockAccessories.ArrowShape(.24).Bounds.Width==0,"wider arrow straightens into vertical line");
  Check(QuickBarWindow.Expansion(.22)==0&&QuickBarWindow.Expansion(1)==1&&QuickBarWindow.Expansion(1.08)>1,"tray grows after straightening and preserves spring overshoot");
  var startRect=QuickMorph.Bounds(new Point(400,240),new Rect(80,60,300,200),1,0);
  Check(Math.Abs(startRect.Width-1.65)<.001&&Math.Abs(startRect.Height-17.65)<.001,"straight stroke expands from matching capsule without vanishing");
  Check(QuickMorph.Bounds(new Point(400,240),new Rect(80,60,300,200),1,1)==new Rect(80,60,300,200),"continuous morph reaches exact final tray bounds");
  foreach(double progress in new[]{0d,.22,.4,.6,.8,1}){
   var m=new QuickMorph{Width=440,Height=280};double amount=QuickBarWindow.Expansion(progress);
   m.Update(new Point(400,240),QuickMorph.Bounds(new Point(400,240),new Rect(80,60,300,200),1,amount),1,progress,amount);
   m.Measure(new Size(440,280));m.Arrange(new Rect(0,0,440,280));m.UpdateLayout();
   var bitmap=new RenderTargetBitmap(440,280,96,96,PixelFormats.Pbgra32);bitmap.Render(m);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
   using var frameFile=File.Create(Path.Combine(store.Root,"morph-"+progress.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+".png"));encoder.Save(frameFile);
  }
  foreach(double hz in new[]{60d,144d,240d}) {
   var spring=new PopSpring();spring.SetTarget(1);double peak=0;
   for(int i=0;i<hz*2;i++){spring.Step(1/hz);peak=Math.Max(peak,spring.Value);}
   Check(peak>1.06&&peak<1.2&&spring.Value==1&&!spring.Moving,"recycle pop overshoots and settles at "+hz+"Hz");
   spring.SetTarget(0);for(int i=0;i<hz*2;i++)spring.Step(1/hz);
   Check(spring.Value==0&&!spring.Moving,"recycle hides completely without rebound ghost at "+hz+"Hz");
  }
  var interrupted=new PopSpring();interrupted.SetTarget(1);
  for(int i=0;i<12;i++)interrupted.Step(1.0/144);
  double previous=interrupted.Value;interrupted.SetTarget(0);interrupted.Step(1.0/144);
  Check(Math.Abs(previous-interrupted.Value)<.1,"rapid toggle keeps scale continuous");
  var button=new PopSpring(1);button.SetTarget(.62);for(int i=0;i<12;i++)button.Step(1.0/144);
  Check(button.Value<.9,"dot compresses on press");button.SetTarget(1);double buttonPeak=0;
  for(int i=0;i<300;i++){button.Step(1.0/144);buttonPeak=Math.Max(buttonPeak,button.Value);}
  Check(buttonPeak>1&&button.Value==1&&!button.Moving,"dot rebounds and rests on release");
  var quickClick=new PopSpring(1);quickClick.Kick(-4);quickClick.Step(1.0/144);
  Check(quickClick.Value<1,"sub-frame quick clicks still produce visible feedback");
  int original=Native.DesktopIcons(-1);
  Check(original>=0,"desktop icon view is accessible");
  try {
   Check(Native.DesktopIcons(original==1?0:1)==1-original,"desktop icons can be toggled");
   Check(Native.DesktopIcons(-1)==1-original,"desktop icon visibility reads back correctly");
  } finally {int restored=Native.DesktopIcons(original);if(restored!=original)throw new Exception("Failed to restore desktop icon visibility");}
  Check(Native.DesktopIcons(-1)==original,"desktop icon visibility restored to user's original state");
  Check(Native.Icon(DockAccessories.RecycleBin,96)!=null,"native recycle bin icon resolves");
  store.Load();store.Config.ShowRecycleBin=false;store.Save();
  var app=(App)Application.Current;app.Dock=new DockWindow();app.Dock.Show();
  var controls=app.Dock.Accessories;app.Dock.UpdateLayout();
  double originalSize=store.Config.IconSize;
  foreach(double size in new[]{24d,48d,96d}){
   store.Config.IconSize=size;app.Dock.Reload();app.Dock.UpdateLayout();
   Check(controls.Center(0).X==controls.Center(1).X&&controls.Center(2).X<controls.Center(0).X,"gear/eye stack with arrow to left at "+size);
   Check(controls.HitBounds(1).Bottom-controls.HitBounds(0).Top<=size,"controls stay within icon height at "+size);
   Check(Enumerable.Range(0,3).All(i=>controls.HitAt(controls.Center(i))==i),"independent control hit targets at "+size);
  }
  store.Config.IconSize=originalSize;app.Dock.Reload();app.Dock.UpdateLayout();
  try{
   Native.DesktopIcons(0);controls.SyncDesktopState();Check(!controls.DesktopVisible,"closed eye reflects hidden desktop");
   Native.DesktopIcons(1);controls.SyncDesktopState();Check(controls.DesktopVisible,"open eye reflects visible desktop");
  }finally{Native.DesktopIcons(original);controls.SyncDesktopState();}
  controls.HoverAt(controls.Center(0));
  int itemCount=store.Config.Items.Count;
  app.Dock.Accessories.Activate(2);
  Check(Application.Current.Windows.OfType<QuickBarWindow>().Any(w=>w.Opened)&&store.Config.Items.Count==itemCount,"bottom dot opens independent quickbar");
  store.Load();Check(!store.Config.ShowRecycleBin,"legacy recycle visibility is not toggled");
  var timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(1500)};int stage=0;
  timer.Tick+=(s,e)=>{
   try {
    if(stage==0) {
     Check(controls.HoverScale(0)>1.13&&controls.HoverScale(0)>controls.HoverScale(2),"proximity smoothly enlarges nearest control");controls.HoverAt(null);
     Check(Application.Current.Windows.OfType<QuickBarWindow>().Any(w=>w.Reveal==1),"quickbar animation settles in real UI");
     app.Dock.UpdateLayout();
     var surface=app.Dock.Content as FrameworkElement;
     var shot=new RenderTargetBitmap((int)surface!.ActualWidth,(int)surface.ActualHeight,96,96,PixelFormats.Pbgra32);shot.Render(surface);
     var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(shot));using(var f=File.Create(Path.Combine(store.Root,"controls-visible.png")))png.Save(f);
     app.Dock.Accessories.Activate(0);
     Check(Application.Current.Windows.OfType<SettingsWindow>().Any(w=>w.IsVisible),"top dot opens settings");
     foreach(var w in Application.Current.Windows.OfType<SettingsWindow>().ToArray())w.Close();
     app.Dock.Accessories.Activate(2);
    } else {
     Check(Application.Current.Windows.OfType<QuickBarWindow>().All(w=>w.Reveal==0),"second bottom-dot click hides quickbar");
     Check(!app.Dock.Accessories.Animating,"accessory rendering stops once idle");
     var controlShot=new RenderTargetBitmap((int)controls.ActualWidth,(int)controls.ActualHeight,96,96,PixelFormats.Pbgra32);controlShot.Render(controls);
     var controlPng=new PngBitmapEncoder();controlPng.Frames.Add(BitmapFrame.Create(controlShot));using(var imageFile=File.Create(Path.Combine(store.Root,"control-glyphs.png")))controlPng.Save(imageFile);
     File.WriteAllText(Path.Combine(store.Root,"accessory-results.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
     timer.Stop();app.Shutdown(0);
    }
    stage++;
   }catch(Exception ex){timer.Stop();File.WriteAllText(Path.Combine(store.Root,"accessory-failure.txt"),ex.ToString());app.Shutdown(1);}
  };timer.Start();
 }
}


