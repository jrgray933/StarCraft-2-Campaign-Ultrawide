using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

public static class SC2HubGate {
 public static ulong SceneRva {get{return SC2Addresses.Rva(0x4046470);}}
 public sealed class Scene {
  public bool Allowed;public string Asset="";public ulong Render,Model,Entry,Definition,Session,Info,Cameras;public uint Index,CameraCount;
  public string Key {get{return Model.ToString("X")+":"+Entry.ToString("X")+":"+Definition.ToString("X");}}
 }
 public static byte[] Read(int pid,ulong a,int n){if(a<0x10000)throw new InvalidOperationException("Scene is unavailable.");var b=SC2Memory.Read(pid,a,n);if(b==null||b.Length!=n)throw new InvalidOperationException("Scene is unavailable.");return b;}
 public static ulong Ptr(int pid,ulong a){return BitConverter.ToUInt64(Read(pid,a,8),0);}
 public static uint U32(int pid,ulong a){return BitConverter.ToUInt32(Read(pid,a,4),0);}
 static readonly string[] HubPaths={"campaign/tstory01.sc2map","campaign/swarm/zstorychar.sc2map","campaign/swarm/zstoryexpedition.sc2map","campaign/swarm/zstoryzerus.sc2map"};
 public static bool HubMap(string map){if(map=="")return true;string canonical=SC2CampaignGate.CanonicalPath(map);foreach(string path in HubPaths)if(String.Equals(canonical,"maps/"+path,StringComparison.OrdinalIgnoreCase))return true;return false;}
 public static bool Matches(bool online,string map,string asset){return !online&&HubMap(map)&&asset!=null&&asset.Replace('\\','/').StartsWith("assets/storymodesets/",StringComparison.OrdinalIgnoreCase);}
 static Scene Observe(int pid,ulong module){
  var s=new Scene();s.Session=Ptr(pid,module+SC2CampaignGate.SessionRva);s.Info=Ptr(pid,s.Session+0x60);
  if((U32(pid,s.Info+0x1ec8)&2)!=0)return s;
  var mapBytes=Read(pid,module+SC2CampaignGate.MapRva,260);int mapEnd=Array.IndexOf(mapBytes,(byte)0);if(mapEnd<0)return s;
  string map=Encoding.ASCII.GetString(mapBytes,0,mapEnd);if(!HubMap(map))return s;
  ulong manager=Ptr(pid,module+SceneRva),scene=Ptr(pid,manager+0x1b28),bundle=Ptr(pid,scene+0x550),camera=Ptr(pid,bundle+0x10);
  s.Render=Ptr(pid,camera+8);s.Model=Ptr(pid,s.Render);s.Index=U32(pid,s.Render+8);if(s.Index>=1024)return s;
  ulong actor=Ptr(pid,s.Model+0x158),name=Ptr(pid,actor+0x40);var bytes=Read(pid,name,260);int end=Array.IndexOf(bytes,(byte)0);if(end<0)return s;
  s.Asset=Encoding.ASCII.GetString(bytes,0,end);if(!Matches(false,map,s.Asset))return s;
  s.Cameras=Ptr(pid,s.Model+0x448);s.CameraCount=U32(pid,s.Model+0x450);if(s.Cameras<0x10000||s.CameraCount==0||s.CameraCount>256||s.Index>=s.CameraCount)return s;
  s.Entry=s.Cameras+s.Index*0x78;s.Definition=Ptr(pid,s.Entry+0x50);
  s.Allowed=s.Definition>=0x10000;return s;
 }
 public static Scene Check(int pid,ulong module){try{
  SC2Addresses.Ensure(pid,module);
  using(var p=Process.GetProcessById(pid)){if((ulong)p.MainModule.BaseAddress.ToInt64()!=module||!String.Equals(Path.GetFileName(p.MainModule.FileName),"SC2_x64.exe",StringComparison.OrdinalIgnoreCase))return new Scene();}
  var a=Observe(pid,module);if(!a.Allowed)return a;var b=Observe(pid,module);
  if(!b.Allowed||a.Key!=b.Key||a.Asset!=b.Asset||a.Session!=b.Session||a.Info!=b.Info||a.Render!=b.Render||a.Cameras!=b.Cameras||a.CameraCount!=b.CameraCount)return new Scene();return b;
 }catch{return new Scene();}}
 public static void B(List<byte> c,string hex){foreach(var x in hex.Split(' '))if(x.Length!=0)c.Add(Convert.ToByte(x,16));}
 public static int J(List<byte> c,string op){B(c,op);int p=c.Count;c.AddRange(new byte[4]);return p;}
 public static void Fix(List<byte> c,int p,int target){var b=BitConverter.GetBytes(target-p-4);for(int i=0;i<4;i++)c[p+i]=b[i];}
 static void Nonzero(List<byte> c,List<int> denied){B(c,"48 85 C0");denied.Add(J(c,"0F 84"));}
 static void Load(List<byte> c,List<int> denied,int offset){B(c,"48 8B 80");c.AddRange(BitConverter.GetBytes(offset));Nonzero(c,denied);}
 static void EmitPath(List<byte> c,List<int> mismatch,string path){
  for(int i=0;i<path.Length;i++){
   B(c,"41 0F B6 43");c.Add((byte)i);
   if(path[i]=='/'){B(c,"3C 5C");int slash=J(c,"0F 84");B(c,"3C 2F");mismatch.Add(J(c,"0F 85"));Fix(c,slash,c.Count);}
   else{B(c,"0C 20 3C");c.Add((byte)path[i]);mismatch.Add(J(c,"0F 85"));}
  }
 }
 static void EmitHubMap(List<byte> c,List<int> denied,ulong map){
  B(c,"49 BB");c.AddRange(BitConverter.GetBytes(map));B(c,"41 80 3B 00");var allowed=new List<int>{J(c,"0F 84")};
  // Normalize the optional maps/ prefix once so both path forms share the guard.
  var relative=new List<int>();EmitPath(c,relative,"maps/");B(c,"49 83 C3 05");SC2CampaignGate.Resolve(c,relative,c.Count);
  foreach(string path in HubPaths){
   var next=new List<int>();EmitPath(c,next,path);
   B(c,"41 80 7B");c.Add((byte)path.Length);c.Add(0);allowed.Add(J(c,"0F 84"));
   SC2CampaignGate.Resolve(c,next,c.Count);
  }
  denied.Add(J(c,"E9"));SC2CampaignGate.Resolve(c,allowed,c.Count);
 }
 // RAX/R11 only: safe inside the resolution callback as well as the camera wrapper.
 public static void Emit(List<byte> c,List<int> denied,ulong session,ulong map,ulong scene,ulong expectedModel,uint index){
  B(c,"48 B8");c.AddRange(BitConverter.GetBytes(session));Load(c,denied,0);Load(c,denied,0x60);B(c,"F6 80 C8 1E 00 00 02");denied.Add(J(c,"0F 85"));
  EmitHubMap(c,denied,map);
  B(c,"48 B8");c.AddRange(BitConverter.GetBytes(scene));Load(c,denied,0);Load(c,denied,0x1b28);Load(c,denied,0x550);Load(c,denied,0x10);Load(c,denied,8);
  if(expectedModel!=0){B(c,"81 78 08");c.AddRange(BitConverter.GetBytes(index));denied.Add(J(c,"0F 85"));}
  Load(c,denied,0);
  if(expectedModel!=0){B(c,"49 BB");c.AddRange(BitConverter.GetBytes(expectedModel));B(c,"4C 39 D8");denied.Add(J(c,"0F 85"));}
  Load(c,denied,0x158);Load(c,denied,0x40);B(c,"49 89 C3");
  const string prefix="assets/storymodesets/";
  for(int i=0;i<prefix.Length;i++){
   B(c,"41 0F B6 43");c.Add((byte)i);
   if(prefix[i]=='/'){B(c,"3C 5C");int slash=J(c,"0F 84");B(c,"3C 2F");denied.Add(J(c,"0F 85"));Fix(c,slash,c.Count);}
   else{B(c,"0C 20 3C");c.Add((byte)prefix[i]);denied.Add(J(c,"0F 85"));}
  }
  // Bounded terminator check: never accept a partial/unreadable asset name.
  B(c,"4C 89 D8 48 05 04 01 00 00");int scan=c.Count;B(c,"41 80 3B 00");int found=J(c,"0F 84");B(c,"49 FF C3 49 39 C3");int again=J(c,"0F 82");denied.Add(J(c,"E9"));Fix(c,again,scan);Fix(c,found,c.Count);
 }
}
public static class SC2DisplayGate {
 public const int Revision=3;
 public static bool Allowed(int pid,ulong module){return SC2CampaignGate.Check(pid,module).Allowed||SC2HubGate.Check(pid,module).Allowed;}
 public static void Require(int pid,ulong module){if(!Allowed(pid,module))throw new InvalidOperationException("Waiting for an offline campaign.");}
 public static void Emit(List<byte> c,List<int> denied,ulong session,ulong map,ulong scene){
  var hub=new List<int>();SC2CampaignGate.Emit(c,hub,session,map);int allowed=SC2HubGate.J(c,"E9");SC2CampaignGate.Resolve(c,hub,c.Count);
  SC2HubGate.Emit(c,denied,session,map,scene,0,0);SC2HubGate.Fix(c,allowed,c.Count);
 }
}
public sealed class SC2HubCamera {
 [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr OpenProcess(uint a,bool inherit,int pid);
 [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr h);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool WriteProcessMemory(IntPtr h,IntPtr a,byte[] b,UIntPtr n,out UIntPtr wrote);
 [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr VirtualAllocEx(IntPtr h,IntPtr a,UIntPtr n,uint flags,uint protection);
 [DllImport("kernel32.dll")]static extern bool VirtualProtectEx(IntPtr h,IntPtr a,UIntPtr n,uint protection,out uint old);
 [DllImport("kernel32.dll")]static extern bool FlushInstructionCache(IntPtr h,IntPtr a,UIntPtr n);
 const ulong Magic=0x33425548324353UL;
 sealed class Hook {public ulong Entry,Definition,Model,Original,Table,Code,State;public uint Index,OriginalVertical;}
 readonly int pid;readonly ulong module;readonly string record;readonly long started;readonly List<Hook> hooks=new List<Hook>();
 public SC2HubCamera(int pid,ulong module,string record){this.pid=pid;this.module=module;this.record=record;using(var p=Process.GetProcessById(pid))started=p.StartTime.ToUniversalTime().Ticks;Load();}
 ulong Ptr(ulong a){return SC2HubGate.Ptr(pid,a);}uint U32(ulong a){return SC2HubGate.U32(pid,a);}
 void Write(ulong a,byte[] bytes){var h=OpenProcess(0x438,false,pid);if(h==IntPtr.Zero)throw new InvalidOperationException("Cannot update the hub camera.");try{Write(h,a,bytes);}finally{CloseHandle(h);}}
 static void Write(IntPtr h,ulong a,byte[] bytes){UIntPtr n;if(!WriteProcessMemory(h,(IntPtr)(long)a,bytes,(UIntPtr)bytes.Length,out n)||n.ToUInt64()!=(ulong)bytes.Length)throw new InvalidOperationException("Cannot update the hub camera.");}
 bool Owned(Hook h){try{return Ptr(h.Entry)==h.Table&&Ptr(h.Entry+0x50)==h.Definition&&Ptr(h.Table+0x180)==Magic&&h.State==h.Table+0x200;}catch{return false;}}
 void Load(){if(!File.Exists(record))return;try{
  var lines=File.ReadAllLines(record);if(lines.Length==0||lines[0]!=pid+":"+started)return;
  for(int i=1;i<lines.Length;i++){var v=lines[i].Split(':');if(v.Length!=9)continue;var h=new Hook{Entry=UInt64.Parse(v[0],NumberStyles.HexNumber),Definition=UInt64.Parse(v[1],NumberStyles.HexNumber),Model=UInt64.Parse(v[2],NumberStyles.HexNumber),Original=UInt64.Parse(v[3],NumberStyles.HexNumber),Table=UInt64.Parse(v[4],NumberStyles.HexNumber),Code=UInt64.Parse(v[5],NumberStyles.HexNumber),State=UInt64.Parse(v[6],NumberStyles.HexNumber),Index=UInt32.Parse(v[7]),OriginalVertical=UInt32.Parse(v[8])};if(h.OriginalVertical<=1&&h.Original==module+SC2Addresses.Rva(0x2e94880)&&Owned(h)){Write(h.State,BitConverter.GetBytes(0));hooks.Add(h);}}
 }catch{throw new InvalidOperationException("Close the game before restarting camera setup.");}}
 void Save(){var lines=new List<string>{pid+":"+started};foreach(var h in hooks)lines.Add(String.Format("{0:X}:{1:X}:{2:X}:{3:X}:{4:X}:{5:X}:{6:X}:{7}:{8}",h.Entry,h.Definition,h.Model,h.Original,h.Table,h.Code,h.State,h.Index,h.OriginalVertical));File.WriteAllLines(record,lines);}
 bool Restore(Hook h){if(!Owned(h))return true;Write(h.State,BitConverter.GetBytes(0));if(U32(h.State+4)!=0)return false;
  if(!Owned(h))return true;Write(h.Entry,BitConverter.GetBytes(h.Original));if(U32(h.Definition+0x24)==1)Write(h.Definition+0x24,BitConverter.GetBytes(h.OriginalVertical));return true;}
 public bool Stop(){bool changed=false;for(int i=hooks.Count-1;i>=0;i--){if(Restore(hooks[i])){hooks.RemoveAt(i);changed=true;}}if(changed)Save();return hooks.Count==0;}
 public static bool SameCameraSet(SC2HubGate.Scene a,SC2HubGate.Scene b){return a.Allowed&&b.Allowed&&a.Model==b.Model&&a.Cameras==b.Cameras&&a.CameraCount==b.CameraCount;}
 public static bool ContainsCamera(SC2HubGate.Scene scene,ulong model,ulong entry,uint index){return scene.Allowed&&scene.Cameras>=0x10000&&scene.CameraCount>0&&scene.CameraCount<=256&&scene.Model==model&&index<scene.CameraCount&&entry==scene.Cameras+index*0x78;}
 public bool Tick(SC2HubGate.Scene scene,string mode,double aspect){
  int selected=mode=="Fit"?1:mode=="Expanded"?2:0;
  if(selected==0)return Stop();
  if(Double.IsNaN(aspect)||Double.IsInfinity(aspect)||aspect<1||aspect>4)throw new ArgumentException("Invalid hub aspect ratio.");
  bool changed=false;
  for(int i=hooks.Count-1;i>=0;i--){var h=hooks[i];if(!ContainsCamera(scene,h.Model,h.Entry,h.Index)||!Owned(h)){if(Restore(h)){hooks.RemoveAt(i);changed=true;}}}
  if(changed)Save();if(!scene.Allowed)return false;
  bool ready=false;
  // Prepare every camera in this scene before a submenu can select it. Each
  // native callback still requires its camera to be active in an offline hub.
  for(uint index=0;index<scene.CameraCount;index++){
   ulong entry=scene.Cameras+index*0x78,definition=Ptr(entry+0x50);
   bool prepared=PrepareCamera(scene,index,entry,definition,selected,aspect);
   if(index==scene.Index)ready=prepared;
  }
  return ready;
 }
 bool PrepareCamera(SC2HubGate.Scene scene,uint index,ulong entry,ulong definition,int selected,double aspect){
  foreach(var h in hooks)if(h.Entry==entry&&Owned(h)){if(U32(h.State)!=(uint)selected)Write(h.State,BitConverter.GetBytes(selected));return U32(h.State+8)>0;}
  if(definition<0x10000||U32(definition+0x24)>1||Ptr(entry)!=module+SC2Addresses.Rva(0x2e94880)||Ptr(module+SC2Addresses.Rva(0x2e94888))!=module+SC2Addresses.Rva(0x14b4090)){
   if(index==scene.Index)throw new InvalidOperationException("This hub camera is not supported yet.");return false;
  }
  var latest=SC2HubGate.Check(pid,module);if(!latest.Allowed||!SameCameraSet(scene,latest))return false;
  var process=OpenProcess(0x438,false,pid);if(process==IntPtr.Zero)throw new InvalidOperationException("Cannot open the hub camera.");
  try{
   var h=new Hook{Entry=entry,Definition=definition,Model=scene.Model,Index=index,Original=module+SC2Addresses.Rva(0x2e94880),OriginalVertical=U32(definition+0x24)};
   h.Table=(ulong)VirtualAllocEx(process,IntPtr.Zero,(UIntPtr)4096,0x3000,4).ToInt64();h.Code=(ulong)VirtualAllocEx(process,IntPtr.Zero,(UIntPtr)4096,0x3000,4).ToInt64();if(h.Table==0||h.Code==0)throw new InvalidOperationException("Cannot allocate hub camera setup.");h.State=h.Table+0x200;
   var data=SC2HubGate.Read(pid,h.Original,0x100);Array.Copy(BitConverter.GetBytes(h.Code),0,data,8,8);Write(process,h.Table,data);Write(process,h.Table+0x180,BitConverter.GetBytes(Magic));
   Write(process,h.State,BitConverter.GetBytes(selected));Write(process,h.State+0x10,BitConverter.GetBytes(.5f));Write(process,h.State+0x14,BitConverter.GetBytes(2f));Write(process,h.State+0x18,BitConverter.GetBytes(16f/9f));
   Write(process,h.State+0x1c,BitConverter.GetBytes((float)aspect));Write(process,h.State+0x20,BitConverter.GetBytes((float)(10*Math.PI/180)));Write(process,h.State+0x24,BitConverter.GetBytes((float)(89*Math.PI/180)));Write(process,h.State+0x28,BitConverter.GetBytes(h.OriginalVertical));
   var code=Build(h.State,module+SC2Addresses.Rva(0x14b4090),h.Entry,h.Definition,h.Model,h.Index,module+SC2CampaignGate.SessionRva,module+SC2CampaignGate.MapRva,module+SC2HubGate.SceneRva);if(code.Length>4096)throw new InvalidOperationException("Camera callback is too large.");Write(process,h.Code,code);uint old;if(!VirtualProtectEx(process,(IntPtr)(long)h.Code,(UIntPtr)4096,0x20,out old)||!FlushInstructionCache(process,(IntPtr)(long)h.Code,(UIntPtr)code.Length))throw new InvalidOperationException("Cannot prepare hub camera callback.");
   latest=SC2HubGate.Check(pid,module);if(!latest.Allowed||!SameCameraSet(scene,latest)||Ptr(h.Entry)!=h.Original||Ptr(h.Entry+0x50)!=h.Definition)return false;
   hooks.Add(h);Save();Write(process,h.Entry,BitConverter.GetBytes(h.Table));return false;
  }finally{CloseHandle(process);}
 }
 public static byte[] Build(ulong state,ulong original,ulong entry,ulong definition,ulong model,uint index,ulong session,ulong map,ulong scene){
  var c=new List<byte>();var skip=new List<int>();var deny=new List<int>();
  SC2HubGate.B(c,"53 48 83 EC 30 48 89 CB 48 B8");c.AddRange(BitConverter.GetBytes(state));SC2HubGate.B(c,"F0 FF 40 04 48 B8");c.AddRange(BitConverter.GetBytes(original));SC2HubGate.B(c,"FF D0 48 89 44 24 20 48 B8");c.AddRange(BitConverter.GetBytes(entry));SC2HubGate.B(c,"48 39 C3");skip.Add(SC2HubGate.J(c,"0F 85"));
  SC2HubGate.B(c,"48 B8");c.AddRange(BitConverter.GetBytes(definition));SC2HubGate.B(c,"48 39 43 50");skip.Add(SC2HubGate.J(c,"0F 85"));
  SC2HubGate.B(c,"48 B8");c.AddRange(BitConverter.GetBytes(state));SC2HubGate.B(c,"83 38 01");deny.Add(SC2HubGate.J(c,"0F 82"));SC2HubGate.B(c,"83 38 02");deny.Add(SC2HubGate.J(c,"0F 87"));
  SC2HubGate.Emit(c,deny,session,map,scene,model,index);
  SC2HubGate.B(c,"8B 43 38 3D");c.AddRange(BitConverter.GetBytes(.05f));deny.Add(SC2HubGate.J(c,"0F 82"));SC2HubGate.B(c,"3D");c.AddRange(BitConverter.GetBytes(3.1f));deny.Add(SC2HubGate.J(c,"0F 87"));
  SC2HubGate.B(c,"48 B8");c.AddRange(BitConverter.GetBytes(definition));SC2HubGate.B(c,"C7 40 24 01 00 00 00 48 B8");c.AddRange(BitConverter.GetBytes(state+0x10));
  // Convert animated horizontal FOV to the original 16:9 vertical framing.
  SC2HubGate.B(c,"D9 43 38 83 78 18 01");int vertical=SC2HubGate.J(c,"0F 84");
  SC2HubGate.B(c,"D8 08 D9 F2 DD D8 D8 70 08 D9 E8 D9 F3 D8 48 04");SC2HubGate.Fix(c,vertical,c.Count);
  SC2HubGate.B(c,"83 78 F0 02");int fit=SC2HubGate.J(c,"0F 85");
  // Expanded: vertical -> actual horizontal, add 20 degrees, then back to vertical.
  // Cap horizontal FOV at 178 degrees to stay below the projection singularity.
  SC2HubGate.B(c,"D8 08 D9 F2 DD D8 D8 48 0C D9 E8 D9 F3 D8 40 10 D9 5C 24 28 8B 4C 24 28 3B 48 14");
  int bounded=SC2HubGate.J(c,"0F 86");SC2HubGate.B(c,"8B 48 14 89 4C 24 28");SC2HubGate.Fix(c,bounded,c.Count);
  SC2HubGate.B(c,"D9 44 24 28 D9 F2 DD D8 D8 70 0C D9 E8 D9 F3 D8 48 04");SC2HubGate.Fix(c,fit,c.Count);
  SC2HubGate.B(c,"D9 5B 38 48 B8");c.AddRange(BitConverter.GetBytes(state));SC2HubGate.B(c,"FF 40 08");skip.Add(SC2HubGate.J(c,"E9"));
  SC2CampaignGate.Resolve(c,deny,c.Count);SC2HubGate.B(c,"48 B8");c.AddRange(BitConverter.GetBytes(state));SC2HubGate.B(c,"8B 48 28 48 B8");c.AddRange(BitConverter.GetBytes(definition));SC2HubGate.B(c,"89 48 24");SC2CampaignGate.Resolve(c,skip,c.Count);
  SC2HubGate.B(c,"48 B8");c.AddRange(BitConverter.GetBytes(state));SC2HubGate.B(c,"F0 FF 48 04 48 8B 44 24 20 48 83 C4 30 5B C3");return c.ToArray();
 }
}
