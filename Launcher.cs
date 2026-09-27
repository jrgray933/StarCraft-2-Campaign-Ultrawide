using System;

using System.IO;

using System.Linq;

using System.Text;

using System.Reflection;

using System.Diagnostics;

using System.Threading;

using System.Threading.Tasks;

using System.Collections.Generic;

using System.Drawing;

using System.Windows.Forms;

using System.Web.Script.Serialization;

using System.Security.Cryptography;

using System.Runtime.InteropServices;

[assembly: AssemblyTitle("StarCraft II Campaign Ultrawide")]

[assembly: AssemblyDescription("Single-player campaign display helper")]

[assembly: AssemblyVersion("1.6.1.0")]

static class Engine {

 internal static string Root;

 internal const string WorkerMutex = @"Local\SC2CampaignAuto97563";

 internal static string PowerShell { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"System32\WindowsPowerShell\v1.0\powershell.exe"); } }

 internal static void Extract() {

  Assembly a=Assembly.GetExecutingAssembly();

  var files=new SortedDictionary<string,byte[]>();

  foreach(string name in a.GetManifestResourceNames().Where(n=>n.StartsWith("Engine."))) {

   using(var input=a.GetManifestResourceStream(name)) using(var output=new MemoryStream()) { input.CopyTo(output); files.Add(name.Substring(7),output.ToArray()); }

  }

  if(files.Count!=12 || !files.ContainsKey("CampaignGate.cs") || !files.ContainsKey("CampaignDisplayRefresh.cs")) throw new Exception("The embedded engine is incomplete.");

  string version;

  using(var all=new MemoryStream()) {

   foreach(var item in files) { var name=Encoding.UTF8.GetBytes(item.Key); all.Write(name,0,name.Length); all.Write(item.Value,0,item.Value.Length); }

   using(var hash=SHA256.Create()) version=BitConverter.ToString(hash.ComputeHash(all.ToArray())).Replace("-","").Substring(0,16);

  }

  Root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SC2CampaignUltrawide",version);

  Directory.CreateDirectory(Root);

  foreach(var item in files) {

   string path=Path.Combine(Root,item.Key);

   if(File.Exists(path) && File.ReadAllBytes(path).SequenceEqual(item.Value)) continue;

   File.WriteAllBytes(path,item.Value);

  }

 }

 internal static bool Running() {

  try { using(var m=Mutex.OpenExisting(WorkerMutex)) {

   bool acquired=false;

   try { acquired=m.WaitOne(0); } catch(AbandonedMutexException) { acquired=true; }

   if(acquired) m.ReleaseMutex();

   return !acquired;

  }} catch(WaitHandleCannotBeOpenedException) { return false; }

 }

 internal static Process Start(string script,string arguments,Action<string> output) {

  var p=new Process();

  p.StartInfo=new ProcessStartInfo(PowerShell,"-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File \""+Path.Combine(Root,script)+"\" "+arguments) {

   UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=Root,RedirectStandardOutput=true,RedirectStandardError=true

  };

  p.OutputDataReceived+=(s,e)=>{if(e.Data!=null) output(e.Data);};

  p.ErrorDataReceived+=(s,e)=>{if(e.Data!=null) output(e.Data);};

  p.Start();
  if(script=="Auto-Campaign.ps1")File.WriteAllText(Path.Combine(Root,"worker-process.json"),new JavaScriptSerializer().Serialize(new WorkerRecord{Pid=p.Id,Started=p.StartTime.ToUniversalTime().Ticks}));
  p.BeginOutputReadLine();p.BeginErrorReadLine();return p;

 }

 internal static async Task<int> Run(string action,Action<string> output) {

  using(var p=Start("Control.ps1","-Action "+action,output)) { await Task.Run(()=>p.WaitForExit());return p.ExitCode; }

 }

 internal static Dictionary<string,object> ParseStatus(string json){
  if(String.IsNullOrWhiteSpace(json))return null;
  try{var state=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(json);object phase;
   return state!=null&&state.TryGetValue("Phase",out phase)&&phase is string&&!String.IsNullOrWhiteSpace((string)phase)?state:null;
  }catch(ArgumentException){return null;}catch(InvalidOperationException){return null;}
 }
 internal static string ReadyDetail(Dictionary<string,object> state){
  object detail;var d=state.TryGetValue("Detail",out detail)?detail as Dictionary<string,object>:null;object width,height,hud,scale;
  if(d==null||!d.TryGetValue("Width",out width)||width==null||!d.TryGetValue("Height",out height)||height==null)return "Your display and HUD are ready.";
  d.TryGetValue("Hud",out hud);d.TryGetValue("HudScale",out scale);
  return width+" \u00d7 "+height+(Convert.ToString(hud)=="Standard"?" \u00b7 Standard HUD":" \u00b7 HUD "+(scale==null?"100":Convert.ToString(scale))+"% \u00b7 Full battlefield view");
 }
 internal static string StatusSelfTest(){
  foreach(string value in new[]{""," ","null","{","{}","{\"Phase\":null}","{\"Phase\":[]}"})if(ParseStatus(value)!=null)throw new Exception("Incomplete status accepted");
  foreach(string value in new[]{"{\"Phase\":\"Ready\"}","{\"Phase\":\"Ready\",\"Detail\":null}","{\"Phase\":\"Ready\",\"Detail\":{}}","{\"Phase\":\"Ready\",\"Detail\":{\"Width\":3440,\"Height\":1440,\"HudScale\":null}}"})if(String.IsNullOrEmpty(ReadyDetail(ParseStatus(value))))throw new Exception("Ready status formatting");
  return "PASS: empty, truncated, null and incomplete status updates are safe; optional ready details are safe.";
 }
 internal sealed class WorkerRecord {public int Pid {get;set;}public long Started {get;set;}}
 internal static Process RecordedWorker(){
  try{var r=new JavaScriptSerializer().Deserialize<WorkerRecord>(File.ReadAllText(Path.Combine(Root,"worker-process.json")));if(r==null)return null;var p=Process.GetProcessById(r.Pid);
   if(!p.HasExited&&p.StartTime.ToUniversalTime().Ticks==r.Started&&String.Equals(p.MainModule.FileName,PowerShell,StringComparison.OrdinalIgnoreCase))return p;p.Dispose();
  }catch(IOException){}catch(ArgumentException){}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}
  return null;
 }
 internal static void RequestStop() { File.WriteAllText(Path.Combine(Root,"auto-campaign-stop.request"),"stop"); }

}

