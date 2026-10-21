using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace EurekaDock;
internal sealed partial class SettingsWindow {
 void AboutPage(){
  Heading("EurekaDock",L.P("版本 1.5.0 · Windows 11 桌面启动栏","Version 1.5.0 · A desktop launcher for Windows 11"));
  void Section(string zhTitle,string enTitle,string zh,string en){
   page.Children.Add(new TextBlock{Text=L.P(zhTitle,enTitle),FontSize=18,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,12,0,8)});
   page.Children.Add(new TextBlock{Text=L.P(zh,en),TextWrapping=TextWrapping.Wrap,LineHeight=23,Foreground=Brushes.DimGray,Margin=new Thickness(0,0,0,12)});
  }
  Section("产品说明","About EurekaDock",
   "EurekaDock 将常用应用、文件和文件夹集中在透明的桌面启动栏中，提供平滑滚动、独立快捷栏和音乐律动。它显示在桌面层级，不替代 Windows 任务栏。\n\n所有配置保存在本机。应用完全离线运行，不含遥测、上传或自动更新。初版主要针对单显示器优化。",
   "EurekaDock brings your favorite apps, files, and folders together in a transparent desktop launcher, with smooth scrolling, a separate Quick Panel, and a music visualizer. It stays at desktop level and does not replace the Windows taskbar.\n\nYour configuration stays on this PC. The app works entirely offline, with no telemetry, uploads, or automatic updates. This first release is optimized for a single monitor.");
  Section("作者署名","Author","EurekaOrette","EurekaOrette");
  Section("1. 添加和管理项目","1. Add and manage items",
   "在“应用管理”中，将桌面快捷方式、应用、文件或文件夹拖入页面，也可使用添加按钮。拖动每行右侧的三横杠调整顺序。选中项目后可重命名、编辑路径或启动参数、更换图标。\n重命名只改变 Dock 内的名称；移除项目不会删除原文件。显示“找不到目标”时，请编辑路径，项目不会自动被删除。",
   "In Apps, drag desktop shortcuts, apps, files, or folders onto the page, or use the add buttons. Drag the handle on the right of a row to reorder it. Select an item to rename it, edit its path or launch arguments, or change its icon.\nRenaming affects only the Dock. Removing an item never deletes the original file. If an item says “Target not found,” update its path; it is not removed automatically.");
  Section("暂时隐藏项目","Temporarily hide items",
   "在应用管理或快捷栏设置中选中项目，点击“隐藏所选项目”。隐藏后仅从对应的 Dock 或快捷栏中消失，在设置中仍可编辑、排序或恢复显示。隐藏状态会自动保存。\n快捷栏可通过“从应用管理添加”批量复制已有项目，两处独立管理。隐藏项目不占可见容量；恢复显示后若超出容量，可增加行列数。",
   "Select an item in Apps or Quick Panel settings and click Hide selected item. It disappears only from that launcher and remains editable, sortable, and available to show again in Settings. Visibility is saved automatically.\nUse Add from Apps to copy existing items into the Quick Panel. The two lists remain independent. Hidden items do not use visible capacity; add rows or columns if showing them again exceeds capacity.");
  Section("2. 使用 Dock","2. Use the Dock",
   "单击图标打开项目，右键单击打开 Windows 菜单。在 Dock 上滚动鼠标滚轮，或按住后横向拖动，可浏览列表；直接拖动 Dock 图标不会改变排列顺序。\n在“Dock”中调整大小、位置和外观，并选择是否循环滚动。关闭 Dock 栏目后，音浪仍可单独显示；左侧按钮也可独立隐藏。在“动画与操作”中可反转滚轮方向。",
   "Click an icon to open an item, or right-click for its Windows menu. Scroll the wheel over the Dock, or hold and drag horizontally, to browse the list. Dragging on the Dock does not reorder items.\nUse Dock settings to adjust size, position, appearance, and loop scrolling. The visualizer can remain visible when the carousel is hidden, and the left-side controls can be hidden separately. Reverse the wheel direction in Motion & Input.");
  Section("软件图标与动画效果","App icon appearance and feedback",
   "在设置的“软件图标”栏目中，分别选择“点击反馈”和“常态显示”。两项默认均保留原有效果，可以独立搭配，修改立即生效并自动保存。\n“弹跳（macOS 风格）”在单击启动后让图标向上跳起，随后以逐渐减弱的幅度回弹；拖动和右键菜单不会触发。“镜面反射”在图标下方添加带透视倾斜的倒影，越向下越模糊、越透明。效果适用于 Dock 和快捷栏，图标大小、悬停放大和边缘渐隐仍沿用原设置。\n快捷栏中的项目启动后仍会自动收起。恢复任一“默认”选项即可关闭对应效果。反射预先生成并缓存，静止时不会持续计算。",
   "Open App Icons in Settings to choose Click feedback and Resting appearance independently. Both default to the previous behavior. Changes apply immediately and save automatically.\nBounce (macOS style) makes a confirmed launch click lift the icon through diminishing hops; dragging and context menus do not trigger it. Mirror reflection adds a perspective reflection below the icon that becomes blurrier and more transparent downwards. Effects apply to the Dock and Quick Panel while keeping the existing icon size, hover zoom, and edge fading.\nLaunching a Quick Panel item still closes the panel automatically. Choose Default to turn off either effect. Reflections are generated once and cached, with no continuous work when idle.");
  Section("3. 左侧控制按钮","3. Controls on the left",
   "齿轮：打开设置。\n眼睛：显示或隐藏桌面原有图标；睁眼表示显示，闭眼表示隐藏。\n左箭头：打开或收起快捷栏。快捷栏展开后，点击原箭头位置也可收起。",
   "Gear: open Settings.\nEye: show or hide your regular desktop icons. An open eye means they are visible; a closed eye means they are hidden.\nLeft arrow: open or close the Quick Panel. While it is open, click the original arrow position to close it.");
  Section("4. 配置快捷栏","4. Set up the Quick Panel",
   "在“快捷栏”页面拖入项目，或使用添加按钮。拖动网格中的图标调整顺序。最多可设置为 5 列、10 行；图标大小和间距跟随 Dock。\n减少容量不会删除已有项目，超出的项目会变灰，增加容量后恢复显示。打开项目或点击快捷栏外部会自动收起。",
   "Drag items onto the Quick Panel settings page or use the add buttons. Drag icons within the grid to reorder them. Choose up to 5 columns and 10 rows; icon size and spacing follow the Dock.\nReducing capacity keeps all items. Extra items appear dimmed in Settings and become visible again when you increase capacity. The panel closes when you open an item or click outside.");
  Section("5. 开启线性音浪","5. Turn on the Linear Wave",
   "在“音乐律动”中启用音浪并选择“线性音浪”，再播放音乐。可调整长度、位置、起伏高度和响应强度，音浪采用左右对称的单峰造型。两种音浪分别保存配色：可选默认渐变、单色或自定义多色渐变，使用色盘、十六进制色号或常用色，并在 10 个位置保存颜色。\n音浪跟随 Windows 默认播放设备和系统音量，不使用麦克风。没有反应时，请检查播放设备、音量和声音响应强度；部分受保护音频无法用于律动。",
   "Enable Music Visualizer and choose Linear Wave, then play music. Adjust its length, position, wave height, and sensitivity, with a symmetric central crest. The two visualizers save colors separately: choose the default gradient, a single color, or a custom gradient using the wheel, a hex code, or a preset, and save colors in 10 slots.\nThe wave follows the default Windows playback device and system volume without using the microphone. If it does not respond, check the playback device, volume, and sensitivity. Some protected audio cannot drive the visualizer.");
  Section("条形音浪与歌曲信息","Bar Wave and track information",
   "在“音乐律动”中选择“条形音浪”。默认由音浪铺满整条区域，独立频段只向上跳动，静音时是一排小方块。播放器开始播放歌曲后，展开专辑封面、缓慢旋转的黑胶、歌名和作者；暂停时保留，关闭播放器后收回，音浪平滑填满空位。歌曲文字跟随柱条配色，默认使用白色；可选单色或自定义渐变，保留明暗层次。使用全屏应用时，音浪采集与黑胶旋转暂停，离开全屏后恢复。\n歌曲信息由播放器通过 Windows 提供，不会联网查找歌曲。",
   "Choose Bar Wave in Music Visualizer. By default, bars fill the full area, rising with sound and resting as small blocks in silence. Album art, a slowly rotating record, title, and artist appear when a player starts a song. They remain while paused and retract when the player closes, with the bars smoothly filling the space. Track text follows the bar color, which defaults to white; choose a solid color or custom gradient while keeping the shaded texture. Audio capture and record rotation pause in fullscreen apps and resume afterward.\nTrack details are supplied by your player through Windows; EurekaDock does not look up songs online.");
  Section("6. 启动、退出与问题处理","6. Startup, exit, and troubleshooting",
   "在“常规”中设置登录后自动启动和界面语言。所有修改自动保存，无需点击保存按钮。\n关闭设置窗口不会退出程序。右键单击任务栏通知区域中的 EurekaDock 图标，可暂时隐藏 Dock、重启或完全退出。\n显示异常时可在“维护”中刷新 Dock；配置有问题时可检查文件或从有效备份恢复。Windows 原生菜单和文件选择窗口的语言由 Windows 决定。",
   "Set automatic startup and interface language in General. All changes are saved automatically; there is no Save button.\nClosing Settings does not exit the app. Right-click the EurekaDock icon in the Windows notification area to temporarily hide the Dock, restart, or exit completely.\nFor display issues, refresh the Dock in Maintenance. For configuration issues, check the files or restore a valid backup. Windows controls the language of native context menus and file dialogs.");
 }
}
