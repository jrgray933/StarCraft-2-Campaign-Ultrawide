using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
public class SC2AutoStart {
 [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
 [DllImport("kernel32.dll")] static extern bool ReadProcessMemory(IntPtr h,IntPtr address,byte[] buffer,UIntPtr length,out UIntPtr read);
 [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
 static ulong ReadPtr(IntPtr h,ulong address){var b=new byte[8];UIntPtr n;if(!ReadProcessMemory(h,(IntPtr)(long)address,b,(UIntPtr)8,out n)||n.ToUInt64()!=8)return 0;return BitConverter.ToUInt64(b,0);}
 static bool Signature(IntPtr h,ulong address){byte[] wanted={0x48,0x8b,0xc4,0x48,0x89,0x58,0x08,0x55,0x56,0x57,0x41,0x54};var b=new byte[wanted.Length];UIntPtr n;if(!ReadProcessMemory(h,(IntPtr)(long)address,b,(UIntPtr)b.Length,out n)||n.ToUInt64()!=(ulong)b.Length)return false;for(int i=0;i<b.Length;i++)if(b[i]!=wanted[i])return false;return true;}
 public static void ValidateExecutable(string path,string version){
  if(!String.Equals(Path.GetFileName(path),"SC2_x64.exe",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Select the 64-bit StarCraft II game.");
 }
 public class Result {public ulong Device,Code,State,VTable;public int Length;public double AttachedAfterMilliseconds;public bool ResourceAlreadyExisted;}
 public static Result Attach(int pid,string expectedExe,string recordPath,string stopPath,int timeoutMs,int width,int height){
  SC2CampaignModeHook.ValidateTarget(width,height);
  var process=Process.GetProcessById(pid);var h=OpenProcess(0x410,false,pid);if(h==IntPtr.Zero)throw new Exception("Cannot open the game for startup checks: "+Marshal.GetLastWin32Error());
  var timer=Stopwatch.StartNew();SC2CampaignModeHook.Prepared prepared=null;ulong module=0,table=0;DateTime started=process.StartTime;
  try{while(timer.ElapsedMilliseconds<timeoutMs){
   if(process.HasExited)throw new Exception("Game exited during startup.");if(File.Exists(stopPath))throw new OperationCanceledException("Automatic helper stopped.");
   if(module==0){try{var main=process.MainModule;if(!String.Equals(main.FileName,expectedExe,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Game executable path differs from the supported installation.");ValidateExecutable(main.FileName,main.FileVersionInfo.FileVersion);module=(ulong)main.BaseAddress.ToInt64();table=module+0x2DB8098;}catch(System.ComponentModel.Win32Exception){Thread.Sleep(5);continue;}}
   if(!SC2CampaignGate.Check(pid,module).Allowed){Thread.Sleep(150);continue;}
   if(prepared==null){if(ReadPtr(h,table+0x28)!=module+0xE60F70||!Signature(h,module+0xE60F70)){Thread.Sleep(2);continue;}prepared=SC2CampaignModeHook.Prepare(pid,table,width,height);}
   // The D3D9-specific pointer is published by the constructor, before the
   // generic graphics pointer. Only accept the fully installed derived vtable.
   ulong device=ReadPtr(h,module+0x43D1E10);if(device==0||ReadPtr(h,device)!=table)device=ReadPtr(h,module+0x43D0E08);
   if(device!=0&&ReadPtr(h,device)==table){
    bool hadResource=ReadPtr(h,device+0x80)!=0;
    string json=String.Format(CultureInfo.InvariantCulture,"{{\"GateRevision\":2,\"GamePid\":{0},\"Started\":\"{1}\",\"Build\":\"{10}\",\"Device\":\"{2:X}\",\"OriginalVTable\":\"{3:X}\",\"VTable\":\"{4:X}\",\"Code\":\"{5:X}\",\"State\":\"{6:X}\",\"Length\":{7},\"TargetWidth\":{8},\"TargetHeight\":{9}}}",pid,started.ToString("o",CultureInfo.InvariantCulture),device,table,prepared.VTable,prepared.Code,prepared.State,prepared.Length,width,height,process.MainModule.FileVersionInfo.FileVersion.Replace("\\","\\\\").Replace("\"","\\\""));
    File.WriteAllText(recordPath,json);
    SC2CampaignGate.Require(pid,module);
    SC2CampaignModeHook.SwapPointer(pid,device,table,prepared.VTable);
    return new Result{Device=device,Code=prepared.Code,State=prepared.State,VTable=prepared.VTable,Length=prepared.Length,AttachedAfterMilliseconds=(DateTime.Now-started).TotalMilliseconds,ResourceAlreadyExisted=hadResource};
   }
   Thread.Sleep(1);
  }throw new TimeoutException("The supported graphics device did not become ready in time.");}
  finally{CloseHandle(h);process.Dispose();}
 }
}
