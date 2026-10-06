using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Text.Json;
namespace EurekaDock;
internal static class StartupProbes {
 internal static void Run(Store store) {
  var results=new List<string>();
  void Check(bool ok,string name){if(!ok)throw new Exception(name);results.Add("PASS "+name);}
  store.Load();
  string shortcut=Path.Combine(store.Root,"startup-entry.lnk");
  string expectedTarget=StartupRegistration.LaunchTarget;
  StartupRegistration.CreateVerified(shortcut,expectedTarget,store.Root);
  var target=new StringBuilder(32768);var args=new StringBuilder(32768);var directory=new StringBuilder(32768);
  Check(Native.ReadStartupLink(shortcut,target,target.Capacity,args,args.Capacity,directory,directory.Capacity)==0,"startup shortcut reads back");
  Check(string.Equals(target.ToString(),expectedTarget,StringComparison.OrdinalIgnoreCase),"exact launch target retained");
  Check(args.ToString()==StartupRegistration.Arguments(store.Root),"current data path and silent login flag retained");
  Check(directory.ToString()==Path.GetDirectoryName(expectedTarget),"working directory is the launcher folder");
  Check(args.ToString().Contains("--startup")&&args.ToString().Contains("--data="),"login does not fall back to unrelated default config");
  results.Add("INFO Combined target and arguments length="+(target.Length+args.Length+3));
  results.Add("INFO Configured items="+store.Config.Items.Count);
  results.Add("INFO Shortcut="+shortcut);
  File.WriteAllText(Path.Combine(store.Root,"startup-probe-results.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions {WriteIndented=true}));
 }
}
