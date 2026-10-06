using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
namespace EurekaDock;
internal static class Native {
 const string Dll="EurekaDock.Native.dll";
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct ShellFileInfo {
  public IntPtr Icon;public int IconIndex;public uint Attributes;
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)] public string DisplayName;
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=80)] public string TypeName;
 }
 [DllImport("shell32.dll",CharSet=CharSet.Unicode)] static extern IntPtr SHGetFileInfo(string path,uint attributes,out ShellFileInfo info,uint size,uint flags);
 internal static string DisplayName(string path) {
  // Ask the same Shell namespace that names desktop/Explorer items. Do not
  // substitute the shortcut's target filename or guess from EXE metadata.
  if(SHGetFileInfo(path,0,out var info,(uint)Marshal.SizeOf<ShellFileInfo>(),0x200)!=IntPtr.Zero&&!string.IsNullOrWhiteSpace(info.DisplayName))return info.DisplayName;
  string leaf=Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
  if(Path.GetExtension(leaf).Equals(".lnk",StringComparison.OrdinalIgnoreCase)||Path.GetExtension(leaf).Equals(".url",StringComparison.OrdinalIgnoreCase))leaf=Path.GetFileNameWithoutExtension(leaf);
  return string.IsNullOrWhiteSpace(leaf)?path:leaf;
 }
 [DllImport(Dll)] internal static extern void OpenExistingSettings();
 [DllImport(Dll,CharSet=CharSet.Unicode)] internal static extern IntPtr ReadCleanIcon(string path,int size);
 [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] internal static extern bool DestroyIcon(IntPtr icon);
 [DllImport(Dll,CharSet=CharSet.Unicode)] internal static extern int WriteStartupLink(string target,string args,string directory,string path);
 [DllImport(Dll,CharSet=CharSet.Unicode)] internal static extern int ReadStartupLink(string path,System.Text.StringBuilder target,int targetSize,System.Text.StringBuilder args,int argsSize,System.Text.StringBuilder directory,int directorySize);
 [DllImport(Dll)] internal static extern int DesktopIcons(int action);
 [DllImport(Dll)] internal static extern IntPtr DesktopHost();
 [DllImport(Dll)] internal static extern int AttachDesktop(IntPtr h);
 [DllImport(Dll)] internal static extern void DetachDesktop(IntPtr h);
 [DllImport(Dll)] internal static extern int WindowDpiMode(IntPtr h);
 [UnmanagedFunctionPointer(CallingConvention.StdCall,CharSet=CharSet.Unicode)] internal delegate void AppVisitor([MarshalAs(UnmanagedType.LPWStr)]string name,[MarshalAs(UnmanagedType.LPWStr)]string target);
 [DllImport(Dll)] internal static extern int EnumerateApps(AppVisitor visit);
 [DllImport(Dll,CharSet=CharSet.Unicode)] internal static extern int CreateTestLink(string target,string path);
 [DllImport(Dll)] [return:MarshalAs(UnmanagedType.Bool)] internal static extern bool IsDesktopForeground();
 [DllImport(Dll)] internal static extern void PlaceAtBottom(IntPtr hwnd);
 [DllImport(Dll,CharSet=CharSet.Unicode)] internal static extern IntPtr ReadIcon(string path,int size);
 [DllImport(Dll,CharSet=CharSet.Unicode)] [return:MarshalAs(UnmanagedType.Bool)] internal static extern bool LaunchTarget(string target,string args,IntPtr owner);
 [DllImport(Dll,CharSet=CharSet.Unicode)] internal static extern int ShellMenu(IntPtr owner,string target,int x,int y,[MarshalAs(UnmanagedType.Bool)]bool queryOnly);
 [DllImport("gdi32.dll")] [return:MarshalAs(UnmanagedType.Bool)] internal static extern bool DeleteObject(IntPtr h);
 [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] internal static extern IntPtr SetWinEventHook(uint min,uint max,IntPtr module,WinEvent callback,uint process,uint thread,uint flags);
 [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] internal static extern bool UnhookWinEvent(IntPtr hook);
 internal delegate void WinEvent(IntPtr hook,uint evt,IntPtr hwnd,int obj,int child,uint thread,uint time);
 [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] internal static extern IntPtr GetWindowLongPtr(IntPtr h,int index);
 [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")] internal static extern IntPtr SetWindowLongPtr(IntPtr h,int index,IntPtr value);
 internal static ImageSource? Icon(DockItem item,int size) {
  try {
   if(!string.IsNullOrEmpty(item.Icon)&&File.Exists(item.Icon)) {
    var custom=new BitmapImage(); custom.BeginInit(); custom.CacheOption=BitmapCacheOption.OnLoad; custom.UriSource=new Uri(Path.GetFullPath(item.Icon)); custom.DecodePixelWidth=size; custom.EndInit(); custom.Freeze(); return custom;
   }
   var clean=ReadCleanIcon(item.Target,size);
   if(clean!=IntPtr.Zero) {try {var source=Imaging.CreateBitmapSourceFromHIcon(clean,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());source.Freeze();return source;}finally{DestroyIcon(clean);}}
   var bitmap=ReadIcon(item.Target,size); if(bitmap==IntPtr.Zero) return null;
   try { var source=Imaging.CreateBitmapSourceFromHBitmap(bitmap,IntPtr.Zero,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions()); source.Freeze(); return source; }
   finally { DeleteObject(bitmap); }
  } catch(Exception ex) { App.Store.Log("Icon "+item.Target+": "+ex.Message); return null; }
 }
}
