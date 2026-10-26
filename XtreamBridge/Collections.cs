using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Model.Entities;
using Jellyfin.Data.Enums;
namespace XtreamBridge;
public sealed class Groups(ICollectionManager collections,ILibraryManager library,LiveEngine live)
{
 public async Task Apply(CancellationToken ct)
 {
  if(!Panel.Config.EnableCollections)return;
  var inventory=Panel.Config.EnableLiveTv?await live.Inventory(ct):[];
  var channels=library.GetItemList(new InternalItemsQuery{IncludeItemTypes=[BaseItemKind.TvChannel],Recursive=true}).OfType<LiveTvChannel>().ToList();
  var nativeMap=Store.Read<Dictionary<string,string>>("native-channel-map.json")??[];
  var ownership=Store.Read<Dictionary<string,Guid>>("collections.json")??[];
  var members=Store.Read<Dictionary<string,Guid[]>>("collection-members.json")??[];
  foreach(var group in inventory.GroupBy(e=>e.Group)){
   ct.ThrowIfCancellationRequested();var ids=group.Select(e=>Panel.Config.NativeDvr?nativeMap.GetValueOrDefault(e.Id,""):e.Id).ToHashSet();
   // Native DVR imports use tuner channel identifiers; match by exact generated ID.
   var desired=channels.Where(ch=>ids.Contains(ch.ExternalId)&&(Panel.Config.NativeDvr||ch.ServiceName=="Xtream Bridge")).Select(ch=>ch.Id).ToHashSet();
   if(desired.Count==0)continue;
   if(!ownership.TryGetValue(group.Key,out var cid)||library.GetItemById<BoxSet>(cid)==null){
    var box=await collections.CreateCollectionAsync(new CollectionCreationOptions{Name="TV · "+group.Key,ItemIdList=desired.Select(i=>i.ToString("N")).ToArray(),ProviderIds=new(){["XtreamBridgeGroup"]=group.Key}});ownership[group.Key]=cid=box.Id;
   }else{var previous=members.GetValueOrDefault(group.Key,[]).ToHashSet();var remove=previous.Except(desired).ToArray();if(remove.Length>0)await collections.RemoveFromCollectionAsync(cid,remove);await collections.AddToCollectionAsync(cid,desired.Except(previous));}
   members[group.Key]=desired.ToArray();
  }
  // Remove only members this plugin previously added; never delete collections or user content.
  foreach(var stale in ownership.Keys.Except(inventory.Select(e=>e.Group)).ToArray())if(members.TryGetValue(stale,out var ids)&&ids.Length>0){await collections.RemoveFromCollectionAsync(ownership[stale],ids);members[stale]=[];}
  Store.Save("collections.json",ownership);Store.Save("collection-members.json",members);
 }
}
