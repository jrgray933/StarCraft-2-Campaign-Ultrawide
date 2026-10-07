using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public partial class SC2HudScale {
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
  B(c,"48 8D 77 40 8B 6F 30 85 ED");int empty=J(c,"0F 84");B(c,"81 FD 00 40 00 00");int excessive=J(c,"0F 87");
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
 public static Prepared Prepare(int pid,ulong owner,ulong table,ulong setter,Command[] commands,ulong activeTable=0,WidthTarget[] cargoWidths=null){
  ulong module=table-SC2Addresses.Rva(0x2D51C58);SC2CampaignGate.Require(pid,module);
  if(commands.Length<1||commands.Length>16384)throw new Exception("Invalid command count");
  var h=OpenProcess(0x438,false,pid);if(h==IntPtr.Zero)throw Error("OpenProcess");IntPtr data=IntPtr.Zero,code=IntPtr.Zero;bool ready=false;
  try{
   if(activeTable==0)activeTable=table;
   if(BitConverter.ToUInt64(Read(h,owner,8),0)!=activeTable)throw new Exception("Unexpected frame vtable");
   var vt=Read(h,activeTable-16,0x278);ulong original=BitConverter.ToUInt64(vt,0x158);
   data=VirtualAllocEx(h,IntPtr.Zero,(UIntPtr)0xA3000,0x3000,4);code=VirtualAllocEx(h,IntPtr.Zero,(UIntPtr)12288,0x3000,4);if(data==IntPtr.Zero||code==IntPtr.Zero)throw Error("Allocation");
   ulong state=(ulong)data.ToInt64(),entry=(ulong)code.ToInt64();var bytes=Build(state,original,module);var buffer=new byte[0xA3000];
   // A per-instance queue update keeps the game-computed row width proportional.
   foreach(var x in commands)if(x.Side==6){
    if(Q(Read(h,x.Frame,8),0)!=module+SC2Addresses.Rva(0x2d99510))throw new Exception("Queue panel changed before resizing.");
    if(Q(Read(h,module+SC2Addresses.Rva(0x2d99510)+0x150,8),0)!=module+SC2Addresses.Rva(0xdc31c0))throw new Exception("Unexpected queue update.");
    var queueTable=Read(h,module+SC2Addresses.Rva(0x2d99510)-16,0x400);
    var queueCode=BuildQueueUpdate(x.Frame,Q(Read(h,x.Frame+0x138,8),0),entry+0xC00,module+SC2Addresses.Rva(0xdc31c0),module+SC2Addresses.Rva(0xadde10),setter,x.Offset,module);
    if(queueCode.Length>0x400)throw new Exception("Queue callback exceeds capacity.");
    Array.Copy(BitConverter.GetBytes(entry+0xC00),0,queueTable,0x160,8);Array.Copy(queueTable,0,buffer,0xA1800,queueTable.Length);
    x.Parent=state+0xA1810;Write(h,entry+0xC00,queueCode);
   }
   foreach(var x in commands)if(x.Side==7){
    if(Q(Read(h,x.Frame,8),0)!=module+SC2Addresses.Rva(0x2d97840)||Q(Read(h,module+SC2Addresses.Rva(0x2d97840)+0x150,8),0)!=module+SC2Addresses.Rva(0xdba490)||cargoWidths==null||cargoWidths.Length<1||cargoWidths.Length>8)throw new Exception("Cargo panel changed before resizing.");
    for(int i=0;i<cargoWidths.Length;i++){var w=cargoWidths[i];Array.Copy(BitConverter.GetBytes(w.Frame),0,buffer,0xA1400+16*i,8);Array.Copy(BitConverter.GetBytes(w.Width),0,buffer,0xA1408+16*i,4);}
    var cargoTable=Read(h,module+SC2Addresses.Rva(0x2d97840)-16,0x400);var cargoCode=BuildCargoUpdate(x.Frame,state+0xA1400,cargoWidths.Length,module+SC2Addresses.Rva(0xdba490),module+SC2Addresses.Rva(0x16a7680),module+SC2Addresses.Rva(0x16bfb40),x.Offset,module);
    if(cargoCode.Length>4096)throw new Exception("Cargo callback exceeds capacity.");Array.Copy(BitConverter.GetBytes(entry+0x1000),0,cargoTable,0x160,8);Array.Copy(cargoTable,0,buffer,0xA1C00,cargoTable.Length);x.Parent=state+0xA1C10;Write(h,entry+0x1000,cargoCode);
   }
   if(Q(Read(h,module+SC2Addresses.Rva(0x2dccec8)+0x138,8),0)!=module+SC2Addresses.Rva(0x16d3ed0)||Q(Read(h,module+SC2Addresses.Rva(0x2dccec8)+0x148,8),0)!=module+SC2Addresses.Rva(0x16d6250))throw new Exception("Unexpected text layout callbacks.");
   // Share per-HUD label callbacks, including labels that are not visible yet.
   var textTable=Read(h,module+SC2Addresses.Rva(0x2dccec8)-16,0x400);
   Array.Copy(BitConverter.GetBytes(entry+0x2000),0,textTable,0x148,8);
   Array.Copy(BitConverter.GetBytes(entry+0x2400),0,textTable,0x158,8);
   Array.Copy(textTable,0,buffer,0xA2000,textTable.Length);
   float textScale=1;
   foreach(var x in commands)if(x.Side==8){textScale=x.Offset;x.Parent=state+0xA2010;}
   var measureCode=BuildTextMeasure(module+SC2Addresses.Rva(0x16d3ed0),textScale,module);
   var layoutCode=BuildTextLayout(module+SC2Addresses.Rva(0x16a7680),module+SC2Addresses.Rva(0x16a6490),module+SC2Addresses.Rva(0x16d8a20),module+SC2Addresses.Rva(0x16d8900),module+SC2Addresses.Rva(0x16d8b40),module+SC2Addresses.Rva(0x16d6250),textScale,module);
   if(measureCode.Length>0x400||layoutCode.Length>0xC00)throw new Exception("Text callbacks exceed capacity.");
   Write(h,entry+0x2000,measureCode);Write(h,entry+0x2400,layoutCode);
   Array.Copy(BitConverter.GetBytes(owner),0,buffer,24,8);Array.Copy(BitConverter.GetBytes(original),0,buffer,32,8);Array.Copy(BitConverter.GetBytes(entry+0x800),0,buffer,40,8);Array.Copy(BitConverter.GetBytes(commands.Length),0,buffer,48,4);
   for(int i=0;i<commands.Length;i++){var x=commands[i];int p=64+i*40;Array.Copy(BitConverter.GetBytes(x.Frame),0,buffer,p,8);Array.Copy(BitConverter.GetBytes(x.Parent),0,buffer,p+8,8);Array.Copy(BitConverter.GetBytes(x.Side),0,buffer,p+16,4);Array.Copy(BitConverter.GetBytes(x.Position),0,buffer,p+20,4);Array.Copy(BitConverter.GetBytes(x.Offset),0,buffer,p+24,4);Array.Copy(BitConverter.GetBytes(x.OldPosition),0,buffer,p+28,4);Array.Copy(BitConverter.GetBytes(x.OldOffset),0,buffer,p+32,4);}
   Array.Copy(BitConverter.GetBytes(entry),0,vt,0x158,8);Array.Copy(vt,0,buffer,0xA1000,vt.Length);Write(h,state,buffer);Write(h,entry,bytes);Write(h,entry+0x800,BuildTextDispatcher(setter,module+SC2Addresses.Rva(0x16a7680),module+SC2Addresses.Rva(0x16a6490),module+SC2Addresses.Rva(0x16d8a20),module+SC2Addresses.Rva(0x16d8900),module+SC2Addresses.Rva(0x16d8b40),module+SC2Addresses.Rva(0x16d6250)));uint old;
   if(!VirtualProtectEx(h,code,(UIntPtr)12288,0x20,out old)||!FlushInstructionCache(h,code,(UIntPtr)12288))throw Error("Protect code");
   var check=Read(h,entry,bytes.Length);for(int i=0;i<bytes.Length;i++)if(check[i]!=bytes[i])throw new Exception("Code readback failed");
   ready=true;return new Prepared{Code=entry,State=state,VTable=state+0xA1010,Length=bytes.Length};
  }finally{if(!ready){if(code!=IntPtr.Zero)VirtualFreeEx(h,code,UIntPtr.Zero,0x8000);if(data!=IntPtr.Zero)VirtualFreeEx(h,data,UIntPtr.Zero,0x8000);}CloseHandle(h);}
 }


 static void Call(List<byte> c,ulong address){B(c,"48 B8");I(c,address);B(c,"FF D0");}
 public static byte[] BuildTextDispatcher(ulong anchor,ulong width,ulong height,ulong baseWidth,ulong baseHeight,ulong scale,ulong layout,ulong invalidate=0){
  if(invalidate==0)invalidate=anchor-SC2Addresses.Rva(0x16bcfc0)+SC2Addresses.Rva(0x16a8480);
  var c=new List<byte>();
  B(c,"83 FA 05");int notCargo=J(c,"0F 85");
  B(c,"44 89 89 F0 01 00 00 83 89 E8 01 00 00 01 B0 01 C3");F(c,notCargo,c.Count);
  B(c,"83 FA 07");int notCargoHook=J(c,"0F 85");B(c,"45 85 C9");int restoreCargo=J(c,"0F 84");B(c,"4C 89 01 B0 01 C3");F(c,restoreCargo,c.Count);B(c,"48 B8");I(c,anchor-SC2Addresses.Rva(0x16bcfc0)+SC2Addresses.Rva(0x2d97840));B(c,"48 89 01 B0 01 C3");F(c,notCargoHook,c.Count);
  B(c,"83 FA 06");int notQueue=J(c,"0F 85");
  B(c,"45 85 C9");int restoreQueue=J(c,"0F 84");B(c,"4C 89 01 B0 01 C3");
  F(c,restoreQueue,c.Count);B(c,"48 B8");I(c,anchor-SC2Addresses.Rva(0x16bcfc0)+SC2Addresses.Rva(0x2d99510));B(c,"48 89 01 B0 01 C3");F(c,notQueue,c.Count);
  B(c,"83 FA 08");int notTextHook=J(c,"0F 85");
  B(c,"53 48 83 EC 20 48 89 CB 45 85 C9");int restoreText=J(c,"0F 84");
  B(c,"4C 89 01");int textDone=J(c,"E9");
  F(c,restoreText,c.Count);B(c,"48 B8");I(c,anchor-SC2Addresses.Rva(0x16bcfc0)+SC2Addresses.Rva(0x2dccec8));B(c,"48 89 01 31 D2");Call(c,scale);
  B(c,"48 8B 83 D8 01 00 00 48 85 C0");int noText=J(c,"0F 84");
  B(c,"48 C7 40 38 00 00 00 00 C7 40 48 00 00 80 3F C7 40 4C 00 00 80 3F 83 60 20 FA");
  F(c,noText,c.Count);F(c,textDone,c.Count);
  B(c,"48 89 D9 BA 0F 00 00 00");Call(c,invalidate);
  B(c,"B0 01 48 83 C4 20 5B C3");F(c,notTextHook,c.Count);
  B(c,"83 FA 04");int ordinary=J(c,"0F 85");
  B(c,"53 56 57 48 83 EC 30 48 89 CB 44 89 CF");
  B(c,"48 89 D9");Call(c,width);B(c,"48 89 D9 0F 28 C8");Call(c,baseWidth);
  B(c,"48 89 D9");Call(c,height);B(c,"48 89 D9 0F 28 C8");Call(c,baseHeight);
  B(c,"48 89 D9 BA 01 00 00 00");Call(c,scale);
  B(c,"48 89 D9");Call(c,layout);
  B(c,"85 FF");int enabled=J(c,"0F 85");B(c,"48 89 D9 31 D2");Call(c,scale);
  F(c,enabled,c.Count);B(c,"B0 01 48 83 C4 30 5F 5E 5B C3");
  F(c,ordinary,c.Count);B(c,"48 B8");I(c,anchor);B(c,"FF E0");return c.ToArray();
 }
 // Keep natural measurements proportional without freezing them to the current string.
 public static byte[] BuildTextMeasure(ulong original,float scale,ulong module=0){
  var c=new List<byte>();var denied=new List<int>();
  if(module!=0)SC2CampaignGate.Emit(c,denied,module+SC2CampaignGate.SessionRva,module+SC2CampaignGate.MapRva);
  B(c,"48 83 EC 28");Call(c,original);
  B(c,"B8");c.AddRange(BitConverter.GetBytes(scale));B(c,"66 0F 6E C8 F3 0F 59 C1 48 83 C4 28 C3");SC2CampaignGate.Resolve(c,denied,c.Count);B(c,"48 B8");I(c,original);B(c,"FF E0");return c.ToArray();
 }
 // The renderer needs unscaled clipping bounds even though the frame is scaled.
 public static byte[] BuildTextLayout(ulong width,ulong height,ulong baseWidth,ulong baseHeight,ulong scaleToFit,ulong original,float scale,ulong module=0){
  var c=new List<byte>();var denied=new List<int>();
  if(module!=0)SC2CampaignGate.Emit(c,denied,module+SC2CampaignGate.SessionRva,module+SC2CampaignGate.MapRva);
  B(c,"53 48 83 EC 20 48 89 CB");
  foreach(var pair in new[]{new[]{width,baseWidth},new[]{height,baseHeight}}){
   B(c,"48 89 D9");Call(c,pair[0]);B(c,"B8");c.AddRange(BitConverter.GetBytes(scale));
   B(c,"66 0F 6E C8 F3 0F 5E C1 0F 28 C8 48 89 D9");Call(c,pair[1]);
  }
  B(c,"48 89 D9 BA 01 00 00 00");Call(c,scaleToFit);
  B(c,"48 89 D9 48 83 C4 20 5B");SC2CampaignGate.Resolve(c,denied,c.Count);B(c,"48 B8");I(c,original);B(c,"FF E0");return c.ToArray();
 }
 public static byte[] BuildQueueUpdate(ulong frame,ulong panel,ulong entry,ulong original,ulong selected,ulong setter,float scale,ulong module){
  var c=new List<byte>();var denied=new List<int>();
  // Preserve the original return and all forwarded arguments, including the fifth stack argument.
  B(c,"53 48 83 EC 50 48 89 CB 48 8B 84 24 80 00 00 00 48 89 44 24 20");Call(c,original);B(c,"89 44 24 30");
  B(c,"48 B8");I(c,frame);B(c,"48 39 C3");denied.Add(J(c,"0F 85"));
  B(c,"48 B8");I(c,panel);B(c,"48 39 83 38 01 00 00");denied.Add(J(c,"0F 85"));
  if(module!=0)SC2CampaignGate.Emit(c,denied,module+SC2CampaignGate.SessionRva,module+SC2CampaignGate.MapRva);
  Call(c,selected);B(c,"48 85 C0");denied.Add(J(c,"0F 84"));
  foreach(int side in new[]{1,3}){
   B(c,"48 B9");I(c,panel);B(c,"49 89 D8 BA");c.AddRange(BitConverter.GetBytes(side));B(c,"41 B9 00 00 00 3F");
   B(c,"F3 0F 10 81");c.AddRange(BitConverter.GetBytes(0x74+16*side));
   B(c,"B8");c.AddRange(BitConverter.GetBytes(scale));B(c,"66 0F 6E C8 F3 0F 59 C1 F3 0F 11 44 24 20 48 C7 44 24 28 00 00 00 00");Call(c,setter);
  }
  SC2CampaignGate.Resolve(c,denied,c.Count);B(c,"8B 44 24 30 48 83 C4 50 5B C3");return c.ToArray();
 }
 public static byte[] BuildCargoUpdate(ulong frame,ulong widths,int count,ulong original,ulong getWidth,ulong setWidth,float scale,ulong module){
  var c=new List<byte>();var denied=new List<int>();
  B(c,"53 48 83 EC 60 48 89 CB 48 89 4C 24 30 48 89 54 24 38 4C 89 44 24 40 4C 89 4C 24 48 C7 44 24 58 00 00 00 00");
  B(c,"48 B8");I(c,frame);B(c,"48 39 C3");denied.Add(J(c,"0F 85"));
  if(module!=0)SC2CampaignGate.Emit(c,denied,module+SC2CampaignGate.SessionRva,module+SC2CampaignGate.MapRva);
  B(c,"C7 44 24 58 01 00 00 00");
  // Restore the previous native widths before its state-dependent layout, then scale the newly computed widths.
  for(int i=0;i<count;i++){B(c,"48 B8");I(c,widths+(ulong)i*16);B(c,"48 8B 08 F3 0F 10 48 08");Call(c,setWidth);}
  SC2CampaignGate.Resolve(c,denied,c.Count);
  B(c,"48 8B 4C 24 30 48 8B 54 24 38 4C 8B 44 24 40 4C 8B 4C 24 48 48 8B 84 24 90 00 00 00 48 89 44 24 20");Call(c,original);B(c,"89 44 24 50 83 7C 24 58 00");var done=new List<int>();done.Add(J(c,"0F 84"));
  if(module!=0)SC2CampaignGate.Emit(c,done,module+SC2CampaignGate.SessionRva,module+SC2CampaignGate.MapRva);
  for(int i=0;i<count;i++){
   B(c,"48 B8");I(c,widths+(ulong)i*16);B(c,"48 8B 08");Call(c,getWidth);B(c,"0F 57 D2 0F 2F C2");int empty=J(c,"0F 86");
   B(c,"48 B8");I(c,widths+(ulong)i*16);B(c,"F3 0F 11 40 08 48 8B 08 BA");c.AddRange(BitConverter.GetBytes(scale));B(c,"66 0F 6E CA F3 0F 59 C8");Call(c,setWidth);F(c,empty,c.Count);
  }
  SC2CampaignGate.Resolve(c,done,c.Count);B(c,"8B 44 24 50 48 83 C4 60 5B C3");return c.ToArray();
 }
 [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate float SizeGetter(IntPtr frame);
 [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate void SizeSetter(IntPtr frame,float value);
 [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate void ScaleSetter(IntPtr frame,byte value);
 [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate void LayoutSetter(IntPtr frame);
 public static string TextSelfTest(){
  var calls=new List<string>();
  SizeGetter width=delegate(IntPtr f){if(f.ToInt64()!=123)throw new Exception("Width frame");calls.Add("width");return 200;};
  SizeGetter height=delegate(IntPtr f){if(f.ToInt64()!=123)throw new Exception("Height frame");calls.Add("height");return 50;};
  SizeSetter bw=delegate(IntPtr f,float v){if(f.ToInt64()!=123||v!=200)throw new Exception("Base width");calls.Add("bw");};
  SizeSetter bh=delegate(IntPtr f,float v){if(f.ToInt64()!=123||v!=50)throw new Exception("Base height");calls.Add("bh");};
  ScaleSetter scale=delegate(IntPtr f,byte v){if(f.ToInt64()!=123)throw new Exception("Scale frame");calls.Add("scale"+v);};
  LayoutSetter layout=delegate(IntPtr f){if(f.ToInt64()!=123)throw new Exception("Layout frame");calls.Add("layout");};
  Setter anchor=delegate(IntPtr f,int side,IntPtr parent,uint pos,float off,byte flag){if(f.ToInt64()!=123||side!=1||parent.ToInt64()!=456||pos!=0x3f000000||off!=42||flag!=0)throw new Exception("Forward anchor");calls.Add("anchor");return 1;};
  IntPtr code=IntPtr.Zero;var h=GetCurrentProcess();try{
   var bytes=BuildTextDispatcher((ulong)Marshal.GetFunctionPointerForDelegate(anchor).ToInt64(),(ulong)Marshal.GetFunctionPointerForDelegate(width).ToInt64(),(ulong)Marshal.GetFunctionPointerForDelegate(height).ToInt64(),(ulong)Marshal.GetFunctionPointerForDelegate(bw).ToInt64(),(ulong)Marshal.GetFunctionPointerForDelegate(bh).ToInt64(),(ulong)Marshal.GetFunctionPointerForDelegate(scale).ToInt64(),(ulong)Marshal.GetFunctionPointerForDelegate(layout).ToInt64());
   code=VirtualAllocEx(h,IntPtr.Zero,(UIntPtr)4096,0x3000,4);if(code==IntPtr.Zero)throw Error("Allocation");Marshal.Copy(bytes,0,code,bytes.Length);uint old;if(!VirtualProtectEx(h,code,(UIntPtr)4096,0x20,out old))throw Error("Protect");FlushInstructionCache(h,code,(UIntPtr)bytes.Length);var fn=(Setter)Marshal.GetDelegateForFunctionPointer(code,typeof(Setter));
   fn((IntPtr)123,4,(IntPtr)456,0x3f800000,0,0);if(String.Join(",",calls)!="width,bw,height,bh,scale1,layout")throw new Exception("Apply order");calls.Clear();
   fn((IntPtr)123,4,(IntPtr)456,0,0,0);if(String.Join(",",calls)!="width,bw,height,bh,scale1,layout,scale0")throw new Exception("Restore order");calls.Clear();
   fn((IntPtr)123,1,(IntPtr)456,0x3f000000,42,0);if(String.Join(",",calls)!="anchor")throw new Exception("Anchor path");
   GC.KeepAlive(width);GC.KeepAlive(height);GC.KeepAlive(bw);GC.KeepAlive(bh);GC.KeepAlive(scale);GC.KeepAlive(layout);GC.KeepAlive(anchor);return "PASS: native text dispatcher apply/restore order, float arguments, and anchor forwarding.";
  }finally{if(code!=IntPtr.Zero)VirtualFreeEx(h,code,UIntPtr.Zero,0x8000);}
 }
 public static string BatchSelfTest(){
  const int count=16384;var data=Marshal.AllocHGlobal(0xA2000);IntPtr code=IntPtr.Zero;var h=GetCurrentProcess();int setters=0,originals=0;
  Original original=delegate(IntPtr owner,ulong a2,ulong a3,ulong a4,ulong a5){originals++;return 12345;};
  Setter setter=delegate(IntPtr frame,int side,IntPtr parent,uint pos,float offset,byte flag){if(frame.ToInt64()!=100||parent.ToInt64()!=200||side!=1||flag!=0||offset!=.5f)throw new Exception("Batch arguments");setters++;return 1;};
  try{
   Marshal.Copy(new byte[0xA3000],0,data,0xA2000);Marshal.WriteInt64(data,24,123);Marshal.WriteInt64(data,40,Marshal.GetFunctionPointerForDelegate(setter).ToInt64());Marshal.WriteInt32(data,48,count);
   for(int i=0;i<count;i++){int p=64+i*40;Marshal.WriteInt64(data,p,100);Marshal.WriteInt64(data,p+8,200);Marshal.WriteInt32(data,p+16,1);Marshal.WriteInt32(data,p+24,0x3f000000);Marshal.WriteInt32(data,p+32,0x3f000000);}
   var bytes=Build((ulong)data.ToInt64(),(ulong)Marshal.GetFunctionPointerForDelegate(original).ToInt64());code=VirtualAllocEx(h,IntPtr.Zero,(UIntPtr)4096,0x3000,4);if(code==IntPtr.Zero)throw Error("Allocation");Marshal.Copy(bytes,0,code,bytes.Length);uint old;if(!VirtualProtectEx(h,code,(UIntPtr)4096,0x20,out old))throw Error("Protect");FlushInstructionCache(h,code,(UIntPtr)bytes.Length);var wrapper=(Original)Marshal.GetDelegateForFunctionPointer(code,typeof(Original));
   Marshal.WriteInt32(data,0,1);wrapper((IntPtr)123,22,33,44,55);if(setters!=count)throw new Exception("Full batch failed");
   Marshal.WriteInt32(data,0,2);wrapper((IntPtr)123,22,33,44,55);if(setters!=2*count)throw new Exception("Full restore failed");
   Marshal.WriteInt32(data,48,count+1);Marshal.WriteInt32(data,0,1);wrapper((IntPtr)123,22,33,44,55);if(setters!=2*count||originals!=3||Marshal.ReadInt32(data,4)!=0)throw new Exception("Capacity guard failed");
   GC.KeepAlive(original);GC.KeepAlive(setter);GC.KeepAlive(wrapper);return "PASS: 16,384-command apply and restore; excess-capacity rejection.";
  }finally{if(code!=IntPtr.Zero)VirtualFreeEx(h,code,UIntPtr.Zero,0x8000);Marshal.FreeHGlobal(data);}
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

public partial class SC2HudScale {
 public class Identity { public ulong Frame,VTable,Parent; }
 public class WidthTarget {public ulong Frame;public float Width;}
 public class Plan {public WidthTarget[] CargoWidths;public ulong Owner,Console,World,MenuConsole,MenuFullscreen;public Command[] Commands;public Identity[] Frames;}
 class Frame {public ulong Address,Table,Parent;public byte[] Data;public int Root,Depth;public bool Excluded;}
 static ulong Q(byte[] b,int n){return BitConverter.ToUInt64(b,n);}
 static float S(byte[] b,int n){return BitConverter.ToSingle(b,n);}
 static void Walk(IntPtr h,ulong module,ulong address,int root,int depth,bool excluded,List<Frame> frames,HashSet<ulong> seen,Dictionary<ulong,ulong> aliases=null){
  if(depth>40||frames.Count>=20000||!seen.Add(address))throw new Exception("Campaign HUD changed while reading its layout.");
  var b=Read(h,address,0x130);ulong vt=Q(b,0),parent=Q(b,0x50);if(aliases!=null&&aliases.ContainsKey(vt))vt=aliases[vt];
  if(!SC2Addresses.IsReadOnlyAddress(module,vt)&&!(root==0xc68&&depth==0))throw new Exception("Unexpected campaign HUD frame.");
  if(root==0xc68&&(vt==module+SC2Addresses.Rva(0x2d6a748)||vt==module+SC2Addresses.Rva(0x2d78b08)))excluded=true;
  if(root==0xc68 && depth==1 && IsNativeCampaignPanel(h,module,new Frame{Address=address,Table=vt,Parent=parent},parent))excluded=true;
  frames.Add(new Frame{Address=address,Table=vt,Parent=parent,Data=b,Root=root,Depth=depth,Excluded=excluded});
  ulong node=Q(b,0x40);int count=0;
  while(node!=0&&(node&1)==0){ulong child=node-0x18;if(Q(Read(h,child+0x50,8),0)!=address||++count>1000)throw new Exception("Campaign HUD hierarchy changed.");Walk(h,module,child,root,depth+1,excluded,frames,seen,aliases);node=Q(Read(h,node+8,8),0);}
 }
 static string CampaignPanelName(IntPtr h,Frame f){
  ulong descriptor=Q(Read(h,f.Address+8,8),0);if(descriptor==0)return "";
  ulong token=Q(Read(h,descriptor+0x58,8),0);if(token==0)return "";
  var data=Read(h,token+0x18,24);int length=(int)(BitConverter.ToUInt32(data,0)>>2);
  if(length<1||length>128)return "";
  ulong address=(BitConverter.ToUInt32(data,4)&2)!=0?Q(data,8):token+0x20;
  return System.Text.Encoding.ASCII.GetString(Read(h,address,length));
 }
 static bool IsLeftCampaignPanel(IntPtr h,ulong module,Frame f,ulong owner){
  return f.Parent==owner && (f.Table==module+SC2Addresses.Rva(0x2d9ebe0) || (f.Table==module+SC2Addresses.Rva(0x2d51c58) && CampaignPanelName(h,f)=="HeroUnitFrame"));
 }
 static bool IsNativeCampaignPanel(IntPtr h,ulong module,Frame f,ulong owner){
  if(f.Parent!=owner)return false;
  if(f.Table==module+SC2Addresses.Rva(0x2d9ebe0)||f.Table==module+SC2Addresses.Rva(0x2d6f9d0))return true;
  if(f.Table!=module+SC2Addresses.Rva(0x2d51c58))return false;
  switch(CampaignPanelName(h,f)){
   case "HeroUnitFrame":case "SecondaryHeroUnitFrame":case "BossUnitFrame":
   case "ProgressUnitFrame":case "SmallProgressUnitFrame":case "TugOfWarFrame":return true;
   default:return false;
  }
 }
 public static void ValidateScale(int width,int height,int percent){
  if(percent<50||percent>100||percent%5!=0)throw new Exception("Choose a HUD size from 50% to 100%, in steps of 5%.");
  if(width*9L*100<height*16L*percent)throw new Exception("This HUD size does not fit the selected resolution. Choose a smaller HUD size or a wider resolution.");
 }
 public static Plan CreatePlan(int pid,ulong module,int width,int height,int percent){
  ValidateScale(width,height,percent);SC2CampaignGate.Require(pid,module);
  var h=OpenProcess(0x410,false,pid);if(h==IntPtr.Zero)throw Error("Read campaign HUD");
  try{
   ulong ui=Q(Read(h,module+SC2Addresses.Rva(0x4032368),8),0),owner=Q(Read(h,ui+0xc68,8),0),console=Q(Read(h,ui+0xc60,8),0);
   if(Q(Read(h,ui,8),0)!=module+SC2Addresses.Rva(0x2d522a8)||Q(Read(h,console,8),0)!=module+SC2Addresses.Rva(0x2d62e98))throw new Exception("Waiting for the supported campaign HUD.");
   var frames=new List<Frame>();var seen=new HashSet<ulong>();foreach(int root in new[]{0xc60,0xc68,0xc78})Walk(h,module,Q(Read(h,ui+(ulong)root,8),0),root,0,false,frames,seen);
   ulong menu=Q(Read(h,ui+0xc80,8),0),full=Q(Read(h,ui+0xc88,8),0);
   foreach(ulong a in new[]{menu,full}){var d=Read(h,a,0x130);if(Q(d,0)!=module+SC2Addresses.Rva(0x2d51c58))throw new Exception("Unexpected campaign menu anchor.");frames.Add(new Frame{Address=a,Table=Q(d,0),Parent=Q(d,0x50),Data=d,Root=0xc80});}
   float scale=percent/100f,inset=600f*width/height-scale*1200f*8/9;float horizontal=(float)width/height/(4f/3);
   var cargoWidths=new List<WidthTarget>();ulong cargo=0;foreach(var f in frames)if(f.Table==module+SC2Addresses.Rva(0x2d97840)&&!f.Excluded)cargo=f.Address;
   var textCommands=new List<Command>();var commands=new List<Command>();var checks=new List<Identity>();
   foreach(var f in frames){
    bool leftPanel=IsLeftCampaignPanel(h,module,f,owner),nativePanel=IsNativeCampaignPanel(h,module,f,owner);

    if((f.Excluded&&!nativePanel)||f.Address==owner)continue;

    byte[] d=f.Data;
    if(f.Address==cargo||(f.Parent==cargo&&f.Table==module+SC2Addresses.Rva(0x2dccec8))){
     float w=0;if(Q(d,0x78)==f.Address&&BitConverter.ToInt16(d,0x80)==2048)w=-S(d,0x84);
     else if(Q(d,0x78)==Q(d,0x98)&&BitConverter.ToInt16(d,0x80)==BitConverter.ToInt16(d,0xa0))w=S(d,0xa4)-S(d,0x84);
     if(w<=0||w>2000)throw new Exception("Unexpected cargo panel width.");cargoWidths.Add(new WidthTarget{Frame=f.Address,Width=w});
    }
    if(percent!=100 && f.Table==module+SC2Addresses.Rva(0x2d97840)){float cell=S(Read(h,f.Address+0x1f0,4),0);if(cell!=64f)throw new Exception("Unexpected cargo cell size.");textCommands.Add(new Command{Frame=f.Address,Parent=f.Address,Side=5,Position=cell*scale,OldPosition=cell});}
    if(percent!=100 && f.Table==module+SC2Addresses.Rva(0x2d97840))commands.Add(new Command{Frame=f.Address,Parent=f.Address,Side=7,Position=1,OldPosition=0,Offset=scale});
    if(percent!=100 && f.Table==module+SC2Addresses.Rva(0x2d99510))commands.Add(new Command{Frame=f.Address,Parent=f.Address,Side=6,Position=1,OldPosition=0,Offset=scale});
    checks.Add(new Identity{Frame=f.Address,VTable=f.Table,Parent=f.Parent});
    if(percent!=100 && f.Table==module+SC2Addresses.Rva(0x2dccec8)&&(BitConverter.ToUInt32(Read(h,f.Address+0x1d0,4),0)&16)==0){
     textCommands.Add(new Command{Frame=f.Address,Parent=f.Address,Side=8,Position=1,OldPosition=0,Offset=scale});
    }
    if(f.Table==module+SC2Addresses.Rva(0x2d97498))continue;
    for(int side=0;side<4;side++){
     int a=0x68+16*side;ulong relative=Q(d,a);if(relative==0)continue;
     if((BitConverter.ToUInt16(d,a+10)&4)!=0)throw new Exception("This campaign uses an unsupported HUD offset.");
     float position=BitConverter.ToInt16(d,a+8)/2048f,offset=S(d,a+12),np=position,no=nativePanel?offset:offset*scale;
     if(relative==f.Address&&Math.Abs(offset)<.0001f&&f.Table!=module+SC2Addresses.Rva(0x2dccec8)){float size=(side%2==1)?(S(d,0xb4)-S(d,0xac))*horizontal:S(d,0xb0)-S(d,0xa8);no=size*(nativePanel?1:scale)*(side<2?-1:1);}
     if(f.Address==console){no=side==0?1200*(1-scale):side==1?inset:side==3?-inset:0;}
     else if(relative==owner&&!nativePanel)np=side%2==1?(1-scale)/2+scale*position:1-scale+scale*position;
     if(f.Root==0xc80&&side==3)no=-inset;
     if(relative==owner && side%2==1){
      if(leftPanel && side==1){np=0;no=offset;}
      else if(!nativePanel)no+=(float)Math.Max(0,600.0*width/height-1200.0*8/9)*(1-2*np);
     }
     if(Math.Abs(np-position)<.0001&&Math.Abs(no-offset)<.0001)continue;
     commands.Add(new Command{Frame=f.Address,Parent=relative,Side=side,Position=np,Offset=no,OldPosition=position,OldOffset=offset});
    }
   }
   // Keep the clipping container full width; child anchors retain their HUD positions.
   var ownerData=Read(h,owner,0x130);ulong ownerParent=Q(ownerData,0x50);
   checks.Add(new Identity{Frame=owner,VTable=Q(ownerData,0),Parent=ownerParent});
   foreach(int side in new[]{1,3}){
    int a=0x68+side*16;
    if(Q(ownerData,a)!=ownerParent || (BitConverter.ToUInt16(ownerData,a+10)&4)!=0)throw new Exception("Unexpected campaign container anchor.");
    commands.Add(new Command{Frame=owner,Parent=ownerParent,Side=side,Position=side==1?0f:1f,Offset=0,OldPosition=BitConverter.ToInt16(ownerData,a+8)/2048f,OldOffset=S(ownerData,a+12)});
   }
   textCommands.AddRange(commands);if(textCommands.Count>16384)throw new Exception("This campaign HUD is too large for scaling.");
   foreach(var f in frames){if(f.Excluded||f.Address==owner)continue;var current=Read(h,f.Address,0x130);if(Q(current,0)!=f.Table||Q(current,0x50)!=f.Parent)throw new Exception("Campaign HUD changed; waiting for it to settle.");for(int j=0x68;j<0xa8;j++)if(current[j]!=f.Data[j])throw new Exception("Campaign HUD changed; waiting for it to settle.");}
   return new Plan{CargoWidths=cargoWidths.ToArray(),Owner=owner,Console=console,World=Q(Read(h,ui+0xc18,8),0),MenuConsole=menu,MenuFullscreen=full,Commands=textCommands.ToArray(),Frames=checks.ToArray()};
  }finally{CloseHandle(h);}
 }
 public static void GuardRestore(int pid,ulong module,ulong state,int count,Identity[] expected){
  if(count<1||count>16384)throw new Exception("Invalid saved HUD size.");
  var h=OpenProcess(0x438,false,pid);if(h==IntPtr.Zero)throw Error("Restore campaign HUD");
  try{
   if(BitConverter.ToUInt32(Read(h,state+4,4),0)!=0)throw new Exception("HUD update is busy.");
   var aliases=new Dictionary<ulong,ulong>();for(int i=0;i<count;i++){var cmd=Read(h,state+64+(ulong)i*40,40);if(BitConverter.ToInt32(cmd,16)==6)aliases[Q(cmd,8)]=module+SC2Addresses.Rva(0x2d99510);else if(BitConverter.ToInt32(cmd,16)==7)aliases[Q(cmd,8)]=module+SC2Addresses.Rva(0x2d97840);else if(BitConverter.ToInt32(cmd,16)==8)aliases[Q(cmd,8)]=module+SC2Addresses.Rva(0x2dccec8);}
   ulong ui=Q(Read(h,module+SC2Addresses.Rva(0x4032368),8),0);var live=new List<Frame>();var seen=new HashSet<ulong>();foreach(int root in new[]{0xc60,0xc68,0xc78})Walk(h,module,Q(Read(h,ui+(ulong)root,8),0),root,0,false,live,seen,aliases);
   seen.Add(Q(Read(h,ui+0xc80,8),0));seen.Add(Q(Read(h,ui+0xc88,8),0));var valid=new HashSet<ulong>();
   ulong currentOwner=Q(Read(h,ui+0xc68,8),0);
   foreach(var e in expected)if(e.Frame==currentOwner && Q(Read(h,currentOwner,8),0)==state+0xA1010)aliases[state+0xA1010]=e.VTable;
   foreach(var e in expected){if(!seen.Contains(e.Frame))continue;var d=Read(h,e.Frame,0x58);ulong vt=Q(d,0);if(aliases.ContainsKey(vt))vt=aliases[vt];if(vt==e.VTable&&Q(d,0x50)==e.Parent)valid.Add(e.Frame);}
   for(int i=0;i<count;i++){ulong a=state+64+(ulong)i*40;ulong frame=Q(Read(h,a,8),0);if(frame!=0&&!valid.Contains(frame))Write(h,a,new byte[8]);}
  }finally{CloseHandle(h);}
 }
 [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate IntPtr SelectedGetter();
 public static string DynamicSelfTest(){
  var frame=Marshal.AllocHGlobal(0x300);var panel=Marshal.AllocHGlobal(0x200);IntPtr code=IntPtr.Zero;var h=GetCurrentProcess();int calls=0;bool selected=true;float expected=0;
  Marshal.Copy(new byte[0x300],0,frame,0x300);Marshal.Copy(new byte[0x200],0,panel,0x200);Marshal.WriteInt64(frame,0x138,panel.ToInt64());
  Original original=delegate(IntPtr f,ulong a,ulong b,ulong c,ulong d){if(f!=frame||a!=22||b!=33||c!=44||d!=55)throw new Exception("Queue original arguments");if(selected){Marshal.WriteInt32(panel,0x84,BitConverter.ToInt32(BitConverter.GetBytes(-131f),0));Marshal.WriteInt32(panel,0xa4,BitConverter.ToInt32(BitConverter.GetBytes(131f),0));}return 12345;};
  SelectedGetter get=delegate(){return selected?(IntPtr)1:IntPtr.Zero;};
  Setter setter=delegate(IntPtr f,int side,IntPtr parent,uint position,float offset,byte flags){if(f!=panel||parent!=frame||(side!=1&&side!=3)||position!=0x3f000000||flags!=0||Math.Abs(offset-(side==1?-expected:expected))>.0001)throw new Exception("Queue scaled anchor");Marshal.WriteInt32(panel,0x74+16*side,BitConverter.ToInt32(BitConverter.GetBytes(offset),0));calls++;return 1;};
  try{
   for(int p=50;p<=100;p+=5){float s=p/100f;expected=131*s;selected=true;
    var bytes=BuildQueueUpdate((ulong)frame.ToInt64(),(ulong)panel.ToInt64(),0,(ulong)Marshal.GetFunctionPointerForDelegate(original).ToInt64(),(ulong)Marshal.GetFunctionPointerForDelegate(get).ToInt64(),(ulong)Marshal.GetFunctionPointerForDelegate(setter).ToInt64(),s,0);
    code=VirtualAllocEx(h,IntPtr.Zero,(UIntPtr)4096,0x3000,4);Marshal.Copy(bytes,0,code,bytes.Length);uint old;if(!VirtualProtectEx(h,code,(UIntPtr)4096,0x20,out old))throw Error("Queue test protection");FlushInstructionCache(h,code,(UIntPtr)4096);var fn=(Original)Marshal.GetDelegateForFunctionPointer(code,typeof(Original));
    int before=calls;for(int i=0;i<3;i++)if(fn(frame,22,33,44,55)!=12345)throw new Exception("Queue result");if(calls!=before+6)throw new Exception("Repeated queue sizing");
    selected=false;fn(frame,22,33,44,55);if(calls!=before+6)throw new Exception("Empty selection rescaled");
    Marshal.WriteInt64(frame,0x138,0);selected=true;fn(frame,22,33,44,55);if(calls!=before+6)throw new Exception("Queue identity guard");Marshal.WriteInt64(frame,0x138,panel.ToInt64());
    VirtualFreeEx(h,code,UIntPtr.Zero,0x8000);code=IntPtr.Zero;
   }
   var dispatcher=BuildTextDispatcher((ulong)Marshal.GetFunctionPointerForDelegate(setter).ToInt64(),0,0,0,0,0,0);
   code=VirtualAllocEx(h,IntPtr.Zero,(UIntPtr)4096,0x3000,4);Marshal.Copy(dispatcher,0,code,dispatcher.Length);uint prot;VirtualProtectEx(h,code,(UIntPtr)4096,0x20,out prot);FlushInstructionCache(h,code,(UIntPtr)4096);var dispatch=(Setter)Marshal.GetDelegateForFunctionPointer(code,typeof(Setter));
   dispatch(frame,5,frame,0x42000000,0,0);if(Marshal.ReadInt32(frame,0x1f0)!=0x42000000||Marshal.ReadInt32(frame,0x1e8)!=1)throw new Exception("Cargo size and rebuild");
   dispatch(frame,5,frame,0x42800000,0,0);if(Marshal.ReadInt32(frame,0x1f0)!=0x42800000)throw new Exception("Cargo restore");
   dispatch(frame,6,panel,0x3f800000,0,0);if(Marshal.ReadInt64(frame)!=panel.ToInt64())throw new Exception("Queue install");
   dispatch(frame,6,panel,0,0,0);long vt=Marshal.GetFunctionPointerForDelegate(setter).ToInt64()-(long)SC2Addresses.Rva(0x16bcfc0)+(long)SC2Addresses.Rva(0x2d99510);if(Marshal.ReadInt64(frame)!=vt)throw new Exception("Queue restore");
   GC.KeepAlive(original);GC.KeepAlive(get);GC.KeepAlive(setter);return "PASS: dynamic queue sizing at all 11 scales without compounding, empty selection and identity guards, argument/result forwarding, cargo resize/restore, queue install/restore.";
  }finally{if(code!=IntPtr.Zero)VirtualFreeEx(h,code,UIntPtr.Zero,0x8000);Marshal.FreeHGlobal(frame);Marshal.FreeHGlobal(panel);}
 }
 public static string CargoSelfTest(){
  var widths=Marshal.AllocHGlobal(48);IntPtr code=IntPtr.Zero;var h=GetCurrentProcess();var current=new float[3];float[] native={402,430,430};int originals=0;bool change=false;
  SizeGetter getter=delegate(IntPtr f){return current[f.ToInt32()-100];};
  SizeSetter setter=delegate(IntPtr f,float v){current[f.ToInt32()-100]=v;};
  Original original=delegate(IntPtr f,ulong a,ulong b,ulong c,ulong d){if(f!=(IntPtr)123||a!=22||b!=33||c!=44||d!=55)throw new Exception("Cargo arguments");for(int n=0;n<3;n++)if(Math.Abs(current[n]-native[n])>.001)throw new Exception("Cargo native baseline");if(change){native[0]=201;native[1]=215;native[2]=215;Array.Copy(native,current,3);change=false;}originals++;return 987;};
  try{
   for(int p=50;p<=100;p+=5){float scale=p/100f;native=new float[]{402,430,430};Array.Copy(native,current,3);for(int n=0;n<3;n++){Marshal.WriteInt64(widths,n*16,100+n);Marshal.WriteInt32(widths,n*16+8,BitConverter.ToInt32(BitConverter.GetBytes(native[n]),0));}
    var bytes=BuildCargoUpdate(123,(ulong)widths.ToInt64(),3,(ulong)Marshal.GetFunctionPointerForDelegate(original).ToInt64(),(ulong)Marshal.GetFunctionPointerForDelegate(getter).ToInt64(),(ulong)Marshal.GetFunctionPointerForDelegate(setter).ToInt64(),scale,0);
    code=VirtualAllocEx(h,IntPtr.Zero,(UIntPtr)4096,0x3000,4);Marshal.Copy(bytes,0,code,bytes.Length);uint old;VirtualProtectEx(h,code,(UIntPtr)4096,0x20,out old);FlushInstructionCache(h,code,(UIntPtr)4096);var fn=(Original)Marshal.GetDelegateForFunctionPointer(code,typeof(Original));
    for(int j=0;j<6;j++){change=j==3;if(fn((IntPtr)123,22,33,44,55)!=987)throw new Exception("Cargo return");for(int n=0;n<3;n++)if(Math.Abs(current[n]-native[n]*scale)>.001)throw new Exception("Cargo scale drift or state change");}
    VirtualFreeEx(h,code,UIntPtr.Zero,0x8000);code=IntPtr.Zero;
   }
   GC.KeepAlive(original);GC.KeepAlive(getter);GC.KeepAlive(setter);return "PASS: 66 cargo updates across all 11 scales, panel/label centering widths, state-dependent width changes, no accumulated scaling, full argument/result forwarding.";
  }finally{if(code!=IntPtr.Zero)VirtualFreeEx(h,code,UIntPtr.Zero,0x8000);Marshal.FreeHGlobal(widths);}
 }
 public static string ScaleSelfTest(){
  int count=0;for(int p=50;p<=100;p+=5){ValidateScale(3440,1440,p);float s=p/100f,inset=600f*3440/1440-s*1200f*8/9;float pixels=inset*1440/1200;if(Math.Abs((3440-2*pixels)-2560*s)>.01)throw new Exception("HUD width calculation");count++;}
  foreach(int p in new[]{49,51,105,125,126}){bool failed=false;try{ValidateScale(3440,1440,p);}catch{failed=true;}if(!failed)throw new Exception("Scale guard");}
  bool narrow=false;try{ValidateScale(1280,1024,100);}catch{narrow=true;}if(!narrow)throw new Exception("Narrow screen guard");return "PASS: all 11 HUD sizes, centered widths, and unsupported-size guards.";
 }
}
