using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Threading;
using System.Net;
using System.Diagnostics;

[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
[assembly: System.Reflection.AssemblyVersion("1.0.1.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.0.1.0")]
[assembly: System.Reflection.AssemblyProduct("Unturned 模組管理器")]

namespace UnturnedModManager {
 class Row { public Package Package; public InstalledPackage Installed;public string ServerIssue;public List<VersionOption> Options; public string Operation;public string Conflict;public string TargetIssue;public string TargetRoot; }
 public partial class MainForm : Form {
  readonly string home,store; Settings settings; bool busy; bool preview;int scanRuns;string scannedGame,scannedSource;bool scannedServer;bool serverMode;string startupIssue;
  ComboBox targetChoice=new ComboBox();Label targetPathLabel;Button autoFind;
  TextBox game=new TextBox(),workshop=new TextBox(),log=new TextBox(),detail=new TextBox();
  DataGridView grid=new DataGridView(),versionGrid=new DataGridView();TabControl tabs;Button removeVersion,useVersionButton,openVersionButton,installButton,uninstallButton;Label status=new Label(); ProgressBar progress=new ProgressBar();bool populating;
  List<VersionOption> versionRows=new List<VersionOption>();
  List<Control> actions=new List<Control>(); List<Package> imported=new List<Package>();List<Row> rows=new List<Row>();
  readonly ToolTip hints=new ToolTip{InitialDelay=600,ReshowDelay=150,AutoPopDelay=12000,ShowAlways=true};
  public MainForm(string folder,string state,bool renderOnly=false) {
   preview=renderOnly;
   home=folder;store=state;Font=new Font("Microsoft JhengHei UI",10);Text=L.Get("Text001")+ManagerUpdates.Current;ClientSize=new Size(1080,790);MinimumSize=new Size(960,740);StartPosition=FormStartPosition.CenterScreen;BackColor=Color.FromArgb(245,247,251);AutoScaleMode=AutoScaleMode.Dpi;
   try{settings=File.Exists(Path.Combine(store,"settings.json"))?Data.Read<Settings>(Path.Combine(store,"settings.json")):new Settings();if(settings==null)throw new Exception(L.Get("Text002"));}catch(Exception ex){startupIssue=L.Get("Text003")+Environment.NewLine+Problems.Explain(ex);settings=new Settings();}
   InitializeOrganization();if(settings.packageFolders==null)settings.packageFolders=new List<string>();
   if(settings.versionChoices==null)settings.versionChoices=new Dictionary<string,string>();
   L.Language=settings.language;serverMode=settings.serverMode;Build();BuildExperience();BuildModernWorkflow();game.Text=(serverMode?settings.serverRoot:settings.gameRoot)??"";workshop.Text=settings.workshopRoot??"";UpdateTarget();
   targetChoice.SelectedIndexChanged+=async delegate{if(busy)return;bool next=targetChoice.SelectedIndex==1;if(next==serverMode)return;RememberTarget();serverMode=next;game.Text=(serverMode?settings.serverRoot:settings.gameRoot)??"";UpdateTarget();await Scan(false,string.IsNullOrWhiteSpace(game.Text));};
   Shown+=async delegate{await Scan(false,string.IsNullOrWhiteSpace(game.Text)||string.IsNullOrWhiteSpace(workshop.Text));if(!preview)await CheckUpdates();};FormClosing+=delegate(object s,FormClosingEventArgs e){if(busy){e.Cancel=true;Dialogs.Show(L.Get("Text004"),Text);}};
  }
  Button Button(string caption,EventHandler action,string hint=null) {var b=new ModernButton{Text=caption,AutoSize=true,Height=35,FlatStyle=FlatStyle.Flat,BackColor=Color.White,Padding=new Padding(10,3,10,3),Margin=new Padding(4)};b.FlatAppearance.BorderColor=Color.FromArgb(210,217,228);b.Click+=action;actions.Add(b);hints.SetToolTip(b,hint??ButtonHint(caption));return b;}
  static string ButtonHint(string caption){
   string[] captions={L.Get("Text005"),L.Get("Text007"),L.Get("Text009"),L.Get("Text011"),L.Get("Text013")};
   string[] keys={"Text006","Text008","Text010","Text012","Text014"};
   int i=Array.IndexOf(captions,caption);return L.Get(i<0?"Text015":keys[i]);
  }
  protected override void Dispose(bool disposing){if(disposing)hints.Dispose();base.Dispose(disposing);}
  void Build() {
   var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=6,Padding=new Padding(22,0,22,16)};
   layout.RowStyles.Add(new RowStyle(SizeType.Absolute,90));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,146));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,46));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,58));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,46));Controls.Add(layout);
   var heading=new Panel{Dock=DockStyle.Fill,BackColor=Color.FromArgb(24,43,70),Padding=new Padding(12)};
   heading.Controls.Add(new Label{Text=L.Get("Text016"),ForeColor=Color.FromArgb(193,210,230),Dock=DockStyle.Bottom,Height=24});
   heading.Controls.Add(new Label{Text=L.Get("Text017"),Font=new Font(Font.FontFamily,18,FontStyle.Bold),ForeColor=Color.White,Dock=DockStyle.Top,Height=36});layout.Controls.Add(heading,0,0);
   var paths=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=3,Padding=new Padding(0,12,0,0)};paths.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,140));paths.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));paths.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,114));
   paths.Controls.Add(new Label{Text=L.Get("Text018"),Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,0);targetChoice.DropDownStyle=ComboBoxStyle.DropDownList;targetChoice.Items.AddRange(new object[]{L.Get("Text019"),L.Get("Text020")});targetChoice.Dock=DockStyle.Fill;targetChoice.Margin=new Padding(4,8,4,4);targetChoice.SelectedIndex=serverMode?1:0;paths.Controls.Add(targetChoice,1,0);actions.Add(targetChoice);
   targetPathLabel=AddPath(paths,1,L.Get("Text021"),game,delegate{Browse(game);});AddPath(paths,2,L.Get("Text022"),workshop,delegate{Browse(workshop);});
   layout.Controls.Add(paths,0,1);
   var toolbar=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};toolbar.Controls.Add(Button(L.Get("Text005"),async delegate{await Scan(true);}));
   autoFind=Button(L.Get("Text023"),async delegate{await Scan(true,true);});toolbar.Controls.Add(autoFind);toolbar.Controls.Add(Button(L.Get("Text007"),async delegate{await ImportZip();}));layout.Controls.Add(toolbar,0,2);
   tabs=new TabControl{Dock=DockStyle.Fill};var packages=new TabPage(L.Get("Text024")){BackColor=Color.White};var versions=new TabPage(L.Get("Text025")){BackColor=Color.White};var logs=new TabPage(L.Get("Text026"));tabs.TabPages.Add(packages);tabs.TabPages.Add(versions);tabs.TabPages.Add(logs);layout.Controls.Add(tabs,0,3);
   grid.Dock=DockStyle.Fill;grid.BackgroundColor=Color.White;grid.BorderStyle=BorderStyle.None;grid.AllowUserToAddRows=false;grid.AllowUserToDeleteRows=false;grid.AllowUserToResizeRows=false;grid.RowHeadersVisible=false;grid.SelectionMode=DataGridViewSelectionMode.FullRowSelect;grid.MultiSelect=true;grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;grid.RowTemplate.Height=40;grid.EnableHeadersVisualStyles=false;grid.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(232,238,247);grid.ColumnHeadersHeight=38;
   grid.Columns.Add(new DataGridViewCheckBoxColumn{HeaderText=L.Get("Text027"),FillWeight=12});grid.Columns.Add("name",L.Get("Text028"));grid.Columns.Add(new DataGridViewComboBoxColumn{Name="version",HeaderText=L.Get("Text029"),DisplayStyle=DataGridViewComboBoxDisplayStyle.DropDownButton,FlatStyle=FlatStyle.Flat});grid.Columns.Add("installed",L.Get("Text030"));grid.Columns.Add("origin",L.Get("Text031"));grid.Columns[1].FillWeight=48;grid.Columns[2].FillWeight=76;grid.Columns[3].FillWeight=38;grid.Columns[4].FillWeight=28;foreach(DataGridViewColumn c in grid.Columns)if(c.Index!=0&&c.Index!=2)c.ReadOnly=true;
   grid.CurrentCellDirtyStateChanged+=delegate{if(grid.IsCurrentCellDirty)grid.CommitEdit(DataGridViewDataErrorContexts.Commit);};grid.CellValueChanged+=delegate(object sender,DataGridViewCellEventArgs e){if(!populating&&e.ColumnIndex==0)UpdateSelection();if(!populating&&e.ColumnIndex==2&&e.RowIndex>=0&&e.RowIndex<rows.Count)ChangeVersion(e.RowIndex);};grid.DataError+=delegate(object sender,DataGridViewDataErrorEventArgs e){e.ThrowException=false;Append(L.Get("Text032"));};
   grid.SelectionChanged+=delegate{ShowDetail();};detail.Dock=DockStyle.Bottom;detail.Height=116;detail.Multiline=true;detail.ReadOnly=true;detail.ScrollBars=ScrollBars.Vertical;detail.BorderStyle=BorderStyle.None;detail.BackColor=Color.FromArgb(240,244,249);packages.Controls.Add(grid);packages.Controls.Add(detail);
   log.Dock=DockStyle.Fill;log.Multiline=true;log.ReadOnly=true;log.ScrollBars=ScrollBars.Both;log.BackColor=Color.White;logs.Controls.Add(log);
   versionGrid.Dock=DockStyle.Fill;versionGrid.BackgroundColor=Color.White;versionGrid.AllowUserToAddRows=false;versionGrid.AllowUserToDeleteRows=false;versionGrid.RowHeadersVisible=false;versionGrid.ReadOnly=true;versionGrid.MultiSelect=false;versionGrid.SelectionMode=DataGridViewSelectionMode.FullRowSelect;versionGrid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;versionGrid.RowTemplate.Height=36;
   versionGrid.Columns.Add("name",L.Get("Text028"));versionGrid.Columns.Add("version",L.Get("Text033"));versionGrid.Columns.Add("state",L.Get("Text034"));versionGrid.Columns.Add("origin",L.Get("Text031"));versionGrid.Columns.Add("folder",L.Get("Text035"));versionGrid.Columns[4].FillWeight=200;versionGrid.SelectionChanged+=delegate{UpdateVersionActions();};
   var versionTools=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=48};useVersionButton=Button(L.Get("Text036"),delegate{UseVersion();},L.Get("Text037"));versionTools.Controls.Add(useVersionButton);openVersionButton=Button(L.Get("Text038"),delegate{OpenVersion();},L.Get("Text039"));versionTools.Controls.Add(openVersionButton);removeVersion=Button(L.Get("Text040"),async delegate{await RemoveVersion();},L.Get("Text041") );versionTools.Controls.Add(removeVersion);versions.Controls.Add(versionGrid);versions.Controls.Add(versionTools);
   var commands=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(0,8,0,0)};var install=installButton=Button(L.Get("Text009"),async delegate{await Install();});install.BackColor=Color.FromArgb(31,101,178);install.ForeColor=Color.White;commands.Controls.Add(install);uninstallButton=Button(L.Get("Text011"),async delegate{await Uninstall();});commands.Controls.Add(uninstallButton);commands.Controls.Add(Button(L.Get("Text013"),async delegate{try{var recovery=rows.Where(r=>r.TargetIssue==null).Select(r=>GetEngine(r)).Concat(pendingScopeIds.Select(scope=>GetScopeEngine(scope))).Concat(new[]{GetEngine()}).GroupBy(e=>e.State).Select(g=>g.First()).ToList();await Run(delegate{foreach(var e in recovery)e.RecoverPending();});}catch(Exception ex){Error(ex);}}));layout.Controls.Add(commands,0,4);
   var bottom=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,230));status.Text=L.Get("Text042");status.Dock=DockStyle.Fill;status.TextAlign=ContentAlignment.MiddleLeft;progress.Dock=DockStyle.Fill;progress.Margin=new Padding(8,12,0,8);bottom.Controls.Add(status,0,0);bottom.Controls.Add(progress,1,0);layout.Controls.Add(bottom,0,5);
  }
  Label AddPath(TableLayoutPanel panel,int row,string caption,TextBox field,EventHandler browse) {var label=new Label{Text=caption,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft};panel.Controls.Add(label,0,row);field.Dock=DockStyle.Fill;field.Margin=new Padding(4,8,4,4);panel.Controls.Add(field,1,row);panel.Controls.Add(Button(L.Get("Text043"),browse,field==workshop?L.Get("Text044"):L.Get("Text045")),2,row);return label;}
  void RememberTarget(){if(serverMode)settings.serverRoot=game.Text.Trim();else settings.gameRoot=game.Text.Trim();}
  void UpdateTarget(){RefreshModernTexts();targetPathLabel.Text=serverMode?L.Get("Text046"):L.Get("Text021");autoFind.Text=serverMode?L.Get("Text047"):L.Get("Text023");hints.SetToolTip(autoFind,serverMode?L.Get("Text048"):L.Get("Text049"));hints.SetToolTip(targetChoice,L.Get("Text050"));}
  async Task ImportZip(){if(busy)return;string file;using(var dialog=new OpenFileDialog{Title=L.Get("Text051"),Filter=L.Get("Text052"),CheckFileExists=true,Multiselect=false}){if(dialog.ShowDialog(this)!=DialogResult.OK)return;file=dialog.FileName;}
   Package package=null;SetBusy(true,true);status.Text=L.Get("Text053");Append(L.Get("Text054")+file);
   try{package=await Task.Run(()=>Packages.ImportZip(file,store));imported.Add(package);if(!settings.packageFolders.Any(p=>string.Equals(p,package.Folder,StringComparison.OrdinalIgnoreCase)))settings.packageFolders.Add(package.Folder);Append(L.Get("Text055")+package.Manifest.name+" · "+package.Manifest.version);}
   catch(Exception ex){Error(ex);}
   finally{SetBusy(false);progress.Value=package==null?0:100;}
   if(package==null)return;await Scan();int index=rows.FindIndex(r=>r.Package!=null&&r.Package.Manifest.id==package.Manifest.id);if(index>=0){search.Clear();filter.SelectedIndex=0;categoryFilter.SelectedIndex=0;ApplyFilter();grid.ClearSelection();grid.CurrentCell=grid.Rows[index].Cells[1];grid.Rows[index].Selected=true;ShowDetail();}
   status.Text=L.Get("Text056")+package.Manifest.name+L.Get("Text057");Append(status.Text);ShowResult(L.Get("Text058"),package.Manifest.name+" · "+package.Manifest.version+Environment.NewLine+string.Format(L.Get("ImportedScopes"),rows.Count(r=>r.Options!=null&&r.Options.Any(o=>Data.Same(o.Key,package.Folder))))+Environment.NewLine+L.Get("Text059"),new List<string>());
  }
  async void Browse(TextBox field) {using(var d=new FolderBrowserDialog()){if(Directory.Exists(field.Text))d.SelectedPath=field.Text;if(d.ShowDialog()==DialogResult.OK){field.Text=d.SelectedPath;await Scan(true);}}}
  void SaveSettings(){string settingFile=Path.Combine(store,"settings.json");if(settings.uiSchema<2&&File.Exists(settingFile)){Directory.CreateDirectory(Path.Combine(store,"SettingsBackups"));File.Copy(settingFile,Path.Combine(store,"SettingsBackups","before-light-"+DateTime.UtcNow.ToString("yyyyMMddHHmmssfff")+"-"+Guid.NewGuid().ToString("N")+".json"));}settings.uiSchema=2;RememberTarget();settings.language=L.Language;settings.serverMode=serverMode;settings.workshopRoot=workshop.Text.Trim();Data.Write(Path.Combine(store,"settings.json"),settings);}
  List<string> workshopLocations=new List<string>(),pendingScopeIds=new List<string>();
  Engine GetEngine(Row row=null){string scope=row==null?null:row.Package==null?row.Installed.scopeId:row.Package.Manifest.scopeId;return GetScopeEngine(scope);}
  Engine GetScopeEngine(string scope){var e=new Engine(game.Text.Trim(),store,preview,serverMode,scope,workshopLocations);e.Progress=Report;return e;}
  void SetBusy(bool value,bool scanning=false){busy=value;SetExperienceBusy(value);foreach(var c in actions)c.Enabled=!value;if(cancelScan!=null)cancelScan.Enabled=value&&scanCancellation!=null;grid.Enabled=versionGrid.Enabled=!value;game.Enabled=workshop.Enabled=!value;UseWaitCursor=value;progress.Style=value&&scanning?ProgressBarStyle.Marquee:ProgressBarStyle.Continuous;if(value)progress.Value=0;UpdateVersionActions();UpdateSelection();}
  async Task Scan(bool notify=false,bool find=false) {
   if(busy)return;
   scanRuns++;if(notify)rowErrors.Clear();scanCancellation=new CancellationTokenSource();SetBusy(true,true);bool completed=false;string targetName=serverMode?L.Get("Text060"):L.Get("Text061");string operation=find?L.Get("Text062")+targetName+L.Get("Text063"):L.Get("Text064");status.Text=L.Get("Text065")+operation+L.Get("Text066");Append(status.Text);
   try {
    string gamePath=game.Text.Trim(),workshopPath=workshop.Text.Trim();var folders=settings.packageFolders.ToArray();
    Func<ScanResult> collect=delegate{var issues=new List<string>();var libraries=preview?new List<string>():SteamFinder.Libraries(issues);var data=Scanner.Collect(home,store,gamePath,workshopPath,folders,libraries,find,Report,serverMode,scanCancellation.Token);data.Issues.InsertRange(0,issues);return data;};
    var result=preview?collect():await Task.Run(collect);game.Text=result.Game;workshop.Text=result.Workshop;
    if(startupIssue!=null){result.Issues.Add(startupIssue);startupIssue=null;}
    try{SaveSettings();}catch(Exception ex){result.Issues.Add(L.Get("Text067")+Problems.Explain(ex));}
    workshopLocations=result.WorkshopRoots;pendingScopeIds=result.PendingScopes;var available=result.Available;var installed=result.Installed;available.AddRange(imported);
    populating=true;rows.Clear();grid.Rows.Clear();
    foreach(var group in available.GroupBy(p=>p.Manifest.id).OrderBy(g=>g.First().Manifest.name)) {var options=PackageVersions.Options(group);string saved;settings.versionChoices.TryGetValue(group.Key,out saved);var p=PackageVersions.Choose(options,saved).Package;string reason="";if(serverMode)InstallTarget.SupportsServer(p,out reason);rows.Add(new Row{Package=p,Installed=installed.FirstOrDefault(x=>x.id==p.Manifest.id),ServerIssue=reason,Options=options});}
    foreach(var p in installed.Where(p=>!rows.Any(r=>r.Package!=null&&r.Package.Manifest.id==p.id)))rows.Add(new Row{Installed=p});
    foreach(var row in rows){var p=row.Package;int index=grid.Rows.Add(false,p==null?row.Installed.name:p.Manifest.name,null,"",p==null?L.Get("Text068"):L.Text(p.Origin));var cell=(DataGridViewComboBoxCell)grid.Rows[index].Cells[2];if(p!=null){cell.DisplayMember="Label";cell.ValueMember="Key";cell.DataSource=row.Options;cell.Value=p.Folder;}else{cell.Items.Add("—");cell.Value="—";cell.ReadOnly=true;}RefreshRow(index);}
    scannedGame=game.Text.Trim();scannedSource=workshop.Text.Trim();scannedServer=serverMode;RefreshTargets();DetectConflicts(installed);populating=false;SortRows();RefreshVersions();ApplyFilter();
    int count=rows.Count(r=>r.Package!=null&&string.IsNullOrEmpty(r.ServerIssue));string summary=operation+L.Get("Text069")+count+L.Get("Text070")+installed.Count+L.Get("Text071");
    string extra=count==0?L.Get("Text072"):"";
    if(serverMode)extra=L.Get("Text073")+Environment.NewLine+extra;
    if(find){summary=targetName+L.Get("Text074")+(result.FoundGame?L.Get("Text075")+targetName+"。":L.Get("Text076")+targetName+"。");
     extra=L.Get("Text077")+targetName+L.Get("Text078")+(string.IsNullOrWhiteSpace(result.Game)?L.Get("Text079"):result.Game)+Environment.NewLine
      +L.Get("Text080")+(string.IsNullOrWhiteSpace(result.Workshop)?L.Get("Text081"):result.Workshop)+Environment.NewLine+Environment.NewLine
      +L.Get("Text082")+count+L.Get("Text083")+installed.Count+L.Get("Text071")+Environment.NewLine+extra;
    }
    foreach(string issue in result.Issues)Append(L.Get("Text084")+issue);
    status.Text=summary+(result.Issues.Count>0?L.Get("Text085")+result.Issues.Count+L.Get("Text086"):"");Append(summary);if(extra.Length>0)Append(extra);ShowDetail();
    completed=true;SetBusy(false);progress.Value=100;if(!preview&&(notify||result.Issues.Count>0))ShowResult(summary,extra,result.Issues);
   }catch(OperationCanceledException){status.Text=L.Get("ScanCancelled");Append(status.Text);}catch(Exception ex){status.Text=operation+L.Get("Text087");Error(ex);}
   finally{populating=false;if(scanCancellation!=null){scanCancellation.Dispose();scanCancellation=null;}SetBusy(false);progress.Value=completed?100:0;}
  }
  void ShowResult(string summary,string extra,List<string> issues,string technical=null){using(var dialog=CreateResultDialog(summary,extra,issues,technical))dialog.ShowDialog(this);}
  Form CreateResultDialog(string summary,string extra,List<string> issues,string technical){
   var dialog=new Form{Text=issues.Count>0?L.Get("Text088"):L.Get("Text089"),Size=new Size(780,520),MinimumSize=new Size(640,400),StartPosition=FormStartPosition.CenterParent,Font=Font,MinimizeBox=false,MaximizeBox=false,AutoScaleMode=AutoScaleMode.Dpi};
   var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Padding=new Padding(16)};layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,0));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,54));
   string message=summary+Environment.NewLine+Environment.NewLine+extra+(issues.Count==0?"":Environment.NewLine+Environment.NewLine+string.Join(Environment.NewLine+Environment.NewLine,issues.Select((s,i)=>(i+1)+". "+s)));
   var body=new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,Text=message};layout.Controls.Add(body,0,0);
   var details=new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,WordWrap=false,Text=technical??"",Visible=false};layout.Controls.Add(details,0,1);
   var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,WrapContents=false};var close=new System.Windows.Forms.Button{Text=L.Get("Text090"),AutoSize=true,Height=38,DialogResult=DialogResult.OK,Margin=new Padding(4,8,4,4)};buttons.Controls.Add(close);dialog.AcceptButton=close;dialog.CancelButton=close;
   var copy=new System.Windows.Forms.Button{Text=L.Get("CopyDetails"),AutoSize=true,Height=38,Margin=close.Margin};copy.Click+=delegate{CopyText(message+(string.IsNullOrEmpty(technical)?"":Environment.NewLine+technical));};buttons.Controls.Add(copy);
   if(!string.IsNullOrEmpty(technical)){var toggle=new System.Windows.Forms.Button{Text=L.Get("TechnicalDetails"),AutoSize=true,Height=38,Margin=close.Margin};toggle.Click+=delegate{details.Visible=!details.Visible;layout.RowStyles[1].Height=details.Visible?140:0;};buttons.Controls.Add(toggle);}
   layout.Controls.Add(buttons,0,2);dialog.Controls.Add(layout);Theme(dialog);return dialog;
  }
  void ShowDetail(){RefreshDetailActions();var row=CurrentRowModel();if(row==null){detail.Text=L.Get("SelectModHint");return;}var p=row.Package;var installed=row.Installed;var latest=LatestPackage(row);string id=RowId(row);List<string> categories;settings.modCategories.TryGetValue(id,out categories);string target=row.TargetRoot??game.Text;string origin=p==null?"—":p.Folder;detail.Text=string.Join(Environment.NewLine,new[]{
    L.Get("BasicInfo"),p==null?installed.name:p.Manifest.name,"ID: "+id,L.Get("Text031")+": "+(p==null?L.Get("Text068"):L.Text(p.Origin)),L.Get("Categories")+": "+(categories==null||categories.Count==0?"—":string.Join(", ",categories)),"",
    L.Get("VersionInfo"),L.Get("InstalledVersion")+": "+(installed==null?"—":installed.version),L.Get("SelectedVersion")+": "+(p==null?"—":PackageVersions.Display(p)),L.Get("LatestVersion")+": "+(latest==null?L.Get("VersionUncertain"):PackageVersions.Display(latest)),"",
    L.Get("PathsInfo"),L.Get("Text118")+ShortPath(target),L.Get("Text035")+": "+ShortPath(origin),row.ServerIssue,row.TargetIssue,row.Conflict,RowError(row),"",p==null?"":p.Manifest.description,p==null?"":InstallTarget.Instructions(p,serverMode)}.Where(t=>t!=null));hints.SetToolTip(detail,L.Get("Text118")+target+Environment.NewLine+L.Get("Text035")+": "+origin);}
  void RefreshRow(int index){var row=rows[index];var p=row.Package;var old=row.Installed;string reason="";if(p!=null&&serverMode)InstallTarget.SupportsServer(p,out reason);row.ServerIssue=reason;
   string state=old==null?L.Get("Text092"):p==null?L.Get("Text093"):HasUpdate(row)?L.Get("Text094")+old.version:VersionOrder.Newer(old.version,p.Manifest.version)?L.Get("Text095")+old.version:L.Get("Text096")+old.version;
   if(reason.Length>0)state=old==null?L.Get("Text097"):L.Get("Text098");if(!string.IsNullOrEmpty(row.TargetIssue))state=L.Get("TargetUnavailable");string error;string id=p==null?old.id:p.Manifest.id;if(!string.IsNullOrEmpty(row.Conflict))state=L.Get("ConflictState");if(rowErrors.TryGetValue(id,out error))state=L.Get("ErrorState");if(!string.IsNullOrEmpty(row.Operation))state=row.Operation;grid.Rows[index].Cells[3].Value=state;grid.Rows[index].Cells[3].ToolTipText=error??row.Conflict??reason;grid.Rows[index].Cells[1].ToolTipText=id+Environment.NewLine+(p==null?"":p.Manifest.description);grid.Rows[index].Cells[2].ToolTipText=p==null?"":PackageVersions.Display(p);grid.Rows[index].Cells[3].Style.ForeColor=!string.IsNullOrEmpty(row.Conflict)||rowErrors.ContainsKey(id)?Color.FromArgb(220,38,38):reason.Length>0?Color.FromArgb(180,83,9):old==null?Color.FromArgb(100,116,139):p!=null&&VersionOrder.Newer(p.Manifest.version,old.version)?Color.FromArgb(37,99,235):Color.FromArgb(22,130,74);grid.Rows[index].Cells[4].Value=p==null?L.Get("Text068"):L.Text(p.Origin);grid.Rows[index].Cells[0].ReadOnly=reason.Length>0&&old==null;
   if(reason.Length>0&&old==null)grid.Rows[index].Cells[0].Value=false;
  }
  void ChangeVersion(int index){var row=rows[index];if(row.Options==null)return;var key=Convert.ToString(grid.Rows[index].Cells[2].Value);var option=row.Options.FirstOrDefault(o=>Data.Same(o.Key,key));if(option==null)return;row.Package=option.Package;settings.versionChoices[row.Package.Manifest.id]=option.Key;DetectConflicts(rows.Where(r=>r.Installed!=null).Select(r=>r.Installed).ToList());ShowDetail();RefreshVersions();ApplyFilter();try{SaveSettings();}catch(Exception ex){Error(ex);}}
  void LegacyRefreshVersions(){versionRows=rows.Where(r=>r.Options!=null).SelectMany(r=>r.Options).ToList();versionGrid.Rows.Clear();foreach(var option in versionRows){var row=rows.First(r=>r.Package!=null&&r.Package.Manifest.id==option.Package.Manifest.id);string state=Data.Same(row.Package.Folder,option.Key)?L.Get("Text099"):"";if(row.Installed!=null&&VersionOrder.Compare(row.Installed.version,option.Package.Manifest.version)==0)state+=(state.Length>0?"；":"")+L.Get("Text100");versionGrid.Rows.Add(option.Package.Manifest.name,option.Label,state,L.Text(option.Package.Origin),option.Key);}UpdateVersionActions();}
  VersionOption CurrentVersion(){return versionGrid.CurrentRow==null||versionGrid.CurrentRow.Index>=versionRows.Count?null:versionRows[versionGrid.CurrentRow.Index];}
  void UpdateVersionActions(){if(removeVersion==null)return;UpdateGroupActions();var option=CurrentVersion();removeVersion.Enabled=!busy&&option!=null&&PackageVersions.CanRemove(option.Package,store);if(useVersionButton!=null)useVersionButton.Enabled=!busy&&option!=null;if(openVersionButton!=null)openVersionButton.Enabled=!busy&&option!=null;}
  void UseVersion(){var option=CurrentVersion();if(option==null)return;int index=rows.FindIndex(r=>r.Package!=null&&r.Package.Manifest.id==option.Package.Manifest.id);if(index<0)return;search.Text="";filter.SelectedIndex=0;ApplyFilter();grid.Rows[index].Cells[2].Value=option.Key;grid.CurrentCell=grid.Rows[index].Cells[2];tabs.SelectedIndex=0;}
  void OpenVersion(){var option=CurrentVersion();if(option==null)return;try{if(!Directory.Exists(option.Key))throw new Exception(L.Get("Text101"));Process.Start("explorer.exe","\""+option.Key+"\"");}catch(Exception ex){Error(ex);}}
  async Task RemoveVersion(){var option=CurrentVersion();if(option==null||busy)return;try{var p=option.Package;string root=PackageVersions.ImportRoot(p,store);if(Dialogs.Show(L.Get("Text102")+p.Manifest.name+" · "+PackageVersions.Display(p)+"\n\n"+p.Folder+L.Get("Text103"),Text,MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
   SetBusy(true);string archive=await Task.Run(()=>PackageVersions.RemoveImport(p,store,settings));imported.RemoveAll(q=>PackageVersions.Under(q.Folder,root));SaveSettings();Append(L.Get("Text104")+archive);SetBusy(false);await Scan();
  }catch(Exception ex){Error(ex);}finally{SetBusy(false);}}
  async Task CheckUpdates(){try{Append(L.Get("Text105"));var update=await Task.Run(()=>ManagerUpdates.Fetch());if(IsDisposed)return;if(update==null){Append(L.Get("Text106")+ManagerUpdates.Current+"。");return;}Append(L.Get("Text107")+update.tag_name);if(!busy&&Dialogs.Show(L.Get("Text108")+update.tag_name+L.Get("Text109")+ManagerUpdates.Current+L.Get("Text110"), L.Get("Text111"),MessageBoxButtons.YesNo,MessageBoxIcon.Information)==DialogResult.Yes)Process.Start(update.html_url);}catch(Exception ex){if(!IsDisposed)Append(L.Get("Text112")+ex.Message+L.Get("Text113"));}}
  List<Row> Selected(){grid.EndEdit();return grid.Rows.Cast<DataGridViewRow>().Where(r=>r.Visible&&Convert.ToBoolean(r.Cells[0].Value)).Select(r=>rows[r.Index]).ToList();}
  async Task Install() {
   if(busy)return;
   try {if(!CatalogMatchesTarget())throw new Exception(L.Get("PathsNeedScan"));var chosen=Selected();if(busy)return;if(chosen.Count==0)throw new Exception(L.Get("Text114"));if(chosen.Any(r=>r.Package==null))throw new Exception(L.Get("Text115"));
    foreach(var row in chosen)InstallTarget.Validate(row.Package,serverMode);if(!ConfirmInstall(chosen))return;var engines=chosen.ToDictionary(r=>r,r=>GetEngine(r));if(engines.Values.Any(e=>e.NeedsRecovery)){await Run(delegate{foreach(var e in engines.Values.GroupBy(x=>x.State).Select(g=>g.First()))e.RecoverPending();});return;}
    bool allowDowngrade=chosen.Any(r=>r.Installed!=null&&(VersionOrder.Newer(r.Installed.version,r.Package.Manifest.version)||VersionOrder.Compare(r.Installed.version,r.Package.Manifest.version)==null));if(allowDowngrade&&Dialogs.Show(L.Get("Text116")+string.Join("\n",chosen.Where(r=>r.Installed!=null&&(VersionOrder.Newer(r.Installed.version,r.Package.Manifest.version)||VersionOrder.Compare(r.Installed.version,r.Package.Manifest.version)==null)).Select(r=>r.Package.Manifest.name+"："+r.Installed.version+" → "+r.Package.Manifest.version))+L.Get("Text117"),Text,MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
    if(chosen.Any(r=>r.Package.Manifest.kind=="module")&&Dialogs.Show(L.Get("Text118")+(serverMode?L.Get("Text020"):L.Get("Text019"))+"\n"+game.Text+L.Get("Text119"),Text,MessageBoxButtons.YesNo,MessageBoxIcon.Information)!=DialogResult.Yes)return;
    var approvals=new Dictionary<string,Dictionary<string,string>>();
    foreach(var row in chosen){SetBusy(true,true);Dictionary<string,string> collisions;try{collisions=await Task.Run(()=>engines[row].Collisions(row.Package));}finally{SetBusy(false);}if(collisions.Count>0){string list=string.Join("\n",collisions.Keys.Take(8));if(Dialogs.Show("「"+row.Package.Manifest.name+L.Get("Text120")+collisions.Count+L.Get("Text121")+list+L.Get("Text122"),Text,MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;}approvals[row.Package.Manifest.id]=collisions;}
    bool server=serverMode;bool success=await Run(delegate{foreach(var row in chosen){SetRowOperation(row,L.Get(row.Installed==null?"InstallingState":"UpdatingState"));try{engines[row].Install(row.Package,approvals[row.Package.Manifest.id],allowDowngrade);}catch(Exception ex){RecordRowError(row,ex);throw;}finally{SetRowOperation(row,null);}string text=InstallTarget.Instructions(row.Package,server);if(!string.IsNullOrEmpty(text))Report(100,text);}});
    if(success){string instructions=L.Get("Text123")+(server?L.Get("Text060"):L.Get("Text019"))+"：\n"+game.Text+"\n\n"+string.Join("\n\n",chosen.Select(r=>r.Package.Manifest.name+"：\n"+InstallTarget.Instructions(r.Package,server)));Dialogs.Show(instructions,L.Get("Text124"),MessageBoxButtons.OK,MessageBoxIcon.Information);}
   }catch(Exception ex){Error(ex);}
  }
  async Task Uninstall(){if(busy)return;try{if(!CatalogMatchesTarget())throw new Exception(L.Get("PathsNeedScan"));var chosen=Selected();if(chosen.Count==0||chosen.Any(r=>r.Installed==null))throw new Exception(L.Get("Text125"));if(Dialogs.Show(L.Get("ReviewUninstall")+Environment.NewLine+game.Text+Environment.NewLine+Environment.NewLine+string.Join(Environment.NewLine,chosen.Select(r=>r.Installed.name+" · "+r.Installed.version+Environment.NewLine+(r.TargetRoot??game.Text)))+Environment.NewLine+Environment.NewLine+L.Get("RestoreOriginals"),Text,MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;var uninstallEngines=chosen.ToDictionary(r=>r,r=>GetEngine(r));await Run(delegate{foreach(var row in chosen){SetRowOperation(row,L.Get("RemovingState"));try{uninstallEngines[row].Uninstall(row.Installed.id);}catch(Exception ex){RecordRowError(row,ex);throw;}finally{SetRowOperation(row,null);}}});}catch(Exception ex){Error(ex);}}
  async Task<bool> Run(Action action){SetBusy(true);status.Text=L.Get("Text128");
   bool success=false;string finalText="";try{SaveSettings();await Task.Run(action);success=true;finalText=L.Get("Text129");}catch(Exception ex){Error(ex);finalText=L.Get("Text130")+ex.Message;}finally{SetBusy(false);}await RefreshInstalledState();status.Text=finalText;progress.Value=success?100:0;Append(finalText);return success;}
  void Report(int value,string message){if(InvokeRequired){BeginInvoke(new Action<int,string>(Report),value,message);return;}progress.Value=Math.Max(0,Math.Min(100,value));status.Text=message;if(value==100||DateTime.UtcNow-lastProgressLog>TimeSpan.FromMilliseconds(300)){lastProgressLog=DateTime.UtcNow;Append(message);}}
  void Append(string message){AddActivity(message);}
  void Error(Exception ex){string message=Problems.Explain(ex);Append(L.Get("Text131")+message);status.Text=L.Get("Text132")+ex.Message;if(preview)throw ex;ShowResult(L.Get("Text133"),"",new List<string>{message},ex.ToString());}
  public void Preview(string file){ShowInTaskbar=false;Opacity=0;StartPosition=FormStartPosition.Manual;Location=new Point(-20000,-20000);Show();Application.DoEvents();PerformLayout();using(var bitmap=new Bitmap(ClientSize.Width,ClientSize.Height)){Controls[0].DrawToBitmap(bitmap,new Rectangle(0,0,ClientSize.Width,ClientSize.Height));bitmap.Save(file,System.Drawing.Imaging.ImageFormat.Png);}Hide();}
 }
 static class Program {
  [STAThread] static int Main(string[] args){
   try{if(args.Length>0&&args[0]=="--self-test")return Tests.Run(args.Length>1?args[1]:Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"TestResults"));
    if(args.Length>=3&&args[0]=="--test-package")return Tests.ExistingPackage(args[1],args[2]);
    if(args.Length>=3&&args[0]=="--test-server-package")return Tests.ExistingPackage(args[1],args[2],true);
    if(args.Length>=3&&args[0]=="--test-zip")return Tests.ExistingZip(args[1],args[2]);
    Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);string home=AppDomain.CurrentDomain.BaseDirectory;string store=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"UnturnedModManager");
    if(args.Length>=2&&args[0]=="--workflow-test")return WorkflowTests.Run(args[1]);
    if(args.Length>=3&&args[0]=="--test-scope-zip")return WorkflowTests.RealZip(args[1],args[2]);
    if(args.Length>=4&&args[0]=="--test-localization")return WorkflowTests.RealPackage(args[1],args[2],args[3]);
    if(args.Length>=2&&args[0]=="--ui-test"){return ExperienceTests.Run(args[1]);}
    if(args.Length>=3&&args[0]=="--preview"){using(var f=new MainForm(home,args[2],true))f.Preview(args[1]);return 0;}
    using(var f=new MainForm(home,store))Application.Run(f);return 0;
   }catch(Exception ex){if(args.Length>0){Console.Error.WriteLine(ex);return 1;}Dialogs.Show(ex.Message,L.Get("Text000"));return 1;}
  }
}
}
