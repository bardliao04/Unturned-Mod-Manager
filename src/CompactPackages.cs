using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.IO.Compression;
using System.Security.Cryptography;

namespace UnturnedModManager {
 public static class CompactPackages {
  // One payload and the existing manifests: both projects keep the same identity and hashes.
  public static void Export(string folder,string destination) {
   folder=Path.GetFullPath(folder);destination=Path.GetFullPath(destination);
   Paths.CheckLinks(folder);Paths.CheckLinks(destination);
   if(File.Exists(destination))throw new IOException("Output already exists: "+destination);
   var packages=Packages.LoadAll(folder,"compact");
   var files=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
   foreach(var package in packages)foreach(var file in package.Manifest.files){
    string hash;if(files.TryGetValue(file.source,out hash)&&!Data.Same(hash,file.sha256))throw new IOException("Conflicting source: "+file.source);
    files[file.source]=file.sha256;
   }
   var metadata=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   if(File.Exists(Path.Combine(folder,Packages.ManifestName)))metadata.Add(Packages.ManifestName);
   else {
    string prefix=File.Exists(Path.Combine(folder,"Data","version.json"))?"Data/":"";
    metadata.Add(prefix+"version.json");metadata.Add(prefix+"manifest.json");
    if(File.Exists(Paths.Safe(folder,prefix+"profiles.json")))metadata.Add(prefix+"profiles.json");
   }
   foreach(string name in new[]{ModNames.FileName,"Data/"+ModNames.FileName})
    if(File.Exists(Paths.Safe(folder,name)))metadata.Add(name);
   foreach(string name in metadata) {
    if(files.ContainsKey(name))throw new IOException("Payload overlaps metadata: "+name);
    files.Add(name,Data.Hash(Paths.Safe(folder,name)));
   }
   Directory.CreateDirectory(Path.GetDirectoryName(destination));
   string temp=destination+"."+Guid.NewGuid().ToString("N")+".tmp";
   try {
    using(var output=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))
    using(var archive=new ZipArchive(output,ZipArchiveMode.Create)) {
     var buffer=new byte[65536];
     foreach(var file in files.OrderBy(f=>f.Key,StringComparer.Ordinal)) {
      string path=Paths.Safe(folder,file.Key);
      var entry=archive.CreateEntry(file.Key,CompressionLevel.Optimal);
      using(var input=File.OpenRead(path))using(var stream=entry.Open())using(var hash=SHA256.Create()) {
       int count;while((count=input.Read(buffer,0,buffer.Length))>0){hash.TransformBlock(buffer,0,count,buffer,0);stream.Write(buffer,0,count);}
       hash.TransformFinalBlock(new byte[0],0,0);
       if(!Data.Same(BitConverter.ToString(hash.Hash).Replace("-",""),file.Value))throw new IOException("Source changed or damaged: "+file.Key);
      }
     }
    }
    File.Move(temp,destination);
   } finally {if(File.Exists(temp))File.Delete(temp);}
  }

#if TEST_BUILD
  public static int VerifyRoundTrip(string folder,string output) {
   Directory.CreateDirectory(output);
   string zip=Path.Combine(output,"compact.zip");
   var original=Packages.LoadAll(folder,"original");
   Export(folder,zip);
   var imported=Packages.ImportZip(zip,Path.Combine(output,"cache"));
   var actual=Packages.LoadAll(imported.Folder,"compact");
   if(!original.Select(Packages.InstallSignature).OrderBy(x=>x,StringComparer.Ordinal).SequenceEqual(actual.Select(Packages.InstallSignature).OrderBy(x=>x,StringComparer.Ordinal)))throw new Exception("Install identity or manifest changed.");
   foreach(var package in actual)foreach(var file in package.Manifest.files)
    if(!Data.Same(Data.Hash(Paths.Safe(package.Folder,file.source)),file.sha256))throw new Exception("Roundtrip hash mismatch.");
   long bytes=Directory.GetFiles(folder,"*",SearchOption.AllDirectories).Sum(f=>new FileInfo(f).Length);
   Data.Write(Path.Combine(output,"verification.json"),new{source=folder,sourceBytes=bytes,zipBytes=new FileInfo(zip).Length,scopes=actual.Count,files=actual.Sum(p=>p.Manifest.files.Count),result="PASS: identity, version, targets, dependency declarations and payload hashes preserved"});
   return 0;
  }
  public static void Test(string root) {
   string source=Path.Combine(root,"compact-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(source);
   string payload=Paths.Safe(source,"payload/Modules/Compact/test.dll");Directory.CreateDirectory(Path.GetDirectoryName(payload));File.WriteAllText(payload,"payload");
   Data.Write(Path.Combine(source,Packages.ManifestName),new PackageManifest{schemaVersion=1,id="test.compact",name="Compact",version="1.0.0",kind="module",files=new List<PackageFile>{new PackageFile{source="payload/Modules/Compact/test.dll",target="Modules/Compact/test.dll",sha256=Data.Hash(payload)}}});
   File.WriteAllText(Path.Combine(source,"unused.exe"),"not needed");string zip=source+".zip";Export(source,zip);
   using(var archive=ZipFile.OpenRead(zip))if(archive.Entries.Count!=2||archive.GetEntry("unused.exe")!=null)throw new Exception("Compact export included unrelated files.");
   var imported=Packages.ImportZip(zip,Path.Combine(source,"cache"));
   if(imported.Manifest.id!="test.compact"||!Data.Same(Data.Hash(Paths.Safe(imported.Folder,imported.Manifest.files[0].source)),Data.Hash(payload)))throw new Exception("Compact import identity/hash mismatch.");
   using(var form=new MainForm(source,Path.Combine(source,"handoff-state"),true,source)){}
   bool refused=false;try{Export(source,zip);}catch(IOException){refused=true;}if(!refused)throw new Exception("Existing export overwritten.");
   File.WriteAllText(payload,"damaged");string broken=source+"-broken.zip";refused=false;try{Export(source,broken);}catch(IOException){refused=true;}
   if(!refused||File.Exists(broken)||Directory.GetFiles(root,Path.GetFileName(broken)+".*.tmp").Length!=0)throw new Exception("Damaged export was published or leaked.");
  }
#endif
 }
}
