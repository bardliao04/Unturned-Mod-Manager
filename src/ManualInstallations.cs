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
   var owned=(knownRegistry??ReadRegistry()).packages.SingleOrDefault(p=>p.id==selected.Manifest.id);
   // Existing manager ownership is authoritative. Do not reread a different
   // installer's history on every scan or before the normal preflight.
   if(owned!=null)return null;
   bool matches=selected.Manifest.files.All(f=>File.Exists(Target(f.target))&&Data.Same(Data.Hash(Target(f.target)),f.sha256));
   if(matches){var package=Packages.Reload(selected);VerifyIncoming(package);return new ManualInstallInfo{Version=package.Manifest.version,Preserve=true};}
   // Installer records are advisory. Stale or absent history must not prevent
   // the normal first-install path from backing up and updating existing files.
   if(selected.Manifest.kind!="localization"||selected.Manifest.packageIdentity==null||!File.Exists(Path.Combine(assistant,"installed.json")))return null;
   try{Paths.CheckLinks(assistant);using(var gate=AssistantLock(assistant)){
    var record=Data.Read<AssistantRecord>(Paths.Safe(assistant,"installed.json"));
    if(record==null||record.product!="Unturned.AI.Tchinese"||string.IsNullOrEmpty(record.gameRoot)||!Data.Same(Path.GetFullPath(record.gameRoot).TrimEnd('\\'),Root)||record.files==null||record.files.Count==0||!VersionOrder.Newer(record.version,selected.Manifest.version))return null;
    var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(var f in record.files){if(f==null||!Data.ValidHash(f.hash)||!names.Add(f.relative))return null;Paths.CheckTarget(f.relative,"localization",ScopeId);if(!File.Exists(Target(f.relative))||!Data.Same(Data.Hash(Target(f.relative)),f.hash))return null;}
    if(!selected.Manifest.files.Any(f=>names.Contains(f.target)))return null;
    return new ManualInstallInfo{Version=record.version,Preserve=true};
   }}catch{return null;}
  }
 }

 public partial class MainForm {
  Task DetectManualRows(){if(serverMode||!InstallTarget.IsRoot(game.Text,false))return Task.FromResult(0);var snapshots=rows.Where(r=>r.Package!=null).ToArray();string root=game.Text;var roots=workshopLocations.ToArray();Action detect=()=>{var engines=new Dictionary<string,Engine>();var registries=new Dictionary<string,Registry>();foreach(var row in snapshots)try{row.ManualIssue=null;string scope=row.Package.Manifest.scopeId??"base";Engine e;if(!engines.TryGetValue(scope,out e)){e=new Engine(root,store,preview,false,row.Package.Manifest.scopeId,roots);engines[scope]=e;registries[scope]=e.ReadRegistry();}row.Manual=e.DetectManualInstall(row.Package,assistantStateResolver(e.Root),registries[scope]);}catch(Exception ex){row.Manual=null;row.ManualIssue=Problems.Explain(ex);}};if(preview){detect();return Task.FromResult(0);}return Task.Run(detect);}
 }
}
