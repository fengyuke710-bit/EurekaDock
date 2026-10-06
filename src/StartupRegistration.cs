using System;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using Microsoft.Win32;
namespace EurekaDock;
internal static class StartupRegistration {
 const string RunKey=@"Software\Microsoft\Windows\CurrentVersion\Run";
 internal static string LinkPath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup),"EurekaDock.lnk");
 internal static string LaunchTarget=>Environment.GetEnvironmentVariable("EUREKADOCK_LAUNCHER") is string launcher&&File.Exists(launcher)?Path.GetFullPath(launcher):Environment.ProcessPath!;
 internal static bool IsRegistered {
  get {using var run=Registry.CurrentUser.OpenSubKey(RunKey);return File.Exists(LinkPath)||run?.GetValue("EurekaDock")!=null;}
 }
 internal static string Quote(string value) {
  var result=new StringBuilder("\"");int slashes=0;
  foreach(char c in value) {
   if(c=='\\'){slashes++;continue;}
   result.Append('\\',c=='"'?slashes*2+1:slashes);result.Append(c);slashes=0;
  }
  return result.Append('\\',slashes*2).Append('"').ToString();
 }
 internal static string Arguments(string root)=>"--startup --data="+Quote(Path.GetFullPath(root));
 internal static void CreateVerified(string path,string executable,string root) {
  executable=Path.GetFullPath(executable);root=Path.GetFullPath(root);
  if(!File.Exists(executable))throw new FileNotFoundException(L.T("启动程序不存在"),executable);
  string directory=Path.GetDirectoryName(executable)!;
  string args=Arguments(root);
  Directory.CreateDirectory(Path.GetDirectoryName(path)!);
  string pending=path+".pending.lnk";
  try {
   Marshal.ThrowExceptionForHR(Native.WriteStartupLink(executable,args,directory,pending));
   var target=new StringBuilder(32768);var readArgs=new StringBuilder(32768);var workdir=new StringBuilder(32768);
   Marshal.ThrowExceptionForHR(Native.ReadStartupLink(pending,target,target.Capacity,readArgs,readArgs.Capacity,workdir,workdir.Capacity));
   if(!string.Equals(target.ToString(),executable,StringComparison.OrdinalIgnoreCase)||readArgs.ToString()!=args||!string.Equals(workdir.ToString(),directory,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException(L.T("启动快捷方式校验失败"));
   File.Move(pending,path,true);
  } finally {if(File.Exists(pending))File.Delete(pending);}
 }
 internal static void SetEnabled(bool enabled) {
  if(enabled)CreateVerified(LinkPath,LaunchTarget,App.Store.Root);
  else if(File.Exists(LinkPath))File.Delete(LinkPath);
  // Remove only our obsolete registration after the replacement is verified.
  using var run=Registry.CurrentUser.OpenSubKey(RunKey,true);run?.DeleteValue("EurekaDock",false);
  App.Store.Log("Autostart "+(enabled?"enabled: "+LinkPath+"; data="+App.Store.Root:"disabled"));
 }
}
