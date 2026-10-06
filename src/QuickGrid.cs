using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
namespace EurekaDock;
internal sealed class QuickGrid:Canvas {
 const double Cell=92;
 readonly List<Border> cards=new();
 readonly List<DockItem> items;
 readonly int columns;
 int from=-1,to=-1;Point down;bool dragging,settling;
 readonly System.Windows.Threading.DispatcherTimer autoScroll=new(){Interval=TimeSpan.FromMilliseconds(16)};
 internal DockItem? Selected {get;private set;}
 internal event Action? Changed;
 internal event Action? SelectionChanged;
 internal int TargetIndex=>to;
 public QuickGrid(List<DockItem> source,int cols,int capacity){
  items=source;columns=cols;HorizontalAlignment=HorizontalAlignment.Left;VerticalAlignment=VerticalAlignment.Top;Width=columns*Cell;Height=Math.Max(Cell,Math.Ceiling(Math.Max(capacity,items.Count)/(double)columns)*Cell);Background=Brushes.Transparent;Focusable=true;
  int visibleIndex=0;
  for(int i=0;i<items.Count;i++){
   int index=i;var item=items[i];var panel=new StackPanel{VerticalAlignment=VerticalAlignment.Center};
   bool overflow=!item.Hidden&&visibleIndex++>=capacity;
   panel.Children.Add(new Image{Source=Native.Icon(item,64),Width=44,Height=44,Margin=new Thickness(0,0,0,4)});
   panel.Children.Add(new TextBlock{Text=item.Name,TextAlignment=TextAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis,FontSize=11});
   if(item.Hidden)panel.Children.Add(new TextBlock{Text=L.P("已隐藏","Hidden"),TextAlignment=TextAlignment.Center,FontSize=10,Foreground=Brushes.DimGray});
   var card=new Border{Width=Cell-6,Height=Cell-6,CornerRadius=new CornerRadius(10),Background=new SolidColorBrush(Color.FromRgb(238,241,246)),Child=panel,Padding=new Thickness(5),ToolTip=item.ToString()+(overflow?L.T(" · 超出当前容量，扩大行列后显示"):""),Opacity=(item.Hidden||overflow)?0.5:1,RenderTransform=new TranslateTransform()};
   var p=Position(i);SetLeft(card,p.X);SetTop(card,p.Y);cards.Add(card);Children.Add(card);
   card.MouseLeftButtonDown+=(s,e)=>{if(settling)return;Begin(index,e.GetPosition(this));CaptureMouse();Focus();e.Handled=true;};
  }
  autoScroll.Tick+=(s,e)=>{if(from<0||settling||!IsMouseCaptured)return;DependencyObject? parent=VisualTreeHelper.GetParent(this);while(parent!=null&&parent is not ScrollViewer)parent=VisualTreeHelper.GetParent(parent);if(parent is ScrollViewer scroll){double y=Mouse.GetPosition(scroll).Y;double speed=y<30?-8:y>scroll.ActualHeight-30?8:0;if(speed!=0){scroll.ScrollToVerticalOffset(scroll.VerticalOffset+speed);MoveTo(Mouse.GetPosition(this));}}};
  MouseMove+=(s,e)=>{if(from<0||settling||e.LeftButton!=MouseButtonState.Pressed)return;MoveTo(e.GetPosition(this));};
  MouseLeftButtonUp+=(s,e)=>Finish();
  LostMouseCapture+=(s,e)=>{if(!settling)Cancel();};
  KeyDown+=(s,e)=>{if(e.Key==Key.Escape){Cancel();e.Handled=true;}};
  Unloaded+=(s,e)=>Cancel();
 }
 Point Position(int index)=>new Point(index%columns*Cell,index/columns*Cell);
 internal void Begin(int index,Point pointer){Cancel();from=to=index;down=pointer;Selected=items[index];SelectionChanged?.Invoke();cards[index].BorderBrush=Brushes.SlateGray;cards[index].BorderThickness=new Thickness(1);SetZIndex(cards[index],10);autoScroll.Start();}
 internal void MoveTo(Point pointer){if(from<0||settling)return;var delta=pointer-down;if(!dragging&&delta.Length<5)return;dragging=true;
  var t=(TranslateTransform)cards[from].RenderTransform;t.X=delta.X;t.Y=delta.Y;
  var center=Position(from)+delta+new Vector(Cell/2,Cell/2);
  int col=Math.Clamp((int)Math.Floor(center.X/Cell),0,columns-1),row=Math.Max(0,(int)Math.Floor(center.Y/Cell));
  int next=Math.Clamp(row*columns+col,0,items.Count-1);if(next==to)return;to=next;
  for(int i=0;i<cards.Count;i++)if(i!=from){int target=to>from&&i>from&&i<=to?i-1:to<from&&i>=to&&i<from?i+1:i;var offset=Position(target)-Position(i);Animate(cards[i],offset);}
 }
 static void Animate(Border card,Vector offset,Action? done=null){var t=(TranslateTransform)card.RenderTransform;
  void Axis(DependencyProperty property,double value,Action? complete){double old=(double)t.GetValue(property);t.BeginAnimation(property,null);t.SetValue(property,value);var a=new DoubleAnimation(old,value,TimeSpan.FromMilliseconds(220)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}};if(complete!=null)a.Completed+=(s,e)=>complete();t.BeginAnimation(property,a);}
  Axis(TranslateTransform.XProperty,offset.X,null);Axis(TranslateTransform.YProperty,offset.Y,done);
 }
 internal void Finish(){autoScroll.Stop();if(from<0||settling)return;if(!dragging){Cancel();return;}settling=true;ReleaseMouseCapture();int source=from,target=to;
  Animate(cards[source],Position(target)-Position(source),()=>{if(from!=source)return;var item=items[source];items.RemoveAt(source);items.Insert(target,item);Cancel();Changed?.Invoke();});
 }
 internal void Cancel(){autoScroll.Stop();from=to=-1;dragging=settling=false;if(IsMouseCaptured)ReleaseMouseCapture();foreach(var card in cards){var t=(TranslateTransform)card.RenderTransform;t.BeginAnimation(TranslateTransform.XProperty,null);t.BeginAnimation(TranslateTransform.YProperty,null);t.X=t.Y=0;SetZIndex(card,0);card.BorderThickness=new Thickness(Selected!=null&&items.IndexOf(Selected)==cards.IndexOf(card)?1:0);}}
}