sealed class Resolution {

 public int Width {get;set;} public int Height {get;set;} public int HudScale {get;set;}
 public Resolution(){HudScale=100;}

 public bool Desktop {get;set;}

 public override string ToString(){

  string aspect=Math.Abs((double)Width/Height-16.0/9)<0.01?"16:9":Math.Abs((double)Width/Height-32.0/9)<0.01?"32:9":"Ultrawide";

  return Width+" \u00d7 "+Height+"   \u00b7   "+aspect+(Desktop?"   (desktop)":"");

 }

}

static class DisplayModes {

 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct Mode {

  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string Device;

  public ushort Spec,Driver,Size,Extra;public uint Fields;

  public int X,Y;public uint Orientation,Fixed;

  public short Color,Duplex,YResolution,TT,Collate;

  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string Form;

  public ushort LogPixels;public uint Bits,Width,Height,Flags,Frequency,IcmMethod,IcmIntent,Media,Dither,Reserved1,Reserved2,PanningWidth,PanningHeight;

 }

 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern bool EnumDisplaySettings(string device,int number,ref Mode mode);

 public static List<Resolution> Available(){

  var result=new List<Resolution>();var screen=Screen.PrimaryScreen;

  for(int i=0;i<2048;i++){

   var m=new Mode{Size=(ushort)Marshal.SizeOf(typeof(Mode))};if(!EnumDisplaySettings(screen.DeviceName,i,ref m))break;

   int w=(int)m.Width,h=(int)m.Height;

   if(w<1280||w>7680||h<720||h>4320||w*9<h*16||w*9>h*32||m.Bits<24)continue;

   if(!result.Any(r=>r.Width==w&&r.Height==h))result.Add(new Resolution{Width=w,Height=h,Desktop=w==screen.Bounds.Width&&h==screen.Bounds.Height});

  }

  return result.OrderByDescending(r=>r.Desktop).ThenByDescending(r=>r.Width*r.Height).ThenByDescending(r=>r.Width).ToList();

 }

}

