using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace EurekaDock;
internal static class WaveColorProbes{
 static IEnumerable<DependencyObject> Children(DependencyObject x){yield return x;for(int i=0;i<VisualTreeHelper.GetChildrenCount(x);i++)foreach(var c in Children(VisualTreeHelper.GetChild(x,i)))yield return c;}
 static void Pump(int ms=100){var f=new DispatcherFrame();var t=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(ms)};t.Tick+=(s,e)=>{t.Stop();f.Continue=false;};t.Start();Dispatcher.PushFrame(f);}
 static RenderTargetBitmap Shot(FrameworkElement v,string path){v.UpdateLayout();var bitmap=new RenderTargetBitmap((int)v.ActualWidth,(int)v.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(v);var p=new PngBitmapEncoder();p.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(path);p.Save(file);return bitmap;}
 internal static void Run(Store store){
  var results=new List<string>();void Check(bool ok,string name){if(!ok)throw new Exception(name);results.Add("PASS "+name);}
  store.Load();var c=store.Config;c.Language="en";c.MusicStyle="line";
  Check(c.MusicColorMode=="original"&&c.MusicColorMemory.Length==10,"original appearance and ten empty memories are defaults");
  Check(c.BarColorMode=="solid"&&c.BarColor=="#FFFFFF"&&c.BarColorMemory.Length==10,"bar wave starts white with its own empty memories");
  foreach(string hex in new[]{"#FF0000","#00FF00","#0000FF","#D987A5","#000000","#FFFFFF"}){
   var original=WaveColors.Parse(hex);var hsv=WaveColors.ToHsv(original);Check(WaveColors.Hex(WaveColors.Hsv(hsv.h,hsv.s,hsv.v))==hex,"HSV round trip "+hex);
  }
  var legacy=JsonSerializer.Deserialize<Configuration>("{\"MusicSymmetric\":false}")!;legacy.Validate();Check(legacy.MusicColorMode=="original","legacy asymmetric config loads with default color");
  var invalid=new Configuration{MusicColor="bad",MusicColorMode="bad",MusicGradient=new(){"bad"},MusicColorMemory=new[]{"bad"},BarColor="bad",BarColorMode="bad",BarGradient=new(){"bad"},BarColorMemory=new[]{"bad"}};invalid.Validate();Check(invalid.MusicColor=="#AAB7C8"&&invalid.MusicGradient.Count>=2&&invalid.MusicColorMemory.Length==10&&invalid.MusicColorMemory[0]==null&&invalid.BarColorMode=="solid"&&invalid.BarColor=="#FFFFFF"&&invalid.BarColorMemory[0]==null,"invalid color data recovers safely");
  var app=(App)Application.Current;app.Dock=new DockWindow();app.Dock.Show();var window=new SettingsWindow();window.Show();window.SelectPage(5);Pump();
  var wheel=Children(window).OfType<ColorWheel>().Single();var mode=Children(window).OfType<ComboBox>().Last();
  void Click(string label)=>Children(window).OfType<Button>().Single(b=>b.Content?.ToString()==label).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
  for(int i=0;i<10;i++){
   Click((i+1).ToString());typeof(ColorWheel).GetMethod("Choose",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(wheel,new object[]{new Point(100+85*Math.Cos(i*Math.PI/5),100+85*Math.Sin(i*Math.PI/5))});Click("Save current color");
  }
  Check(c.MusicColorMemory.All(x=>x!=null)&&c.MusicColorMemory.Distinct().Count()==10,"all ten memory slots store separate wheel colors");
  string saved=c.MusicColorMemory[0]!;Click("1");Check(c.MusicColor==saved,"memory click recalls exact color");
  Click("Red");Check(c.MusicColor=="#FF0000","common color shortcut applies its exact hex code");
  var hexInput=Children(window).OfType<TextBox>().Single();hexInput.Focus();hexInput.Text="2e9bdb";mode.Focus();Pump();
  Check(c.MusicColor=="#2E9BDB"&&hexInput.Text=="#2E9BDB","typed hex code updates wheel, preview, and configuration");
  hexInput.Focus();hexInput.Text="not-a-color";mode.Focus();Pump();Check(c.MusicColor=="#2E9BDB"&&hexInput.Text=="#2E9BDB","invalid hex code cannot overwrite saved color");
  Click("1");Check(c.MusicColor==saved,"memory still recalls color after hex input");
  Click("Use as single color");Check(c.MusicColorMode=="solid"&&WaveColors.Sample(c,0)==WaveColors.Sample(c,.37),"single color has no moving color changes");
  Click("Add current color");Check(c.MusicGradient.Last()==saved,"selected wheel color appended to gradient");
  string last=c.MusicGradient.Last();Click("Move up");Check(c.MusicGradient[^2]==last,"gradient order can be edited");
  Click("Replace with current color");Click("Use custom gradient");Check(c.MusicColorMode=="custom","custom mode applies immediately");
  Check(WaveColors.Sample(c,0)==WaveColors.Sample(c,1)&&WaveColors.Sample(c,.999999)==WaveColors.Sample(c,-.000001),"gradient wraps without a color seam");
  Check(WaveColors.Sample(c,.05)!=WaveColors.Sample(c,.25),"custom gradient contains changing colors");
  var loaded=new Store(store.Root);loaded.Load();Check(loaded.Config.MusicColorMemory.SequenceEqual(c.MusicColorMemory)&&loaded.Config.MusicGradient.SequenceEqual(c.MusicGradient)&&loaded.Config.MusicColorMode=="custom","colors memories order and mode persist");
  var scroll=Children(window).OfType<ScrollViewer>().First(x=>x.Content is StackPanel);scroll.ScrollToBottom();Pump();Shot(window,Path.Combine(store.Root,"color-settings.png"));
  var line=new MusicLine{Width=800,Height=140};var host=new Window{Content=line,Width=820,Height=180};host.Show();
  var bands=new float[48];bands[0]=bands[1]=.5f;bands[20]=.2f;bands[38]=.1f;line.Feed(bands);Pump(300);
  double peak=line.Displacement(.5);Check(Math.Abs(peak-2.05)<.01,"central peak uses normal amplitude ceiling");
  Check(line.Displacement(.5-.025)<peak*.9&&line.Displacement(.5-.0125)>peak*.9,"central peak stays rounded and pointed rather than flat");
  Check(Enumerable.Range(0,201).All(i=>Math.Abs(line.Displacement(i/200.0)-line.Displacement(1-i/200.0))<1e-9),"only symmetric shape is rendered");
  foreach(string style in new[]{"original","solid","custom"}){c.MusicColorMode=style;line.Refresh();Shot(line,Path.Combine(store.Root,"wave-"+style+".png"));}
  line.Stop();var image=Shot(line,Path.Combine(store.Root,"glow.png"));var pixels=new byte[image.PixelWidth*image.PixelHeight*4];image.CopyPixels(pixels,image.PixelWidth*4,0);int y=(int)Math.Round(line.Baseline);byte Alpha(int dy)=>pixels[((y+dy)*image.PixelWidth+400)*4+3];Check(Alpha(2)>0&&Alpha(0)>Alpha(2),"soft halo remains visible outside the main stroke");
  host.Close();var styleSelector=Children(window).OfType<ComboBox>().First();styleSelector.SelectedIndex=1;Pump();
  Check(Children(window).OfType<ColorWheel>().Count()==1&&Children(window).OfType<TextBox>().Count()==1,"bar mode exposes the same color controls");
  Check(c.MusicStyle=="bars"&&c.BarColorMode=="solid"&&c.BarColor=="#FFFFFF"&&c.MusicColorMode=="custom","bar mode defaults to white without changing line colors");
  Click("Blue");Click("Use as single color");Check(c.BarColor=="#0000FF"&&c.BarColorMode=="solid"&&c.MusicColorMode=="custom","bar edits preserve line palette");
  Click("Save current color");Click("Purple");Click("1");Check(c.BarColor=="#0000FF"&&c.BarColorMemory[0]=="#0000FF","bar mode saves and recalls a separate color memory");
  Click("Add current color");Click("Use custom gradient");Check(c.BarColorMode=="custom"&&c.BarGradient.Last()=="#0000FF","bar mode edits and applies its own custom gradient");
  var savedAgain=new Store(store.Root);savedAgain.Load();Check(savedAgain.Config.BarColor=="#0000FF"&&savedAgain.Config.BarColorMode=="custom"&&savedAgain.Config.BarColorMemory[0]=="#0000FF","bar palette persists independently");
  var barScroll=Children(window).OfType<ScrollViewer>().First(x=>x.Content is StackPanel);barScroll.ScrollToBottom();Pump();Shot(window,Path.Combine(store.Root,"bar-color-settings.png"));
  window.Close();app.Dock.Close();File.WriteAllText(Path.Combine(store.Root,"wavecolor-results.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
 }
}
