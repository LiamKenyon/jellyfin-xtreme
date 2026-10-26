using System.Collections.Concurrent;
using System.Xml;
using System.Xml.Linq;
using MediaBrowser.Controller.LiveTv;
namespace XtreamBridge;
public sealed class GuideFeed
{
 public DateTime Updated { get; set; }
 public Dictionary<string,List<ProgramInfo>> Channels { get; set; }=[];
}
public sealed class GuideEngine(LiveEngine live)
{
 private readonly ConcurrentDictionary<string,SemaphoreSlim> gates=new();
 private readonly ConcurrentDictionary<string,GuideFeed> feeds=new();
 private readonly ConcurrentDictionary<string,DateTime> retryAfter=new();
 public ConcurrentDictionary<string,string> Errors { get; }=new();
 private static string FeedKey(string identity)=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(identity)));
 private async Task<GuideFeed> Load(string key,Uri url,bool force,CancellationToken ct)
 {
  var gate=gates.GetOrAdd(key,_=>new(1,1));await gate.WaitAsync(ct);
  try{
   if(!feeds.TryGetValue(key,out var old))old=Store.Read<GuideFeed>("guide-"+key+".json");
   if(!force&&retryAfter.TryGetValue(key,out var retry)&&retry>DateTime.UtcNow)return old??new();
   if(old!=null&&!force&&old.Updated.AddMinutes(Panel.Config.GuideRefreshMinutes)>DateTime.UtcNow)return feeds[key]=old;
   Directory.CreateDirectory(Plugin.Instance.StoragePath);var path=Path.Combine(Plugin.Instance.StoragePath,"guide-"+key+".xml.tmp");
   try{
    await Panel.Download(url,path,256L*1024*1024,ct);
    var next=new GuideFeed{Updated=DateTime.UtcNow};
    using var reader=XmlReader.Create(path,new XmlReaderSettings{Async=true,DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=256L*1024*1024});
    while(await reader.ReadAsync().WaitAsync(ct)){
     if(reader.NodeType!=XmlNodeType.Element || reader.LocalName!="programme")continue;
     using var sub=reader.ReadSubtree();var e=await XElement.LoadAsync(sub,LoadOptions.None,ct);
     var id=(string?)e.Attribute("channel")??"";
     if(id.Length==0 || !Panel.Date((string?)e.Attribute("start"),out var start)||!Panel.Date((string?)e.Attribute("stop"),out var stop)||stop<=start||stop<DateTime.UtcNow.AddDays(-14)||start>DateTime.UtcNow.AddDays(14))continue;
     if(!next.Channels.TryGetValue(id,out var list))next.Channels[id]=list=[];
     list.Add(new ProgramInfo{Name=(string?)e.Element("title")??"Untitled",Overview=(string?)e.Element("desc"),EpisodeTitle=(string?)e.Element("sub-title"),StartDate=start,EndDate=stop,ImageUrl=LiveEngine.SafeImage((string?)e.Element("icon")?.Attribute("src")??""),OfficialRating=(string?)e.Element("rating")?.Element("value"),IsRepeat=e.Element("previously-shown")!=null,IsLive=e.Element("live")!=null,Genres=e.Elements("category").Select(x=>x.Value).ToList()});
    }
    if(next.Channels.Count==0 && old?.Channels.Count>0)throw new PanelException("Empty guide response; previous listings retained.");
    // Keep already-aired programmes for provider catch-up even when the next feed
    // no longer carries yesterday's listings. New provider entries take precedence.
    if(old!=null)foreach(var pair in old.Channels){var history=pair.Value.Where(p=>p.EndDate<=DateTime.UtcNow&&p.EndDate>=DateTime.UtcNow.AddDays(-14));if(!next.Channels.TryGetValue(pair.Key,out var list))next.Channels[pair.Key]=list=[];var starts=list.Select(p=>p.StartDate).ToHashSet();list.AddRange(history.Where(p=>!starts.Contains(p.StartDate)));}
    foreach(var id in next.Channels.Keys.ToArray())next.Channels[id]=next.Channels[id].DistinctBy(x=>(x.StartDate,x.EndDate,x.Name)).OrderBy(x=>x.StartDate).ToList();
    Store.Save("guide-"+key+".json",next);feeds[key]=next;Errors.TryRemove(key,out _);retryAfter.TryRemove(key,out _);return next;
   }catch(Exception ex) when(ex is PanelException or XmlException or IOException){Errors[key]=ex is PanelException?ex.Message:"Guide parsing/storage failed; previous listings retained.";retryAfter[key]=DateTime.UtcNow.AddMinutes(5);if(old!=null)feeds[key]=old;return old??new();}
   finally{if(File.Exists(path))File.Delete(path);}
  }finally{gate.Release();}
 }
 public async Task<List<ProgramInfo>> Programmes(LiveEntry entry,DateTime start,DateTime end,CancellationToken ct)
 {
  var c=Panel.Config;if(!c.EnableEpg)return [];
  var provider=Providers.All(c).First(p=>p.Id==entry.ProviderId);var rule=c.ChannelRules.FirstOrDefault(r=>r.Id==entry.Id);
  var useAlternative=rule?.GuideSource=="alternative" && c.AlternativeEpgUrl.Length>0;
  var key=FeedKey(useAlternative?c.AlternativeEpgUrl:Providers.Identity(provider.Config));
  var uri=useAlternative?new Uri(c.AlternativeEpgUrl):Panel.Url(provider.Config,"xmltv.php");
  var feed=await Load(key,uri,false,ct);var offset=rule?.OffsetMinutes??0;
  return feed.Channels.GetValueOrDefault(entry.EpgId,[]).Where(p=>p.StartDate.AddMinutes(offset)<end&&p.EndDate.AddMinutes(offset)>start).Select(p=>new ProgramInfo{Id=entry.Id+"_"+p.StartDate.Ticks,ChannelId=entry.Id,Name=p.Name,Overview=p.Overview,EpisodeTitle=p.EpisodeTitle,StartDate=p.StartDate.AddMinutes(offset),EndDate=p.EndDate.AddMinutes(offset),Genres=p.Genres,ImageUrl=p.ImageUrl,OfficialRating=p.OfficialRating,IsRepeat=p.IsRepeat,IsLive=p.IsLive}).ToList();
 }
 public async Task Refresh(CancellationToken ct){foreach(var p in Providers.All(Panel.Config))await Load(FeedKey(Providers.Identity(p.Config)),Panel.Url(p.Config,"xmltv.php"),true,ct);if(Panel.Config.AlternativeEpgUrl.Length>0)await Load(FeedKey(Panel.Config.AlternativeEpgUrl),new Uri(Panel.Config.AlternativeEpgUrl),true,ct);}
 public async Task<object> Coverage(CancellationToken ct){var items=await live.Inventory(ct);var now=DateTime.UtcNow;var rows=new List<object>();foreach(var e in items){var programmes=await Programmes(e,now,now.AddDays(2),ct);rows.Add(new {e.Id,e.Name,e.EpgId,HasEpg=programmes.Count>0,Programmes=programmes.Count});}return rows;}
 public async Task<string[]> EpgIds(string providerId,bool alternative,CancellationToken ct){var p=Providers.All(Panel.Config).First(x=>x.Id==providerId);var uri=alternative?new Uri(Panel.Config.AlternativeEpgUrl):Panel.Url(p.Config,"xmltv.php");var feed=await Load(FeedKey(alternative?Panel.Config.AlternativeEpgUrl:Providers.Identity(p.Config)),uri,false,ct);return feed.Channels.Keys.Order().ToArray();}
}
