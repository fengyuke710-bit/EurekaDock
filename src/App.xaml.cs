using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Windows;
using Forms = System.Windows.Forms;
namespace EurekaDock;
public partial class App : Application {
 internal static Store Store = null!;
 internal DockWindow Dock = null!;
 DesktopLifecycle? desktop;
 SettingsWindow? settings;
 Forms.NotifyIcon? tray;
 System.Drawing.Icon? trayIcon;
 Mutex? instance;
 bool ownsMutex;
 System.Windows.Threading.DispatcherTimer? startupWait;
 protected override void OnStartup(StartupEventArgs e) {
  base.OnStartup(e);
  var marker=e.Args.FirstOrDefault(x=>x.StartsWith("--launch-marker="))?[16..];
  if(marker!=null) {File.WriteAllText(marker,"launch and arguments OK");Shutdown();return;}
  var data=e.Args.FirstOrDefault(x=>x.StartsWith("--data="))?[7..];
  Store=new Store(data??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"EurekaDock"));
  if(e.Args.Contains("--coverage-window-probe")){FullscreenProbes.RunCoverageWindow(e.Args.First(x=>x.StartsWith("--coverage-phase="))[17..]);return;}
  if(e.Args.Contains("--fullscreen-probe")) {try{FullscreenProbes.Run(Store);Shutdown(0);}catch(Exception ex){File.WriteAllText(Path.Combine(Store.Root,"fullscreen-failure.txt"),ex.ToString());Shutdown(1);}return;}
  if(e.Args.Contains("--default-settings-probe")) {try{DefaultSettingsProbes.Run(Store);Shutdown(0);}catch(Exception ex){File.WriteAllText(Path.Combine(Store.Root,"default-settings-failure.txt"),ex.ToString());Shutdown(1);}return;}
  if(e.Args.Contains("--audio-layout-probe")) {try{AudioLayoutProbes.Run(Store);Shutdown(0);}catch(Exception ex){File.WriteAllText(Path.Combine(Store.Root,"audio-layout-failure.txt"),ex.ToString());Shutdown(1);}return;}
  if(e.Args.Contains("--visibility-probe")) {try{VisibilityProbes.Run(Store);Shutdown(0);}catch(Exception ex){File.WriteAllText(Path.Combine(Store.Root,"visibility-failure.txt"),ex.ToString());Shutdown(1);}return;}
  if(e.Args.Contains("--language-probe")) {try{LanguageProbes.Run(Store);Shutdown(0);}catch(Exception ex){File.WriteAllText(Path.Combine(Store.Root,"language-failure.txt"),ex.ToString());Shutdown(1);}return;}
  if(e.Args.Contains("--barwave-probe")) {try{BarWaveProbes.Run(Store);Shutdown(0);}catch(Exception ex){File.WriteAllText(Path.Combine(Store.Root,"barwave-failure.txt"),ex.ToString());Shutdown(1);}return;}
  if(e.Args.Contains("--wavecolor-probe")) {try{WaveColorProbes.Run(Store);Shutdown(0);}catch(Exception ex){File.WriteAllText(Path.Combine(Store.Root,"wavecolor-failure.txt"),ex.ToString());Shutdown(1);}return;}
  if(e.Args.Contains("--music-probe")) {try{MusicProbes.Run(Store);Shutdown(0);}catch(Exception ex){File.WriteAllText(Path.Combine(Store.Root,"music-failure.txt"),ex.ToString());Shutdown(1);}return;}
  if(e.Args.Contains("--edge-probe")) {try{EdgeProbes.Run(Store);Shutdown(0);}catch(Exception ex){File.WriteAllText(Path.Combine(Store.Root,"edge-failure.txt"),ex.ToString());Shutdown(1);}return;}
  if(e.Args.Contains("--quick-probe")) {try{QuickProbes.Run(Store);Shutdown(0);}catch(Exception ex){File.WriteAllText(Path.Combine(Store.Root,"quick-failure.txt"),ex.ToString());Shutdown(1);}return;}
  if(e.Args.Contains("--list-probe")) {try{ListProbes.Run(Store);Shutdown(0);}catch(Exception ex){File.WriteAllText(Path.Combine(Store.Root,"list-failure.txt"),ex.ToString());Shutdown(1);}return;}
  if(e.Args.Contains("--usability-probe")) {try{UsabilityProbes.Run(Store);Shutdown(0);}catch(Exception ex){File.WriteAllText(Path.Combine(Store.Root,"usability-failure.txt"),ex.ToString());Shutdown(1);}return;}
  DispatcherUnhandledException+=(s,a)=> { Store.Log("Unhandled UI: "+a.Exception); a.Handled=true; MessageBox.Show(a.Exception.Message,"EurekaDock"); };
  AppDomain.CurrentDomain.UnhandledException+=(s,a)=>Store.Log("Fatal: "+a.ExceptionObject);
  if(e.Args.Contains("--configure-startup")) {try{StartupRegistration.SetEnabled(true);Shutdown(0);}catch(Exception ex){Store.Log("Autostart setup failed: "+ex);Shutdown(1);}return;}
  if(e.Args.Contains("--startup-probe")) {try{StartupProbes.Run(Store);Shutdown(0);}catch(Exception ex){File.WriteAllText(Path.Combine(Store.Root,"startup-probe-failure.txt"),ex.ToString());Shutdown(1);}return;}
  if(e.Args.Contains("--probe")) { try { Probes.Run(Store); Shutdown(0); } catch(Exception ex) { Store.Log(ex.ToString()); File.WriteAllText(Path.Combine(Store.Root,"probe-failure.txt"),ex.ToString()); Shutdown(1); } return; }
  if(e.Args.Contains("--smoke")) {Probes.Smoke(Store);return;}
  if(e.Args.Contains("--visual-probe")) {try{VisualProbes.Run(Store);}catch(Exception ex){File.WriteAllText(Path.Combine(Store.Root,"visual-failure.txt"),ex.ToString());Shutdown(1);}return;}
  if(e.Args.Contains("--accessory-probe")) {try{AccessoryProbes.Run(Store);}catch(Exception ex){File.WriteAllText(Path.Combine(Store.Root,"accessory-failure.txt"),ex.ToString());Shutdown(1);}return;}
  instance=new Mutex(true,"Local\\EurekaDock",out ownsMutex);
  if(!ownsMutex) { if(e.Args.Contains("--settings"))Native.OpenExistingSettings(); Store.Log("Duplicate instance skipped; data="+Store.Root); Shutdown(); return; }
  Store.Log("Startup entry; mode="+(e.Args.Contains("--startup")?"login":"manual")+"; exe="+Environment.ProcessPath+"; data="+Store.Root);
  Store.Load();
  bool login=e.Args.Contains("--startup");
  if(login&&Native.DesktopHost()==IntPtr.Zero) {
   Store.Log("Login: waiting for Explorer desktop");int attempts=0;
   startupWait=new System.Windows.Threading.DispatcherTimer {Interval=TimeSpan.FromSeconds(1)};
   startupWait.Tick+=(s,a)=>{if(Native.DesktopHost()!=IntPtr.Zero||++attempts>=60){startupWait.Stop();StartDock(true);}};
   startupWait.Start();return;
  }
  StartDock(login);
 }
 void StartDock(bool login) {
  desktop=new DesktopLifecycle(this);desktop.Start();
  using(var iconStream=Assembly.GetExecutingAssembly().GetManifestResourceStream("EurekaDock.Assets.EurekaDock.ico")??throw new InvalidOperationException("Embedded EurekaDock icon is missing")) trayIcon=new System.Drawing.Icon(iconStream);
  tray=new Forms.NotifyIcon { Text="EurekaDock", Icon=trayIcon, Visible=true };
  var menu=new Forms.ContextMenuStrip();
  menu.Items.Add(L.T("打开设置"),null,(s,a)=>OpenSettings());
  var hide=new Forms.ToolStripMenuItem(L.T("暂时隐藏 Dock")) { CheckOnClick=true };
  hide.CheckedChanged+=(s,a)=>desktop.SetHidden(hide.Checked); menu.Items.Add(hide);
  menu.Items.Add(L.T("重启 EurekaDock"),null,(s,a)=>Restart());
  menu.Items.Add(L.T("退出"),null,(s,a)=>Shutdown());
  tray.ContextMenuStrip=menu; tray.DoubleClick+=(s,a)=>OpenSettings();
  Store.Log("Startup; rendering tier="+(System.Windows.Media.RenderCapability.Tier>>16));
  Store.Log("Configuration loaded; items="+Store.Config.Items.Count);
  if((Store.Config.Items.Count==0&&!login)||Environment.GetCommandLineArgs().Contains("--settings")) OpenSettings();
 }
 internal void OpenSettings() { if(settings==null) { settings=new SettingsWindow(); settings.Closed+=(s,a)=>{if(ReferenceEquals(MainWindow,settings))MainWindow=null;settings=null;}; } settings.Show(); settings.Activate(); }
 internal void ReloadDock(){if(desktop!=null)desktop.Reload();else Dock?.Reload();}
 internal void RefreshTrayLanguage(){
  if(tray?.ContextMenuStrip is not {} menu)return;
  string[] labels={L.T("打开设置"),L.T("暂时隐藏 Dock"),L.T("重启 EurekaDock"),L.T("退出")};
  for(int i=0;i<labels.Length;i++)menu.Items[i].Text=labels[i];
 }
 internal void Restart() { var exe=StartupRegistration.LaunchTarget; var root=Store.Root; Shutdown(); Process.Start(new ProcessStartInfo(exe) { Arguments="--data=\""+root+"\"", UseShellExecute=true }); }
 protected override void OnExit(ExitEventArgs e) { startupWait?.Stop(); desktop?.Dispose();Dock?.Close(); tray?.Dispose(); trayIcon?.Dispose(); Store?.Log("Exit"); if(ownsMutex) instance?.ReleaseMutex(); instance?.Dispose(); base.OnExit(e); }
}



