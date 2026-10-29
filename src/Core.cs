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
using System.Threading;
using Microsoft.Win32;

namespace UnturnedModManager {
 public class PackageFile { public string source; public string target; public string sha256; public string source_sha256;public string source_file;public List<SourceDependency> source_dependencies; }
 public class PackageManifest {
  public string scopeId;public string packageIdentity;public int schemaVersion; public string id; public string name; public string version; public string kind;
  public string displayVersion; public string description; public string afterInstall; public string afterInstallServer; public List<PackageFile> files;
 }
 public class Package { public string ServerIssue;public ModMetadata Metadata;public string MetadataIssue;public string Folder; public PackageManifest Manifest; public string Origin;public DateTime ModifiedUtc,ImportedUtc;public int ScanOrder; }
 public class OwnedFile {public string backupReference; public string target; public string hash; public bool hadOriginal; public string originalHash; }
 public class InstalledPackage {public bool retained; public string id; public string name; public string version; public string kind; public string scopeId;public List<OwnedFile> files; }
 public class Registry { public string ownerGameRoot;public string scopeId;public string gameRoot; public List<InstalledPackage> packages = new List<InstalledPackage>(); }
 public class Snapshot {public string expectedHash;public bool expectedAbsent; public string target; public bool existed; public string hash; public string saved; }
 public class Journal {public int schemaVersion;public string gameRoot,ownerGameRoot,scopeId; public string priorRegistry; public bool hadRegistry; public List<Snapshot> files; }
 public class LegacyVersion { public string product; public string version; public string manifest_sha256; public string display_version;public string package_label;public string package_id; }
 public class LegacyAsset { public string target; public string sha256; public string group;public string source_sha256;public string source_file;public List<SourceDependency> source_dependencies; }
 public class Settings {public string theme;public bool navigationCollapsed;public bool detailHidden;public int detailWidth;public int uiSchema; public List<string> categories;public Dictionary<string,List<string>> modCategories;public List<string> expandedGroups;public string language; public string gameRoot; public string serverRoot; public bool serverMode; public string workshopRoot; public List<string> packageFolders; public Dictionary<string,string> versionChoices; }
 public class ModuleDescription {public List<ModuleRole> Assemblies;}
 public class ModuleRole {public string Path;public object Role;}
 public static class InstallTarget {
  public static bool IsRoot(string root,bool server){return !string.IsNullOrWhiteSpace(root)&&(File.Exists(Path.Combine(root,"Unturned.exe"))||server&&File.Exists(Path.Combine(root,"Unturned_Headless.exe")));}
  public static string Instructions(Package p,bool server){return server?(string.IsNullOrWhiteSpace(p.Manifest.afterInstallServer)?L.Get("Text134"):p.Manifest.afterInstallServer):(p.Manifest.scopeId==null?"":L.Get("ScopedLocalizationGuide")+Environment.NewLine)+(p.Manifest.packageIdentity!=null?L.Get("Text147"):p.Manifest.afterInstall);}
  public static bool SupportsServer(Package p,out string reason){
   reason="";if(p.Manifest.kind!="module"){reason=L.Get("Text135");return false;}
   try{foreach(var file in p.Manifest.files.Where(f=>f.target.EndsWith(".module",StringComparison.OrdinalIgnoreCase))){var config=Data.Read<ModuleDescription>(Paths.Safe(p.Folder,file.source));if(config==null||config.Assemblies==null)continue;
     foreach(var assembly in config.Assemblies){if(assembly==null)continue;string role=Convert.ToString(assembly.Role);if(role!="Server"&&role!="Both_Optional"&&role!="Both_Required"&&role!="2"&&role!="3"&&role!="4")continue;
      if(string.IsNullOrWhiteSpace(assembly.Path))continue;string dll=file.target.Substring(0,file.target.LastIndexOf('/')+1)+assembly.Path.Replace('\\','/').TrimStart('/');Paths.Safe(p.Folder,dll,false);
      if(p.Manifest.files.Any(f=>Data.Same(f.target,dll)))return true;
     }
    }reason=L.Get("Text136");
   }catch(Exception ex){reason=L.Get("Text137")+Problems.Explain(ex);}return false;
  }
  public static void Validate(Package p,bool server){string reason;if(server&&!SupportsServer(p,out reason))throw new Exception(reason);}
 }
 public static class Data {
  [ThreadStatic] static JavaScriptSerializer json;
  static JavaScriptSerializer Json{get{return json??(json=new JavaScriptSerializer{MaxJsonLength=33554432});}}
  public static T Read<T>(string file) { string text=File.ReadAllText(file,Encoding.UTF8);return Json.Deserialize<T>(text); }
  public static T Parse<T>(string value) {return Json.Deserialize<T>(value); }
  public static void Write(string file, object value) {
   Directory.CreateDirectory(Path.GetDirectoryName(file)); string temp = file + ".tmp";
   string serialized=Json.Serialize(value);File.WriteAllText(temp,serialized,new UTF8Encoding(false));
   if(File.Exists(file)) File.Replace(temp,file,null); else File.Move(temp,file);
  }
  public static string Hash(string file) { using(var sha=SHA256.Create()) using(var stream=File.OpenRead(file)) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant(); }
  public static bool Same(string a,string b) { return string.Equals(a,b,StringComparison.OrdinalIgnoreCase); }
  public static bool ValidHash(string hash) { return hash != null && Regex.IsMatch(hash,"^[a-fA-F0-9]{64}$"); }
  public static string Key(string value) { using(var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value.ToLowerInvariant()))).Replace("-", "").Substring(0,24); }
 }
 public static class Paths {
  public static string Safe(string root,string relative,bool checkLinks=true) {
   if(string.IsNullOrWhiteSpace(relative) || relative.Contains('\\') || relative.Contains(':') || relative.StartsWith("/")) throw new Exception(L.Get("Text138")+relative);
   foreach(string part in relative.Split('/')) {
    if(part==""||part=="."||part==".."||part.EndsWith(".")||part.EndsWith(" ")||part.IndexOfAny(Path.GetInvalidFileNameChars())>=0 || Regex.IsMatch(part,"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])($|\\.)",RegexOptions.IgnoreCase)) throw new Exception(L.Get("Text139")+relative);
   }
   root=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
   string result=Path.GetFullPath(Path.Combine(root,relative.Replace('/',Path.DirectorySeparatorChar)));
   if(!result.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) throw new Exception(L.Get("Text140"));
   if(checkLinks)CheckLinks(result); return result;
  }
  public static void CheckLinks(string path) {
   for(string p=Path.GetFullPath(path);p!=null;p=Path.GetDirectoryName(p))
    try{if((File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)throw new Exception(L.Get("Text141")+p);}catch(FileNotFoundException){}catch(DirectoryNotFoundException){}
  }
  public static void MoveDirectoryChecked(string source,string target){source=Path.GetFullPath(source);target=Path.GetFullPath(target);for(int attempt=0;;attempt++){try{CheckLinks(source);CheckLinks(target);Directory.Move(source,target);return;}catch(IOException ex){int code=ex.HResult&65535;if(attempt>=3||(code!=5&&code!=32&&code!=33))throw;}catch(UnauthorizedAccessException){if(attempt>=3)throw;}Thread.Sleep(80*(attempt+1));}}
  public static void CheckTarget(string target,string kind,string scope=null) {
   var parts=target.Split('/');
   bool module = parts.Length>=3 && parts[0]=="Modules" && parts[1]!="Framework" && parts[1]!="Unturned" && parts[1]!="Rocket.Unturned";
   bool locale = target.StartsWith("Localization/Tchinese/",StringComparison.Ordinal)&&target.EndsWith(".dat",StringComparison.OrdinalIgnoreCase)
    || (target.StartsWith("Maps/",StringComparison.Ordinal)||target.StartsWith("Bundles/",StringComparison.Ordinal))&&target.EndsWith("/Tchinese.dat",StringComparison.OrdinalIgnoreCase);
   if(scope!=null){if(kind!="localization")throw new Exception(L.Get("ScopeInvalid"));if(!Regex.IsMatch(scope,@"^\d{1,20}$"))throw new Exception(L.Get("ScopeInvalid"));locale=target.EndsWith("/Tchinese.dat",StringComparison.OrdinalIgnoreCase);}
   if(!(kind=="module"?module:kind=="localization"&&locale)) throw new Exception(L.Get("Text142")+target);
  }
 }
 public static class Packages {
  public const string ManifestName="unturned-manager.json";
  public static Package Load(string folder,string origin){return LoadAll(folder,origin).First();}
  public static string InstallSignature(Package p){return new JavaScriptSerializer{MaxJsonLength=32*1024*1024}.Serialize(new {p.Manifest.id,p.Manifest.version,p.Manifest.kind,p.Manifest.scopeId,files=p.Manifest.files.OrderBy(f=>f.target,StringComparer.Ordinal).Select(f=>new{f.source,f.target,f.sha256,f.source_sha256,f.source_file,dependencies=f.source_dependencies==null?null:f.source_dependencies.OrderBy(d=>d.relative,StringComparer.Ordinal).ToArray()}).ToArray()});}
  public static Package Reload(Package package){return LoadAll(package.Folder,package.Origin).Single(p=>p.Manifest.id==package.Manifest.id);}
  public static List<Package> LoadAll(string folder,string origin){
   Paths.CheckLinks(folder);var result=new List<Package>();string metadata=Path.Combine(folder,ManifestName);
   if(File.Exists(metadata)){var manifest=Data.Read<PackageManifest>(metadata);ValidateManifest(manifest,folder);result.Add(Wrap(folder,origin,manifest,metadata));return result;}
   string dataFolder=Path.Combine(folder,"Data"),prefix="Data/";if(!File.Exists(Path.Combine(dataFolder,"version.json"))){dataFolder=folder;prefix="";}
   var legacy=Data.Read<LegacyVersion>(Path.Combine(dataFolder,"version.json"));metadata=Path.Combine(dataFolder,"manifest.json");
   if(legacy==null||legacy.product!="Unturned.AI.Tchinese"||!Data.ValidHash(legacy.manifest_sha256)||!Data.Same(Data.Hash(metadata),legacy.manifest_sha256))throw new Exception(L.Get("Text143"));
   var assets=Data.Read<List<LegacyAsset>>(metadata);if(assets==null||assets.Any(f=>f==null||string.IsNullOrEmpty(f.target)))throw new Exception(L.Get("Text150"));
   string identity=(string.IsNullOrWhiteSpace(legacy.package_id)||legacy.package_id=="official"||legacy.package_id=="base"||legacy.package_id=="core")?"unturned.ai.tchinese":"unturned.ai.tchinese."+legacy.package_id;
   var profiles=File.Exists(Path.Combine(dataFolder,"profiles.json"))?Data.Read<List<PackageScope>>(Path.Combine(dataFolder,"profiles.json")):new List<PackageScope>();
   foreach(var group in assets.Where(f=>f.group!="escalation").GroupBy(f=>f.target.StartsWith("Workshop/",StringComparison.Ordinal)?f.target.Split('/')[1]:"base")){
    string scope=group.Key=="base"?null:group.Key;if(scope!=null&&!Regex.IsMatch(scope,@"^\d{1,20}$"))throw new Exception(L.Get("ScopeInvalid"));string targetPrefix=scope==null?"":"Workshop/"+scope+"/";var profile=profiles==null?null:profiles.FirstOrDefault(p=>p.id==group.Key);
    var manifest=new PackageManifest{schemaVersion=1,id=identity+(scope==null?"":".workshop."+scope),packageIdentity=identity,scopeId=scope,name=string.IsNullOrWhiteSpace(legacy.package_label)?L.Get("Text144"):legacy.package_label,version=legacy.version,displayVersion=legacy.display_version,kind="localization",description=profile==null?"":profile.label,afterInstall=L.Get("Text147"),files=group.Select(f=>new PackageFile{source=prefix+f.target,target=scope==null?f.target:f.target.Substring(targetPrefix.Length),sha256=f.sha256,source_sha256=f.source_sha256,source_file=f.source_file,source_dependencies=f.source_dependencies}).ToList()};
    if(scope!=null)manifest.name+=" · Workshop "+scope;ValidateManifest(manifest,folder);result.Add(Wrap(folder,origin,manifest,metadata));
   }if(result.Count==0)throw new Exception(L.Get("Text150"));return result;
  }
  static Package Wrap(string folder,string origin,PackageManifest manifest,string metadata){return new Package{Folder=Path.GetFullPath(folder),Origin=origin,Manifest=manifest,Metadata=ModNames.Read(folder,manifest),MetadataIssue=ModNames.LastIssue,ModifiedUtc=File.GetLastWriteTimeUtc(metadata),ImportedUtc=folder.IndexOf(Path.DirectorySeparatorChar+"imports"+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)>=0?Directory.GetCreationTimeUtc(folder):DateTime.MinValue};}
  static void ValidateManifest(PackageManifest m,string folder){
   if(m==null||m.schemaVersion!=1||m.id==null||!Regex.IsMatch(m.id,"^[a-z0-9][a-z0-9.-]{0,159}$")||string.IsNullOrWhiteSpace(m.name)||m.name.Length>200||(m.kind!="module"&&m.kind!="localization")||m.files==null||m.files.Count==0||m.files.Count>20000)throw new Exception(L.Get("Text148"));
   if(!VersionOrder.Valid(m.version))throw new Exception(L.Get("Text149"));if(m.scopeId!=null&&m.kind!="localization")throw new Exception(L.Get("ScopeInvalid"));var targets=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   foreach(var f in m.files){if(f==null)throw new Exception(L.Get("Text150"));Paths.Safe(folder,f.source,false);Paths.Safe(folder,f.target,false);Paths.CheckTarget(f.target,m.kind,m.scopeId);if(!Data.ValidHash(f.sha256)||!targets.Add(f.target))throw new Exception(L.Get("Text151"));}
  }
  public static List<Package> Scan(string root,string origin,List<string> errors,int maxDepth) {
   ScanSession.Check();var list=new List<Package>(); if(!Directory.Exists(root)) return list;
   ScanInto(root,origin,errors,maxDepth,list); return list;
  }
  static void ScanInto(string folder,string origin,List<string> errors,int depth,List<Package> result) {
   try {
    ScanSession.Check();if(ScanSession.Current!=null&&!ScanSession.Current.Visited.Add(Path.GetFullPath(folder)))return;Paths.CheckLinks(folder);
    if(File.Exists(Path.Combine(folder,ManifestName)) || File.Exists(Path.Combine(folder,"Data","version.json")) || File.Exists(Path.Combine(folder,"version.json"))&&File.Exists(Path.Combine(folder,"manifest.json"))) { result.AddRange(ScanCache.Read(folder,origin)); return; }
    if(depth<=0) return;
    foreach(string child in Directory.GetDirectories(folder).OrderBy(p=>p,StringComparer.OrdinalIgnoreCase)) {
     if((File.GetAttributes(child)&FileAttributes.ReparsePoint)==0) ScanInto(child,origin,errors,depth-1,result);
    }
   } catch(OperationCanceledException){throw;}catch(Exception ex) { errors.Add(folder+"："+Problems.Explain(ex));if(ScanSession.Current!=null)ScanSession.Current.Problems.Add(new SourceProblem{Folder=folder,SummaryKey="UnreadablePackage",RemedyKey="ReloadSourceHelp",Details=Problems.Explain(ex)}); }
  }
  public static Package ImportZip(string zip,string cache,Action<int,string> report=null,CancellationToken cancellation=default(CancellationToken)) {
   cancellation.ThrowIfCancellationRequested();string token=Guid.NewGuid().ToString("N"),dest=Paths.Safe(cache,"ImportStaging/"+token),published=Paths.Safe(cache,"imports/"+token);Paths.CheckLinks(dest);Directory.CreateDirectory(dest);var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);long total=0,done=0;
   using(var archive=ZipFile.OpenRead(zip)){if(archive.Entries.Count>25000)throw new Exception(L.Get("Text152"));long expected=archive.Entries.Sum(e=>e.Length);if(expected>2147483648L)throw new Exception(L.Get("Text154"));var buffer=new byte[65536];foreach(var entry in archive.Entries){cancellation.ThrowIfCancellationRequested();string normalized=entry.FullName.Replace('\\','/'),rel=normalized.TrimEnd('/');if(rel.Length==0)continue;string target=Paths.Safe(dest,rel);if(!names.Add(rel))throw new Exception(L.Get("Text153"));total+=entry.Length;if(total>2147483648L)throw new Exception(L.Get("Text154"));if(normalized.EndsWith("/")){Directory.CreateDirectory(target);continue;}Directory.CreateDirectory(Path.GetDirectoryName(target));using(var source=entry.Open())using(var output=new FileStream(target,FileMode.CreateNew,FileAccess.Write,FileShare.None)){int count;while((count=source.Read(buffer,0,buffer.Length))>0){cancellation.ThrowIfCancellationRequested();output.Write(buffer,0,count);done+=count;if(done>2147483648L)throw new Exception(L.Get("Text154"));if(report!=null)report(expected==0?0:(int)Math.Min(94,done*94/expected),L.Get("ExtractingZip"));}}File.SetLastWriteTimeUtc(target,entry.LastWriteTime.UtcDateTime);}}
   cancellation.ThrowIfCancellationRequested();var errors=new List<string>();var found=Scan(dest,L.Get("Text155"),errors,4);if(found.Count==0||found.Select(p=>p.Folder).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=1||errors.Count!=0)throw new Exception(L.Get("Text156")+string.Join("\n",errors));string relative=found[0].Folder.Substring(Path.GetFullPath(dest).Length).TrimStart(Path.DirectorySeparatorChar);Paths.CheckLinks(published);Directory.CreateDirectory(Path.GetDirectoryName(published));cancellation.ThrowIfCancellationRequested();Paths.MoveDirectoryChecked(dest,published);var result=Load(relative.Length==0?published:Paths.Safe(published,relative.Replace('\\','/')),L.Get("Text155"));if(report!=null)report(100,L.Get("ImportComplete"));return result;
  }

 }
 public partial class Engine {
  public readonly string Root, State,GameRoot,ScopeId; public readonly bool ServerMode;public Action<int,string> Progress=delegate{}; public bool TestMode; public int FailAfter=-1;public int LastWrittenFiles,LastUnchangedFiles;
  string RegistryPath { get { return Path.Combine(State,"installed.json"); } }
  string JournalPath { get { return Path.Combine(State,"pending.json"); } }
  public Engine(string root,string stateBase,bool test=false,bool server=false,string scope=null,IEnumerable<string> workshopRoots=null) {
   GameRoot=Path.GetFullPath(root).TrimEnd('\\');Paths.CheckLinks(GameRoot);
   if(!InstallTarget.IsRoot(GameRoot,server)) throw new Exception(server?L.Get("Text157"):L.Get("Text158"));
   if(scope!=null&&server)throw new Exception(L.Get("Text135"));ScopeId=scope;Root=scope==null?GameRoot:WorkshopTargets.Resolve(GameRoot,scope,workshopRoots);Paths.CheckLinks(Root);ServerMode=server;
   State=Path.Combine(stateBase,"Installs",Data.Key(Root)); Paths.CheckLinks(State); TestMode=test;
  }
  string Target(string rel) { Paths.CheckTarget(rel,ScopeId!=null?"localization":rel.StartsWith("Modules/",StringComparison.Ordinal)?"module":"localization",ScopeId);return Paths.Safe(Root,rel); }
  string Original(string rel) { return Paths.Safe(State,"original/"+Data.Key(rel)+".bak"); }
  public Registry ReadRegistry() {
   if(!File.Exists(RegistryPath)) return new Registry {gameRoot=Root,ownerGameRoot=GameRoot,scopeId=ScopeId};
   var r=Data.Read<Registry>(RegistryPath); if(!Data.Same(r.gameRoot,Root)||r.scopeId!=ScopeId||r.packages==null) throw new Exception(L.Get("Text159"));
   var ids=new HashSet<string>(); var targets=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   foreach(var p in r.packages) {
    if(p==null||p.id==null||!ids.Add(p.id)||p.files==null) throw new Exception(L.Get("Text160"));
    foreach(var f in p.files) {Original(f);Target(f.target); Paths.CheckTarget(f.target,p.kind,ScopeId); if(!targets.Add(f.target)||!Data.ValidHash(f.hash)||f.hadOriginal&&!Data.ValidHash(f.originalHash)) throw new Exception(L.Get("Text161"));}
   } return r;
  }
  void CheckGame() {
   if(!TestMode && (Process.GetProcessesByName("Unturned").Length>0 || Process.GetProcessesByName("Unturned_Headless").Length>0)) throw new Exception(L.Get("Text162"));
  }
  void VerifyOwned(InstalledPackage p) {
   if(p==null) return;
   foreach(var f in p.files) {
    string target=Target(f.target);
    if(File.Exists(target)&&!Data.Same(Data.Hash(target),f.hash)) throw new InstalledContentChangedException(f.target);
    if(f.hadOriginal&&(!File.Exists(Original(f))||!Data.Same(Data.Hash(Original(f)),f.originalHash))) throw new Exception(L.Get("Text164")+f.target);
   }
  }
  void Begin(IEnumerable<string> files,Dictionary<string,string> expected) {
   string token=Guid.NewGuid().ToString("N"); var j=new Journal {schemaVersion=2,gameRoot=Root,ownerGameRoot=GameRoot,scopeId=ScopeId,hadRegistry=File.Exists(RegistryPath),priorRegistry=File.Exists(RegistryPath)?File.ReadAllText(RegistryPath):null,files=new List<Snapshot>()};
   int i=0;
   foreach(string rel in files.Distinct(StringComparer.OrdinalIgnoreCase)) {
    string target=Target(rel), saved="transactions/"+token+"/"+(i++)+".bak"; bool exists=File.Exists(target);
    if(exists) {string path=Paths.Safe(State,saved);Directory.CreateDirectory(Path.GetDirectoryName(path));File.Copy(target,path);}
    j.files.Add(new Snapshot {target=rel,expectedHash=expected[rel],expectedAbsent=expected[rel]==null,existed=exists,hash=exists?Data.Hash(Paths.Safe(State,saved)):null,saved=saved});
   } Data.Write(JournalPath,j);
  }
  void Recover() {
   if(!File.Exists(JournalPath)) return;
   Progress(0,L.Get("Text165")); var j=Data.Read<Journal>(JournalPath);
   if(j.gameRoot!=null&&(!Data.Same(j.gameRoot,Root)||j.scopeId!=ScopeId))throw new Exception(L.Get("Text166"));if(j.files==null) throw new Exception(L.Get("Text166"));
   foreach(var f in j.files) {Target(f.target); string saved=Paths.Safe(State,f.saved);if(f.existed&&(!File.Exists(saved)||!Data.Same(Data.Hash(saved),f.hash))) throw new Exception(L.Get("Text167"));}
   if(j.schemaVersion>=2)foreach(var f in j.files){string path=Target(f.target);bool exists=File.Exists(path);string current=exists?Data.Hash(path):null;bool original=f.existed?exists&&Data.Same(current,f.hash):!exists;bool planned=f.expectedAbsent?!exists:exists&&Data.ValidHash(f.expectedHash)&&Data.Same(current,f.expectedHash);if(!original&&!planned)throw new Exception(L.Get("RecoveryModified")+Environment.NewLine+f.target);}
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
   LastWrittenFiles=LastUnchangedFiles=0;Locked(delegate {
    var verified=Packages.Reload(package);if(!string.Equals(Packages.InstallSignature(package),Packages.InstallSignature(verified),StringComparison.Ordinal))throw new Exception(L.Get("SourceChanged"));package=verified;if(package.Manifest.scopeId!=ScopeId)throw new Exception(L.Get("ScopeInvalid"));WorkshopTargets.VerifySources(package,Root);InstallTarget.Validate(package,ServerMode);var m=package.Manifest;var r=ReadRegistry();var old=r.packages.FirstOrDefault(p=>p.id==m.id);
    if(old!=null && (VersionOrder.Newer(old.version,m.version)||VersionOrder.Compare(old.version,m.version)==null)&&!allowDowngrade) throw new Exception(L.Get("Text168"));
    if(old!=null && old.kind!=m.kind) throw new Exception(L.Get("Text169"));
    VerifyOwned(old);
    var oldFiles=old==null?new Dictionary<string,OwnedFile>(StringComparer.OrdinalIgnoreCase):old.files.ToDictionary(f=>f.target,StringComparer.OrdinalIgnoreCase);
    var otherFiles=new HashSet<string>(r.packages.Where(p=>p.id!=m.id).SelectMany(p=>p.files.Select(f=>f.target)),StringComparer.OrdinalIgnoreCase);
    string stage=Path.Combine(State,"staging",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
    var changed=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var next=new InstalledPackage {id=m.id,name=m.name,version=m.version,kind=m.kind,scopeId=ScopeId,files=new List<OwnedFile>()}; int count=0;
    foreach(var f in m.files) {
     if(otherFiles.Contains(f.target)) throw new Exception(L.Get("Text170")+f.target);
     string source=Paths.Safe(package.Folder,f.source), staged=Paths.Safe(stage,Data.Key(f.target)+".bin"),target=Target(f.target);
     if(!File.Exists(source)||!Data.Same(Data.Hash(source),f.sha256)) throw new Exception(L.Get("Text171")+f.source);
     OwnedFile prior; bool known=oldFiles.TryGetValue(f.target,out prior), exists=File.Exists(target);string hash=exists?Data.Hash(target):null;
     string consent;
     if(!known&&exists&&!Data.Same(hash,f.sha256)&&(approved==null||!approved.TryGetValue(f.target,out consent)||!Data.Same(hash,consent))) throw new Exception(L.Get("Text173")+f.target);
     if(!exists||!Data.Same(hash,f.sha256)){Directory.CreateDirectory(Path.GetDirectoryName(staged));File.Copy(source,staged,true);if(!Data.Same(Data.Hash(staged),f.sha256))throw new Exception(L.Get("Text172"));changed.Add(f.target);}else LastUnchangedFiles++;
     next.files.Add(new OwnedFile {target=f.target,hash=f.sha256,hadOriginal=known?prior.hadOriginal:exists,originalHash=known?prior.originalHash:hash,backupReference=known?prior.backupReference:null});
     Progress(++count*30/m.files.Count,L.Get("Text174")+count+" / "+m.files.Count);
    }
    var nextTargets=new HashSet<string>(next.files.Select(f=>f.target),StringComparer.OrdinalIgnoreCase);var expected=next.files.ToDictionary(f=>f.target,f=>f.hash,StringComparer.OrdinalIgnoreCase);foreach(var f in oldFiles.Values)if(!expected.ContainsKey(f.target))expected[f.target]=f.hadOriginal?f.originalHash:null;Begin(changed.Concat(oldFiles.Keys.Where(path=>!nextTargets.Contains(path))),expected);
    try {
     foreach(var f in next.files.Where(f=>f.hadOriginal&&!oldFiles.ContainsKey(f.target))) {string saved=Original(f);Directory.CreateDirectory(Path.GetDirectoryName(saved));File.Copy(Target(f.target),saved,true);if(!Data.Same(Data.Hash(saved),f.originalHash))throw new Exception(L.Get("Text175"));}
     count=0;
     foreach(var f in m.files) {string target=Target(f.target);if(changed.Contains(f.target)){Directory.CreateDirectory(Path.GetDirectoryName(target));File.Copy(Paths.Safe(stage,Data.Key(f.target)+".bin"),target,true);LastWrittenFiles++;if(!Data.Same(Data.Hash(target),f.sha256))throw new Exception(L.Get("Text176"));}if(FailAfter==count++)throw new Exception(L.Get("Text177"));Progress(30+count*60/m.files.Count,L.Get("Text178")+count+" / "+m.files.Count);}
     var names=new HashSet<string>(next.files.Select(f=>f.target),StringComparer.OrdinalIgnoreCase);
     foreach(var stale in oldFiles.Values.Where(f=>!names.Contains(f.target))) Restore(stale);
     foreach(var f in m.files.Where(f=>!changed.Contains(f.target))){string target=Target(f.target);if(!File.Exists(target)||!Data.Same(Data.Hash(target),f.sha256))throw new InstalledContentChangedException(f.target);}
     r.packages.RemoveAll(p=>p.id==m.id);r.packages.Add(next);Data.Write(RegistryPath,r);File.Delete(JournalPath);
    } catch {Recover();throw;}
    Progress(100,ModNames.Display(package)+L.Get("Text179")+" · "+string.Format(L.Get("FileOutcome"),LastWrittenFiles,LastUnchangedFiles));
   });
  }
  public Dictionary<string,string> Preflight(Package package){
   if(package.Manifest.scopeId!=ScopeId)throw new Exception(L.Get("ScopeInvalid"));CheckGame();Paths.CheckLinks(Root);if(NeedsRecovery)throw new Exception(L.Get("PendingBlocksInstall"));WorkshopTargets.VerifySources(package,Root);InstallTarget.Validate(package,ServerMode);var registry=ReadRegistry();var old=registry.packages.FirstOrDefault(p=>p.id==package.Manifest.id);if(old!=null&&old.kind!=package.Manifest.kind)throw new Exception(L.Get("Text169"));VerifyOwned(old);var owners=new HashSet<string>(registry.packages.Where(p=>p.id!=package.Manifest.id).SelectMany(p=>p.files.Select(f=>f.target)),StringComparer.OrdinalIgnoreCase);var directories=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   foreach(var file in package.Manifest.files){if(owners.Contains(file.target))throw new Exception(L.Get("Text170")+file.target);string source=Paths.Safe(package.Folder,file.source);if(!File.Exists(source)||!Data.Same(Data.Hash(source),file.sha256))throw new Exception(L.Get("Text171")+file.source);string target=Target(file.target);if(File.Exists(target)&&(File.GetAttributes(target)&FileAttributes.ReadOnly)!=0)throw new UnauthorizedAccessException(target);string directory=Path.GetDirectoryName(target);while(!Directory.Exists(directory))directory=Path.GetDirectoryName(directory);directories.Add(directory);}
   foreach(string directory in directories){Paths.CheckLinks(directory);string probe=Path.Combine(directory,".umm-write-check-"+Guid.NewGuid().ToString("N"));try{using(var stream=new FileStream(probe,FileMode.CreateNew,FileAccess.Write,FileShare.None,1,FileOptions.DeleteOnClose))stream.WriteByte(0);}finally{if(File.Exists(probe))File.Delete(probe);}}return ReadCollisions(package,registry);
  }
  public Dictionary<string,string> Collisions(Package package) {
   if(package.Manifest.scopeId!=ScopeId)throw new Exception(L.Get("ScopeInvalid"));WorkshopTargets.VerifySources(package,Root);InstallTarget.Validate(package,ServerMode);
   return ReadCollisions(package,ReadRegistry());
  }
  Dictionary<string,string> ReadCollisions(Package package,Registry r){var old=r.packages.FirstOrDefault(p=>p.id==package.Manifest.id);var known=new HashSet<string>(old==null?new string[0]:old.files.Select(f=>f.target),StringComparer.OrdinalIgnoreCase);var result=new Dictionary<string,string>();
   foreach(var f in package.Manifest.files) {string target=Target(f.target);if(!known.Contains(f.target)&&File.Exists(target)&&!Data.Same(Data.Hash(target),f.sha256))result.Add(f.target,Data.Hash(target));}return result;
  }
  void Restore(OwnedFile f) {string target=Target(f.target);if(f.hadOriginal){Directory.CreateDirectory(Path.GetDirectoryName(target));File.Copy(Original(f),target,true);}else if(File.Exists(target))File.Delete(target);}
  public void Uninstall(string id) {
   Locked(delegate {var r=ReadRegistry();var p=r.packages.FirstOrDefault(x=>x.id==id);if(p==null)throw new Exception(L.Get("Text180"));VerifyOwned(p);Begin(p.files.Select(f=>f.target),p.files.ToDictionary(f=>f.target,f=>f.hadOriginal?f.originalHash:null,StringComparer.OrdinalIgnoreCase));
    try {int i=0;foreach(var f in p.files){Restore(f);if(FailAfter==i++)throw new Exception(L.Get("Text181"));Progress(i*90/p.files.Count,L.Get("Text182")+i+" / "+p.files.Count);}r.packages.Remove(p);Data.Write(RegistryPath,r);File.Delete(JournalPath);}catch{Recover();throw;}Progress(100,p.name+L.Get("Text183"));});
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
   try{using(var key=Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\\Valve\\Steam"))if(key!=null)steam=key.GetValue("SteamPath") as string;}catch(Exception ex){if(errors!=null)errors.Add(L.Get("Text184")+Problems.Explain(ex));}
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
   string hint=L.Get("Text185");
   string m=L.ToTraditional(ex.Message);
   if(ex is InstalledContentChangedException)hint=L.Get("ReconcileHint");
   else if(ex is UnauthorizedAccessException || ex is System.Security.SecurityException)hint=L.Get("Text186");
   else if(ex is PathTooLongException)hint=L.Get("Text187");
   else if(ex is FileNotFoundException || ex is DirectoryNotFoundException)hint=L.Get("Text188");
   else if(ex is IOException){int code=ex.HResult&65535;hint=code==112||code==39?L.Get("Text189"):code==32||code==33?L.Get("Text190"):L.Get("Text191");}
   else if(m.Contains("關閉 Unturned"))hint=L.Get("Text193");
   else if(m.Contains("備份")||m.Contains("復原紀錄"))hint=L.Get("Text196");
   else if(m.Contains("安裝紀錄")||m.Contains("檔案紀錄"))hint=L.Get("Text198");
   else if(m.Contains("其他工具")||m.Contains("其他mod"))hint=L.Get("Text201");
   else if(m.Contains("驗證")||m.Contains("識別")||m.Contains("清單")||m.Contains("版本")||ex is ArgumentException||ex is InvalidDataException)hint=L.Get("Text205");
   else if(m.Contains("路徑")||m.Contains("位置")||m.Contains("連結"))hint=L.Get("Text209");
   return ex.Message+Environment.NewLine+L.Get("Text210")+hint;
  }
 }
 public class ScanResult {
  public List<SourceProblem> SourceProblems=new List<SourceProblem>();public int ParsedPackages,CacheHits,VisitedDirectories;public long ElapsedMilliseconds;public List<string> PendingScopes=new List<string>();public List<string> WorkshopRoots=new List<string>();public string Game,Workshop; public bool FoundGame; public List<Package> Available=new List<Package>();public List<InstalledPackage> Installed=new List<InstalledPackage>();public List<string> Issues=new List<string>();public int Roots;
 }
 public static class Scanner {
  public static ScanResult Collect(string home,string store,string game,string workshop,IEnumerable<string> folders,IEnumerable<string> libraries,bool find,Action<int,string> report,bool server=false,CancellationToken cancellation=default(CancellationToken)) {
   using(var session=new ScanSession(cancellation)){var timer=Stopwatch.StartNew();
   var r=new ScanResult{Game=string.IsNullOrWhiteSpace(game)?"":Path.GetFullPath(game),Workshop=string.IsNullOrWhiteSpace(workshop)?"":Path.GetFullPath(workshop)};var libs=libraries.ToList();
   string targetName=server?L.Get("Text060"):L.Get("Text061");
   if(find){report(0,L.Get("Text211")+targetName+L.Get("Text212"));var games=SteamFinder.Targets(libs,server,r.Issues);
    r.FoundGame=games.Count>0;
    if(r.FoundGame)r.Game=games.FirstOrDefault(p=>Data.Same(p,r.Game))??games[0];else r.Issues.Add(server?L.Get("Text213"):L.Get("Text214"));
    if(games.Count>1)r.Issues.Add(L.Get("Text215")+targetName+L.Get("Text216")+r.Game+L.Get("Text217"));
    var workshops=libs.Select(l=>Path.Combine(l,"steamapps","workshop","content","304930")).Where(Directory.Exists).ToList();
    if(workshops.Count>0&&!Directory.Exists(r.Workshop))r.Workshop=workshops[0];
   }
   if(!InstallTarget.IsRoot(r.Game,server))r.Issues.Add(targetName+L.Get("Text218")+(server?"Unturned.exe／Unturned_Headless.exe":"Unturned.exe")+L.Get("Text219"));
   if(string.IsNullOrWhiteSpace(r.Workshop))r.Issues.Add(L.Get("Text220"));
   else if(!Directory.Exists(r.Workshop))r.Issues.Add(L.Get("Text221")+r.Workshop+L.Get("Text222"));
   var roots=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);roots[Path.Combine(home,"Packages")]=L.Get("Text223");roots[Path.Combine(store,"imports")]=L.Get("Text155");
   if(!string.IsNullOrWhiteSpace(r.Workshop))roots[r.Workshop]=r.Workshop.Replace('\\','/').TrimEnd('/').EndsWith("/steamapps/workshop/content/304930",StringComparison.OrdinalIgnoreCase)?L.Get("Text224"):L.Get("Text225");
   foreach(string lib in libs)roots[Path.Combine(lib,"steamapps","workshop","content","304930")]=L.Get("Text224");
   foreach(var pair in roots){if(!Directory.Exists(pair.Key))continue;report(0,L.Get("Text226")+pair.Key);r.Roots++;r.Available.AddRange(Packages.Scan(pair.Key,pair.Value,r.Issues,5));}
   foreach(string folder in folders)try{ScanSession.Check();if(session.Visited.Add(Path.GetFullPath(folder)))r.Available.AddRange(ScanCache.Read(folder,L.Get("Text227")));}catch(Exception ex){r.Issues.Add(folder+"："+Problems.Explain(ex));session.Problems.Add(new SourceProblem{Folder=folder,SummaryKey="UnreadablePackage",RemedyKey="ReloadSourceHelp",Details=Problems.Explain(ex)});}
   if(InstallTarget.IsRoot(r.Game,server))try{var e=new Engine(r.Game,store,false,server);r.Installed=e.ReadRegistry().packages;r.WorkshopRoots=WorkshopTargets.ContentRoots(r.Game,libs,r.Workshop);if(!server){var scopes=r.Available.Select(p=>p.Manifest.scopeId).Where(id=>id!=null).ToList();string installs=Path.Combine(store,"Installs");if(Directory.Exists(installs))foreach(string record in Directory.GetDirectories(installs).SelectMany(d=>new[]{"installed.json","pending.json"}.Select(name=>Path.Combine(d,name))).Where(File.Exists)){try{var saved=Data.Read<Registry>(record);if(saved.scopeId!=null&&Data.Same(saved.ownerGameRoot,r.Game))scopes.Add(saved.scopeId);}catch(Exception ex){r.Issues.Add(record+": "+Problems.Explain(ex));}}foreach(string scope in scopes.Distinct())try{var scoped=new Engine(r.Game,store,false,false,scope,r.WorkshopRoots);r.Installed.AddRange(scoped.ReadRegistry().packages);if(scoped.NeedsRecovery){r.PendingScopes.Add(scope);r.Issues.Add(L.Get("Text228")+" Workshop "+scope);}}catch(Exception ex){r.Issues.Add(Problems.Explain(ex));}}if(e.NeedsRecovery)r.Issues.Add(L.Get("Text228"));}catch(Exception ex){r.Issues.Add(L.Get("Text229")+Problems.Explain(ex));}
   ScanSession.Check();r.Available=r.Available.GroupBy(p=>p.Manifest.id+"|"+p.Folder,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).ToList();for(int i=0;i<r.Available.Count;i++)r.Available[i].ScanOrder=i;if(server)foreach(var p in r.Available){string reason;InstallTarget.SupportsServer(p,out reason);p.ServerIssue=reason;}r.SourceProblems=session.Problems;r.ParsedPackages=session.Parsed;r.CacheHits=session.CacheHits;r.VisitedDirectories=session.Visited.Count;r.ElapsedMilliseconds=timer.ElapsedMilliseconds;return r;
   }
  }
 }
}
