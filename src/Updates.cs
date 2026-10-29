using System;
using System.Net;
using System.IO;
namespace UnturnedModManager {
 public class GithubRelease {public string tag_name;public string html_url;public bool draft;public bool prerelease;}
 public static class ManagerUpdates {
  public const string Current="1.0.1";
  public const string Latest="https://github.com/bardliao04/Unturned-Mod-Manager/releases/latest";
  static Version Normal(Version v){return new Version(v.Major,v.Minor,Math.Max(0,v.Build),Math.Max(0,v.Revision));}
  public static GithubRelease Parse(string json){var release=Data.Parse<GithubRelease>(json);if(release==null||release.draft||release.prerelease)return null;Version version;string tag=release.tag_name??"";if(!Version.TryParse(tag.TrimStart('v','V'),out version))throw new Exception("GitHub 版本格式無效");Uri uri;if(!Uri.TryCreate(release.html_url,UriKind.Absolute,out uri)||uri.Scheme!="https"||uri.Host!="github.com"||!uri.AbsolutePath.StartsWith("/bardliao04/Unturned-Mod-Manager/releases/",StringComparison.Ordinal))throw new Exception("GitHub 更新連結無效");return Normal(version)>Normal(new Version(Current))?release:null;}
  public static GithubRelease Fetch(){ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;var request=(HttpWebRequest)WebRequest.Create("https://api.github.com/repos/bardliao04/Unturned-Mod-Manager/releases/latest");request.UserAgent="Unturned-Mod-Manager/"+Current;request.Accept="application/vnd.github+json";request.Timeout=8000;request.ReadWriteTimeout=8000;using(var response=request.GetResponse())using(var stream=response.GetResponseStream())using(var reader=new StreamReader(stream)){string json=reader.ReadToEnd();if(json.Length>1048576)throw new Exception("GitHub 回覆過大");return Parse(json);}}
 }
}