class Launcher : Form {

 Label status=new Label(),detail=new Label(),choiceHint=new Label();

 Button play,stop,more;

 ComboBox resolutions=new ComboBox(),hudSizes=new ComboBox();
 int HudScale {get{return hudSizes.SelectedIndex<0?100:50+5*hudSizes.SelectedIndex;}}
 bool ScaleFits {get{return Selected==null||Selected.Width*9L*100>=Selected.Height*16L*HudScale;}}

 FlowLayoutPanel options;

 NotifyIcon tray=new NotifyIcon();

 System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();

 Process worker;

 bool busy,quitting,smoke,initializing=true,automaticEnabled=true;
 string lastAutoGame="";
 Dictionary<string,object> lastStatus;
 Button recheck;

 string notice="",lastPhase="";

 readonly object outputLock=new object();

 List<Resolution> available;

 string settingsPath {get{return Path.Combine(Directory.GetParent(Engine.Root).FullName,"settings.json");}}

 Resolution Selected {get{return resolutions.SelectedItem as Resolution;}}

 static Button Button(string text,EventHandler action){var b=new Button {Text=text,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Padding=new Padding(13,7,13,7),Margin=new Padding(0,0,10,8)};b.Click+=action;return b;}

 static Label TextLabel(string text,float size,bool bold){return new Label {Text=text,AutoSize=true,MaximumSize=new Size(590,0),Font=new Font("Segoe UI",size,bold?FontStyle.Bold:FontStyle.Regular),Margin=new Padding(0,0,0,12)};}

 static FlowLayoutPanel Flow(){return new FlowLayoutPanel {AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Dock=DockStyle.Fill,WrapContents=true,Margin=new Padding(0)};}

