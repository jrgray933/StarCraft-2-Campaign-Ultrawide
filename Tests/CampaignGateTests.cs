using System;
using System.Runtime.InteropServices;
using System.Text;
public static class CampaignGateTests {
 [DllImport("kernel32.dll")]static extern IntPtr VirtualAlloc(IntPtr p,UIntPtr n,uint a,uint protect);
 [DllImport("kernel32.dll")]static extern bool VirtualProtect(IntPtr p,UIntPtr n,uint protect,out uint old);
 [DllImport("kernel32.dll")]static extern bool VirtualFree(IntPtr p,UIntPtr n,uint flags);
 [DllImport("kernel32.dll")]static extern bool FlushInstructionCache(IntPtr h,IntPtr p,UIntPtr n);
 [DllImport("kernel32.dll")]static extern IntPtr GetCurrentProcess();
 [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate int Native(IntPtr owner,ulong a2,ulong a3,ulong a4,ulong a5);
 [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate byte Setter(IntPtr f,int s,IntPtr p,uint pos,float off,byte flag);
 static IntPtr At(IntPtr p,int offset){return IntPtr.Add(p,offset);}
 static void PutPath(IntPtr p,string text){Marshal.Copy(new byte[260],0,p,260);byte[] b=Encoding.ASCII.GetBytes(text);Marshal.Copy(b,0,p,Math.Min(260,b.Length));}
 static IntPtr Code(byte[] b){if(b.Length>4096)throw new Exception("Code too large");IntPtr p=VirtualAlloc(IntPtr.Zero,(UIntPtr)4096,0x3000,4);if(p==IntPtr.Zero)throw new Exception("Allocation failed");Marshal.Copy(b,0,p,b.Length);uint old;if(!VirtualProtect(p,(UIntPtr)4096,0x20,out old))throw new Exception("Protect failed");FlushInstructionCache(GetCurrentProcess(),p,(UIntPtr)b.Length);return p;}
 static void Assert(bool x,string text){if(!x)throw new Exception(text);}
 public static string Run(){
  IntPtr module=VirtualAlloc(IntPtr.Zero,(UIntPtr)0x6000000,0x3000,4),state=Marshal.AllocHGlobal(1024),desc=Marshal.AllocHGlobal(64),modeCode=IntPtr.Zero,hudCode=IntPtr.Zero;
  if(module==IntPtr.Zero)throw new Exception("Mock module allocation failed");
  int calls=0,setters=0;Native original=delegate(IntPtr owner,ulong a2,ulong a3,ulong a4,ulong a5){Assert(owner==(IntPtr)123&&a3==33&&a4==44&&a5==55,"Arguments changed");calls++;return 12345;};
  Setter setter=delegate(IntPtr f,int side,IntPtr parent,uint pos,float off,byte flag){Assert(f==(IntPtr)111&&parent==(IntPtr)222&&side==1,"HUD arguments changed");setters++;return 1;};
  IntPtr session=At(module,0x1000),info=At(module,0x2000),map=At(module,(int)SC2CampaignGate.MapRva),global=At(module,(int)SC2CampaignGate.SessionRva);
  try{
   Marshal.WriteInt64(global,session.ToInt64());Marshal.WriteInt64(session,0x60,info.ToInt64());
   modeCode=Code(SC2CampaignModeHook.BuildMode((ulong)state.ToInt64(),(ulong)Marshal.GetFunctionPointerForDelegate(original).ToInt64(),3440,1440,(ulong)global.ToInt64(),(ulong)map.ToInt64()));
   hudCode=Code(SC2HudScale.Build((ulong)state.ToInt64(),(ulong)Marshal.GetFunctionPointerForDelegate(original).ToInt64(),(ulong)module.ToInt64()));
   Native mode=(Native)Marshal.GetDelegateForFunctionPointer(modeCode,typeof(Native)),hud=(Native)Marshal.GetDelegateForFunctionPointer(hudCode,typeof(Native));
   string[] paths={"Campaign/TTychus01.SC2Map","Campaign/Swarm/ZChar02.SC2Map","MAPS\\CAMPAIGN\\VOID\\x.SC2Map","E:/Game/Maps/Campaign/Nova/x.SC2Map","","Maps/Multiplayer/Test.SC2Map","Mods/Campaign/x.SC2Map","Maps/Campaign/x.SC2Map",new string('x',260),new string('x',246)+"maps/campaign/"};
   for(int i=0;i<paths.Length;i++){
    PutPath(map,paths[i]);int flags=i==7?2:0;Marshal.WriteInt32(info,0x1ec8,flags);bool expected=i<4;
    Assert(SC2CampaignGate.Matches(flags!=0,paths[i])==(expected||i==9),"Managed predicate mismatch");
    Marshal.Copy(new byte[1024],0,state,1024);Marshal.Copy(new byte[64],0,desc,64);Marshal.WriteInt32(state,1);Marshal.WriteInt32(desc,32,2560);Marshal.WriteInt32(desc,36,1440);
    Assert(mode((IntPtr)123,(ulong)desc.ToInt64(),33,44,55)==12345,"Mode return changed");Assert(Marshal.ReadInt32(desc,32)==(expected?3440:2560),"Native mode gate failed case "+i);
    Marshal.Copy(new byte[1024],0,state,1024);Marshal.WriteInt64(state,24,123);Marshal.WriteInt64(state,40,Marshal.GetFunctionPointerForDelegate(setter).ToInt64());Marshal.WriteInt32(state,48,1);Marshal.WriteInt64(state,64,111);Marshal.WriteInt64(state,72,222);Marshal.WriteInt32(state,80,1);Marshal.WriteInt32(state,0,1);
    int before=setters;Assert(hud((IntPtr)123,22,33,44,55)==12345,"HUD return changed");Assert(setters-before==(expected?1:0),"Native HUD gate failed case "+i);Assert(Marshal.ReadInt32(state,0)==0&&Marshal.ReadInt32(state,4)==0,"HUD request not retired");
    Marshal.WriteInt32(state,0,2);before=setters;hud((IntPtr)123,22,33,44,55);Assert(setters==before+1,"Blocked restoration");
   }
   // One real callback sequence: allowed -> session becomes online -> restore
   // the saved native resolution on the next fullscreen reset.
   PutPath(map,"Campaign/test.SC2Map");Marshal.WriteInt32(info,0x1ec8,0);Marshal.Copy(new byte[1024],0,state,1024);Marshal.WriteInt32(state,1);Marshal.WriteInt32(desc,32,2560);Marshal.WriteInt32(desc,36,1440);
   mode((IntPtr)123,(ulong)desc.ToInt64(),33,44,55);Assert(Marshal.ReadInt32(desc,32)==3440,"Initial apply failed");Marshal.WriteInt32(info,0x1ec8,2);mode((IntPtr)123,(ulong)desc.ToInt64(),33,44,55);Assert(Marshal.ReadInt32(desc,32)==2560,"Native baseline restore failed");
   // Both absent session pointers must deny without dereferencing zero.
   for(int i=0;i<2;i++){
    Marshal.WriteInt64(global,i==0?0:session.ToInt64());Marshal.WriteInt64(session,0x60,0);Marshal.WriteInt32(desc,32,2560);mode((IntPtr)123,(ulong)desc.ToInt64(),33,44,55);Assert(Marshal.ReadInt32(desc,32)==2560,"Null state allowed");
   }
   GC.KeepAlive(mode);GC.KeepAlive(hud);GC.KeepAlive(original);GC.KeepAlive(setter);
   return "PASS: 34 native callback checks cover relative WoL/HotS and absolute paths, case/slashes, online/menu/non-campaign/unterminated paths, null pointers, queued HUD rejection, restoration, and allowed-to-online transition. All use local mock memory.";
  }finally{if(modeCode!=IntPtr.Zero)VirtualFree(modeCode,UIntPtr.Zero,0x8000);if(hudCode!=IntPtr.Zero)VirtualFree(hudCode,UIntPtr.Zero,0x8000);VirtualFree(module,UIntPtr.Zero,0x8000);Marshal.FreeHGlobal(state);Marshal.FreeHGlobal(desc);}
 }
 [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate IntPtr Getter();
 public static string RunQueue(){
  IntPtr module=VirtualAlloc(IntPtr.Zero,(UIntPtr)0x6000000,0x3000,4),frame=Marshal.AllocHGlobal(0x300),panel=Marshal.AllocHGlobal(0x200),code=IntPtr.Zero;
  int calls=0,setters=0;Native original=delegate(IntPtr f,ulong a,ulong b,ulong c,ulong d){Assert(f==frame&&a==22&&b==33&&c==44&&d==55,"Queue forwarding");Marshal.WriteInt32(panel,0x84,unchecked((int)0xc3030000));Marshal.WriteInt32(panel,0xa4,0x43030000);calls++;return 123;};
  Setter setter=delegate(IntPtr f,int side,IntPtr p,uint pos,float off,byte flag){Assert(f==panel&&p==frame&&Math.Abs(off)==65.5f,"Queue scaling");setters++;return 1;};Getter getter=delegate(){return (IntPtr)1;};
  IntPtr session=At(module,0x1000),info=At(module,0x2000),map=At(module,(int)SC2CampaignGate.MapRva),global=At(module,(int)SC2CampaignGate.SessionRva);
  try{
   Marshal.Copy(new byte[0x300],0,frame,0x300);Marshal.Copy(new byte[0x200],0,panel,0x200);Marshal.WriteInt64(frame,0x138,panel.ToInt64());Marshal.WriteInt64(global,session.ToInt64());Marshal.WriteInt64(session,0x60,info.ToInt64());
   var bytes=SC2HudScale.BuildQueueUpdate((ulong)frame.ToInt64(),(ulong)panel.ToInt64(),0,(ulong)Marshal.GetFunctionPointerForDelegate(original).ToInt64(),(ulong)Marshal.GetFunctionPointerForDelegate(getter).ToInt64(),(ulong)Marshal.GetFunctionPointerForDelegate(setter).ToInt64(),.5f,(ulong)module.ToInt64());Assert(bytes.Length<=1024,"Queue code slot capacity");code=Code(bytes);var fn=(Native)Marshal.GetDelegateForFunctionPointer(code,typeof(Native));
   string[] paths={"Campaign/TTychus01.SC2Map","Campaign/Swarm/ZChar02.SC2Map","MAPS\\CAMPAIGN\\VOID\\x.SC2Map","E:/Game/Maps/Campaign/Nova/x.SC2Map","","Maps/Multiplayer/Test.SC2Map","Mods/Campaign/x.SC2Map","Maps/Campaign/x.SC2Map",new string('x',260),new string('x',246)+"maps/campaign/"};
   for(int i=0;i<paths.Length;i++){PutPath(map,paths[i]);Marshal.WriteInt32(info,0x1ec8,i==7?2:0);int before=setters;Assert(fn(frame,22,33,44,55)==123,"Queue return");Assert(setters-before==(i<4?2:0),"Queue campaign gate case "+i);}
   PutPath(map,"Campaign/test.SC2Map");Marshal.WriteInt32(info,0x1ec8,0);
   for(int i=0;i<2;i++){Marshal.WriteInt64(global,i==0?0:session.ToInt64());Marshal.WriteInt64(session,0x60,0);int before=setters;fn(frame,22,33,44,55);Assert(setters==before,"Queue null state guard");}
   Assert(calls==12,"Original update omitted");GC.KeepAlive(original);GC.KeepAlive(setter);GC.KeepAlive(getter);return "PASS: 12 dynamic queue native campaign-gate cases, original update preserved; local mock memory only.";
  }finally{if(code!=IntPtr.Zero)VirtualFree(code,UIntPtr.Zero,0x8000);VirtualFree(module,UIntPtr.Zero,0x8000);Marshal.FreeHGlobal(frame);Marshal.FreeHGlobal(panel);}
 }
 [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate float WidthGetter(IntPtr frame);
 [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate void WidthSetter(IntPtr frame,float size);
 public static string RunCargo(){
  IntPtr module=VirtualAlloc(IntPtr.Zero,(UIntPtr)0x6000000,0x3000,4),widths=Marshal.AllocHGlobal(48),code=IntPtr.Zero;
  IntPtr session=At(module,0x1000),info=At(module,0x2000),map=At(module,(int)SC2CampaignGate.MapRva),global=At(module,(int)SC2CampaignGate.SessionRva);
  int calls=0,setters=0;bool switchOnline=false;float value=402;
  Native original=delegate(IntPtr f,ulong a,ulong b,ulong c,ulong d){Assert(f==(IntPtr)123&&a==22&&b==33&&c==44&&d==55,"Cargo forwarding");value=402;calls++;if(switchOnline)Marshal.WriteInt32(info,0x1ec8,2);return 123;};
  WidthSetter setter=delegate(IntPtr f,float w){Assert(f==(IntPtr)456,"Cargo target");value=w;setters++;};WidthGetter getter=delegate(IntPtr f){return value;};
  try{
   Marshal.Copy(new byte[48],0,widths,48);Marshal.WriteInt64(widths,456);Marshal.WriteInt32(widths,8,BitConverter.ToInt32(BitConverter.GetBytes(402f),0));Marshal.WriteInt64(global,session.ToInt64());Marshal.WriteInt64(session,0x60,info.ToInt64());
   var bytes=SC2HudScale.BuildCargoUpdate(123,(ulong)widths.ToInt64(),1,(ulong)Marshal.GetFunctionPointerForDelegate(original).ToInt64(),(ulong)Marshal.GetFunctionPointerForDelegate(getter).ToInt64(),(ulong)Marshal.GetFunctionPointerForDelegate(setter).ToInt64(),.5f,(ulong)module.ToInt64());Assert(bytes.Length<=4096,"Cargo code capacity");code=Code(bytes);var fn=(Native)Marshal.GetDelegateForFunctionPointer(code,typeof(Native));
   string[] paths={"Campaign/TTychus01.SC2Map","Campaign/Swarm/ZChar02.SC2Map","MAPS\\CAMPAIGN\\VOID\\x.SC2Map","E:/Game/Maps/Campaign/Nova/x.SC2Map","","Maps/Multiplayer/Test.SC2Map","Mods/Campaign/x.SC2Map","Maps/Campaign/x.SC2Map",new string('x',260),new string('x',246)+"maps/campaign/"};
   for(int i=0;i<paths.Length;i++){PutPath(map,paths[i]);Marshal.WriteInt32(info,0x1ec8,i==7?2:0);int before=setters;Assert(fn((IntPtr)123,22,33,44,55)==123,"Cargo return");Assert(setters-before==(i<4?2:0),"Cargo campaign gate case "+i);Assert(value==(i<4?201:402),"Cargo gate final width");}
   PutPath(map,"Campaign/test.SC2Map");Marshal.WriteInt32(info,0x1ec8,0);switchOnline=true;int changed=setters;fn((IntPtr)123,22,33,44,55);Assert(setters-changed==1&&value==402,"Cargo gate change within update");switchOnline=false;Marshal.WriteInt32(info,0x1ec8,0);
   for(int i=0;i<2;i++){Marshal.WriteInt64(global,i==0?0:session.ToInt64());Marshal.WriteInt64(session,0x60,0);int before=setters;fn((IntPtr)123,22,33,44,55);Assert(setters==before,"Cargo null state guard");}
   Assert(calls==13,"Original cargo update omitted");GC.KeepAlive(original);GC.KeepAlive(setter);GC.KeepAlive(getter);return "PASS: 13 cargo native campaign-gate cases including eligibility lost within update; original behavior preserved; local mock memory only.";
  }finally{if(code!=IntPtr.Zero)VirtualFree(code,UIntPtr.Zero,0x8000);VirtualFree(module,UIntPtr.Zero,0x8000);Marshal.FreeHGlobal(widths);}
 }
}

