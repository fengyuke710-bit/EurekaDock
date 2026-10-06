using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Text.Json;
using System.Runtime.InteropServices;
namespace EurekaDock;
internal static class QuickProbes {
 static void Pump(int ms){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(ms)};timer.Tick+=(s,e)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
 static IEnumerable<DependencyObject> Children(DependencyObject root){yield return root;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Children(VisualTreeHelper.GetChild(root,i)))yield return child;}
 [DllImport("user32.dll")]static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")]static extern void mouse_event(uint flags,uint dx,uint dy,uint data,UIntPtr extra);
 [DllImport("user32.dll")]static extern bool GetCursorPos(out CursorPoint p);
 [StructLayout(LayoutKind.Sequential)]struct CursorPoint{public int X,Y;}
 internal static void Run(Store store){
  var results=new List<string>();void Check(bool ok,string name){if(!ok)throw new Exception(name);results.Add("PASS "+name);}
  store.Load();var c=store.Config;Check(c.QuickColumns==3&&c.QuickRows==2&&c.QuickItems.Count==0,"existing config receives empty independent quickbar");
  string exe=Environment.ProcessPath!;for(int i=0;i<6;i++)c.QuickItems.Add(new DockItem{Name="快捷应用 "+i,Target=exe});
  c.QuickColumns=5;c.QuickRows=10;c.Validate();Check(true,"maximum capacity 5 by 10 accepted");c.QuickColumns=6;bool invalid=false;try{c.Validate();}catch{invalid=true;}Check(invalid,"invalid column count rejected");c.QuickColumns=3;c.QuickRows=2;
  store.Save();var app=(App)Application.Current;app.Dock=new DockWindow();app.Dock.Show();
  var settings=new SettingsWindow();settings.Show();settings.SelectPage(3);settings.UpdateLayout();
  var grid=Children(settings).OfType<QuickGrid>().Single();var first=c.QuickItems[0];grid.Begin(0,new Point(20,20));grid.MoveTo(new Point(204,112));Check(grid.TargetIndex==5,"two-dimensional drag crosses rows and columns");grid.Finish();Pump(400);Check(c.QuickItems[5]==first,"2D reorder commits after animation");
  settings.SelectPage(3);settings.UpdateLayout();grid=Children(settings).OfType<QuickGrid>().Single();grid.Begin(0,new Point(10,10));grid.MoveTo(new Point(100,10));grid.Cancel();Check(c.QuickItems[5]==first,"cancel preserves ordering");
  c.QuickColumns=1;c.QuickRows=1;store.Save();Check(c.QuickItems.Count==6,"shrinking capacity does not delete items");
  Check(settings.ImportQuick(new[]{Environment.GetFolderPath(Environment.SpecialFolder.Windows)})==0,"full capacity rejects imports without deleting overflow");
  c.QuickColumns=3;c.QuickRows=3;string folder=Path.Combine(store.Root,"快捷目录.2026");Directory.CreateDirectory(folder);Check(settings.ImportQuick(new[]{folder})==1&&c.QuickItems.Last().Name=="快捷目录.2026","quickbar import retains Shell name");
  string marker=Path.Combine(store.Root,"quick-launched.txt");c.QuickItems[0].Arguments="--launch-marker="+StartupRegistration.Quote(marker);store.Save();
  var bar=new QuickBarWindow(app.Dock);bar.Open();Pump(1600);Check(bar.Opened&&bar.Reveal==1&&!bar.Animating,"spring settles and stops rendering");
  bar.UpdateLayout();var trayShot=new RenderTargetBitmap((int)bar.ActualWidth,(int)bar.ActualHeight,96,96,PixelFormats.Pbgra32);trayShot.Render(bar);var trayPng=new PngBitmapEncoder();trayPng.Frames.Add(BitmapFrame.Create(trayShot));using(var f=File.Create(Path.Combine(store.Root,"quickbar.png")))trayPng.Save(f);
  double pitch=c.IconSize+c.Gap;bar.HoverAt(new Point(pitch/2,pitch/2));Pump(40);
  Check(bar.IconScale(0)>1&&bar.IconScale(0)<c.HoverScale,"hover starts gradually with shared Dock damping");Pump(650);
  Check(Math.Abs(bar.IconScale(0)-c.HoverScale)<.001,"hover uses configured Dock maximum scale");
  Check(bar.IconScale(1)>1&&bar.IconScale(1)<bar.IconScale(0),"neighbour icon receives continuous distance wave");
  Check(!bar.Animating,"stationary hover stops rendering");
  bar.HoverAt(new Point(pitch*1.5,pitch/2));Pump(40);Check(bar.IconScale(0)>1&&bar.IconScale(1)>1,"wave sweep retains smooth response on both icons");
  bar.HoverAt(null);Pump(700);Check(Math.Abs(bar.IconScale(0)-1)<.001&&!bar.Animating,"mouse leave eases back and stops rendering");
  var button=Children(bar).OfType<Button>().First();button.ApplyTemplate();
  Check(button.Template.Triggers.Count==0&&Children(button).OfType<Border>().All(b=>b.Background is SolidColorBrush brush&&brush.Color.A==0),"icon template contains no blue hover or press background");
  var buttonImage=Children(button).OfType<Image>().Single();Check(buttonImage.RenderTransformOrigin==new Point(.5,1),"icon enlargement grows upwards from baseline");button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));Pump(1500);Check(File.Exists(marker)&&!bar.IsVisible,"launch receives arguments and closes quickbar");
  bar.Open();Pump(1500);bar.Dismiss();Pump(1500);Check(!bar.IsVisible&&bar.Reveal==0&&!bar.Animating,"reverse spring returns to dot and stops");
  bar.Open();Pump(1500);
  // A dedicated blank test window receives the synthetic outside click.
  var outside=new Window{Width=180,Height=120,Left=50,Top=50,Content=new TextBlock{Text="EurekaDock outside-click test"}};outside.Show();Pump(100);var click=outside.PointToScreen(new Point(80,70));GetCursorPos(out var cursor);
  try{SetCursorPos((int)click.X,(int)click.Y);mouse_event(2,0,0,0,UIntPtr.Zero);mouse_event(4,0,0,0,UIntPtr.Zero);Pump(1500);Check(!bar.Opened&&!bar.IsVisible,"real global outside click closes quickbar without consuming click");}finally{SetCursorPos(cursor.X,cursor.Y);outside.Close();}
  c.QuickColumns=5;c.QuickRows=10;c.IconSize=128;c.Gap=80;store.Save();bar.Open();Pump(1500);Check(bar.IsVisible&&Children(bar).OfType<ScrollViewer>().Single().VerticalScrollBarVisibility==ScrollBarVisibility.Auto,"large dimensions preserve capacity with scrollable viewport");bar.Dismiss();Pump(650);bar.Close();
  c.IconSize=56;c.Gap=30;c.QuickColumns=3;c.QuickRows=3;store.Save();settings.SelectPage(0);settings.SelectPage(3);settings.UpdateLayout();var image=new RenderTargetBitmap((int)settings.ActualWidth,(int)settings.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(settings);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using(var file=File.Create(Path.Combine(store.Root,"quick-settings.png")))png.Save(file);
  var restored=new Store(store.Root);restored.Load();Check(restored.Config.QuickItems.Count==7&&restored.Config.QuickColumns==3,"independent quickbar configuration persisted");settings.Close();app.Dock.Close();
  File.WriteAllText(Path.Combine(store.Root,"quick-results.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
 }
}



