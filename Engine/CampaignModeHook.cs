using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public class SC2CampaignModeHook {
 [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr OpenProcess(uint a,bool b,int pid);
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool ReadProcessMemory(IntPtr h,IntPtr a,byte[] b,UIntPtr n,out UIntPtr read);
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool WriteProcessMemory(IntPtr h,IntPtr a,byte[] b,UIntPtr n,out UIntPtr written);
 [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr VirtualAllocEx(IntPtr h,IntPtr a,UIntPtr n,uint flags,uint protection);
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool VirtualProtectEx(IntPtr h,IntPtr a,UIntPtr n,uint protection,out uint old);
 [DllImport("kernel32.dll")] static extern bool VirtualFreeEx(IntPtr h,IntPtr a,UIntPtr n,uint kind);
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool FlushInstructionCache(IntPtr h,IntPtr a,UIntPtr n);
 [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
 [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
 [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate int ResetDelegate(IntPtr device,ulong a2,ulong a3,ulong a4,ulong a5);
 public class Prepared { public ulong Code,State,VTable; public int Length; }
 static Exception Error(string s){return new Exception(s+"; Win32 error "+Marshal.GetLastWin32Error());}
 static void Bytes(List<byte> c,string hex){foreach(string s in hex.Split(' '))if(s.Length>0)c.Add(Convert.ToByte(s,16));}
 static int Jump(List<byte> c,string op){Bytes(c,op);int p=c.Count;c.AddRange(new byte[4]);return p;}
 static void Fix(List<byte> c,int p,int target){var d=BitConverter.GetBytes(target-p-4);for(int i=0;i<4;i++)c[p+i]=d[i];}
 public static void ValidateTarget(int width,int height){
  if(width<1280||width>7680||height<720||height>4320||width*9<height*16||width*9>height*32)throw new ArgumentException("Choose a widescreen resolution between 16:9 and 32:9.");
 }
 public static double HudInset(int width,int height){ValidateTarget(width,height);return Math.Max(0,600.0*width/height-1200.0*8/9);}
 public static byte[] BuildMode(ulong state,ulong original){return BuildMode(state,original,3440,1440);}
 public static byte[] BuildMode(ulong state,ulong original,int width,int height){
  return BuildMode(state,original,width,height,0,0);
 }
 public static byte[] BuildMode(ulong state,ulong original,int width,int height,ulong sessionGlobal,ulong map){return BuildMode(state,original,width,height,sessionGlobal,map,0);}
 public static byte[] BuildMode(ulong state,ulong original,int width,int height,ulong sessionGlobal,ulong map,ulong hubScene){
  ValidateTarget(width,height);
  var denied=new List<int>();
  var c=new List<byte>();var skips=new List<int>();Bytes(c,"49 BA");c.AddRange(BitConverter.GetBytes(state));
  Bytes(c,"F0 41 FF 42 20 41 83 3A 00");skips.Add(Jump(c,"0F 84"));
  Bytes(c,"48 85 D2");skips.Add(Jump(c,"0F 84"));
  Bytes(c,"44 8B 5A 20 45 89 5A 28 44 8B 5A 24 45 89 5A 2C 80 7A 10 00");skips.Add(Jump(c,"0F 85"));
  // Only ordinary widescreen fullscreen requests are rewritten. Leave small
  // fallback modes, portrait modes and non-display descriptors untouched.
  Bytes(c,"44 8B 5A 20 41 81 FB 00 05 00 00");skips.Add(Jump(c,"0F 82"));
  Bytes(c,"41 81 FB 00 1E 00 00");skips.Add(Jump(c,"0F 87"));
  Bytes(c,"8B 42 24 3D D0 02 00 00");skips.Add(Jump(c,"0F 82"));
  Bytes(c,"3D E0 10 00 00");skips.Add(Jump(c,"0F 87"));
  Bytes(c,"45 6B DB 09 C1 E0 04 41 39 C3");skips.Add(Jump(c,"0F 82"));
  Bytes(c,"D1 E0 41 39 C3");skips.Add(Jump(c,"0F 87"));
  if(sessionGlobal!=0){if(hubScene!=0)SC2DisplayGate.Emit(c,denied,sessionGlobal,map,hubScene);else SC2CampaignGate.Emit(c,denied,sessionGlobal,map);}
  // Keep the last unmodified fullscreen request for a later normal reset.
  Bytes(c,"81 7A 20");c.AddRange(BitConverter.GetBytes(width));int different=Jump(c,"0F 85");
  Bytes(c,"81 7A 24");c.AddRange(BitConverter.GetBytes(height));int same=Jump(c,"0F 84");
  Fix(c,different,c.Count);Bytes(c,"8B 42 20 41 89 42 34 8B 42 24 41 89 42 38");Fix(c,same,c.Count);
  Bytes(c,"41 C7 42 30 01 00 00 00");
  Bytes(c,"C7 42 20");c.AddRange(BitConverter.GetBytes(width));Bytes(c,"C7 42 24");c.AddRange(BitConverter.GetBytes(height));Bytes(c,"F0 41 FF 42 24");
  int applied=Jump(c,"E9");
  SC2CampaignGate.Resolve(c,denied,c.Count);
  // A rejected session never receives the target resolution. If a normal reset
  // carries our previous target, substitute the saved native fullscreen size.
  Bytes(c,"41 83 7A 30 01");int noApplied=Jump(c,"0F 85");
  Bytes(c,"81 7A 20");c.AddRange(BitConverter.GetBytes(width));int otherWidth=Jump(c,"0F 85");
  Bytes(c,"81 7A 24");c.AddRange(BitConverter.GetBytes(height));int otherHeight=Jump(c,"0F 85");
  Bytes(c,"41 83 7A 34 00");int noBaseline=Jump(c,"0F 84");
  Bytes(c,"41 8B 42 34 89 42 20 41 8B 42 38 89 42 24 41 C7 42 30 00 00 00 00");
  int invoke=c.Count;Fix(c,applied,invoke);Fix(c,noApplied,invoke);Fix(c,otherWidth,invoke);Fix(c,otherHeight,invoke);Fix(c,noBaseline,invoke);Bytes(c,"48 B8");c.AddRange(BitConverter.GetBytes(original));Bytes(c,"FF E0");
  foreach(int p in skips)Fix(c,p,invoke);return c.ToArray();
 }
 static byte[] Read(IntPtr h,ulong a,int length){var b=new byte[length];UIntPtr n;if(!ReadProcessMemory(h,(IntPtr)(long)a,b,(UIntPtr)length,out n)||n.ToUInt64()!=(ulong)length)throw Error("Read failed");return b;}
 static void Write(IntPtr h,ulong a,byte[] b){UIntPtr n;if(!WriteProcessMemory(h,(IntPtr)(long)a,b,(UIntPtr)b.Length,out n)||n.ToUInt64()!=(ulong)b.Length)throw Error("Write failed");}
 public static Prepared Prepare(int pid,ulong originalVtable){return Prepare(pid,originalVtable,3440,1440);}
 public static Prepared Prepare(int pid,ulong originalVtable,int width,int height){
  ValidateTarget(width,height);
  ulong module=originalVtable-0x2DB8098;SC2DisplayGate.Require(pid,module);
  var h=OpenProcess(0x438,false,pid);if(h==IntPtr.Zero)throw Error("OpenProcess failed");IntPtr data=IntPtr.Zero,code=IntPtr.Zero;bool ready=false;
  try{
   // Preserve the RTTI prefix and all 51 methods; the table ends at offset 0x198.
   var table=Read(h,originalVtable-8,0x1A0);ulong original=BitConverter.ToUInt64(table,0x30);
   data=VirtualAllocEx(h,IntPtr.Zero,(UIntPtr)4096,0x3000,4);if(data==IntPtr.Zero)throw Error("State allocation failed");
   code=VirtualAllocEx(h,IntPtr.Zero,(UIntPtr)4096,0x3000,4);if(code==IntPtr.Zero)throw Error("Code allocation failed");
   ulong state=(ulong)data.ToInt64(),entry=(ulong)code.ToInt64();var modeBytes=BuildMode(state,original,width,height,module+SC2CampaignGate.SessionRva,module+SC2CampaignGate.MapRva,module+SC2HubGate.SceneRva);
   Write(h,state,BitConverter.GetBytes(1));Write(h,entry,modeBytes);
   Array.Copy(BitConverter.GetBytes(entry),0,table,0x30,8);Write(h,state+0x100,table);
   uint old;if(!VirtualProtectEx(h,code,(UIntPtr)4096,0x20,out old))throw Error("Executable protection failed");
   if(!FlushInstructionCache(h,code,(UIntPtr)modeBytes.Length))throw Error("Cache flush failed");
   var check=Read(h,entry,modeBytes.Length);for(int i=0;i<modeBytes.Length;i++)if(check[i]!=modeBytes[i])throw new Exception("Code verification failed");
   check=Read(h,state+0x100,table.Length);for(int i=0;i<table.Length;i++)if(check[i]!=table[i])throw new Exception("Vtable verification failed");
   ready=true;return new Prepared{Code=entry,State=state,VTable=state+0x108,Length=modeBytes.Length};
  }finally{if(!ready){if(code!=IntPtr.Zero)VirtualFreeEx(h,code,UIntPtr.Zero,0x8000);if(data!=IntPtr.Zero)VirtualFreeEx(h,data,UIntPtr.Zero,0x8000);}CloseHandle(h);}
 }
 public static string ModeSelfTest(){
  int[,] targets={{1920,1080},{2560,1440},{3440,1440},{2560,1080},{3840,1600},{5120,1440},{5120,2160},{7680,2160},{1366,768}};
  int checks=0;
  for(int t=0;t<targets.GetLength(0);t++){
   int width=targets[t,0],height=targets[t,1];
   var state=Marshal.AllocHGlobal(64);var desc=Marshal.AllocHGlobal(64);IntPtr code=IntPtr.Zero;int observedWidth=0,observedHeight=0,calls=0;var h=GetCurrentProcess();
   ResetDelegate fake=delegate(IntPtr dev,ulong address,ulong a3,ulong a4,ulong a5){if(dev!=(IntPtr)123||a3!=22||a4!=33||a5!=44)throw new Exception("Mode argument forwarding failed");observedWidth=address==0?-1:Marshal.ReadInt32((IntPtr)(long)address,32);observedHeight=address==0?-1:Marshal.ReadInt32((IntPtr)(long)address,36);calls++;return 0x20060000;};
   try{
    Marshal.Copy(new byte[64],0,state,64);var bytes=BuildMode((ulong)state.ToInt64(),(ulong)Marshal.GetFunctionPointerForDelegate(fake).ToInt64(),width,height);
    code=VirtualAllocEx(h,IntPtr.Zero,(UIntPtr)4096,0x3000,4);if(code==IntPtr.Zero)throw Error("Local allocation failed");Marshal.Copy(bytes,0,code,bytes.Length);
    uint old;if(!VirtualProtectEx(h,code,(UIntPtr)4096,0x20,out old))throw Error("Local protection failed");FlushInstructionCache(h,code,(UIntPtr)bytes.Length);
    var wrapper=(ResetDelegate)Marshal.GetDelegateForFunctionPointer(code,typeof(ResetDelegate));
    int[,] cases={{2560,1440,0,1,1},{1920,1080,0,1,1},{width,height,0,1,1},{3440,1440,0,1,1},{3440,1200,0,1,1},{3024,1296,0,1,1},{1920,820,0,1,1},{2560,1440,1,1,0},{2560,1440,0,0,0},{1280,1440,0,1,0},{640,480,0,1,0},{0,0,0,1,0}};
    for(int i=0;i<cases.GetLength(0);i++){
     var initial=new byte[64];for(int j=0;j<64;j++)initial[j]=0xAB;Marshal.Copy(initial,0,desc,64);
     Marshal.WriteInt32(state,cases[i,3]);Marshal.WriteByte(desc,16,(byte)cases[i,2]);Marshal.WriteInt32(desc,32,cases[i,0]);Marshal.WriteInt32(desc,36,cases[i,1]);
     bool absent=i==cases.GetLength(0)-1,changed=cases[i,4]==1;
     int result=wrapper((IntPtr)123,absent?0:(ulong)desc.ToInt64(),22,33,44);
     if(result!=0x20060000||observedWidth!=(absent?-1:changed?width:cases[i,0])||observedHeight!=(absent?-1:changed?height:cases[i,1]))throw new Exception("Resolution output/forwarding failed at target "+t+", case "+i);
     for(int j=0;j<64;j++)if(j!=16&&(j<32||j>=40)&&Marshal.ReadByte(desc,j)!=0xAB)throw new Exception("Unrelated descriptor field changed");
     if(Marshal.ReadByte(desc,16)!=cases[i,2])throw new Exception("Windowed flag changed");checks++;
    }
    if(calls!=12||Marshal.ReadInt32(state,32)!=12||Marshal.ReadInt32(state,36)!=7)throw new Exception("Mode counters failed");
    double inset=HudInset(width,height),physicalMargin=inset*height/1200;
    if(Math.Abs(width-2*physicalMargin-height*16.0/9)>0.001)throw new Exception("HUD centering math failed");
    GC.KeepAlive(wrapper);GC.KeepAlive(fake);
   }finally{if(code!=IntPtr.Zero)VirtualFreeEx(h,code,UIntPtr.Zero,0x8000);Marshal.FreeHGlobal(desc);Marshal.FreeHGlobal(state);}
  }
  if(Math.Abs(HudInset(3440,1440)-366.6666667)>0.001||HudInset(1920,1080)!=0)throw new Exception("HUD baseline regression");
  int[,] invalid={{0,1440},{1024,768},{7681,2160},{2560,0},{1920,1200},{7680,720}};
  for(int i=0;i<invalid.GetLength(0);i++){bool rejected=false;try{ValidateTarget(invalid[i,0],invalid[i,1]);}catch(ArgumentException){rejected=true;}if(!rejected)throw new Exception("Invalid dimensions accepted");}
  return "PASS: "+checks+" native resolution tests across nine targets; HUD spacing and invalid dimension checks passed.";
 }
 public static void SwapPointer(int pid,ulong slot,ulong expected,ulong replacement){
  if((slot&7)!=0)throw new Exception("Unaligned function pointer");var h=OpenProcess(0x438,false,pid);if(h==IntPtr.Zero)throw Error("OpenProcess failed");try{if(BitConverter.ToUInt64(Read(h,slot,8),0)!=expected)throw new Exception("Graphics pointer changed; no write");Write(h,slot,BitConverter.GetBytes(replacement));if(BitConverter.ToUInt64(Read(h,slot,8),0)!=replacement)throw new Exception("Pointer verification failed");}finally{CloseHandle(h);}
 }
 public static void Disable(int pid,ulong state){var h=OpenProcess(0x438,false,pid);if(h==IntPtr.Zero)throw Error("OpenProcess failed");try{Write(h,state,BitConverter.GetBytes(0));}finally{CloseHandle(h);}}
}
