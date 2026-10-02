using System;
using System.Collections.Generic;
public static class AutoStartTests {
 static void Need(bool value,string text){if(!value)throw new Exception(text);}
 public static void Run(){
  const ulong module=0x140000000,device=0x20000000,state=0x21000000,code=0x22000000;
  ulong table=state+0x108,native=module+0x2DB8098;
  var original=new byte[0x1A0];for(int i=0;i<original.Length;i++)original[i]=(byte)(i*13);
  Array.Copy(BitConverter.GetBytes(module+0xE60F70),0,original,0x30,8);
  var clone=(byte[])original.Clone();Array.Copy(BitConverter.GetBytes(code),0,clone,0x30,8);
  var callback=SC2CampaignModeHook.BuildMode(state,module+0xE60F70,3440,1440,module+SC2CampaignGate.SessionRva,module+SC2CampaignGate.MapRva,module+SC2HubGate.SceneRva);
  var memory=new Dictionary<ulong,byte[]>{{device,BitConverter.GetBytes(table)},{state,BitConverter.GetBytes(1)},{table-8,clone},{native-8,original},{code,callback}};
  Func<ulong,int,byte[]> read=(a,n)=>{byte[] bytes;if(!memory.TryGetValue(a,out bytes)||bytes.Length<n)return null;var result=new byte[n];Array.Copy(bytes,result,n);return result;};
  Func<SC2AutoStart.Result> inspect=()=>SC2AutoStart.InspectExisting(read,module,device,3440,1440);
  var found=inspect();Need(found!=null&&found.State==state&&found.Code==code&&found.VTable==table,"Recover matching hook without a session record");
  memory[device]=BitConverter.GetBytes(native);Need(inspect()==null,"Native device needs normal attachment");memory[device]=BitConverter.GetBytes(table);
  clone[0]^=1;Need(inspect()==null,"Changed RTTI rejected");clone[0]^=1;
  clone[0x48]^=1;Need(inspect()==null,"Another modified vtable method rejected");clone[0x48]^=1;
  callback[callback.Length-2]^=1;Need(inspect()==null,"Changed native callback rejected");callback[callback.Length-2]^=1;
  Need(SC2AutoStart.InspectExisting(read,module,device,2560,1440)==null,"Different target dimensions rejected");
  memory[state]=BitConverter.GetBytes(0);Need(inspect()==null,"Disabled hook rejected");memory[state]=BitConverter.GetBytes(1);
  memory[code]=new byte[1];Need(inspect()==null,"Unreadable or partial callback rejected");memory[code]=callback;
  int deviceReads=0;Func<ulong,int,byte[]> changing=(a,n)=>{if(a==device&&++deviceReads==2)return BitConverter.GetBytes(native);return read(a,n);};
  Need(SC2AutoStart.InspectExisting(changing,module,device,3440,1440)==null,"Device change during verification rejected");
  Need(inspect()!=null,"Matching hook remains recoverable");
  Console.WriteLine("PASS: late attachment recovers a matching existing hook without its record; native/foreign/disabled/changed/partial/different-resolution hooks are not adopted.");
 }
}
