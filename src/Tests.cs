using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.IO.Compression;
using System.Text;

namespace UnturnedModManager {
 public static class Tests {
  static int passed; static List<string> lines=new List<string>();
  static void Assert(bool value,string message){if(!value)throw new Exception("FAIL: "+message);passed++;lines.Add("PASS "+message);Console.WriteLine(lines.Last());}
  static void Reject(Action action,string name){bool rejected=false;try{action();}catch{rejected=true;}Assert(rejected,name);}
  static void Put(string root,string rel,string text){string path=Path.Combine(root,rel.Replace('/','\\'));Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,text,Encoding.UTF8);}
  static Package Create(string dir,string version,Dictionary<string,string> files,string id="test.module",string kind="module") {
   Directory.CreateDirectory(dir);var m=new PackageManifest {schemaVersion=1,id=id,name="測試套件",version=version,kind=kind,files=new List<PackageFile>()};
   foreach(var pair in files){string source="payload/"+pair.Key;Put(dir,source,pair.Value);m.files.Add(new PackageFile {source=source,target=pair.Key,sha256=Data.Hash(Path.Combine(dir,source.Replace('/','\\')))});}
   Data.Write(Path.Combine(dir,Packages.ManifestName),m);return Packages.Load(dir,"測試");
  }
  public static int ExistingPackage(string folder,string baseDir,bool server=false) {
   var p=Packages.Load(folder,"既有手動安裝包");string scratch=Path.Combine(Path.GetFullPath(baseDir),"actual-package-"+Guid.NewGuid().ToString("N"));string game=Path.Combine(scratch,server?"server":"game");string executable=server?"Unturned_Headless.exe":"Unturned.exe";Put(game,executable,"fake executable");
   var e=new Engine(game,Path.Combine(scratch,"state"),true,server);e.Install(p,null);
   Assert(e.ReadRegistry().packages.Single().files.Count==p.Manifest.files.Count,"existing manual package installs all "+p.Manifest.files.Count+" files");
   foreach(var f in p.Manifest.files)if(!Data.Same(Data.Hash(Paths.Safe(game,f.target)),f.sha256))throw new Exception("Installed file mismatch: "+f.target);
   Assert(true,"all installed files match original manual package hashes");e.Uninstall(p.Manifest.id);
   Assert(!Directory.GetFiles(game,"*",SearchOption.AllDirectories).Any(f=>!f.EndsWith(executable)),"existing manual package uninstall removes only installed files");
   File.WriteAllLines(Path.Combine(baseDir,server?"actual-server-package-results.txt":"actual-package-results.txt"),lines,Encoding.UTF8);return 0;
  }
  public static int ExistingZip(string zip,string baseDir) {
   string cache=Path.Combine(baseDir,"actual-zip-"+Guid.NewGuid().ToString("N"));
   var p=Packages.ImportZip(zip,cache);Assert(p.Manifest.id=="unturned.ai.tchinese","original release ZIP identified without repackaging");
   Assert(p.Manifest.files.Count==2976,"original release ZIP exposes 2976 translation files");
   foreach(var f in p.Manifest.files)if(!Data.Same(Data.Hash(Paths.Safe(p.Folder,f.source)),f.sha256))throw new Exception("Original ZIP file hash mismatch: "+f.source);
   Assert(true,"all files extracted from original release ZIP match manifest hashes");File.WriteAllLines(Path.Combine(baseDir,"actual-zip-results.txt"),lines,Encoding.UTF8);return 0;
  }
  static void VersionManagementTests(string baseDir){
   string root=Path.Combine(baseDir,"versions-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);var files=new Dictionary<string,string>{{"Modules/VersionTest/a.dll","old"}};
   var old=Create(Path.Combine(root,"old"),"1.0.6",files);files["Modules/VersionTest/a.dll"]="new";var latest=Create(Path.Combine(root,"workshop"),"1.0.7",files);latest.Origin="工作坊";var imported=Create(Path.Combine(root,"imported"),"1.0.7",files);imported.Origin="匯入安裝包";
   var options=PackageVersions.Options(new[]{old,imported,latest,latest});Assert(options.Count==3&&options[0].Package==latest&&options[0].Label.Contains("（推薦）最新版"),"all versions remain selectable and equal-version workshop is recommended without duplicate paths");
   Assert(PackageVersions.Choose(options,old.Folder).Package==old,"remembered older version is retained after scanning");Assert(PackageVersions.Choose(options,Path.Combine(root,"removed")).Package==latest,"missing saved version falls back to recommended latest");
   string game=Path.Combine(root,"game");Put(game,"Unturned.exe","fake");var engine=new Engine(game,Path.Combine(root,"state"),true);engine.Install(latest,null);Reject(()=>engine.Install(old,null),"downgrade requires explicit confirmation flag");engine.Install(old,null,true);Assert(engine.ReadRegistry().packages.Single().version=="1.0.6"&&File.ReadAllText(Paths.Safe(game,"Modules/VersionTest/a.dll"))=="old","confirmed version switch installs selected older content and updates registry");engine.Uninstall(old.Manifest.id);Assert(!File.Exists(Paths.Safe(game,"Modules/VersionTest/a.dll")),"version downgrade retains original uninstall ownership");
   string store=Path.Combine(root,"cache"),guid=Guid.NewGuid().ToString("N"),folder=Path.Combine(store,"imports",guid,"nested");var package=Create(folder,"1.0.7",files);var settings=new Settings{packageFolders=new List<string>{folder,latest.Folder},versionChoices=new Dictionary<string,string>{{package.Manifest.id,folder},{"other",latest.Folder}}};
   string archived=PackageVersions.RemoveImport(package,store,settings);Assert(!Directory.Exists(folder)&&File.Exists(Path.Combine(archived,"nested",Packages.ManifestName))&&settings.packageFolders.SequenceEqual(new[]{latest.Folder})&&!settings.versionChoices.ContainsKey(package.Manifest.id)&&settings.versionChoices.ContainsKey("other"),"removing nested import archives its files and only removes matching source and version preferences");
   Assert(File.Exists(Paths.Safe(latest.Folder,"payload/Modules/VersionTest/a.dll")),"removing import preserves workshop and other sources");Reject(()=>PackageVersions.RemoveImport(latest,store,settings),"workshop source removal is rejected");Reject(()=>PackageVersions.ImportRoot(new Package{Folder=Path.Combine(store,"imports")},store),"import-root removal is rejected");
   string json="{\"tag_name\":\"v1.0.2\",\"html_url\":\"https://github.com/bardliao04/Unturned-Mod-Manager/releases/tag/v1.0.2\",\"draft\":false,\"prerelease\":false}";
   Assert(ManagerUpdates.Parse(json)!=null,"new stable GitHub version triggers update information");Assert(ManagerUpdates.Parse(json.Replace("1.0.2","1.0.1"))==null&&ManagerUpdates.Parse(json.Replace("1.0.2","1.0"))==null,"same and older GitHub versions do not prompt");Assert(ManagerUpdates.Parse(json.Replace("\"prerelease\":false","\"prerelease\":true"))==null,"GitHub prerelease does not trigger stable update prompt");Reject(()=>ManagerUpdates.Parse(json.Replace("https://github.com/","https://example.com/")),"foreign update download page is rejected");Reject(()=>ManagerUpdates.Parse("invalid JSON"),"invalid update response propagates to non-blocking error handler");
   var legacy=Data.Parse<Settings>("{\"gameRoot\":\"old\",\"packageFolders\":[]}");Assert(legacy.gameRoot=="old"&&legacy.versionChoices==null,"existing manager settings remain compatible");
   Assert(ManagerUpdates.Parse(json.Replace("1.0.2","1.0.1.0"))==null,"four-part equivalent manager version does not create a false update prompt");
  }
  public static int Run(string baseDir){
   VersionManagementTests(baseDir);
   string scratch=Path.Combine(Path.GetFullPath(baseDir),Guid.NewGuid().ToString("N"));Directory.CreateDirectory(scratch);string game=Path.Combine(scratch,"game"),state=Path.Combine(scratch,"state");Put(game,"Unturned.exe","fake game");
   string a="Modules/Test/a.dll",b="Modules/Test/b.module";var p=Create(Path.Combine(scratch,"v1"),"1.0.0",new Dictionary<string,string>{{a,"new a"},{b,"new b"}});var e=new Engine(game,state,true);
   Put(game,a,"original");Reject(delegate{e.Install(p,null);},"existing files require explicit overwrite approval");Assert(File.ReadAllText(Paths.Safe(game,a))=="original","conflict changes no files");
   var consent=e.Collisions(p);e.Install(p,consent);Assert(File.ReadAllText(Paths.Safe(game,a))=="new a","install copies payload");Assert(e.ReadRegistry().packages.Count==1,"install records ownership");
   Put(game,a,"external edit");Reject(delegate{e.Uninstall(p.Manifest.id);},"uninstall preserves externally modified files");Assert(File.ReadAllText(Paths.Safe(game,a))=="external edit","external content remains intact");Put(game,a,"new a");
   var p2=Create(Path.Combine(scratch,"v2"),"2.0.0",new Dictionary<string,string>{{a,"version two"}});e.Install(p2,null);Assert(!File.Exists(Paths.Safe(game,b)),"update removes obsolete owned file");Reject(delegate{e.Install(p,null);},"downgrades rejected");e.Uninstall(p.Manifest.id);Assert(File.ReadAllText(Paths.Safe(game,a))=="original","uninstall restores pre-install original");Assert(e.ReadRegistry().packages.Count==0,"uninstall removes record");
   e.FailAfter=0;Reject(delegate{e.Install(p,e.Collisions(p));},"injected installation failure");Assert(File.ReadAllText(Paths.Safe(game,a))=="original"&&!File.Exists(Paths.Safe(game,b)),"failed installation rolls back all game files");Assert(e.ReadRegistry().packages.Count==0&&!e.NeedsRecovery,"failed install rolls back registry");e.FailAfter=-1;
   e.Install(p,e.Collisions(p));e.FailAfter=0;Reject(delegate{e.Uninstall(p.Manifest.id);},"injected uninstall failure");Assert(File.ReadAllText(Paths.Safe(game,a))=="new a"&&e.ReadRegistry().packages.Count==1,"failed uninstall restores installed state");e.FailAfter=-1;
   var other=Create(Path.Combine(scratch,"other"),"1.0.0",new Dictionary<string,string>{{a,"other"}},"other.module");Reject(delegate{e.Install(other,e.Collisions(other));},"different packages cannot own same file");e.Uninstall(p.Manifest.id);
   Put(p.Folder,"payload/"+a,"tampered");Reject(delegate{e.Install(p,e.Collisions(p));},"tampered package rejected");Assert(File.ReadAllText(Paths.Safe(game,a))=="original","hash rejection occurs before mutation");Put(p.Folder,"payload/"+a,"new a");
   foreach(string bad in new[]{"../escape","/absolute","Modules/Test/a.dll:stream","Modules/Test/NUL.txt","Modules/Test/a. ","Modules\\Test\\x"})Reject(delegate{Paths.Safe(game,bad);},"reject unsafe path "+bad);
   Reject(delegate{Paths.CheckTarget("BattlEye/test.dll","module");},"reject writes outside Modules");Reject(delegate{Paths.CheckTarget("Modules/Unturned/test.dll","module");},"reject overwriting official module");
   string zip=Path.Combine(scratch,"evil.zip");using(var archive=ZipFile.Open(zip,ZipArchiveMode.Create)){var entry=archive.CreateEntry("../escape.txt");using(var w=new StreamWriter(entry.Open()))w.Write("bad");}Reject(delegate{Packages.ImportZip(zip,state);},"ZIP traversal rejected");Assert(!File.Exists(Path.Combine(scratch,"escape.txt")),"ZIP cannot escape destination");
   string goodZip=Path.Combine(scratch,"good.zip");ZipFile.CreateFromDirectory(p.Folder,goodZip);var imported=Packages.ImportZip(goodZip,state);Assert(imported.Manifest.id==p.Manifest.id,"valid ZIP imports");
   string workshop=Path.Combine(scratch,"workshop","304930","123456","Installer");Directory.CreateDirectory(workshop);File.Copy(Path.Combine(p.Folder,Packages.ManifestName),Path.Combine(workshop,Packages.ManifestName));var errors=new List<string>();Assert(Packages.Scan(Path.Combine(scratch,"workshop","304930"),"工作坊",errors,4).Count==1,"nested workshop manifest detected");
   e.Install(p,e.Collisions(p));string stateFolder=e.State,saved="transactions/recovery/0.bak";Put(stateFolder,saved,"new a");var journal=new Journal {hadRegistry=true,priorRegistry=File.ReadAllText(Path.Combine(stateFolder,"installed.json")),files=new List<Snapshot>{new Snapshot {target=a,existed=true,saved=saved,hash=Data.Hash(Paths.Safe(stateFolder,saved))}}};Data.Write(Path.Combine(stateFolder,"pending.json"),journal);Put(game,a,"interrupted write");e.RecoverPending();Assert(File.ReadAllText(Paths.Safe(game,a))=="new a"&&!e.NeedsRecovery,"restart journal recovery");e.Uninstall(p.Manifest.id);
   var locale=Create(Path.Combine(scratch,"locale"),"1.0.0",new Dictionary<string,string>{{"Localization/Tchinese/Example.dat","Hello 你好"}},"test.locale","localization");e.Install(locale,null);e.Uninstall(locale.Manifest.id);Assert(!File.Exists(Paths.Safe(game,"Localization/Tchinese/Example.dat")),"localization install and uninstall");
   var corrupt=Packages.Load(p.Folder,"測試");corrupt.Manifest.files[0].target="../escape";Data.Write(Path.Combine(p.Folder,Packages.ManifestName),corrupt.Manifest);Reject(delegate{Packages.Load(p.Folder,"測試");},"manifest path traversal rejected");
   string scanHome=Path.Combine(scratch,"scanhome"),scanStore=Path.Combine(scratch,"scanstate"),scanRoot=Path.Combine(scratch,"scanworkshop");Directory.CreateDirectory(scanRoot);
   var messages=new List<string>();Action<int,string> report=delegate(int n,string message){messages.Add(message);};
   var empty=Scanner.Collect(scanHome,scanStore,game,scanRoot,new string[0],new string[0],false,report);
   Assert(empty.Available.Count==0&&empty.Issues.Count==0&&messages.Any(m=>m.Contains("正在掃描")),"empty valid scan reports progress without inventing an error");
   var missing=Scanner.Collect(scanHome,scanStore,"",Path.Combine(scratch,"missing"),new[]{Path.Combine(scratch,"missingmod")},new string[0],true,report);
   Assert(!missing.FoundGame&&missing.Issues.Any(m=>m.Contains("未在 Steam 遊戲庫找到 Unturned"))&&missing.Issues.Any(m=>m.Contains("工作坊資料夾不存在"))&&missing.Issues.Any(m=>m.Contains("missingmod")),"missing game workshop and remembered mod produce actionable warnings");
   Create(Path.Combine(scanRoot,"good"),"1.0.0",new Dictionary<string,string>{{a,"valid"}});Put(scanRoot,"broken/unturned-manager.json","invalid json");
   var partial=Scanner.Collect(scanHome,scanStore,game,scanRoot,new string[0],new string[0],false,report);
   Assert(partial.Available.Count==1&&partial.Issues.Any(m=>m.Contains("broken")&&m.Contains("處理方式")),"corrupt mod reports reason while retaining valid results");
   string lib1=Path.Combine(scratch,"library1"),lib2=Path.Combine(scratch,"library2"),game1=Path.Combine(lib1,"steamapps/common/Unturned"),game2=Path.Combine(lib2,"steamapps/common/Unturned");Put(game1,"Unturned.exe","fake");Put(game2,"Unturned.exe","fake");
   string ws2=Path.Combine(lib2,"steamapps/workshop/content/304930");Directory.CreateDirectory(ws2);
   var found=Scanner.Collect(scanHome,scanStore,game2,ws2,new string[0],new[]{lib1,lib2},true,report);
   Assert(found.FoundGame&&Data.Same(found.Game,Path.GetFullPath(game2))&&Data.Same(found.Workshop,Path.GetFullPath(ws2))&&found.Issues.Any(m=>m.Contains("多個")),"automatic search preserves selected valid game and reports multiple locations");
   var scanEngine=new Engine(game,scanStore,true);Put(scanEngine.State,"installed.json","broken");
   var badRegistry=Scanner.Collect(scanHome,scanStore,game,scanRoot,new string[0],new string[0],false,report);
   Assert(badRegistry.Available.Count==1&&badRegistry.Issues.Any(m=>m.Contains("讀取安裝紀錄失敗")),"registry failure is surfaced without discarding detected mods");
   Assert(Problems.Explain(new UnauthorizedAccessException("denied")).Contains("權限"),"permission errors include a remedy");
   Assert(Problems.Explain(new IOException("locked",unchecked((int)0x80070020))).Contains("占用"),"locked file errors include a remedy");
   Assert(Problems.Explain(new IOException("disk full",unchecked((int)0x80070070))).Contains("空間不足"),"disk full errors include a remedy");
   Assert(Problems.Explain(new PathTooLongException()).Contains("較短"),"long path errors include a remedy");
   string serverRoot=Path.Combine(scratch,"dedicated");Put(serverRoot,"Unturned_Headless.exe","fake server");var serverEngine=new Engine(serverRoot,state,true,true);
   Reject(delegate{new Engine(serverRoot,state,true);},"headless server root is not silently accepted as a player game");
   Reject(delegate{serverEngine.Install(locale,null);},"server mode refuses localization before copying files");
   string roleConfig="{\"Assemblies\":[{\"Path\":\"/sort.dll\",\"Role\":\"Server\"}]}";
   var serverPackage=Create(Path.Combine(scratch,"serverpackage"),"1.0.0",new Dictionary<string,string>{{"Modules/Sort/sort.dll","server dll"},{"Modules/Sort/sort.module",roleConfig}},"test.server");string reason;
   Assert(InstallTarget.SupportsServer(serverPackage,out reason),"server role with included assembly is recognized");
   serverEngine.Install(serverPackage,null);Assert(File.Exists(Path.Combine(serverRoot,"Modules/Sort/sort.dll"))&&!File.Exists(Path.Combine(game,"Modules/Sort/sort.dll")),"server install writes only to selected server root");
   Assert(serverEngine.ReadRegistry().packages.Count==1&&new Engine(game,state,true).ReadRegistry().packages.Count==0,"server and game roots have independent installation records");
   var upgraded=Create(Path.Combine(scratch,"serverpackage2"),"2.0.0",new Dictionary<string,string>{{"Modules/Sort/sort.dll","new server dll"},{"Modules/Sort/sort.module",roleConfig}},"test.server");serverEngine.Install(upgraded,null);Assert(serverEngine.ReadRegistry().packages.Single().version=="2.0.0","server package can be updated");serverEngine.Uninstall(serverPackage.Manifest.id);Assert(!File.Exists(Path.Combine(serverRoot,"Modules/Sort/sort.dll")),"server uninstall removes own files");
   var clientOnly=Create(Path.Combine(scratch,"clientonly"),"1.0.0",new Dictionary<string,string>{{"Modules/Only/only.dll","client"},{"Modules/Only/only.module","{\"Assemblies\":[{\"Path\":\"/only.dll\",\"Role\":\"Client\"}]}"}},"test.client");Reject(delegate{serverEngine.Install(clientOnly,null);},"client-only module is blocked in server mode");
   var missingDll=Create(Path.Combine(scratch,"missingserverdll"),"1.0.0",new Dictionary<string,string>{{"Modules/Sort/sort.module",roleConfig}},"test.missing");Assert(!InstallTarget.SupportsServer(missingDll,out reason),"declared server role without included DLL is blocked");
   var optional=Create(Path.Combine(scratch,"optional"),"1.0.0",new Dictionary<string,string>{{"Modules/Sort/sort.dll","both"},{"Modules/Sort/sort.module",roleConfig.Replace("Server","Both_Optional")}},"test.both");Assert(InstallTarget.SupportsServer(optional,out reason),"optional shared module role is recognized");
   string serverLib=Path.Combine(scratch,"serverlibrary"),customServer=Path.Combine(serverLib,"steamapps/common/MyDedicated");Put(customServer,"Unturned_Headless.exe","fake server");Put(serverLib,"steamapps/appmanifest_1110390.acf","\"AppState\" { \"installdir\" \"MyDedicated\" }");
   var serverFound=Scanner.Collect(scanHome,scanStore,"",scanRoot,new string[0],new[]{serverLib},true,report,true);Assert(serverFound.FoundGame&&Data.Same(serverFound.Game,Path.GetFullPath(customServer)),"server auto-discovery uses dedicated server Steam manifest");
   var playerFound=Scanner.Collect(scanHome,scanStore,"",scanRoot,new string[0],new[]{serverLib},true,report);Assert(!playerFound.FoundGame,"player discovery never selects dedicated server installation");
   string discoveredWorkshop=Path.Combine(serverLib,"steamapps/workshop/content/304930");Directory.CreateDirectory(discoveredWorkshop);var preservedSource=Scanner.Collect(scanHome,scanStore,"",scanRoot,new string[0],new[]{serverLib},true,report,true);Assert(Data.Same(preservedSource.Workshop,Path.GetFullPath(scanRoot)),"automatic server discovery preserves manually selected mod source");
   Assert(!InstallTarget.Instructions(optional,true).Contains("BattlEye")&&InstallTarget.Instructions(optional,true).Contains("重新啟動"),"server instructions do not reuse client launch instructions");
   lines.Add("TOTAL: "+passed+" checks passed. Test root: "+scratch);File.WriteAllLines(Path.Combine(baseDir,"results.txt"),lines,Encoding.UTF8);Console.WriteLine(lines.Last());return 0;
  }
 }
}
