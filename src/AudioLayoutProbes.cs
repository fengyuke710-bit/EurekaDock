using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;

namespace EurekaDock;

internal static class AudioLayoutProbes {
 static void Pump(int ms=150) {
  var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(ms)};
  timer.Tick+=(s,e)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);
 }
 internal static void Run(Store store) {
  var checks=new List<string>();void Check(bool condition,string name){if(!condition)throw new Exception(name);checks.Add("PASS "+name);}
  store.Load();var c=store.Config;c.DockEnabled=true;c.MusicEnabled=true;c.MusicStyle="bars";c.ShowAccessories=true;store.Save();
  var app=(App)Application.Current;app.Dock=new DockWindow();app.Dock.Show();Pump(250);
  var music=Application.Current.Windows.OfType<MusicWindow>().Single();
  Check(app.Dock.IsVisible&&music.IsVisible&&app.Dock.Surface.Visibility==Visibility.Visible&&app.Dock.Accessories.Visibility==Visibility.Visible,"Dock, visualizer and controls initially visible");
  c.DockEnabled=false;store.Save();Pump(250);
  Check(app.Dock.IsVisible&&music.IsVisible&&app.Dock.Surface.Visibility==Visibility.Hidden,"visualizer survives independent Dock hide");
  var dpi=System.Windows.Media.VisualTreeHelper.GetDpi(app.Dock);var screen=System.Windows.Forms.Screen.PrimaryScreen!.Bounds;
  double expectedMusicLeft=screen.X/dpi.DpiScaleX+(screen.Width/dpi.DpiScaleX-music.Width)/2;
  Check(Math.Abs(music.Left-expectedMusicLeft)<2,"visualizer remains screen-centered after owner relocation");
  double visualLeft=music.Left+music.Bars.HorizontalPadding;
  Check(app.Dock.Accessories.Visibility==Visibility.Visible&&app.Dock.Left<visualLeft&&Math.Abs((app.Dock.Left+app.Dock.Accessories.ActualWidth)-visualLeft)<15,"controls relocate to the visible visualizer left edge, accounting for animation padding");
  c.ShowAccessories=false;store.Save();Pump(200);
  Check(music.IsVisible&&app.Dock.Accessories.Visibility==Visibility.Hidden&&app.Dock.Surface.Visibility==Visibility.Hidden,"both Dock and controls can hide while visualizer remains");
  c.ShowAccessories=true;store.Save();Pump(200);
  Check(app.Dock.Accessories.Visibility==Visibility.Visible&&app.Dock.Left<music.Left,"controls restore beside visualizer");
  Check(Math.Abs(music.Left-expectedMusicLeft)<2,"visualizer position remains stable when controls toggle");
  var paused=new TrackInfo("Paused Song","Artist",null,false,"Album");music.Bars.SetTrack(paused);music.Bars.Feed(new float[48]);Pump(200);
  Check(music.Bars.Track.DisplayTitle=="Paused Song · Album"&&music.Bars.Track.DisplayArtist=="Artist","paused track details remain visible without audio");
  app.Dock.SetHidden(true);Pump(150);Check(!app.Dock.IsVisible&&!music.IsVisible,"temporary tray hide still hides all desktop widgets");
  app.Dock.SetHidden(false);Pump(200);Check(app.Dock.IsVisible&&music.IsVisible&&app.Dock.Surface.Visibility==Visibility.Hidden,"temporary hide restore respects separate Dock setting");
  c.DockEnabled=true;store.Save();Pump(150);Check(app.Dock.Surface.Visibility==Visibility.Visible&&music.IsVisible,"Dock restores independently");
  c.DockEnabled=false;c.MusicEnabled=false;store.Save();Pump(150);Check(!app.Dock.IsVisible&&!music.IsVisible,"disabling both desktop components hides owner and visualizer");
  app.Dock.Close();
  var line=new MusicLine();var bars=new MusicBars();var host=new Window{Width=900,Height=180,Content=line};host.Show();Pump();
  var equal=Enumerable.Repeat(.025f,48).ToArray();line.Feed(equal);bars.Feed(equal);Pump(300);
  Check(line.BandLevel(12)>line.BandLevel(2)&&line.BandLevel(22)>line.BandLevel(12),"line mid and high bands respond more to equal audio input");
  Check(bars.BandLevel(18)>bars.BandLevel(3)&&bars.BandLevel(32)>bars.BandLevel(18),"bar mid and high bands respond more to equal audio input");
  line.Stop();bars.Stop();host.Close();
  File.WriteAllText(Path.Combine(store.Root,"audio-layout-results.json"),JsonSerializer.Serialize(checks,new JsonSerializerOptions{WriteIndented=true}));
 }
}
