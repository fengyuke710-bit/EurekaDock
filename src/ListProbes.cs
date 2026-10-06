using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Text.Json;
namespace EurekaDock;
internal static class ListProbes {
 static IEnumerable<DependencyObject> Children(DependencyObject root){yield return root;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Children(VisualTreeHelper.GetChild(root,i)))yield return child;}
 static void Pump(int milliseconds){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(milliseconds)};timer.Tick+=(s,e)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
 internal static void Run(Store store){
  var results=new List<string>();void Check(bool ok,string name){if(!ok)throw new Exception(name);results.Add("PASS "+name);}
  store.Load();store.Config.Items.Clear();store.Config.Items.Add(new DockItem{Name="回收站",Target=DockAccessories.RecycleBin.Target});
  string file=Path.Combine(store.Root,"original.txt");File.WriteAllText(file,"unchanged");
  for(int i=0;i<6;i++)store.Config.Items.Add(new DockItem{Name="应用 "+i,Target=file,Arguments="arg"+i});store.Save();
  var window=new SettingsWindow();window.Show();window.SelectPage(2);window.UpdateLayout();
  var list=Children(window).OfType<AppReorderList>().Single();
  ListBoxItem Row(int i)=>(ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(i);
  Thumb Grip(int i)=>Children(Row(i)).OfType<Thumb>().Single();
  double Y(int i)=>((TranslateTransform)Row(i).RenderTransform).Y;
  Check(Enumerable.Range(0,7).All(i=>Children(Row(i)).OfType<Thumb>().Count()==1),"one handle per row");Check(Grip(0).IsEnabled&&Grip(1).IsEnabled,"recycle bin handle enabled");
  Check(Children(Row(1)).OfType<Border>().Any(b=>b.BorderThickness.Bottom==1),"row separator present");
  list.Begin(store.Config.Items[0]);list.UpdateOffset(34);Check(Y(0)==34,"recycle bin can be dragged normally");list.Cancel();
  var item=store.Config.Items[1];string target=item.Target,args=item.Arguments;Guid id=item.Id;
  Grip(1).RaiseEvent(new DragStartedEventArgs(0,0){RoutedEvent=Thumb.DragStartedEvent});list.UpdateOffset(68);
  Check(Y(1)==68,"dragged row follows requested position without easing lag");
  Pump(90);Check(Y(2)<0&&Y(2)>-34,"neighbour animates smoothly between slots");
  list.UpdateOffset(68);list.Finish();Pump(350);window.UpdateLayout();
  Check(store.Config.Items[3].Id==id,"release commits new order after settling");
  Check(store.Config.Items[0].Target==DockAccessories.RecycleBin.Target,"unmoved recycle bin remains first");
  Check(Enumerable.Range(0,list.Items.Count).All(i=>Math.Abs(Y(i))<.001),"transforms reset after commit");
  list.Begin(store.Config.Items[2]);list.UpdateOffset(-10000);list.Cancel();Check(Enumerable.Range(0,list.Items.Count).All(i=>Y(i)==0),"cancel restores all rows without changing order");
  Check(AppReorderList.Destination(3,-10000,7)==0,"any item can move to first position");
  Check(window.Rename(item,"我的软件"),"dedicated rename accepts display name");
  Check(item.Target==target&&item.Arguments==args&&item.Id==id&&File.ReadAllText(file)=="unchanged","rename preserves original file target arguments and identity");
  Check(!window.Rename(item,"  "),"empty rename rejected");
  var read=new Store(store.Root);read.Load();Check(read.Config.Items[3].Name=="我的软件","name and order persisted together");
  window.UpdateLayout();Check(Row(1).Height==34&&list.Height>=450,"compact rows and expanded viewport");
  string folder=Path.Combine(store.Root,"App.Release.2026");Directory.CreateDirectory(folder);Check(window.ImportPaths(new[]{folder})==1&&store.Config.Items.Last().Name=="App.Release.2026","import uses full Shell folder display name");
  string shortcut=Path.Combine(store.Root,"我的桌面应用.lnk");System.Runtime.InteropServices.Marshal.ThrowExceptionForHR(Native.CreateTestLink(Environment.ProcessPath!,shortcut));Check(window.ImportPaths(new[]{shortcut})==1&&store.Config.Items.Last().Name=="我的桌面应用","shortcut import uses visible shortcut name instead of executable name");
  window.UpdateLayout();var image=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(window);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using(var stream=File.Create(Path.Combine(store.Root,"app-list.png")))png.Save(stream);
  window.Close();File.WriteAllText(Path.Combine(store.Root,"list-results.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
 }
}



