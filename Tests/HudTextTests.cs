using System;
using System.Runtime.InteropServices;
public partial class SC2HudScale {
 [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate float TextMeasure(IntPtr frame,int side);
 public static string DynamicTextSelfTest(){
  IntPtr code=IntPtr.Zero;var h=GetCurrentProcess();float w=0,hgt=0,bw=0,bh=0,current=1;int layouts=0;bool fit=false;
  TextMeasure measure=delegate(IntPtr f,int side){if(f!=(IntPtr)123||side!=3)throw new Exception("Text measurement arguments");return 24;};
  SizeGetter getW=delegate(IntPtr f){return w;};SizeGetter getH=delegate(IntPtr f){return hgt;};
  SizeSetter setW=delegate(IntPtr f,float v){bw=v;};SizeSetter setH=delegate(IntPtr f,float v){bh=v;};
  ScaleSetter setFit=delegate(IntPtr f,byte v){fit=v==1;};
  LayoutSetter layout=delegate(IntPtr f){if(f!=(IntPtr)123||!fit)throw new Exception("Text layout arguments");if(w>0&&(Math.Abs(bw-160)>.001||Math.Abs(bh-24)>.001||Math.Abs(w/bw-current)>.001))throw new Exception("Text bounds compound");layouts++;};
  try{
   foreach(int pct in new[]{75,65,50,100,95,65,75,50,100}){
    current=pct/100f;
    var m=BuildTextMeasure((ulong)Marshal.GetFunctionPointerForDelegate(measure).ToInt64(),current);
    var l=BuildTextLayout((ulong)Marshal.GetFunctionPointerForDelegate(getW).ToInt64(),(ulong)Marshal.GetFunctionPointerForDelegate(getH).ToInt64(),(ulong)Marshal.GetFunctionPointerForDelegate(setW).ToInt64(),(ulong)Marshal.GetFunctionPointerForDelegate(setH).ToInt64(),(ulong)Marshal.GetFunctionPointerForDelegate(setFit).ToInt64(),(ulong)Marshal.GetFunctionPointerForDelegate(layout).ToInt64(),current);
    code=VirtualAllocEx(h,IntPtr.Zero,(UIntPtr)4096,0x3000,4);Marshal.Copy(m,0,code,m.Length);Marshal.Copy(l,0,IntPtr.Add(code,1024),l.Length);uint old;VirtualProtectEx(h,code,(UIntPtr)4096,0x20,out old);FlushInstructionCache(h,code,(UIntPtr)4096);
    var mf=(TextMeasure)Marshal.GetDelegateForFunctionPointer(code,typeof(TextMeasure));var lf=(LayoutSetter)Marshal.GetDelegateForFunctionPointer(IntPtr.Add(code,1024),typeof(LayoutSetter));
    if(Math.Abs(mf((IntPtr)123,3)-24*current)>.001)throw new Exception("Automatic text measurement scale");
    w=hgt=0;lf((IntPtr)123);if(bw!=0||bh!=0)throw new Exception("Hidden text bounds");
    w=160*current;hgt=24*current;for(int i=0;i<10;i++)lf((IntPtr)123);
    VirtualFreeEx(h,code,UIntPtr.Zero,0x8000);code=IntPtr.Zero;
   }
   GC.KeepAlive(measure);GC.KeepAlive(getW);GC.KeepAlive(getH);GC.KeepAlive(setW);GC.KeepAlive(setH);GC.KeepAlive(setFit);GC.KeepAlive(layout);
   return "PASS: 99 dynamic text layouts across repeated scale changes, hidden-to-visible labels, natural measurements, and stable clipping bounds.";
  }finally{if(code!=IntPtr.Zero)VirtualFreeEx(h,code,UIntPtr.Zero,0x8000);}
 }
}

public partial class SC2HudScale {
 public static string TextGateSelfTest(){
  var h=GetCurrentProcess();IntPtr module=VirtualAllocEx(h,IntPtr.Zero,(UIntPtr)0x6000000,0x3000,4),code=IntPtr.Zero;
  int changes=0,originals=0;
  TextMeasure measure=delegate(IntPtr f,int side){if(f!=(IntPtr)123||side!=3)throw new Exception("Gated measure forwarding");originals++;return 24;};
  SizeGetter getter=delegate(IntPtr f){changes++;return 12;};SizeSetter setter=delegate(IntPtr f,float v){changes++;};ScaleSetter fit=delegate(IntPtr f,byte b){changes++;};LayoutSetter layout=delegate(IntPtr f){if(f!=(IntPtr)123)throw new Exception("Gated layout forwarding");originals++;};
  try{
   if(module==IntPtr.Zero)throw Error("Mock allocation");
   var session=IntPtr.Add(module,0x1000);var info=IntPtr.Add(module,0x2000);var map=IntPtr.Add(module,(int)SC2CampaignGate.MapRva);var global=IntPtr.Add(module,(int)SC2CampaignGate.SessionRva);
   Marshal.WriteInt64(global,session.ToInt64());Marshal.WriteInt64(session,0x60,info.ToInt64());
   var m=BuildTextMeasure((ulong)Marshal.GetFunctionPointerForDelegate(measure).ToInt64(),.5f,(ulong)module.ToInt64());
   var l=BuildTextLayout((ulong)Marshal.GetFunctionPointerForDelegate(getter).ToInt64(),(ulong)Marshal.GetFunctionPointerForDelegate(getter).ToInt64(),(ulong)Marshal.GetFunctionPointerForDelegate(setter).ToInt64(),(ulong)Marshal.GetFunctionPointerForDelegate(setter).ToInt64(),(ulong)Marshal.GetFunctionPointerForDelegate(fit).ToInt64(),(ulong)Marshal.GetFunctionPointerForDelegate(layout).ToInt64(),.5f,(ulong)module.ToInt64());
   if(m.Length>0x400||l.Length>0xC00)throw new Exception("Text code capacity");
   code=VirtualAllocEx(h,IntPtr.Zero,(UIntPtr)4096,0x3000,4);Marshal.Copy(m,0,code,m.Length);Marshal.Copy(l,0,IntPtr.Add(code,1024),l.Length);uint old;VirtualProtectEx(h,code,(UIntPtr)4096,0x20,out old);FlushInstructionCache(h,code,(UIntPtr)4096);
   var mf=(TextMeasure)Marshal.GetDelegateForFunctionPointer(code,typeof(TextMeasure));var lf=(LayoutSetter)Marshal.GetDelegateForFunctionPointer(IntPtr.Add(code,1024),typeof(LayoutSetter));
   string[] paths={"Campaign/Test.SC2Map","MAPS\\CAMPAIGN\\Swarm\\Test.SC2Map","","Maps/Multiplayer/Test.SC2Map","Campaign/Test.SC2Map",new string('x',260),"Campaign/Test.SC2Map","Campaign/Test.SC2Map"};
   for(int i=0;i<paths.Length;i++){
    Marshal.Copy(new byte[260],0,map,260);var bytes=System.Text.Encoding.ASCII.GetBytes(paths[i]);Marshal.Copy(bytes,0,map,bytes.Length);
    Marshal.WriteInt32(info,0x1ec8,i==4?2:0);Marshal.WriteInt64(global,i==6?0:session.ToInt64());Marshal.WriteInt64(session,0x60,i==7?0:info.ToInt64());
    int before=changes;bool allowed=i<2;
    if(mf((IntPtr)123,3)!=(allowed?12:24))throw new Exception("Text measure campaign gate "+i);lf((IntPtr)123);
    if(changes-before!=(allowed?5:0))throw new Exception("Text layout campaign gate "+i);
   }
   if(originals!=16)throw new Exception("Text original callbacks skipped");
   GC.KeepAlive(measure);GC.KeepAlive(getter);GC.KeepAlive(setter);GC.KeepAlive(fit);GC.KeepAlive(layout);
   return "PASS: text callbacks preserve native behavior for online, menu, non-campaign, null, and unterminated states; local mock memory only.";
  }finally{if(code!=IntPtr.Zero)VirtualFreeEx(h,code,UIntPtr.Zero,0x8000);if(module!=IntPtr.Zero)VirtualFreeEx(h,module,UIntPtr.Zero,0x8000);}
 }
 public static string TextRestoreSelfTest(){
  var frame=Marshal.AllocHGlobal(0x220);var text=Marshal.AllocHGlobal(0xC0);var h=GetCurrentProcess();IntPtr code=IntPtr.Zero;int invalidations=0;bool fit=true;
  ScaleSetter setFit=delegate(IntPtr f,byte b){if(f!=frame)throw new Exception("Restore text target");fit=b!=0;};
  ScaleSetter invalidate=delegate(IntPtr f,byte flags){if(f!=frame||flags!=15)throw new Exception("Text invalidation target");invalidations++;};
  Setter anchor=delegate(IntPtr f,int side,IntPtr parent,uint position,float offset,byte flag){return 1;};
  try{
   Marshal.Copy(new byte[0x220],0,frame,0x220);Marshal.Copy(new byte[0xC0],0,text,0xC0);Marshal.WriteInt64(frame,0x1d8,text.ToInt64());
   ulong anchorAddress=(ulong)Marshal.GetFunctionPointerForDelegate(anchor).ToInt64();
   var b=BuildTextDispatcher(anchorAddress,0,0,0,0,(ulong)Marshal.GetFunctionPointerForDelegate(setFit).ToInt64(),0,(ulong)Marshal.GetFunctionPointerForDelegate(invalidate).ToInt64());
   if(b.Length>0x400)throw new Exception("Text dispatcher capacity");
   code=VirtualAllocEx(h,IntPtr.Zero,(UIntPtr)4096,0x3000,4);Marshal.Copy(b,0,code,b.Length);uint old;VirtualProtectEx(h,code,(UIntPtr)4096,0x20,out old);FlushInstructionCache(h,code,(UIntPtr)4096);var fn=(Setter)Marshal.GetDelegateForFunctionPointer(code,typeof(Setter));
   for(int i=0;i<16;i++){
    fn(frame,8,(IntPtr)456,0x3f800000,.5f,0);if(Marshal.ReadInt64(frame)!=456)throw new Exception("Label callback install");
    foreach(int off in new[]{0x38,0x3c,0x48,0x4c})Marshal.WriteInt32(text,off,0x3f000000);Marshal.WriteInt32(text,0x20,0x15);fit=true;
    fn(frame,8,(IntPtr)456,0,0,0);
    if((ulong)Marshal.ReadInt64(frame)!=anchorAddress-0x16bcfc0+0x2dccec8||fit||Marshal.ReadInt64(text,0x38)!=0||Marshal.ReadInt32(text,0x48)!=0x3f800000||Marshal.ReadInt32(text,0x4c)!=0x3f800000||Marshal.ReadInt32(text,0x20)!=0x10)throw new Exception("Text baseline restoration");
   }
   Marshal.WriteInt64(frame,0x1d8,0);fn(frame,8,(IntPtr)456,0,0,0);
   if(invalidations!=33)throw new Exception("Label invalidation missing");GC.KeepAlive(setFit);GC.KeepAlive(invalidate);GC.KeepAlive(anchor);
   return "PASS: 16 label install/restore cycles reset clipping caches and text scale, preserve unrelated flags, and handle a missing text object.";
  }finally{if(code!=IntPtr.Zero)VirtualFreeEx(h,code,UIntPtr.Zero,0x8000);Marshal.FreeHGlobal(frame);Marshal.FreeHGlobal(text);}
 }
}
