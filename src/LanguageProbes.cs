using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace EurekaDock;
internal static class LanguageProbes {
 static IEnumerable<DependencyObject> Children(DependencyObject x){yield return x;for(int i=0;i<VisualTreeHelper.GetChildrenCount(x);i++)foreach(var c in Children(VisualTreeHelper.GetChild(x,i)))yield return c;}
 static void Pump(){var f=new DispatcherFrame();var t=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(80)};t.Tick+=(s,e)=>{t.Stop();f.Continue=false;};t.Start();Dispatcher.PushFrame(f);}
 static void Shot(FrameworkElement view,string file){view.UpdateLayout();var b=new RenderTargetBitmap((int)view.ActualWidth,(int)view.ActualHeight,96,96,PixelFormats.Pbgra32);b.Render(view);var p=new PngBitmapEncoder();p.Frames.Add(BitmapFrame.Create(b));using var f=File.Create(file);p.Save(f);}
 internal static void Run(Store store){
  var results=new List<string>();void Check(bool ok,string name){if(!ok)throw new Exception(name);results.Add("PASS "+name);}
  store.Load();Check(store.Config.Language=="zh-CN","existing configuration defaults to Chinese");
  store.Config.Items.Add(new DockItem{Name="Test app",Target=Environment.ProcessPath!});store.Config.QuickItems.Add(new DockItem{Name="Test quick app",Target=Environment.ProcessPath!});store.Save();
  var before=JsonSerializer.Serialize(store.Config.Items)+JsonSerializer.Serialize(store.Config.QuickItems);
  var app=(App)Application.Current;app.Dock=new DockWindow();app.Dock.Show();
  var settings=new SettingsWindow();settings.Show();Pump();
  var selector=Children(settings).OfType<ComboBox>().Single();selector.SelectedIndex=1;Pump();
  Check(store.Config.Language=="en"&&settings.Title=="EurekaDock Settings","General language selector immediately translates window");
  var loaded=new Store(store.Root);loaded.Load();Check(loaded.Config.Language=="en","language persists to configuration");
  foreach(string language in new[]{"en","zh-CN"}){
   settings.ChangeLanguage(language);
   for(int i=0;i<9;i++){
    settings.SelectPage(i);Pump();settings.UpdateLayout();
    string[] texts=Children(settings).OfType<TextBlock>().Select(x=>x.Text).Where(x=>!string.IsNullOrEmpty(x)).ToArray();
    if(language=="en")Check(!texts.Any(x=>Regex.IsMatch(x,@"[\p{IsCJKUnifiedIdeographs}]")&&!x.Contains("简体中文")&&!x.Contains("界面语言")),"English page "+i+" has no untranslated app text");
    Check(texts.Length>3,"page renders: "+language+" "+i);
    Shot(settings,Path.Combine(store.Root,language+"-"+i+".png"));
    if(i==7){Check(texts.Any(x=>x.Contains(language=="en"?"6. Startup":"6. 启动")),"complete guide is available in "+language);var scroll=Children(settings).OfType<ScrollViewer>().First(x=>x.Content is StackPanel);scroll.ScrollToBottom();Pump();Shot(settings,Path.Combine(store.Root,language+"-guide-bottom.png"));}
   }
  }
  settings.ChangeLanguage("en");settings.SelectPage(2);Pump();
  Children(settings).OfType<AppReorderList>().Single().SelectedIndex=0;
  foreach(string label in new[]{"Rename","Edit details"}){
   bool translated=false;var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(100)};
   timer.Tick+=(s,e)=>{timer.Stop();var dialog=Application.Current.Windows.OfType<Window>().Single(w=>w.Owner==settings);
    translated=!Regex.IsMatch(dialog.Title,@"[\p{IsCJKUnifiedIdeographs}]")&&!Children(dialog).OfType<TextBlock>().Any(t=>Regex.IsMatch(t.Text,@"[\p{IsCJKUnifiedIdeographs}]"));
    Shot(dialog,Path.Combine(store.Root,"en-dialog-"+label.Replace(" ","-")+".png"));dialog.Close();};
   timer.Start();Children(settings).OfType<Button>().Single(b=>b.Content?.ToString()==label).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
   Check(translated,"English dialog: "+label);
  }
  settings.SelectPage(0);settings.Width=780;settings.Height=520;Pump();Shot(settings,Path.Combine(store.Root,"en-small.png"));
  Check(before==JsonSerializer.Serialize(store.Config.Items)+JsonSerializer.Serialize(store.Config.QuickItems),"language changes preserve both app lists");
  settings.Close();var reopened=new SettingsWindow();reopened.Show();Pump();Check(reopened.Title=="EurekaDock Settings","reopened Settings retains English");reopened.Close();
  store.Config.Language="unknown";store.Config.Validate();Check(store.Config.Language=="zh-CN","unsupported language falls back safely");
  app.Dock.Close();File.WriteAllText(Path.Combine(store.Root,"language-results.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
 }
}
