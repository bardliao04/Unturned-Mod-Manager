using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Forms;
namespace UnturnedModManager {
 public sealed class InstallPreflight {public Package Package;public Engine Engine;public string Error;public bool NeedsRecordReview;public Dictionary<string,string> Collisions;public bool Safe{get{return Error==null;}}}
 public static class BatchPlanner {
  public static List<InstallPreflight> Build(IEnumerable<Package> packages,Func<Package,Engine> engine,bool overwrite=false,CancellationToken cancellation=default(CancellationToken),Action<int,string> report=null){
   using(var session=new PreflightSession(cancellation)){
    cancellation.ThrowIfCancellationRequested();var sources=packages.ToArray();var plan=new List<InstallPreflight>();
    for(int i=0;i<sources.Length;i++){
     cancellation.ThrowIfCancellationRequested();var package=sources[i];int index=i;
     string title=string.Format(L.Get("PreflightPackage"),i+1,sources.Length,ModNames.Display(package));
     if(report!=null)report(i*100/Math.Max(1,sources.Length),title);
     var item=new InstallPreflight{Package=package};
     try{
      item.Engine=engine(package);
      if(overwrite){
       // Compare against the selected manifest when the installer reloads it under the lock.
       item.Engine.CheckInstallReady(package);
      }else{
       item.Engine.Progress=(value,message)=>{cancellation.ThrowIfCancellationRequested();if(report!=null)report((index*100+Math.Min(100,value))/Math.Max(1,sources.Length),title+" · "+message);};
       item.Package=Packages.Reload(package);
       item.Collisions=item.Engine.Preflight(item.Package);
      }
     }catch(OperationCanceledException){throw;}catch(Exception ex){item.Error=Problems.Explain(ex);item.NeedsRecordReview=ex is InstalledContentChangedException&&item.Package.Manifest.kind=="localization"&&item.Package.Manifest.packageIdentity!=null;}
     plan.Add(item);
    }
    var destinations=new Dictionary<string,InstallPreflight>(StringComparer.OrdinalIgnoreCase);
    foreach(var item in plan.Where(p=>p.Engine!=null))foreach(var file in item.Package.Manifest.files){
     cancellation.ThrowIfCancellationRequested();string key=Path.GetFullPath(Path.Combine(item.Engine.Root,file.target));InstallPreflight owner;
     if(destinations.TryGetValue(key,out owner)&&owner!=item){owner.Error=item.Error=L.Get("BatchOverlap");owner.NeedsRecordReview=item.NeedsRecordReview=false;}else destinations[key]=item;
    }
    cancellation.ThrowIfCancellationRequested();if(report!=null)report(100,L.Get("PreflightReady"));return plan;
   }
  }
 } public partial class MainForm {
#if TEST_BUILD
  internal Action PreflightTestStarted;
#endif
  async Task InstallBatch(bool skipIdentical=false){if(busy)return;try{if(!CatalogMatchesTarget())throw new Exception(L.Get("PathsNeedScan"));var chosen=Selected();if(chosen.Count==0)throw new Exception(L.Get("Text114"));var sourceRows=chosen.Where(r=>r.Package!=null).ToList();var map=sourceRows.ToDictionary(r=>RowId(r));string root=game.Text;bool server=serverMode;var roots=workshopLocations.ToArray();List<InstallPreflight> plan;var sources=sourceRows.Select(r=>r.Package).ToArray();batchCancellation=new CancellationTokenSource();var cancellation=batchCancellation.Token;
   SetBusy(true,true);status.Text=L.Get("Preflight");try{plan=await Task.Run(()=>{
#if TEST_BUILD
    if(PreflightTestStarted!=null)PreflightTestStarted();
#endif
    return BatchPlanner.Build(sources,p=>new Engine(root,store,preview,server,p.Manifest.scopeId,roots),true,cancellation,Report);
   });}finally{SetBusy(false);}
   cancellation.ThrowIfCancellationRequested();
   foreach(var item in plan.Where(p=>p.Safe))rowErrors.Remove(item.Package.Manifest.id);
   foreach(var item in plan.Where(p=>!p.Safe)){rowErrors[item.Package.Manifest.id]=item.Error;AddActivity(item.Error,"ErrorLog",item.Package.Manifest.id,L.Get("Preflight"));}
   var safe=plan.Where(p=>p.Safe).ToList();int skipped=chosen.Count-safe.Count;
   var issues=plan.Where(p=>!p.Safe).Select(p=>ModNames.Display(p.Package)+Environment.NewLine+p.Error).ToList();
   RefreshProblems();
   if(safe.Count==0){
    status.Text=L.Get("NothingSafe");progress.Value=0;batchCompleted=batchSuccess=batchFailed=0;batchSkipped=chosen.Count;UpdateProgressPanel();
    ShowResult(status.Text,string.Format(L.Get("PreflightSummary"),0,plan.Count,chosen.Count-plan.Count),issues);return;
   }
   bool downgrade=safe.Any(i=>{var row=map[i.Package.Manifest.id];return row.Installed!=null&&(VersionOrder.Newer(row.Installed.version,i.Package.Manifest.version)||VersionOrder.Compare(row.Installed.version,i.Package.Manifest.version)==null);});
   if(downgrade&&Dialogs.Show(L.Get("Text116")+L.Get("Text117"),Text,MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
   var completed=new List<Package>();
   var blocked=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   batchCompleted=batchSuccess=batchFailed=0;batchSkipped=skipped;batchWriting=true;SetBusy(true);
   try{
    for(int index=0;index<safe.Count;index++){
     if(cancellation.IsCancellationRequested){batchSkipped+=safe.Count-index;break;}
     var item=safe[index];var row=map[item.Package.Manifest.id];
     if(blocked.Contains(item.Engine.State)){batchSkipped++;issues.Add(ModNames.Display(item.Package)+Environment.NewLine+L.Get("PendingBlocksInstall"));continue;}
     SetBatchItem(item.Engine,index,safe.Count,ModNames.Display(item.Package));
     SetRowOperation(row,L.Get(row.Installed==null?"InstallingState":"UpdatingState"));
     try{
      await Task.Run(()=>item.Engine.Install(item.Package,null,downgrade,true,cancellation,skipIdentical));
      rowErrors.Remove(RowId(row));row.Manual=null;row.ManualIssue=null;completed.Add(item.Package);batchSuccess++;batchCompleted++;
      AddActivity(L.Get("InstallComplete")+" "+string.Format(L.Get("FileOutcome"),item.Engine.LastWrittenFiles,item.Engine.LastUnchangedFiles),"Success",RowId(row),L.Get("Text009"));
     }catch(OperationCanceledException){batchSkipped+=safe.Count-index;break;}
     catch(Exception ex){
      batchFailed++;batchCompleted++;RecordRowError(row,ex);issues.Add(batchModule+Environment.NewLine+RowError(row));
      if(item.Engine.NeedsRecovery)blocked.Add(item.Engine.State);
     }finally{SetRowOperation(row,null);UpdateProgressPanel();}
    }
   }finally{batchModule=null;SetBusy(false);}
   await RefreshInstalledState();string summary=string.Format(L.Get("BatchCounts"),batchCompleted,batchSuccess,batchSkipped,batchFailed);status.Text=summary;progress.Value=batchFailed==0&&batchSkipped==0?100:0;AddActivity(summary,batchFailed>0?"ErrorLog":batchSkipped>0?"WarningLog":"Success");RefreshProblems();string instructions=string.Join(Environment.NewLine+Environment.NewLine,completed.Select(p=>ModNames.Display(p)+Environment.NewLine+InstallTarget.Instructions(p,server)));if(cancellation.IsCancellationRequested){summary=L.Get("BatchStopped")+" · "+summary;status.Text=summary;AddActivity(summary,"WarningLog");}ShowResult(summary,instructions,issues);
  }catch(OperationCanceledException){status.Text=L.Get("PreflightCancelled");progress.Value=0;AddActivity(status.Text,"Info");}
   catch(Exception ex){Error(ex);}
   finally{FinishBatchOperation();}
  }

  void SetBatchItem(Engine engine,int index,int count,string name){
   batchModule=string.Format(L.Get("BatchItem"),index+1,count,name);
   UpdateProgressPanel();status.Text=batchModule;
   string caption=batchModule;
   engine.Progress=(percent,message)=>Report((index*100+percent)/count,caption+" · "+message);
  }

  void FinishBatchOperation(){
   batchWriting=batchRemoving=false;
   if(batchCancellation!=null){batchCancellation.Dispose();batchCancellation=null;}
   SetBusy(false);
   if(closeAfterBatch){closeAfterBatch=false;BeginInvoke(new Action(Close));}
  }
 }
}
