using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Text.Json;
namespace EurekaDock;
internal static class EdgeProbes {
 internal static void Run(Store store){
  var results=new List<string>();void Check(bool ok,string name){if(!ok)throw new Exception(name);results.Add("PASS "+name);}
  store.Load();store.Config.InfiniteCarousel=false;store.Config.DockEnabled=true;
  if(store.Config.Items.Count<12){store.Config.Items.Clear();for(int i=0;i<24;i++)store.Config.Items.Add(new DockItem{Name="Edge "+i,Target=Environment.ProcessPath!});}
  foreach(double span in new[]{3.1,3.49,3.51,3.9,6.25}){
   var motion=new Carousel();motion.Configure(false,24,span);
   foreach(double end in new[]{motion.Minimum,motion.Maximum}){
    motion.Position=motion.Target=end;motion.Release(0);for(int n=0;n<300;n++)motion.Step(1.0/144);
    Check(motion.Position==end&&!motion.Moving,"fractional endpoint remains exact after release: "+span+" "+end);
   }
  }
  var dock=new DockWindow();dock.Show();dock.UpdateLayout();var surface=dock.Surface;
  List<(Rect rect,DockItem item)> Hits()=>(List<(Rect,DockItem)>)typeof(DockSurface).GetField("hits",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(surface)!;
  void Capture(string name){surface.InvalidateVisual();surface.UpdateLayout();var image=new RenderTargetBitmap((int)surface.ActualWidth,(int)surface.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(surface);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using var file=File.Create(Path.Combine(store.Root,name+".png"));png.Save(file);}
  void ClickReleaseAtEdge(double end){
   surface.Motion.Position=surface.Motion.Target=end;surface.Motion.Velocity=0;
   typeof(DockSurface).GetField("pressed",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(surface,true);
   var up=new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice,0,System.Windows.Input.MouseButton.Left){RoutedEvent=UIElement.MouseLeftButtonUpEvent};
   surface.RaiseEvent(up);for(int n=0;n<300;n++)surface.Motion.Step(1.0/144);
   Check(surface.Motion.Position==end&&surface.Motion.Target==end,"actual click release preserves fractional edge "+end);
   surface.CancelDrag();for(int n=0;n<300;n++)surface.Motion.Step(1.0/144);
   Check(surface.Motion.Position==end,"cancel/context-menu cleanup preserves edge "+end);
  }
  ClickReleaseAtEdge(surface.Motion.Maximum);ClickReleaseAtEdge(surface.Motion.Minimum);
  surface.Motion.Position=surface.Motion.Target=surface.Motion.Maximum;surface.Motion.Velocity=0;Capture("first-edge");
  var first=store.Config.Items.First(i=>i.Available);Check(Math.Abs(Hits().First(h=>h.item.Id==first.Id).rect.Left-2)<.01,"first icon aligns to left edge with 2 DIP padding");
  surface.Motion.Wheel(1);surface.Motion.Step(.04);Check(surface.Motion.VisualPosition>surface.Motion.Position&&surface.Motion.VisualPosition-surface.Motion.Position<.28,"outward wheel gives bounded visual pull at start");
  for(int i=0;i<400;i++)surface.Motion.Step(1.0/144);Check(!surface.Motion.Moving&&surface.Motion.VisualPosition==surface.Motion.Maximum,"start pull returns exactly and becomes idle");
  surface.Motion.Position=surface.Motion.Target=surface.Motion.Minimum;surface.Motion.Velocity=0;Capture("last-edge");var last=store.Config.Items.Last(i=>i.Available);
  Check(Math.Abs(Hits().First(h=>h.item.Id==last.Id).rect.Right-(surface.ActualWidth-2))<.01,"last icon aligns to right edge with 2 DIP padding");
  surface.Motion.Wheel(-1);surface.Motion.Step(.04);Check(surface.Motion.VisualPosition<surface.Motion.Position,"end pull responds in opposite direction");for(int i=0;i<400;i++)surface.Motion.Step(1.0/144);Check(!surface.Motion.Moving&&surface.Motion.VisualPosition==surface.Motion.Minimum,"end pull returns exactly and becomes idle");
  var data=Enumerable.Repeat((byte)255,32*32*4).ToArray();var source=BitmapSource.Create(32,32,96,96,PixelFormats.Pbgra32,null,data,128);var icon=new FogIcon(source,3);var pixels=new byte[icon.Width*icon.Height*4];
  icon.Compose(pixels,icon.Padded(new Rect(2,0,32,32)),500,0,100);int sample=((icon.Padding+16)*icon.Width+icon.Padding+16)*4;
  Check(pixels[sample+3]==255,"left endpoint has full alpha and sharp resource");icon.Compose(pixels,icon.Padded(new Rect(466,0,32,32)),500,100,0);Check(pixels[sample+3]==255,"right endpoint has full alpha and sharp resource");
  dock.Close();File.WriteAllText(Path.Combine(store.Root,"edge-results.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
 }
}
