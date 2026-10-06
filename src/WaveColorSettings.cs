using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
namespace EurekaDock;
internal sealed partial class SettingsWindow{
 static bool ReadableDarkText(Color color)=>.299*color.R+.587*color.G+.114*color.B>145;
 void WaveColorSettings(){
  bool bars=C.MusicStyle=="bars";
  string Mode()=>bars?C.BarColorMode:C.MusicColorMode;
  void SetMode(string value){if(bars)C.BarColorMode=value;else C.MusicColorMode=value;}
  string CurrentColor()=>bars?C.BarColor:C.MusicColor;
  void SetCurrentColor(string value){if(bars)C.BarColor=value;else C.MusicColor=value;}
  System.Collections.Generic.List<string> GradientColors()=>bars?C.BarGradient:C.MusicGradient;
  string?[] MemoryColors()=>bars?C.BarColorMemory:C.MusicColorMemory;
  var section=new StackPanel{Margin=new Thickness(0,22,0,0)};page.Children.Add(section);
  section.Children.Add(new TextBlock{Text=bars?L.P("条形音浪颜色","Bar Wave colors"):L.P("线性音浪颜色","Linear Wave colors"),FontSize=20,FontWeight=FontWeights.SemiBold});
  var mode=new ComboBox{ItemsSource=bars?new[]{L.P("单色","Single color"),L.P("自定义渐变","Custom gradient")}:new[]{L.P("默认渐变","Default gradient"),L.P("单色","Single color"),L.P("自定义渐变","Custom gradient")},SelectedIndex=bars?(Mode()=="custom"?1:0):Mode()=="solid"?1:Mode()=="custom"?2:0,Width=250,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,12,0,8)};
  mode.SelectionChanged+=(s,e)=>{SetMode((bars?new[]{"solid","custom"}:new[]{"original","solid","custom"})[mode.SelectedIndex]);Save();};section.Children.Add(mode);
  section.Children.Add(new TextBlock{Text=L.P("在色盘上选色、输入十六进制色号，或使用下方常用色。渐变会沿音浪向右缓慢流动；线性音浪保留柔和光晕，条形音浪保留柱条的明暗质感。","Choose on the wheel, enter a hex color, or use a preset below. Gradients move gently to the right. Linear Wave keeps its glow, and Bar Wave keeps the bars' shaded texture."),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,10)});
  var row=new WrapPanel();var wheel=new ColorWheel();wheel.SetColor(WaveColors.Parse(CurrentColor()));row.Children.Add(wheel);
  var right=new StackPanel{Width=240,Margin=new Thickness(18,20,0,0)};
  var preview=new Border{Height=30,CornerRadius=new CornerRadius(6),Background=new SolidColorBrush(wheel.Selected)};
  var hex=new TextBox{Text=CurrentColor(),Width=132,MaxLength=7,Margin=new Thickness(0,6,0,10),ToolTip=L.P("输入 #RRGGBB 或 RRGGBB，按回车应用","Enter #RRGGBB or RRGGBB, then press Enter")};
  right.Children.Add(preview);right.Children.Add(new TextBlock{Text=L.P("颜色色号（#RRGGBB）","Color code (#RRGGBB)"),Margin=new Thickness(0,8,0,0)});right.Children.Add(hex);right.Children.Add(new TextBlock{Text=L.P("亮度","Brightness")});
  var brightness=new Slider{Minimum=0,Maximum=1,Value=wheel.Value,Margin=new Thickness(0,8,0,12)};right.Children.Add(brightness);row.Children.Add(right);section.Children.Add(row);
  var persist=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(250)};bool pending=false,changing=false;
  void Flush(){persist.Stop();if(pending){pending=false;Save();}}
  persist.Tick+=(s,e)=>Flush();section.Unloaded+=(s,e)=>Flush();
  void DisplayColor(Color color){SetCurrentColor(WaveColors.Hex(color));preview.Background=new SolidColorBrush(color);hex.Text=CurrentColor();((App)Application.Current).Dock?.PreviewMusicColor();pending=true;persist.Stop();persist.Start();}
  void Recall(string color){wheel.SetColor(WaveColors.Parse(color));changing=true;brightness.Value=wheel.Value;changing=false;DisplayColor(wheel.Selected);}
  void ApplyHex(){if(!WaveColors.TryHex(hex.Text,out string color)){Say(L.P("色号应为 6 位十六进制颜色，例如 #AAB7C8。","Enter a six-digit hex color, such as #AAB7C8."));hex.Text=CurrentColor();return;}Recall(color);}
  hex.KeyDown+=(s,e)=>{if(e.Key==Key.Enter){ApplyHex();e.Handled=true;}};
  hex.LostKeyboardFocus+=(s,e)=>{if(hex.Text!=CurrentColor())ApplyHex();};
  wheel.Changed+=DisplayColor;brightness.ValueChanged+=(s,e)=>{if(!changing)wheel.Brightness(e.NewValue);};
  right.Children.Add(Button(L.P("应用为单色","Use as single color"),()=>{Flush();mode.SelectedIndex=bars?0:1;}));
  section.Children.Add(new TextBlock{Text=L.P("常用色号（点击即可选取）","Common colors (click to choose)"),FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,14,0,6)});
  var presets=new WrapPanel();section.Children.Add(presets);
  foreach(var (nameZh,nameEn,color) in new[]{("白","White","#FFFFFF"),("黑","Black","#000000"),("红","Red","#FF0000"),("橙","Orange","#FFA500"),("黄","Yellow","#FFFF00"),("绿","Green","#00FF00"),("青","Cyan","#00FFFF"),("蓝","Blue","#0000FF"),("紫","Purple","#800080"),("粉","Pink","#FFC0CB")}){
   string selected=color;var swatch=new Button{Content=L.P(nameZh,nameEn),ToolTip=color,Width=68,Height=34,Margin=new Thickness(0,4,6,4),Background=new SolidColorBrush(WaveColors.Parse(color)),Foreground=ReadableDarkText(WaveColors.Parse(color))?Brushes.Black:Brushes.White};
   swatch.Click+=(s,e)=>Recall(selected);presets.Children.Add(swatch);
  }
  section.Children.Add(new TextBlock{Text=L.P("常用颜色（10 个保存位置）","Saved colors (10 slots)"),FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,14,0,8)});
  section.Children.Add(new TextBlock{Text=L.P("点击色块即可选中并载入已保存的颜色；点击“保存当前颜色”可写入该位置，替换原有颜色。","Click a slot to select it and load its saved color. Save current color replaces the color in that slot."),TextWrapping=TextWrapping.Wrap});
  int memoryIndex=0;var memory=new WrapPanel();section.Children.Add(memory);
  void Memory(){
   memory.Children.Clear();for(int i=0;i<10;i++){int slot=i;string? color=MemoryColors()[i];
    var b=new Button{Content=(i+1).ToString(),Width=46,Height=32,Margin=new Thickness(0,8,6,0),Background=color==null?Brushes.White:new SolidColorBrush(WaveColors.Parse(color)),Foreground=color==null||ReadableDarkText(WaveColors.Parse(color))?Brushes.Black:Brushes.White,BorderBrush=i==memoryIndex?Brushes.DodgerBlue:Brushes.Gray,BorderThickness=new Thickness(i==memoryIndex?2:1),ToolTip=color??L.P("尚未保存颜色","No saved color")};
    b.Click+=(s,e)=>{memoryIndex=slot;if(MemoryColors()[slot] is string saved)Recall(saved);Memory();};memory.Children.Add(b);
   }
  }
  Memory();var memoryActions=new WrapPanel();memoryActions.Children.Add(Button(L.P("保存当前颜色","Save current color"),()=>{Flush();MemoryColors()[memoryIndex]=CurrentColor();Save();Memory();}));memoryActions.Children.Add(Button(L.P("清空所选位置","Clear selected slot"),()=>{MemoryColors()[memoryIndex]=null;Save();Memory();}));section.Children.Add(memoryActions);
  section.Children.Add(new TextBlock{Text=L.P("渐变配色（2–10 种颜色，按列表顺序循环）","Gradient palette (2–10 colors, cycling in list order)"),FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,14,0,8)});
  var colors=new ListBox{Height=100,SelectionMode=SelectionMode.Single,Margin=new Thickness(0,8,0,8)};section.Children.Add(colors);
  void Gradient(int selected=0){
   colors.Items.Clear();for(int i=0;i<GradientColors().Count;i++){var content=new StackPanel{Orientation=Orientation.Horizontal};content.Children.Add(new Border{Width=36,Height=18,Background=new SolidColorBrush(WaveColors.Parse(GradientColors()[i])),Margin=new Thickness(0,0,10,0)});content.Children.Add(new TextBlock{Text=$"{i+1}   {GradientColors()[i]}"});colors.Items.Add(new ListBoxItem{Content=content});}colors.SelectedIndex=Math.Clamp(selected,0,colors.Items.Count-1);
  }
  Gradient();var gradientActions=new WrapPanel();
  gradientActions.Children.Add(Button(L.P("添加当前颜色","Add current color"),()=>{if(GradientColors().Count>=10){Say(L.P("最多可使用 10 种渐变颜色。","Use up to 10 gradient colors."));return;}Flush();GradientColors().Add(CurrentColor());Save();Gradient(GradientColors().Count-1);}));
  gradientActions.Children.Add(Button(L.P("用当前颜色替换","Replace with current color"),()=>{if(colors.SelectedIndex<0)return;Flush();int i=colors.SelectedIndex;GradientColors()[i]=CurrentColor();Save();Gradient(i);}));
  gradientActions.Children.Add(Button(L.P("删除所选颜色","Remove selected"),()=>{if(GradientColors().Count<=2){Say(L.P("渐变至少保留两种颜色。","Keep at least two gradient colors."));return;}int i=colors.SelectedIndex;if(i<0)return;GradientColors().RemoveAt(i);Save();Gradient(i);}));
  void Move(int delta){int i=colors.SelectedIndex,j=i+delta;if(i<0||j<0||j>=GradientColors().Count)return;(GradientColors()[i],GradientColors()[j])=(GradientColors()[j],GradientColors()[i]);Save();Gradient(j);}
  gradientActions.Children.Add(Button(L.P("上移","Move up"),()=>Move(-1)));gradientActions.Children.Add(Button(L.P("下移","Move down"),()=>Move(1)));section.Children.Add(gradientActions);
  section.Children.Add(Button(L.P("应用自定义渐变","Use custom gradient"),()=>{Flush();mode.SelectedIndex=bars?1:2;}));
 }
}

