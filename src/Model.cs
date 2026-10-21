using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
namespace EurekaDock;
public sealed class DockItem {
 public Guid Id {get;set;}=Guid.NewGuid();
 public string Name {get;set;}="";
 public string Target {get;set;}="";
 public string Arguments {get;set;}="";
 public string? Icon {get;set;}
 public bool Hidden {get;set;}
 public bool Available => Target.StartsWith("shell:",StringComparison.OrdinalIgnoreCase)||Target.StartsWith("::{")||File.Exists(Target)||Directory.Exists(Target);
 public override string ToString()=>Name+(Hidden?L.P(" · 已隐藏"," · Hidden"):"")+(Available?"":L.T("  · 找不到目标"));
}
public sealed class Configuration {
 public string Language {get;set;}="zh-CN";
 public int QuickColumns {get;set;}=3;
 public int QuickRows {get;set;}=3;
 public List<DockItem> QuickItems {get;set;}=new();
 public bool DockEnabled {get;set;}=true;
 public bool ShowAccessories {get;set;}=true;
 public bool SuspendWhenCovered {get;set;}=true;
 public int Version {get;set;}=1;
 public double IconSize {get;set;}=40.1320754716981;
 public string IconClickFeedback {get;set;}="default";
 public string IconDisplayEffect {get;set;}="default";
 public double Gap {get;set;}=25.188679245283;
 public double WidthFraction {get;set;}=.5;
 public double VerticalFraction {get;set;}=.946686320754718;
 public double HoverScale {get;set;}=1.25;
 public double FadeWidth {get;set;}=90;
 public double Blur {get;set;}=1.50943396226415;
 public string MusicColorMode {get;set;}="original";
 public string BarColorMode {get;set;}="solid";
 public double MusicBarScale {get;set;}=1.19758745476473;
 public string MusicStyle {get;set;}="bars";
 public string MusicColor {get;set;}="#AAB7C8";
 public string BarColor {get;set;}="#FFFFFF";
 public List<string> MusicGradient {get;set;}=new(){"#AAB7C8","#B6A6C5","#8ABEBB"};
 public List<string> BarGradient {get;set;}=new(){"#FFFFFF","#C9D0DA"};
 public string?[] MusicColorMemory {get;set;}=new string?[10];
 public string?[] BarColorMemory {get;set;}=new string?[10];
 public bool MusicEnabled {get;set;}=true;
 public double MusicLength {get;set;}=.501900803992036;
 public double MusicOffset {get;set;}=-24.7515306006324;
 public double MusicAmplitude {get;set;}=29.8702830188687;
 public double MusicSensitivity {get;set;}=6;
 public bool InfiniteCarousel {get;set;}=false;
 public bool ReverseWheel {get;set;}=true;
 public bool ShowRecycleBin {get;set;}=false;
 public double DragThreshold {get;set;}=7;
 public double HoldMilliseconds {get;set;}=180;
 public double Spring {get;set;}=100;
 public double Damping {get;set;}=20;
 public List<DockItem> Items {get;set;}=new();
 public void Validate() {
  if(IconClickFeedback!="default"&&IconClickFeedback!="bounce")IconClickFeedback="default";
  if(IconDisplayEffect!="default"&&IconDisplayEffect!="reflection")IconDisplayEffect="default";
  if(MusicStyle!="line"&&MusicStyle!="bars")MusicStyle="line";
  if(MusicColorMode!="original"&&MusicColorMode!="solid"&&MusicColorMode!="custom")MusicColorMode="original";
  if(BarColorMode!="solid"&&BarColorMode!="custom")BarColorMode="solid";
  static bool ColorValid(string? s)=>s!=null&&System.Text.RegularExpressions.Regex.IsMatch(s,"^#[0-9a-fA-F]{6}$");
  if(!ColorValid(MusicColor))MusicColor="#AAB7C8";
  if(!ColorValid(BarColor))BarColor="#FFFFFF";
  if(MusicGradient==null||MusicGradient.Count<2||MusicGradient.Count>10||MusicGradient.Any(x=>!ColorValid(x)))MusicGradient=new(){"#AAB7C8","#B6A6C5","#8ABEBB"};
  if(BarGradient==null||BarGradient.Count<2||BarGradient.Count>10||BarGradient.Any(x=>!ColorValid(x)))BarGradient=new(){"#FFFFFF","#C9D0DA"};
  var memory=new string?[10];if(MusicColorMemory!=null)for(int i=0;i<Math.Min(10,MusicColorMemory.Length);i++)if(ColorValid(MusicColorMemory[i]))memory[i]=MusicColorMemory[i];MusicColorMemory=memory;
  var barMemory=new string?[10];if(BarColorMemory!=null)for(int i=0;i<Math.Min(10,BarColorMemory.Length);i++)if(ColorValid(BarColorMemory[i]))barMemory[i]=BarColorMemory[i];BarColorMemory=barMemory;
  if(Language!="en"&&Language!="zh-CN")Language="zh-CN";
  if(QuickColumns<1||QuickColumns>5||QuickRows<1||QuickRows>10||QuickItems==null||QuickItems.Count>50||QuickItems.Any(i=>i==null||string.IsNullOrWhiteSpace(i.Target)||i.Name==null||i.Arguments==null)||QuickItems.Select(i=>i.Id).Distinct().Count()!=QuickItems.Count)throw new InvalidDataException(L.T("快捷栏配置无效"));
  if(Version!=1||Items==null||Items.Count>1000) throw new InvalidDataException(L.T("不支持的配置版本或应用列表"));
  static void Range(double n,double min,double max) { if(!double.IsFinite(n)||n<min||n>max) throw new InvalidDataException(L.T("参数超出有效范围")); }
  Range(MusicBarScale,.6,2);
  Range(MusicLength,.15,1.5);Range(MusicOffset,-100,400);Range(MusicAmplitude,4,70);Range(MusicSensitivity,.2,6);
  Range(IconSize,32,128); Range(Gap,8,80); Range(WidthFraction,.3,.95); Range(VerticalFraction,.1,.97);
  Range(HoverScale,1,1.5); Range(FadeWidth,0,250); Range(Blur,0,8); Range(DragThreshold,3,20); Range(HoldMilliseconds,100,500);
  Range(Spring,40,200); Range(Damping,10,35);
  if(Items.Any(i=>i==null||string.IsNullOrWhiteSpace(i.Target)||i.Name==null||i.Arguments==null)||Items.Select(i=>i.Id).Distinct().Count()!=Items.Count) throw new InvalidDataException(L.T("应用条目无效"));
 }
}
public sealed class Store {
 public string Root {get;}
 public Configuration Config {get;private set;}=new();
 public string Recovery {get;private set;}="";
 public event Action? Changed;
 static readonly JsonSerializerOptions Json=new() { WriteIndented=true };
 string Main=>Path.Combine(Root,"config.json");
 string Backup=>Path.Combine(Root,"config.backup.json");
 public Store(string root) { Root=root; Directory.CreateDirectory(root); Directory.CreateDirectory(Path.Combine(root,"logs")); }
 public void Log(string text) {
  try {
   string dir=Path.Combine(Root,"logs"), file=Path.Combine(dir,DateTime.Now.ToString("yyyy-MM-dd")+".log");
   if(File.Exists(file)&&new FileInfo(file).Length>1024*1024) File.Move(file,Path.Combine(dir,DateTime.Now.ToString("yyyy-MM-dd-HHmmssfff")+".log"));
   File.AppendAllText(file,DateTime.Now.ToString("O")+" "+text+Environment.NewLine);
   foreach(var old in new DirectoryInfo(dir).GetFiles("*.log").OrderByDescending(f=>f.LastWriteTimeUtc).Skip(7)) old.Delete();
  } catch { }
 }
 static Configuration Read(string path) { var c=JsonSerializer.Deserialize<Configuration>(File.ReadAllText(path))??throw new InvalidDataException(L.T("配置为空")); c.Validate(); return c; }
 void Preserve(string path) { if(File.Exists(path)) File.Move(path,path+".bad-"+DateTime.UtcNow.ToString("yyyyMMddHHmmssfff")+"-"+Guid.NewGuid().ToString("N")); }
 public void Load() {
  Recovery="";
  try { if(File.Exists(Main)) { Config=Read(Main); return; } }
  catch(Exception ex) { Log("Config invalid: "+ex.Message); Preserve(Main); Recovery=L.T("主配置损坏，已保留原文件。"); }
  if(File.Exists(Backup)) {
   try { Config=Read(Backup); Recovery+=L.T("已从备份恢复。"); Save(); return; }
   catch(Exception ex) { Log("Backup invalid: "+ex.Message); Preserve(Backup); Recovery+=L.T("备份也无效，已保留。"); }
  }
  Config=new(); Save();
 }
 public void Save() {
  Config.Validate();
  string temporary=Main+".tmp";
  File.WriteAllText(temporary,JsonSerializer.Serialize(Config,Json)); Read(temporary);
  if(File.Exists(Main)) {
   bool valid; try { Read(Main); valid=true; } catch { valid=false; }
   if(valid) File.Copy(Main,Backup,true); else Preserve(Main);
  }
  File.Move(temporary,Main,true);
  if(!File.Exists(Backup)) File.Copy(Main,Backup);
  Changed?.Invoke();
 }
 public string Verify() { Read(Main); Read(Backup); return L.T("主配置与备份校验通过。"); }
}
// Units are slots and seconds, independent of monitor DPI and rendering framework.
public sealed class Carousel {
 double wheelRemainder;
 bool infinite=true;int count;double halfSpan,rebound,reboundVelocity;
 public double Minimum=>count-1<=halfSpan*2?-(Math.Max(0,count-1))/2.0:-(count-1)+halfSpan;
 public double Maximum=>count-1<=halfSpan*2?-(Math.Max(0,count-1))/2.0:-halfSpan;
 public double VisualPosition=>Position+rebound;
 public double Bound(double value)=>infinite?value:Math.Clamp(value,Minimum,Maximum);
 public void Configure(bool loop,int itemCount,double visibleHalfSpan=0){bool changed=infinite!=loop;infinite=loop;count=Math.Max(0,itemCount);halfSpan=Math.Max(0,visibleHalfSpan);if(changed){wheelRemainder=0;rebound=reboundVelocity=0;if(!loop&&count>0)Position=-((-Position%count+count)%count);Target=Math.Round(Position);Velocity=0;}Position=Bound(Position);Target=Bound(Target);if(!loop&&count<=1)Velocity=0;}
 public void DragBy(double slots){rebound=reboundVelocity=0;Position=Bound(Position+slots);Target=Position;}
 public double Position, Velocity, Target;
 public bool Dragging;
 public bool Moving=>Math.Abs(Target-Position)>.0005||Math.Abs(Velocity)>.002||Math.Abs(rebound)>.0001||Math.Abs(reboundVelocity)>.001;
 public void Wheel(double slots) { if(Dragging)return; wheelRemainder+=slots;double steps=Math.Truncate(wheelRemainder+Math.Sign(wheelRemainder)*1e-9);wheelRemainder-=steps;double proposed=Target+steps;Target=Bound(proposed);if(!infinite&&count>1&&steps!=0&&proposed!=Target&&Math.Abs(Position-Target)<.08)reboundVelocity=Math.Clamp(reboundVelocity+Math.Sign(steps)*4,-6,6); }
 public double Snap(double value) {
  value=Bound(value);double snapped=Bound(Math.Round(value));
  if(!infinite){if(Math.Abs(value-Minimum)<Math.Abs(value-snapped))snapped=Minimum;if(Math.Abs(value-Maximum)<Math.Abs(value-snapped))snapped=Maximum;}
  return snapped;
 }
 public void Release(double velocity) { Dragging=false; Velocity=Math.Clamp(velocity,-18,18); Target=Snap(Position+Velocity*.16); }
 public void Step(double dt,double spring=100,double damping=20) {
  if(Dragging) return;
  dt=Math.Clamp(dt,0,.05);
  while(dt>0) { double h=Math.Min(dt,1.0/240); Velocity+=(spring*(Target-Position)-damping*Velocity)*h; Position+=Velocity*h;if(!infinite){double bounded=Bound(Position);if(bounded!=Position){Position=bounded;Velocity=0;}} reboundVelocity+=(-220*rebound-22*reboundVelocity)*h;rebound+=reboundVelocity*h;if(Math.Abs(rebound)>.28){rebound=Math.Sign(rebound)*.28;reboundVelocity=0;}dt-=h; }
  if(Math.Abs(rebound)<.0001&&Math.Abs(reboundVelocity)<.001)rebound=reboundVelocity=0;
  if(!Moving) { Position=Target; Velocity=0; }
 }
 public static int Wrap(int index,int count)=>((index%count)+count)%count;
}