 internal Launcher(bool test) {

  smoke=test;Text="StarCraft II Campaign Ultrawide";Font=new Font("Segoe UI",10);AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;

  ClientSize=new Size(680,620);MinimumSize=new Size(650,650);StartPosition=FormStartPosition.CenterScreen;

  var scroll=new Panel {Dock=DockStyle.Fill,AutoScroll=true};

  var layout=new TableLayoutPanel {Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,ColumnCount=1,Padding=new Padding(24),Margin=new Padding(0)};

  layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));

  Action<Control> row=c=>{int n=layout.RowCount++;layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.Controls.Add(c,0,n);};

  row(TextLabel("Campaign Ultrawide",21,true));

  row(TextLabel("Resolution",10,true));

  resolutions.DropDownStyle=ComboBoxStyle.DropDownList;resolutions.Dock=DockStyle.Fill;resolutions.Margin=new Padding(0,0,0,10);resolutions.DropDownWidth=540;

  available=DisplayModes.Available();foreach(var r in available)resolutions.Items.Add(r);if(available.Count>0)resolutions.SelectedIndex=0;

  for(int scale=50;scale<=125;scale+=5)hudSizes.Items.Add(scale+"%");hudSizes.SelectedIndex=10;
  try {if(File.Exists(settingsPath)){var saved=new JavaScriptSerializer().Deserialize<Resolution>(File.ReadAllText(settingsPath));var match=saved==null?null:available.FirstOrDefault(r=>r.Width==saved.Width&&r.Height==saved.Height);if(match!=null)resolutions.SelectedItem=match;if(saved!=null&&saved.HudScale>=50&&saved.HudScale<=125&&saved.HudScale%5==0)hudSizes.SelectedIndex=(saved.HudScale-50)/5;}}catch(IOException){}catch(ArgumentException){}

  resolutions.SelectedIndexChanged+=(s,e)=>{if(!initializing){SaveSelection();notice="";RefreshState();}};row(resolutions);

  choiceHint.AutoSize=false;choiceHint.Height=40;choiceHint.Dock=DockStyle.Top;choiceHint.MaximumSize=new Size(590,0);choiceHint.ForeColor=Color.FromArgb(80,80,80);choiceHint.Margin=new Padding(0,0,0,18);row(choiceHint);

  row(TextLabel("Bottom HUD size",10,true));hudSizes.DropDownStyle=ComboBoxStyle.DropDownList;hudSizes.Dock=DockStyle.Fill;hudSizes.Margin=new Padding(0,0,0,18);
  hudSizes.SelectedIndexChanged+=(s,e)=>{if(!initializing){SaveSelection();notice="";RefreshState();}};row(hudSizes);

  var card=new TableLayoutPanel {ColumnCount=1,RowCount=2,AutoSize=false,Height=112,Dock=DockStyle.Top,BackColor=Color.FromArgb(238,244,248),Padding=new Padding(16),Margin=new Padding(0,0,0,18)};

  card.RowStyles.Add(new RowStyle(SizeType.Absolute,24));card.RowStyles.Add(new RowStyle(SizeType.Percent,100));
  status.AutoSize=false;status.Dock=DockStyle.Fill;status.MaximumSize=new Size(554,0);status.Font=new Font(Font,FontStyle.Bold);status.Margin=new Padding(0,0,0,7);

  detail.AutoSize=false;detail.Dock=DockStyle.Fill;detail.MaximumSize=new Size(554,0);detail.Margin=new Padding(0);card.Controls.Add(status,0,0);card.Controls.Add(detail,0,1);row(card);

  var main=Flow();play=Button("Enable",async(s,e)=>await StartWorker());stop=Button("Stop automatic setup",async(s,e)=>await StopWorker());main.Controls.Add(play);main.Controls.Add(stop);recheck=Button("Recheck game",async(s,e)=>{await StopWorker();if(!Engine.Running()&&!WorkerAlive())await StartWorker();});main.Controls.Add(recheck);row(main);

  var footer=Flow();more=Button("More options",(s,e)=>{options.Visible=!options.Visible;more.Text=options.Visible?"Fewer options":"More options";});footer.Controls.Add(more);footer.Controls.Add(Button("Help",(s,e)=>ShowHelp()));row(footer);

  options=Flow();options.Visible=false;

  options.Controls.Add(Button("Resume automatic setup",async(s,e)=>await StartWorker()));

  options.Controls.Add(Button("Reapply HUD",async(s,e)=>{await StopWorker();await StartWorker();}));

  options.Controls.Add(Button("Restore original HUD",async(s,e)=>await ManualAction("RestoreHUD")));

  options.Controls.Add(Button("Use game resolution",async(s,e)=>await ManualAction("DisableResolution")));

  options.Controls.Add(Button("Reapply selected resolution",async(s,e)=>{await StopWorker();await StartWorker();}));

  options.Controls.Add(Button("Open activity log",(s,e)=>{string p=Path.Combine(Engine.Root,"activity.log");var chunks=new List<string>();foreach(string name in new[]{"launcher.log","auto-campaign.log"}){string path=Path.Combine(Engine.Root,name);if(File.Exists(path))chunks.Add(File.ReadAllText(path));}File.WriteAllText(p,chunks.Count==0?"No activity recorded yet.":String.Join(Environment.NewLine,chunks));Process.Start(new ProcessStartInfo(p){UseShellExecute=true});}));row(options);

  scroll.Controls.Add(layout);Controls.Add(scroll);

  layout.SizeChanged+=(s,e)=>{int w=Math.Max(240,layout.ClientSize.Width-layout.Padding.Horizontal);foreach(Control c in layout.Controls){var label=c as Label;if(label!=null)label.MaximumSize=new Size(w,0);}status.MaximumSize=new Size(Math.Max(220,w-32),0);detail.MaximumSize=status.MaximumSize;};

  tray.Icon=SystemIcons.Application;tray.Text="Campaign Ultrawide";tray.DoubleClick+=(s,e)=>Reveal();

  tray.ContextMenuStrip=new ContextMenuStrip();tray.ContextMenuStrip.Items.Add("Open Campaign Ultrawide",null,(s,e)=>Reveal());

  tray.ContextMenuStrip.Items.Add("Exit",null,async(s,e)=>{if(busy)return;await StopWorker();if(!Engine.Running()){quitting=true;Close();}});

  timer.Interval=1000;timer.Tick+=async(s,e)=>{RefreshState();await CheckForRunningGame();};timer.Start();

  FormClosing+=(s,e)=>{if(!quitting&&!smoke&&(busy||Engine.Running()||WorkerAlive())){e.Cancel=true;Hide();tray.Visible=true;tray.ShowBalloonTip(2500,"Campaign Ultrawide","Automatic setup is still running. Double-click this icon to reopen.",ToolTipIcon.Info);}};

  FormClosed+=(s,e)=>{timer.Dispose();tray.Dispose();};

  initializing=false;RefreshState();

  Shown+=async(s,e)=>{RefreshState();if(!smoke)await CheckForRunningGame();if(smoke){var t=new System.Windows.Forms.Timer{Interval=800};t.Tick+=(a,b)=>{t.Stop();t.Dispose();try{LayoutTest();}catch(Exception ex){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"layout-test-results.txt"),ex.ToString());Environment.ExitCode=1;}quitting=true;Close();};t.Start();}};

 }

 void SaveSelection(){if(Selected==null||!ScaleFits)return;Selected.HudScale=HudScale;try{File.WriteAllText(Path.Combine(Engine.Root,"resolution.json"),new JavaScriptSerializer().Serialize(Selected));Directory.CreateDirectory(Directory.GetParent(settingsPath).FullName);File.WriteAllText(settingsPath,new JavaScriptSerializer().Serialize(Selected));}catch(IOException){notice="Your selection could not be saved. You can still choose it before playing.";}}

 bool WorkerAlive(){try{return worker!=null&&!worker.HasExited;}catch{return false;}}

 bool GameOpen(){var games=Process.GetProcessesByName("SC2_x64");bool found=games.Length>0;foreach(var g in games)g.Dispose();return found;}

 void Reveal(){Show();WindowState=FormWindowState.Normal;Activate();tray.Visible=false;}

 void Append(string text){lock(outputLock){try{File.AppendAllText(Path.Combine(Engine.Root,"launcher.log"),DateTime.Now.ToString("s")+" "+text+Environment.NewLine);}catch(IOException){}}}

 string GameIdentity(){
  var games=Process.GetProcessesByName("SC2_x64");try{return games.Length==1?games[0].Id+":"+games[0].StartTime.ToUniversalTime().Ticks:"";}catch(InvalidOperationException){return "";}catch(System.ComponentModel.Win32Exception){return "";}finally{foreach(var g in games)g.Dispose();}
 }
 async Task CheckForRunningGame(){
  if(smoke||!automaticEnabled||busy||hudSizes.DroppedDown||resolutions.DroppedDown)return;
  string game=GameIdentity();if(game.Length==0){lastAutoGame="";return;}
  if(Engine.Running()||WorkerAlive()||game==lastAutoGame)return;
  lastAutoGame=game;await StartWorker();
 }
 void RefreshState(){

  if(hudSizes.DroppedDown||resolutions.DroppedDown)return;
  string nextStatus="",nextDetail="",nextHint="";
  bool running=Engine.Running()||WorkerAlive(),game=GameOpen();

  resolutions.Enabled=!busy&&!running&&!game;play.Enabled=!busy&&!running&&Selected!=null&&ScaleFits;hudSizes.Enabled=!busy;play.Text="Enable";stop.Enabled=!busy&&running;recheck.Enabled=!busy&&Selected!=null&&ScaleFits;

  foreach(Control c in options.Controls)c.Enabled=!busy;

  nextHint=game||running?"Close StarCraft II to change resolution.":"Choose a widescreen resolution supported by your primary display. Your choice is saved for next time.";

  nextStatus=busy?"Applying your changes\u2026":running?"Preparing your campaign\u2026":game?"StarCraft II is open":"Ready to play";

  nextDetail=busy?"Return to the mission to let the game apply any HUD changes.":game&&!running?"Choose Enable, then return to your campaign mission.":"Choose Enable, then start StarCraft II and load a single-player campaign mission.";

  if(!running&&!busy&&!String.IsNullOrEmpty(notice))nextDetail=notice;

  if(!ScaleFits){nextStatus="HUD size needs a wider resolution";nextDetail="Choose a smaller bottom HUD size to fit this resolution.";}
  if(Selected==null){nextStatus="No compatible resolutions found";nextDetail="Use a display with a widescreen resolution of at least 1280 \u00d7 720.";}

  try{

   string path=Path.Combine(Engine.Root,"auto-campaign-state.json");

   if(File.Exists(path)){

    var state=Engine.ParseStatus(File.ReadAllText(path))??lastStatus;if(state!=null){lastStatus=state;string phase=(string)state["Phase"];

    if(running&&!busy){
     if(phase=="Stopped"||phase=="Game closed"){nextStatus=game?"StarCraft II found":"Waiting for StarCraft II";nextDetail="Use Recheck game to refresh automatic setup.";}

     if(phase=="Waiting for StarCraft II"){nextStatus=game?"StarCraft II found":"Waiting for StarCraft II";nextDetail=game?"Checking your campaign. Use Recheck game if setup does not continue.":"Start StarCraft II yourself, then load your campaign mission.";}

     else if(phase=="Attaching resolution hook"||phase=="Preparing"){nextStatus="Setting up your display\u2026";nextDetail="Setup waits until an offline campaign mission is loaded.";}

     else if(phase=="Waiting for offline campaign"){nextStatus="Waiting for an offline campaign";nextDetail="The fix stays inactive until an offline session and campaign map are confirmed.";}

     else if(phase=="Preparing campaign display"||phase=="Applying campaign resolution"){nextStatus="Setting your resolution\u2026";nextDetail="Keep the campaign visible for a few seconds. A brief screen refresh is normal.";}

     else if(phase=="Return to campaign"){nextStatus="Return to your campaign";nextDetail="Automatic setup will continue when the mission is visible.";}

     else if(phase=="Display setup paused"){nextStatus="Display setup could not finish";nextDetail="Choose Recheck game, then return to your campaign to retry.";}

     else if(phase=="Waiting for fullscreen"){nextStatus="Waiting for fullscreen mode";nextDetail="Choose fullscreen in the game\u2019s Graphics options, then close and reopen the game yourself.";}

     else if(phase=="Waiting for campaign mission"){nextStatus="Ready";nextDetail="Load a campaign mission to finish setting up the HUD.";}

     else if(phase=="Applying campaign HUD"){nextStatus="Adjusting the HUD\u2026";nextDetail="Return to the mission. The HUD will move into place automatically.";}

     else if(phase=="Applying HUD size"){nextStatus="Resizing your HUD\u2026";nextDetail="Return to the campaign to apply the selected size.";}
     else if(phase=="Ready"){

      nextStatus="Ready to play";nextDetail=Engine.ReadyDetail(state);

     }

    }

    if(!busy&&phase=="Error"&&(running||worker!=null)){nextStatus="Setup could not finish";nextDetail="Choose Recheck game to retry. If it continues, check the activity log under More options.";}

    lastPhase=phase;
    }

   }

  }catch(IOException){}catch(ArgumentException){}catch(InvalidOperationException){}
  if(!ScaleFits){nextStatus="HUD size needs a wider resolution";nextDetail="Choose a smaller bottom HUD size to fit this resolution.";}

  if(choiceHint.Text!=nextHint)choiceHint.Text=nextHint;
  if(status.Text!=nextStatus)status.Text=nextStatus;
  if(detail.Text!=nextDetail)detail.Text=nextDetail;

 }

 async Task StartWorker(){

  if(busy||Engine.Running()||WorkerAlive()||Selected==null||!ScaleFits)return;
  automaticEnabled=true;lastAutoGame=GameIdentity();busy=true;RefreshState();

  try{

   SaveSelection();File.WriteAllText(Path.Combine(Engine.Root,"resolution.json"),new JavaScriptSerializer().Serialize(Selected));

   lastStatus=null;var state=Path.Combine(Engine.Root,"auto-campaign-state.json");if(File.Exists(state))File.Delete(state);

   notice="";worker=Engine.Start("Auto-Campaign.ps1","-TargetWidth "+Selected.Width+" -TargetHeight "+Selected.Height+" -HudScale "+HudScale,Append);

   Append("Automatic setup started for "+Selected.Width+"x"+Selected.Height+".");RefreshState();await Task.Delay(200);RefreshState();

  }catch(Exception ex){Append(ex.ToString());notice=ex.Message;MessageBox.Show(this,ex.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Information);RefreshState();}finally{busy=false;RefreshState();}

 }

 async Task StopWorker(){

  if(busy)return;automaticEnabled=false;busy=true;RefreshState();

  try{

   Engine.RequestStop();for(int i=0;i<100&&(Engine.Running()||WorkerAlive());i++)await Task.Delay(100);

   if(Engine.Running()||WorkerAlive()){
    // Recover only a child we launched, or an exact PID/start-time record from this engine.
    using(var known=Engine.RecordedWorker()){var stuck=WorkerAlive()?worker:known;if(stuck!=null&&!stuck.HasExited){Append("Restarting unresponsive automatic setup.");stuck.Kill();await Task.Run(()=>stuck.WaitForExit(3000));}}
   }
   if(Engine.Running()||WorkerAlive())throw new Exception("Automatic setup is still stopping. Wait a moment, then choose Recheck game.");

   notice="Automatic setup is stopped. Your current display and HUD stay in place until you restore them or close the game.";Append(notice);

  }catch(Exception ex){notice=ex.Message;Append(ex.ToString());}finally{busy=false;RefreshState();}

 }

 async Task ManualAction(string action){

  if(busy||Selected==null)return;

  if(!GameOpen()){notice="Open a campaign mission before using this option.";RefreshState();return;}

  await StopWorker();if(Engine.Running()||WorkerAlive())return;

  busy=true;RefreshState();

  try{

   File.WriteAllText(Path.Combine(Engine.Root,"resolution.json"),new JavaScriptSerializer().Serialize(Selected));

   int code=await Engine.Run(action,Append);

   if(code!=0)notice="This change could not finish. Return to your mission and try again, or restart the game and choose Enable.";

   else if(action=="RestoreHUD")notice="The original HUD has been restored. Automatic setup remains stopped.";

   else if(action=="DisableResolution")notice="Choose a resolution in the game's Graphics options, or close the game. Automatic setup remains stopped.";

   else if(action=="EnableResolution")notice="Return to your campaign to finish automatic setup.";

   else notice="Return to the mission to see your centered HUD. Automatic setup remains stopped.";

  }catch(Exception ex){Append(ex.ToString());notice="This change could not finish. See the activity log for details.";}finally{busy=false;RefreshState();}

 }

 void ShowHelp(){MessageBox.Show(this,"ENABLE CAMPAIGN ULTRAWIDE\n1. Choose your resolution and bottom HUD size, then click Enable.\n2. Start StarCraft II yourself and load a campaign mission, or return to a mission already open.\n3. Keep the mission visible for a few seconds. The resolution and HUD apply automatically.\n\nThe fix activates only when an offline game session and a campaign map are confirmed. After loading your mission, the helper requests a normal display refresh and adjusts the HUD. There is no need to switch resolutions manually. Use fullscreen display mode. Resolution choices come from your primary display; HUD sizes run from 50% to 125% in 5% steps. You can change HUD size while enabled; return to the mission to see the change. Objectives and dialogs keep their normal size. Larger HUD sizes require enough horizontal space. Close the game before choosing a different resolution.\n\nWHILE PLAYING\nClosing this window keeps automatic setup in the notification area. It stops monitoring when the game closes. Use the tray menu to exit the app.\n\nRESTORE YOUR SETTINGS\nMore options lets you restore the original HUD or use the game's resolution. Return to the mission so HUD changes can finish. Closing StarCraft II removes all session changes. Leaving campaign blocks further application; an existing display size can remain until the next display reset or game exit.\n\nTROUBLESHOOTING\nIf setup stalls, choose Recheck game. It restarts automatic setup without closing StarCraft II. Opening this app while StarCraft II is running starts setup automatically. Choosing Stop automatic setup keeps it stopped until you choose Enable or Recheck game. Other display sizes may depend on your monitor and graphics settings. The activity log under More options can help diagnose problems.\n\nFor single-player campaigns.","Help",MessageBoxButtons.OK,MessageBoxIcon.Information);}

 void LayoutTest(){

  var lines=new List<string>();lines.Add(Engine.StatusSelfTest());
  if(hudSizes.Items.Count!=16)throw new Exception("HUD size option count");
  for(int i=0;i<16;i++)if(hudSizes.Items[i].ToString()!=(50+5*i)+"%")throw new Exception("HUD size increment");
  lines.Add("PASS: HUD selector contains 50%-125% in 5% steps.");
  if(recheck==null||recheck.Text!="Recheck game")throw new Exception("Missing game recheck button");
  lines.Add("PASS: Recheck game is available in the main controls.");
  var oldStatus=status.Text;var oldDetail=detail.Text;var oldHint=choiceHint.Text;
  var buttons=Descendants(this).OfType<Button>().ToArray();var positions=buttons.Select(b=>b.RectangleToScreen(b.ClientRectangle)).ToArray();
  foreach(string text in new[]{"Ready","Preparing your campaign...","Waiting for an offline campaign"}){status.Text=text;detail.Text="The fix stays inactive until an offline session and campaign map are confirmed.";choiceHint.Text="Choose a widescreen resolution supported by your primary display. Your choice is saved for next time.";PerformLayout();for(int i=0;i<buttons.Length;i++)if(buttons[i].RectangleToScreen(buttons[i].ClientRectangle)!=positions[i])throw new Exception("Status update moved a button");}
  status.Text=oldStatus;detail.Text=oldDetail;choiceHint.Text=oldHint;
  hudSizes.DroppedDown=true;for(int i=0;i<10;i++)RefreshState();if(!hudSizes.DroppedDown)throw new Exception("Refresh closed HUD dropdown");hudSizes.DroppedDown=false;
  lines.Add("PASS: status changes keep button positions stable; ten refreshes keep HUD dropdown open.");

  foreach(float factor in new float[]{1f,1.25f,1.5f,2f}){

   float previous=Tag is float?(float)Tag:1f;Scale(new SizeF(factor/previous,factor/previous));Tag=factor;

   ClientSize=new Size((int)(634*factor),(int)(590*factor));PerformLayout();Application.DoEvents();

   foreach(var b in Descendants(this).OfType<Button>().Where(c=>c.Visible)){

    if(!b.Parent.ClientRectangle.Contains(b.Bounds))throw new Exception("Button clipped by parent: "+b.Text+" at "+factor);

    Rectangle onForm=RectangleToClient(b.RectangleToScreen(b.ClientRectangle));

    if(!ClientRectangle.Contains(onForm))throw new Exception("Button outside window: "+b.Text+" at "+factor);

   }

   lines.Add("PASS: default layout buttons contained at "+factor+" scaling.");

  }

  options.Visible=true;PerformLayout();Application.DoEvents();

  foreach(var b in options.Controls.OfType<Button>())if(!options.ClientRectangle.Contains(b.Bounds))throw new Exception("Expanded button clipped: "+b.Text);

  lines.Add("PASS: More options buttons fit their scrolling content area.");

  lines.AddRange(available.Select(r=>"Available: "+r));

  File.WriteAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"layout-test-results.txt"),lines);

 }

 static IEnumerable<Control> Descendants(Control c){foreach(Control child in c.Controls){yield return child;foreach(Control next in Descendants(child))yield return next;}}

}

