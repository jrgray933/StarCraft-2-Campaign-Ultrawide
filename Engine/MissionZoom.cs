using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

public sealed class SC2MissionZoom {
 const ulong Magic=0x3153504554533253;
 readonly Func<ulong,int,byte[]> read;readonly Action<ulong,byte[],byte[]> write;
 readonly Func<int,ulong> allocate;readonly Action<ulong,int> executable;readonly Func<bool> allowed;
 readonly ulong module;readonly string record,identity;
 sealed class Hook {public ulong Camera,Data,Array,Table,State,Code;public int Count;public byte[] Original,ShadowOriginal,ShadowApplied;public int Percent=-1,Steps=-1;}
 Hook hook;
 [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr OpenProcess(uint a,bool inherit,int pid);
 [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr h);
 [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr VirtualAllocEx(IntPtr h,IntPtr a,UIntPtr n,uint flags,uint protection);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool VirtualProtectEx(IntPtr h,IntPtr a,UIntPtr n,uint protection,out uint old);
 [DllImport("kernel32.dll")]static extern bool FlushInstructionCache(IntPtr h,IntPtr a,UIntPtr n);
 static ulong Allocate(int pid,int n){var h=OpenProcess(0x438,false,pid);try{var a=VirtualAllocEx(h,IntPtr.Zero,(UIntPtr)n,0x3000,4);if(a==IntPtr.Zero)throw new InvalidOperationException("Cannot prepare zoom settings.");return (ulong)a.ToInt64();}finally{CloseHandle(h);}}
 static void Executable(int pid,ulong a,int n){var h=OpenProcess(0x438,false,pid);try{uint old;if(!VirtualProtectEx(h,(IntPtr)(long)a,(UIntPtr)n,0x20,out old)||!FlushInstructionCache(h,(IntPtr)(long)a,(UIntPtr)n))throw new InvalidOperationException("Cannot prepare zoom controls.");}finally{CloseHandle(h);}}
 public SC2MissionZoom(int pid,ulong module,string record):this((a,n)=>SC2HubGate.Read(pid,a,n),(a,b,c)=>SC2HudScale.CheckedWrite(pid,a,b,c),()=>{var g=SC2CampaignGate.Check(pid,module);return g.Allowed&&!SC2HubGate.HubMap(g.MapPath);},module,record,Identity(pid,module),n=>Allocate(pid,n),(a,n)=>Executable(pid,a,n)){}
 static string Identity(int pid,ulong module){using(var p=Process.GetProcessById(pid))return pid+":"+p.StartTime.ToUniversalTime().Ticks+":"+module.ToString("X");}
 public SC2MissionZoom(Func<ulong,int,byte[]> read,Action<ulong,byte[],byte[]> write,Func<bool> allowed,ulong module,string record,string identity,Func<int,ulong> allocate,Action<ulong,int> executable){this.read=read;this.write=write;this.allowed=allowed;this.module=module;this.record=record;this.identity=identity;this.allocate=allocate;this.executable=executable;Load();}
 ulong Ptr(ulong a){return BitConverter.ToUInt64(read(a,8),0);}uint U32(ulong a){return BitConverter.ToUInt32(read(a,4),0);}void Put(ulong a,byte[] b){write(a,read(a,b.Length),b);}
 static bool Equal(byte[] a,byte[] b){if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
 static float F(byte[] b,int i){return BitConverter.ToSingle(b,i);}static void Set(byte[] b,int i,float f){Array.Copy(BitConverter.GetBytes(f),0,b,i,4);}
 static bool Finite(float n){return !Single.IsNaN(n)&&!Single.IsInfinity(n);}
 public static void Validate(int percent){if(percent<0||percent>100||percent%10!=0)throw new ArgumentException("Choose Default or +10% to +100%, in steps of 10%.");}
 public static void ValidateSteps(int steps){if(steps<5||steps>20)throw new ArgumentException("Choose 5 to 20 zoom steps.");}
 public static float Distance(float baseline,int percent){Validate(percent);if(!Finite(baseline)||baseline<=0||baseline>1000)throw new ArgumentException("Unexpected camera distance.");return baseline*(1+percent/100f);}
 public static float ShadowClip(float baseline,int percent){Validate(percent);if(!Finite(baseline)||baseline<=0||baseline>100000)throw new ArgumentException("Unexpected shadow distance.");return baseline*(1+percent/100f);}
 public static byte[] Presets(byte[] original,int percent,int steps){
  Validate(percent);ValidateSteps(steps);int count=original.Length/0x90;if(count<2||count>64||original.Length!=count*0x90)throw new ArgumentException("Unexpected zoom presets.");
  for(int i=0;i<count;i++){
   float d=F(original,i*0x90+0x24);if(BitConverter.ToUInt32(original,i*0x90+0x20)!=1||!Finite(d)||d<=0||d>1000||(i>0&&d>=F(original,(i-1)*0x90+0x24)))throw new ArgumentException("Unexpected zoom distances.");
   for(int j=0;j<18;j++){uint flag=BitConverter.ToUInt32(original,i*0x90+j*8);if(flag>1||flag!=BitConverter.ToUInt32(original,j*8)||(flag==1&&!Finite(F(original,i*0x90+j*8+4))))throw new ArgumentException("Unexpected zoom preset fields.");}
  }
  var result=new byte[steps*0x90];float maximum=Distance(F(original,0x24),percent),minimum=F(original,(count-1)*0x90+0x24);
  for(int i=0;i<steps;i++){
   float distance=maximum-(maximum-minimum)*i/(steps-1);int a=0;
   while(a<count-2&&distance<F(original,(a+1)*0x90+0x24))a++;
   float high=F(original,a*0x90+0x24),low=F(original,(a+1)*0x90+0x24),t=Math.Max(0,Math.Min(1,(high-distance)/(high-low)));
   Array.Copy(original,a*0x90,result,i*0x90,0x90);
   for(int j=0;j<18;j++)if(BitConverter.ToUInt32(original,a*0x90+j*8)==1)Set(result,i*0x90+j*8+4,F(original,a*0x90+j*8+4)*(1-t)+F(original,(a+1)*0x90+j*8+4)*t);
   Set(result,i*0x90+0x24,distance);
  }
  return result;
 }
 static int Nearest(byte[] table,float distance){int best=0;float error=Single.MaxValue;for(int i=0;i<table.Length/0x90;i++){float e=Math.Abs(F(table,i*0x90+0x24)-distance);if(e<error){best=i;error=e;}}return best;}
 ulong Observe(){try{if(!allowed())return 0;ulong c=Ptr(Ptr(Ptr(Ptr(module+0x4046470)+0x1b28)+0x550)+0x10),v=Ptr(c);if(v!=module+0x2d609e8&&(hook==null||c!=hook.Camera||v!=hook.Table))return 0;if(Ptr(Ptr(c+8))!=0||U32(c+0xa8)!=0)return 0;return c;}catch{return 0;}}
 bool Owned(){try{return hook!=null&&Ptr(hook.Camera)==hook.Table&&Ptr(hook.State+0x80)==Magic;}catch{return false;}}
 bool Quiet(){Put(hook.State,BitConverter.GetBytes(0));for(int i=0;i<20;i++){if(U32(hook.State+4)==0)return true;Thread.Sleep(1);}return false;}
 public bool Tick(int percent,int steps){
  Validate(percent);ValidateSteps(steps);ulong camera=Observe();
  if(hook!=null&&(camera!=hook.Camera||unchecked(Ptr(camera+0x18)<<5)!=hook.Data||percent==0&&steps==hook.Count)){if(!Stop())return false;camera=Observe();}
  if(camera==0)return false;
  if(hook==null){
   ulong data=unchecked(Ptr(camera+0x18)<<5);int count=(int)U32(data+0xd8);ulong array=Ptr(data+0xe8);if(count<2||count>64||array<0x10000)return false;
   if(percent==0&&steps==count)return true;
   byte[] baseline=read(array,count*0x90);Presets(baseline,percent,steps);
   if(Ptr(module+0x2d609e8+0x70)!=module+0xbf7840)throw new InvalidOperationException("This camera's zoom controls are unavailable.");
   var h=new Hook{Camera=camera,Data=data,Array=array,Count=count,Original=baseline};h.State=allocate(8192);h.Table=h.State+0x1010;h.Code=allocate(4096);
   var vt=read(module+0x2d609e8-16,0x88);Array.Copy(BitConverter.GetBytes(h.Code),0,vt,0x80,8);Put(h.Table-16,vt);
   Put(h.State+0x10,BitConverter.GetBytes(camera));Put(h.State+0x18,BitConverter.GetBytes(data));Put(h.State+0x20,BitConverter.GetBytes((ulong)count));Put(h.State+0x28,BitConverter.GetBytes(array));Put(h.State+0x38,BitConverter.GetBytes(h.State+0x100));Put(h.State+0x80,BitConverter.GetBytes(Magic));
   var code=Build(h.State,module+0xbf7840,module+SC2CampaignGate.SessionRva,module+SC2CampaignGate.MapRva,module+0x4046470);if(code.Length>4096)throw new InvalidOperationException("Zoom callback is too large.");Put(h.Code,code);executable(h.Code,4096);
   if(Observe()!=camera||Ptr(data+0xe8)!=array||U32(data+0xd8)!=count)return false;
   hook=h;CaptureShadow();Save();write(camera,BitConverter.GetBytes(module+0x2d609e8),BitConverter.GetBytes(h.Table));
  }
  if(hook.Percent==percent&&hook.Steps==steps)return true;
  if(!Owned()||!Quiet())return false;
  if(Observe()!=hook.Camera||Ptr(hook.Data+0xe8)!=hook.Array||U32(hook.Data+0xd8)!=hook.Count||!Equal(read(hook.Array,hook.Original.Length),hook.Original))throw new InvalidOperationException("The mission camera changed. Waiting for its zoom controls.");
  var table=Presets(hook.Original,percent,steps);int oldCount=hook.Steps>0?hook.Steps:hook.Count;
  int oldIndex=hook.Steps>0&&(U32(hook.Camera+0x13c)==U32(hook.State+0x48))?(int)U32(hook.State+0xc):(int)U32(hook.Camera+0xa4);
  if(oldIndex<0||oldIndex>=oldCount)oldIndex=0;int index=(int)Math.Round((double)oldIndex*(steps-1)/(oldCount-1));
  Put(hook.State+0x100,table);Put(hook.State+0x30,BitConverter.GetBytes((ulong)steps));Put(hook.State+0xc,BitConverter.GetBytes(index));
  var map=new byte[80];for(int i=0;i<steps;i++)Array.Copy(BitConverter.GetBytes(Nearest(hook.Original,F(table,i*0x90+0x24))),0,map,i*4,4);Put(hook.State+0xd00,map);
  map=new byte[256];for(int i=0;i<hook.Count;i++)Array.Copy(BitConverter.GetBytes(Nearest(table,F(hook.Original,i*0x90+0x24))),0,map,i*4,4);Put(hook.State+0xe00,map);
  ApplyShadow(percent);
  Adjust(F(table,index*0x90+0x24),F(table,index*0x90+0x2c));Put(hook.Camera+0xa4,BitConverter.GetBytes(Nearest(hook.Original,F(table,index*0x90+0x24))));Put(hook.State+0x48,read(hook.Camera+0x13c,4));
  hook.Percent=percent;hook.Steps=steps;Save();Put(hook.State,BitConverter.GetBytes(1));return true;
 }
 void CaptureShadow(){
  if(hook.ShadowOriginal!=null)return;
  var bytes=read(hook.Camera+0x118,12);var cipher=read(module+0x3a902e0,0x4000);
  for(int i=0;i<3;i++){float value=Decode(BitConverter.ToUInt32(bytes,i*4),cipher);if(!Finite(value)||value<=0||value>100000)return;}
  hook.ShadowOriginal=bytes;
 }
 void ApplyShadow(int percent){
  if(Observe()!=hook.Camera)return;CaptureShadow();if(hook.ShadowOriginal==null)return;
  var before=read(hook.Camera+0x118,12);
  if(!Equal(before,hook.ShadowApplied??hook.ShadowOriginal)&&!Equal(before,hook.ShadowOriginal))return;
  var cipher=read(module+0x3a902e0,0x4000);var next=new byte[12];
  // Use this mission's original clip, never an already expanded value.
  if(percent==0)Array.Copy(hook.ShadowOriginal,next,12);
  else{var value=Encode(ShadowClip(Decode(BitConverter.ToUInt32(hook.ShadowOriginal,0),cipher),percent),cipher);for(int i=0;i<3;i++)Array.Copy(value,0,next,i*4,4);}
  var previous=hook.ShadowApplied;hook.ShadowApplied=next;
  try{Save();if(!Equal(before,next))write(hook.Camera+0x118,before,next);}
  catch{if(Equal(read(hook.Camera+0x118,12),before)){hook.ShadowApplied=previous;Save();}throw;}
 }
 void RestoreShadow(){
  if(hook.ShadowOriginal==null||hook.ShadowApplied==null||unchecked(Ptr(hook.Camera+0x18)<<5)!=hook.Data)return;
  var current=read(hook.Camera+0x118,12);if(Equal(current,hook.ShadowApplied)&&!Equal(current,hook.ShadowOriginal))write(hook.Camera+0x118,current,hook.ShadowOriginal);
 }
 void Adjust(float distance,float pitch){
  if(Observe()!=hook.Camera)return;var table=read(module+0x3a902e0,0x4000);
  foreach(var field in new[]{new[]{0x13c,0x24},new[]{0x160,0x2c}}){var old=read(hook.Camera+(ulong)field[0],12);float value=field[0]==0x13c?distance:pitch;float target=Decode(BitConverter.ToUInt32(old,0),table);bool resting=Finite(target);for(int i=1;i<3;i++)resting&=Math.Abs(Decode(BitConverter.ToUInt32(old,i*4),table)-target)<.1f;if(!resting)continue;var next=new byte[12];for(int i=0;i<3;i++)Array.Copy(Encode(value,table),0,next,i*4,4);write(hook.Camera+(ulong)field[0],old,next);}
 }
 public bool Stop(){
  if(hook==null)return true;
  if(Owned()){
   if(!Quiet())return false;
   if(Observe()==hook.Camera){int index=(int)U32(hook.Camera+0xa4);if(index>=0&&index<hook.Count)Adjust(F(hook.Original,index*0x90+0x24),F(hook.Original,index*0x90+0x2c));}
   RestoreShadow();
   if(Owned())write(hook.Camera,BitConverter.GetBytes(hook.Table),BitConverter.GetBytes(module+0x2d609e8));
  }
  // Detached callbacks are retained until process exit in case a native caller
  // already fetched their address. Game-owned preset arrays are never replaced
  // outside the synchronous wheel callback and are never freed by this helper.
  hook=null;Save();return true;
 }
 void Save(){if(String.IsNullOrEmpty(record))return;if(hook==null){if(File.Exists(record))File.Delete(record);return;}var h=hook;var lines=new[]{"zoom-steps-2",identity,h.Camera.ToString("X"),h.Data.ToString("X"),h.Array.ToString("X"),h.Table.ToString("X"),h.State.ToString("X"),h.Code.ToString("X"),h.Count.ToString(),Convert.ToBase64String(h.Original),h.ShadowOriginal==null?"":Convert.ToBase64String(h.ShadowOriginal),h.ShadowApplied==null?"":Convert.ToBase64String(h.ShadowApplied)};File.WriteAllLines(record+".tmp",lines);if(File.Exists(record))File.Replace(record+".tmp",record,null);else File.Move(record+".tmp",record);}
 void Load(){if(String.IsNullOrEmpty(record)||!File.Exists(record))return;var v=File.ReadAllLines(record);if(!((v.Length==10&&v[0]=="zoom-steps-1")||(v.Length==12&&v[0]=="zoom-steps-2"))||v[1]!=identity)return;hook=new Hook{Camera=UInt64.Parse(v[2],NumberStyles.HexNumber),Data=UInt64.Parse(v[3],NumberStyles.HexNumber),Array=UInt64.Parse(v[4],NumberStyles.HexNumber),Table=UInt64.Parse(v[5],NumberStyles.HexNumber),State=UInt64.Parse(v[6],NumberStyles.HexNumber),Code=UInt64.Parse(v[7],NumberStyles.HexNumber),Count=Int32.Parse(v[8]),Original=Convert.FromBase64String(v[9])};if(v.Length==12){hook.ShadowOriginal=v[10].Length==0?null:Convert.FromBase64String(v[10]);hook.ShadowApplied=v[11].Length==0?null:Convert.FromBase64String(v[11]);if((hook.ShadowOriginal!=null&&hook.ShadowOriginal.Length!=12)||(hook.ShadowApplied!=null&&hook.ShadowApplied.Length!=12))throw new InvalidOperationException("The saved shadow setting is incomplete.");}if(!Owned()){hook=null;return;}if(!Quiet())throw new InvalidOperationException("Zoom controls are busy. Try again.");hook.Steps=(int)U32(hook.State+0x30);}
 static void B(List<byte> c,string s){SC2HubGate.B(c,s);}static int J(List<byte> c,string s){return SC2HubGate.J(c,s);}static void Fix(List<byte> c,int p,int n){SC2HubGate.Fix(c,p,n);}static void Q(List<byte> c,ulong v){c.AddRange(BitConverter.GetBytes(v));}
 public static byte[] Build(ulong state,ulong original,ulong session,ulong map,ulong scene){
  var c=new List<byte>();var denied=new List<int>();var release=new List<int>();
  B(c,"53 56 57 41 54 41 55 41 56 41 57 48 83 EC 40 48 89 CB 48 89 54 24 20 4C 89 44 24 28 4C 89 4C 24 30 48 BF");Q(c,state);
  B(c,"48 3B 5F 10");denied.Add(J(c,"0F 85"));B(c,"83 3F 01");denied.Add(J(c,"0F 85"));
  if(session!=0)SC2CampaignGate.Emit(c,denied,session,map);
  if(scene!=0){B(c,"48 B8");Q(c,scene);B(c,"48 8B 00 48 85 C0");denied.Add(J(c,"0F 84"));B(c,"48 8B 80 28 1B 00 00 48 85 C0");denied.Add(J(c,"0F 84"));B(c,"48 8B 80 50 05 00 00 48 85 C0");denied.Add(J(c,"0F 84"));B(c,"48 39 58 10");denied.Add(J(c,"0F 85"));}
  B(c,"83 BB A8 00 00 00 00");denied.Add(J(c,"0F 85"));B(c,"48 8B 43 08 48 85 C0");denied.Add(J(c,"0F 84"));B(c,"48 83 38 00");denied.Add(J(c,"0F 85"));
  B(c,"31 C0 BA 01 00 00 00 F0 0F B1 57 04 85 C0");denied.Add(J(c,"0F 85"));B(c,"83 3F 01");release.Add(J(c,"0F 85"));
  B(c,"48 8B 73 18 48 C1 E6 05 48 3B 77 18");release.Add(J(c,"0F 85"));B(c,"4C 8B 6F 20 4C 39 AE D8 00 00 00");release.Add(J(c,"0F 85"));B(c,"4C 8B 77 28 4C 39 B6 E8 00 00 00");release.Add(J(c,"0F 85"));
  B(c,"44 8B A3 A4 00 00 00 45 39 EC");release.Add(J(c,"0F 83"));
  B(c,"8B 83 3C 01 00 00 3B 47 48");int changed=J(c,"0F 85");B(c,"8B 47 0C");int chosen=J(c,"E9");Fix(c,changed,c.Count);B(c,"42 8B 84 A7 00 0E 00 00");Fix(c,chosen,c.Count);
  B(c,"3B 47 30");release.Add(J(c,"0F 83"));B(c,"89 83 A4 00 00 00 48 8B 47 38 48 89 86 E8 00 00 00 48 8B 47 30 48 89 86 D8 00 00 00");
  B(c,"48 89 D9 48 8B 54 24 20 4C 8B 44 24 28 4C 8B 4C 24 30 48 B8");Q(c,original);B(c,"FF D0 48 89 44 24 38");
  B(c,"8B 83 A4 00 00 00 3B 47 30");int bad=J(c,"0F 83");B(c,"89 47 0C 8B 8C 87 00 0D 00 00 89 8B A4 00 00 00");int mapped=J(c,"E9");Fix(c,bad,c.Count);B(c,"44 89 A3 A4 00 00 00");Fix(c,mapped,c.Count);
  B(c,"8B 83 3C 01 00 00 89 47 48 4C 89 AE D8 00 00 00 4C 89 B6 E8 00 00 00 FF 47 08 C7 47 04 00 00 00 00 48 8B 44 24 38");int done=J(c,"E9");
  foreach(int p in release)Fix(c,p,c.Count);B(c,"C7 47 04 00 00 00 00");foreach(int p in denied)Fix(c,p,c.Count);
  B(c,"48 89 D9 48 8B 54 24 20 4C 8B 44 24 28 4C 8B 4C 24 30 48 B8");Q(c,original);B(c,"FF D0");Fix(c,done,c.Count);
  B(c,"48 83 C4 40 41 5F 41 5E 41 5D 41 5C 5F 5E 5B C3");return c.ToArray();
 }
 public static float Decode(uint value,byte[] table){uint lo=value&65535,hi=value>>16;hi=(~(hi-BitConverter.ToUInt16(table,(int)(lo&4095)*4)))&65535;lo=(~(lo-BitConverter.ToUInt16(table,(int)(hi&4095)*4)))&65535;return BitConverter.ToSingle(BitConverter.GetBytes(lo|(hi<<16)),0);}
 public static byte[] Encode(float value,byte[] table){uint bits=BitConverter.ToUInt32(BitConverter.GetBytes(value),0),lo=bits&65535,hi=bits>>16;uint low=(~lo+BitConverter.ToUInt16(table,(int)(hi&4095)*4))&65535,high=(~hi+BitConverter.ToUInt16(table,(int)(low&4095)*4))&65535;return BitConverter.GetBytes(low|(high<<16));}
}
