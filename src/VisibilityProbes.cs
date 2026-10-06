using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
namespace EurekaDock;
internal static class VisibilityProbes{
 static IEnumerable<DependencyObject> Children(DependencyObject x){yield return x;for(int i=0;i<VisualTreeHelper.GetChildrenCount(x);i++)foreach(var c in Children(VisualTreeHelper.GetChild(x,i)))yield return c;}
 static void Pump(){var f=new DispatcherFrame();var t=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(100)};t.Tick+=(s,e)=>{t.Stop();f.Continue=false;};t.Start();Dispatcher.PushFrame(f);}
 internal static void Run(Store store){
  var checks=new List<string>();void Check(bool ok,string text){if(!ok)throw new Exception(text);checks.Add("PASS "+text);}
  store.Load();var c=store.Config;c.Language="en";c.QuickColumns=1;c.QuickRows=1;
  var a=new DockItem{Name="Alpha",Target=Environment.ProcessPath!,Arguments="--test",Icon="custom.png"};
  var b=new DockItem{Name="Beta",Target=Environment.GetFolderPath(Environment.SpecialFolder.Windows)};
  c.Items.Add(a);c.Items.Add(b);store.Save();Check(!a.Hidden,"existing items default visible");
  var app=(App)Application.Current;app.Dock=new DockWindow();app.Dock.Show();var settings=new SettingsWindow();settings.Show();settings.SelectPage(2);Pump();
  var list=Children(settings).OfType<AppReorderList>().Single();list.SelectedItem=a;
  Children(settings).OfType<Button>().Single(x=>x.Content?.ToString()=="Hide selected item").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));Pump();
  Check(((System.Collections.ICollection)typeof(DockSurface).GetField("items",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(app.Dock.Surface)!).Count==1,"hidden item removed from actual Dock surface");
  Check(a.Hidden&&c.Items.Count==2&&list.Items.Count==2,"Dock hide keeps item in settings");
  Check(a.ToString().Contains("Hidden"),"hidden row is labelled");
  Children(settings).OfType<Button>().Single(x=>x.Content?.ToString()=="Show selected item").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
  Check(!a.Hidden,"Dock item restores through actual button");
  settings.SetItemHidden(a,true);
  Check(settings.AddManagedToQuick(new[]{a})==1,"hidden Dock item can be copied to panel");
  var copy=c.QuickItems[0];Check(copy.Id!=a.Id&&!copy.Hidden&&copy.Name==a.Name&&copy.Arguments==a.Arguments&&copy.Icon==a.Icon&&copy.Target==a.Target,"copy retains details with independent identity and visible state");
  Check(settings.AddManagedToQuick(new[]{a})==0,"duplicate is not added");
  Check(settings.AddManagedToQuick(new[]{b})==0,"visible capacity is respected");
  settings.SelectPage(3);Pump();var grid=Children(settings).OfType<QuickGrid>().Single();grid.Begin(0,new Point(20,20));grid.Cancel();
  Children(settings).OfType<Button>().Single(x=>x.Content?.ToString()=="Hide selected item").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
  Check(copy.Hidden&&c.QuickItems.Count==1&&a.Hidden,"panel hide retains independent entries");
  Check(settings.AddManagedToQuick(new[]{b})==1,"hidden items free visible panel capacity");
  var bar=new QuickBarWindow(app.Dock);bar.Open();Pump();
  var rendered=Children(bar).OfType<Button>().ToList();Check(rendered.Count==1&&rendered[0].ToolTip?.ToString()=="Beta","visible panel packs items without hidden gaps");bar.Close();
  settings.SetItemHidden(copy,false);Check(a.Hidden&&!copy.Hidden,"showing panel copy does not show Dock original");
  var loaded=new Store(store.Root);loaded.Load();Check(loaded.Config.Items[0].Hidden&&!loaded.Config.QuickItems[0].Hidden&&loaded.Config.QuickItems.Count==2,"visibility and both lists persist");
  settings.SelectPage(3);Pump();Check(Children(settings).OfType<Button>().Any(x=>x.Content?.ToString()=="Add from Apps"),"picker entry is available");
  c.QuickColumns=3;store.Save();c.QuickItems.Clear();store.Save();
  bool sawPicker=false;var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(100)};
  timer.Tick+=(s,e)=>{timer.Stop();var dialog=Application.Current.Windows.OfType<Window>().Single(x=>x.Owner==settings);var choices=Children(dialog).OfType<ListBox>().Single();sawPicker=choices.SelectionMode==SelectionMode.Multiple&&choices.Items.Count==2;choices.SelectedItems.Add(a);choices.SelectedItems.Add(b);Children(dialog).OfType<Button>().Single(x=>x.Content?.ToString()=="Add selected items").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));};
  timer.Start();Children(settings).OfType<Button>().Single(x=>x.Content?.ToString()=="Add from Apps").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
  Check(sawPicker&&c.QuickItems.Count==2,"picker multi-select adds actual selected items");
  Check(c.Items.Count==2&&a.Hidden&&a.Name=="Alpha","picker leaves Dock items unchanged");
  settings.Close();app.Dock.Close();File.WriteAllText(Path.Combine(store.Root,"visibility-results.json"),JsonSerializer.Serialize(checks,new JsonSerializerOptions{WriteIndented=true}));
 }
}
