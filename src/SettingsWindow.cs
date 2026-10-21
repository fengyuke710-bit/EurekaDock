using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using Forms=System.Windows.Forms;
namespace EurekaDock;
internal sealed partial class SettingsWindow : Window {
 readonly StackPanel page=new() {Margin=new Thickness(28)};
 readonly ListBox nav=new();
 readonly ScrollViewer pageScroll=new(){VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
 readonly TextBlock status=new() {TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,16,0,0),Foreground=Brushes.DarkSlateBlue};
 ListBox? apps;


 Configuration C=>App.Store.Config;
 int SelectedPage=>nav.SelectedItem is ListBoxItem item&&item.Tag is int index?index:0;
 static string[] PageNames()=>new[]{L.T("常规"),"Dock",L.T("应用管理"),L.T("快捷栏"),L.T("动画"),L.T("音乐律动"),L.T("维护"),L.T("关于"),L.P("软件图标","App Icons")};
 public SettingsWindow() {
  SettingsRendering.Stabilize(this);
  Title=L.T("EurekaDock 设置 · 技术预览");Width=940;Height=Math.Min(820,SystemParameters.WorkArea.Height);MinWidth=780;MinHeight=520;WindowStartupLocation=WindowStartupLocation.CenterScreen;
  Background=new SolidColorBrush(Color.FromRgb(247,248,251));
  var root=new DockPanel();
  var rail=new StackPanel {Width=195,Background=Brushes.White};
  rail.Children.Add(new TextBlock {Text="EurekaDock",FontSize=22,FontWeight=FontWeights.SemiBold,Margin=new Thickness(20,28,10,24)});
  nav.BorderThickness=new Thickness(0);nav.FontSize=15;nav.Margin=new Thickness(10);
  var names=PageNames();foreach(int index in new[]{0,1,8,2,3,4,5,6,7})nav.Items.Add(new ListBoxItem {Content=names[index],Tag=index,Padding=new Thickness(12)});
  nav.SelectionChanged+=(s,e)=>{Render();pageScroll.ScrollToTop();};
  SizeChanged+=(s,e)=>{if(apps!=null)apps.Height=Math.Max(180,ActualHeight-350);};
  page.AllowDrop=true;page.Background=Brushes.Transparent;
  page.PreviewDragOver+=(s,e)=>{if((SelectedPage==2||SelectedPage==3)&&e.Data.GetDataPresent(DataFormats.FileDrop)){e.Effects=DragDropEffects.Copy;e.Handled=true;}};
  page.PreviewDrop+=(s,e)=>{if(SelectedPage==2&&e.Data.GetData(DataFormats.FileDrop) is string[] paths){e.Handled=true;ImportPaths(paths);}};
  page.PreviewDrop+=(s,e)=>{if(SelectedPage==3&&e.Data.GetData(DataFormats.FileDrop) is string[] paths){e.Handled=true;int count=ImportQuick(paths);Render();Say(L.F("已添加 {0} 项；重复或超出容量的项目已跳过。","Added {0} items. Duplicates or items beyond capacity were skipped.",count));}};
  rail.Children.Add(nav);DockPanel.SetDock(rail,Dock.Left);root.Children.Add(rail);
  pageScroll.Content=page;root.Children.Add(pageScroll);Content=root;nav.SelectedIndex=0;
 }
 void Say(string message)=>status.Text=message;
 internal void SelectPage(int index)=>nav.SelectedItem=nav.Items.Cast<ListBoxItem>().Single(item=>(int)item.Tag==index);
 internal void ChangeLanguage(string language) {
  if(C.Language==language)return;
  string previous=C.Language;C.Language=language;
  try{App.Store.Save();}catch(Exception ex){C.Language=previous;App.Store.Log("Language save: "+ex);RefreshLanguage();Say(L.T("保存失败：")+ex.Message);return;}
  RefreshLanguage();((App)Application.Current).RefreshTrayLanguage();
 }
 void RefreshLanguage(){
  Title=L.T("EurekaDock 设置 · 技术预览");
  string[] names=PageNames();foreach(ListBoxItem item in nav.Items)item.Content=names[(int)item.Tag];Render();pageScroll.ScrollToTop();
 }
 void Save() {try {App.Store.Save();Say(L.T("已自动保存"));}catch(Exception ex){App.Store.Log("Save: "+ex);Say(L.T("保存失败：")+ex.Message);}}
 void Heading(string title,string description) {page.Children.Add(new TextBlock {Text=title,FontSize=28,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,10)});page.Children.Add(new TextBlock {Text=description,TextWrapping=TextWrapping.Wrap,Foreground=Brushes.DimGray,Margin=new Thickness(0,0,0,20)});}
 Button Button(string label,Action action) {var b=new Button {Content=label,Padding=new Thickness(12,7,12,7),Margin=new Thickness(0,4,8,4)};b.Click+=(s,e)=>{try{action();}catch(Exception ex){App.Store.Log(label+": "+ex);Say(ex.Message);}};return b;}
 void Slider(string title,double value,double min,double max,Action<double> change,string format="0") {
  var label=new TextBlock {Text=title+"  "+value.ToString(format,L.Culture),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,4)};page.Children.Add(label);
  var slider=new Slider {Minimum=min,Maximum=max,Value=value,TickFrequency=(max-min)/100};
  slider.ValueChanged+=(s,e)=>{label.Text=title+"  "+e.NewValue.ToString(format,L.Culture);change(e.NewValue);Save();};page.Children.Add(slider);
 }
 void Render() {
  quickGrid?.Cancel();page.Children.Clear();status.Text="";
  switch(SelectedPage) {
   case 0:
    Heading(L.T("常规"),L.T("离线运行；关闭设置窗口后，Dock 与系统托盘继续运行。"));
    page.Children.Add(new TextBlock{Text=L.P("界面语言 / Language","Language / 界面语言"),Margin=new Thickness(0,4,0,6)});
    var languages=new ComboBox{ItemsSource=new[]{"简体中文","English"},SelectedIndex=L.English?1:0,Width=200,HorizontalAlignment=HorizontalAlignment.Left,Padding=new Thickness(6),Margin=new Thickness(0,0,0,12)};
    languages.SelectionChanged+=(s,e)=>ChangeLanguage(languages.SelectedIndex==1?"en":"zh-CN");page.Children.Add(languages);
    var start=new CheckBox {Content=L.T("登录 Windows 时静默启动（当前用户）"),Margin=new Thickness(0,12,0,12)};
    start.IsChecked=StartupRegistration.IsRegistered;
    start.Click+=(s,e)=> {try {StartupRegistration.SetEnabled(start.IsChecked==true);Say(L.T("登录启动设置已更新，将使用当前应用列表和配置。"));}catch(Exception ex){start.IsChecked=StartupRegistration.IsRegistered;Say(L.T("更新失败：")+ex.Message);}};
    page.Children.Add(start);
    var suspend=new CheckBox {Name="SuspendWhenCoveredToggle",Content=L.P("全屏或最大化时暂停桌面组件","Pause desktop widgets in fullscreen or maximized apps"),IsChecked=C.SuspendWhenCovered,Margin=new Thickness(0,4,0,8)};
    suspend.Click+=(s,e)=>{C.SuspendWhenCovered=suspend.IsChecked==true;Save();};page.Children.Add(suspend);
    page.Children.Add(new TextBlock {Text=L.P("启用后，Dock、快捷按钮和音乐律动会暂时收起并停止运行，返回桌面时一起弹出。关闭后保持运行，仍只显示在桌面层。","When enabled, the Dock, its controls, and the visualizer pause and disappear while a fullscreen or maximized app is active, then reappear together when you return to the desktop. When disabled, they keep running on the desktop layer."),TextWrapping=TextWrapping.Wrap,Foreground=Brushes.DimGray,Margin=new Thickness(0,0,0,14)});
    page.Children.Add(new TextBlock {Text=L.T("托盘保留打开设置、暂时隐藏、重启和退出入口。\n配置位置：")+App.Store.Root,TextWrapping=TextWrapping.Wrap});
    if(App.Store.Recovery!="")page.Children.Add(new TextBlock {Text=App.Store.Recovery,Foreground=Brushes.DarkOrange,Margin=new Thickness(0,12,0,0)});
    break;
   case 1:
    Heading("Dock",L.T("修改实时生效并自动保存。尺寸单位为 DPI 无关像素。"));
    var enabled=new CheckBox {Content=L.T("显示 Dock 栏目（关闭后仍可从托盘或桌面快捷方式打开设置）"),IsChecked=C.DockEnabled,Margin=new Thickness(0,8,0,14)};
    enabled.Click+=(s,e)=>{C.DockEnabled=enabled.IsChecked==true;Save();};page.Children.Add(enabled);
    page.Children.Add(new TextBlock{Text=L.P("关闭 Dock 后，已启用的音浪仍可单独显示；左侧按钮会移至音浪左侧。","When the Dock is hidden, an enabled visualizer remains visible. The left-side controls move beside it."),TextWrapping=TextWrapping.Wrap,Foreground=Brushes.DimGray,Margin=new Thickness(0,0,0,12)});
    var controls=new CheckBox{Content=L.P("显示左侧快捷按钮（设置、桌面图标、快捷栏）","Show left-side controls (Settings, desktop icons, Quick Panel)"),IsChecked=C.ShowAccessories,Margin=new Thickness(0,0,0,12)};
    controls.Click+=(s,e)=>{C.ShowAccessories=controls.IsChecked==true;Save();};page.Children.Add(controls);
    var loop=new CheckBox {Content=L.T("无限循环栏目（关闭后为有穷模式，首尾停止）"),IsChecked=C.InfiniteCarousel,Margin=new Thickness(0,0,0,12)};
    loop.Click+=(s,e)=>{C.InfiniteCarousel=loop.IsChecked==true;Save();};page.Children.Add(loop);
    Slider(L.T("图标大小"),C.IconSize,32,128,v=>C.IconSize=v);
    Slider(L.T("图标间距"),C.Gap,8,80,v=>C.Gap=v);
    Slider(L.T("屏幕宽度比例"),C.WidthFraction,.3,.95,v=>C.WidthFraction=v,"P0");
    Slider(L.T("图标基线垂直位置"),C.VerticalFraction,.1,.97,v=>C.VerticalFraction=v,"P0");
    Slider(L.T("Hover 最大倍率"),C.HoverScale,1,1.5,v=>C.HoverScale=v,"0.00");
    Slider(L.T("边缘渐隐宽度"),C.FadeWidth,0,250,v=>C.FadeWidth=v);
    Slider(L.T("边缘雾化强度"),C.Blur,0,8,v=>C.Blur=v,"0.0");
    break;
   case 2: AppsPage();break;
   case 3: QuickPage();break;
   case 4:
    Heading(L.T("动画"),L.T("滚轮目标累计；拖动后按速度惯性吸附。静止后停止逐帧计算。"));
    var reverse=new CheckBox {Content=L.T("反转鼠标滚轮方向（开启后：下滑向左，上滑向右）"),IsChecked=C.ReverseWheel,Margin=new Thickness(0,8,0,14)};
    reverse.Click+=(s,e)=>{C.ReverseWheel=reverse.IsChecked==true;Save();};page.Children.Add(reverse);
    Slider(L.T("吸附弹性"),C.Spring,40,200,v=>C.Spring=v);
    Slider(L.T("阻尼"),C.Damping,10,35,v=>C.Damping=v);
    Slider(L.T("按住转为拖动（毫秒）"),C.HoldMilliseconds,100,500,v=>C.HoldMilliseconds=v);
    Slider(L.T("横向拖动阈值"),C.DragThreshold,3,20,v=>C.DragThreshold=v);
    break;
   case 5:
    Heading(L.T("音乐律动"),L.T("柔和音浪：主要向上起伏，大峰带动周围，强弱跟随音乐与系统音量；只有颜色向右流动。不使用麦克风或保存音频。"));
    var music=new CheckBox{Content=L.T("启用音乐律动"),IsChecked=C.MusicEnabled,Margin=new Thickness(0,8,0,14)};music.Click+=(s,e)=>{C.MusicEnabled=music.IsChecked==true;Save();};page.Children.Add(music);
    page.Children.Add(new TextBlock{Text=L.P("显示样式","Display style"),Margin=new Thickness(0,0,0,6)});
    var style=new ComboBox{ItemsSource=new[]{L.P("线性音浪","Linear Wave"),L.P("条形音浪","Bar Wave")},SelectedIndex=C.MusicStyle=="bars"?1:0,Width=220,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,0,0,12)};
    style.SelectionChanged+=(s,e)=>{C.MusicStyle=style.SelectedIndex==1?"bars":"line";Save();Render();};page.Children.Add(style);
    var audioStatus=new TextBlock{Text=L.AudioStatus,TextWrapping=TextWrapping.Wrap,Foreground=Brushes.DimGray};Action report=()=>audioStatus.Dispatcher.BeginInvoke(()=>audioStatus.Text=L.AudioStatus);MusicAudio.StatusChanged+=report;audioStatus.Unloaded+=(s,e)=>MusicAudio.StatusChanged-=report;page.Children.Add(audioStatus);
    Slider(L.T("线条长度（相对栏目）"),C.MusicLength,.15,1.5,v=>C.MusicLength=v,"P0");
    Slider(L.T("向上偏移（负值向下）"),C.MusicOffset,-100,400,v=>C.MusicOffset=v);
    if(C.MusicStyle=="bars")Slider(L.P("条形音浪缩放（封面、文字及柱高）","Bar Wave scale (artwork, text, and bar height)"),C.MusicBarScale,.6,2,v=>C.MusicBarScale=v,"P0");
    if(C.MusicStyle=="line")Slider(L.T("波形振幅"),C.MusicAmplitude,4,70,v=>C.MusicAmplitude=v);
    Slider(L.T("音乐灵敏度"),C.MusicSensitivity,.2,6,v=>C.MusicSensitivity=v,"0.0");
    page.Children.Add(new TextBlock{Text=L.T("跟随默认输出设备，切换耳机/音箱后会自动重新连接；单独路由到其他设备或受保护的声音可能无法驱动。"),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,16,0,0)});
    if(C.MusicStyle=="bars")page.Children.Add(new TextBlock{Text=L.P("条形音浪默认铺满整条区域，包含 36 根独立柱条，随声音向上起伏，无声时回落为小方块。播放器开始播放歌曲后，展开封面、歌名和歌手信息；暂停时保留，关闭播放器后平滑收回，音浪重新填满空位。缩放比例会同步改变封面、文字和柱条高度，不拉宽柱条；文字颜色跟随柱条。全屏或最大化时是否暂停，可在“常规”中设置。","Bar Wave fills the full visualizer area by default. Its 36 independent bars rise with sound and rest as small blocks in silence. Artwork, title, and artist appear when a player starts a song, remain while paused, and smoothly retract when the player closes as the bars expand to fill the space. Scaling changes artwork, text, and bar height without stretching the bars; text follows the bar color. Pause behavior for fullscreen or maximized apps is controlled in General."),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,18,0,0)});
    WaveColorSettings();break;
   case 6:
    Heading(L.T("维护"),L.T("仅处理 EurekaDock 自己的配置、缓存与状态。"));
    page.Children.Add(Button(L.T("校验配置"),()=>Say(App.Store.Verify())));
    page.Children.Add(Button(L.T("尝试修复配置"),()=>{App.Store.Load();((App)Application.Current).ReloadDock();Say(App.Store.Recovery==""?L.T("配置正常。"):App.Store.Recovery);}));
    page.Children.Add(Button(L.T("重新加载 Dock"),()=>{((App)Application.Current).ReloadDock();Say(L.T("已重新加载"));}));
    page.Children.Add(Button(L.T("重启 EurekaDock"),()=>((App)Application.Current).Restart()));
    break;
   case 8:
    Heading(L.P("软件图标","App Icons"),L.P("分别选择点击反馈与常态显示效果，更改立即生效并自动保存。","Choose click feedback and a resting appearance independently. Changes apply immediately and save automatically."));
    page.Children.Add(new TextBlock{Text=L.P("点击反馈","Click feedback"),Margin=new Thickness(0,8,0,6)});
    var clickEffect=new ComboBox{Name="IconClickFeedbackSelector",ItemsSource=new[]{L.P("默认（现有效果）","Default (current behavior)"),L.P("弹跳（macOS 风格）","Bounce (macOS style)")},SelectedIndex=C.IconClickFeedback=="bounce"?1:0,Width=270,HorizontalAlignment=HorizontalAlignment.Left,Padding=new Thickness(6)};
    clickEffect.SelectionChanged+=(s,e)=>{C.IconClickFeedback=clickEffect.SelectedIndex==1?"bounce":"default";Save();};page.Children.Add(clickEffect);
    page.Children.Add(new TextBlock{Text=L.P("仅在单击启动项目时触发逐渐减弱的弹跳。横向拖动和右键菜单不会触发。","A confirmed click starts a short series of diminishing hops. Dragging and context menus do not trigger it."),TextWrapping=TextWrapping.Wrap,Foreground=Brushes.DimGray,Margin=new Thickness(0,8,0,20)});
    page.Children.Add(new TextBlock{Text=L.P("常态显示","Resting appearance"),Margin=new Thickness(0,8,0,6)});
    var displayEffect=new ComboBox{Name="IconDisplayEffectSelector",ItemsSource=new[]{L.P("默认（无反射）","Default (no reflection)"),L.P("镜面反射","Mirror reflection")},SelectedIndex=C.IconDisplayEffect=="reflection"?1:0,Width=270,HorizontalAlignment=HorizontalAlignment.Left,Padding=new Thickness(6)};
    displayEffect.SelectionChanged+=(s,e)=>{C.IconDisplayEffect=displayEffect.SelectedIndex==1?"reflection":"default";Save();};page.Children.Add(displayEffect);
    page.Children.Add(new TextBlock{Text=L.P("在图标下方显示向屏幕外倾斜的柔和倒影，越向下越模糊、越透明。两项设置适用于 Dock 与快捷栏；快捷栏启动项目后仍会自动收起。默认选项保留原有外观和操作。","Shows a soft reflection below each icon, widening towards the viewer and becoming blurrier and more transparent downwards. Both settings apply to the Dock and Quick Panel; launching a Quick Panel item still closes it automatically. Defaults preserve the existing look and behavior."),TextWrapping=TextWrapping.Wrap,Foreground=Brushes.DimGray,Margin=new Thickness(0,8,0,0)});
    break;
   default:
    AboutPage();
    break;
  }
  foreach(var check in page.Children.OfType<CheckBox>()){
   var text=new FrameworkElementFactory(typeof(TextBlock));text.SetBinding(TextBlock.TextProperty,new System.Windows.Data.Binding());text.SetValue(TextBlock.TextWrappingProperty,TextWrapping.Wrap);
   check.ContentTemplate=new DataTemplate{VisualTree=text};check.HorizontalContentAlignment=HorizontalAlignment.Stretch;
  }
  page.Children.Add(status);
 }
 void AppsPage() {
  Heading(L.T("应用管理"),L.T("将桌面快捷方式、软件、文件或文件夹拖入此页即可添加（支持多选）。按住每行右侧三横杠排序；改名仅作用于 Dock。"));
  var bar=new WrapPanel();
  bar.Children.Add(Button(L.T("添加文件 / 应用"),()=>{var d=new Microsoft.Win32.OpenFileDialog {Filter=L.T("全部文件|*.*"),Multiselect=true};if(d.ShowDialog(this)==true)foreach(var f in d.FileNames)Add(f);}));
  bar.Children.Add(Button(L.T("添加文件夹"),()=>{using var d=new Forms.FolderBrowserDialog();if(d.ShowDialog()==Forms.DialogResult.OK)Add(d.SelectedPath);}));
  bar.Children.Add(Button(L.T("Windows 应用"),PickWindowsApp));
  bar.Children.Add(Button(L.T("手动路径"),()=>{var item=new DockItem();if(Edit(item)) {C.Items.Add(item);Save();RefreshApps();}}));
  page.Children.Add(bar);
  var reorder=new AppReorderList {Height=Math.Max(180,Height-350),Margin=new Thickness(0,12,0,12)};
  reorder.Reordered+=(item,index)=>{C.Items.Remove(item);C.Items.Insert(Math.Clamp(index,0,C.Items.Count),item);Save();RefreshApps();apps!.SelectedItem=item;};
  apps=reorder;
  page.Children.Add(apps);RefreshApps();
  var actions=new WrapPanel();
  actions.Children.Add(Button(L.T("改名"),()=>{if(apps.SelectedItem is DockItem item)RenameDialog(item);}));
  actions.Children.Add(Button(L.T("编辑 / 修复路径"),()=>{if(apps.SelectedItem is DockItem item&&Edit(item)){Save();RefreshApps();}}));
  actions.Children.Add(Button(L.T("更换图标"),()=>{if(apps.SelectedItem is DockItem item){var d=new Microsoft.Win32.OpenFileDialog {Filter=L.T("图标|*.png;*.jpg;*.jpeg;*.ico")};if(d.ShowDialog(this)==true){item.Icon=d.FileName;Save();}}}));
  actions.Children.Add(Button(L.T("恢复原图标"),()=>{if(apps.SelectedItem is DockItem item){item.Icon=null;Save();}}));
  actions.Children.Add(Button(L.T("从 Dock 删除"),()=>{if(apps.SelectedItem is DockItem item){C.Items.Remove(item);Save();RefreshApps();}}));
  var visibility=VisibilityButton(()=>apps?.SelectedItem as DockItem,RefreshApps,out var updateVisibility);apps.SelectionChanged+=(s,e)=>updateVisibility();actions.Children.Add(visibility);
  page.Children.Add(actions);
 }
 void PickWindowsApp() {
  var installed=new System.Collections.Generic.List<DockItem>();
  int result=Native.EnumerateApps((name,path)=>installed.Add(new DockItem {Name=name,Target=path}));
  if(result<0)throw new System.Runtime.InteropServices.COMException(L.T("读取 Windows 应用失败"),result);
  var dialog=new Window {Title=L.T("选择 Windows 应用"),Width=560,Height=620,Owner=this,WindowStartupLocation=WindowStartupLocation.CenterOwner};
  SettingsRendering.Stabilize(dialog);
  var panel=new DockPanel {Margin=new Thickness(20)};
  var search=new TextBox {Padding=new Thickness(8),Margin=new Thickness(0,0,0,12)};
  DockPanel.SetDock(search,Dock.Top);panel.Children.Add(search);
  var list=new ListBox {ItemsSource=installed.OrderBy(x=>x.Name).ToList()};
  search.TextChanged+=(s,e)=>list.ItemsSource=installed.Where(x=>x.Name.Contains(search.Text,StringComparison.CurrentCultureIgnoreCase)).OrderBy(x=>x.Name).ToList();
  var add=Button(L.T("添加所选应用"),()=>{if(list.SelectedItem is DockItem item) {C.Items.Add(item);Save();RefreshApps();dialog.Close();}});
  DockPanel.SetDock(add,Dock.Bottom);panel.Children.Add(add);panel.Children.Add(list);dialog.Content=panel;dialog.ShowDialog();
 }
 void Add(string path) {C.Items.Add(new DockItem {Target=path,Name=Native.DisplayName(path)});Save();RefreshApps();}
 internal int ImportPaths(string[] paths) {
  int added=0,skipped=0;
  foreach(string path in paths) {
   if((!File.Exists(path)&&!Directory.Exists(path))||C.Items.Any(i=>string.Equals(i.Target,path,StringComparison.OrdinalIgnoreCase))||C.Items.Count>=1000){skipped++;continue;}
   C.Items.Add(new DockItem {Target=path,Name=Native.DisplayName(path)});added++;
  }
  if(added>0){Save();RefreshApps();}
  Say(L.F("已添加 {0} 项，跳过 {1} 项重复、无效或超出数量限制的项目。","Added {0} items; skipped {1} duplicate, invalid, or excess items.",added,skipped));return added;
 }
 void RefreshApps() {if(apps!=null){(apps as AppReorderList)?.Cancel();var selected=apps.SelectedItem;apps.ItemsSource=null;apps.ItemsSource=C.Items.ToList();apps.SelectedItem=selected;}}
 bool Edit(DockItem item) {
  var dialog=new Window {Title=L.T("应用信息"),Width=590,Height=380,Owner=this,WindowStartupLocation=WindowStartupLocation.CenterOwner,ResizeMode=ResizeMode.NoResize};
  SettingsRendering.Stabilize(dialog);
  var panel=new StackPanel {Margin=new Thickness(24)};
  TextBox Field(string label,string value) {panel.Children.Add(new TextBlock {Text=label,Margin=new Thickness(0,8,0,4)});var t=new TextBox {Text=value,Padding=new Thickness(6)};panel.Children.Add(t);return t;}
  var name=Field(L.T("显示名称"),item.Name);var target=Field(L.T("路径或 shell:AppsFolder\\应用 ID"),item.Target);var args=Field(L.T("启动参数"),item.Arguments);
  var ok=new Button {Content=L.T("完成"),Padding=new Thickness(14,7,14,7),Margin=new Thickness(0,18,0,0)};
  ok.Click+=(s,e)=>{if(string.IsNullOrWhiteSpace(target.Text))return;item.Target=Environment.ExpandEnvironmentVariables(target.Text.Trim().Trim('"'));item.Name=string.IsNullOrWhiteSpace(name.Text)?Path.GetFileNameWithoutExtension(item.Target):name.Text.Trim();item.Arguments=args.Text;dialog.DialogResult=true;};
  panel.Children.Add(ok);dialog.Content=panel;return dialog.ShowDialog()==true;
 }
 internal bool Rename(DockItem item,string name) {
  if(string.IsNullOrWhiteSpace(name))return false;
  item.Name=name.Trim();Save();RefreshApps();return true;
 }
 void RenameDialog(DockItem item) {
  var dialog=new Window{Title=L.T("修改 Dock 名称"),Width=480,Height=240,Owner=this,WindowStartupLocation=WindowStartupLocation.CenterOwner,ResizeMode=ResizeMode.NoResize};SettingsRendering.Stabilize(dialog);
  var panel=new StackPanel{Margin=new Thickness(24)};
  panel.Children.Add(new TextBlock{Text=L.T("只修改 Dock 中的名称，不改动原软件或文件。"),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12)});
  var name=new TextBox{Text=item.Name,Padding=new Thickness(6)};panel.Children.Add(name);
  var done=new Button{Content=L.T("完成"),IsDefault=true,Padding=new Thickness(14,6,14,6),Margin=new Thickness(0,14,0,0)};
  done.Click+=(s,e)=>{if(Rename(item,name.Text))dialog.DialogResult=true;};panel.Children.Add(done);dialog.Content=panel;
  dialog.Loaded+=(s,e)=>{name.Focus();name.SelectAll();};dialog.ShowDialog();
 }
}











