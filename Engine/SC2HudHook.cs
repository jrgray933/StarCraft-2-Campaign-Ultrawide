using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public class SC2HudHook {
 [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr OpenProcess(uint a,bool b,int pid);
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool ReadProcessMemory(IntPtr h,IntPtr a,byte[] b,UIntPtr n,out UIntPtr read);
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool WriteProcessMemory(IntPtr h,IntPtr a,byte[] b,UIntPtr n,out UIntPtr written);
 [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr VirtualAllocEx(IntPtr h,IntPtr a,UIntPtr n,uint flags,uint protection);
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool VirtualProtectEx(IntPtr h,IntPtr a,UIntPtr n,uint protection,out uint old);
 [DllImport("kernel32.dll")] static extern bool VirtualFreeEx(IntPtr h,IntPtr a,UIntPtr n,uint kind);
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool FlushInstructionCache(IntPtr h,IntPtr a,UIntPtr n);
 [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
 [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
 [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate int Original(IntPtr owner,ulong a2,ulong a3,ulong a4,ulong a5);
 [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate byte Setter(IntPtr frame,int side,IntPtr parent,uint position,float offset,byte flag);
 public class Prepared {public ulong Code,State,VTable;public int Length;}
 public class Command {public ulong Frame,Parent;public int Side;public float Position,Offset,OldPosition,OldOffset;}
 static Exception Error(string s){return new Exception(s+"; Win32 error "+Marshal.GetLastWin32Error());}
 static void B(List<byte> c,string hex){foreach(string s in hex.Split(' '))if(s.Length>0)c.Add(Convert.ToByte(s,16));}
 static void I(List<byte> c,ulong n){c.AddRange(BitConverter.GetBytes(n));}
 static int J(List<byte> c,string op){B(c,op);int p=c.Count;c.AddRange(new byte[4]);return p;}
 static void F(List<byte> c,int p,int target){var d=BitConverter.GetBytes(target-p-4);for(int i=0;i<4;i++)c[p+i]=d[i];}
 public static byte[] Build(ulong state,ulong original){
  return Build(state,original,0);
 }
 public static byte[] Build(ulong state,ulong original,ulong module){
  var denied=new List<int>();var c=new List<byte>(); B(c,"49 BA");I(c,state);
  B(c,"F0 41 FF 42 08 49 3B 4A 18");int wrong=J(c,"0F 85");
  B(c,"31 C0 41 BB 01 00 00 00 F0 45 0F B1 5A 04 85 C0");int busy=J(c,"0F 85");
  B(c,"41 8B 02 85 C0");int idle=J(c,"0F 84");B(c,"83 F8 02");int invalid=J(c,"0F 87");
  if(module!=0){
   B(c,"83 F8 02");int restoring=J(c,"0F 84");
   SC2CampaignGate.Emit(c,denied,module+SC2CampaignGate.SessionRva,module+SC2CampaignGate.MapRva);
   B(c,"B8 01 00 00 00");F(c,restoring,c.Count);
  }
  B(c,"41 C7 02 00 00 00 00 53 56 57 55 48 81 EC D8 00 00 00 4C 89 D7 89 C3");
  B(c,"48 89 4C 24 30 48 89 54 24 38 4C 89 44 24 40 4C 89 4C 24 48");
  B(c,"0F 11 44 24 50 0F 11 4C 24 60 0F 11 54 24 70 0F 11 9C 24 80 00 00 00 0F 11 A4 24 90 00 00 00 0F 11 AC 24 A0 00 00 00");
  B(c,"48 8D 77 40 8B 6F 30 85 ED");int empty=J(c,"0F 84");B(c,"83 FD 10");int excessive=J(c,"0F 87");
  int loop=c.Count;B(c,"48 8B 0E 48 85 C9");int nullFrame=J(c,"0F 84");B(c,"4C 8B 46 08 4D 85 C0");int nullParent=J(c,"0F 84");
  B(c,"8B 56 10 83 FB 01");int restore=J(c,"0F 85");B(c,"44 8B 4E 14 8B 46 18");int callApply=J(c,"E9");
  int restoreLabel=c.Count;B(c,"44 8B 4E 1C 8B 46 20");
  int invoke=c.Count;B(c,"89 44 24 20 48 C7 44 24 28 00 00 00 00 48 8B 47 28 FF D0 0F B6 C0 01 47 10");
  int next=c.Count;B(c,"48 83 C6 28 FF CD");int again=J(c,"0F 85");
  int done=c.Count;B(c,"FF 47 0C C7 47 04 00 00 00 00");
  B(c,"0F 10 44 24 50 0F 10 4C 24 60 0F 10 54 24 70 0F 10 9C 24 80 00 00 00 0F 10 A4 24 90 00 00 00 0F 10 AC 24 A0 00 00 00");
  B(c,"48 8B 4C 24 30 48 8B 54 24 38 4C 8B 44 24 40 4C 8B 4C 24 48 48 81 C4 D8 00 00 00 5D 5F 5E 5B");int completed=J(c,"E9");
  int rejected=c.Count;B(c,"41 C7 02 00 00 00 00");SC2CampaignGate.Resolve(c,denied,rejected);
  int release=c.Count;B(c,"41 C7 42 04 00 00 00 00");
  int tail=c.Count;B(c,"48 B8");I(c,original);B(c,"FF E0");
  F(c,wrong,tail);F(c,busy,tail);F(c,idle,release);F(c,invalid,release);F(c,empty,done);F(c,excessive,done);F(c,nullFrame,next);F(c,nullParent,next);F(c,restore,restoreLabel);F(c,callApply,invoke);F(c,again,loop);F(c,completed,tail);return c.ToArray();
 }
 static byte[] Read(IntPtr h,ulong a,int size){var b=new byte[size];UIntPtr n;if(!ReadProcessMemory(h,(IntPtr)(long)a,b,(UIntPtr)size,out n)||n.ToUInt64()!=(ulong)size)throw Error("Read failed");return b;}
 static void Write(IntPtr h,ulong a,byte[] b){UIntPtr n;if(!WriteProcessMemory(h,(IntPtr)(long)a,b,(UIntPtr)b.Length,out n)||n.ToUInt64()!=(ulong)b.Length)throw Error("Write failed");}
 public static void CheckedWrite(int pid,ulong address,byte[] expected,byte[] replacement){
  if(expected.Length!=replacement.Length)throw new Exception("Write length mismatch");var h=OpenProcess(0x438,false,pid);if(h==IntPtr.Zero)throw Error("OpenProcess");try{var old=Read(h,address,expected.Length);for(int i=0;i<old.Length;i++)if(old[i]!=expected[i])throw new Exception("Game data changed; no write");Write(h,address,replacement);}finally{CloseHandle(h);}
 }
 public static Prepared Prepare(int pid,ulong owner,ulong table,ulong setter,Command[] commands){
  ulong module=table-0x2D51C58;SC2CampaignGate.Require(pid,module);
  if(commands.Length<1||commands.Length>16)throw new Exception("Invalid command count");
  var h=OpenProcess(0x438,false,pid);if(h==IntPtr.Zero)throw Error("OpenProcess");IntPtr data=IntPtr.Zero,code=IntPtr.Zero;bool ready=false;
  try{
   if(BitConverter.ToUInt64(Read(h,owner,8),0)!=table)throw new Exception("Unexpected frame vtable");
   var vt=Read(h,table-16,0x278);ulong original=BitConverter.ToUInt64(vt,0x158);
   data=VirtualAllocEx(h,IntPtr.Zero,(UIntPtr)4096,0x3000,4);code=VirtualAllocEx(h,IntPtr.Zero,(UIntPtr)4096,0x3000,4);if(data==IntPtr.Zero||code==IntPtr.Zero)throw Error("Allocation");
   ulong state=(ulong)data.ToInt64(),entry=(ulong)code.ToInt64();var bytes=Build(state,original,module);var buffer=new byte[4096];
   Array.Copy(BitConverter.GetBytes(owner),0,buffer,24,8);Array.Copy(BitConverter.GetBytes(original),0,buffer,32,8);Array.Copy(BitConverter.GetBytes(setter),0,buffer,40,8);Array.Copy(BitConverter.GetBytes(commands.Length),0,buffer,48,4);
   for(int i=0;i<commands.Length;i++){var x=commands[i];int p=64+i*40;Array.Copy(BitConverter.GetBytes(x.Frame),0,buffer,p,8);Array.Copy(BitConverter.GetBytes(x.Parent),0,buffer,p+8,8);Array.Copy(BitConverter.GetBytes(x.Side),0,buffer,p+16,4);Array.Copy(BitConverter.GetBytes(x.Position),0,buffer,p+20,4);Array.Copy(BitConverter.GetBytes(x.Offset),0,buffer,p+24,4);Array.Copy(BitConverter.GetBytes(x.OldPosition),0,buffer,p+28,4);Array.Copy(BitConverter.GetBytes(x.OldOffset),0,buffer,p+32,4);}
   Array.Copy(BitConverter.GetBytes(entry),0,vt,0x158,8);Array.Copy(vt,0,buffer,0x400,vt.Length);Write(h,state,buffer);Write(h,entry,bytes);uint old;
   if(!VirtualProtectEx(h,code,(UIntPtr)4096,0x20,out old)||!FlushInstructionCache(h,code,(UIntPtr)bytes.Length))throw Error("Protect code");
   var check=Read(h,entry,bytes.Length);for(int i=0;i<bytes.Length;i++)if(check[i]!=bytes[i])throw new Exception("Code readback failed");
   ready=true;return new Prepared{Code=entry,State=state,VTable=state+0x410,Length=bytes.Length};
  }finally{if(!ready){if(code!=IntPtr.Zero)VirtualFreeEx(h,code,UIntPtr.Zero,0x8000);if(data!=IntPtr.Zero)VirtualFreeEx(h,data,UIntPtr.Zero,0x8000);}CloseHandle(h);}
 }
 public static string SelfTest(){
  var data=Marshal.AllocHGlobal(1024);IntPtr code=IntPtr.Zero;var h=GetCurrentProcess();int setters=0,originals=0;int request=1;
  Original original=delegate(IntPtr owner,ulong a2,ulong a3,ulong a4,ulong a5){if(a2!=22||a3!=33||a4!=44||a5!=55)throw new Exception("Forwarding failed");originals++;return 12345;};
  Setter setter=delegate(IntPtr frame,int side,IntPtr parent,uint pos,float offset,byte flag){int index=setters%4;if(frame.ToInt64()!=100+index||parent.ToInt64()!=200+index||side!=index||flag!=0)throw new Exception("Setter args failed");float expected=request==1?366.6667f:-5f;if(Math.Abs(offset-expected)>0.001f||pos!=BitConverter.ToUInt32(BitConverter.GetBytes(request==1?0.5f:1f),0))throw new Exception("Setter floats failed");setters++;return 1;};
  try{
   Marshal.Copy(new byte[1024],0,data,1024);Marshal.WriteInt64(data,24,123);Marshal.WriteInt64(data,40,Marshal.GetFunctionPointerForDelegate(setter).ToInt64());Marshal.WriteInt32(data,48,4);
   for(int i=0;i<4;i++){int p=64+i*40;Marshal.WriteInt64(data,p,100+i);Marshal.WriteInt64(data,p+8,200+i);Marshal.WriteInt32(data,p+16,i);Marshal.WriteInt32(data,p+20,0x3f000000);Marshal.WriteInt32(data,p+24,BitConverter.ToInt32(BitConverter.GetBytes(366.6667f),0));Marshal.WriteInt32(data,p+28,0x3f800000);Marshal.WriteInt32(data,p+32,BitConverter.ToInt32(BitConverter.GetBytes(-5f),0));}
   var bytes=Build((ulong)data.ToInt64(),(ulong)Marshal.GetFunctionPointerForDelegate(original).ToInt64());code=VirtualAllocEx(h,IntPtr.Zero,(UIntPtr)4096,0x3000,4);if(code==IntPtr.Zero)throw Error("Local allocation");Marshal.Copy(bytes,0,code,bytes.Length);uint old;if(!VirtualProtectEx(h,code,(UIntPtr)4096,0x20,out old))throw Error("Local protect");FlushInstructionCache(h,code,(UIntPtr)bytes.Length);var wrapper=(Original)Marshal.GetDelegateForFunctionPointer(code,typeof(Original));
   Marshal.WriteInt32(data,0,1);if(wrapper((IntPtr)123,22,33,44,55)!=12345||setters!=4||Marshal.ReadInt32(data,0)!=0||Marshal.ReadInt32(data,4)!=0)throw new Exception("Apply failed");
   wrapper((IntPtr)123,22,33,44,55);if(setters!=4)throw new Exception("Repeated apply");
   request=2;Marshal.WriteInt32(data,0,2);wrapper((IntPtr)123,22,33,44,55);if(setters!=8)throw new Exception("Restore failed");
   Marshal.WriteInt32(data,0,1);wrapper((IntPtr)124,22,33,44,55);if(setters!=8||Marshal.ReadInt32(data,0)!=1)throw new Exception("Owner guard failed");
   Marshal.WriteInt32(data,4,1);wrapper((IntPtr)123,22,33,44,55);if(setters!=8||Marshal.ReadInt32(data,0)!=1)throw new Exception("Reentry guard failed");Marshal.WriteInt32(data,4,0);
   Marshal.WriteInt32(data,0,3);wrapper((IntPtr)123,22,33,44,55);if(setters!=8||Marshal.ReadInt32(data,4)!=0)throw new Exception("Request guard failed");
   if(originals!=6||Marshal.ReadInt32(data,8)!=6||Marshal.ReadInt32(data,12)!=2||Marshal.ReadInt32(data,16)!=8)throw new Exception("Counters failed");
   GC.KeepAlive(original);GC.KeepAlive(setter);GC.KeepAlive(wrapper);return "PASS: six native tests verified apply/restore, no repeat, owner/reentry/request guards, six setter arguments, five original arguments including stack, and original return.";
  }finally{if(code!=IntPtr.Zero)VirtualFreeEx(h,code,UIntPtr.Zero,0x8000);Marshal.FreeHGlobal(data);}
 }
}
