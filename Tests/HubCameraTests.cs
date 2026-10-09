using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public static class HubCameraTests {
 [DllImport("kernel32.dll")]static extern IntPtr VirtualAlloc(IntPtr a,UIntPtr n,uint flags,uint protect);
 [DllImport("kernel32.dll")]static extern bool VirtualProtect(IntPtr a,UIntPtr n,uint protect,out uint old);
 [DllImport("kernel32.dll")]static extern bool VirtualFree(IntPtr a,UIntPtr n,uint kind);
 [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate int Gate();
 [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate ulong Update(IntPtr entry,ulong arg);
 static readonly List<IntPtr> buffers=new List<IntPtr>(),code=new List<IntPtr>();
 static ulong New(int n){var p=Marshal.AllocHGlobal(n);Marshal.Copy(new byte[n],0,p,n);buffers.Add(p);return (ulong)p.ToInt64();}
 static void Ptr(ulong a,ulong v){Marshal.WriteInt64((IntPtr)(long)a,(long)v);}static void I(ulong a,int v){Marshal.WriteInt32((IntPtr)(long)a,v);}static void S(ulong a,string v){var b=new byte[260];Encoding.ASCII.GetBytes(v).CopyTo(b,0);Marshal.Copy(b,0,(IntPtr)(long)a,260);}
 static float F(ulong a){return BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32((IntPtr)(long)a)),0);}static void F(ulong a,float v){I(a,BitConverter.ToInt32(BitConverter.GetBytes(v),0));}
 static void Need(bool b,string s){if(!b)throw new Exception(s);}
 static IntPtr Code(byte[] b){Need(b.Length<=4096,"Callback fits allocated page");var p=VirtualAlloc(IntPtr.Zero,(UIntPtr)4096,0x3000,4);Need(p!=IntPtr.Zero,"Allocate mock callback");code.Add(p);Marshal.Copy(b,0,p,b.Length);uint old;Need(VirtualProtect(p,(UIntPtr)4096,0x20,out old),"Protect mock callback");return p;}
 static Gate Predicate(ulong session,ulong map,ulong root,ulong model,bool display){var c=new List<byte>();var denied=new List<int>();if(display)SC2DisplayGate.Emit(c,denied,session,map,root);else SC2HubGate.Emit(c,denied,session,map,root,model,1);SC2HubGate.B(c,"B8 01 00 00 00 C3");SC2CampaignGate.Resolve(c,denied,c.Count);SC2HubGate.B(c,"31 C0 C3");return (Gate)Marshal.GetDelegateForFunctionPointer(Code(c.ToArray()),typeof(Gate));}
 public static void Run(){try{
  ulong session=New(8),manager=New(0x68),info=New(0x1ed0),map=New(260),root=New(8),sceneManager=New(0x1b30),scene=New(0x558),bundle=New(0x18),camera=New(0x10),render=New(0x18),model=New(0x160),actor=New(0x48),name=New(260),entry=New(0x78),definition=New(0x80),state=New(0x40);
  Ptr(session,manager);Ptr(manager+0x60,info);Ptr(root,sceneManager);Ptr(sceneManager+0x1b28,scene);Ptr(scene+0x550,bundle);Ptr(bundle+0x10,camera);Ptr(camera+8,render);Ptr(render,model);I(render+8,1);Ptr(model+0x158,actor);Ptr(actor+0x40,name);Ptr(entry+0x50,definition);S(name,"Assets/StoryModeSets/Terran/SM_HyperionLab.m3");
  var hub=Predicate(session,map,root,model,false);var display=Predicate(session,map,root,model,true);int cases=0;
  Action<int,int,string> expect=(h,d,label)=>{Need(hub()==h&&display()==d,label);cases++;};
  foreach(var path in new[]{"Assets/StoryModeSets/Terran/SM_HyperionLab.m3","assets\\storymodesets\\zerg\\leviathan.m3","ASSETS/STORYMODESETS/Protoss/Spear.m3","Assets/StoryModeSets/Terran/NovaShip.m3"}){S(name,path);Need(SC2HubGate.Matches(false,"",path),"Managed hub classification");expect(1,1,"Hub species paths");}
  I(info+0x1ec8,2);expect(0,0,"Online session rejected");I(info+0x1ec8,0);
  foreach(var path in new[]{"Maps/Campaign/Liberty/Level.SC2Map","Campaign/Swarm/Level.SC2Map"}){S(map,path);Need(!SC2HubGate.Matches(false,path,"Assets/StoryModeSets/Terran/Lab.m3"),"Managed level rejected");expect(0,1,"Mission resolution remains allowed; camera correction denied");}
  foreach(var path in new[]{"Maps/Campaign/TStory01.SC2Map","Campaign/TStory01.SC2Map","MAPS\\CAMPAIGN\\TSTORY01.SC2MAP","Maps/Campaign/Swarm/ZStoryChar.SC2Map","Campaign/Swarm/ZStoryExpedition.SC2Map","MAPS\\CAMPAIGN\\SWARM\\ZSTORYZERUS.SC2MAP"}){
   S(map,path);Need(SC2HubGate.Matches(false,path,"assets/storymodesets/terran/bridge.m3"),"Managed return-to-hub classification");expect(1,1,"Return-to-hub map and camera accepted");
   I(info+0x1ec8,2);expect(0,0,"Online story map rejected");I(info+0x1ec8,0);
   S(name,"assets/units/terran/marine.m3");expect(0,1,"Story map still requires hub model");S(name,"assets/storymodesets/terran/bridge.m3");
  }
  foreach(var path in new[]{"Maps/Campaign/TStory01.SC2Map.extra","Maps/Campaign/TStory010.SC2Map","Maps/Campaign/Swarm/ZStoryZerus.SC2Map.extra","Maps/Campaign/Swarm/ZStoryChar01.SC2Map","Maps/Campaign/TZeratul04.SC2Map"}){S(map,path);expect(0,1,"Other map names do not enable hub framing");}
  S(map,"Maps/Multiplayer/Test.SC2Map");expect(0,0,"Noncampaign map rejected");S(map,"");
  foreach(var path in new[]{"Assets/Units/Marine.m3","prefix/Assets/StoryModeSets/Lab.m3",""}){S(name,path);expect(0,0,"Unrecognized model rejected");}
  var unterminated=new byte[260];for(int i=0;i<260;i++)unterminated[i]=(byte)'a';Encoding.ASCII.GetBytes("assets/storymodesets/").CopyTo(unterminated,0);Marshal.Copy(unterminated,0,(IntPtr)(long)name,260);expect(0,0,"Unterminated asset rejected");S(name,"assets/storymodesets/terran/lab.m3");
  Ptr(root,0);expect(0,0,"Missing scene rejected");Ptr(root,sceneManager);Ptr(manager+0x60,0);expect(0,0,"Missing session rejected");Ptr(manager+0x60,info);
  I(render+8,2);expect(0,1,"Wrong active camera rejected");I(render+8,1);
  float[] values={.15f,.55f,.959931f,1.080839f,1.5f,2.5f};int tick=0;bool changeMap=false;
  Update original=(e,arg)=>{F(entry+0x38,values[tick%values.Length]);if(changeMap)S(map,"Maps/Campaign/Level.SC2Map");return arg;};
  I(state,1);F(state+0x10,.5f);F(state+0x14,2f);F(state+0x18,16f/9f);
  var bytes=SC2HubCamera.Build(state,(ulong)Marshal.GetFunctionPointerForDelegate(original).ToInt64(),entry,definition,model,1,session,map,root);
  var update=(Update)Marshal.GetDelegateForFunctionPointer(Code(bytes),typeof(Update));
  for(tick=0;tick<120;tick++){Need(update((IntPtr)(long)entry,1234)==1234,"Callback return");double wanted=2*Math.Atan(Math.Tan(values[tick%values.Length]/2.0)/(16f/9f));Need(Math.Abs(F(entry+0x38)-wanted)<.000001,"Animated FOV conversion");Need(Marshal.ReadInt32((IntPtr)(long)(state+4))==0,"Busy counter balanced");}
  Need(Marshal.ReadInt32((IntPtr)(long)(state+8))==120,"Conversion counter");
  F(state+0x20,(float)(10*Math.PI/180));F(state+0x24,(float)(89*Math.PI/180));
  int expandedChecks=0;
  foreach(float aspect in new[]{16f/9f,3440f/1440f,32f/9f})foreach(int sourceVertical in new[]{0,1}){
   F(state+0x1c,aspect);I(state+0x28,sourceVertical);
   // Alternate modes every callback to exercise live switching without accumulated zoom.
   for(tick=0;tick<120;tick++){
    int selected=tick%2+1;I(state,selected);Need(update((IntPtr)(long)entry,5678)==5678,"Mode switch return");
    double source=values[tick%values.Length];double fit=sourceVertical==1?source:2*Math.Atan(Math.Tan(source/2)/(16f/9f));
    double wanted=fit;
    if(selected==2){double horizontal=2*Math.Atan(Math.Tan(fit/2)*aspect);double expanded=Math.Min(horizontal+20*Math.PI/180,178*Math.PI/180);wanted=2*Math.Atan(Math.Tan(expanded/2)/aspect);}
    Need(Math.Abs(F(entry+0x38)-wanted)<.00001,"Fit/Expanded aspect calculation");
    Need(Marshal.ReadInt32((IntPtr)(long)(definition+0x24))==1,"Corrected camera uses vertical FOV");
    Need(Marshal.ReadInt32((IntPtr)(long)(state+4))==0,"Mode switch busy counter balanced");expandedChecks++;
   }
   I(state,0);tick=0;update((IntPtr)(long)entry,0);
   Need(Math.Abs(F(entry+0x38)-values[0])<.000001,"Off keeps animated FOV");
   Need(Marshal.ReadInt32((IntPtr)(long)(definition+0x24))==sourceVertical,"Off restores original FOV convention");
  }
  // Replay mission -> named hub -> mission -> empty-path hub on the same callback.
  I(state+0x28,0);F(state+0x1c,3440f/1440f);I(state,2);tick=1;
  foreach(string path in new[]{"Maps/Campaign/TZeratul04.SC2Map","Maps/Campaign/TStory01.SC2Map","Maps/Campaign/Swarm/ZStoryZerus.SC2Map","Maps/Campaign/Swarm/ZStoryChar.SC2Map","Campaign/Swarm/ZStoryExpedition.SC2Map","Maps/Campaign/Liberty/Level.SC2Map",""}){
   S(map,path);update((IntPtr)(long)entry,5);bool isHub=SC2HubGate.HubMap(path);
   Need(Marshal.ReadInt32((IntPtr)(long)(definition+0x24))==(isHub?1:0),"Mission/hub transition camera convention");
   if(isHub)Need(Math.Abs(F(entry+0x38)-values[1])>.01,"Expanded resumes after mission");else Need(Math.Abs(F(entry+0x38)-values[1])<.000001,"Mission camera remains unchanged");
  }
  I(state,1);I(state+0x28,0);
  Action<string> rejected=label=>{tick=0;Need(update((IntPtr)(long)entry,42)==42,label+" return");Need(Math.Abs(F(entry+0x38)-values[0])<.000001,label+" unchanged FOV");Need(Marshal.ReadInt32((IntPtr)(long)(definition+0x24))==0,label+" horizontal restored");};
  I(info+0x1ec8,2);rejected("Online");I(info+0x1ec8,0);I(state,0);rejected("Stopped");I(state,1);changeMap=true;rejected("Transition during original callback");changeMap=false;S(map,"");I(render+8,2);rejected("Camera switch");I(render+8,1);Ptr(root,0);rejected("Scene destroyed");Ptr(root,sceneManager);
  // All three callbacks are installed before switching. No watcher tick runs
  // between selecting a camera and its first rendered update.
  ulong collection=New(3*0x78);var prepared=new Update[3];var originals=new List<Update>();var states=new ulong[3];var defs=new ulong[3];
  for(uint i=0;i<3;i++){
   ulong e=collection+i*0x78,d=New(0x80),st=New(0x40);states[i]=st;defs[i]=d;Ptr(e+0x50,d);
   I(st,2);F(st+0x10,.5f);F(st+0x14,2f);F(st+0x18,16f/9f);F(st+0x1c,3440f/1440f);F(st+0x20,(float)(10*Math.PI/180));F(st+0x24,(float)(89*Math.PI/180));
   Update native=(address,arg)=>{F((ulong)address.ToInt64()+0x38,1.029099f);return arg;};originals.Add(native);
   prepared[i]=(Update)Marshal.GetDelegateForFunctionPointer(Code(SC2HubCamera.Build(st,(ulong)Marshal.GetFunctionPointerForDelegate(native).ToInt64(),e,d,model,i,session,map,root)),typeof(Update));
  }
  var cameraSet=new SC2HubGate.Scene{Allowed=true,Model=model,Cameras=collection,CameraCount=3,Index=2};
  var switched=new SC2HubGate.Scene{Allowed=true,Model=model,Cameras=collection,CameraCount=3,Index=1};
  Need(SC2HubCamera.SameCameraSet(cameraSet,switched),"Submenu retains camera set");
  for(uint i=0;i<3;i++)Need(SC2HubCamera.ContainsCamera(switched,model,collection+i*0x78,i),"Inactive cameras stay prepared");
  Need(!SC2HubCamera.ContainsCamera(switched,model,collection+3*0x78,3),"Camera count bound");
  Need(!SC2HubCamera.ContainsCamera(switched,model+8,collection,0),"Old model retired");
  Need(!SC2HubCamera.ContainsCamera(switched,model,collection+8,0),"Old array retired");
  cameraSet.Allowed=false;Need(!SC2HubCamera.SameCameraSet(cameraSet,switched),"Leaving hub retires set");
  double fitV=2*Math.Atan(Math.Tan(1.029099/2)/(16f/9f));double expandedV=2*Math.Atan(Math.Tan(Math.Atan(Math.Tan(fitV/2)*(3440f/1440f))+10*Math.PI/180)/(3440f/1440f));
  for(int i=0;i<120;i++){
   int active=new[]{2,1,2,1,0,2}[i%6];I(render+8,active);ulong e=collection+(uint)active*0x78;
   Need(prepared[active]((IntPtr)(long)e,123)==123,"Prepared callback return");
   Need(Math.Abs(F(e+0x38)-expandedV)<.00001,"First update already corrected after submenu switch");
   int inactive=(active+1)%3;ulong other=collection+(uint)inactive*0x78;prepared[inactive]((IntPtr)(long)other,0);
   Need(Math.Abs(F(other+0x38)-1.029099f)<.000001,"Inactive camera guard preserved");
  }
  S(map,"Maps/Campaign/Level.SC2Map");
  for(int i=0;i<3;i++){I(render+8,i);ulong e=collection+(uint)i*0x78;prepared[i]((IntPtr)(long)e,0);Need(Math.Abs(F(e+0x38)-1.029099f)<.000001,"Prepared cameras reject missions");I(states[i],0);}
  S(map,"");for(int i=0;i<3;i++){I(render+8,i);ulong e=collection+(uint)i*0x78;prepared[i]((IntPtr)(long)e,0);Need(Marshal.ReadInt32((IntPtr)(long)(defs[i]+0x24))==0,"Off restores every prepared camera");}
  GC.KeepAlive(originals);Console.WriteLine("PASS: 120 submenu switches corrected on their first update without polling; camera-set lifecycle, inactive/mission guards and Off restoration.");
  GC.KeepAlive(original);Console.WriteLine("PASS: "+cases+" hub/display gate cases; 120 animated Fit updates; "+expandedChecks+" live Fit/Expanded switches across 16:9, 3440x1440 and 32:9; Off restores horizontal and vertical cameras; online, stop, mission transition, camera switch, and destroyed-scene restoration. All use local mock memory.");
 }finally{foreach(var p in code)VirtualFree(p,UIntPtr.Zero,0x8000);foreach(var p in buffers)Marshal.FreeHGlobal(p);code.Clear();buffers.Clear();}}
}
