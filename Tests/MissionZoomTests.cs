using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Runtime.InteropServices;

public static class MissionZoomTests {
 static void Need(bool value,string message){if(!value)throw new Exception("Mission zoom: "+message);}
 static bool Equal(byte[] a,byte[] b){if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
 sealed class Memory {
  public const ulong Module=0x10000000,Camera=0x20010000,Data=0x20020000,Presets=0x20030000,Render=0x20040000;
  readonly Dictionary<ulong,byte[]> blocks=new Dictionary<ulong,byte[]>();
  public bool Allowed=true,FailNext;public int Writes;public byte[] Table=new byte[0x4000],Baseline;
  void Block(ulong a,int n){blocks.Add(a,new byte[n]);}
  public byte[] Read(ulong a,int n){foreach(var block in blocks)if(a>=block.Key&&a-block.Key+(ulong)n<=(ulong)block.Value.Length){var b=new byte[n];Array.Copy(block.Value,(int)(a-block.Key),b,0,n);return b;}throw new Exception("Unmapped memory");}
  public void Put(ulong a,byte[] b){foreach(var block in blocks)if(a>=block.Key&&a-block.Key+(ulong)b.Length<=(ulong)block.Value.Length){Array.Copy(b,0,block.Value,(int)(a-block.Key),b.Length);return;}throw new Exception("Unmapped write");}
  public void U(ulong a,ulong v){Put(a,BitConverter.GetBytes(v));}
  public void I(ulong a,uint v){Put(a,BitConverter.GetBytes(v));}
  public void F(ulong a,float v){Put(a,BitConverter.GetBytes(v));}
  public float F(ulong a){return BitConverter.ToSingle(Read(a,4),0);}
  public void Write(ulong a,byte[] before,byte[] after){if(FailNext){FailNext=false;throw new IOException("Test write failure");}Need(Equal(Read(a,before.Length),before),"checked write ownership");Put(a,after);Writes++;}
  public void Distance(float value){byte[] b=SC2MissionZoom.Encode(value,Table);for(int i=0;i<3;i++)Put(Camera+0x13c+(ulong)i*4,b);}
  public void Shadow(float value){var b=SC2MissionZoom.Encode(value,Table);for(int i=0;i<3;i++)Put(Camera+0x118+(ulong)i*4,b);}
  public float Shadow(){return SC2MissionZoom.Decode(BitConverter.ToUInt32(Read(Camera+0x118,4),0),Table);}
  public float Distance(){return SC2MissionZoom.Decode(BitConverter.ToUInt32(Read(Camera+0x13c,4),0),Table);}
  ulong nextAllocation=0x30000000;
  public ulong Allocate(int n){ulong a=nextAllocation;nextAllocation+=0x10000;Block(a,n);return a;}
  public SC2MissionZoom Zoom(string record){return new SC2MissionZoom(Read,Write,()=>Allowed,Module,record,"test-session",Allocate,(a,n)=>{});}
  public Memory(){
   Block(Module+0x2d609e8-16,0x88);U(Module+0x2d609e8+0x70,Module+0xbf7840);Block(Module+0x4046470,8);Block(0x20050000,0x1b30);Block(0x20060000,0x558);Block(0x20070000,0x18);
   Block(Camera,0x200);Block(Data,0xf0);Block(Presets,5*0x90);Block(Render,0x20);Block(Module+0x3a902e0,0x4000);
   U(Module+0x4046470,0x20050000);U(0x20050000+0x1b28,0x20060000);U(0x20060000+0x550,0x20070000);U(0x20070000+0x10,Camera);
   U(Camera,Module+0x2d609e8);U(Camera+8,Render);U(Camera+0x18,Data>>5);I(Data+0xd8,5);U(Data+0xe8,Presets);
   for(int i=0;i<5;i++){I(Presets+(ulong)i*0x90+0x20,1);F(Presets+(ulong)i*0x90+0x24,34-4*i);I(Presets+(ulong)i*0x90+0x28,1);F(Presets+(ulong)i*0x90+0x2c,56-4*i);}
   new Random(741).NextBytes(Table);Put(Module+0x3a902e0,Table);Distance(34);Shadow(75);Baseline=Read(Presets,5*0x90);
  }
 }

 public static void Run(){
  string record=Path.Combine(Path.GetTempPath(),"sc2-zoom-test-"+Guid.NewGuid().ToString("N")+".txt");
  try{
   foreach(int bad in new[]{-10,1,15,110,200}){bool rejected=false;try{SC2MissionZoom.Validate(bad);}catch(ArgumentException){rejected=true;}Need(rejected,"invalid percentage");}
   foreach(int bad in new[]{0,4,21,100}){bool rejected=false;try{SC2MissionZoom.ValidateSteps(bad);}catch(ArgumentException){rejected=true;}Need(rejected,"invalid step count");}
   var m=new Memory();var zoom=m.Zoom(record);int cases=0;
   for(int steps=5;steps<=20;steps++)for(int percent=0;percent<=100;percent+=10){
    var presets=SC2MissionZoom.Presets(m.Baseline,percent,steps);float maximum=SC2MissionZoom.Distance(34,percent),spacing=(maximum-18)/(steps-1);
    Need(presets.Length==steps*0x90,"preset count");
    for(int i=0;i<steps;i++){
     float distance=BitConverter.ToSingle(presets,i*0x90+0x24),pitch=BitConverter.ToSingle(presets,i*0x90+0x2c);
     Need(Math.Abs(distance-(maximum-spacing*i))<.0001,"even spacing");Need(Math.Abs(pitch-Math.Min(56,distance+22))<.0001,"native tilt interpolation and extension clamp");
    }
    Need(zoom.Tick(percent,steps),"live switch");Need(Math.Abs(m.Shadow()-75*(1+percent/100f))<.001,"shadow clip scales with selected maximum without compounding");Need(Equal(m.Baseline,m.Read(Memory.Presets,m.Baseline.Length)),"original table never changed");Need(BitConverter.ToUInt32(m.Read(Memory.Data+0xd8,4),0)==5,"original count retained between callbacks");cases++;
   }
   var restarted=m.Zoom(record);Need(restarted.Tick(50,8),"restart attachment");Need(Math.Abs(m.Shadow()-112.5f)<.001,"restart retains original shadow baseline");Need(restarted.Stop(),"stop");Need(m.Shadow()==75,"stop restores original shadow clip");Need(!File.Exists(record),"session record removed");Need(BitConverter.ToUInt64(m.Read(Memory.Camera,8),0)==Memory.Module+0x2d609e8,"native vtable restored");
   foreach(int blocked in new[]{0,1,2,3}){m=new Memory();if(blocked==0)m.Allowed=false;if(blocked==1)m.U(Memory.Render,0xabcdef);if(blocked==2)m.I(Memory.Camera+0xa8,1);if(blocked==3)m.U(Memory.Camera,0xabcdef);zoom=m.Zoom(null);Need(!zoom.Tick(50,10)&&m.Writes==0,"blocked/cinematic camera unchanged");}
   m=new Memory();zoom=m.Zoom(null);zoom.Tick(50,20);m.Allowed=false;zoom.Tick(50,20);Need(BitConverter.ToUInt64(m.Read(Memory.Camera,8),0)==Memory.Module+0x2d609e8,"leaving campaign detaches callback");
   m=new Memory();m.Shadow(120);zoom=m.Zoom(record);zoom.Tick(100,10);Need(m.Shadow()==240,"mission-specific shadow baseline");zoom.Tick(0,10);Need(m.Shadow()==120,"Default maximum restores shadow clip while retaining custom steps");zoom.Tick(50,20);Need(m.Shadow()==180,"shadow setting can be reapplied");zoom.Stop();Need(m.Shadow()==120,"mission-specific baseline restored");
   m=new Memory();zoom=m.Zoom(null);zoom.Tick(100,10);m.Shadow(90);zoom.Tick(50,10);Need(m.Shadow()==90,"external shadow change respected");zoom.Stop();Need(m.Shadow()==90,"restore does not overwrite external shadow changes");
   foreach(float bad in new[]{0f,-1f,Single.NaN,Single.PositiveInfinity}){m=new Memory();m.Shadow(bad);var before=m.Read(Memory.Camera+0x118,12);zoom=m.Zoom(null);zoom.Tick(100,10);Need(Equal(before,m.Read(Memory.Camera+0x118,12)),"unsupported shadow value left unchanged");zoom.Stop();}
   Native(m.Baseline);Console.WriteLine("PASS: "+cases+" zoom range/step combinations, original data ownership, restart/restore, native wheel endpoints, automatic shadow range, restoration, and campaign guards.");
  }finally{if(File.Exists(record))File.Delete(record);if(File.Exists(record+".tmp"))File.Delete(record+".tmp");}
 }
 [DllImport("kernel32.dll")]static extern IntPtr VirtualAlloc(IntPtr a,UIntPtr n,uint flags,uint protect);
 [DllImport("kernel32.dll")]static extern bool VirtualProtect(IntPtr a,UIntPtr n,uint protect,out uint old);
 [DllImport("kernel32.dll")]static extern bool VirtualFree(IntPtr a,UIntPtr n,uint kind);
 [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate ulong Wheel(IntPtr camera,int delta,ulong arg3,ulong arg4);
 static void Native(byte[] baseline){
  var allocations=new List<IntPtr>();IntPtr code=IntPtr.Zero;
  Func<int,ulong> alloc=n=>{var p=Marshal.AllocHGlobal(n+32);allocations.Add(p);ulong a=((ulong)p.ToInt64()+31)&~31UL;Marshal.Copy(new byte[n],0,(IntPtr)(long)a,n);return a;};
  Action<ulong,ulong> ptr=(a,v)=>Marshal.WriteInt64((IntPtr)(long)a,(long)v);
  Action<ulong,int> integer=(a,v)=>Marshal.WriteInt32((IntPtr)(long)a,v);
  Func<ulong,ulong> q=a=>(ulong)Marshal.ReadInt64((IntPtr)(long)a);
  Func<ulong,int> u=a=>Marshal.ReadInt32((IntPtr)(long)a);
  Action<ulong,byte[]> put=(a,b)=>Marshal.Copy(b,0,(IntPtr)(long)a,b.Length);
  try{
   ulong state=alloc(8192),camera=alloc(0x900),data=alloc(0x240),array=alloc(baseline.Length),render=alloc(16),session=alloc(8),manager=alloc(0x68),info=alloc(0x1ed0),map=alloc(260);
   put(array,baseline);ptr(camera+0x18,data>>5);ptr(camera+8,render);ptr(data+0xd8,5);ptr(data+0xe8,array);ptr(session,manager);ptr(manager+0x60,info);put(map,System.Text.Encoding.ASCII.GetBytes("Campaign/Test.SC2Map\0"));
   ptr(state+0x10,camera);ptr(state+0x18,data);ptr(state+0x20,5);ptr(state+0x28,array);ptr(state+0x38,state+0x100);
   int seenCount=0;bool arguments=true;
   Wheel original=(cam,delta,a3,a4)=>{
    arguments&=(ulong)cam.ToInt64()==camera&&a3==123&&a4==456;seenCount=u(data+0xd8);int index=Math.Max(0,Math.Min(seenCount-1,u(camera+0xa4)+delta));
    integer(camera+0xa4,index);integer(camera+0x13c,u(q(data+0xe8)+(ulong)index*0x90+0x24));return 789;
   };
   byte[] bytes=SC2MissionZoom.Build(state,(ulong)Marshal.GetFunctionPointerForDelegate(original).ToInt64(),session,map,0);
   code=VirtualAlloc(IntPtr.Zero,(UIntPtr)4096,0x3000,4);Need(code!=IntPtr.Zero,"native allocation");Marshal.Copy(bytes,0,code,bytes.Length);uint old;Need(VirtualProtect(code,(UIntPtr)4096,0x20,out old),"native protection");var wheel=(Wheel)Marshal.GetDelegateForFunctionPointer(code,typeof(Wheel));
   for(int steps=5;steps<=20;steps++)foreach(int percent in new[]{0,10,50,100}){
    var presets=SC2MissionZoom.Presets(baseline,percent,steps);put(state+0x100,presets);ptr(state+0x30,(ulong)steps);
    for(int i=0;i<steps;i++){float d=BitConverter.ToSingle(presets,i*0x90+0x24);int closest=0;float error=Single.MaxValue;for(int j=0;j<5;j++){float e=Math.Abs(d-BitConverter.ToSingle(baseline,j*0x90+0x24));if(e<error){error=e;closest=j;}}integer(state+0xd00+(ulong)i*4,closest);}
    integer(state,1);integer(state+0xc,0);integer(camera+0xa4,0);integer(camera+0x13c,0);integer(state+0x48,0);
    for(int n=0;n<steps+2;n++){
     Need(wheel((IntPtr)(long)camera,1,123,456)==789,"return forwarding");Need(seenCount==steps,"expanded count visible only during native wheel call");Need(u(state+0xc)==Math.Min(n+1,steps-1),"wheel zoom in steps and clamp");Need(q(data+0xd8)==5&&q(data+0xe8)==array,"original header restored after wheel");Need(u(camera+0xa4)>=0&&u(camera+0xa4)<5,"native index remains in original bounds");Need(u(state+4)==0,"busy flag released");
    }
    for(int n=0;n<steps+2;n++){wheel((IntPtr)(long)camera,-1,123,456);Need(u(state+0xc)==Math.Max(steps-2-n,0),"wheel zoom out steps and clamp");}
   }
   foreach(int blocked in new[]{0,1,2,3,4}){
    integer(state,1);integer(state+4,0);integer(camera+0xa8,0);ptr(render,0);integer(info+0x1ec8,0);integer(camera+0xa4,0);
    if(blocked==0)integer(state,0);if(blocked==1)integer(info+0x1ec8,2);if(blocked==2)ptr(render,123);if(blocked==3)integer(camera+0xa8,1);if(blocked==4)integer(state+4,1);
    wheel((IntPtr)(long)camera,1,123,456);Need(seenCount==5,"disabled/online/cinematic/reentry forwards native presets");Need(q(data+0xd8)==5&&q(data+0xe8)==array,"guard preserves header");
   }
   Need(arguments,"all native arguments preserved");GC.KeepAlive(original);
  }finally{if(code!=IntPtr.Zero)VirtualFree(code,UIntPtr.Zero,0x8000);foreach(var p in allocations)Marshal.FreeHGlobal(p);}
 }
}
