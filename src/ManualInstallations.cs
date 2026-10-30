using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace UnturnedModManager {
 public sealed class ManualInstallInfo {public string Version,AssistantState,RecordHash;public bool Preserve;public AssistantRecord Record;}
 public partial class Engine {
  void VerifyIncoming(Package p){WorkshopTargets.VerifySources(p,Root);foreach(var f in p.Manifest.files){string path=Paths.Safe(p.Folder,f.source);if(!File.Exists(path)||!Data.Same(Data.Hash(path),f.sha256))throw new Exception(L.Get("Text171")+f.source);}}
  public ManualInstallInfo DetectManualInstall(Package selected,string assistant,Registry knownRegistry=null){
   var package=selected;var owned=(knownRegistry??ReadRegistry()).packages.SingleOrDefault(p=>p.id==package.Manifest.id);string recordPath=Path.Combine(assistant,"installed.json");
   if(package.Manifest.kind!="localization"||package.Manifest.packageIdentity==null){if(owned==null&&package.Manifest.files.All(f=>File.Exists(Target(f.target))&&Data.Same(Data.Hash(Target(f.target)),f.sha256))){package=Packages.Reload(selected);VerifyIncoming(package);return new ManualInstallInfo{Version=package.Manifest.version,Preserve=true};}return null;}package=Packages.Reload(selected);
   if(!File.Exists(recordPath)){
    if(owned!=null)return null;
    if(package.Manifest.files.All(f=>File.Exists(Target(f.target))&&Data.Same(Data.Hash(Target(f.target)),f.sha256))){VerifyIncoming(package);return new ManualInstallInfo{Version=package.Manifest.version,Preserve=true};}return null;
   }
   if(ScopeId==null&&package.Manifest.packageIdentity!="unturned.ai.tchinese")return null;Paths.CheckLinks(assistant);using(var gate=AssistantLock(assistant)){
    if(NeedsRecovery||File.Exists(Paths.Safe(assistant,"transaction.json")))throw new Exception(L.Get("ReconcilePending"));
    var record=Data.Read<AssistantRecord>(recordPath);if(record==null||record.product!="Unturned.AI.Tchinese"||string.IsNullOrEmpty(record.gameRoot)||!Data.Same(Path.GetFullPath(record.gameRoot).TrimEnd('\\'),Root)||!VersionOrder.Valid(record.version)||record.files==null||record.files.Count==0)throw new Exception(L.Get("ReconcileRecordMismatch"));
    var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(var f in record.files){if(f==null||!Data.ValidHash(f.hash)||!names.Add(f.relative))throw new Exception(L.Get("ReconcileRecordMismatch"));Paths.CheckTarget(f.relative,"localization",ScopeId);Target(f.relative);}
    // A manager-owned installation continues to use its existing verification.
    var recorded=record.files.ToDictionary(f=>f.relative,StringComparer.OrdinalIgnoreCase);if(owned!=null&&owned.version==record.version&&owned.files.Count==record.files.Count&&owned.files.All(f=>{AssistantOwned proof;return recorded.TryGetValue(f.target,out proof)&&Data.Same(proof.hash,f.hash);}))return null;
    if(owned!=null){try{VerifyOwned(owned);return null;}catch(InstalledContentChangedException){}}
    if(!package.Manifest.files.Any(f=>names.Contains(f.target)))return null;
    foreach(var f in record.files)if(!File.Exists(Target(f.relative))||!Data.Same(Data.Hash(Target(f.relative)),f.hash))throw new Exception(L.Get("ManualContentChanged")+f.relative);
    int? order=VersionOrder.Compare(package.Manifest.version,record.version);if(!order.HasValue)throw new Exception(L.Get("VersionUncertain"));
    if(order==0){if(!names.SetEquals(package.Manifest.files.Select(f=>f.target))||package.Manifest.files.Any(f=>!Data.Same(record.files.Single(r=>Data.Same(r.relative,f.target)).hash,f.sha256)))throw new Exception(L.Get("ReconcileRecordMismatch"));VerifyIncoming(package);}
    return new ManualInstallInfo{Version=record.version,AssistantState=assistant,RecordHash=Data.Hash(recordPath),Record=record,Preserve=order<=0};
   }
  }
  public Package StageManualEvidence(Package incoming,ManualInstallInfo reviewed){
   CheckGame();var current=DetectManualInstall(incoming,reviewed.AssistantState);if(current==null||current.Preserve||current.RecordHash!=reviewed.RecordHash)throw new Exception(L.Get("ReconcileChanged"));VerifyIncoming(incoming);
   string folder=Paths.Safe(State,"ManualEvidence/"+Guid.NewGuid().ToString("N"));var files=new List<PackageFile>();foreach(var f in current.Record.files){string relative="payload/"+f.relative,dest=Paths.Safe(folder,relative);Directory.CreateDirectory(Path.GetDirectoryName(dest));File.Copy(Target(f.relative),dest,false);if(!Data.Same(Data.Hash(dest),f.hash))throw new Exception(L.Get("ReconcileChanged"));files.Add(new PackageFile{target=f.relative,source=relative,sha256=f.hash});}
   Data.Write(Path.Combine(folder,Packages.ManifestName),new PackageManifest{schemaVersion=1,id=incoming.Manifest.id,packageIdentity=incoming.Manifest.packageIdentity,scopeId=ScopeId,name=incoming.Manifest.name,kind="localization",version=current.Version,files=files});return Packages.Load(folder,incoming.Origin);
  }
 }
 public partial class MainForm {
  async Task<List<string>> PrepareManualInstalls(List<Row> selected){var preserved=new List<string>();string root=game.Text;bool server=serverMode;var roots=workshopLocations.ToArray();foreach(var row in selected.ToArray()){row.ManualIssue=null;try{
   if(server)continue;Engine engine=null;ManualInstallInfo manual=null;SetBusy(true,true);status.Text=L.Get("ManualChecking");try{await Task.Run(()=>{engine=new Engine(root,store,preview,false,row.Package.Manifest.scopeId,roots);manual=engine.DetectManualInstall(row.Package,assistantStateResolver(engine.Root));});}finally{SetBusy(false);}row.Manual=manual;if(manual==null)continue;
   if(manual.Preserve){rowErrors.Remove(RowId(row));preserved.Add(RawName(row)+" · "+manual.Version);selected.Remove(row);continue;}
   if(Dialogs.Show(string.Format(L.Get("ManualUpdateReview"),RawName(row),manual.Version,row.Package.Manifest.version,engine.Root),L.Get("ManualUpdateTitle"),MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes){selected.Remove(row);continue;}
   Package evidence=null;SetBusy(true,true);try{evidence=await Task.Run(()=>engine.StageManualEvidence(row.Package,manual));}finally{SetBusy(false);}if(!await ReconcileExisting(new Row{Package=evidence},manual.AssistantState,false,true))selected.Remove(row);else row.Manual=null;
   }catch(Exception ex){row.ManualIssue=Problems.Explain(ex);rowErrors[RowId(row)]=row.ManualIssue;AddActivity(row.ManualIssue,"ErrorLog",RowId(row));}
  }for(int i=0;i<rows.Count;i++)RefreshRow(i);ApplyFilter();RefreshModernTexts();return preserved;}
  Task DetectManualRows(){if(serverMode||!InstallTarget.IsRoot(game.Text,false))return Task.FromResult(0);var snapshots=rows.Where(r=>r.Package!=null).ToArray();string root=game.Text;var roots=workshopLocations.ToArray();Action detect=()=>{var engines=new Dictionary<string,Engine>();var registries=new Dictionary<string,Registry>();foreach(var row in snapshots)try{row.ManualIssue=null;string scope=row.Package.Manifest.scopeId??"base";Engine e;if(!engines.TryGetValue(scope,out e)){e=new Engine(root,store,preview,false,row.Package.Manifest.scopeId,roots);engines[scope]=e;registries[scope]=e.ReadRegistry();}row.Manual=e.DetectManualInstall(row.Package,assistantStateResolver(e.Root),registries[scope]);}catch(Exception ex){row.Manual=null;row.ManualIssue=Problems.Explain(ex);}};if(preview){detect();return Task.FromResult(0);}return Task.Run(detect);}
 }
}
