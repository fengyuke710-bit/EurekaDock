using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace EurekaDock;

// WPF requested coordinates may be fractional while HWNDs occupy whole pixels.
// Snapshot placement must follow the actual client geometry at the handoff.
internal static class DesktopPlacement {
 [StructLayout(LayoutKind.Sequential)] struct NativePoint {public int X,Y;}
 [StructLayout(LayoutKind.Sequential)] struct NativeRect {public int Left,Top,Right,Bottom;}
 [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] static extern bool ClientToScreen(IntPtr hwnd,ref NativePoint point);
 [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] static extern bool GetClientRect(IntPtr hwnd,out NativeRect rect);
 internal static Rect ClientBounds(Window window){
  var dpi=VisualTreeHelper.GetDpi(window);var hwnd=new WindowInteropHelper(window).Handle;
  var point=new NativePoint();
  if(hwnd!=IntPtr.Zero&&ClientToScreen(hwnd,ref point)&&GetClientRect(hwnd,out var rect))
   return new Rect(point.X/dpi.DpiScaleX,point.Y/dpi.DpiScaleY,(rect.Right-rect.Left)/dpi.DpiScaleX,(rect.Bottom-rect.Top)/dpi.DpiScaleY);
  return new Rect(window.Left,window.Top,window.Width,window.Height);
 }
}
