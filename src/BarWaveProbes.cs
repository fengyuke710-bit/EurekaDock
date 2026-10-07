using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace EurekaDock;
internal static class BarWaveProbes{
 static IEnumerable<DependencyObject> Children(DependencyObject x){yield return x;for(int i=0;i<VisualTreeHelper.GetChildrenCount(x);i++)foreach(var c in Children(VisualTreeHelper.GetChild(x,i)))yield return c;}
 static void Pump(int ms=100){var f=new DispatcherFrame();var t=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(ms)};t.Tick+=(s,e)=>{t.Stop();f.Continue=false;};t.Start();Dispatcher.PushFrame(f);}
 static void Shot(FrameworkElement v,string path){v.UpdateLayout();var b=new RenderTargetBitmap((int)v.ActualWidth,(int)v.ActualHeight,96,96,PixelFormats.Pbgra32);b.Render(v);var p=new PngBitmapEncoder();p.Frames.Add(BitmapFrame.Create(b));using var f=File.Create(path);p.Save(f);}
 internal static void Run(Store store){
  var results=new List<string>();void Check(bool ok,string name){if(!ok)throw new Exception(name);results.Add("PASS "+name);}
  store.Load();Check(store.Config.MusicStyle=="bars","new installations use the current bar-wave preference");
  store.Config.Language="en";store.Config.IconSize=80;store.Config.MusicBarScale=1;store.Config.MusicEnabled=false;
  var app=(App)Application.Current;app.Dock=new DockWindow();app.Dock.Show();var settings=new SettingsWindow();settings.Show();settings.SelectPage(5);Pump();
  var style=Children(settings).OfType<ComboBox>().First();style.SelectedIndex=0;Pump();style.SelectedIndex=1;Pump();
  var loaded=new Store(store.Root);loaded.Load();Check(loaded.Config.MusicStyle=="bars","settings style selector persists bars");
  var toggle=Children(settings).OfType<CheckBox>().Single();toggle.IsChecked=true;toggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));Pump(300);
  var music=Application.Current.Windows.OfType<MusicWindow>().Single();Check(music.Content is MusicBars&&music.IsVisible&&!music.Topmost&&!music.IsHitTestVisible,"bar view remains noninteractive desktop-owned content");
  Check(Math.Abs(music.Top+music.Bars.Baseline-(app.Dock.Top-store.Config.MusicOffset))<1,"bar baseline obeys vertical offset");
  double oldBarWidth=music.Width-2*music.Bars.HorizontalPadding-music.Bars.CoverSize*1.5-14*music.Bars.Scale,oldCover=music.Bars.CoverSize;
  store.Config.MusicBarScale=1.5;store.Save();Pump();Check(Math.Abs(music.Bars.CoverSize-oldCover*1.5)<.01,"height scale enlarges cover proportionally");
  Check(Math.Abs(music.Width-2*music.Bars.HorizontalPadding-music.Bars.CoverSize*1.5-14*music.Bars.Scale-oldBarWidth)<1,$"height scale does not stretch bar region horizontally: before={oldBarWidth}, after={music.Width-music.Bars.CoverSize*1.5-14*music.Bars.Scale}, width={music.Width}, cover={music.Bars.CoverSize}");
  var scaleSaved=new Store(store.Root);scaleSaved.Load();Check(scaleSaved.Config.MusicBarScale==1.5,"height scale persists");
  store.Config.MusicBarScale=1;store.Save();Pump();
  Check(MusicBars.VolumeResponse(.01,2)>MusicLine.VolumeResponse(.01,2)&&MusicBars.VolumeResponse(.01,2)<MusicLine.VolumeResponse(.01,2)*1.3,"quiet response gains a modest lift");
  Check(MusicBars.VolumeResponse(0,2)==0&&MusicBars.VolumeResponse(10,2)==2.05,"sensitivity lift preserves silence and amplitude ceiling");
  var info=new TrackInfo("Song","Artist · Album",null,true,"Album");Check(info.DisplayTitle=="Song · Album"&&info.DisplayArtist=="Artist","album appears after title instead of artist");
  var combined=new TrackInfo("泡沫","邓紫棋 — Xposed (Deluxe Version)",null,true);
  Check(combined.DisplayTitle=="泡沫 · Xposed (Deluxe Version)"&&combined.DisplayArtist=="邓紫棋","actual player format works when AlbumTitle is empty");
  Check(new TrackInfo("Song","Artist-One",null,true).DisplayArtist=="Artist-One","ordinary artist hyphens remain intact");
  Check(new TrackInfo("Song · Album","Artist",null,true,"Album").DisplayTitle=="Song · Album","existing album suffix is not duplicated");
  Check(new TrackInfo("Song","Artist",null,true).DisplayTitle=="Song","missing album adds no separator");
  Shot(settings,Path.Combine(store.Root,"bar-settings-en.png"));
  var bars=new MusicBars{Width=800,Height=92};var host=new Window{Content=bars,Width=820,Height=132};host.Show();Pump();
  bars.SetTrack(info);Check(bars.TitleText=="Song"&&bars.Track.DisplayArtist=="Artist","bar title excludes album while artist stays separate");bars.SetTrack(TrackInfo.Empty);
  Check(Enumerable.Range(0,36).All(i=>bars.BarHeight(i)==3),"silence rests as short blocks");
  var spectrum=new float[48];spectrum[4]=spectrum[5]=.02f;bars.Feed(spectrum);Pump(300);double quiet=bars.BarHeight(3);
  Check(quiet>3&&bars.BarHeight(21)==3,"low-frequency band moves independently without mirroring");
  spectrum[4]=spectrum[5]=.5f;bars.Feed(spectrum);Pump(300);
  Check(Math.Abs(bars.BarHeight(3)-bars.CoverSize/2)<.01&&bars.BarHeight(3)>quiet,"volume increases height with ceiling at half cover height");
  byte[] ColorPixel(){bars.UpdateLayout();var bitmap=new RenderTargetBitmap((int)bars.ActualWidth,(int)bars.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(bars);int x=(int)(bars.HorizontalPadding+bars.CoverSize*1.5+14*bars.Scale+(bars.ActualWidth-2*bars.HorizontalPadding-bars.CoverSize*1.5-14*bars.Scale-2)*3.5/36),y=(int)(bars.Baseline-2);var pixel=new byte[4];bitmap.CopyPixels(new Int32Rect(x,y,1,1),pixel,4,0);return pixel;}
  var config=store.Config;config.BarColorMode="solid";config.BarColor="#FF0000";bars.Refresh();Pump();var red=ColorPixel();config.BarColor="#0000FF";bars.Refresh();Pump();var blue=ColorPixel();
  Check(red[2]>red[0]&&blue[0]>blue[2],"bar colors follow independent single-color palette");
  config.BarColorMode="solid";config.BarColor="#FFFFFF";bars.Refresh();Pump();
  Check(bars.TrackTextColor==Colors.White,"bar title and artist default to white");
  var drawing=new DrawingGroup();using(var dc=drawing.Open()){dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(79,102,165),Color.FromRgb(183,126,116),45),null,new Rect(0,0,200,200));dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(100,246,213,146)),null,new Point(125,80),48,48);dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(100,20,25,45)),null,new Rect(0,135,200,65));}drawing.Freeze();var art=new DrawingImage(drawing);art.Freeze();
  bars.SetTrack(new TrackInfo("Evening Light","Eureka",art,true,"Preview album"));double angle=bars.Angle;Pump(250);Check(bars.Angle>angle,"record rotates slowly with track artwork");
  config.BarColor="#E07070";bars.Refresh();Pump(100);bars.UpdateLayout();
  var header=new RenderTargetBitmap((int)bars.ActualWidth,(int)bars.ActualHeight,96,96,PixelFormats.Pbgra32);header.Render(bars);
  var headerPixels=new byte[header.PixelWidth*header.PixelHeight*4];header.CopyPixels(headerPixels,header.PixelWidth*4,0);int coloredText=0;
  int headerLeft=(int)Math.Ceiling(bars.HorizontalPadding+bars.CoverSize*1.5+14*bars.Scale),headerTop=(int)Math.Max(0,bars.Baseline-bars.CoverSize),headerBottom=(int)(headerTop+bars.CoverSize*.4);
  for(int y=headerTop;y<Math.Min(headerBottom,header.PixelHeight);y++)for(int x=headerLeft;x<header.PixelWidth;x++){int at=(y*header.PixelWidth+x)*4;if(headerPixels[at+3]>100&&headerPixels[at+2]>headerPixels[at+1]+25&&headerPixels[at+2]>headerPixels[at]+25)coloredText++;}
  Check(coloredText>20,"cached title and artist glyphs render the updated palette color");
  config.BarColor="#FFFFFF";bars.Refresh();
  config.BarColorMode="custom";config.BarGradient=new(){"#FF0000","#0000FF"};bars.Refresh();Pump(150);var textBefore=bars.TrackTextColor;Pump(600);Check(bars.TrackTextColor!=textBefore,"track text follows the moving bar gradient");config.BarColorMode="solid";config.BarColor="#FFFFFF";bars.Refresh();
  for(int i=0;i<48;i++)spectrum[i]=(float)(.03+.05*(1+Math.Sin(i*.67)));bars.Feed(spectrum);Pump(300);Shot(bars,Path.Combine(store.Root,"bar-wave-demo.png"));
  bars.Feed(new float[48]);Pump(650);Check(Enumerable.Range(0,36).All(i=>bars.BarHeight(i)==3),"silence returns every bar to baseline blocks");Shot(bars,Path.Combine(store.Root,"bar-wave-silent.png"));
  bars.SetTrack(TrackInfo.Empty);Pump(150);Check(!bars.Animating,"no track and no audio stop frame rendering");Shot(bars,Path.Combine(store.Root,"bar-wave-placeholder.png"));
  bars.SetTrack(new TrackInfo(new string('W',150),new string('M',200),null,false));Pump();Shot(bars,Path.Combine(store.Root,"bar-wave-long-title.png"));Check(bars.Track.Artwork==null,"missing cover uses local placeholder");
  bars.Stop();host.Close();
  var real=Task.Run(MediaInfoReader.Read).GetAwaiter().GetResult();Check(real.result>=0,"Windows media session API is accessible");File.WriteAllText(Path.Combine(store.Root,"metadata-display.txt"),"Raw title: "+real.track.Title+"\nRaw artist: "+real.track.Artist+"\nRaw album: "+real.track.Album+"\nDisplay title: "+real.track.DisplayTitle+"\nDisplay artist: "+real.track.DisplayArtist);results.Add("INFO current session="+(real.result==1?"none":real.track.Available?"metadata available":"no track details"));
  app.Dock.SetHidden(true);Pump();Check(!music.IsVisible&&!music.Bars.Animating,"hiding Dock stops bars and record");
  app.Dock.SetHidden(false);Pump(200);Check(music.IsVisible,"restoring Dock restores bar view");
  store.Config.MusicStyle="line";store.Save();Pump();Check(music.Content is MusicLine&&!music.Bars.Animating,"switching back stops bars and restores existing line");
  store.Config.MusicEnabled=false;store.Save();Pump();Check(!music.IsVisible,"turning visualizer off hides both views");
  settings.Close();app.Dock.Close();File.WriteAllText(Path.Combine(store.Root,"barwave-results.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
 }
}