static class Program {

 [STAThread] static int Main(string[] args) {

  bool test=args.Contains("--self-test"),smoke=args.Contains("--smoke-test");

  try {

   if(test)Engine.Extract();
     if(test) {

      var output=new StringBuilder();output.AppendLine(Engine.StatusSelfTest());int result=Engine.Run("SelfTest",s=>{lock(output)output.AppendLine(s);}).GetAwaiter().GetResult();

      output.AppendLine("Runtime: "+Engine.Root);output.AppendLine("Exit code: "+result);

      File.WriteAllText(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"self-test-results.txt"),output.ToString());return result;

     }

   bool created;

   using(var single=new Mutex(true,smoke?@"Local\SC2CampaignUltrawideLayoutTest":@"Local\SC2CampaignUltrawideLauncher",out created)) {

    if(!created){if(!test&&!smoke)MessageBox.Show("Campaign Ultrawide is already open. Look for its window or notification-area icon.");return 2;}

    try {

     Engine.Extract();

     Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new Launcher(smoke));return Environment.ExitCode;

    } finally { single.ReleaseMutex(); }

   }

  } catch(Exception ex) { if(!test&&!smoke)MessageBox.Show("Campaign Ultrawide could not open. Close the app and try again.\n\n"+ex.Message,"Campaign Ultrawide",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1; }

 }

}
