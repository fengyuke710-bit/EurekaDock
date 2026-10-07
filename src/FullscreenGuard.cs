using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace EurekaDock;

internal static class FullscreenGuard {
 const uint NearestMonitor=2;
 [StructLayout(LayoutKind.Sequential)] struct Rect {public int Left,Top,Right,Bottom;internal Rectangle Bounds=>Rectangle.FromLTRB(Left,Top,Right,Bottom);}
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct MonitorInfo {public int Size;public Rect Monitor,Work;public uint Flags;}
 [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
 [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr window);
 [DllImport("user32.dll")] static extern bool IsIconic(IntPtr window);
 delegate bool WindowVisitor(IntPtr window,IntPtr parameter);
 [DllImport("user32.dll")] static extern bool EnumWindows(WindowVisitor visitor,IntPtr parameter);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr window,StringBuilder text,int size);
 [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window,out Rect rect);
 [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr window,uint flags);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
 [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr window,int attribute,out Rect rect,int size);
 [DllImport("dwmapi.dll",EntryPoint="DwmGetWindowAttribute")] static extern int GetDwmInt(IntPtr window,int attribute,out int value,int size);
 internal static bool CoversMonitor(Rectangle window,Rectangle monitor,bool maximizedWithCaption){
  const int tolerance=3;
  return !maximizedWithCaption&&window.Width>0&&window.Height>0&&
   window.Left<=monitor.Left+tolerance&&window.Top<=monitor.Top+tolerance&&
   window.Right>=monitor.Right-tolerance&&window.Bottom>=monitor.Bottom-tolerance;
 }
 internal static bool CoversWorkArea(Rectangle window,Rectangle work,bool maximized)=>maximized&&
  window.Width>0&&window.Height>0&&window.Left<=work.Left+3&&window.Top<=work.Top+3&&
  window.Right>=work.Right-3&&window.Bottom>=work.Bottom-3;
 internal static bool IsActive()=>Detect(false);
 internal static bool IsBackgroundCovered()=>DetectCovered(null);
 // The same detector can restrict its candidates to the external test fixture.
 internal static bool IsProcessCovered(uint process)=>DetectCovered(process);
 static bool DetectCovered(uint? onlyProcess){
  IntPtr monitor=MonitorFromWindow(Native.DesktopHost(),NearestMonitor);
  var info=new MonitorInfo{Size=Marshal.SizeOf<MonitorInfo>()};
  if(monitor==IntPtr.Zero||!GetMonitorInfo(monitor,ref info))return false;
  bool covered=false;var name=new StringBuilder(256);
  EnumWindows((window,parameter)=>{
   if(!IsWindowVisible(window)||IsIconic(window))return true;
   GetWindowThreadProcessId(window,out uint process);
   if(process==(uint)Environment.ProcessId||(onlyProcess.HasValue&&process!=onlyProcess.Value))return true;
   if(GetDwmInt(window,14,out int cloaked,4)>=0&&cloaked!=0)return true;
   long extended=Native.GetWindowLongPtr(window,-20).ToInt64();
   if((extended&(0x80L|0x08000000L))!=0)return true;
   name.Clear();GetClassName(window,name,name.Capacity);string cls=name.ToString();
   if(cls is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd")return true;
   if(DwmGetWindowAttribute(window,9,out Rect frame,Marshal.SizeOf<Rect>())<0||frame.Bounds.IsEmpty)
    if(!GetWindowRect(window,out frame))return true;
   bool maximized=IsZoomed(window),caption= (Native.GetWindowLongPtr(window,-16).ToInt64()&0x00C00000)!=0;
   // Some custom-titlebar apps fill the work area without WS_MAXIMIZE.
   covered=CoversMonitor(frame.Bounds,info.Monitor.Bounds,maximized&&caption)||CoversWorkArea(frame.Bounds,info.Work.Bounds,true);
   return !covered;
  },IntPtr.Zero);
  return covered;
 }
 static bool Detect(bool includeMaximized){
  if(Native.IsDesktopForeground())return false;
  IntPtr window=Native.GetForegroundWindow();if(window==IntPtr.Zero||!IsWindowVisible(window))return false;
  GetWindowThreadProcessId(window,out uint process);if(process==(uint)Environment.ProcessId)return false;
  IntPtr monitor=MonitorFromWindow(window,NearestMonitor);var info=new MonitorInfo{Size=Marshal.SizeOf<MonitorInfo>()};
  if(monitor==IntPtr.Zero||!GetMonitorInfo(monitor,ref info))return false;
  if(DwmGetWindowAttribute(window,9,out Rect frame,Marshal.SizeOf<Rect>())<0||frame.Bounds.IsEmpty)if(!GetWindowRect(window,out frame))return false;
  bool maximizedWithCaption=IsZoomed(window)&&(Native.GetWindowLongPtr(window,-16).ToInt64()&0x00C00000)!=0;
  return CoversMonitor(frame.Bounds,info.Monitor.Bounds,maximizedWithCaption)||
   (includeMaximized&&CoversWorkArea(frame.Bounds,info.Work.Bounds,IsZoomed(window)));
 }
}
