using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
namespace UnturnedModManager {
 public class VersionOption {public string Key {get;set;} public string Label {get;set;} public Package Package;}
 public static class PackageVersions {
  static int Priority(Package p){return L.Key(p.Origin)=="Text224"?0:L.Key(p.Origin)=="Text155"?1:2;}
  public static string Display(Package p){return string.IsNullOrWhiteSpace(p.Manifest.displayVersion)||p.Manifest.displayVersion==p.Manifest.version?p.Manifest.version:p.Manifest.displayVersion+" ("+p.Manifest.version+")";}
  public static List<VersionOption> Options(IEnumerable<Package> packages){
   var list=packages.GroupBy(p=>Path.GetFullPath(p.Folder),StringComparer.OrdinalIgnoreCase).Select(g=>g.OrderBy(Priority).First()).OrderByDescending(p=>new Version(p.Manifest.version)).ThenBy(Priority).ThenBy(p=>p.Folder,StringComparer.OrdinalIgnoreCase).ToList();
   return list.Select((p,i)=>new VersionOption{Key=p.Folder,Package=p,Label=Display(p)+(i==0?L.Get("Text230"):"")+(list.Count(q=>q.Manifest.version==p.Manifest.version)>1?" | "+L.Text(p.Origin):"")+(list.Count(q=>q.Manifest.version==p.Manifest.version&&q.Origin==p.Origin)>1?" · "+Data.Key(p.Folder).Substring(0,6):"")}).ToList();
  }
  public static VersionOption Choose(List<VersionOption> options,string saved){return options.FirstOrDefault(o=>Data.Same(o.Key,saved))??options.FirstOrDefault();}
  public static string ImportRoot(Package p,string store){
   string root=Path.GetFullPath(Path.Combine(store,"imports")).TrimEnd('\\'),folder=Path.GetFullPath(p.Folder);
   if(!folder.StartsWith(root+"\\",StringComparison.OrdinalIgnoreCase))throw new Exception(L.Get("Text231"));
   string part=folder.Substring(root.Length+1).Split('\\')[0];if(!Regex.IsMatch(part,"^[a-fA-F0-9]{32}$"))throw new Exception(L.Get("Text232"));
   string result=Path.GetFullPath(Path.Combine(root,part));Paths.CheckLinks(result);Paths.CheckLinks(folder);return result;
  }
  public static bool CanRemove(Package p,string store){try{ImportRoot(p,store);return true;}catch{return false;}}
  public static bool Under(string folder,string root){folder=Path.GetFullPath(folder).TrimEnd('\\');root=Path.GetFullPath(root).TrimEnd('\\');return Data.Same(folder,root)||folder.StartsWith(root+"\\",StringComparison.OrdinalIgnoreCase);}
  public static string RemoveImport(Package p,string store,Settings settings){
   string root=ImportRoot(p,store);var errors=new List<string>();var contents=Packages.Scan(root,L.Get("Text155"),errors,4);
   if(errors.Count>0||contents.Count!=1||!Data.Same(contents[0].Folder,p.Folder)||contents[0].Manifest.id!=p.Manifest.id)throw new Exception(L.Get("Text233"));
   string archive=Path.GetFullPath(Path.Combine(store,"RemovedImports",Guid.NewGuid().ToString("N")));Paths.CheckLinks(archive);Directory.CreateDirectory(Path.GetDirectoryName(archive));Directory.Move(root,archive);
   if(settings.packageFolders!=null)settings.packageFolders.RemoveAll(f=>Under(f,root));
   if(settings.versionChoices!=null)foreach(string key in settings.versionChoices.Where(k=>Under(k.Value,root)).Select(k=>k.Key).ToArray())settings.versionChoices.Remove(key);
   return archive;
  }
 }
}
