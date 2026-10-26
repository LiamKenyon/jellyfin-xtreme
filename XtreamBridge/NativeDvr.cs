using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Model.LiveTv;
namespace XtreamBridge;
public sealed class NativeDvr(IServerConfigurationManager configuration,LiveEngine live,GuideEngine guide,Playback playback)
{
 private const string Prefix="XtreamBridge managed ";
 private static string Id(string value)=>Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
 public async Task Apply(CancellationToken ct)
 {
  var c=Panel.Config;var options=(LiveTvOptions)configuration.GetConfiguration("livetv");
  var ownedIds=Store.Read<HashSet<string>>("managed-tuners.json")??[];
  foreach(var p in Providers.All(c))ownedIds.Add(Id("xtream-tuner-"+p.Id));
  var tuners=options.TunerHosts.Where(t=>!ownedIds.Contains(t.Id)).ToList();
  var nextOwned=new HashSet<string>();
  var listings=options.ListingProviders.Where(p=>p.Id!=Id("xtream-xmltv")).ToList();
  var channelMap=new Dictionary<string,string>();
  if(c.NativeDvr&&c.EnableLiveTv){
   var entries=await live.Inventory(ct);var dir=Plugin.Instance.StoragePath;Directory.CreateDirectory(dir);
   foreach(var p in Providers.All(c)){
    var file=Path.Combine(dir,"tuner-"+Id(p.Id)+".m3u");var content=new StringBuilder("#EXTM3U\n");
    string Clean(string v)=>v.Replace('"','\'').Replace('\r',' ').Replace('\n',' ');
    int number=0;
    foreach(var e in entries.Where(e=>e.ProviderId==p.Id)){
     number++;
     var src=await playback.Source(e.Id,ct);
     channelMap[e.Id]="m3u_"+file.GetMD5().ToString("N")+src.Path.GetMD5().ToString("N");
     content.AppendLine($"#EXTINF:-1 tvg-id=\"xtream-{e.Id}\" tvg-name=\"{Clean(e.Name)}\" tvg-logo=\"{Clean(e.Logo)}\" group-title=\"{Clean(e.Group)}\" tvg-chno=\"{(c.ShowChannelNumbers&&e.Number.Length>0?Clean(e.Number):number.ToString(System.Globalization.CultureInfo.InvariantCulture))}\",");content.AppendLine(src.Path);
    }
    await Atomic(file,content.ToString(),ct);
    nextOwned.Add(Id("xtream-tuner-"+p.Id));
    tuners.Add(new TunerHostInfo{Id=Id("xtream-tuner-"+p.Id),FriendlyName=Prefix+p.Id,Type="m3u",Url=file,TunerCount=p.Limit,AllowStreamSharing=true,EnableStreamLooping=true});
   }
   var xml=new XElement("tv");
   foreach(var e in entries){xml.Add(new XElement("channel",new XAttribute("id","xtream-"+e.Id),new XElement("display-name",e.Name)));
    if(c.EnableEpg)foreach(var programme in await guide.Programmes(e,DateTime.UtcNow.AddDays(-1),DateTime.UtcNow.AddDays(14),ct)){
     var x=new XElement("programme",new XAttribute("channel","xtream-"+e.Id),new XAttribute("start",programme.StartDate.ToString("yyyyMMddHHmmss")+" +0000"),new XAttribute("stop",programme.EndDate.ToString("yyyyMMddHHmmss")+" +0000"),new XElement("title",programme.Name??""),new XElement("desc",programme.Overview??""));
     if(!string.IsNullOrEmpty(programme.EpisodeTitle))x.Add(new XElement("sub-title",programme.EpisodeTitle));
     if(!string.IsNullOrEmpty(programme.OfficialRating))x.Add(new XElement("rating",new XElement("value",programme.OfficialRating)));
     if(!string.IsNullOrEmpty(programme.ImageUrl))x.Add(new XElement("icon",new XAttribute("src",programme.ImageUrl)));
     if(programme.IsRepeat)x.Add(new XElement("previously-shown"));if(programme.IsLive)x.Add(new XElement("live"));
     foreach(var genre in programme.Genres)x.Add(new XElement("category",genre));xml.Add(x);
    }
   }
   var epg=Path.Combine(dir,"managed-guide.xml");await Atomic(epg,new XDocument(xml).ToString(),ct);
   listings.Add(new ListingsProviderInfo{Id=Id("xtream-xmltv"),Type="xmltv",Path=epg,EnableAllTuners=false,EnabledTuners=tuners.Where(t=>nextOwned.Contains(t.Id)).Select(t=>t.Id).ToArray()});
  }
  options.TunerHosts=tuners.ToArray();options.ListingProviders=listings.ToArray();configuration.SaveConfiguration("livetv",options);Store.Save("native-channel-map.json",channelMap);Store.Save("managed-tuners.json",nextOwned);
 }
 private static async Task Atomic(string path,string content,CancellationToken ct){var temp=path+".tmp";await File.WriteAllTextAsync(temp,content,ct);if(!OperatingSystem.IsWindows())File.SetUnixFileMode(temp,UnixFileMode.UserRead|UnixFileMode.UserWrite);File.Move(temp,path,true);}
}
