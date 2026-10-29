using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
namespace UnturnedModManager {
 public sealed class MapMetadata {public string name;public string workshopId;}
 public sealed class ModMetadata {public string modId,workshopId,type;public MapMetadata targetMap;public Dictionary<string,string> displayNames;}
 public sealed class MetadataDocument {public int schemaVersion;public List<ModMetadata> mods;}
 public sealed class SourceProblem {public string ModId,Folder,SummaryKey,RemedyKey,Details;}
 public static class ModNames {
  public const string FileName="unturned-manager.metadata.json";
  [ThreadStatic] public static string LastIssue;
  static bool TextValid(string value,int max){return !string.IsNullOrWhiteSpace(value)&&value.Length<=max&&!value.Any(char.IsControl);}
  static bool WorkshopId(string id){return id==null||Regex.IsMatch(id,@"^[0-9]{1,20}$");}
  public static ModMetadata Read(string folder,PackageManifest manifest){LastIssue=null;string file=Path.Combine(folder,FileName);if(!File.Exists(file))file=Path.Combine(folder,"Data",FileName);if(!File.Exists(file))return null;
   try{Paths.CheckLinks(file);var info=new FileInfo(file);if(info.Length>262144)throw new InvalidDataException();var document=Data.Read<MetadataDocument>(file);if(document==null||document.schemaVersion!=1||document.mods==null||document.mods.Count>128||document.mods.Any(m=>m==null||!TextValid(m.modId,160))||document.mods.GroupBy(m=>m.modId,StringComparer.Ordinal).Any(g=>g.Count()>1))throw new InvalidDataException();var item=document.mods.SingleOrDefault(m=>string.Equals(m.modId,manifest.id,StringComparison.Ordinal));if(item==null)throw new InvalidDataException();if(!WorkshopId(item.workshopId)||manifest.scopeId!=null&&item.workshopId!=manifest.scopeId||item.type!="map_translation"&&item.type!="localization"&&item.type!="module"||manifest.kind=="module"&&item.type!="module"||manifest.kind=="localization"&&item.type=="module")throw new InvalidDataException();if(item.targetMap!=null&&(!TextValid(item.targetMap.name,200)||!WorkshopId(item.targetMap.workshopId)))throw new InvalidDataException();if(item.type=="map_translation"&&item.targetMap==null)throw new InvalidDataException();if(item.displayNames==null||item.displayNames.Count==0||item.displayNames.Any(n=>!L.Languages.Contains(n.Key)||!TextValid(n.Value,200)))throw new InvalidDataException();return item;
   }catch(Exception ex){LastIssue=L.Get("MetadataIgnored")+Environment.NewLine+file+Environment.NewLine+ex.GetType().Name;return null;}
  }
  public static string Display(ModMetadata data,string original,string id){string value;if(data!=null&&data.displayNames!=null&&(data.displayNames.TryGetValue(L.Language,out value)||data.displayNames.TryGetValue("zh-TW",out value))&&!string.IsNullOrWhiteSpace(value))return value;return !string.IsNullOrWhiteSpace(original)?original:id;}
  public static string Display(Package p){return Display(p.Metadata,p.Manifest.name,p.Manifest.id);}
  public static string Search(Package p){return string.Join(" ",new[]{p.Manifest.name,p.Manifest.id,p.Metadata==null?null:p.Metadata.workshopId,p.Metadata==null||p.Metadata.targetMap==null?null:p.Metadata.targetMap.name,p.Metadata==null||p.Metadata.displayNames==null?null:string.Join(" ",p.Metadata.displayNames.Values)}.Where(v=>v!=null));}
 }
}
