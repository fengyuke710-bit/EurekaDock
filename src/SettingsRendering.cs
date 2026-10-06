using System;
using System.Windows;
using System.Windows.Interop;
namespace EurekaDock;
internal static class SettingsRendering {
 internal static void Stabilize(Window window) {
  window.SourceInitialized+=(s,e)=> {
   var source=HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
   if(source?.CompositionTarget!=null)source.CompositionTarget.RenderMode=RenderMode.SoftwareOnly;
  };
  window.UseLayoutRounding=true;
  window.SnapsToDevicePixels=true;
 }
}
