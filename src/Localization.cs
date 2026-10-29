using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Web.Script.Serialization;
namespace UnturnedModManager {
 public static class L {
  public static readonly string[] Languages={"zh-TW","en-US","zh-CN"};
  static readonly Dictionary<string,Dictionary<string,string>> tables=Load();
  static readonly Dictionary<string,string> reverse=Reverse();
  static Dictionary<string,string> Reverse(){var map=new Dictionary<string,string>(StringComparer.Ordinal);foreach(var table in tables.Values)foreach(var pair in table)if(!map.ContainsKey(pair.Value))map[pair.Value]=pair.Key;return map;}
  static readonly KeyValuePair<string,string>[] prefixes=tables.Values.SelectMany(t=>t).Where(p=>(p.Value.Length>=4||p.Key=="Text131"||p.Key=="Text132"||p.Key=="Text210")&&p.Value.IndexOf('\n')<0).OrderByDescending(p=>p.Value.Length).ToArray();
  static string language="zh-TW";
  public static string Language {get{return language;}set{language=Languages.Contains(value)?value:"zh-TW";}}
  static Dictionary<string,Dictionary<string,string>> Load(){var result=new Dictionary<string,Dictionary<string,string>>();foreach(string code in Languages){using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("UnturnedModManager.Resources."+code+".json")){if(stream==null)throw new InvalidOperationException("Missing language resource: "+code);using(var reader=new StreamReader(stream))result[code]=new JavaScriptSerializer().Deserialize<Dictionary<string,string>>(reader.ReadToEnd());}}return result;}
  public static string Get(string key){string value;if(tables[Language].TryGetValue(key,out value)&&!string.IsNullOrEmpty(value))return value;if(tables["zh-TW"].TryGetValue(key,out value))return value;return "["+key+"]";}
  public static string Key(string text){string key;return text!=null&&reverse.TryGetValue(text,out key)?key:null;}
  public static string Text(string text){string key=Key(text);return key==null?text:Get(key);}
  public static string ManagerMessage(string text){if(string.IsNullOrEmpty(text))return text;return string.Join(Environment.NewLine,text.Replace("\r\n","\n").Split('\n').Select(line=>{string key=Key(line);if(key!=null)return Get(key);foreach(var entry in prefixes){if(line.StartsWith(entry.Value,StringComparison.Ordinal)){string suffix=line.Substring(entry.Value.Length);string suffixKey=Key(suffix);bool chained=entry.Key=="Text131"||entry.Key=="Text132"||entry.Key=="Text130"||entry.Key=="Text210";return Get(entry.Key)+(chained?ManagerMessage(suffix):suffixKey==null?suffix:Get(suffixKey));}}return line;}));}
  // Diagnostic classification is invariant even when exceptions are localized.
  public static string ToTraditional(string text){foreach(var pair in tables[Language].OrderByDescending(p=>p.Value.Length)){if(pair.Value.Length>=3)text=text.Replace(pair.Value,tables["zh-TW"][pair.Key]);}return text;}
  public static void Validate(){var keys=tables["zh-TW"].Keys.OrderBy(k=>k).ToArray();foreach(var t in tables){if(!keys.SequenceEqual(t.Value.Keys.OrderBy(k=>k))||t.Value.Any(p=>string.IsNullOrWhiteSpace(p.Value)))throw new Exception("Incomplete language: "+t.Key);}}
 }
}
