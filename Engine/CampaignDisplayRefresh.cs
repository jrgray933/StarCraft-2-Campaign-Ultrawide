using System;
using System.Runtime.InteropServices;
public static class SC2CampaignDisplayRefresh {
 public const ulong RequestRva=0x3a12701;
 [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
 public static bool IsForeground(int pid){uint actual;GetWindowThreadProcessId(GetForegroundWindow(),out actual);return actual==(uint)pid;}
 static byte[] Read(int pid,ulong a,int n){var b=SC2Memory.Read(pid,a,n);if(b==null||b.Length!=n)throw new InvalidOperationException("Display state is unavailable.");return b;}
 static ulong Ptr(int pid,ulong a){return BitConverter.ToUInt64(Read(pid,a,8),0);}
 static uint U32(int pid,ulong a){return BitConverter.ToUInt32(Read(pid,a,4),0);}
 public static bool Pending(int pid,ulong module){var b=Read(pid,module+RequestRva,2);return b[0]!=0||b[1]!=0;}
 public static string Request(int pid,ulong module,ulong device,ulong vtable,ulong state){
  SC2DisplayGate.Require(pid,module);
  if(!IsForeground(pid))return "Waiting for game focus";
  // These two booleans are the normal graphics-settings queue, not the
  // D3D lost-device reset flag. The game consumes and clears them itself.
  byte[] signature={0x88,0x0d,0x6b,0x84,0x7e,0x03,0x88,0x15,0x66,0x84,0x7e,0x03,0xc3};
  var live=Read(pid,module+0x22a290,signature.Length);
  for(int i=0;i<live.Length;i++)if(live[i]!=signature[i])throw new InvalidOperationException("Graphics refresh is not supported by this game version.");
  if(device==0||vtable==module+0x2DB8098||Ptr(pid,module+0x43D0E08)!=device||Ptr(pid,device)!=vtable||U32(pid,state)!=1)throw new InvalidOperationException("Resolution helper is not attached.");
  ulong ui=Ptr(pid,module+0x4032368);if(ui==0||Ptr(pid,ui)!=module+0x2D522A8)return "Waiting for campaign interface";
  ulong resource=Ptr(pid,device+0x80);if(resource==0||Ptr(pid,resource)!=module+0x2DB9DA8)return "Waiting for display";
  if(Read(pid,resource+0x80,1)[0]!=0)return "Waiting for fullscreen mode";
  uint packed=U32(pid,resource+0x60);if((packed&0x3fff)<1280||((packed>>14)&0x3fff)<720)return "Waiting for display";
  if(Pending(pid,module))return "Waiting for graphics update";
  SC2DisplayGate.Require(pid,module);
  if(!IsForeground(pid)||Ptr(pid,module+0x4032368)!=ui||Ptr(pid,device)!=vtable)return "Waiting for stable campaign";
  SC2HudHook.CheckedWrite(pid,module+RequestRva,new byte[]{0,0},new byte[]{1,1});
  return "Requested";
 }
}

// Pure scheduling policy, independently tested without opening a game process.
public sealed class SC2DisplayRefreshSchedule {
 string context="";double stable=-1,last=-1;int attempts;
 public void Clear(){context="";stable=-1;last=-1;attempts=0;}
 public string Observe(string key,bool focused,bool ready,double now){
  if(context!=key){Clear();context=key;}
  if(ready){stable=-1;return "Ready";}
  if(!focused){stable=-1;return "Focus";}
  if(stable<0)stable=now;
  if(now-stable<2000)return "Stable";
  if(last>=0&&now-last<15000)return "Pending";
  if(attempts>=2)return "Failed";
  return "Request";
 }
 public void Requested(double now){attempts++;last=now;}
 public static string SelfTest(){
  var s=new SC2DisplayRefreshSchedule();
  if(s.Observe("one",false,false,0)!="Focus"||s.Observe("one",true,false,100)!="Stable"||s.Observe("one",true,false,2099)!="Stable"||s.Observe("one",true,false,2100)!="Request")throw new Exception("Focus/stability scheduling failed");
  s.Requested(2100);if(s.Observe("one",true,false,17099)!="Pending"||s.Observe("one",true,false,17100)!="Request")throw new Exception("Retry interval failed");
  s.Requested(17100);if(s.Observe("one",true,false,32100)!="Failed")throw new Exception("Retry bound failed");
  s.Observe("one",false,false,33000);s.Observe("one",true,false,34000);if(s.Observe("one",true,false,36000)!="Failed")throw new Exception("Focus cycling reset retry bound");
  if(s.Observe("one",true,true,37000)!="Ready"||s.Observe("two",true,false,38000)!="Stable"||s.Observe("two",true,false,40000)!="Request")throw new Exception("Ready/new mission failed");
  s.Clear();if(s.Observe("two",true,false,41000)!="Stable")throw new Exception("Campaign exit did not clear context");
  return "PASS: automatic refresh waits for focus/stability, backs off pending updates, stops after two requests, handles mission changes, and does nothing when already at target.";
 }
}
