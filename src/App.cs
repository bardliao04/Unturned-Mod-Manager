using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Net;
using System.Diagnostics;

[assembly: System.Reflection.AssemblyVersion("1.0.1.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.0.1.0")]
[assembly: System.Reflection.AssemblyProduct("Unturned 模組管理器")]

namespace UnturnedModManager {
 class Row { public Package Package; public InstalledPackage Installed;public string ServerIssue;public List<VersionOption> Options; }
 public class MainForm : Form {
  readonly string home,store; Settings settings; bool busy; bool preview;bool serverMode;string startupIssue;
  ComboBox targetChoice=new ComboBox();Label targetPathLabel;Button autoFind;
  TextBox game=new TextBox(),workshop=new TextBox(),log=new TextBox(),detail=new TextBox();
  DataGridView grid=new DataGridView(),versionGrid=new DataGridView();TabControl tabs;Button removeVersion;Label status=new Label(); ProgressBar progress=new ProgressBar();bool populating;
  List<VersionOption> versionRows=new List<VersionOption>();
  List<Control> actions=new List<Control>(); List<Package> imported=new List<Package>();List<Row> rows=new List<Row>();
  readonly ToolTip hints=new ToolTip{InitialDelay=600,ReshowDelay=150,AutoPopDelay=12000,ShowAlways=true};
  public MainForm(string folder,string state,bool renderOnly=false) {
   preview=renderOnly;
   home=folder;store=state;Font=new Font("Microsoft JhengHei UI",10);Text="Unturned 模組管理器 v"+ManagerUpdates.Current;ClientSize=new Size(1080,790);MinimumSize=new Size(960,740);StartPosition=FormStartPosition.CenterScreen;BackColor=Color.FromArgb(245,247,251);AutoScaleMode=AutoScaleMode.Dpi;
   try{settings=File.Exists(Path.Combine(store,"settings.json"))?Data.Read<Settings>(Path.Combine(store,"settings.json")):new Settings();if(settings==null)throw new Exception("設定檔內容無效。");}catch(Exception ex){startupIssue="無法讀取先前設定，請重新確認資料夾位置。"+Environment.NewLine+Problems.Explain(ex);settings=new Settings();}
   if(settings.packageFolders==null)settings.packageFolders=new List<string>();
   if(settings.versionChoices==null)settings.versionChoices=new Dictionary<string,string>();
   serverMode=settings.serverMode;Build();game.Text=(serverMode?settings.serverRoot:settings.gameRoot)??"";workshop.Text=settings.workshopRoot??"";UpdateTarget();
   targetChoice.SelectedIndexChanged+=async delegate{if(busy)return;bool next=targetChoice.SelectedIndex==1;if(next==serverMode)return;RememberTarget();serverMode=next;game.Text=(serverMode?settings.serverRoot:settings.gameRoot)??"";UpdateTarget();await Scan(false,string.IsNullOrWhiteSpace(game.Text));};
   Shown+=async delegate{await Scan(false,string.IsNullOrWhiteSpace(game.Text)||string.IsNullOrWhiteSpace(workshop.Text));if(!preview)await CheckUpdates();};FormClosing+=delegate(object s,FormClosingEventArgs e){if(busy){e.Cancel=true;MessageBox.Show("操作進行中，請等待完成後再關閉。",Text);}};
  }
  Button Button(string caption,EventHandler action,string hint=null) {var b=new Button{Text=caption,AutoSize=true,Height=35,FlatStyle=FlatStyle.Flat,BackColor=Color.White,Padding=new Padding(10,3,10,3),Margin=new Padding(4)};b.FlatAppearance.BorderColor=Color.FromArgb(210,217,228);b.Click+=action;actions.Add(b);hints.SetToolTip(b,hint??ButtonHint(caption));return b;}
  static string ButtonHint(string caption){switch(caption){
   case "重新掃描":return "重新讀取 mod 來源與匯入內容，更新可用版本及安裝狀態。";
   case "匯入 ZIP":return "選擇含相容安裝清單的 ZIP，解壓縮並加入 mod 清單；匯入後再勾選安裝。";
   case "安裝／更新勾選mod":return "將勾選的 mod 安裝到目前選擇的遊戲或伺服器資料夾。請先關閉遊戲與伺服器。";
   case "解除安裝勾選mod":return "移除勾選的已安裝 mod，並還原管理器備份的原始檔案。";
   case "復原未完成的操作":return "操作曾中斷時，依管理器紀錄復原檔案。這不是解除所有 mod。";
   default:return "從 Steam 遊戲庫尋找安裝位置，再掃描可用 mod。自訂路徑可用「瀏覽」指定。";
  }}
  protected override void Dispose(bool disposing){if(disposing)hints.Dispose();base.Dispose(disposing);}
  void Build() {
   var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=6,Padding=new Padding(22,0,22,16)};
   layout.RowStyles.Add(new RowStyle(SizeType.Absolute,90));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,146));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,46));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,58));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,46));Controls.Add(layout);
   var heading=new Panel{Dock=DockStyle.Fill,BackColor=Color.FromArgb(24,43,70),Padding=new Padding(12)};
   heading.Controls.Add(new Label{Text="漢化與模組，一次管理",ForeColor=Color.FromArgb(193,210,230),Dock=DockStyle.Bottom,Height=24});
   heading.Controls.Add(new Label{Text="UNTURNED  模組管理器",Font=new Font(Font.FontFamily,18,FontStyle.Bold),ForeColor=Color.White,Dock=DockStyle.Top,Height=36});layout.Controls.Add(heading,0,0);
   var paths=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=3,Padding=new Padding(0,12,0,0)};paths.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,140));paths.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));paths.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,114));
   paths.Controls.Add(new Label{Text="安裝目標",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,0);targetChoice.DropDownStyle=ComboBoxStyle.DropDownList;targetChoice.Items.AddRange(new object[]{"玩家遊戲","Unturned 伺服器"});targetChoice.Dock=DockStyle.Fill;targetChoice.Margin=new Padding(4,8,4,4);targetChoice.SelectedIndex=serverMode?1:0;paths.Controls.Add(targetChoice,1,0);actions.Add(targetChoice);
   targetPathLabel=AddPath(paths,1,"遊戲資料夾",game,delegate{Browse(game);});AddPath(paths,2,"mod來源資料夾",workshop,delegate{Browse(workshop);});
   layout.Controls.Add(paths,0,1);
   var toolbar=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};toolbar.Controls.Add(Button("重新掃描",async delegate{await Scan(true);}));
   autoFind=Button("自動尋找遊戲資料夾",async delegate{await Scan(true,true);});toolbar.Controls.Add(autoFind);toolbar.Controls.Add(Button("匯入 ZIP",async delegate{await ImportZip();}));layout.Controls.Add(toolbar,0,2);
   tabs=new TabControl{Dock=DockStyle.Fill};var packages=new TabPage("mod清單"){BackColor=Color.White};var versions=new TabPage("版本管理"){BackColor=Color.White};var logs=new TabPage("操作紀錄");tabs.TabPages.Add(packages);tabs.TabPages.Add(versions);tabs.TabPages.Add(logs);layout.Controls.Add(tabs,0,3);
   grid.Dock=DockStyle.Fill;grid.BackgroundColor=Color.White;grid.BorderStyle=BorderStyle.None;grid.AllowUserToAddRows=false;grid.AllowUserToDeleteRows=false;grid.AllowUserToResizeRows=false;grid.RowHeadersVisible=false;grid.SelectionMode=DataGridViewSelectionMode.FullRowSelect;grid.MultiSelect=false;grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;grid.RowTemplate.Height=40;grid.EnableHeadersVisualStyles=false;grid.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(232,238,247);grid.ColumnHeadersHeight=38;
   grid.Columns.Add(new DataGridViewCheckBoxColumn{HeaderText="選擇",FillWeight=12});grid.Columns.Add("name","mod名稱");grid.Columns.Add(new DataGridViewComboBoxColumn{Name="version",HeaderText="可用版本",DisplayStyle=DataGridViewComboBoxDisplayStyle.DropDownButton,FlatStyle=FlatStyle.Flat});grid.Columns.Add("installed","安裝狀態");grid.Columns.Add("origin","來源");grid.Columns[1].FillWeight=48;grid.Columns[2].FillWeight=76;grid.Columns[3].FillWeight=38;grid.Columns[4].FillWeight=28;foreach(DataGridViewColumn c in grid.Columns)if(c.Index!=0&&c.Index!=2)c.ReadOnly=true;
   grid.CurrentCellDirtyStateChanged+=delegate{if(grid.IsCurrentCellDirty)grid.CommitEdit(DataGridViewDataErrorContexts.Commit);};grid.CellValueChanged+=delegate(object sender,DataGridViewCellEventArgs e){if(!populating&&e.ColumnIndex==2&&e.RowIndex>=0&&e.RowIndex<rows.Count)ChangeVersion(e.RowIndex);};grid.DataError+=delegate(object sender,DataGridViewDataErrorEventArgs e){e.ThrowException=false;Append("版本選擇無效，請重新掃描。");};
   grid.SelectionChanged+=delegate{ShowDetail();};detail.Dock=DockStyle.Bottom;detail.Height=116;detail.Multiline=true;detail.ReadOnly=true;detail.ScrollBars=ScrollBars.Vertical;detail.BorderStyle=BorderStyle.None;detail.BackColor=Color.FromArgb(240,244,249);packages.Controls.Add(grid);packages.Controls.Add(detail);
   log.Dock=DockStyle.Fill;log.Multiline=true;log.ReadOnly=true;log.ScrollBars=ScrollBars.Both;log.BackColor=Color.White;logs.Controls.Add(log);
   versionGrid.Dock=DockStyle.Fill;versionGrid.BackgroundColor=Color.White;versionGrid.AllowUserToAddRows=false;versionGrid.AllowUserToDeleteRows=false;versionGrid.RowHeadersVisible=false;versionGrid.ReadOnly=true;versionGrid.MultiSelect=false;versionGrid.SelectionMode=DataGridViewSelectionMode.FullRowSelect;versionGrid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;versionGrid.RowTemplate.Height=36;
   versionGrid.Columns.Add("name","mod名稱");versionGrid.Columns.Add("version","版本");versionGrid.Columns.Add("state","狀態");versionGrid.Columns.Add("origin","來源");versionGrid.Columns.Add("folder","來源位置");versionGrid.Columns[4].FillWeight=200;versionGrid.SelectionChanged+=delegate{UpdateVersionActions();};
   var versionTools=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=48};versionTools.Controls.Add(Button("使用此版本",delegate{UseVersion();},"將此版本選入 mod 清單；再勾選並按安裝。切換選擇不會立刻修改已安裝的 mod。"));versionTools.Controls.Add(Button("開啟來源資料夾",delegate{OpenVersion();},"開啟選定版本的檔案位置。"));removeVersion=Button("移除匯入版本",async delegate{await RemoveVersion();},"移除選定的 ZIP 匯入來源；已安裝的 mod 保留。工作坊版本由 Steam 管理。移除內容會保留於本機備份資料夾。" );versionTools.Controls.Add(removeVersion);versions.Controls.Add(versionGrid);versions.Controls.Add(versionTools);
   var commands=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(0,8,0,0)};var install=Button("安裝／更新勾選mod",async delegate{await Install();});install.BackColor=Color.FromArgb(31,101,178);install.ForeColor=Color.White;commands.Controls.Add(install);commands.Controls.Add(Button("解除安裝勾選mod",async delegate{await Uninstall();}));commands.Controls.Add(Button("復原未完成的操作",async delegate{try{var e=GetEngine();await Run(delegate{e.RecoverPending();});}catch(Exception ex){Error(ex);}}));layout.Controls.Add(commands,0,4);
   var bottom=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,230));status.Text="選擇遊戲位置後，勾選要安裝的mod。";status.Dock=DockStyle.Fill;status.TextAlign=ContentAlignment.MiddleLeft;progress.Dock=DockStyle.Fill;progress.Margin=new Padding(8,12,0,8);bottom.Controls.Add(status,0,0);bottom.Controls.Add(progress,1,0);layout.Controls.Add(bottom,0,5);
  }
  Label AddPath(TableLayoutPanel panel,int row,string caption,TextBox field,EventHandler browse) {var label=new Label{Text=caption,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft};panel.Controls.Add(label,0,row);field.Dock=DockStyle.Fill;field.Margin=new Padding(4,8,4,4);panel.Controls.Add(field,1,row);panel.Controls.Add(Button("瀏覽…",browse,field==workshop?"選擇工作坊內容或已解壓縮的 mod 來源資料夾，再掃描可安裝的 mod。":"選擇目前安裝目標的根目錄，需包含 Unturned.exe 或伺服器執行檔。"),2,row);return label;}
  void RememberTarget(){if(serverMode)settings.serverRoot=game.Text.Trim();else settings.gameRoot=game.Text.Trim();}
  void UpdateTarget(){targetPathLabel.Text=serverMode?"伺服器資料夾":"遊戲資料夾";autoFind.Text=serverMode?"自動尋找伺服器資料夾":"自動尋找遊戲資料夾";hints.SetToolTip(autoFind,serverMode?"從 Steam 遊戲庫尋找 Unturned 專用伺服器位置，再掃描 mod。SteamCMD 安裝請手動瀏覽。":"從 Steam 遊戲庫尋找 Unturned 遊戲與工作坊位置，再掃描 mod。");hints.SetToolTip(targetChoice,"選擇 mod 要安裝到玩家遊戲或專用伺服器；兩者的資料夾分別記住。");}
  async Task ImportZip(){if(busy)return;string file;using(var dialog=new OpenFileDialog{Title="匯入 mod 壓縮檔",Filter="ZIP 壓縮檔 (*.zip)|*.zip",CheckFileExists=true,Multiselect=false}){if(dialog.ShowDialog(this)!=DialogResult.OK)return;file=dialog.FileName;}
   Package package=null;SetBusy(true,true);status.Text="正在匯入 ZIP，請稍候…";Append("正在匯入："+file);
   try{package=await Task.Run(()=>Packages.ImportZip(file,store));imported.Add(package);if(!settings.packageFolders.Any(p=>string.Equals(p,package.Folder,StringComparison.OrdinalIgnoreCase)))settings.packageFolders.Add(package.Folder);Append("已匯入："+package.Manifest.name+" · "+package.Manifest.version);}
   catch(Exception ex){Error(ex);}
   finally{SetBusy(false);progress.Value=package==null?0:100;}
   if(package==null)return;await Scan();int index=rows.FindIndex(r=>r.Package!=null&&r.Package.Manifest.id==package.Manifest.id);if(index>=0){grid.ClearSelection();grid.CurrentCell=grid.Rows[index].Cells[1];grid.Rows[index].Selected=true;ShowDetail();}
   status.Text="匯入完成："+package.Manifest.name+"。請勾選 mod 並按安裝。";Append(status.Text);ShowResult("ZIP 匯入完成。",package.Manifest.name+" · "+package.Manifest.version+Environment.NewLine+"已加入 mod 清單，尚未安裝到遊戲或伺服器。請勾選後按「安裝／更新勾選mod」。",new List<string>());
  }
  async void Browse(TextBox field) {using(var d=new FolderBrowserDialog()){if(Directory.Exists(field.Text))d.SelectedPath=field.Text;if(d.ShowDialog()==DialogResult.OK){field.Text=d.SelectedPath;await Scan(true);}}}
  void SaveSettings(){RememberTarget();settings.serverMode=serverMode;settings.workshopRoot=workshop.Text.Trim();Data.Write(Path.Combine(store,"settings.json"),settings);}
  Engine GetEngine(){var e=new Engine(game.Text.Trim(),store,false,serverMode);e.Progress=Report;return e;}
  void SetBusy(bool value,bool scanning=false){busy=value;foreach(var c in actions)c.Enabled=!value;grid.Enabled=versionGrid.Enabled=!value;game.Enabled=workshop.Enabled=!value;UseWaitCursor=value;progress.Style=value&&scanning?ProgressBarStyle.Marquee:ProgressBarStyle.Continuous;if(value)progress.Value=0;UpdateVersionActions();}
  async Task Scan(bool notify=false,bool find=false) {
   if(busy)return;
   SetBusy(true,true);bool completed=false;string targetName=serverMode?"伺服器":"遊戲";string operation=find?"尋找"+targetName+"與掃描":"掃描";status.Text="正在"+operation+"，請稍候…";Append(status.Text);
   try {
    string gamePath=game.Text.Trim(),workshopPath=workshop.Text.Trim();var folders=settings.packageFolders.ToArray();
    Func<ScanResult> collect=delegate{var issues=new List<string>();var libraries=preview?new List<string>():SteamFinder.Libraries(issues);var data=Scanner.Collect(home,store,gamePath,workshopPath,folders,libraries,find,Report,serverMode);data.Issues.InsertRange(0,issues);return data;};
    var result=preview?collect():await Task.Run(collect);game.Text=result.Game;workshop.Text=result.Workshop;
    if(startupIssue!=null){result.Issues.Add(startupIssue);startupIssue=null;}
    try{SaveSettings();}catch(Exception ex){result.Issues.Add("無法儲存資料夾設定，下次開啟可能需要重新選擇："+Problems.Explain(ex));}
    var available=result.Available;var installed=result.Installed;available.AddRange(imported);
    populating=true;rows.Clear();grid.Rows.Clear();
    foreach(var group in available.GroupBy(p=>p.Manifest.id).OrderBy(g=>g.First().Manifest.name)) {var options=PackageVersions.Options(group);string saved;settings.versionChoices.TryGetValue(group.Key,out saved);var p=PackageVersions.Choose(options,saved).Package;string reason="";if(serverMode)InstallTarget.SupportsServer(p,out reason);rows.Add(new Row{Package=p,Installed=installed.FirstOrDefault(x=>x.id==p.Manifest.id),ServerIssue=reason,Options=options});}
    foreach(var p in installed.Where(p=>!rows.Any(r=>r.Package!=null&&r.Package.Manifest.id==p.id)))rows.Add(new Row{Installed=p});
    foreach(var row in rows){var p=row.Package;int index=grid.Rows.Add(false,p==null?row.Installed.name:p.Manifest.name,null,"",p==null?"安裝紀錄":p.Origin);var cell=(DataGridViewComboBoxCell)grid.Rows[index].Cells[2];if(p!=null){cell.DisplayMember="Label";cell.ValueMember="Key";cell.DataSource=row.Options;cell.Value=p.Folder;}else{cell.Items.Add("—");cell.Value="—";cell.ReadOnly=true;}RefreshRow(index);}
    populating=false;RefreshVersions();
    int count=rows.Count(r=>r.Package!=null&&string.IsNullOrEmpty(r.ServerIssue));string summary=operation+"完成：找到 "+count+" 個適用 mod，已安裝 "+installed.Count+" 個。";
    string extra=count==0?"未找到支援的 mod。請確認工作坊下載完成、內容已解壓縮，且包含管理器識別檔或支援的漢化 Data。一般地圖、物品及未提供識別檔的 mod 不會列入清單。":"";
    if(serverMode)extra="安裝目標：Unturned 伺服器。只安裝宣告伺服器角色的 mod；不適用項目仍會顯示但不能勾選安裝。"+Environment.NewLine+extra;
    if(find){summary=targetName+"資料夾搜尋完成："+(result.FoundGame?"已找到 Unturned "+targetName+"。":"未在 Steam 遊戲庫找到 Unturned "+targetName+"。");
     extra="目前"+targetName+"資料夾："+(string.IsNullOrWhiteSpace(result.Game)?"尚未設定":result.Game)+Environment.NewLine
      +"目前工作坊資料夾："+(string.IsNullOrWhiteSpace(result.Workshop)?"尚未找到":result.Workshop)+Environment.NewLine+Environment.NewLine
      +"已接著掃描 mod：找到 "+count+" 個支援 mod，已安裝 "+installed.Count+" 個。"+Environment.NewLine+extra;
    }
    foreach(string issue in result.Issues)Append("問題："+issue);
    status.Text=summary+(result.Issues.Count>0?" 有 "+result.Issues.Count+" 個問題，請查看操作紀錄。":"");Append(summary);if(extra.Length>0)Append(extra);ShowDetail();
    completed=true;SetBusy(false);progress.Value=100;if(!preview&&(notify||result.Issues.Count>0))ShowResult(summary,extra,result.Issues);
   }catch(Exception ex){status.Text=operation+"失敗，請查看操作紀錄。";Error(ex);}
   finally{populating=false;SetBusy(false);progress.Value=completed?100:0;}
  }
  void ShowResult(string summary,string extra,List<string> issues){
   using(var dialog=new Form{Text=issues.Count>0?"完成，但有問題需要處理":"操作完成",Size=new Size(730,480),MinimumSize=new Size(600,350),StartPosition=FormStartPosition.CenterParent,Font=Font,MinimizeBox=false,MaximizeBox=false}){
    var body=new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,BackColor=Color.White,Text=summary+Environment.NewLine+Environment.NewLine+extra+(issues.Count==0?"":Environment.NewLine+Environment.NewLine+string.Join(Environment.NewLine+Environment.NewLine,issues.Select((s,i)=>(i+1)+". "+s)))};
    var close=new System.Windows.Forms.Button{Text="確定",Dock=DockStyle.Bottom,Height=40,DialogResult=DialogResult.OK};hints.SetToolTip(close,"關閉這個結果視窗，回到管理器。");dialog.Controls.Add(body);dialog.Controls.Add(close);dialog.AcceptButton=close;dialog.ShowDialog(this);
   }
  }
  void ShowDetail(){
   if(grid.CurrentRow==null||grid.CurrentRow.Index>=rows.Count){detail.Text="";return;}
   var row=rows[grid.CurrentRow.Index];
   if(row.Package==null){detail.Text="";return;}
   var m=row.Package.Manifest;
   detail.Text=string.Join(Environment.NewLine+Environment.NewLine,new[]{m.description,serverMode?m.afterInstallServer:m.afterInstall}.Where(s=>!string.IsNullOrWhiteSpace(s)));
  }
  void RefreshRow(int index){var row=rows[index];var p=row.Package;var old=row.Installed;string reason="";if(p!=null&&serverMode)InstallTarget.SupportsServer(p,out reason);row.ServerIssue=reason;
   string state=old==null?"未安裝":p==null?"已安裝（來源未找到）":new Version(old.version)<new Version(p.Manifest.version)?"可更新 · "+old.version:new Version(old.version)>new Version(p.Manifest.version)?"可切換舊版 · "+old.version:"已安裝 · "+old.version;
   if(reason.Length>0)state=old==null?"不適用於伺服器":"已安裝（所選版本不適用）";grid.Rows[index].Cells[3].Value=state;grid.Rows[index].Cells[4].Value=p==null?"安裝紀錄":p.Origin;grid.Rows[index].Cells[0].ReadOnly=reason.Length>0&&old==null;
   if(reason.Length>0&&old==null)grid.Rows[index].Cells[0].Value=false;
  }
  void ChangeVersion(int index){var row=rows[index];if(row.Options==null)return;var key=Convert.ToString(grid.Rows[index].Cells[2].Value);var option=row.Options.FirstOrDefault(o=>Data.Same(o.Key,key));if(option==null)return;row.Package=option.Package;settings.versionChoices[row.Package.Manifest.id]=option.Key;RefreshRow(index);ShowDetail();RefreshVersions();try{SaveSettings();}catch(Exception ex){Error(ex);}}
  void RefreshVersions(){versionRows=rows.Where(r=>r.Options!=null).SelectMany(r=>r.Options).ToList();versionGrid.Rows.Clear();foreach(var option in versionRows){var row=rows.First(r=>r.Package!=null&&r.Package.Manifest.id==option.Package.Manifest.id);string state=Data.Same(row.Package.Folder,option.Key)?"目前選擇":"";if(row.Installed!=null&&new Version(row.Installed.version)==new Version(option.Package.Manifest.version))state+=(state.Length>0?"；":"")+"已安裝同版本";versionGrid.Rows.Add(option.Package.Manifest.name,option.Label,state,option.Package.Origin,option.Key);}UpdateVersionActions();}
  VersionOption CurrentVersion(){return versionGrid.CurrentRow==null||versionGrid.CurrentRow.Index>=versionRows.Count?null:versionRows[versionGrid.CurrentRow.Index];}
  void UpdateVersionActions(){if(removeVersion==null)return;var option=CurrentVersion();removeVersion.Enabled=!busy&&option!=null&&PackageVersions.CanRemove(option.Package,store);}
  void UseVersion(){var option=CurrentVersion();if(option==null)return;int index=rows.FindIndex(r=>r.Package!=null&&r.Package.Manifest.id==option.Package.Manifest.id);if(index<0)return;grid.Rows[index].Cells[2].Value=option.Key;grid.CurrentCell=grid.Rows[index].Cells[2];tabs.SelectedIndex=0;}
  void OpenVersion(){var option=CurrentVersion();if(option==null)return;try{if(!Directory.Exists(option.Key))throw new Exception("來源資料夾不存在，請重新掃描。");Process.Start("explorer.exe","\""+option.Key+"\"");}catch(Exception ex){Error(ex);}}
  async Task RemoveVersion(){var option=CurrentVersion();if(option==null||busy)return;try{var p=option.Package;string root=PackageVersions.ImportRoot(p,store);if(MessageBox.Show("移除匯入來源："+p.Manifest.name+" · "+PackageVersions.Display(p)+"\n\n"+p.Folder+"\n\n已安裝的 mod 保留；匯入內容會移至本機備份，不再參與掃描。",Text,MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
   SetBusy(true);string archive=await Task.Run(()=>PackageVersions.RemoveImport(p,store,settings));imported.RemoveAll(q=>PackageVersions.Under(q.Folder,root));SaveSettings();Append("已移除匯入版本，備份："+archive);SetBusy(false);await Scan();
  }catch(Exception ex){Error(ex);}finally{SetBusy(false);}}
  async Task CheckUpdates(){try{Append("正在檢查管理器 GitHub 版本…");var update=await Task.Run(()=>ManagerUpdates.Fetch());if(IsDisposed)return;if(update==null){Append("管理器已是最新版本 v"+ManagerUpdates.Current+"。");return;}Append("管理器有新版："+update.tag_name);if(MessageBox.Show("管理器有新版 "+update.tag_name+"，目前為 v"+ManagerUpdates.Current+"。\n\n前往 GitHub 下載新版？", "管理器更新",MessageBoxButtons.YesNo,MessageBoxIcon.Information)==DialogResult.Yes)Process.Start(update.html_url);}catch(Exception ex){if(!IsDisposed)Append("無法檢查管理器更新："+ex.Message+"。可到 GitHub 下載頁查看。");}}
  List<Row> Selected(){grid.EndEdit();return grid.Rows.Cast<DataGridViewRow>().Where(r=>Convert.ToBoolean(r.Cells[0].Value)).Select(r=>rows[r.Index]).ToList();}
  async Task Install() {
   try {var chosen=Selected();if(chosen.Count==0)throw new Exception("請先勾選要安裝的mod。");if(chosen.Any(r=>r.Package==null))throw new Exception("部分 mod 來源已移除，請確認工作坊內容已下載，再重新掃描。");
    foreach(var row in chosen)InstallTarget.Validate(row.Package,serverMode);var e=GetEngine();if(e.NeedsRecovery){await Run(delegate{e.RecoverPending();});return;}
    bool allowDowngrade=chosen.Any(r=>r.Installed!=null&&new Version(r.Installed.version)>new Version(r.Package.Manifest.version));if(allowDowngrade&&MessageBox.Show("所選項目包含較舊版本：\n"+string.Join("\n",chosen.Where(r=>r.Installed!=null&&new Version(r.Installed.version)>new Version(r.Package.Manifest.version)).Select(r=>r.Package.Manifest.name+"："+r.Installed.version+" → "+r.Package.Manifest.version))+"\n\n要切換至所選版本嗎？",Text,MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
    if(chosen.Any(r=>r.Package.Manifest.kind=="module")&&MessageBox.Show("安裝目標："+(serverMode?"Unturned 伺服器":"玩家遊戲")+"\n"+e.Root+"\n\n勾選項目包含程式模組。請確認來源可信，並閱讀模組的相容性說明。\n管理器不會修改反作弊設定。要安裝嗎？",Text,MessageBoxButtons.YesNo,MessageBoxIcon.Information)!=DialogResult.Yes)return;
    var approvals=new Dictionary<string,Dictionary<string,string>>();
    foreach(var row in chosen){var collisions=e.Collisions(row.Package);if(collisions.Count>0){string list=string.Join("\n",collisions.Keys.Take(8));if(MessageBox.Show("「"+row.Package.Manifest.name+"」將覆蓋 "+collisions.Count+" 個已有檔案：\n"+list+"\n\n會先備份，解除安裝時還原。若曾使用舊漢化工具，建議先用舊工具還原。\n允許備份後覆蓋嗎？",Text,MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;}approvals[row.Package.Manifest.id]=collisions;}
    bool server=serverMode;bool success=await Run(delegate{foreach(var row in chosen){e.Install(row.Package,approvals[row.Package.Manifest.id],allowDowngrade);string text=InstallTarget.Instructions(row.Package,server);if(!string.IsNullOrEmpty(text))Report(100,text);}});
    if(success){string instructions="已安裝至"+(server?"伺服器":"玩家遊戲")+"：\n"+e.Root+"\n\n"+string.Join("\n\n",chosen.Select(r=>r.Package.Manifest.name+"：\n"+InstallTarget.Instructions(r.Package,server)));MessageBox.Show(instructions,"安裝完成",MessageBoxButtons.OK,MessageBoxIcon.Information);}
   }catch(Exception ex){Error(ex);}
  }
  async Task Uninstall(){try{var chosen=Selected();if(chosen.Count==0||chosen.Any(r=>r.Installed==null))throw new Exception("請勾選已安裝的mod。");if(MessageBox.Show("解除安裝 "+chosen.Count+" 個mod，並還原它們安裝前的檔案？",Text,MessageBoxButtons.YesNo)!=DialogResult.Yes)return;var e=GetEngine();await Run(delegate{foreach(var row in chosen)e.Uninstall(row.Installed.id);});}catch(Exception ex){Error(ex);}}
  async Task<bool> Run(Action action){SetBusy(true);status.Text="正在處理，請稍候…";
   bool success=false;string finalText="";try{SaveSettings();await Task.Run(action);success=true;finalText="操作已完成。";}catch(Exception ex){Error(ex);finalText="操作停止："+ex.Message;}finally{SetBusy(false);}await Scan();status.Text=finalText;return success;}
  void Report(int value,string message){if(InvokeRequired){BeginInvoke(new Action<int,string>(Report),value,message);return;}progress.Value=Math.Max(0,Math.Min(100,value));status.Text=message;Append(message);}
  void Append(string message){log.AppendText(DateTime.Now.ToString("HH:mm:ss")+"  "+message+Environment.NewLine);}
  void Error(Exception ex){string message=Problems.Explain(ex);Append("停止："+message);status.Text="操作失敗："+ex.Message;if(preview)throw ex;ShowResult("操作已停止。","",new List<string>{message});}
  public void Preview(string file){ShowInTaskbar=false;Opacity=0;StartPosition=FormStartPosition.Manual;Location=new Point(-20000,-20000);Show();Application.DoEvents();PerformLayout();using(var bitmap=new Bitmap(ClientSize.Width,ClientSize.Height)){Controls[0].DrawToBitmap(bitmap,new Rectangle(0,0,ClientSize.Width,ClientSize.Height));bitmap.Save(file,System.Drawing.Imaging.ImageFormat.Png);}Hide();}
 }
 static class Program {
  [STAThread] static int Main(string[] args){
   try{if(args.Length>0&&args[0]=="--self-test")return Tests.Run(args.Length>1?args[1]:Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"TestResults"));
    if(args.Length>=3&&args[0]=="--test-package")return Tests.ExistingPackage(args[1],args[2]);
    if(args.Length>=3&&args[0]=="--test-server-package")return Tests.ExistingPackage(args[1],args[2],true);
    if(args.Length>=3&&args[0]=="--test-zip")return Tests.ExistingZip(args[1],args[2]);
    Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);string home=AppDomain.CurrentDomain.BaseDirectory;string store=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"UnturnedModManager");
    if(args.Length>=3&&args[0]=="--preview"){using(var f=new MainForm(home,args[2],true))f.Preview(args[1]);return 0;}
    using(var f=new MainForm(home,store))Application.Run(f);return 0;
   }catch(Exception ex){if(args.Length>0){Console.Error.WriteLine(ex);return 1;}MessageBox.Show(ex.Message,"Unturned 模組管理器");return 1;}
  }
}
}
