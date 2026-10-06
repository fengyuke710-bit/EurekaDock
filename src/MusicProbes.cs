using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Text.Json;
namespace EurekaDock;
internal static class MusicProbes {
 static void Pump(int ms){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(ms)};timer.Tick+=(s,e)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
 static IEnumerable<DependencyObject> Children(DependencyObject root){yield return root;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Children(VisualTreeHelper.GetChild(root,i)))yield return child;}
 static void Screenshot(FrameworkElement view,string path){view.UpdateLayout();var image=new RenderTargetBitmap((int)view.ActualWidth,(int)view.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(view);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using var f=File.Create(path);png.Save(f);}
 [System.Runtime.InteropServices.DllImport("EurekaDock.Native.dll")]static extern void MusicTestSpectrum(double frequency,[System.Runtime.InteropServices.Out]float[] bands);
 internal static void Run(Store store){
  var results=new List<string>();void Check(bool ok,string name){if(!ok)throw new Exception(name);results.Add("PASS "+name);}
  int lastPeak=-1;foreach(double frequency in new[]{240.0,1000.0,3200.0}){var bands=new float[48];MusicTestSpectrum(frequency,bands);int peakBand=Array.IndexOf(bands,bands.Max());double expectedBand=48*Math.Log(frequency/60)/Math.Log(Math.Pow(16000.0/60,.75));Check(Math.Abs(peakBand-expectedBand)<2&&peakBand>lastPeak,"FFT tone maps to its own position: "+frequency+"Hz");lastPeak=peakBand;}
  var outside=new float[48];MusicTestSpectrum(8000,outside);Check(outside.Max()<.001,"removed high frequencies no longer occupy the line");
  double lowStep=MusicLine.VolumeResponse(.02,2)-MusicLine.VolumeResponse(.01,2),midStep=MusicLine.VolumeResponse(.06,2)-MusicLine.VolumeResponse(.05,2),highStep=MusicLine.VolumeResponse(.10,2)-MusicLine.VolumeResponse(.09,2);Check(lowStep>0&&midStep>lowStep&&highStep>midStep,"equal volume increases produce progressively larger amplitude gains");Check(MusicLine.VolumeResponse(0,2)==0&&MusicLine.VolumeResponse(100,2)==2.05,"silence remains zero and maximum amplitude is bounded");
  double originalQuiet=2.05*(.1*.08+.9*.08*.08),updatedQuiet=MusicLine.VolumeResponse(.01,2);Check(updatedQuiet>originalQuiet&&updatedQuiet<originalQuiet*1.3,"quiet input gains a modest lift without raising maximum");
  store.Load();store.Config.MusicStyle="line";store.Config.MusicEnabled=false;store.Config.MusicSensitivity=2;store.Save();Check(!store.Config.MusicEnabled,"probe starts with music off");
  var app=(App)Application.Current;app.Dock=new DockWindow();app.Dock.Show();var settings=new SettingsWindow();settings.Show();settings.SelectPage(5);settings.UpdateLayout();
  var toggle=Children(settings).OfType<CheckBox>().Single(x=>x.Content.ToString()!.StartsWith("启用"));toggle.IsChecked=true;toggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));Pump(350);
  var music=Application.Current.Windows.OfType<MusicWindow>().Single();Check(music.IsVisible&&store.Config.MusicEnabled,"settings toggle creates visible owned music line");
  Check(!music.Topmost&&!music.IsHitTestVisible&&music.Owner==app.Dock,"music remains desktop-owned and noninteractive");
  store.Config.MusicLength=.6;store.Config.MusicOffset=70;store.Config.MusicAmplitude=40;store.Save();Pump(100);double expected=System.Windows.Forms.Screen.PrimaryScreen!.Bounds.Width/VisualTreeHelper.GetDpi(app.Dock).DpiScaleX*store.Config.WidthFraction*.6;
  Check(Math.Abs(music.Width-expected)<1&&Math.Abs(music.Top+music.Line.Baseline-(app.Dock.Top-70))<1,"length and vertical position apply immediately");
  app.Dock.SetHidden(true);Pump(100);Check(!music.IsVisible&&MusicAudio.Status=="已关闭","hiding Dock stops capture and hides waveform");app.Dock.SetHidden(false);Pump(300);Check(music.IsVisible,"restoring Dock restores enabled waveform");
  // Test deterministic response independently of whatever the user is playing.
  var line=new MusicLine{Width=800,Height=100};var host=new Window{Width=820,Height=140,Content=line};host.Show();var spectrum=new float[48];spectrum[5]=.04f;spectrum[22]=.02f;spectrum[39]=.01f;line.Feed(spectrum);Pump(300);Check(line.Energy>.05&&line.Animating,"audio band energy drives smooth visible wave");  double peakValue=Enumerable.Range(0,501).Max(i=>line.Displacement(i/500.0));double trough=Enumerable.Range(0,501).Min(i=>line.Displacement(i/500.0));
  Check(peakValue>0&&Math.Abs(trough)<peakValue*.3,"upward crests dominate with small outer troughs");
  double peakX=Enumerable.Range(0,501).OrderByDescending(i=>line.Displacement(i/500.0)).First()/500.0;Check(line.Displacement(peakX+.005)>0,"strong region gently lifts its neighbours");
  double quiet=peakValue;for(int i=0;i<48;i++)spectrum[i]*=4;line.Feed(spectrum);Pump(450);double louder=Enumerable.Range(0,501).Max(i=>line.Displacement(i/500.0));Check(louder>quiet*1.5,"louder audio visibly increases wave amplitude");
  double curvature=Enumerable.Range(1,499).Max(i=>Math.Abs(line.Displacement((i-1)/500.0)-2*line.Displacement(i/500.0)+line.Displacement((i+1)/500.0)));Check(curvature<.025,"broad smooth crests avoid sharp serrations");Screenshot(line,Path.Combine(store.Root,"music-wave.png"));
  line.Height=150;host.Height=190;var bass=new float[48];bass[0]=bass[1]=bass[2]=bass[3]=.5f;bass[22]=.15f;bass[38]=.1f;line.Feed(bass);Pump(250);
  Check(Enumerable.Range(0,101).All(i=>Math.Abs(line.Displacement(.5-i/200.0)-line.Displacement(.5+i/200.0))<1e-10),"wave mirrors exactly around centered source-frequency axis");
  double centerPeak=line.Displacement(.5);Check(Math.Abs(centerPeak-2.05)<.01,"axis peak maximum matches normal maximum");
  Check(Enumerable.Range(0,401).All(i=>line.Displacement(i/400.0)<=centerPeak+.001),"strong axis-band energy produces the tallest crest");
  Check(music.Line.Baseline>store.Config.MusicAmplitude*2.05,"window reserves space for raised central crest");
  Screenshot(line,Path.Combine(store.Root,"music-symmetric.png"));
  Check(line.SymmetricDisplacement(.05)<centerPeak*.9&&line.SymmetricDisplacement(.025)>centerPeak*.9,"central crest rounds off without a broad flat cap or needle tip");
  var nearby=new float[48];nearby[6]=nearby[7]=.5f;line.Feed(nearby);Pump(300);Check(line.Displacement(.5)>1,"nearby frequency energy gathers into central crest");
  line.Feed(bass);Pump(250);
  for(int i=0;i<4;i++)bass[i]=.02f;line.Feed(bass);Pump(180);Check(line.Displacement(.5)<centerPeak*.3,"axis crest falls with quieter input rather than holding its maximum");
  double beforeDrop=line.Energy;line.Feed(new float[48]);Pump(120);Check(line.Energy<beforeDrop*.10,"wave drops more than 90 percent within 120ms");Pump(600);Check(line.Energy==0&&!line.Animating,"silence decays to flat line and stops rendering");host.Close();line.Stop();
  toggle.IsChecked=false;toggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));Pump(150);Check(!music.IsVisible&&MusicAudio.Status=="已关闭","disabling hides line and releases audio worker");
  // Quiet generated tone checks the actual render loopback end to end. No captured audio is saved.
  string tone=Path.Combine(store.Root,"generated-test-tone.wav");const int rate=48000,frames=48000;
  using(var writer=new BinaryWriter(File.Create(tone))){writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+frames*2);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(rate);writer.Write(rate*2);writer.Write((short)2);writer.Write((short)16);writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(frames*2);for(int i=0;i<frames;i++){double envelope=Math.Min(1,Math.Min(i/2400.0,(frames-i)/2400.0));writer.Write((short)(Math.Sin(i*2*Math.PI*440/rate)*1000*envelope));}}
  float peak=0;using(var audio=new MusicAudio(bands=>{float v=bands.Max();if(v>peak)peak=v;})){Pump(300);Check(MusicAudio.Status.StartsWith("已连接"),"WASAPI default render loopback connects");using var player=new SoundPlayer(tone);player.Play();Pump(1300);player.Stop();}
  Check(peak>.00001,"generated tone detected through real system output loopback");results.Add("INFO observed peak="+peak);
  var restored=new Store(store.Root);restored.Load();Check(restored.Config.MusicLength==.6&&restored.Config.MusicOffset==70&&!restored.Config.MusicEnabled,"music settings persist without changing item lists");
  Pump(100);settings.SelectPage(0);settings.SelectPage(5);Screenshot(settings,Path.Combine(store.Root,"music-settings.png"));settings.Close();app.Dock.Close();
  File.WriteAllText(Path.Combine(store.Root,"music-results.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
 }
}








