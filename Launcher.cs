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

[assembly: AssemblyVersion("1.9.0.0")]

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

  if(files.Count!=15 || !files.ContainsKey("SC2Addresses.cs") || !files.ContainsKey("CampaignGate.cs") || !files.ContainsKey("CampaignDisplayRefresh.cs") || !files.ContainsKey("HubCamera.cs")) throw new Exception("The embedded engine is incomplete.");

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
  if(d.ContainsKey("Hub")&&Convert.ToBoolean(d["Hub"]))return width+" \u00d7 "+height;
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

 public int Width {get;set;} public int Height {get;set;} public int HudScale {get;set;} public int MaxZoom {get;set;} public int ZoomSteps {get;set;} public string HubScale {get;set;} public string Theme {get;set;}
 public Resolution(){ZoomSteps=5;HudScale=100;HubScale="Off";Theme="Light";}

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

 ComboBox resolutions=new ComboBox(),hudSizes=new ComboBox(),hubScales=new ComboBox(),themes=new ComboBox(),maxZoom=new ComboBox(),zoomSteps=new ComboBox();
 string ThemeChoice {get{return themes.SelectedItem as string??"Light";}}
 bool Dark {get{return ThemeChoice=="Dark";}}
 Control statusCard;
 string HubScale {get{return hubScales.SelectedItem as string??"Off";}}
 int ZoomSteps {get{return 5+Math.Max(0,zoomSteps.SelectedIndex);}}
 int MaxZoom {get{return Math.Max(0,maxZoom.SelectedIndex)*10;}}
 int HudScale {get{return hudSizes.SelectedIndex<0?100:50+5*hudSizes.SelectedIndex;}}
 bool ScaleFits {get{return Selected==null||Selected.Width*9L*100>=Selected.Height*16L*HudScale;}}

 FlowLayoutPanel options;

 NotifyIcon tray=new NotifyIcon();
 Icon appIcon;

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

 static Button Button(string text,EventHandler action){var b=new ThemeButton {Text=text,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Padding=new Padding(13,7,13,7),Margin=new Padding(0,0,10,8)};b.Click+=action;return b;}

 static Label TextLabel(string text,float size,bool bold){return new Label {Text=text,AutoSize=true,MaximumSize=new Size(590,0),Font=new Font("Segoe UI",size,bold?FontStyle.Bold:FontStyle.Regular),Margin=new Padding(0,0,0,12)};}

 static FlowLayoutPanel Flow(){return new FlowLayoutPanel {AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Dock=DockStyle.Fill,WrapContents=true,Margin=new Padding(0)};}

 internal Launcher(bool test) {

  using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Application.Icon"))using(var loaded=new Icon(stream)){appIcon=(Icon)loaded.Clone();}
  Icon=appIcon;
  smoke=test;Text="StarCraft II Campaign Ultrawide";Font=new Font("Segoe UI",10);AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;

  ClientSize=new Size(680,690);MinimumSize=new Size(650,720);StartPosition=FormStartPosition.CenterScreen;

  var scroll=new Panel {Dock=DockStyle.Fill,AutoScroll=true};

  var layout=new TableLayoutPanel {Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,ColumnCount=1,Padding=new Padding(24),Margin=new Padding(0)};

  layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));

  Action<Control> row=c=>{int n=layout.RowCount++;layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.Controls.Add(c,0,n);};

  row(TextLabel("Campaign Ultrawide",21,true));

  row(TextLabel("Resolution",10,true));

  resolutions.DropDownStyle=ComboBoxStyle.DropDownList;resolutions.Dock=DockStyle.Fill;resolutions.Margin=new Padding(0,0,0,10);resolutions.DropDownWidth=540;

  available=DisplayModes.Available();foreach(var r in available)resolutions.Items.Add(r);if(available.Count>0)resolutions.SelectedIndex=0;

  maxZoom.Items.Add("Default");for(int zoom=10;zoom<=100;zoom+=10)maxZoom.Items.Add("+"+zoom+"%");maxZoom.SelectedIndex=0;for(int steps=5;steps<=20;steps++)zoomSteps.Items.Add(steps.ToString());zoomSteps.SelectedIndex=0;
  themes.Items.AddRange(new object[]{"Light","Dark"});themes.SelectedIndex=0;
  hubScales.Items.AddRange(new object[]{"Off","Fit","Expanded"});hubScales.SelectedIndex=0;
  for(int scale=50;scale<=100;scale+=5)hudSizes.Items.Add(scale+"%");hudSizes.SelectedIndex=10;
  try {if(File.Exists(settingsPath)){var saved=new JavaScriptSerializer().Deserialize<Resolution>(File.ReadAllText(settingsPath));var match=saved==null?null:available.FirstOrDefault(r=>r.Width==saved.Width&&r.Height==saved.Height);if(match!=null)resolutions.SelectedItem=match;if(saved!=null&&saved.HudScale>=50&&saved.HudScale<=125&&saved.HudScale%5==0)hudSizes.SelectedIndex=(Math.Min(100,saved.HudScale)-50)/5;if(saved!=null&&hubScales.Items.Contains(saved.HubScale??"Off"))hubScales.SelectedItem=saved.HubScale??"Off";if(saved!=null&&saved.MaxZoom>=0&&saved.MaxZoom<=200&&saved.MaxZoom%10==0)maxZoom.SelectedIndex=Math.Min(100,saved.MaxZoom)/10;if(saved!=null&&saved.ZoomSteps>=5&&saved.ZoomSteps<=20)zoomSteps.SelectedIndex=saved.ZoomSteps-5;if(saved!=null&&saved.Theme=="Dark")themes.SelectedIndex=1;}}catch(IOException){}catch(ArgumentException){}

  resolutions.SelectedIndexChanged+=(s,e)=>{if(!initializing){SaveSelection();notice="";RefreshState();}};row(resolutions);

  choiceHint.AutoSize=false;choiceHint.Height=40;choiceHint.Dock=DockStyle.Top;choiceHint.MaximumSize=new Size(590,0);choiceHint.ForeColor=Color.FromArgb(80,80,80);choiceHint.Margin=new Padding(0,0,0,18);row(choiceHint);

  row(TextLabel("Bottom HUD size",10,true));hudSizes.DropDownStyle=ComboBoxStyle.DropDownList;hudSizes.Dock=DockStyle.Fill;hudSizes.Margin=new Padding(0,0,0,18);
  hudSizes.SelectedIndexChanged+=(s,e)=>{if(!initializing){SaveSelection();notice="";RefreshState();}};row(hudSizes);

  var zoomOptions=new TableLayoutPanel {ColumnCount=2,RowCount=2,AutoSize=true,Dock=DockStyle.Top,Margin=new Padding(0,0,0,18)};
  zoomOptions.RowStyles.Add(new RowStyle(SizeType.AutoSize));zoomOptions.RowStyles.Add(new RowStyle(SizeType.AutoSize));
  zoomOptions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,65));zoomOptions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,35));
  zoomOptions.Controls.Add(TextLabel("Max zoom out",10,true),0,0);zoomOptions.Controls.Add(TextLabel("Zoom steps",10,true),1,0);
  maxZoom.DropDownStyle=ComboBoxStyle.DropDownList;maxZoom.Dock=DockStyle.Fill;maxZoom.Margin=new Padding(0,0,16,4);maxZoom.AccessibleName="Max zoom out";
  zoomSteps.DropDownStyle=ComboBoxStyle.DropDownList;zoomSteps.Dock=DockStyle.Fill;zoomSteps.Margin=new Padding(0,0,0,4);zoomSteps.AccessibleName="Zoom steps";
  maxZoom.SelectedIndexChanged+=(s,e)=>{if(!initializing){SaveSelection();notice="";RefreshState();}};
  zoomSteps.SelectedIndexChanged+=(s,e)=>{if(!initializing){SaveSelection();notice="";RefreshState();}};
  zoomOptions.Controls.Add(maxZoom,0,1);zoomOptions.Controls.Add(zoomSteps,1,1);
  Action sizeZoomOptions=()=>{int height=Math.Max(maxZoom.Height+maxZoom.Margin.Vertical,zoomSteps.Height+zoomSteps.Margin.Vertical);var style=zoomOptions.RowStyles[1];if(style.SizeType!=SizeType.Absolute||style.Height!=height){style.SizeType=SizeType.Absolute;style.Height=height;zoomOptions.PerformLayout();}};
  maxZoom.SizeChanged+=(s,e)=>sizeZoomOptions();zoomSteps.SizeChanged+=(s,e)=>sizeZoomOptions();zoomOptions.Layout+=(s,e)=>sizeZoomOptions();sizeZoomOptions();row(zoomOptions);

  var framing=new TableLayoutPanel {ColumnCount=2,RowCount=2,AutoSize=true,Dock=DockStyle.Top,Margin=new Padding(0,0,0,18)};
  framing.RowStyles.Add(new RowStyle(SizeType.AutoSize));framing.RowStyles.Add(new RowStyle(SizeType.AutoSize));
  framing.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,65));framing.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,35));
  framing.Controls.Add(TextLabel("Hub Scale",10,true),0,0);framing.Controls.Add(TextLabel("Theme",10,true),1,0);
  hubScales.DropDownStyle=ComboBoxStyle.DropDownList;hubScales.Dock=DockStyle.Fill;hubScales.Margin=new Padding(0,0,16,4);hubScales.AccessibleName="Hub Scale";
  themes.DropDownStyle=ComboBoxStyle.DropDownList;themes.Dock=DockStyle.Fill;themes.Margin=new Padding(0,0,0,4);themes.AccessibleName="Theme";
  hubScales.SelectedIndexChanged+=(s,e)=>{if(!initializing){SaveSelection();notice="";RefreshState();}};
  themes.SelectedIndexChanged+=(s,e)=>{if(!initializing){ApplyTheme();if(!smoke)SaveTheme();}};
  framing.Controls.Add(hubScales,0,1);framing.Controls.Add(themes,1,1);
  // Native combo boxes can be taller than their reported preferred size.
  Action sizeFraming=()=>{
   int height=Math.Max(hubScales.Height+hubScales.Margin.Vertical,themes.Height+themes.Margin.Vertical);
   var style=framing.RowStyles[1];
   if(style.SizeType!=SizeType.Absolute||style.Height!=height){style.SizeType=SizeType.Absolute;style.Height=height;framing.PerformLayout();}
  };
  hubScales.SizeChanged+=(s,e)=>sizeFraming();themes.SizeChanged+=(s,e)=>sizeFraming();
  framing.Layout+=(s,e)=>sizeFraming();sizeFraming();row(framing);

  var card=new TableLayoutPanel {ColumnCount=1,RowCount=2,AutoSize=false,Height=112,Dock=DockStyle.Top,BackColor=Color.FromArgb(238,244,248),Padding=new Padding(16),Margin=new Padding(0,0,0,18)};

  statusCard=card;
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

  tray.Icon=appIcon;tray.Text="Campaign Ultrawide";tray.DoubleClick+=(s,e)=>Reveal();

  tray.ContextMenuStrip=new ContextMenuStrip();tray.ContextMenuStrip.Items.Add("Open Campaign Ultrawide",null,(s,e)=>Reveal());

  tray.ContextMenuStrip.Items.Add("Exit",null,async(s,e)=>{if(busy)return;await StopWorker();if(!Engine.Running()){quitting=true;Close();}});

  timer.Interval=1000;timer.Tick+=async(s,e)=>{RefreshState();await CheckForRunningGame();};timer.Start();

  FormClosing+=(s,e)=>{if(!quitting&&!smoke&&(busy||Engine.Running()||WorkerAlive())){e.Cancel=true;Hide();tray.Visible=true;tray.ShowBalloonTip(2500,"Campaign Ultrawide","Automatic setup is still running. Double-click this icon to reopen.",ToolTipIcon.Info);}};

  FormClosed+=(s,e)=>{timer.Dispose();tray.Dispose();appIcon.Dispose();};

  foreach(var combo in new[]{resolutions,hudSizes,hubScales,themes,maxZoom,zoomSteps}){combo.DrawMode=DrawMode.OwnerDrawFixed;combo.ItemHeight=TextRenderer.MeasureText("Ag",combo.Font).Height+4;combo.FlatStyle=FlatStyle.Flat;combo.DrawItem+=DrawChoice;}
  HandleCreated+=(s,e)=>ThemeTitleBar(this);
  initializing=false;ApplyTheme();RefreshState();

  Shown+=async(s,e)=>{RefreshState();if(!smoke)await CheckForRunningGame();if(smoke){var t=new System.Windows.Forms.Timer{Interval=800};t.Tick+=(a,b)=>{t.Stop();t.Dispose();try{LayoutTest();}catch(Exception ex){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"layout-test-results.txt"),ex.ToString());Environment.ExitCode=1;}quitting=true;Close();};t.Start();}};

 }

 [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
 void ThemeTitleBar(Form form){
  if(!form.IsHandleCreated)return;
  int value=Dark?1:0;
  try{if(DwmSetWindowAttribute(form.Handle,20,ref value,4)!=0)DwmSetWindowAttribute(form.Handle,19,ref value,4);}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}
 }
 Color Surface {get{return Dark?Color.FromArgb(27,30,35):Color.FromArgb(248,249,251);}}
 Color TextColor {get{return Dark?Color.FromArgb(236,239,243):Color.FromArgb(28,33,40);}}
 Color InputColor {get{return Dark?Color.FromArgb(43,48,56):Color.White;}}
 void PaintTheme(Control root){
  root.BackColor=Surface;root.ForeColor=TextColor;
  foreach(Control c in Descendants(root)){
   c.BackColor=Surface;c.ForeColor=TextColor;
   var button=c as Button;
   if(button!=null){button.UseVisualStyleBackColor=false;button.FlatStyle=FlatStyle.Flat;button.BackColor=InputColor;button.FlatAppearance.BorderColor=Dark?Color.FromArgb(85,95,108):Color.FromArgb(177,186,198);button.FlatAppearance.MouseOverBackColor=Dark?Color.FromArgb(58,67,80):Color.FromArgb(229,237,248);button.FlatAppearance.MouseDownBackColor=Dark?Color.FromArgb(68,82,103):Color.FromArgb(213,226,244);}
   if(c is ComboBox||c is TextBoxBase)c.BackColor=InputColor;
  }
 }
 void ApplyTheme(){
  SuspendLayout();PaintTheme(this);
  Color card=Dark?Color.FromArgb(36,44,55):Color.FromArgb(232,240,250);
  statusCard.BackColor=card;status.BackColor=card;detail.BackColor=card;
  choiceHint.ForeColor=Dark?Color.FromArgb(180,190,205):Color.FromArgb(80,91,107);
  tray.ContextMenuStrip.Renderer=new ToolStripProfessionalRenderer(new ThemeMenuColors(Dark));
  tray.ContextMenuStrip.BackColor=InputColor;tray.ContextMenuStrip.ForeColor=TextColor;
  foreach(ToolStripItem item in tray.ContextMenuStrip.Items){item.BackColor=InputColor;item.ForeColor=TextColor;}
  ThemeTitleBar(this);ResumeLayout(false);Invalidate(true);
 }
 void DrawChoice(object sender,DrawItemEventArgs e){
  var combo=(ComboBox)sender;bool selected=(e.State&DrawItemState.Selected)!=0;
  Color background=selected?(Dark?Color.FromArgb(61,91,130):Color.FromArgb(214,230,252)):InputColor;
  using(var brush=new SolidBrush(background))e.Graphics.FillRectangle(brush,e.Bounds);
  string text=e.Index>=0?combo.GetItemText(combo.Items[e.Index]):combo.Text;
  Color foreground=combo.Enabled?TextColor:(Dark?Color.FromArgb(150,160,174):Color.FromArgb(108,116,128));
  var bounds=e.Bounds;bounds.X+=4;bounds.Width-=8;
  TextRenderer.DrawText(e.Graphics,text,combo.Font,bounds,foreground,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
  e.DrawFocusRectangle();
 }
 void SaveTheme(){
  try{
   var json=new JavaScriptSerializer();Resolution saved=null;
   if(File.Exists(settingsPath))saved=json.Deserialize<Resolution>(File.ReadAllText(settingsPath));
   if(saved==null)saved=Selected??new Resolution();saved.Theme=ThemeChoice;
   Directory.CreateDirectory(Directory.GetParent(settingsPath).FullName);File.WriteAllText(settingsPath,json.Serialize(saved));
  }catch(IOException){notice="Your theme could not be saved.";}catch(ArgumentException){notice="Your theme could not be saved.";}
 }
 sealed class ThemeButton:Button {
  protected override void OnPaint(PaintEventArgs e){
   if(Enabled){base.OnPaint(e);return;}
   e.Graphics.Clear(BackColor);ControlPaint.DrawBorder(e.Graphics,ClientRectangle,FlatAppearance.BorderColor,ButtonBorderStyle.Solid);
   Color disabled=BackColor.GetBrightness()<.5f?Color.FromArgb(151,160,173):Color.FromArgb(117,125,137);
   TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,disabled,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
  }
 }
 sealed class ThemeMenuColors:ProfessionalColorTable {
  readonly bool dark;public ThemeMenuColors(bool dark){this.dark=dark;UseSystemColors=false;}
  Color Background {get{return dark?Color.FromArgb(43,48,56):Color.White;}}
  public override Color ToolStripDropDownBackground {get{return Background;}}
  public override Color ImageMarginGradientBegin {get{return Background;}}
  public override Color ImageMarginGradientMiddle {get{return Background;}}
  public override Color ImageMarginGradientEnd {get{return Background;}}
  public override Color MenuItemSelected {get{return dark?Color.FromArgb(58,67,80):Color.FromArgb(229,237,248);}}
  public override Color MenuItemBorder {get{return MenuItemSelected;}}
  public override Color MenuBorder {get{return dark?Color.FromArgb(85,95,108):Color.FromArgb(177,186,198);}}
 }

 void SaveSelection(){if(Selected==null||!ScaleFits)return;Selected.HudScale=HudScale;Selected.HubScale=HubScale;Selected.MaxZoom=MaxZoom;Selected.ZoomSteps=ZoomSteps;Selected.Theme=ThemeChoice;try{File.WriteAllText(Path.Combine(Engine.Root,"resolution.json"),new JavaScriptSerializer().Serialize(Selected));Directory.CreateDirectory(Directory.GetParent(settingsPath).FullName);File.WriteAllText(settingsPath,new JavaScriptSerializer().Serialize(Selected));}catch(IOException){notice="Your selection could not be saved. You can still choose it before playing.";}}

 bool WorkerAlive(){try{return worker!=null&&!worker.HasExited;}catch{return false;}}

 bool GameOpen(){var games=Process.GetProcessesByName("SC2_x64");bool found=games.Length>0;foreach(var g in games)g.Dispose();return found;}

 void Reveal(){Show();WindowState=FormWindowState.Normal;Activate();tray.Visible=false;}

 void Append(string text){lock(outputLock){try{File.AppendAllText(Path.Combine(Engine.Root,"launcher.log"),DateTime.Now.ToString("s")+" "+text+Environment.NewLine);}catch(IOException){}}}

 string GameIdentity(){
  var games=Process.GetProcessesByName("SC2_x64");try{return games.Length==1?games[0].Id+":"+games[0].StartTime.ToUniversalTime().Ticks:"";}catch(InvalidOperationException){return "";}catch(System.ComponentModel.Win32Exception){return "";}finally{foreach(var g in games)g.Dispose();}
 }
 async Task CheckForRunningGame(){
  if(smoke||!automaticEnabled||busy||zoomSteps.DroppedDown||maxZoom.DroppedDown||themes.DroppedDown||hubScales.DroppedDown||hudSizes.DroppedDown||resolutions.DroppedDown)return;
  string game=GameIdentity();if(game.Length==0){lastAutoGame="";return;}
  if(Engine.Running()||WorkerAlive()||game==lastAutoGame)return;
  lastAutoGame=game;await StartWorker();
 }
 void RefreshState(){

  if(zoomSteps.DroppedDown||maxZoom.DroppedDown||themes.DroppedDown||hubScales.DroppedDown||hudSizes.DroppedDown||resolutions.DroppedDown)return;
  string nextStatus="",nextDetail="",nextHint="";
  bool running=Engine.Running()||WorkerAlive(),game=GameOpen();

  resolutions.Enabled=!busy&&!running&&!game;play.Enabled=!busy&&!running&&Selected!=null&&ScaleFits;hudSizes.Enabled=!busy;hubScales.Enabled=!busy;maxZoom.Enabled=!busy;zoomSteps.Enabled=!busy;play.Text="Enable";stop.Enabled=!busy&&running;recheck.Enabled=!busy&&Selected!=null&&ScaleFits;

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

     else if(phase=="Waiting for offline campaign"){nextStatus="Waiting for an offline campaign";nextDetail="Load a single-player campaign to apply your settings.";}

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

   notice="";worker=Engine.Start("Auto-Campaign.ps1","-TargetWidth "+Selected.Width+" -TargetHeight "+Selected.Height+" -HudScale "+HudScale+" -HubScale "+HubScale+" -MaxZoom "+MaxZoom+" -ZoomSteps "+ZoomSteps,Append);

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

 void ShowHelp(){ShowThemedHelp("ENABLE CAMPAIGN ULTRAWIDE\n1. Choose your resolution and bottom HUD size, then click Enable.\n2. Start StarCraft II yourself and load a campaign mission, or return to a mission already open.\n3. Keep the mission visible for a few seconds. The resolution and HUD apply automatically.\n\nUltrawide works in single-player campaign missions and hubs. The resolution and HUD adjust automatically. There is no need to switch resolutions manually. Use fullscreen display mode. Resolution choices come from your primary display; HUD sizes run from 50% to 100% in 5% steps. You can change HUD size while enabled; return to the mission to see the change. Objectives and dialogs keep their normal size. Larger HUD sizes require enough horizontal space. Close the game before choosing a different resolution.\n\nMAX ZOOM OUT\nChoose up to +100% additional camera distance and 5 to 20 zoom positions. The positions are evenly spaced between the closest view and your selected maximum. Default keeps the normal maximum distance. You can change both settings while playing.\n\nHUB SCALE\nOff keeps the original hub framing. Fit shows more scenery on the sides while keeping the normal camera height. Expanded adds 20 degrees to the horizontal field of view in Fit mode. You can switch these options while playing; they do not change mission cameras.\n\nWHILE PLAYING\nClosing this window keeps automatic setup in the notification area. It stops monitoring when the game closes. Use the tray menu to exit the app.\n\nRESTORE YOUR SETTINGS\nMore options lets you restore the original HUD or use the game's resolution. Return to the mission so HUD changes can finish. Closing StarCraft II removes all session changes. Leaving campaign blocks further application; an existing display size can remain until the next display reset or game exit.\n\nTROUBLESHOOTING\nIf setup stalls, choose Recheck game. It restarts automatic setup without closing StarCraft II. Opening this app while StarCraft II is running starts setup automatically. Choosing Stop automatic setup keeps it stopped until you choose Enable or Recheck game. Other display sizes may depend on your monitor and graphics settings. The activity log under More options can help diagnose problems.\n\nFor single-player campaigns.");}

 void ShowThemedHelp(string text){
  using(var dialog=new Form{Text="Help",Icon=appIcon,Font=Font,ClientSize=new Size(640,560),MinimumSize=new Size(480,400),StartPosition=FormStartPosition.CenterParent,ShowInTaskbar=false}){
   var body=new RichTextBox{ReadOnly=true,BorderStyle=BorderStyle.None,Dock=DockStyle.Fill,Text=text,Font=Font,DetectUrls=false};
   var footer=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=58,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(8)};
   var close=Button("Close",(s,e)=>dialog.Close());close.DialogResult=DialogResult.Cancel;footer.Controls.Add(close);dialog.CancelButton=close;
   dialog.Padding=new Padding(16);dialog.Controls.Add(body);dialog.Controls.Add(footer);PaintTheme(dialog);dialog.HandleCreated+=(s,e)=>ThemeTitleBar(dialog);dialog.ShowDialog(this);
  }
 }

 void LayoutTest(){

  var lines=new List<string>();lines.Add(Engine.StatusSelfTest());
  if(hudSizes.Items.Count!=11)throw new Exception("HUD size option count");
  for(int i=0;i<11;i++)if(hudSizes.Items[i].ToString()!=(50+5*i)+"%")throw new Exception("HUD size increment");
  lines.Add("PASS: HUD selector contains 50%-100% in 5% steps.");
  if(recheck==null||recheck.Text!="Recheck game")throw new Exception("Missing game recheck button");
  lines.Add("PASS: Recheck game is available in the main controls.");
  if(!hubScales.Items.Cast<string>().SequenceEqual(new[]{"Off","Fit","Expanded"}))throw new Exception("Hub scale choices");
  var serializer=new JavaScriptSerializer();
  if(serializer.Deserialize<Resolution>("{\"Width\":3440,\"Height\":1440}").HubScale!="Off")throw new Exception("Legacy hub default");
  foreach(string mode in new[]{"Off","Fit","Expanded"})if(serializer.Deserialize<Resolution>(serializer.Serialize(new Resolution{HubScale=mode})).HubScale!=mode)throw new Exception("Hub setting round trip");
  lines.Add("PASS: Hub Scale choices and saved settings; missing setting defaults to Off.");
  if(maxZoom.Items.Count!=11||maxZoom.Items[0].ToString()!="Default")throw new Exception("Zoom options");
  for(int i=1;i<=10;i++)if(maxZoom.Items[i].ToString()!="+"+(i*10)+"%")throw new Exception("Zoom increments");
  if(serializer.Deserialize<Resolution>("{}").MaxZoom!=0)throw new Exception("Legacy zoom default");
  for(int i=0;i<=100;i+=10)if(serializer.Deserialize<Resolution>(serializer.Serialize(new Resolution{MaxZoom=i})).MaxZoom!=i)throw new Exception("Zoom setting round trip");
  maxZoom.DroppedDown=true;for(int i=0;i<10;i++)RefreshState();if(!maxZoom.DroppedDown)throw new Exception("Refresh closed zoom dropdown");maxZoom.DroppedDown=false;
  lines.Add("PASS: Max zoom out Default and +10%-+100% options, saved settings, and stable open dropdown.");
  if(zoomSteps.Items.Count!=16)throw new Exception("Zoom step option count");
  for(int i=0;i<16;i++)if(zoomSteps.Items[i].ToString()!=(5+i).ToString())throw new Exception("Zoom step choice");
  for(int i=5;i<=20;i++)if(serializer.Deserialize<Resolution>(serializer.Serialize(new Resolution{ZoomSteps=i})).ZoomSteps!=i)throw new Exception("Zoom step round trip");
  zoomSteps.DroppedDown=true;for(int i=0;i<10;i++)RefreshState();if(!zoomSteps.DroppedDown)throw new Exception("Refresh closed zoom step dropdown");zoomSteps.DroppedDown=false;
  lines.Add("PASS: 5-20 zoom steps, saved choices, and stable open dropdown.");
  var oldStatus=status.Text;var oldDetail=detail.Text;var oldHint=choiceHint.Text;
  var buttons=Descendants(this).OfType<Button>().ToArray();var positions=buttons.Select(b=>b.RectangleToScreen(b.ClientRectangle)).ToArray();
  foreach(string text in new[]{"Ready","Preparing your campaign...","Waiting for an offline campaign"}){status.Text=text;detail.Text="Load a single-player campaign to apply your settings.";choiceHint.Text="Choose a widescreen resolution supported by your primary display. Your choice is saved for next time.";PerformLayout();for(int i=0;i<buttons.Length;i++)if(buttons[i].RectangleToScreen(buttons[i].ClientRectangle)!=positions[i])throw new Exception("Status update moved a button");}
  status.Text=oldStatus;detail.Text=oldDetail;choiceHint.Text=oldHint;
  hudSizes.DroppedDown=true;for(int i=0;i<10;i++)RefreshState();if(!hudSizes.DroppedDown)throw new Exception("Refresh closed HUD dropdown");hudSizes.DroppedDown=false;
  hubScales.DroppedDown=true;for(int i=0;i<10;i++)RefreshState();if(!hubScales.DroppedDown)throw new Exception("Refresh closed Hub Scale dropdown");hubScales.DroppedDown=false;
  lines.Add("PASS: status changes keep button positions stable; refreshes keep HUD and Hub Scale dropdowns open.");

  foreach(float factor in new float[]{1f,1.25f,1.5f,2f}){

   float previous=Tag is float?(float)Tag:1f;Scale(new SizeF(factor/previous,factor/previous));Tag=factor;

   ClientSize=new Size((int)(634*factor),(int)(680*factor));PerformLayout();Application.DoEvents();

   foreach(var b in Descendants(this).Where(c=>c.Visible&&(c is Button||c is ComboBox))){

    if(!b.Parent.ClientRectangle.Contains(b.Bounds))throw new Exception("Control clipped by parent: "+b.Text+" at "+factor);

    Rectangle onForm=RectangleToClient(b.RectangleToScreen(b.ClientRectangle));

    if(!ClientRectangle.Contains(onForm))throw new Exception("Control outside window: "+b.Text+" at "+factor);

   }

   lines.Add("PASS: default layout buttons and dropdowns contained at "+factor+" scaling.");

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
