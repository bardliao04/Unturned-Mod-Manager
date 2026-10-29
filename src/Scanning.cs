using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
namespace UnturnedModManager {
 public sealed class ScanSession : IDisposable {
  [ThreadStatic] static ScanSession current;
  readonly ScanSession previous;public readonly CancellationToken Token;public readonly HashSet<string> Visited=new HashSet<string>(StringComparer.OrdinalIgnoreCase);public int CacheHits,Parsed;
  public static ScanSession Current{get{return current;}}
  public ScanSession(CancellationToken token){previous=current;Token=token;current=this;}
  public void Dispose(){current=previous;}
  public static void Check(){if(current!=null)current.Token.ThrowIfCancellationRequested();}
 }
 public static class ScanCache {
  class Entry{public string Stamp;public List<Package> Packages;public int Files;public long Used;}
  static readonly Dictionary<string,Entry> entries=new Dictionary<string,Entry>(StringComparer.OrdinalIgnoreCase);static int fileCount;
  public static List<Package> Read(string folder,string origin){ScanSession.Check();string key=Path.GetFullPath(folder);string[] files={Path.Combine(folder,Packages.ManifestName),Path.Combine(folder,"version.json"),Path.Combine(folder,"manifest.json"),Path.Combine(folder,"profiles.json"),Path.Combine(folder,"Data","version.json"),Path.Combine(folder,"Data","manifest.json"),Path.Combine(folder,"Data","profiles.json")};string stamp=string.Join("|",files.Select(p=>{var f=new FileInfo(p);return f.Exists?f.Length+":"+f.LastWriteTimeUtc.Ticks:"-";}));Entry entry;lock(entries){if(entries.TryGetValue(key,out entry)&&entry.Stamp==stamp){entry.Used=DateTime.UtcNow.Ticks;if(ScanSession.Current!=null)ScanSession.Current.CacheHits++;return Copy(entry.Packages,origin);}}
   var loaded=Packages.LoadAll(folder,origin);if(ScanSession.Current!=null)ScanSession.Current.Parsed++;lock(entries){Entry old;if(entries.TryGetValue(key,out old)){fileCount-=old.Files;entries.Remove(key);}int count=loaded.Sum(p=>p.Manifest.files.Count);while(entries.Count>0&&(entries.Count>=4096||fileCount+count>100000)){var victim=entries.OrderBy(p=>p.Value.Used).First();fileCount-=victim.Value.Files;entries.Remove(victim.Key);}if(count<=100000){entries[key]=new Entry{Stamp=stamp,Packages=loaded,Files=count,Used=DateTime.UtcNow.Ticks};fileCount+=count;}}return Copy(loaded,origin);
  }
  static List<Package> Copy(List<Package> packages,string origin){return packages.Select(p=>new Package{Folder=p.Folder,Manifest=p.Manifest,Origin=origin,ModifiedUtc=p.ModifiedUtc,ImportedUtc=p.ImportedUtc}).ToList();}
  public static int FileCount{get{lock(entries)return fileCount;}}
  public static int Count{get{lock(entries)return entries.Count;}}
 }
}
