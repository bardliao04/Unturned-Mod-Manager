using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace UnturnedModManager {
 public static class PreflightTests {
  static readonly List<string> results=new List<string>();
  static void Check(bool value,string message){if(!value)throw new Exception("FAIL "+message);results.Add("PASS "+message);Console.WriteLine(results.Last());}
  static string Put(string root,string relative,string value){string path=Path.Combine(root,relative.Replace('/',Path.DirectorySeparatorChar));Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,value);return path;}
  static Package Locale(string root,string game,int count){
   var manifest=new PackageManifest{schemaVersion=1,id="test.preflight",name="Large read-only check",version="1.0",kind="localization",files=new List<PackageFile>()};
   const string folder="Bundles/Test/Long/Shared/Deep/Tree/";
   string dependency=folder+"Shared.asset",dependencyHash=Data.Hash(Put(game,dependency,"original shared definition"));
   for(int i=0;i<count;i++){
    // Localization's per-file source can share a sibling .asset in one directory.
    string target="Localization/Tchinese/Long/Shared/Deep/Tree/f"+i.ToString("D5")+".dat";
    string source="payload/"+target,original=target.Replace("Localization/Tchinese/","Localization/English/");
    string shared="Localization/Tchinese/Long/Shared/Deep/Tree/Shared.asset";
    if(i==0)dependencyHash=Data.Hash(Put(game,shared,"original shared definition"));
    manifest.files.Add(new PackageFile{source=source,target=target,sha256=Data.Hash(Put(root,source,"translation "+i)),source_file=original,source_sha256=Data.Hash(Put(game,original,"original "+i)),source_dependencies=new List<SourceDependency>{new SourceDependency{relative=shared,sha256=dependencyHash}}});
   }
   Data.Write(Path.Combine(root,Packages.ManifestName),manifest);return Packages.Load(root,"fixture");
  }
  static Package Module(string root,string id,int count){
   var manifest=new PackageManifest{schemaVersion=1,id=id,name=id,version="1.0",kind="module",files=new List<PackageFile>()};
   for(int i=0;i<count;i++){string target="Modules/"+id+"/Long/Shared/Deep/Tree/f"+i+".dll",source="payload/"+target;manifest.files.Add(new PackageFile{source=source,target=target,sha256=Data.Hash(Put(root,source,"payload "+i))});}
   Data.Write(Path.Combine(root,Packages.ManifestName),manifest);return Packages.Load(root,"fixture");
  }
  public static int Run(string output){using(var deadline=new TestDeadline(120000)){
   string root=Path.Combine(Path.GetFullPath(output),"preflight-"+Guid.NewGuid().ToString("N")),game=Path.Combine(root,"game"),state=Path.Combine(root,"state");
   Put(game,"Unturned.exe","fixture");var package=Locale(Path.Combine(root,"locale"),game,2000);var engine=new Engine(game,state,true);
   int reads,hits,paths,pathHits;var watch=Stopwatch.StartNew();
   using(var session=new PreflightSession(CancellationToken.None)){engine.Preflight(package,true);reads=session.HashReads;hits=session.HashHits;paths=session.PathLookups;pathHits=session.PathHits;}
   Check(PreflightSession.Current==null,"preflight context is disposed before any transaction can write");
   Check(hits>=1999&&pathHits>2000,"shared source hashes and parent checks are reused only within read-only preflight");
   Check(reads<4100&&paths<8500,"large check performs bounded file reads and path lookups");
   results.Add("BENCHMARK files=2000 elapsed="+watch.ElapsedMilliseconds+"ms hashReads="+reads+" hashHits="+hits+" pathLookups="+paths+" pathHits="+pathHits);
   Console.WriteLine(results.Last());
   var first=package.Manifest.files[0];
   string payload=Paths.Safe(package.Folder,first.source), original=Paths.Safe(game,first.source_file);
   using(File.Open(payload,FileMode.Open,FileAccess.Read,FileShare.None))
   using(File.Open(original,FileMode.Open,FileAccess.Read,FileShare.None)){
    watch.Restart();
    var plan=BatchPlanner.Build(new[]{package},p=>engine,true);
    Check(plan.Single().Safe&&ReferenceEquals(plan[0].Package,package),"fast batch planning uses the selected manifest without opening payload or game source files");
    Console.WriteLine("BENCHMARK fast plan files=2000 elapsed="+watch.ElapsedMilliseconds+"ms");
   }
   var cancellation=new CancellationTokenSource();bool cancelled=false;int reports=0;
   try{BatchPlanner.Build(new[]{package},p=>new Engine(game,state,true),true,cancellation.Token,(v,m)=>{if(++reports==1)cancellation.Cancel();});}catch(OperationCanceledException){cancelled=true;}
   Check(cancelled&&reports==1&&PreflightSession.Current==null,"cancellation leaves the planner instead of becoming a skipped package");
   Check(!Directory.Exists(Path.Combine(game,"Localization","Tchinese","Long","Shared","Deep","Tree"))||Directory.GetFiles(Path.Combine(game,"Localization","Tchinese","Long","Shared","Deep","Tree"),"*.dat").Length==0,"cancelled check installs no translation files");
   Check(!Directory.Exists(state)&&!Directory.GetFiles(game,".umm-write-check-*").Any(),"cancelled check leaves no registry, backup or permission probe");
   File.AppendAllText(payload,"tamper");bool refused=false;
   try{engine.Install(package,null,false,true);}catch(Exception ex){refused=ex.Message.StartsWith(L.Get("Text171"),StringComparison.Ordinal);}
   Check(refused&&!engine.NeedsRecovery&&engine.ReadRegistry().packages.Count==0&&!File.Exists(Paths.Safe(game,first.target)),"the installer refuses tampered payloads during staging before writing game files");

   var firstInstall=Module(Path.Combine(root,"existing"),"test.first-install",2);
   string existingTarget=Put(game,firstInstall.Manifest.files[0].target,"original file");
   using(var stop=new CancellationTokenSource()){
    engine.Progress=(value,message)=>stop.Cancel();
    bool stopped=false;
    try{engine.Install(firstInstall,null,false,true,stop.Token);}catch(OperationCanceledException){stopped=true;}
    engine.Progress=delegate{};
    Check(stopped&&!engine.NeedsRecovery&&File.ReadAllText(existingTarget)=="original file"&&engine.ReadRegistry().packages.Count==0&&!Directory.GetDirectories(game,".umm-transaction-*").Any(),"stopping during staging leaves existing files intact and clears staged files");
   }
   engine.FailAfter=0;bool rolledBack=false;
   try{engine.Install(firstInstall,null,false,true);}catch(Exception ex){rolledBack=ex.Message==L.Get("Text177");}
   Check(rolledBack&&!engine.NeedsRecovery&&File.ReadAllText(existingTarget)=="original file","first-install rollback restores the shared original backup");
   engine.FailAfter=-1;engine.Install(firstInstall,null,false,true);
   Check(!Directory.Exists(Path.Combine(engine.State,"pending-backups")),"first installation backs up the overwritten file once and needs no temporary backup copy");
   var update=Module(Path.Combine(root,"update"),firstInstall.Manifest.id,2);
   update.Manifest.version="2.0";
   var updatedFile=update.Manifest.files[1];
   updatedFile.sha256=Data.Hash(Put(update.Folder,updatedFile.source,"changed"));
   Data.Write(Path.Combine(update.Folder,Packages.ManifestName),update.Manifest);
   var previousWrite=DateTime.UtcNow.AddDays(-3);
   File.SetLastWriteTimeUtc(existingTarget,previousWrite);
   previousWrite=File.GetLastWriteTimeUtc(existingTarget);
   engine.Install(update,null,false,true,skipIdentical:true);
   Check(engine.LastWrittenFiles==1&&engine.LastUnchangedFiles==1&&File.GetLastWriteTimeUtc(existingTarget)==previousWrite&&File.ReadAllText(Paths.Safe(game,updatedFile.target))=="changed","regular update writes only changed files and preserves the timestamp of verified unchanged files");
   File.Delete(existingTarget);
   engine.Install(update,null,false,true,skipIdentical:true);
   Check(engine.LastWrittenFiles==1&&File.Exists(existingTarget),"regular update restores a missing file even when its recorded hash matches the new package");
   VerifyInterruptedMove(engine,game,update);
   using(var stop=new CancellationTokenSource()){
    engine.Progress=(value,message)=>{if(value>=25)stop.Cancel();};
    bool stopped=false;
    try{engine.Uninstall(update.Manifest.id,stop.Token);}catch(OperationCanceledException){stopped=true;}
    engine.Progress=delegate{};
    Check(stopped&&engine.ReadRegistry().packages.Count==1&&File.Exists(existingTarget)&&!engine.NeedsRecovery&&!Directory.GetDirectories(game,".umm-transaction-*").Any(),"stopping removal during preparation preserves both the files and installed record");
   }
   engine.Uninstall(update.Manifest.id);
   Check(File.ReadAllText(existingTarget)=="original file"&&!Directory.GetDirectories(game,".umm-transaction-*").Any()&&!Directory.Exists(Path.Combine(engine.State,"pending-backups")),"uninstall restores the saved original and clears temporary backups");

   string uiRoot=Path.Combine(root,"ui"),uiGame=Path.Combine(uiRoot,"game"),uiState=Path.Combine(uiRoot,"state"),sources=Path.Combine(uiRoot,"sources");
   Put(uiGame,"Unturned.exe","fixture");Module(Path.Combine(sources,"large"),"test.large",2000);
   for(int i=0;i<24;i++)Module(Path.Combine(sources,"small"+i),"test.small"+i,1);
   Data.Write(Path.Combine(uiState,"settings.json"),new Settings{language="zh-TW",gameRoot=uiGame,workshopRoot=sources,theme="graphite",packageFolders=new List<string>(),versionChoices=new Dictionary<string,string>()});
   using(var form=new MainForm(uiRoot,uiState,true)){
    Exception failure=null;form.Shown+=async delegate{try{await form.VerifyPreflightUi(root,results);}catch(Exception ex){failure=ex;}finally{form.EndFixture();form.Close();}};
    Application.Run(form);if(failure!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
   }
   File.WriteAllLines(Path.Combine(root,"preflight-results.txt"),results);Console.WriteLine("Preflight evidence: "+root);return 0;
  }}
  static void VerifyInterruptedMove(Engine engine,string game,Package package){
   string folder=".umm-transaction-"+Guid.NewGuid().ToString("N");
   string directory=Directory.CreateDirectory(Path.Combine(game,folder)).FullName;
   string registry=Path.Combine(engine.State,"installed.json"),pending=Path.Combine(engine.State,"pending.json");
   var first=package.Manifest.files[0];var second=package.Manifest.files[1];
   string firstTarget=Paths.Safe(game,first.target),secondTarget=Paths.Safe(game,second.target);
   string before=File.ReadAllText(registry);
   var journal=new Journal{schemaVersion=3,gameRoot=game,ownerGameRoot=game,localFolder=folder,hadRegistry=true,priorRegistry=before,files=new List<Snapshot>{
    new Snapshot{target=first.target,saved="r0.bak",existed=true,hash=first.sha256,expectedHash=second.sha256},
    new Snapshot{target=second.target,saved="r1.bak",existed=true,hash=second.sha256,expectedAbsent=true}
   }};
   Data.Write(pending,journal);
   File.Move(firstTarget,Path.Combine(directory,"r0.bak"));
   File.WriteAllText(secondTarget,"external edit");
   bool refused=false;
   try{engine.RecoverPending();}catch(IOException ex){refused=ex.Message.StartsWith(L.Get("RecoveryModified"),StringComparison.Ordinal);}
   Check(refused&&engine.NeedsRecovery&&!File.Exists(firstTarget)&&File.ReadAllText(secondTarget)=="external edit","interrupted move recovery refuses to overwrite an unrelated edit and leaves rollback files available");
   File.WriteAllText(secondTarget,"changed");
   engine.RecoverPending();
   Check(!engine.NeedsRecovery&&!Directory.Exists(directory)&&Data.Hash(firstTarget)==first.sha256&&File.ReadAllText(registry)==before,"restart recovery restores a file interrupted between its two moves and leaves untouched files in place");
  }
 }
 public partial class MainForm {
  public async Task VerifyPreflightUi(string output,List<string> results){
   CheckVisible(true);AssertExperience(rows.Count==25,"preflight fixture has a large package and a scrollable mod list",results);
   foreach(string language in L.Languages){
    ChangeLanguage(Array.IndexOf(L.Languages,language));tabs.SelectedIndex=0;
    var started=new ManualResetEventSlim();var release=new ManualResetEventSlim();
    PreflightTestStarted=delegate{started.Set();if(!release.Wait(5000))throw new TimeoutException("Preflight fixture gate");};
    int beats=0;using(var heartbeat=new System.Windows.Forms.Timer{Interval=20}){
     heartbeat.Tick+=delegate{beats++;};heartbeat.Start();
     Task operation=InstallBatch();
     try{
      var until=Stopwatch.StartNew();while(!started.IsSet&&until.ElapsedMilliseconds<3000)await Task.Delay(10);
      AssertExperience(started.IsSet&&busy&&batchCancellation!=null,"preflight is running on a worker",results);
      await Task.Delay(180);
      AssertExperience(beats>=3&&cancelScan.Enabled&&cancelScan.Text==L.Get("CancelPreflight"),"UI heartbeat and actual cancel button remain usable during slow preflight",results);
      AssertExperience(grid.Enabled&&grid.ReadOnly&&!UseWaitCursor,"busy list stays scrollable and readable while editing is blocked",results);
      grid.FirstDisplayedScrollingRowIndex=grid.RowCount-1;grid.CurrentCell=grid.Rows[grid.RowCount-1].Cells[1];
      AssertExperience(grid.FirstDisplayedScrollingRowIndex>0,"mouse-scroll target can move to the end while checking",results);
      tabs.SelectedIndex=2;AssertExperience(navButtons.All(b=>b.Enabled),"operation log navigation remains enabled",results);tabs.SelectedIndex=0;
      cancelScan.PerformClick();AssertExperience(batchCancellation.IsCancellationRequested&&!cancelScan.Enabled,"clicking Cancel requests cancellation exactly once",results);
     }finally{release.Set();}
     if(await Task.WhenAny(operation,Task.Delay(5000))!=operation)throw new Exception("Preflight cancellation did not complete");await operation;
     heartbeat.Stop();
    }
    PreflightTestStarted=null;started.Dispose();release.Dispose();
    AssertExperience(!busy&&batchCancellation==null&&grid.Enabled&&!grid.ReadOnly&&status.Text==L.Get("PreflightCancelled"),"cancel restores normal controls and a clear final status",results);
    AssertExperience(Directory.GetFiles(game.Text,"*",SearchOption.AllDirectories).Length==1&&!Directory.Exists(Path.Combine(store,"Installs")),"UI preflight cancellation changes no game or installation records",results);
   }
  }
 }
}
