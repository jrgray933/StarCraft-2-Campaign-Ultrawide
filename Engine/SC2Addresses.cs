using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
public static class SC2Addresses {
 sealed class Section {public int Start,End;public uint Flags;}
 sealed class Spec {public ulong Key;public string Pattern;public int Operand,ConstraintOperand;public ulong Constraint;public Spec(ulong k,string p,int o,ulong c,int co){Key=k;Pattern=p;Operand=o;Constraint=c;ConstraintOperand=co;}}
 static readonly object Sync=new object();
 static Dictionary<ulong,ulong> current;
 static int activePid;static long activeStarted;static ulong activeModule;static List<Section> activeSections;
 [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
 [DllImport("kernel32.dll")] static extern bool ReadProcessMemory(IntPtr h,IntPtr address,byte[] buffer,UIntPtr size,out UIntPtr got);
 [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
 static readonly Spec[] Specs = new Spec[] {
  new Spec(0xadde10,"48 83 EC 28 80 3D ?? ?? ?? ?? 00 75 ?? 33 C0 48 83 C4 28 C3 48 89 5C 24 20 33 DB 89 5C 24 30 E8 ?? ?? ?? ?? 66 39 18",-1,0x0,0),
  new Spec(0xbf7840,"48 89 5C 24 08 89 54 24 10 57 48 83 EC 20 49 8B D8 48 8B F9 E8 ?? ?? ?? ?? 8B 54 24 38 4C 8B C3 48 8B CF E8 ?? ?? ?? ??",-1,0x0,0),
  new Spec(0xdba490,"40 55 53 57 48 8D 6C 24 B9 48 81 EC 90 00 00 00 48 8B D9 E8 ?? ?? ?? ?? 48 8B F8 48 85 C0 75 ?? 48 8B CB E8 ?? ?? ?? ??",-1,0x0,0),
  new Spec(0xdc31c0,"40 55 57 41 55 48 8D 6C 24 B9 48 81 EC C0 00 00 00 4C 8B E9 E8 ?? ?? ?? ?? 48 8B F8 48 85 C0 75 ?? 49 8B CD",-1,0x0,0),
  new Spec(0xe60f70,"48 8B C4 48 89 58 08 55 56 57 41 54 41 55 41 56 41 57 48 8D A8 E8 FE FF FF 48 81 EC E0 01 00 00 0F 29 70 B8 4C 8B F2",-1,0x0,0),
  new Spec(0x14b4090,"48 89 5C 24 18 48 89 6C 24 20 56 57 41 55 41 56 41 57 48 83 EC 40 48 8B 71 50 4C 8D 69 38 48 8B F9 4C 89 64 24 78",-1,0x0,0),
  new Spec(0x16a6490,"40 53 48 83 EC 30 45 33 C0 48 8D 54 24 48 48 8B D9 E8 ?? ?? ?? ?? 84 C0 74 ?? 41 B8 02 00 00 00 48 8D 54 24 48 48 8B CB",-1,0x0,0),
  new Spec(0x16a7680,"40 53 48 83 EC 30 41 B8 01 00 00 00 48 8D 54 24 48 48 8B D9 E8 ?? ?? ?? ?? 84 C0 74 ?? 41 B8 03 00 00 00 48 8D 54 24 48",-1,0x0,0),
  new Spec(0x16a8480,"48 89 6C 24 10 48 89 74 24 18 57 48 83 EC 30 0F B6 69 28 0F B6 FA 48 8B F1 40 F6 C5 0F 75 ?? 32 C0 48 8B 6C 24 48",-1,0x0,0),
  new Spec(0x16b53c0,"40 57 48 83 EC 40 48 8B F9 48 89 5C 24 58 0F B6 49 4F 0F B6 C1 24 01 75 ?? 80 C9 01 88 4F 4F 48 8B 4F 40 F6 C1 03 75 ??",-1,0x0,0),
  new Spec(0x16bcfc0,"40 53 55 57 48 83 EC 50 0F 29 74 24 40 49 8B E8 66 41 0F 6E F1 48 8B F9 F3 0F 11 74 24 20 8B DA 4D 85 C0 75 ?? 32 C0",-1,0x0,0),
  new Spec(0x16bfb40,"40 55 53 57 41 54 48 8D 6C 24 C1 48 81 EC B8 00 00 00 48 8B 59 78 BA 01 00 00 00 4C 8B A1 98 00 00 00 48 8B F9",-1,0x0,0),
  new Spec(0x16d3ed0,"40 53 48 83 EC 30 0F 29 74 24 20 48 8B D9 85 D2 75 ?? E8 ?? ?? ?? ?? 84 C0 74 ?? 48 8B CB E8 ?? ?? ?? ?? 0F 28 F0",-1,0x0,0),
  new Spec(0x16d6250,"48 89 5C 24 08 48 89 7C 24 10 55 48 8D 6C 24 A9 48 81 EC 90 00 00 00 48 8B 81 D8 01 00 00 48 8D 55 E7 0F 57 C0 48 8B D9",-1,0x0,0),
  new Spec(0x16d8b40,"40 53 48 83 EC 40 8B 81 D0 01 00 00 48 8B D9 C1 E8 04 84 D2 75 ?? F6 D0 24 01 84 C0 0F 85 ?? ?? ?? ?? 8B 81 D0 01 00 00 48 89 7C 24 58 84 D2 74 ?? 83 C8 10 EB ?? 83 E0 EF 89 81 D0 01 00 00 BA 12 00 00 00 48 81 C1 B8 00 00 00 E8 ?? ?? ?? ?? 84 C0 0F 84 ?? ?? ?? ?? BA 12 00 00 00",-1,0x0,0),
  new Spec(0x2d51c58,"48 8D 05 ?? ?? ?? ?? 48 8B F9 48 89 01 B2 01 48 8D 05 ?? ?? ?? ?? 48 89 41 10 48 81 C1 10 01 00 00 E8 ?? ?? ?? ?? 33 ED",3,0x0,0),
  new Spec(0x2d522a8,"48 8D 05 ?? ?? ?? ?? 45 33 E4 48 89 07 48 8D 8F C0 09 00 00 48 8D 05 ?? ?? ?? ?? 48 89 47 10 48 8D 15 ?? ?? ?? ?? 33 C0",3,0x0,0),
  new Spec(0x2d609e8,"48 8D 05 ?? ?? ?? ?? 48 C7 44 24 20 00 00 00 00 48 89 01 48 8B F9 48 8D 4C 24 20 C7 44 24 28 00 00 00 00 0F 57 DB",3,0x0,0),
  new Spec(0x2d62e98,"48 8D 05 ?? ?? ?? ?? 33 FF 48 89 01 48 8B D9 48 8D 05 ?? ?? ?? ?? 48 89 41 10 48 89 B9 F8 04 00 00",3,0x0,0),
  new Spec(0x2d6a748,"48 8D 05 ?? ?? ?? ?? 48 89 03 48 8D 05 ?? ?? ?? ?? 48 89 43 10 33 C0 48 89 83 30 01 00 00 89 83 38 01 00 00 48 89 83 48 01 00 00 89 83 40 01 00 00 48 8B C3 C7 83 44 01 00 00 08 00 00 00",3,0x0,0),
  new Spec(0x2d6c370,"48 8D 05 ?? ?? ?? ?? 45 33 FF 48 89 07 4C 8D 77 10 48 8D 05 ?? ?? ?? ?? 49 89 06 48 8D B7 D8 01 00 00",3,0x0,0),
  new Spec(0x2d6f9d0,"48 8D 05 ?? ?? ?? ?? 48 8B D9 48 89 01 33 FF 48 8D 05 ?? ?? ?? ?? 48 89 41 10 48 8B 89 F0 07 00 00 48 85 C9 74 ??",3,0x0,0),
  new Spec(0x2d78b08,"48 8D 05 ?? ?? ?? ?? 33 D2 48 89 03 48 8B CB 48 8D 05 ?? ?? ?? ?? 48 89 43 10 33 C0 48 89 83 D0 01 00 00",3,0x0,0),
  new Spec(0x2d97498,"48 8D 05 ?? ?? ?? ?? 48 89 03 48 8D 8B F8 02 00 00 48 8D 05 ?? ?? ?? ?? 48 89 43 10 E8 ?? ?? ?? ?? 48 8B C3 48 83 C4 20",3,0x0,0),
  new Spec(0x2d97840,"48 8D 05 ?? ?? ?? ?? 33 D2 48 89 03 48 8D 8B 40 01 00 00 48 8D 05 ?? ?? ?? ?? 41 B8 A0 00 00 00 48 89 43 10 33 C0",3,0x0,0),
  new Spec(0x2d99510,"48 8D 05 ?? ?? ?? ?? B2 01 48 89 03 48 8B CB 48 8D 05 ?? ?? ?? ?? 48 89 43 10 33 C0 48 89 83 30 01 00 00 48 89 83 38 01 00 00 89 83 80 01 00 00 48 89 83 40 01 00 00 48 89 83 48 01 00 00",3,0x0,0),
  new Spec(0x2d9ebe0,"48 8D 05 ?? ?? ?? ?? B2 01 48 89 03 48 8B CB 48 8D 05 ?? ?? ?? ?? 48 89 43 10 33 C0 48 89 83 30 01 00 00 48 89 83 38 01 00 00 48 89 83 40 01 00 00 48 89 83 48 01 00 00 48 89 83 50 01 00 00 48 89 83 58 01 00 00 48 89 83 60 01 00 00 48 89 83 68 01 00 00 48 89 83 70 01 00 00 48 89 83 78 01 00 00 89 83 80 01 00 00 E8 ?? ?? ?? ?? 48 8B C3 48 83 C4 30 5B C3 CC 48 83 E9 10 E9 ?? ?? ?? ??",3,0x0,0),
  new Spec(0x2db8098,"48 8D 05 ?? ?? ?? ?? 48 89 07 B9 F0 0D 00 00 48 89 3D ?? ?? ?? ?? 48 89 B7 08 01 00 00 8D 56 10 48 89 B7 10 01 00 00",3,0x0,0),
  new Spec(0x2db9da8,"48 8D 05 ?? ?? ?? ?? 48 89 07 48 8D 8F B8 00 00 00 33 C0 48 89 87 98 00 00 00 48 89 87 A0 00 00 00 48 89 87 A8 00 00 00",3,0x0,0),
  new Spec(0x2dccec8,"48 8D 05 ?? ?? ?? ?? BB 03 00 00 00 48 89 06 48 8D BE E0 01 00 00 48 8D 05 ?? ?? ?? ?? 48 89 46 10",3,0x0,0),
  new Spec(0x2e94880,"48 8D 05 ?? ?? ?? ?? 48 89 07 48 8D 2D ?? ?? ?? ?? 48 89 3B 48 89 73 08 48 89 73 10 48 89 73 18 89 77 48",3,0x0,0),
  new Spec(0x3a902e0,"48 8D 2D ?? ?? ?? ?? 4C 8D 35 ?? ?? ?? ?? 90 48 8B DD BF 00 10 00 00 E8 ?? ?? ?? ?? 89 03 48 8D 5B 04 48 83 EF 01 75 ??",3,0x0,0),
  new Spec(0x4032368,"48 8B 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B C8 48 8B D7 E8 ?? ?? ?? ?? 48 8B 1F 8B 53 28 8B 43 2C 8D 4A 04 3B C8 76 ??",3,0x0,0),
  new Spec(0x4046470,"48 8B 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 85 C0 74 ?? 48 8B C8 E8 ?? ?? ?? ?? 48 8B C8 48 8B D3 48 83 C4 20 5B",3,0x0,0),
  new Spec(0x43d0e08,"48 8B 0D ?? ?? ?? ?? 48 8B D3 48 8B 01 FF 50 30 3D 07 00 06 20 75 ?? 48 8B 0D ?? ?? ?? ?? 48 8B 01 FF 50 60",3,0x0,0),
  new Spec(0x43d1e10,"48 8B 0D ?? ?? ?? ?? 48 83 B9 28 01 00 00 00 0F 84 ?? ?? ?? ?? 66 90 48 8B 89 28 01 00 00 48 8D 54 24 38",3,0x0,0),
  new Spec(0x5a94ed8,"48 39 1D ?? ?? ?? ?? 74 ?? E8 ?? ?? ?? ?? 84 C0 74 ?? E8 ?? ?? ?? ?? 84 C0 75 ?? 8B 0D ?? ?? ?? ?? EB ?? 40 B7 01",3,0x0,0),
  new Spec(0x22a290,"E9 ?? ?? ?? ?? CC CC CC CC CC CC CC CC CC CC CC CC C2 00 00 CC CC CC CC CC CC CC CC CC CC CC CC CC 48 89 5C 24 18 55 56",1,0x0,0),
  new Spec(0x16d8900,"E8 ?? ?? ?? ?? 0F 28 C8 48 8B CF 0F 28 F0 E8 ?? ?? ?? ?? 48 8B 74 24 48 0F 28 C6 0F 28 74 24 20 48 83 C4 30 5F C3",15,0x16a6490,1),
  new Spec(0x16d8a20,"E8 ?? ?? ?? ?? 0F 28 C8 48 8B CF 0F 28 F0 E8 ?? ?? ?? ?? 48 8B 74 24 48 0F 28 C6 0F 28 74 24 20 48 83 C4 30 5F C3",15,0x16a7680,1),
  new Spec(0x2e99200,"48 8D 05 ?? ?? ?? ?? 48 8B D9 48 89 01 48 8D 05 ?? ?? ?? ?? 48 89 41 10 E8 ?? ?? ?? ?? 8B 83 80 16 04 00 33 ED C1 E8 05",3,0x0,0),
 };
 static InvalidOperationException Unsupported(string detail){return new InvalidOperationException("Cannot identify the current game layout: "+detail+". No changes were applied.");}
 static void Range(byte[] b,int p,int n){if(p<0||n<0||p>b.Length-n)throw Unsupported("invalid image range");}
 static uint U32(byte[] b,int p){Range(b,p,4);return BitConverter.ToUInt32(b,p);}
 static ulong Q(byte[] b,int p){Range(b,p,8);return BitConverter.ToUInt64(b,p);}
 static List<Section> Sections(byte[] b){
  if(b.Length<256||b[0]!=0x4d||b[1]!=0x5a)throw Unsupported("invalid executable");
  int pe=checked((int)U32(b,0x3c));Range(b,pe,24);
  if(U32(b,pe)!=0x4550)throw Unsupported("expected a 64-bit executable");
  int count=BitConverter.ToUInt16(b,pe+6),size=BitConverter.ToUInt16(b,pe+20),opt=pe+24;
  Range(b,opt,size);if(size<64||BitConverter.ToUInt16(b,opt)!=0x20b||U32(b,opt+56)!=b.Length||count<1||count>96)throw Unsupported("invalid executable sections");
  var sections=new List<Section>();for(int i=0;i<count;i++){
   int p=opt+size+i*40;Range(b,p,40);int start=checked((int)U32(b,p+12)),length=checked((int)U32(b,p+8));Range(b,start,length);
   sections.Add(new Section{Start=start,End=start+length,Flags=U32(b,p+36)});
  }return sections;
 }
 static bool In(List<Section> sections,ulong p,int n,uint required,uint forbidden){foreach(var s in sections)if(p>=(ulong)s.Start&&p+(ulong)n<=(ulong)s.End&&(s.Flags&required)==required&&(s.Flags&forbidden)==0)return true;return false;}
 static long Relative(byte[] b,int match,int operand){return checked((long)match+operand+4+BitConverter.ToInt32(b,match+operand));}
 static List<int> Find(byte[] b,List<Section> sections,string pattern,uint required,uint forbidden){
  string[] tokens=pattern.Split(' ');var bytes=new byte[tokens.Length];var fixedByte=new bool[tokens.Length];int anchor=-1,run=0,best=0;
  for(int i=0;i<tokens.Length;i++){fixedByte[i]=tokens[i]!="??";if(fixedByte[i]){bytes[i]=Convert.ToByte(tokens[i],16);run++;if(run>best){best=run;anchor=i-run+1;}}else run=0;}
  if(best<2)throw Unsupported("weak signature");var found=new List<int>();
  foreach(var s in sections){if((s.Flags&required)!=required||(s.Flags&forbidden)!=0)continue;int cursor=s.Start+anchor,last=s.End-bytes.Length+anchor;
   while(cursor<=last){int at=Array.IndexOf(b,bytes[anchor],cursor,last-cursor+1);if(at<0)break;cursor=at+1;int start=at-anchor;bool match=true;
    for(int i=1;i<best;i++)if(b[at+i]!=bytes[anchor+i]){match=false;break;}
    if(match)for(int i=0;i<bytes.Length;i++)if(fixedByte[i]&&b[start+i]!=bytes[i]){match=false;break;}
    if(match)found.Add(start);
   }
  }return found;
 }
 public static Dictionary<ulong,ulong> ResolveImage(byte[] image,ulong module){
  var sections=Sections(image);var result=new Dictionary<ulong,ulong>();
  foreach(var spec in Specs){var matches=Find(image,sections,spec.Pattern,0x20000000,0);int accepted=-1;
   foreach(int match in matches){if(spec.Constraint!=0&&Relative(image,match,spec.ConstraintOperand)!=(long)result[spec.Constraint])continue;if(accepted>=0)throw Unsupported("ambiguous target "+spec.Key.ToString("X"));accepted=match;}
   if(accepted<0)throw Unsupported("missing target "+spec.Key.ToString("X"));long value=spec.Operand<0?accepted:Relative(image,accepted,spec.Operand);
   if(value<0||value>=image.Length)throw Unsupported("target outside executable");
   uint required=spec.Key<0x2d01803?0x20000000U:spec.Key<0x3900000?0x40000000U:0x80000000U;
   uint forbidden=spec.Key>=0x2d01803&&spec.Key<0x3900000?0xa0000000U:0;
   if(!In(sections,(ulong)value,8,required,forbidden))throw Unsupported("target section mismatch");result.Add(spec.Key,(ulong)value);
  }
  // The refresh function addresses both booleans; verify their relationship.
  int setter=checked((int)result[0x22a290]);Range(image,setter,13);
  if(image[setter]!=0x88||image[setter+1]!=0x0d||image[setter+6]!=0x88||image[setter+7]!=0x15||image[setter+12]!=0xc3)throw Unsupported("display refresh shape");
  long flag=Relative(image,setter,2);if(Relative(image,setter,8)!=flag+1||flag<0||!In(sections,(ulong)flag,2,0x80000000,0))throw Unsupported("display refresh fields");result.Add(0x3a12701,(ulong)flag);
  // Identify the static game-state instance by its independently resolved type.
  // Its native flag accessor provides the enclosing member offset. The path
  // string starts 0x38 bytes into that member, beside its length fields.
  ulong type=result[0x2e99200];ulong getter=Q(image,checked((int)type+0xd0))-module;
  if(!In(sections,getter,13,0x20000000,0))throw Unsupported("map accessor");int g=checked((int)getter);
  byte[] shape={0x8b,0x81,0,0,0,0,0x48,0xd1,0xe8,0x83,0xe0,1,0xc3};for(int i=0;i<shape.Length;i++)if((i<2||i>=6)&&image[g+i]!=shape[i])throw Unsupported("map member layout");
  uint member=U32(image,g+2);if(member<0x100||member>0x1000000)throw Unsupported("map member bounds");
  byte[] pointer=BitConverter.GetBytes(module+type);string pointerPattern=BitConverter.ToString(pointer).Replace('-',' ');var objects=Find(image,sections,pointerPattern,0x80000000,0x20000000);
  if(objects.Count!=1)throw Unsupported("ambiguous game-state instance");ulong map=(ulong)objects[0]+member+0x38;
  if(!In(sections,map-16,276,0x80000000,0x20000000))throw Unsupported("map field bounds");result.Add(0x5704068,map);
  // Validate the native methods used when copying and replacing vtables.
  ulong[,] relations={{0x2db8098,0x28,0xe60f70},{0x2d51c58,0x148,0x16b53c0},{0x2d99510,0x150,0xdc31c0},{0x2d97840,0x150,0xdba490},{0x2dccec8,0x138,0x16d3ed0},{0x2dccec8,0x148,0x16d6250},{0x2e94880,8,0x14b4090},{0x2d609e8,0x70,0xbf7840}};
  for(int i=0;i<relations.GetLength(0);i++)if(Q(image,checked((int)(result[relations[i,0]]+relations[i,1])))!=module+result[relations[i,2]])throw Unsupported("native method relationship");
  result.Add(0x2e94888,result[0x2e94880]+8);return result;
 }
 public static void Ensure(int pid,ulong module){lock(Sync){using(var p=Process.GetProcessById(pid)){
  long started=p.StartTime.ToUniversalTime().Ticks;
  if(current!=null&&activePid==pid&&activeModule==module&&activeStarted==started)return;
  current=null;activeSections=null;
  var main=p.MainModule;if(!String.Equals(Path.GetFileName(main.FileName),"SC2_x64.exe",StringComparison.OrdinalIgnoreCase)||(ulong)main.BaseAddress.ToInt64()!=module)throw Unsupported("game identity changed");
  int length=main.ModuleMemorySize;if(length<0x100000||length>0x40000000)throw Unsupported("image size");
  IntPtr h=OpenProcess(0x410,false,pid);if(h==IntPtr.Zero)throw Unsupported("cannot read game");
  try{var image=new byte[length];var page=new byte[4096];for(int offset=0;offset<length;offset+=page.Length){int n=Math.Min(page.Length,length-offset);UIntPtr got;if(ReadProcessMemory(h,(IntPtr)(long)(module+(ulong)offset),page,(UIntPtr)n,out got)&&got.ToUInt64()==(ulong)n)Buffer.BlockCopy(page,0,image,offset,n);}
   var resolved=ResolveImage(image,module);if(p.HasExited||p.StartTime.ToUniversalTime().Ticks!=started)throw Unsupported("game exited");
   activeSections=Sections(image);activePid=pid;activeModule=module;activeStarted=started;current=resolved;
  }finally{CloseHandle(h);}
 }}}
 public static ulong Rva(ulong key){var values=current;ulong value;if(values==null||!values.TryGetValue(key,out value))throw Unsupported("addresses have not been resolved");return value;}
 public static bool IsReadOnlyAddress(ulong module,ulong address){return address>=module&&activeSections!=null&&In(activeSections,address-module,8,0x40000000,0xa0000000);}
#if SC2_TESTS
 public static void UseTestAddresses(){var values=new Dictionary<ulong,ulong>();foreach(var s in Specs)values[s.Key]=s.Key;values[0x5704068]=0x5704068;values[0x3a12701]=0x3a12701;values[0x2e94888]=0x2e94888;current=values;}
#endif
}
