using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace EurekaDock;
internal static class IconEffectsProbes {
 static void Pump(int ms){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(ms)};timer.Tick+=(s,e)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
 static IEnumerable<DependencyObject> Children(DependencyObject root){yield return root;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Children(VisualTreeHelper.GetChild(root,i)))yield return child;}
 static void Shot(FrameworkElement view,string path){view.UpdateLayout();var bitmap=new RenderTargetBitmap((int)Math.Ceiling(view.ActualWidth),(int)Math.Ceiling(view.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(view);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(path);png.Save(file);}
 internal static void CheckReflectionEdges(Store store,List<string> checks){
  void Check(bool condition,string message){if(!condition)throw new Exception(message);checks.Add("PASS "+message);}
  var pixels=new byte[64*64*4];for(int i=0;i<pixels.Length;i+=4){pixels[i]=32;pixels[i+1]=180;pixels[i+2]=70;pixels[i+3]=255;}
  var source=BitmapSource.Create(64,64,96,96,PixelFormats.Pbgra32,null,pixels,256);source.Freeze();
  string iconPath=Path.Combine(store.Root,"uniform-edge-icon.png");var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(source));using(var file=File.Create(iconPath))encoder.Save(file);
  var c=store.Config;c.MusicEnabled=false;c.DockEnabled=true;c.IconClickFeedback="default";c.IconDisplayEffect="reflection";c.IconSize=64;c.Gap=32;c.HoverScale=1;c.FadeWidth=128;c.Blur=0;c.InfiniteCarousel=true;
  c.Items=Enumerable.Range(0,20).Select(i=>new DockItem{Name="Edge sample "+i,Target=Environment.ProcessPath!,Icon=iconPath}).ToList();store.Save();
  var app=(App)Application.Current;app.Dock=new DockWindow();var dock=app.Dock;dock.Show();Pump(120);var surface=dock.Surface;
  var samples=new List<object>();
  (int body,int reflection) Sample(double position,double x,string name){
   surface.Motion.Position=surface.Motion.Target=position;surface.Motion.Velocity=0;surface.InvalidateVisual();Pump(40);surface.UpdateLayout();
   var recorded=(DrawingGroup)typeof(DockSurface).GetField("scene",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(surface)!;
   var visual=new DrawingVisual();using(var dc=visual.RenderOpen())dc.DrawDrawing(recorded);
   var bitmap=new RenderTargetBitmap((int)Math.Ceiling(surface.ActualWidth),(int)Math.Ceiling(surface.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(visual);
   var data=new byte[bitmap.PixelWidth*bitmap.PixelHeight*4];bitmap.CopyPixels(data,bitmap.PixelWidth*4,0);
   double bottom=surface.ActualHeight-12-dock.IconReflectionSpace;
   int column=Math.Clamp((int)Math.Round(x),0,bitmap.PixelWidth-1),bodyY=(int)(bottom-32),reflectionY=(int)(IconEffects.ReflectionBounds(new Rect(x-32,bottom-64,64,64)).Top+3);
   int body=data[(bodyY*bitmap.PixelWidth+column)*4+3],reflected=data[(reflectionY*bitmap.PixelWidth+column)*4+3];
   var layer=(DrawingGroup)typeof(DockSurface).GetField("reflections",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(surface)!;
   var hits=(List<(Rect rect,DockItem item)>)typeof(DockSurface).GetField("hits",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(surface)!;
   samples.Add(new{name,x,body,reflection=reflected,position=surface.Motion.Position,visualPosition=surface.Motion.VisualPosition,firstCenter=hits.FirstOrDefault().rect.X+32,topLevelMask=layer.OpacityMask!=null,maskLeft=layer.Bounds.Left,maskRight=layer.Bounds.Right});File.WriteAllText(Path.Combine(store.Root,"reflection-edge-pixels.json"),JsonSerializer.Serialize(samples,new JsonSerializerOptions{WriteIndented=true}));
   var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(Path.Combine(store.Root,name+".png")))png.Save(file);
   return(body,reflected);
  }
  double width=surface.ActualWidth,pitch=c.IconSize+c.Gap;
  var center=Sample(0,width/2,"reflection-center");
  Check(center.body>=250&&center.reflection>20,"center reference renders a solid icon and a visible reflection at sampled coordinates");
  var left=Sample((32-width/2)/pitch,32,"reflection-left-edge");
  Check(left.body<center.body*.3,"infinite-carousel icon body fades at the left edge");
  Check(Math.Abs((left.reflection-1.0)/(center.reflection-1)-(left.body-1.0)/(center.body-1))<.06,"left-edge reflection fades at the same rate as its icon body");
  var right=Sample((width-32-width/2)/pitch,width-32,"reflection-right-edge");
  Check(Math.Abs((right.reflection-1.0)/(center.reflection-1)-(right.body-1.0)/(center.body-1))<.06,"right-edge reflection fades at the same rate as its icon body");
  var subpixel=Sample((32.2-width/2)/pitch,32,"reflection-subpixel");Check(Math.Abs(subpixel.reflection-left.reflection)<=2,"reflection edge fading is continuous under subpixel movement");
  c.InfiniteCarousel=false;store.Save();Pump(80);
  var start=Sample(surface.Motion.Maximum,34,"reflection-finite-start");Check(start.body>=250&&start.reflection>=center.reflection-2,"finite start endpoint preserves its body and reflection without fading");
  var end=Sample(surface.Motion.Minimum,width-34,"reflection-finite-end");Check(end.body>=250&&end.reflection>=center.reflection-2,"finite end endpoint preserves its body and reflection without fading");
  var pulling=Sample(surface.Motion.Maximum-.25,10,"reflection-finite-transition");
  Check(Math.Abs((pulling.reflection-1.0)/(center.reflection-1)-(pulling.body-1.0)/(center.body-1))<.08,"reflection follows the finite mode's gradually restored edge fade");
  dock.Close();
 }
 internal static void Run(Store store){
  var checks=new List<string>();void Check(bool condition,string message){if(!condition)throw new Exception(message);checks.Add("PASS "+message);}
  store.Load();var c=store.Config;
  var legacy=JsonSerializer.Deserialize<Configuration>("{}")!;
  Check(c.IconClickFeedback=="default"&&c.IconDisplayEffect=="default"&&legacy.IconClickFeedback=="default"&&legacy.IconDisplayEffect=="default","new and legacy configurations retain existing icon behavior");
  legacy.IconClickFeedback="unknown";legacy.IconDisplayEffect=null!;legacy.Validate();Check(legacy.IconClickFeedback=="default"&&legacy.IconDisplayEffect=="default","unsupported effect names fall back without rejecting the configuration");
  Check(IconEffects.Bounce(0)==0&&IconEffects.Bounce(.9)==0&&IconEffects.Bounce(4)==0,"bounce has a finite duration and exact resting endpoints");
  Check(IconEffects.Bounce(.21)>IconEffects.Bounce(.56)&&IconEffects.Bounce(.56)>IconEffects.Bounce(.8),"three launch hops diminish naturally");
  Check(Enumerable.Range(0,901).All(i=>IconEffects.Bounce(i/1000.0)>=0&&IconEffects.Bounce(i/1000.0)<=1),"bounce never sinks below its resting baseline or exceeds its bounded height");
  var pixels=new byte[64*64*4];for(int y=0;y<64;y++)for(int x=0;x<64;x++){int at=(y*64+x)*4;pixels[at]=30;pixels[at+1]=(byte)(x%8<4?230:40);pixels[at+2]=50;pixels[at+3]=255;}
  var source=BitmapSource.Create(64,64,96,96,PixelFormats.Pbgra32,null,pixels,256);source.Freeze();
  var fog=new FogIcon(source,2);Check(ReferenceEquals(fog.Reflection,fog.Reflection)&&fog.Reflection.Image.IsFrozen,"reflection is generated once and cached as an immutable bitmap");
  var reflection=fog.Reflection.Image;var reflected=new byte[reflection.PixelWidth*reflection.PixelHeight*4];reflection.CopyPixels(reflected,reflection.PixelWidth*4,0);
  int Alpha(int x,int y)=>reflected[(y*reflection.PixelWidth+x)*4+3];
  int Span(int y)=>Enumerable.Range(0,reflection.PixelWidth).Count(x=>Alpha(x,y)>0);
  Check(Span(reflection.PixelHeight*2/3)>Span(1),"reflection widens downwards towards the viewer instead of a flat axis mirror");
  Check(Alpha(reflection.PixelWidth/2,1)>Alpha(reflection.PixelWidth/2,reflection.PixelHeight/2)&&Alpha(reflection.PixelWidth/2,reflection.PixelHeight-1)==0,"reflection progressively fades to fully transparent pixels");
  Check(Enumerable.Range(0,reflected.Length/4).All(i=>reflected[i*4]<=reflected[i*4+3]&&reflected[i*4+1]<=reflected[i*4+3]&&reflected[i*4+2]<=reflected[i*4+3]),"blur and fade preserve premultiplied color without colored fringes");
  double Contrast(int y){double total=0;int n=0;for(int x=reflection.PixelWidth/4;x<reflection.PixelWidth*3/4-1;x++){int a=(y*reflection.PixelWidth+x)*4,b=a+4;if(reflected[a+3]<5||reflected[b+3]<5)continue;total+=Math.Abs(reflected[a+1]/(double)reflected[a+3]-reflected[b+1]/(double)reflected[b+3]);n++;}return total/Math.Max(1,n);}
  Check(Contrast(1)>Contrast(reflection.PixelHeight/2),"fine image detail becomes progressively blurrier downwards");
  var art=new ApplicationIcon(source,100,true);var artHost=new Window{Content=new Border{Padding=new Thickness(30,30,30,85),Child=art},SizeToContent=SizeToContent.WidthAndHeight};artHost.Show();Pump(80);Shot(artHost,Path.Combine(store.Root,"reflection-detail.png"));artHost.Close();
  c.MusicEnabled=false;c.DockEnabled=true;c.ShowAccessories=true;c.IconSize=64;c.Gap=28;c.InfiniteCarousel=false;
  c.Items.Clear();foreach(var target in new[]{Environment.ProcessPath!,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"notepad.exe"),Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)})c.Items.Add(new DockItem{Name=Path.GetFileName(target),Target=target});
  string marker=Path.Combine(store.Root,"effect-launch.txt");c.QuickItems=new(){new DockItem{Name="Launch probe",Target=Environment.ProcessPath!,Arguments="--launch-marker="+StartupRegistration.Quote(marker)}};store.Save();
  var app=(App)Application.Current;app.Dock=new DockWindow();app.Dock.Show();Pump(250);var dock=app.Dock;var surface=dock.Surface;
  double baseline=DesktopPlacement.ClientBounds(dock).Top+surface.ActualHeight-12,musicAnchor=dock.MusicAnchorTop;
  var settings=new SettingsWindow();settings.Show();settings.SelectPage(8);Pump(80);
  ComboBox Selector(string name)=>Children(settings).OfType<ComboBox>().Single(x=>x.Name==name);
  Check(Selector("IconClickFeedbackSelector").SelectedIndex==0&&Selector("IconDisplayEffectSelector").SelectedIndex==0,"new settings page displays both defaults without changing existing apps");
  Selector("IconClickFeedbackSelector").SelectedIndex=1;Selector("IconDisplayEffectSelector").SelectedIndex=1;Pump(250);
  var saved=new Store(store.Root);saved.Load();Check(saved.Config.IconClickFeedback=="bounce"&&saved.Config.IconDisplayEffect=="reflection"&&saved.Config.Items.Count==4,"both selectors apply immediately and persist without changing the app list");
  Check(Math.Abs(DesktopPlacement.ClientBounds(dock).Top+surface.ActualHeight-12-dock.IconReflectionSpace-baseline)<.01&&Math.Abs(dock.MusicAnchorTop-musicAnchor)<.01,"effect padding preserves the physical Dock baseline and music position");
  Check(Math.Abs(dock.Accessories.Center(2).Y+DesktopPlacement.ClientBounds(dock).Top-(baseline-c.IconSize/2))<.01,"left controls stay aligned with the icon band when reflections are enabled");
  double maximumSize=c.IconSize*c.HoverScale;Check(IconEffects.ReflectionBounds(new Rect(0,-maximumSize,maximumSize,maximumSize),c.IconSize*.55).Bottom<=dock.IconReflectionSpace,"reflection stays inside its padded window at maximum hover and peak bounce");
  Shot(surface,Path.Combine(store.Root,"dock-reflection.png"));Shot(settings,Path.Combine(store.Root,"icon-settings-zh.png"));
  settings.ChangeLanguage("en");Pump(80);Check(Children(settings).OfType<TextBlock>().Any(x=>x.Text=="App Icons")&&Selector("IconDisplayEffectSelector").SelectedIndex==1,"App Icons translates and preserves selected effects");Shot(settings,Path.Combine(store.Root,"icon-settings-en.png"));
  List<(Rect rect,DockItem item)> Hits()=>(List<(Rect,DockItem)>)typeof(DockSurface).GetField("hits",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(surface)!;
  var targetItem=Hits().First().item;double restingTop=Hits().First(h=>h.item.Id==targetItem.Id).rect.Top,position=surface.Motion.Position;
  surface.PlayLaunchFeedback(targetItem);Pump(100);Shot(surface,Path.Combine(store.Root,"dock-bounce.png"));
  Check(Hits().First(h=>h.item.Id==targetItem.Id).rect.Top<restingTop-2&&surface.Motion.Position==position,"launch feedback moves only the clicked app vertically without moving the carousel");
  Pump(1300);Check(!surface.BackgroundAnimating&&Math.Abs(Hits().First(h=>h.item.Id==targetItem.Id).rect.Top-restingTop)<.01,"bounce returns exactly to its baseline and stops rendering");
  surface.PlayLaunchFeedback(targetItem);Pump(50);surface.SetBackgroundSuspended(true);Check(!surface.BackgroundAnimating,"suspension immediately cancels bounce rendering");surface.SetBackgroundSuspended(false);Pump(100);
  var panel=new QuickBarWindow(dock);panel.Open();Pump(1700);Shot(panel,Path.Combine(store.Root,"quick-reflection.png"));
  Check(Children(panel).OfType<ApplicationIcon>().Count()==1&&!panel.Animating,"Quick Panel uses the same cached reflection and stops when idle");
  Children(panel).OfType<Button>().First().RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));Pump(1700);Check(File.Exists(marker)&&!panel.IsVisible&&!panel.Animating,"Quick Panel launch still opens the target and closes after its feedback");
  c.IconSize=128;c.Gap=8;c.HoverScale=1.5;store.Save();panel.Open();Pump(1700);panel.HoverAt(new Point((c.IconSize+c.Gap)/2,(c.IconSize+c.Gap)/2));Pump(900);
  var largeIcon=Children(panel).OfType<ApplicationIcon>().Single();var viewport=Children(panel).OfType<ScrollViewer>().Single();var mapping=largeIcon.TransformToAncestor(viewport);
  var reflectedBounds=mapping.TransformBounds(IconEffects.ReflectionBounds(new Rect(0,0,128,128)));var iconBounds=mapping.TransformBounds(new Rect(0,0,128,128));
  Check(reflectedBounds.Left>=0&&reflectedBounds.Right<=viewport.ActualWidth&&reflectedBounds.Bottom<=viewport.ActualHeight&&iconBounds.Top>=0,"Quick Panel does not clip maximum-size hovered icons or their perspective reflection");Shot(panel,Path.Combine(store.Root,"quick-large-hover.png"));panel.Close();
  c.IconSize=64;c.Gap=28;c.HoverScale=1.25;store.Save();
  Selector("IconClickFeedbackSelector").SelectedIndex=0;Selector("IconDisplayEffectSelector").SelectedIndex=0;Pump(200);surface.PlayLaunchFeedback(targetItem);Pump(1100);
  Check(c.IconClickFeedback=="default"&&c.IconDisplayEffect=="default"&&!surface.BackgroundAnimating&&Math.Abs(dock.MusicAnchorTop-musicAnchor)<.01,"returning both controls to Default restores existing appearance and idle behavior");
  settings.Close();dock.Close();CheckReflectionEdges(store,checks);File.WriteAllText(Path.Combine(store.Root,"icon-effects-results.json"),JsonSerializer.Serialize(checks,new JsonSerializerOptions{WriteIndented=true}));
 }
}