// Exact critically damped response: retain value and velocity when the target
// changes, so entering, leaving and restoring hover never restart an easing.
public sealed class HoverResponse {
 public double Value {get;private set;}
 public double Velocity {get;private set;}
 public bool Moving {get;private set;}
 public void Step(double target,double dt) {
  target=Math.Clamp(target,0,.5);
  const double omega=26;
  dt=Math.Clamp(dt,0,.05);
  double offset=Value-target, coefficient=Velocity+omega*offset;
  double decay=Math.Exp(-omega*dt);
  Value=target+(offset+coefficient*dt)*decay;
  Velocity=(Velocity-omega*coefficient*dt)*decay;
  if(Value<0) {Value=0;Velocity=0;}
  if(Value>.5) {Value=.5;Velocity=0;}
  Moving=Math.Abs(Value-target)>.0001||Math.Abs(Velocity)>.001;
  if(!Moving) {Value=target;Velocity=0;}
 }
}

public sealed class PopSpring {
 public double Value {get;private set;}
 public double Velocity {get;private set;}
 public double Target {get;private set;}
 public bool Moving=>Math.Abs(Value-Target)>.0001||Math.Abs(Velocity)>.001;
 public PopSpring(double value=0) {Value=Target=value;}
 public void SetTarget(double target)=>Target=target;
 public void Kick(double velocity)=>Velocity+=velocity;
 public void Step(double dt) {
  dt=Math.Clamp(dt,0,.05);
  while(dt>0) {double h=Math.Min(dt,1.0/240);Velocity+=(230*(Target-Value)-18*Velocity)*h;Value+=Velocity*h;dt-=h;}
  if(Target==0&&Value<=0) {Value=0;Velocity=0;}
  if(!Moving) {Value=Target;Velocity=0;}
 }
}





