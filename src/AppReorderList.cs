using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
namespace EurekaDock;
// Layout stays fixed while dragging: only compositor translations change.
// Persist the new order once, after the lifted row settles into its slot.
internal sealed class AppReorderList : ListBox {
 const double RowHeight=34;
 readonly DispatcherTimer scrollTimer=new(){Interval=TimeSpan.FromMilliseconds(16)};
 DockItem? moving;
 int from,to;
 double pointerStart,scrollStart;
 bool settling;
 ScrollViewer? scroll;
 internal event Action<DockItem,int>? Reordered;
 
 internal static int Destination(int source,double offset,int count)=>Math.Clamp(source+(int)Math.Round(offset/RowHeight),0,Math.Max(0,count-1));
 public AppReorderList() {
  AllowDrop=true;HorizontalContentAlignment=HorizontalAlignment.Stretch;
  SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty,ScrollBarVisibility.Disabled);
  SetValue(ScrollViewer.CanContentScrollProperty,false);
  SetValue(VirtualizingPanel.IsVirtualizingProperty,false);
  scrollTimer.Tick+=(s,e)=>UpdateDrag(true);
  Unloaded+=(s,e)=>Cancel();
 }
 protected override void PrepareContainerForItemOverride(DependencyObject element,object value) {
  base.PrepareContainerForItemOverride(element,value);
  if(element is not ListBoxItem row||value is not DockItem item)return;
  row.Padding=new Thickness(0);row.Margin=new Thickness(0);row.HorizontalContentAlignment=HorizontalAlignment.Stretch;
  row.RenderTransform=new TranslateTransform();row.Height=RowHeight;
  var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(48)});
  var label=new TextBlock{Text=item.ToString(),VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(12,0,8,0),TextTrimming=TextTrimming.CharacterEllipsis,ToolTip=item.Name};grid.Children.Add(label);
  var grip=new Thumb{Width=40,Height=30,Cursor=Cursors.SizeNS,ToolTip=L.T("按住此处拖动排序"),Focusable=false};
  var bars=new FrameworkElementFactory(typeof(TextBlock));bars.SetValue(TextBlock.TextProperty,"☰");bars.SetValue(TextBlock.FontSizeProperty,20.0);bars.SetValue(TextBlock.ForegroundProperty,Brushes.Gray);bars.SetValue(TextBlock.HorizontalAlignmentProperty,HorizontalAlignment.Center);bars.SetValue(TextBlock.VerticalAlignmentProperty,VerticalAlignment.Center);
  var gripArea=new FrameworkElementFactory(typeof(Border));gripArea.SetValue(Border.BackgroundProperty,Brushes.Transparent);gripArea.AppendChild(bars);
  grip.Template=new ControlTemplate(typeof(Thumb)){VisualTree=gripArea};Grid.SetColumn(grip,1);grid.Children.Add(grip);
  grip.DragStarted+=(s,e)=>Begin(item);
  grip.DragDelta+=(s,e)=>UpdateDrag(false);
  grip.DragCompleted+=(s,e)=>{if(e.Canceled)Cancel();else Finish();};
  row.Content=new Border{BorderThickness=new Thickness(0,0,0,1),BorderBrush=new SolidColorBrush(Color.FromRgb(224,227,233)),Child=grid};
 }
 static T? Find<T>(DependencyObject root) where T:DependencyObject {for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is T match)return match;var found=Find<T>(child);if(found!=null)return found;}return null;}
 ListBoxItem? Row(int index)=>ItemContainerGenerator.ContainerFromIndex(index) as ListBoxItem;
 internal void Begin(DockItem item) {
  if(settling||moving!=null)return;
  moving=item;from=to=Items.IndexOf(item);SelectedItem=item;
  scroll=Find<ScrollViewer>(this);pointerStart=Mouse.GetPosition(this).Y;scrollStart=scroll?.VerticalOffset??0;
  var row=Row(from);if(row!=null){Panel.SetZIndex(row,10);row.Background=new SolidColorBrush(Color.FromRgb(234,241,252));row.Opacity=.94;}
  scrollTimer.Start();
 }
 void UpdateDrag(bool autoScroll) {
  if(moving==null||settling)return;
  double y=Mouse.GetPosition(this).Y;
  if(autoScroll&&scroll!=null){double step=y<30?-Math.Min(12,(30-y)*.4):y>ActualHeight-30?Math.Min(12,(y-ActualHeight+30)*.4):0;scroll.ScrollToVerticalOffset(scroll.VerticalOffset+step);}
  double offset=y-pointerStart+(scroll?.VerticalOffset??0)-scrollStart;
  UpdateOffset(offset);
 }
 internal void UpdateOffset(double offset) {
  if(moving==null||settling)return;
  int minimum=0;
  offset=Math.Clamp(offset,(minimum-from)*RowHeight,(Items.Count-1-from)*RowHeight);
  var row=Row(from);if(row?.RenderTransform is TranslateTransform translation){translation.BeginAnimation(TranslateTransform.YProperty,null);translation.Y=offset;}
  int destination=Destination(from,offset,Items.Count);if(destination==to)return;to=destination;
  for(int i=0;i<Items.Count;i++)if(i!=from){double shift=to>from&&i>from&&i<=to?-RowHeight:to<from&&i>=to&&i<from?RowHeight:0;Animate(Row(i),shift);}
 }
 static void Animate(ListBoxItem? row,double y,Action? complete=null) {
  if(row?.RenderTransform is not TranslateTransform translation){complete?.Invoke();return;}
  double current=translation.Y;translation.BeginAnimation(TranslateTransform.YProperty,null);translation.Y=y;
  var animation=new DoubleAnimation(current,y,TimeSpan.FromMilliseconds(220)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}};
  if(complete!=null)animation.Completed+=(s,e)=>complete();translation.BeginAnimation(TranslateTransform.YProperty,animation);
 }
 internal void Finish() {
  scrollTimer.Stop();if(moving==null||settling)return;settling=true;
  var item=moving;int destination=to;
  Animate(Row(from),(to-from)*RowHeight,()=>{if(moving!=item)return;Cancel();Reordered?.Invoke(item,destination);});
 }
 internal void Cancel() {
  scrollTimer.Stop();moving=null;settling=false;
  for(int i=0;i<Items.Count;i++)if(Row(i) is ListBoxItem row){if(row.RenderTransform is TranslateTransform t){t.BeginAnimation(TranslateTransform.YProperty,null);t.Y=0;}row.ClearValue(BackgroundProperty);row.Opacity=1;Panel.SetZIndex(row,0);}
 }
}

