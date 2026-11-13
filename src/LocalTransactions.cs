using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace UnturnedModManager {
 public partial class Engine {
  string TryCreateLocalWork(){
   string name=".umm-transaction-"+Guid.NewGuid().ToString("N");
   string folder=LocalWorkPath(name);
   try{Directory.CreateDirectory(folder);return name;}
   catch(IOException){return null;}
   catch(UnauthorizedAccessException){return null;}
  }

  string LocalWorkPath(string name){
   if(name==null||!Regex.IsMatch(name,@"\A\.umm-transaction-[a-f0-9]{32}\z"))throw new InvalidDataException(L.Get("Text166"));
   return Paths.Safe(Root,name);
  }

  string LocalBackupPath(string folder,string file){
   if(file==null||!Regex.IsMatch(file,@"\Ar[0-9]+\.bak\z"))throw new InvalidDataException(L.Get("Text166"));
   return Paths.Safe(folder,file);
  }

  void ApplyLocalFile(string folder,Snapshot snapshot,string replacement){
   string target=Target(snapshot.target);
   if(snapshot.existed)File.Move(target,LocalBackupPath(folder,snapshot.saved));
   else if(File.Exists(target))throw new InstalledContentChangedException(snapshot.target);
   if(replacement!=null){
    Directory.CreateDirectory(Path.GetDirectoryName(target));
    File.Move(replacement,target);
   }
  }

  void RecoverLocalFiles(Journal journal){
   string folder=LocalWorkPath(journal.localFolder);
   var actions=new List<Action>();
   var targets=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   var backups=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   foreach(var entry in journal.files){
    if(entry==null||!targets.Add(entry.target)||!backups.Add(entry.saved))throw new InvalidDataException(L.Get("Text166"));
    string target=Target(entry.target), backup=LocalBackupPath(folder,entry.saved);
    bool exists=File.Exists(target);
    string current=exists?Data.Hash(target):null;
    bool untouched=entry.existed?exists&&Data.Same(current,entry.hash):!exists;
    if(untouched)continue;
    // Between the two moves an existing target is absent and its rollback file already exists.
    bool planned=entry.expectedAbsent?!exists:exists&&Data.ValidHash(entry.expectedHash)&&Data.Same(current,entry.expectedHash);
    if(!planned&&(exists||!entry.existed))throw new IOException(L.Get("RecoveryModified")+Environment.NewLine+entry.target);
    if(entry.existed&&(!File.Exists(backup)||!Data.Same(Data.Hash(backup),entry.hash)))throw new IOException(L.Get("Text167")+Environment.NewLine+entry.target);
    actions.Add(delegate{
     if(File.Exists(target))File.Delete(target);
     if(entry.existed){Directory.CreateDirectory(Path.GetDirectoryName(target));File.Move(backup,target);}
    });
   }
   foreach(var action in actions)action();
  }

  void ReleaseLocalWork(string name){
   try{
    string folder=LocalWorkPath(name);
    if(!Directory.Exists(folder))return;
    foreach(string file in Directory.GetFiles(folder)){
     if(!Regex.IsMatch(Path.GetFileName(file),@"\A(?:r[0-9]+\.bak|[a-f0-9]{24}\.bin)\z",RegexOptions.IgnoreCase))continue;
     Paths.CheckLinks(file);
     File.Delete(file);
    }
    Directory.Delete(folder,false);
   }catch(Exception){} // Cleanup can be retried without affecting the completed operation.
  }
 }
}
