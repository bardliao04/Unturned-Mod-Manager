using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using System.Threading;
using System.Threading.Tasks;
namespace UnturnedModManager {
 sealed class TestDeadline : IDisposable {readonly System.Threading.Timer timer;public TestDeadline(int milliseconds){timer=new System.Threading.Timer(delegate{Console.Error.WriteLine("FAIL UI test exceeded its deadline; possible message-loop starvation");Environment.Exit(1);},null,milliseconds,Timeout.Infinite);}public void Dispose(){timer.Dispose();}}
 public static class ResponsiveTests {
  static List<string> results;static void Check(bool value,string text){if(!value)throw new Exception("FAIL "+text);results.Add("PASS "+text);Console.WriteLine(results.Last());}
  public static int Run(string output){using(var deadline=new TestDeadline(90000)){results=new List<string>();string root=Path.Combine(Path.GetFullPath(output),"responsive-"+Guid.NewGuid().ToString("N")),source=Path.Combine(root,"sources","mod"),game=Path.Combine(root,"game"),state=Path.Combine(root,"state");Directory.CreateDirectory(source);Directory.CreateDirectory(game);File.WriteAllText(Path.Combine(game,"Unturned.exe"),"fixture only");File.WriteAllText(Path.Combine(source,"a.dll"),"fixture mod");Data.Write(Path.Combine(source,Packages.ManifestName),new PackageManifest{schemaVersion=1,id="test.responsive",name="Responsive fixture",version="1.0.0",kind="module",files=new List<PackageFile>{new PackageFile{source="a.dll",target="Modules/Responsive/a.dll",sha256=Data.Hash(Path.Combine(source,"a.dll"))}}});string zip=Path.Combine(root,"mod.zip");ZipFile.CreateFromDirectory(source,zip);
   var package=Packages.Load(source,"fixture");string target=Paths.Safe(game,package.Manifest.files[0].target);Directory.CreateDirectory(Path.GetDirectoryName(target));File.WriteAllText(target,"original fixture");var engine=new Engine(game,state,true);engine.Install(package,engine.Collisions(package));File.SetLastWriteTimeUtc(target,new DateTime(2000,1,1,0,0,0,DateTimeKind.Utc));var installedTime=File.GetLastWriteTimeUtc(target);engine.Install(package,null);Check(engine.LastWrittenFiles==0&&engine.LastUnchangedFiles==1&&File.GetLastWriteTimeUtc(target)==installedTime,"identical updates verify content without rewriting files");File.Delete(target);engine.Install(package,null);Check(engine.LastWrittenFiles==1&&File.Exists(target),"missing managed files are repaired rather than skipped");File.WriteAllText(target,"manual edit");bool protectedEdit=false;try{engine.Install(package,null);}catch{protectedEdit=true;}Check(protectedEdit&&File.ReadAllText(target)=="manual edit","unchanged optimization still protects external modifications");File.WriteAllText(target,File.ReadAllText(Path.Combine(source,"a.dll")));engine.Uninstall(package.Manifest.id);Check(File.ReadAllText(target)=="original fixture","unchanged updates retain the original uninstall backup");
   var parserTasks=Enumerable.Range(0,4).Select(worker=>Task.Run(()=>{for(int i=0;i<200;i++)if(Data.Parse<Dictionary<string,int>>("{\"value\":7}")["value"]!=7)throw new Exception("JSON parse corruption");})).ToArray();Task.WaitAll(parserTasks);Check(true,"per-thread JSON readers preserve concurrent parsing correctness");
   var mailbox=new ProgressMailbox();var clock=Stopwatch.StartNew();for(int i=0;i<50000;i++)mailbox.Publish(i%101,"progress "+i);Check(mailbox.Received==50000&&mailbox.Delivered==0,"progress publishing never queues UI delegates");var packet=mailbox.Take();Check(packet!=null&&mailbox.Take()==null&&mailbox.Delivered==1,"fifty thousand progress reports merge into one pending packet");results.Add("BENCHMARK mailbox 50000 reports="+clock.ElapsedMilliseconds+"ms; delivered=1");
   var cancellation=new CancellationTokenSource();cancellation.Cancel();bool cancelled=false;try{Packages.ImportZip(zip,Path.Combine(root,"cancelled"),null,cancellation.Token);}catch(OperationCanceledException){cancelled=true;}Check(cancelled&&!Directory.Exists(Path.Combine(root,"cancelled","imports")),"cancelled import never publishes a source or touches a game");cancellation.Dispose();
   string crowded=Path.Combine(root,"too-many.zip");using(var archive=ZipFile.Open(crowded,ZipArchiveMode.Create))for(int i=0;i<25001;i++)archive.CreateEntry("file"+i);bool limited=false;try{Packages.ImportZip(crowded,Path.Combine(root,"limited"));}catch(Exception ex){limited=ex.Message==L.Get("Text152");}Check(limited&&!Directory.Exists(Path.Combine(root,"limited","imports")),"ZIP entry limit remains enforced before publishing");
   L.Language="zh-TW";string prefix=L.Get("Text131");string error=L.Get("Text163")+"Localization/Tchinese/Editor/EditorEnvironment.dat"+Environment.NewLine+L.Get("Text210")+L.Get("Text201");L.Language="zh-CN";string localized=L.ManagerMessage(error);Check(localized.StartsWith(L.Get("Text163"))&&localized.Contains(L.Get("Text201")),"stored manager diagnostics switch to Simplified Chinese");Check(L.ManagerMessage(prefix+error).StartsWith(L.Get("Text131")+L.Get("Text163")),"nested stored errors localize without changing file identifiers");Check(localized.Contains("Localization/Tchinese/Editor/EditorEnvironment.dat"),"diagnostic localization preserves technical paths");
   Data.Write(Path.Combine(state,"settings.json"),new Settings{language="zh-TW",gameRoot=game,workshopRoot=Path.Combine(root,"sources"),theme="graphite"});using(var form=new MainForm(root,state,true)){Exception failed=null;form.Shown+=async delegate{try{await form.VerifyResponsive(root,zip,results);}catch(Exception ex){failed=ex;}finally{form.EndFixture();form.Close();}};Application.Run(form);if(failed!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failed).Throw();}File.WriteAllLines(Path.Combine(root,"responsive-results.txt"),results);Console.WriteLine("Responsive evidence: "+root);return 0;}}
 }
 public partial class MainForm {
  [System.Runtime.InteropServices.DllImport("user32.dll",EntryPoint="SendMessageW")]static extern IntPtr SendTestKey(IntPtr handle,int message,IntPtr key,IntPtr flags);
  public async Task VerifyResponsive(string output,string zip,List<string> results){
   int scans=scanRuns;
   AssertExperience(navKeys.SequenceEqual(new[]{"Text024","ProblemCenter","Settings"}),"navigation exposes mods, troubleshooting and settings",results);
   foreach(string code in L.Languages){
    ChangeLanguage(Array.IndexOf(L.Languages,code));tabs.SelectedIndex=0;PerformLayout();Application.DoEvents();
    var commands=batchBar.Controls.Cast<Control>().ToArray();
    AssertExperience(commands.SequenceEqual(new Control[]{installButton,updateRecommended,uninstallButton})&&commands.All(c=>c.Visible),"main view exposes only the three batch actions in "+code,results);
    AssertExperience(filter.Items.Count==4,"mod filter shows the basic installation states in "+code,results);
   }
   VerifyCompactChrome(output,results);
   var picker=(StyledComboBox)themeChoice;SendTestKey(picker.Handle,0x104,(IntPtr)0x28,IntPtr.Zero);AssertExperience(picker.OpenChoices!=null&&picker.OpenChoices.Items.Count==4,"Alt+Down opens the themed selector menu",results);picker.OpenChoices.Close();
   tabs.SelectedIndex=0;int pulses=0;long maxGap=0;var wall=Stopwatch.StartNew();long last=0;using(var heartbeat=new System.Windows.Forms.Timer{Interval=25}){heartbeat.Tick+=delegate{long now=wall.ElapsedMilliseconds;maxGap=Math.Max(maxGap,now-last);last=now;pulses++;};heartbeat.Start();SetBusy(true);await Task.Run(()=>{for(int block=0;block<40;block++){for(int n=0;n<1000;n++)Report(n%100,"progress fixture");Thread.Sleep(10);}});Report(12,"stale progress fixture");status.Text="terminal fixture";SetBusy(false);AssertExperience(status.Text=="terminal fixture","late progress cannot overwrite a completed or cancelled result",results);heartbeat.Stop();}AssertExperience(pulses>=3&&maxGap<2000,"UI heartbeat continues during forty thousand progress events",results);AssertExperience(progressMailbox.Received>=40000&&progressMailbox.Delivered<100,"progress pumping stays bounded under load",results);results.Add("BENCHMARK UI heartbeat pulses="+pulses+" maxGap="+maxGap+"ms; progress received="+progressMailbox.Received+" delivered="+progressMailbox.Delivered);
   Package importedPackage=await ImportFromFile(zip);AssertExperience(importedPackage!=null&&scanRuns==scans,"ZIP import updates the catalog without rescanning Workshop",results);AssertExperience(rows.Single().Options.Count==2,"incremental import preserves the independent original source",results);var importedOption=rows.Single().Options.Single(o=>Data.Same(o.Key,importedPackage.Folder));checkedVersions.Clear();SetVersionChecked(importedOption,true);await ExecuteFixtureOperation(()=>ArchiveCheckedVersions());AssertExperience(scanRuns==scans&&rows.Single().Options.Count==1,"archive removes only the affected catalog source without rescanning",results);AssertExperience(Directory.Exists(rows.Single().Package.Folder),"incremental archive preserves external source files",results);
   detail.Text=string.Join(Environment.NewLine,Enumerable.Range(0,160).Select(i=>"Readable line "+i));var scroll=(ScrollSurface)detail.Parent.Parent;scroll.Sync();scroll.Wheel(-120);AssertExperience(detail.SelectionLength==0,"themed detail scrolling does not select or change text",results);AssertExperience(grid.ScrollBars==ScrollBars.None&&versionGrid.ScrollBars==ScrollBars.None&&FindControls(this).OfType<StyledComboBox>().Count()>=8,"main lists and selectors use the shared controls",results);
   foreach(int page in new[]{0,1,2,3,4}){tabs.SelectedIndex=page;await Task.Delay(40);}AssertExperience(true,"hidden scrollable pages do not starve the message loop",results);tabs.SelectedIndex=3;ChangeLanguage(2);PerformLayout();Application.DoEvents();using(var image=new Bitmap(ClientSize.Width,ClientSize.Height)){Controls[0].DrawToBitmap(image,new Rectangle(Point.Empty,ClientSize));image.Save(Path.Combine(output,"settings-zh-CN.png"));}
   tabs.SelectedIndex=0;for(int i=0;i<80;i++){var package=rows[0].Package;rows.Add(new Row{Package=package,Options=rows[0].Options});}SortRows();ApplyFilter();var gridSkin=((SmoothGrid)grid).Skin;gridSkin.Sync();gridSkin.Wheel(-120);AssertExperience(grid.FirstDisplayedScrollingRowIndex>0,"themed grid scrollbar moves through actual rows",results);rows.RemoveRange(1,rows.Count-1);SortRows();
  }
  void VerifyCompactChrome(string output,List<string> results){
   var original=ClientSize;
   bool collapsed=settings.navigationCollapsed;
   ChangeLanguage(0);
   ClientSize=new Size(1366,768);PerformLayout();Application.DoEvents();
   var filterCard=(SectionPanel)filters.Parent;
   var listCard=(SectionPanel)detailSplit.Panel1.Controls[0];
   var detailCard=FindControls(detailSplit.Panel2).OfType<SectionPanel>().Single();
   var leftAction=openCurrent.RectangleToScreen(openCurrent.ClientRectangle);
   var rightAction=historyCurrent.RectangleToScreen(historyCurrent.ClientRectangle);
   var detailEdge=detailCard.RectangleToScreen(detailCard.ClientRectangle);
   var input=(InputFrame)search.Parent;
   var inputBounds=filterCard.RectangleToClient(input.RectangleToScreen(input.ClientRectangle));
   var centers=filters.Controls.Cast<Control>().Select(c=>c.Top+c.Height/2).ToArray();
   AssertExperience(filterCard.Height<=64&&centers.Max()-centers.Min()<=1&&Math.Abs(inputBounds.Top-(filterCard.ClientSize.Height-inputBounds.Bottom))<=1&&
    filters.Controls.Cast<Control>().All(c=>filters.ClientRectangle.Contains(c.Bounds)),
    "search row uses one compact line with equal padding above and below (height="+filterCard.Height+", input="+inputBounds+", centers="+string.Join(",",centers)+")",results);
   AssertExperience(Math.Abs(listCard.PointToScreen(Point.Empty).Y-detailEdge.Top)<=1&&
    leftAction.Top==rightAction.Top&&leftAction.Height==44&&leftAction.Bottom==rightAction.Bottom&&
    rightAction.Left-leftAction.Right==WorkspaceGap&&Math.Abs(leftAction.Width-rightAction.Width)<=1&&
    leftAction.Left==detailEdge.Left&&rightAction.Right==detailEdge.Right,
    "detail actions are 44 pixels high with equal widths and consistent spacing",results);
   float idleHeight=workspaceRoot.RowStyles[5].Height;
   AssertExperience(!progress.Visible&&idleHeight==status.Font.Height+WorkspaceGap,"idle footer uses only one status line",results);
   SetBusy(true);Application.DoEvents();
   AssertExperience(progress.Visible&&progress.Height>0&&workspaceRoot.RowStyles[5].Height>idleHeight,"running work expands the footer for progress",results);
   SetBusy(false);
   status.GetType().GetMethod("OnClick",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(status,new object[]{EventArgs.Empty});
   AssertExperience(jobSummary.Visible&&workspaceRoot.RowStyles[5].Height>idleHeight,"clicking status expands the existing operation details",results);
   status.GetType().GetMethod("OnClick",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(status,new object[]{EventArgs.Empty});
   AssertExperience(!jobSummary.Visible&&workspaceRoot.RowStyles[5].Height==idleHeight,"closing the details restores the compact footer",results);
   var navCard=(SectionPanel)navigation.Parent;
   if(!settings.navigationCollapsed)navToggle.PerformClick();
   foreach(bool expanded in new[]{false,true}){
    if(expanded)navToggle.PerformClick();
    Refresh();Application.DoEvents();
    using(var border=new Bitmap(navCard.Width,navCard.Height)){
     navCard.DrawToBitmap(border,new Rectangle(Point.Empty,border.Size));
     int center=border.Width/2;
     bool continuous=Enumerable.Range(0,3).All(offset=>{
      int x=center+offset-1,y=border.Height/2;
      return border.GetPixel(x,1).ToArgb()==Themes.Current.Border.ToArgb()&&
       border.GetPixel(1,y+offset-1).ToArgb()==Themes.Current.Border.ToArgb()&&
       border.GetPixel(border.Width-2,y+offset-1).ToArgb()==Themes.Current.Border.ToArgb();
     });
     AssertExperience(continuous&&settings.navigationCollapsed!=expanded,"navigation border paints continuously when "+(expanded?"expanded":"collapsed"),results);
    }
    using(var frame=new Bitmap(ClientSize.Width,ClientSize.Height)){
     Controls[0].DrawToBitmap(frame,new Rectangle(Point.Empty,ClientSize));
     frame.Save(Path.Combine(output,expanded?"v1.3-expanded.png":"v1.3-collapsed.png"));
    }
   }
   settings.navigationCollapsed=collapsed;UpdateNavigation();ClientSize=original;
  }
 }
}
