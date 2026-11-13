using System;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
namespace UnturnedModManager {
 // Each read-only validation pass has its own cache. Dispose it before writing files.
 public sealed class PreflightSession : IDisposable {
  [ThreadStatic] static PreflightSession current;
  readonly PreflightSession previous;
  readonly Dictionary<string,string> hashes=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
  public readonly HashSet<string> CheckedPaths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  public readonly CancellationToken Token;
  public int PathLookups,PathHits,HashReads,HashHits;
  public static PreflightSession Current{get{return current;}}
  public PreflightSession(CancellationToken token){previous=current;Token=token;current=this;}
  public void Dispose(){current=previous;}
  public static void Check(){if(current!=null)current.Token.ThrowIfCancellationRequested();}
  public string Hash(string file){
   Token.ThrowIfCancellationRequested();string key=Path.GetFullPath(file),value;
   if(hashes.TryGetValue(key,out value)){HashHits++;return value;}
   HashReads++;var buffer=new byte[65536];
   using(var sha=SHA256.Create())using(var input=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read,buffer.Length,FileOptions.SequentialScan)){
    int count;while((count=input.Read(buffer,0,buffer.Length))>0){Token.ThrowIfCancellationRequested();sha.TransformBlock(buffer,0,count,buffer,0);}
    Token.ThrowIfCancellationRequested();sha.TransformFinalBlock(new byte[0],0,0);value=BitConverter.ToString(sha.Hash).Replace("-","").ToLowerInvariant();
   }
   hashes[key]=value;return value;
  }
 }
}
