using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.IO.Compression;
using Microsoft.Win32;

namespace UnturnedModManager {
 public class PackageFile { public string source; public string target; public string sha256; }
 public class PackageManifest {
  public int schemaVersion; public string id; public string name; public string version; public string kind;
  public string displayVersion; public string description; public string afterInstall; public string afterInstallServer; public List<PackageFile> files;
 }
 public class Package { public string Folder; public PackageManifest Manifest; public string Origin; }
 public class OwnedFile { public string target; public string hash; public bool hadOriginal; public string originalHash; }
 public class InstalledPackage { public string id; public string name; public string version; public string kind; public List<OwnedFile> files; }
 public class Registry { public string gameRoot; public List<InstalledPackage> packages = new List<InstalledPackage>(); }
 public class Snapshot { public string target; public bool existed; public string hash; public string saved; }
 public class Journal { public string priorRegistry; public bool hadRegistry; public List<Snapshot> files; }
 public class LegacyVersion { public string product; public string version; public string manifest_sha256; public string display_version; }
 public class LegacyAsset { public string target; public string sha256; public string group; }
 public class Settings { public string gameRoot; public string serverRoot; public bool serverMode; public string workshopRoot; public List<string> packageFolders; public Dictionary<string,string> versionChoices; }
 public class ModuleDescription {public List<ModuleRole> Assemblies;}
 public class ModuleRole {public string Path;public object Role;}
 public static class InstallTarget {
  public static bool IsRoot(string root,bool server){return !string.IsNullOrWhiteSpace(root)&&(File.Exists(Path.Combine(root,"Unturned.exe"))||server&&File.Exists(Path.Combine(root,"Unturned_Headless.exe")));}
  public static string Instructions(Package p,bool server){return server?(string.IsNullOrWhiteSpace(p.Manifest.afterInstallServer)?"安裝完成後重新啟動 Unturned 伺服器，並依 mod 說明操作。":p.Manifest.afterInstallServer):p.Manifest.afterInstall;}
  public static bool SupportsServer(Package p,out string reason){
   reason="";if(p.Manifest.kind!="module"){reason="漢化是玩家端內容，不適用於伺服器。";return false;}
   try{foreach(var file in p.Manifest.files.Where(f=>f.target.EndsWith(".module",StringComparison.OrdinalIgnoreCase))){var config=Data.Read<ModuleDescription>(Paths.Safe(p.Folder,file.source));if(config==null||config.Assemblies==null)continue;
     foreach(var assembly in config.Assemblies){if(assembly==null)continue;string role=Convert.ToString(assembly.Role);if(role!="Server"&&role!="Both_Optional"&&role!="Both_Required"&&role!="2"&&role!="3"&&role!="4")continue;
      if(string.IsNullOrWhiteSpace(assembly.Path))continue;string dll=file.target.Substring(0,file.target.LastIndexOf('/')+1)+assembly.Path.Replace('\\','/').TrimStart('/');Paths.Safe(p.Folder,dll,false);
      if(p.Manifest.files.Any(f=>Data.Same(f.target,dll)))return true;
     }
    }reason="mod 沒有宣告伺服器角色，或安裝清單缺少伺服器 DLL。請使用支援伺服器的版本。";
   }catch(Exception ex){reason="無法確認伺服器支援："+Problems.Explain(ex);}return false;
  }
  public static void Validate(Package p,bool server){string reason;if(server&&!SupportsServer(p,out reason))throw new Exception(reason);}
 }
 public static class Data {
  static JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 33554432 };
  public static T Read<T>(string file) { return Json.Deserialize<T>(File.ReadAllText(file, Encoding.UTF8)); }
  public static T Parse<T>(string value) { return Json.Deserialize<T>(value); }
  public static void Write(string file, object value) {
   Directory.CreateDirectory(Path.GetDirectoryName(file)); string temp = file + ".tmp";
   File.WriteAllText(temp, Json.Serialize(value), new UTF8Encoding(false));
   if(File.Exists(file)) File.Replace(temp,file,null); else File.Move(temp,file);
  }
  public static string Hash(string file) { using(var sha=SHA256.Create()) using(var stream=File.OpenRead(file)) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant(); }
  public static bool Same(string a,string b) { return string.Equals(a,b,StringComparison.OrdinalIgnoreCase); }
  public static bool ValidHash(string hash) { return hash != null && Regex.IsMatch(hash,"^[a-fA-F0-9]{64}$"); }
  public static string Key(string value) { using(var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value.ToLowerInvariant()))).Replace("-", "").Substring(0,24); }
 }
 public static class Paths {
  public static string Safe(string root,string relative,bool checkLinks=true) {
   if(string.IsNullOrWhiteSpace(relative) || relative.Contains('\\') || relative.Contains(':') || relative.StartsWith("/")) throw new Exception("無效的mod路徑："+relative);
   foreach(string part in relative.Split('/')) {
    if(part==""||part=="."||part==".."||part.EndsWith(".")||part.EndsWith(" ")||part.IndexOfAny(Path.GetInvalidFileNameChars())>=0 || Regex.IsMatch(part,"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])($|\\.)",RegexOptions.IgnoreCase)) throw new Exception("拒絕不安全的路徑："+relative);
   }
   root=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
   string result=Path.GetFullPath(Path.Combine(root,relative.Replace('/',Path.DirectorySeparatorChar)));
   if(!result.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) throw new Exception("路徑超出安裝範圍。");
   if(checkLinks)CheckLinks(result); return result;
  }
  public static void CheckLinks(string path) {
   for(string p=Path.GetFullPath(path);p!=null;p=Path.GetDirectoryName(p))
    if((File.Exists(p)||Directory.Exists(p))&&(File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0) throw new Exception("不支援連結或接合目錄："+p);
  }
  public static void CheckTarget(string target,string kind) {
   var parts=target.Split('/');
   bool module = parts.Length>=3 && parts[0]=="Modules" && parts[1]!="Framework" && parts[1]!="Unturned" && parts[1]!="Rocket.Unturned";
   bool locale = target.StartsWith("Localization/Tchinese/",StringComparison.Ordinal)&&target.EndsWith(".dat",StringComparison.OrdinalIgnoreCase)
    || (target.StartsWith("Maps/",StringComparison.Ordinal)||target.StartsWith("Bundles/",StringComparison.Ordinal))&&target.EndsWith("/Tchinese.dat",StringComparison.OrdinalIgnoreCase);
   if(!(kind=="module"?module:kind=="localization"&&locale)) throw new Exception("mod不允許寫入此位置："+target);
  }
 }
 public static class Packages {
  public const string ManifestName="unturned-manager.json";
  public static Package Load(string folder,string origin) {
   Paths.CheckLinks(folder); PackageManifest m;
   if(File.Exists(Path.Combine(folder,ManifestName))) m=Data.Read<PackageManifest>(Path.Combine(folder,ManifestName));
   else {
    string dataFolder=Path.Combine(folder,"Data");string prefix="Data/";
    if(!File.Exists(Path.Combine(dataFolder,"version.json"))){dataFolder=folder;prefix="";}
    var legacy=Data.Read<LegacyVersion>(Path.Combine(dataFolder,"version.json"));
    if(legacy.product!="Unturned.AI.Tchinese"||!Data.ValidHash(legacy.manifest_sha256)||!Data.Same(Data.Hash(Path.Combine(dataFolder,"manifest.json")),legacy.manifest_sha256))throw new Exception("漢化包識別或清單驗證失敗。");
    var assets=Data.Read<List<LegacyAsset>>(Path.Combine(dataFolder,"manifest.json"));
    m=new PackageManifest {schemaVersion=1,id="unturned.ai.tchinese",name="Unturned AI 繁中漢化",version=legacy.version,kind="localization",description="Unturned AI 繁中漢化 · 正式版 "+legacy.display_version+"。提供遊戲介面、物品與官方地圖的繁體中文翻譯。",afterInstall="請在 Steam 的 Unturned 啟動選項加入 -Lang=Tchinese，再啟動遊戲。",files=assets.Where(a=>a.group!="escalation").Select(a=>new PackageFile {source=prefix+a.target,target=a.target,sha256=a.sha256}).ToList()};
   }
   if(m.schemaVersion!=1 || m.id==null || !Regex.IsMatch(m.id,"^[a-z0-9][a-z0-9.-]{0,79}$") || string.IsNullOrWhiteSpace(m.name) || m.name.Length>120 || (m.kind!="module"&&m.kind!="localization") || m.files==null || m.files.Count==0 || m.files.Count>20000) throw new Exception("mod識別資料無效。");
   Version v; if(!Version.TryParse(m.version,out v)) throw new Exception("mod版本必須是數字版本，例如 1.0.0。");
   var targets=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   foreach(var f in m.files) {
    if(f==null) throw new Exception("檔案清單無效。");
    Paths.Safe(folder,f.source,false); Paths.Safe(folder,f.target,false); Paths.CheckTarget(f.target,m.kind);
    if(!Data.ValidHash(f.sha256)||!targets.Add(f.target)) throw new Exception("mod雜湊或檔案清單無效。");
   }
   return new Package { Folder=Path.GetFullPath(folder), Manifest=m, Origin=origin };
  }
  public static List<Package> Scan(string root,string origin,List<string> errors,int maxDepth) {
   var list=new List<Package>(); if(!Directory.Exists(root)) return list;
   ScanInto(root,origin,errors,maxDepth,list); return list;
  }
  static void ScanInto(string folder,string origin,List<string> errors,int depth,List<Package> result) {
   try {
    Paths.CheckLinks(folder);
    if(File.Exists(Path.Combine(folder,ManifestName)) || File.Exists(Path.Combine(folder,"Data","version.json")) || File.Exists(Path.Combine(folder,"version.json"))&&File.Exists(Path.Combine(folder,"manifest.json"))) { result.Add(Load(folder,origin)); return; }
    if(depth<=0) return;
    foreach(string child in Directory.GetDirectories(folder)) {
     if((File.GetAttributes(child)&FileAttributes.ReparsePoint)==0) ScanInto(child,origin,errors,depth-1,result);
    }
   } catch(Exception ex) { errors.Add(folder+"："+Problems.Explain(ex)); }
  }
  public static Package ImportZip(string zip,string cache) {
   string dest=Path.Combine(cache,"imports",Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dest);
   long total=0; var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   using(var archive=ZipFile.OpenRead(zip)) {
    if(archive.Entries.Count>25000) throw new Exception("安裝包檔案數量過多。");
    foreach(var entry in archive.Entries) {
     string normalized=entry.FullName.Replace('\\','/');string rel=normalized.TrimEnd('/'); if(rel.Length==0) continue;
     string target=Paths.Safe(dest,rel);
     if(!names.Add(rel)) throw new Exception("安裝包包含重複路徑。");
     total+=entry.Length; if(total>2147483648L) throw new Exception("解壓縮大小超過 2 GB。");
     if(normalized.EndsWith("/")) Directory.CreateDirectory(target);
     else { Directory.CreateDirectory(Path.GetDirectoryName(target)); entry.ExtractToFile(target); }
    }
   }
   var errors=new List<string>(); var found=Scan(dest,"匯入安裝包",errors,4);
   if(found.Count!=1 || errors.Count!=0) throw new Exception("ZIP 必須包含一個有效 mod，並附有 unturned-manager.json 安裝清單，或支援的漢化 Data 清單。一般 DLL、地圖或物品 ZIP 不能直接匯入。"+string.Join("\n",errors));
   return found[0];
  }
 }
 public class Engine {
  public readonly string Root, State; public readonly bool ServerMode;public Action<int,string> Progress=delegate{}; public bool TestMode; public int FailAfter=-1;
  string RegistryPath { get { return Path.Combine(State,"installed.json"); } }
  string JournalPath { get { return Path.Combine(State,"pending.json"); } }
  public Engine(string root,string stateBase,bool test=false,bool server=false) {
   Root=Path.GetFullPath(root).TrimEnd('\\'); Paths.CheckLinks(Root);
   if(!InstallTarget.IsRoot(Root,server)) throw new Exception(server?"請選擇包含 Unturned.exe 或 Unturned_Headless.exe 的伺服器根目錄。":"請選擇包含 Unturned.exe 的遊戲資料夾。");
   ServerMode=server;
   State=Path.Combine(stateBase,"Installs",Data.Key(Root)); Paths.CheckLinks(State); TestMode=test;
  }
  string Target(string rel) { return Paths.Safe(Root,rel); }
  string Original(string rel) { return Paths.Safe(State,"original/"+Data.Key(rel)+".bak"); }
  public Registry ReadRegistry() {
   if(!File.Exists(RegistryPath)) return new Registry {gameRoot=Root};
   var r=Data.Read<Registry>(RegistryPath); if(!Data.Same(r.gameRoot,Root)||r.packages==null) throw new Exception("安裝紀錄與遊戲位置不符。");
   var ids=new HashSet<string>(); var targets=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   foreach(var p in r.packages) {
    if(p==null||p.id==null||!ids.Add(p.id)||p.files==null) throw new Exception("安裝紀錄無效。");
    foreach(var f in p.files) {Target(f.target); Paths.CheckTarget(f.target,p.kind); if(!targets.Add(f.target)||!Data.ValidHash(f.hash)||f.hadOriginal&&!Data.ValidHash(f.originalHash)) throw new Exception("檔案紀錄無效。");}
   } return r;
  }
  void CheckGame() {
   if(!TestMode && (Process.GetProcessesByName("Unturned").Length>0 || Process.GetProcessesByName("Unturned_Headless").Length>0)) throw new Exception("請先關閉 Unturned 遊戲與伺服器，再安裝或解除安裝。");
  }
  void VerifyOwned(InstalledPackage p) {
   if(p==null) return;
   foreach(var f in p.files) {
    string target=Target(f.target);
    if(File.Exists(target)&&!Data.Same(Data.Hash(target),f.hash)) throw new Exception("檔案已被其他工具或遊戲更新修改，為保留內容已停止："+f.target);
    if(f.hadOriginal&&(!File.Exists(Original(f.target))||!Data.Same(Data.Hash(Original(f.target)),f.originalHash))) throw new Exception("原始備份缺失或損毀："+f.target);
   }
  }
  void Begin(IEnumerable<string> files) {
   string token=Guid.NewGuid().ToString("N"); var j=new Journal {hadRegistry=File.Exists(RegistryPath),priorRegistry=File.Exists(RegistryPath)?File.ReadAllText(RegistryPath):null,files=new List<Snapshot>()};
   int i=0;
   foreach(string rel in files.Distinct(StringComparer.OrdinalIgnoreCase)) {
    string target=Target(rel), saved="transactions/"+token+"/"+(i++)+".bak"; bool exists=File.Exists(target);
    if(exists) {string path=Paths.Safe(State,saved);Directory.CreateDirectory(Path.GetDirectoryName(path));File.Copy(target,path);}
    j.files.Add(new Snapshot {target=rel,existed=exists,hash=exists?Data.Hash(Paths.Safe(State,saved)):null,saved=saved});
   } Data.Write(JournalPath,j);
  }
  void Recover() {
   if(!File.Exists(JournalPath)) return;
   Progress(0,"正在復原上次未完成的安裝…"); var j=Data.Read<Journal>(JournalPath);
   if(j.files==null) throw new Exception("復原紀錄無效。");
   foreach(var f in j.files) {Target(f.target); string saved=Paths.Safe(State,f.saved);if(f.existed&&(!File.Exists(saved)||!Data.Same(Data.Hash(saved),f.hash))) throw new Exception("復原備份損毀，請保留管理器資料。");}
   foreach(var f in j.files) {string target=Target(f.target);if(f.existed) {Directory.CreateDirectory(Path.GetDirectoryName(target));File.Copy(Paths.Safe(State,f.saved),target,true);}else if(File.Exists(target))File.Delete(target);}
   if(j.hadRegistry) {string temp=RegistryPath+".recover";File.WriteAllText(temp,j.priorRegistry,Encoding.UTF8);if(File.Exists(RegistryPath))File.Replace(temp,RegistryPath,null);else File.Move(temp,RegistryPath);}
   else if(File.Exists(RegistryPath)) File.Delete(RegistryPath);
   File.Delete(JournalPath);
  }
  public bool NeedsRecovery { get { return File.Exists(JournalPath); } }
  public void RecoverPending() { Locked(delegate { }); }
  void Locked(Action action) {
   Directory.CreateDirectory(State);
   using(var gate=new FileStream(Path.Combine(State,"operation.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)) {CheckGame();Recover();action();}
  }
  public void Install(Package package,Dictionary<string,string> approved,bool allowDowngrade=false) {
   Locked(delegate {
    package=Packages.Load(package.Folder,package.Origin);InstallTarget.Validate(package,ServerMode);var m=package.Manifest;var r=ReadRegistry();var old=r.packages.FirstOrDefault(p=>p.id==m.id);
    if(old!=null && new Version(old.version)>new Version(m.version)&&!allowDowngrade) throw new Exception("已安裝較新版本，請確認版本切換後再安裝較舊版本。");
    if(old!=null && old.kind!=m.kind) throw new Exception("同一mod不能變更類型。");
    VerifyOwned(old);
    var oldFiles=old==null?new Dictionary<string,OwnedFile>(StringComparer.OrdinalIgnoreCase):old.files.ToDictionary(f=>f.target,StringComparer.OrdinalIgnoreCase);
    var otherFiles=new HashSet<string>(r.packages.Where(p=>p.id!=m.id).SelectMany(p=>p.files.Select(f=>f.target)),StringComparer.OrdinalIgnoreCase);
    string stage=Path.Combine(State,"staging",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
    var next=new InstalledPackage {id=m.id,name=m.name,version=m.version,kind=m.kind,files=new List<OwnedFile>()}; int count=0;
    foreach(var f in m.files) {
     if(otherFiles.Contains(f.target)) throw new Exception("其他mod正在管理相同檔案："+f.target);
     string source=Paths.Safe(package.Folder,f.source), staged=Paths.Safe(stage,Data.Key(f.target)+".bin"),target=Target(f.target);
     if(!File.Exists(source)||!Data.Same(Data.Hash(source),f.sha256)) throw new Exception("mod檔案缺失或驗證失敗："+f.source);
     Directory.CreateDirectory(Path.GetDirectoryName(staged));File.Copy(source,staged,true);
     if(!Data.Same(Data.Hash(staged),f.sha256)) throw new Exception("mod暫存驗證失敗。");
     OwnedFile prior; bool known=oldFiles.TryGetValue(f.target,out prior), exists=File.Exists(target);string hash=exists?Data.Hash(target):null;
     string consent;
     if(!known&&exists&&!Data.Same(hash,f.sha256)&&(approved==null||!approved.TryGetValue(f.target,out consent)||!Data.Same(hash,consent))) throw new Exception("已有不同內容的檔案，請先檢查並允許備份覆蓋："+f.target);
     next.files.Add(new OwnedFile {target=f.target,hash=f.sha256,hadOriginal=known?prior.hadOriginal:exists,originalHash=known?prior.originalHash:hash});
     Progress(++count*30/m.files.Count,"驗證mod "+count+" / "+m.files.Count);
    }
    Begin(next.files.Select(f=>f.target).Concat(oldFiles.Keys));
    try {
     foreach(var f in next.files.Where(f=>f.hadOriginal&&!oldFiles.ContainsKey(f.target))) {string saved=Original(f.target);Directory.CreateDirectory(Path.GetDirectoryName(saved));File.Copy(Target(f.target),saved,true);if(!Data.Same(Data.Hash(saved),f.originalHash))throw new Exception("備份驗證失敗。");}
     count=0;
     foreach(var f in m.files) {string target=Target(f.target);Directory.CreateDirectory(Path.GetDirectoryName(target));File.Copy(Paths.Safe(stage,Data.Key(f.target)+".bin"),target,true);if(!Data.Same(Data.Hash(target),f.sha256))throw new Exception("寫入驗證失敗。");if(FailAfter==count++)throw new Exception("模擬安裝中斷");Progress(30+count*60/m.files.Count,"安裝 "+count+" / "+m.files.Count);}
     var names=new HashSet<string>(next.files.Select(f=>f.target),StringComparer.OrdinalIgnoreCase);
     foreach(var stale in oldFiles.Values.Where(f=>!names.Contains(f.target))) Restore(stale);
     r.packages.RemoveAll(p=>p.id==m.id);r.packages.Add(next);Data.Write(RegistryPath,r);File.Delete(JournalPath);
    } catch {Recover();throw;}
    Progress(100,m.name+" 安裝完成。");
   });
  }
  public Dictionary<string,string> Collisions(Package package) {
   InstallTarget.Validate(package,ServerMode);
   var r=ReadRegistry();var old=r.packages.FirstOrDefault(p=>p.id==package.Manifest.id);var known=new HashSet<string>(old==null?new string[0]:old.files.Select(f=>f.target),StringComparer.OrdinalIgnoreCase);var result=new Dictionary<string,string>();
   foreach(var f in package.Manifest.files) {string target=Target(f.target);if(!known.Contains(f.target)&&File.Exists(target)&&!Data.Same(Data.Hash(target),f.sha256))result.Add(f.target,Data.Hash(target));}return result;
  }
  void Restore(OwnedFile f) {string target=Target(f.target);if(f.hadOriginal){Directory.CreateDirectory(Path.GetDirectoryName(target));File.Copy(Original(f.target),target,true);}else if(File.Exists(target))File.Delete(target);}
  public void Uninstall(string id) {
   Locked(delegate {var r=ReadRegistry();var p=r.packages.FirstOrDefault(x=>x.id==id);if(p==null)throw new Exception("沒有這個mod的安裝紀錄。");VerifyOwned(p);Begin(p.files.Select(f=>f.target));
    try {int i=0;foreach(var f in p.files){Restore(f);if(FailAfter==i++)throw new Exception("模擬解除安裝中斷");Progress(i*90/p.files.Count,"還原 "+i+" / "+p.files.Count);}r.packages.Remove(p);Data.Write(RegistryPath,r);File.Delete(JournalPath);}catch{Recover();throw;}Progress(100,p.name+" 已解除安裝，原有檔案已還原。");});
  }
 }
 public static class SteamFinder {
  public static List<string> Targets(IEnumerable<string> libraries,bool server,List<string> errors){var found=new List<string>();foreach(string lib in libraries){var paths=new List<string>();string acf=Path.Combine(lib,"steamapps",server?"appmanifest_1110390.acf":"appmanifest_304930.acf");
    try{if(File.Exists(acf)){var match=Regex.Match(File.ReadAllText(acf),"\"installdir\"\\s+\"([^\"]+)\"");if(match.Success){string common=Path.Combine(lib,"steamapps","common");paths.Add(Paths.Safe(common,match.Groups[1].Value));}}}catch(Exception ex){errors.Add(acf+"："+Problems.Explain(ex));}
    paths.Add(Path.Combine(lib,"steamapps","common",server?"Unturned Dedicated Server":"Unturned"));foreach(string path in paths)if(InstallTarget.IsRoot(path,server)&&!found.Any(p=>Data.Same(p,path)))found.Add(path);
   }return found;
  }
  public static List<string> Libraries(List<string> errors=null) {
   var list=new List<string>();string steam=null;
   try{using(var key=Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\\Valve\\Steam"))if(key!=null)steam=key.GetValue("SteamPath") as string;}catch(Exception ex){if(errors!=null)errors.Add("無法讀取 Steam 設定："+Problems.Explain(ex));}
   if(steam!=null)list.Add(steam.Replace('/','\\'));
   foreach(var drive in DriveInfo.GetDrives().Where(d=>d.IsReady)) {string path=Path.Combine(drive.Name,"SteamLibrary");if(Directory.Exists(path))list.Add(path);}
   foreach(string lib in list.ToArray()) {
    string vdf=Path.Combine(lib,"steamapps","libraryfolders.vdf");if(!File.Exists(vdf))continue;
    try{foreach(Match m in Regex.Matches(File.ReadAllText(vdf),"\"path\"\\s+\"([^\"]+)\""))list.Add(m.Groups[1].Value.Replace("\\\\","\\"));}catch(Exception ex){if(errors!=null)errors.Add(vdf+"："+Problems.Explain(ex));}
   }
   return list.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
  }
  public static string Game() {return Libraries().Select(l=>Path.Combine(l,"steamapps","common","Unturned")).FirstOrDefault(p=>File.Exists(Path.Combine(p,"Unturned.exe")))??"";}
  public static string Workshop() {return Libraries().Select(l=>Path.Combine(l,"steamapps","workshop","content","304930")).FirstOrDefault(Directory.Exists)??"";}
 }
 public static class Problems {
  public static string Explain(Exception ex) {
   string hint="請保留操作紀錄，確認檔案與路徑後重試。";
   string m=ex.Message;
   if(ex is UnauthorizedAccessException || ex is System.Security.SecurityException)hint="無法存取檔案。請確認資料夾權限，並檢查防毒軟體是否阻擋管理器。";
   else if(ex is PathTooLongException)hint="路徑太長。請將管理器或 mod 放到較短的資料夾路徑。";
   else if(ex is FileNotFoundException || ex is DirectoryNotFoundException)hint="檔案或資料夾已不存在。請確認磁碟已連接、工作坊下載完成，再重新掃描。";
   else if(ex is IOException){int code=ex.HResult&65535;hint=code==112||code==39?"磁碟空間不足。請清出遊戲與管理器資料磁碟的空間後重試。":code==32||code==33?"檔案正被占用。請關閉遊戲、其他管理器或正在使用檔案的程式後重試。":"磁碟讀寫失敗。請確認磁碟已連接、空間足夠，且檔案沒有被其他程式占用。";}
   else if(m.Contains("關閉 Unturned"))hint="請完全關閉遊戲與 Unturned 伺服器，再重試這次操作。";
   else if(m.Contains("備份")||m.Contains("復原紀錄"))hint="請保留管理器資料與備份，不要刪除。可先使用「復原未完成的操作」；若仍失敗，提供操作紀錄以確認原因。";
   else if(m.Contains("安裝紀錄")||m.Contains("檔案紀錄"))hint="請確認遊戲位置正確並保留管理器資料，不要刪除安裝紀錄或備份。";
   else if(m.Contains("其他工具")||m.Contains("其他mod"))hint="請先處理衝突的 mod 或確認其他工具修改的內容，再重試。";
   else if(m.Contains("驗證")||m.Contains("識別")||m.Contains("清單")||m.Contains("版本")||ex is ArgumentException||ex is InvalidDataException)hint="請確認 mod 下載完整且支援此管理器；可重新下載，仍有問題請把紀錄交給 mod 作者。";
   else if(m.Contains("路徑")||m.Contains("位置")||m.Contains("連結"))hint="請選擇正確的實體資料夾；mod 內的安裝位置有問題時，請聯絡 mod 作者修正。";
   return m+Environment.NewLine+"處理方式："+hint;
  }
 }
 public class ScanResult {
  public string Game,Workshop; public bool FoundGame; public List<Package> Available=new List<Package>();public List<InstalledPackage> Installed=new List<InstalledPackage>();public List<string> Issues=new List<string>();public int Roots;
 }
 public static class Scanner {
  public static ScanResult Collect(string home,string store,string game,string workshop,IEnumerable<string> folders,IEnumerable<string> libraries,bool find,Action<int,string> report,bool server=false) {
   var r=new ScanResult{Game=string.IsNullOrWhiteSpace(game)?"":Path.GetFullPath(game),Workshop=string.IsNullOrWhiteSpace(workshop)?"":Path.GetFullPath(workshop)};var libs=libraries.ToList();
   string targetName=server?"伺服器":"遊戲";
   if(find){report(0,"正在尋找 Steam "+targetName+"與工作坊資料夾…");var games=SteamFinder.Targets(libs,server,r.Issues);
    r.FoundGame=games.Count>0;
    if(r.FoundGame)r.Game=games.FirstOrDefault(p=>Data.Same(p,r.Game))??games[0];else r.Issues.Add(server?"未在 Steam 遊戲庫找到 Unturned 伺服器。SteamCMD 或自訂位置請按「瀏覽」手動選擇含伺服器執行檔的根目錄。原本填寫的位置會保留。":"未在 Steam 遊戲庫找到 Unturned。請確認已透過 Steam 安裝遊戲，或按遊戲資料夾旁的「瀏覽」選擇含 Unturned.exe 的位置。原本填寫的位置會保留。");
    if(games.Count>1)r.Issues.Add("找到多個 Unturned "+targetName+"安裝位置，目前使用："+r.Game+"。請確認這是你要管理的位置。");
    var workshops=libs.Select(l=>Path.Combine(l,"steamapps","workshop","content","304930")).Where(Directory.Exists).ToList();
    if(workshops.Count>0&&!Directory.Exists(r.Workshop))r.Workshop=workshops[0];
   }
   if(!InstallTarget.IsRoot(r.Game,server))r.Issues.Add(targetName+"位置尚未有效設定：請按「自動尋找」或「瀏覽」，選擇含 "+(server?"Unturned.exe／Unturned_Headless.exe":"Unturned.exe")+" 的根目錄。設定前仍可查看 mod，但無法安裝。");
   if(string.IsNullOrWhiteSpace(r.Workshop))r.Issues.Add("尚未找到工作坊資料夾。請確認已訂閱且下載完成，或按「瀏覽」選擇 steamapps\\workshop\\content\\304930。");
   else if(!Directory.Exists(r.Workshop))r.Issues.Add("工作坊資料夾不存在或無法存取："+r.Workshop+"。請檢查磁碟連接、資料夾權限與 Steam 下載狀態。");
   var roots=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);roots[Path.Combine(home,"Packages")]="隨附mod";roots[Path.Combine(store,"imports")]="匯入安裝包";
   if(!string.IsNullOrWhiteSpace(r.Workshop))roots[r.Workshop]=r.Workshop.Replace('\\','/').TrimEnd('/').EndsWith("/steamapps/workshop/content/304930",StringComparison.OrdinalIgnoreCase)?"工作坊":"本機mod來源";
   foreach(string lib in libs)roots[Path.Combine(lib,"steamapps","workshop","content","304930")]="工作坊";
   foreach(var pair in roots){if(!Directory.Exists(pair.Key))continue;report(0,"正在掃描："+pair.Key);r.Roots++;r.Available.AddRange(Packages.Scan(pair.Key,pair.Value,r.Issues,5));}
   foreach(string folder in folders)try{r.Available.Add(Packages.Load(folder,"本機mod"));}catch(Exception ex){r.Issues.Add(folder+"："+Problems.Explain(ex));}
   if(InstallTarget.IsRoot(r.Game,server))try{var e=new Engine(r.Game,store,false,server);r.Installed=e.ReadRegistry().packages;if(e.NeedsRecovery)r.Issues.Add("上次操作中斷，請按「復原未完成的操作」後再管理 mod。");}catch(Exception ex){r.Issues.Add("讀取安裝紀錄失敗："+Problems.Explain(ex));}
   return r;
  }
 }
}
