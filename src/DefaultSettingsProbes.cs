using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace EurekaDock;

internal static class DefaultSettingsProbes {
 internal static void Run(Store store){
  var checks=new List<string>();void Check(bool ok,string name){if(!ok)throw new Exception(name);checks.Add("PASS "+name);}
  store.Load();var c=store.Config;
  Check(c.SuspendWhenCovered&&JsonSerializer.Deserialize<Configuration>("{}")!.SuspendWhenCovered,"new and existing configurations enable coverage suspension by default");
  Check(c.Items.Count==0&&c.QuickItems.Count==0,"fresh configuration contains no personal Dock or Quick Panel apps");
  Check(c.MusicColorMemory.Length==10&&Array.TrueForAll(c.MusicColorMemory,x=>x==null)&&c.BarColorMemory.Length==10&&Array.TrueForAll(c.BarColorMemory,x=>x==null),"fresh configuration contains no saved personal colors");
  Check(c.MusicColorMode=="original"&&c.BarColorMode=="solid"&&c.BarColor=="#FFFFFF","fresh visualizer colors exclude personal palette and bars default white");
  Check(c.QuickColumns==3&&c.QuickRows==3&&Math.Abs(c.IconSize-40.1320754716981)<1e-10&&Math.Abs(c.Gap-25.188679245283)<1e-10,"current icon and Quick Panel layout became defaults");
  Check(c.DockEnabled&&c.ShowAccessories&&!c.InfiniteCarousel&&c.ReverseWheel&&Math.Abs(c.VerticalFraction-.946686320754718)<1e-10,"current Dock position and input choices became defaults");
  Check(c.MusicEnabled&&c.MusicStyle=="bars"&&Math.Abs(c.MusicSensitivity-6)<1e-10&&Math.Abs(c.MusicLength-.501900803992036)<1e-10,"current music style, sensitivity and length became defaults");
  int iconsBefore=Native.DesktopIcons(-1);var app=(App)Application.Current;app.Dock=new DockWindow();app.Dock.Show();int iconsAfter=Native.DesktopIcons(-1);
  Check(iconsBefore==iconsAfter,"first startup leaves Windows desktop icon visibility unchanged");
  app.Dock.Close();File.WriteAllText(Path.Combine(store.Root,"default-settings-results.json"),JsonSerializer.Serialize(checks,new JsonSerializerOptions{WriteIndented=true}));
 }
}
