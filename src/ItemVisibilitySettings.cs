using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
namespace EurekaDock;
internal sealed partial class SettingsWindow {
 internal void SetItemHidden(DockItem item,bool hidden){item.Hidden=hidden;Save();}
 Button VisibilityButton(Func<DockItem?> selected,Action refresh,out Action update){
  var b=Button("",()=>{if(selected() is DockItem item){SetItemHidden(item,!item.Hidden);refresh();}});
  update=()=>{var item=selected();b.IsEnabled=item!=null;b.Content=item?.Hidden==true?L.P("显示所选项目","Show selected item"):L.P("隐藏所选项目","Hide selected item");};
  update();return b;
 }
 bool QuickHasRoom=>C.QuickItems.Count<50&&C.QuickItems.Count(x=>!x.Hidden)<C.QuickColumns*C.QuickRows;
 internal int AddManagedToQuick(IEnumerable<DockItem> source){
  int added=0;
  foreach(var item in source){
   if(!QuickHasRoom)break;
   if(C.QuickItems.Any(x=>string.Equals(x.Target,item.Target,StringComparison.OrdinalIgnoreCase)))continue;
   C.QuickItems.Add(new DockItem{Name=item.Name,Target=item.Target,Arguments=item.Arguments,Icon=item.Icon});added++;
  }
  if(added>0)Save();return added;
 }
 void PickManagedApps(){
  var dialog=new Window{Title=L.P("从应用管理添加","Add from Apps"),Owner=this,Width=580,Height=600,WindowStartupLocation=WindowStartupLocation.CenterOwner};SettingsRendering.Stabilize(dialog);
  var panel=new DockPanel{Margin=new Thickness(20)};
  var description=new TextBlock{Text=L.P("可多选。复制名称、路径、启动参数和自定义图标；添加后可分别管理，两处的显示状态互不影响。已在快捷栏中的项目不再列出。","Select one or more items. Names, paths, launch arguments, and custom icons are copied. Each list stays independent, including visibility. Items already in the panel are excluded."),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12)};
  DockPanel.SetDock(description,Dock.Top);panel.Children.Add(description);
  var search=new TextBox{Padding=new Thickness(6),Margin=new Thickness(0,0,0,10),ToolTip=L.P("按名称搜索","Search by name")};DockPanel.SetDock(search,Dock.Top);panel.Children.Add(search);
  var candidates=C.Items.Where(i=>!C.QuickItems.Any(q=>string.Equals(q.Target,i.Target,StringComparison.OrdinalIgnoreCase))).ToList();
  var list=new ListBox{ItemsSource=candidates,SelectionMode=SelectionMode.Multiple};
  search.TextChanged+=(s,e)=>list.ItemsSource=candidates.Where(i=>i.Name.Contains(search.Text,StringComparison.CurrentCultureIgnoreCase)).ToList();
  var feedback=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,8)};
  var footer=new StackPanel();footer.Children.Add(feedback);
  if(candidates.Count==0)feedback.Text=L.P("没有可添加的项目。请先在应用管理中添加，或检查快捷栏中的现有项目。","No items to add. Add items in Apps first, or check existing Quick Panel items.");
  var add=Button(L.P("添加所选项目","Add selected items"),()=>{
   int requested=list.SelectedItems.Count;if(requested==0){feedback.Text=L.P("请先选择项目。","Select an item first.");return;}
   int added=AddManagedToQuick(list.SelectedItems.Cast<DockItem>().ToList());
   if(added==0){feedback.Text=L.P("快捷栏已满。可隐藏部分项目或增加行列数；总项目数最多为 50。","The panel is full. Hide some items or add rows or columns. Up to 50 total items can be stored.");return;}
   dialog.Close();Render();Say(L.F("已添加 {0} 项；另有 {1} 项因重复或容量限制未添加。","Added {0} items; {1} were skipped due to duplicates or capacity.",added,requested-added));
  });footer.Children.Add(add);DockPanel.SetDock(footer,Dock.Bottom);panel.Children.Add(footer);panel.Children.Add(list);dialog.Content=panel;dialog.ShowDialog();
 }
}
