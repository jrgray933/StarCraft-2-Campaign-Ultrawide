using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
public static class SC2CampaignGate {
 public const ulong SessionRva=0x5a94ed8, MapRva=0x5704068;
 public const int Revision=2;
 [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr OpenProcess(uint a,bool inherit,int pid);
 [DllImport("kernel32.dll")] static extern bool ReadProcessMemory(IntPtr h,IntPtr a,byte[] b,UIntPtr n,out UIntPtr got);
 [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
 public sealed class Status {public bool Allowed;public string MapPath="",Reason="Campaign state is unavailable.";}
 static byte[] Read(IntPtr h,ulong a,int n){byte[] b=new byte[n];UIntPtr got;if(a==0||!ReadProcessMemory(h,(IntPtr)(long)a,b,(UIntPtr)n,out got)||got.ToUInt64()!=(ulong)n)throw new InvalidOperationException("Campaign state is unavailable.");return b;}
 static ulong Ptr(IntPtr h,ulong a){return BitConverter.ToUInt64(Read(h,a,8),0);}
 public static string CanonicalPath(string path){if(path==null)return null;path=path.Replace('\\','/');return path.StartsWith("campaign/",StringComparison.OrdinalIgnoreCase)?"Maps/"+path:path;}
 public static bool Matches(bool online,string path){path=CanonicalPath(path);return !online&&path!=null&&path.Replace('\\','/').IndexOf("maps/campaign/",StringComparison.OrdinalIgnoreCase)>=0;}
 public static Status Check(int pid,ulong module){
  IntPtr h=IntPtr.Zero;var result=new Status();
  try{
   using(var p=Process.GetProcessById(pid)){if(!String.Equals(System.IO.Path.GetFileName(p.MainModule.FileName),"SC2_x64.exe",StringComparison.OrdinalIgnoreCase)||(ulong)p.MainModule.BaseAddress.ToInt64()!=module){result.Reason="Game executable identity changed.";return result;}}
   h=OpenProcess(0x410,false,pid);if(h==IntPtr.Zero)return result;
   // Repeat the complete observation to reject a changing mission/session.
   string last=null;ulong previousManager=0,previousInfo=0;
   for(int i=0;i<2;i++){
    ulong manager=Ptr(h,module+SessionRva),info=Ptr(h,manager+0x60);
    if(manager==0||info==0)return result;
    uint flags=BitConverter.ToUInt32(Read(h,info+0x1ec8,4),0);
    byte[] bytes=Read(h,module+MapRva,260);int end=Array.IndexOf(bytes,(byte)0);
    if(end<0)return result;string path=Encoding.ASCII.GetString(bytes,0,end);result.MapPath=CanonicalPath(path);
    if(!Matches((flags&2)!=0,path)){result.Reason=(flags&2)!=0?"Online game session: fix disabled.":"Waiting for an offline campaign mission.";return result;}
    if(i!=0&&(path!=last||manager!=previousManager||info!=previousInfo))return result;
    previousManager=manager;previousInfo=info;last=path;
   }
   result.Allowed=true;result.Reason="Offline campaign confirmed.";return result;
  }catch{ return result; }finally{if(h!=IntPtr.Zero)CloseHandle(h);}
 }
 public static void Require(int pid,ulong module){var s=Check(pid,module);if(!s.Allowed)throw new InvalidOperationException(s.Reason);}
 static void B(List<byte> c,string hex){foreach(string x in hex.Split(' '))if(x.Length!=0)c.Add(Convert.ToByte(x,16));}
 static int J(List<byte> c,string op){B(c,op);int p=c.Count;c.AddRange(new byte[4]);return p;}
 static void F(List<byte> c,int p,int target){byte[] d=BitConverter.GetBytes(target-p-4);for(int i=0;i<4;i++)c[p+i]=d[i];}
 // Inline callback guard. Uses only volatile RAX/R11; preserves all arguments,
 // R10 (wrapper state), stack, and nonvolatile registers. No game function calls.
 public static void Emit(List<byte> c,List<int> denied,ulong sessionGlobal,ulong map){
  B(c,"48 B8");c.AddRange(BitConverter.GetBytes(sessionGlobal));B(c,"48 8B 00 48 85 C0");denied.Add(J(c,"0F 84"));
  B(c,"48 8B 40 60 48 85 C0");denied.Add(J(c,"0F 84"));
  B(c,"F6 80 C8 1E 00 00 02");denied.Add(J(c,"0F 85"));
  // Require a terminator within the same 260-byte field used by Check().
  B(c,"49 BB");c.AddRange(BitConverter.GetBytes(map));int scan=c.Count;
  B(c,"41 80 3B 00");int terminated=J(c,"0F 84");B(c,"49 FF C3 48 B8");c.AddRange(BitConverter.GetBytes(map+260));B(c,"49 39 C3");int scanning=J(c,"0F 82");denied.Add(J(c,"E9"));F(c,scanning,scan);F(c,terminated,c.Count);
  // GameMapPath is relative to Maps/. Accept only its leading Campaign/
  // directory as that relative form, not arbitrary occurrences of Campaign/.
  B(c,"49 BB");c.AddRange(BitConverter.GetBytes(map));var absolute=new List<int>();
  const string relative="campaign/";
  for(int i=0;i<relative.Length;i++){
   B(c,"41 0F B6 43");c.Add((byte)i);
   if(relative[i]=='/'){B(c,"3C 5C");int slash=J(c,"0F 84");B(c,"3C 2F");absolute.Add(J(c,"0F 85"));F(c,slash,c.Count);}
   else{B(c,"0C 20 3C");c.Add((byte)relative[i]);absolute.Add(J(c,"0F 85"));}
  }
  int relativeMatched=J(c,"E9");foreach(int p in absolute)F(c,p,c.Count);
  int loop=c.Count;var mismatch=new List<int>();
  const string text="maps/campaign/";
  for(int i=0;i<text.Length;i++){
   B(c,"41 0F B6 43");c.Add((byte)i);
   if(text[i]=='/'){B(c,"3C 5C");int slash=J(c,"0F 84");B(c,"3C 2F");mismatch.Add(J(c,"0F 85"));F(c,slash,c.Count);}
   else{B(c,"0C 20 3C");c.Add((byte)text[i]);mismatch.Add(J(c,"0F 85"));}
  }
  int matched=J(c,"E9"),next=c.Count;B(c,"41 80 3B 00");denied.Add(J(c,"0F 84"));
  B(c,"49 FF C3 48 B8");c.AddRange(BitConverter.GetBytes(map+260-(ulong)text.Length));B(c,"49 39 C3");int again=J(c,"0F 86");denied.Add(J(c,"E9"));F(c,again,loop);F(c,matched,c.Count);F(c,relativeMatched,c.Count);foreach(int p in mismatch)F(c,p,next);
 }
 public static void Resolve(List<byte> c,IEnumerable<int> jumps,int target){foreach(int p in jumps)F(c,p,target);}
}
