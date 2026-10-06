using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
using System.Text.Json;
namespace EurekaDock;
internal static class UsabilityProbes {
 static IEnumerable<DependencyObject> Children(DependencyObject root) {yield return root;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Children(VisualTreeHelper.GetChild(root,i)))yield return child;}
 internal static void Run(Store store) {
  var results=new List<string>();void Check(bool ok,string name){if(!ok)throw new Exception(name);results.Add("PASS "+name);}
  store.Load();store.Config.MusicEnabled=false;store.Save();Check(store.Config.DockEnabled,"new config defaults to visible");
  var dock=new DockWindow();dock.Show();var window=new SettingsWindow();window.Show();window.SelectPage(1);window.UpdateLayout();
  var toggle=Children(window).OfType<CheckBox>().Single(x=>x.Content.ToString()!.StartsWith("显示 Dock"));toggle.IsChecked=false;toggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
  Check(!dock.IsVisible&&!store.Config.DockEnabled,"settings checkbox hides Dock");store.Load();Check(!store.Config.DockEnabled,"disabled state persists");
  toggle.IsChecked=true;toggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));Check(dock.IsVisible,"settings checkbox restores Dock");
  var loop=Children(window).OfType<CheckBox>().Single(x=>x.Content.ToString()!.StartsWith("循环滚动"));loop.IsChecked=false;loop.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));store.Load();Check(!store.Config.InfiniteCarousel,"finite mode toggle saves immediately");
  window.SelectPage(2);window.UpdateLayout();
  string exe=Environment.GetFolderPath(Environment.SpecialFolder.Windows)+@"\explorer.exe", link=Path.Combine(store.Root,"桌面应用.lnk");
  Marshal.ThrowExceptionForHR(Native.WriteStartupLink(exe,"/select,test",store.Root,link));
  var target=Children(window).OfType<ListBox>().Single(x=>x.AllowDrop);
  var data=new DataObject(DataFormats.FileDrop,new[]{link,exe,store.Root});
  var drop=(DragEventArgs)Activator.CreateInstance(typeof(DragEventArgs),System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic,null,new object[]{data,DragDropKeyStates.None,DragDropEffects.Copy,target,new Point(10,10)},null)!;drop.RoutedEvent=DragDrop.PreviewDropEvent;target.RaiseEvent(drop);
  Check(drop.Handled&&store.Config.Items.Count==3,"external multi-file drop imports through routed UI handler");
  Check(store.Config.Items[0].Target==link,"shortcut preserved for arguments and working directory");
  Check(window.ImportPaths(new[]{link,exe,Path.Combine(store.Root,"missing.exe")})==0,"duplicates and missing files skipped");
  Check(File.Exists(link)&&File.Exists(exe),"drag import preserves source files");
  var a=(BitmapSource)Native.Icon(new DockItem{Target=exe},64)!;var b=(BitmapSource)Native.Icon(new DockItem{Target=link},64)!;
  byte[] Pixels(BitmapSource source){var converted=new FormatConvertedBitmap(source,PixelFormats.Bgra32,null,0);var bytes=new byte[converted.PixelWidth*converted.PixelHeight*4];converted.CopyPixels(bytes,converted.PixelWidth*4,0);return bytes;}
  Check(a!=null&&b!=null&&Pixels(a).SequenceEqual(Pixels(b)),"shortcut icon matches executable resource pixels without overlay");
  window.UpdateLayout();var image=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(window);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using(var output=File.Create(Path.Combine(store.Root,"apps.png")))png.Save(output);
  window.Close();dock.Close();File.WriteAllText(Path.Combine(store.Root,"usability-results.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
 }
}


