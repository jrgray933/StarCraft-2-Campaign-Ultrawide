using System;
using System.Runtime.InteropServices;
public class SC2Memory {
 [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
 [DllImport("kernel32.dll")] static extern bool ReadProcessMemory(IntPtr h,IntPtr addr,byte[] b,UIntPtr len,out UIntPtr read);
 [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
 public static byte[] Read(int pid,ulong addr,int size) { var h=OpenProcess(0x410,false,pid);try{var b=new byte[size];UIntPtr read;if(!ReadProcessMemory(h,(IntPtr)(long)addr,b,(UIntPtr)size,out read))return null;return b;}finally{CloseHandle(h);} }
}
