using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace EurekaDock;

internal static class PerformanceProbes {
 [DllImport("EurekaDock.Native.dll")] static extern void MusicTestSpectrum(double frequency,float[] bands);
 [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
 static void Pump(int milliseconds){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(milliseconds)};timer.Tick+=(s,e)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
 [MethodImpl(MethodImplOptions.NoInlining)]
 static void SetTrack(App app,TrackInfo track){if(app.Dock?.Music is {} music){music.FullscreenCheck=()=>false;music.Sync(true);music.Bars.SetTrack(track);}}
 static object Measure(bool countFrames){
  using var process=Process.GetCurrentProcess();process.Refresh();var cpu=process.TotalProcessorTime;
  uint ui=GetCurrentThreadId();var threads=new Dictionary<int,double>();foreach(ProcessThread thread in process.Threads)try{threads[thread.Id]=thread.TotalProcessorTime.TotalSeconds;}catch{}
  long allocated=GC.GetTotalAllocatedBytes(false);int frames=0;
  EventHandler rendered=(s,e)=>frames++;if(countFrames)CompositionTarget.Rendering+=rendered;
  var wall=Stopwatch.StartNew();Pump(6000);wall.Stop();if(countFrames)CompositionTarget.Rendering-=rendered;
  process.Refresh();var hot=new List<object>();foreach(ProcessThread thread in process.Threads)try{if(threads.TryGetValue(thread.Id,out double before)){double delta=thread.TotalProcessorTime.TotalSeconds-before;if(delta>.01)hot.Add(new{Id=thread.Id,UI=(uint)thread.Id==ui,CpuSeconds=delta});}}catch{}
  return new{CpuPercent=(process.TotalProcessorTime-cpu).TotalSeconds/wall.Elapsed.TotalSeconds/Environment.ProcessorCount*100,
   WorkingSetMB=process.WorkingSet64/1048576.0,PrivateMB=process.PrivateMemorySize64/1048576.0,
   AllocatedMBPerSecond=(GC.GetTotalAllocatedBytes(false)-allocated)/1048576.0/wall.Elapsed.TotalSeconds,
   FramesPerSecond=frames/wall.Elapsed.TotalSeconds,HotThreads=hot};
 }
 internal static void Run(Store store){
  store.Load();store.Config.MusicStyle="bars";store.Config.MusicEnabled=true;store.Config.SuspendWhenCovered=true;
  for(int i=0;i<20;i++)store.Config.Items.Add(new DockItem{Name="Performance folder "+i,Target=store.Root});
  var bands=new float[48];for(int i=0;i<10;i++)MusicTestSpectrum(400,bands);
  var fft=Stopwatch.StartNew();for(int i=0;i<1000;i++)MusicTestSpectrum(400,bands);fft.Stop();
  var pixels=new byte[64*64*4];for(int i=0;i<pixels.Length;i+=4){pixels[i]=40;pixels[i+1]=90;pixels[i+2]=170;pixels[i+3]=255;}
  var artwork=BitmapSource.Create(64,64,96,96,PixelFormats.Bgra32,null,pixels,256);artwork.Freeze();
  var fixture=new TrackInfo("Performance verification","EurekaDock",artwork,true);
  var app=(App)Application.Current;bool covered=false;
  using var desktop=new DesktopLifecycle(app){Covered=()=>covered};desktop.Start();SetTrack(app,fixture);
  var metadata=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(250)};metadata.Tick+=(s,e)=>SetTrack(app,fixture);metadata.Start();
  Pump(2000);object active=Measure(true);metadata.Stop();covered=true;desktop.Refresh();Pump(2000);
  if(app.Dock!=null||MusicAudio.Status!="已关闭")throw new Exception("Background workers did not stop");
  object suspended=Measure(false);
  File.WriteAllText(Path.Combine(store.Root,"performance-results.json"),JsonSerializer.Serialize(new{
   LogicalProcessors=Environment.ProcessorCount,FftMillisecondsPerCall=fft.Elapsed.TotalMilliseconds/1000,Active=active,Suspended=suspended
  },new JsonSerializerOptions{WriteIndented=true}));
 }
}
