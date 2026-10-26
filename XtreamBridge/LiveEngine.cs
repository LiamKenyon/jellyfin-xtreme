using System.Collections.Concurrent;
using System.Text.Json;
namespace XtreamBridge;
public sealed class LiveEngine
{
 private readonly ConcurrentDictionary<string,SemaphoreSlim> gates=new();
 private readonly ConcurrentDictionary<string,LiveSnapshot> snapshots=new();
 private readonly ConcurrentDictionary<string,ProviderStatus> statuses=new();
 public IEnumerable<ProviderStatus> Status {
  get { var configured=Providers.All(Panel.Config).ToArray();foreach(var p in configured){var snapshot=Get(p.Id,p.Config);var state=statuses.GetOrAdd(p.Id,_=>new(){Id=p.Id,Name=p.Name,Imported=snapshot?.Entries.Count??0,LastSuccess=snapshot?.Updated,State=snapshot==null?"Not synced":"Cached"});state.Name=p.Name;}return configured.Select(p=>statuses[p.Id]).OrderBy(x=>x.Name).ToArray(); }
 }
 public static string SafeImage(string value)=>Uri.TryCreate(value,UriKind.Absolute,out var u)&&u.Scheme is "http" or "https" && u.UserInfo.Length==0 && u.Query.Length==0 ? value : "";
 private LiveSnapshot? Get(string id,Settings c)
 {
  var identity=Providers.Identity(c);var key=id+identity;
  if(snapshots.TryGetValue(key,out var hit))return hit;
  var disk=Store.Read<LiveSnapshot>("catalogue-"+id+"-"+identity+".json");
  if(disk?.Identity==identity){snapshots[key]=disk;return disk;}return null;
 }
 public async Task<bool> Sync(string id,string name,Settings c,bool approve,CancellationToken ct)
 {
  var gate=gates.GetOrAdd(id,_=>new(1,1));await gate.WaitAsync(ct);
  var state=statuses.GetOrAdd(id,_=>new(){Id=id,Name=name});state.LastAttempt=DateTime.UtcNow;state.State="Syncing";state.Error="";
  try {
   var previous=Get(id,c);
   var account=await Panel.Api(c,"",ct,fresh:true);
   if(!account.TryGetProperty("user_info",out var user)||Panel.S(user,"auth")!="1")throw new PanelException("Provider rejected the account.");
   state.AccountStatus=Panel.S(user,"status");state.Expiry=Panel.S(user,"exp_date");state.ReportedConnections=Panel.S(user,"active_cons");state.ReportedLimit=Panel.S(user,"max_connections");
   if(!state.AccountStatus.Equals("Active",StringComparison.OrdinalIgnoreCase))throw new PanelException("Provider account is not active.");
   var categories=Panel.Rows(await Panel.Api(c,"get_live_categories",ct,fresh:true)).GroupBy(x=>Panel.S(x,"category_id")).ToDictionary(g=>g.Key,g=>Panel.S(g.First(),"category_name"));
   var raw=Panel.Rows(await Panel.Api(c,"get_live_streams",ct,fresh:true)).ToArray();
   // Snapshot the provider's live inventory, independently from user visibility.
   var unrestricted=JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(c))!;unrestricted.DisabledLiveCategories=[];unrestricted.HideOnDemandCategories=false;
   var rows=raw.Where(r=>LiveContent.IsLive(unrestricted,r,categories.GetValueOrDefault(Panel.S(r,"category_id"),""))).ToArray();
   var entries=rows.Select(r=>new LiveEntry { Id=Providers.ChannelId(id,Panel.S(r,"stream_id")),StreamId=Panel.S(r,"stream_id"),ProviderId=id,Name=LiveContent.Name(r),CategoryId=Panel.S(r,"category_id"),Group=categories.GetValueOrDefault(Panel.S(r,"category_id"),"Uncategorised"),Number=Panel.S(r,"channel_number"),Logo=SafeImage(Panel.S(r,"stream_icon")),EpgId=Panel.S(r,"epg_channel_id"),Radio=Panel.S(r,"stream_type").Contains("radio",StringComparison.OrdinalIgnoreCase),ArchiveDays=Panel.S(r,"tv_archive")=="1"&&int.TryParse(Panel.S(r,"tv_archive_duration"),out var days)?Math.Clamp(days,0,14):0 }).DistinctBy(r=>r.Id).ToList();
   foreach(var entry in entries)categories.TryAdd(entry.CategoryId,entry.Group);
   var before=previous?.Entries.ToDictionary(e=>e.Id)??[];var after=entries.ToDictionary(e=>e.Id);
   state.Added=after.Keys.Except(before.Keys).Count();state.Removed=before.Keys.Except(after.Keys).Count();state.Renamed=entries.Count(e=>before.TryGetValue(e.Id,out var old)&&old.Name!=e.Name);
   // Large/empty successful responses may be provider failures; keep known-good data.
   if(!approve&&before.Count>0&&(entries.Count==0 || state.Removed*100.0/before.Count>=c.RemovalThresholdPercent)) {
    state.State="Review required";state.Error="Large removal detected. Existing channels retained. Review counts and approve removals to apply.";state.Imported=before.Count;return false;
   }
   var accepted=previous==null||c.AutoEnableNewCategories?categories.Keys.ToArray():previous.AcceptedCategories;
   var next=new LiveSnapshot{Identity=Providers.Identity(c),Updated=DateTime.UtcNow,Entries=entries,Categories=categories,AcceptedCategories=accepted};
   Store.Save("catalogue-"+id+"-"+next.Identity+".json",next);snapshots[id+next.Identity]=next;
   state.State="Ready";state.Imported=entries.Count;state.LastSuccess=next.Updated;state.PendingCategories=categories.Keys.Except(accepted).ToArray();return true;
  }catch(PanelException e){state.State="Using cached data";state.Error=e.Message;var old=Get(id,c);state.LastSuccess=old?.Updated;state.Imported=old?.Entries.Count??0;if(old==null)state.State="Unavailable";return false;}
  finally{gate.Release();}
 }
 public async Task<List<LiveEntry>> Inventory(CancellationToken ct,bool visible=true)
 {
  var c=Panel.Config;var result=new List<LiveEntry>();
  var categoryRules=c.CategoryRules.ToDictionary(x=>(x.ProviderId,x.CategoryId));var channelRules=c.ChannelRules.ToDictionary(x=>x.Id);
  foreach(var p in Providers.All(c)){
   var snap=Get(p.Id,p.Config);if(snap==null&&(!statuses.TryGetValue(p.Id,out var last)||last.LastAttempt==null||last.LastAttempt<DateTime.UtcNow.AddMinutes(-5))){await Sync(p.Id,p.Name,p.Config,false,ct);snap=Get(p.Id,p.Config);}
   if(snap==null)continue;
   foreach(var item in snap.Entries){
    if(!visible){result.Add(item);continue;}
    var cat=categoryRules.GetValueOrDefault((p.Id,item.CategoryId));
    var rule=channelRules.GetValueOrDefault(item.Id);
    if(rule?.Enabled==false || cat?.Enabled==false)continue;
    if(cat==null && (!snap.AcceptedCategories.Contains(item.CategoryId) || p.Config.DisabledLiveCategories.Contains(item.CategoryId)))continue;
    if(c.HideOnDemandCategories && System.Text.RegularExpressions.Regex.IsMatch(item.Group,@"\b(vod|series|tv\s*shows?|on[\s-]*demand)\b",System.Text.RegularExpressions.RegexOptions.IgnoreCase))continue;
    var copy=JsonSerializer.Deserialize<LiveEntry>(JsonSerializer.Serialize(item))!;
    if(!string.IsNullOrWhiteSpace(cat?.Group))copy.Group=cat.Group;
    if(rule!=null){if(rule.Name.Length>0)copy.Name=rule.Name;if(rule.Group.Length>0)copy.Group=rule.Group;if(rule.Number.Length>0)copy.Number=rule.Number;if(rule.Logo.Length>0)copy.Logo=rule.Logo;if(rule.EpgId.Length>0)copy.EpgId=rule.EpgId;}
    result.Add(copy);
   }
  }
  return result.OrderBy(x=>x.Group).ThenBy(x=>x.Number).ThenBy(x=>x.Name).ToList();
 }
 public async Task<LiveEntry> Find(string id,CancellationToken ct)=>(await Inventory(ct)).FirstOrDefault(e=>e.Id==id)??throw new PanelException("Channel disabled or unavailable.");
 public async Task SyncAll(bool approve,CancellationToken ct)
 {
  foreach(var p in Providers.All(Panel.Config))await Sync(p.Id,p.Name,p.Config,approve,ct);
 }
 public async Task RefreshDue(CancellationToken ct)
 {
  foreach(var p in Providers.All(Panel.Config)){var snap=Get(p.Id,p.Config);if(snap==null || snap.Updated.AddMinutes(Panel.Config.CatalogueRefreshMinutes)<DateTime.UtcNow)await Sync(p.Id,p.Name,p.Config,false,ct);}
 }
}
