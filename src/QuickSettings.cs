using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
namespace EurekaDock;
internal sealed partial class SettingsWindow {
 QuickGrid? quickGrid;
 internal int ImportQuick(string[] paths){int added=0;foreach(var path in paths){if(!QuickHasRoom)break;if((!File.Exists(path)&&!Directory.Exists(path))||C.QuickItems.Any(i=>string.Equals(i.Target,path,StringComparison.OrdinalIgnoreCase)))continue;C.QuickItems.Add(new DockItem{Name=Native.DisplayName(path),Target=path});added++;}if(added>0)Save();return added;}
 void QuickPage(){
  Heading(L.T("快捷栏"),L.T("点击最下方小点展开；打开应用或点击外部自动收起。拖入软件添加，按住图标可在二维网格中排序。灰色条目超出当前容量，但不会被删除。"));
  var sizes=new StackPanel{Orientation=Orientation.Horizontal};
  void Dimension(string label,int value,int maximum,Action<int> change){sizes.Children.Add(new TextBlock{Text=label,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,8,0)});var combo=new ComboBox{Width=65,Margin=new Thickness(0,0,20,0),ItemsSource=Enumerable.Range(1,maximum),SelectedItem=value};combo.SelectionChanged+=(s,e)=>{if(combo.SelectedItem is int n){change(n);Save();Render();}};sizes.Children.Add(combo);}
  Dimension(L.T("宽（列）"),C.QuickColumns,5,n=>C.QuickColumns=n);Dimension(L.T("高（行）"),C.QuickRows,10,n=>C.QuickRows=n);page.Children.Add(sizes);
  page.Children.Add(new TextBlock{Text=L.F("可容纳 {0} 项 · 已添加 {1} 项 · 大小与间距跟随 Dock","Capacity: {0} · Added: {1} · Size and spacing follow Dock",C.QuickColumns*C.QuickRows,C.QuickItems.Count),Margin=new Thickness(0,10,0,10)});
  var actions=new WrapPanel();
  actions.Children.Add(Button(L.T("添加文件 / 软件"),()=>{var d=new Microsoft.Win32.OpenFileDialog{Multiselect=true};if(d.ShowDialog(this)==true){int count=ImportQuick(d.FileNames);Render();Say(L.F("已添加 {0} 项；重复、无效或超出容量的项目已跳过。","Added {0} items. Duplicate, invalid, or excess items were skipped.",count));}}));
  actions.Children.Add(Button(L.T("添加文件夹"),()=>{using var d=new System.Windows.Forms.FolderBrowserDialog();if(d.ShowDialog()==System.Windows.Forms.DialogResult.OK){ImportQuick(new[]{d.SelectedPath});Render();}}));
  actions.Children.Add(Button(L.T("Windows 应用"),()=>{var all=new System.Collections.Generic.List<DockItem>();Native.EnumerateApps((name,path)=>all.Add(new DockItem{Name=name,Target=path}));var dialog=new Window{Title=L.T("添加到快捷栏"),Owner=this,Width=500,Height=560,WindowStartupLocation=WindowStartupLocation.CenterOwner};SettingsRendering.Stabilize(dialog);var list=new ListBox{ItemsSource=all.OrderBy(x=>x.Name).ToList()};list.MouseDoubleClick+=(s,e)=>{if(list.SelectedItem is DockItem item&&QuickHasRoom&&!C.QuickItems.Any(x=>x.Target==item.Target)){C.QuickItems.Add(item);Save();dialog.Close();Render();}};dialog.Content=list;dialog.ShowDialog();}));
  actions.Children.Add(Button(L.P("从应用管理添加","Add from Apps"),PickManagedApps));
  page.Children.Add(actions);
  quickGrid=new QuickGrid(C.QuickItems,C.QuickColumns,C.QuickColumns*C.QuickRows);quickGrid.Changed+=()=>{Save();Render();};
  var scroll=new ScrollViewer{Content=quickGrid,Height=Math.Max(220,Height-420),VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalContentAlignment=HorizontalAlignment.Left,AllowDrop=true,Margin=new Thickness(0,8,0,8)};
  scroll.PreviewDragOver+=(s,e)=>{if(e.Data.GetDataPresent(DataFormats.FileDrop)){e.Effects=DragDropEffects.Copy;e.Handled=true;}};
  scroll.PreviewDrop+=(s,e)=>{if(e.Data.GetData(DataFormats.FileDrop) is string[] paths){int count=ImportQuick(paths);e.Handled=true;Render();Say(L.F("已添加 {0} 项；重复、无效或超出容量的项目已跳过。","Added {0} items. Duplicate, invalid, or excess items were skipped.",count));}};
  page.Children.Add(scroll);
  var edit=new WrapPanel();edit.Children.Add(Button(L.T("改名"),()=>{if(quickGrid?.Selected is DockItem item){RenameDialog(item);Render();}}));
  edit.Children.Add(Button(L.T("编辑 / 启动参数"),()=>{if(quickGrid?.Selected is DockItem item&&Edit(item)){Save();Render();}}));
  edit.Children.Add(Button(L.T("从快捷栏删除"),()=>{if(quickGrid?.Selected is DockItem item){C.QuickItems.Remove(item);Save();Render();}}));var visibility=VisibilityButton(()=>quickGrid?.Selected,Render,out var updateVisibility);quickGrid.SelectionChanged+=updateVisibility;edit.Children.Add(visibility);page.Children.Add(edit);
 }
}
