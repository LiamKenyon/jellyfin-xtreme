using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Dto;
using Jellyfin.Data;
using Jellyfin.Database.Implementations.Enums;
namespace XtreamBridge;
public sealed class Catchup(LiveEngine live,GuideEngine guide,Playback playback,IUserManager users):IChannel,IRequiresMediaInfoCallback
{
 public string Name=>"Xtream Catch-up";public string Description=>"Programme archives supplied by your configured IPTV providers";public string DataVersion=>"2-"+Panel.Key(Panel.Config);public string HomePageUrl=>"";public ChannelParentalRating ParentalRating=>ChannelParentalRating.Adult;
 // An archive is a separate Jellyfin channel. Keep it restricted to explicitly enabled,
 // unrestricted adult accounts until per-programme age metadata is reliably available.
 public bool IsEnabledFor(string userId){var user=Guid.TryParse(userId,out var id)?users.GetUserById(id):null;return Panel.Config.EnableLiveTv&&Panel.Config.EnableCatchup&&user!=null&&user.HasPermission(PermissionKind.IsAdministrator);}
 public InternalChannelFeatures GetChannelFeatures()=>new(){MediaTypes=[ChannelMediaType.Video],ContentTypes=[ChannelMediaContentType.Episode],SupportsContentDownloading=false,AutoRefreshLevels=0};
 public IEnumerable<ImageType> GetSupportedChannelImages()=>[];public Task<DynamicImageResponse> GetChannelImage(ImageType type,CancellationToken cancellationToken)=>throw new NotSupportedException();
 public async Task<ChannelItemResult> GetChannelItems(InternalChannelItemQuery query,CancellationToken cancellationToken)
 {
  if(!IsEnabledFor(query.UserId.ToString()))return new();var entries=(await live.Inventory(cancellationToken)).Where(e=>e.ArchiveDays>0).ToList();var items=new List<ChannelItemInfo>();
  if(string.IsNullOrEmpty(query.FolderId))items=entries.Select(e=>new ChannelItemInfo{Id="channel|"+e.Id,Name=e.Name,Type=ChannelItemType.Folder,ImageUrl=e.Logo,Tags=[e.Group]}).ToList();
  else if(query.FolderId.StartsWith("channel|",StringComparison.Ordinal)){
   var entry=entries.FirstOrDefault(e=>e.Id==query.FolderId[8..]);if(entry==null)return new();
   var now=DateTime.UtcNow;foreach(var p in await guide.Programmes(entry,now.AddDays(-entry.ArchiveDays),now,cancellationToken))items.Add(new ChannelItemInfo{Id=$"programme|{entry.Id}|{p.StartDate.Ticks}|{p.EndDate.Ticks}",Name=p.StartDate.ToString("dd MMM HH:mm")+" UTC · "+p.Name,Overview=p.Overview,ImageUrl=p.ImageUrl??entry.Logo,Type=ChannelItemType.Media,MediaType=ChannelMediaType.Video,ContentType=ChannelMediaContentType.Episode,StartDate=p.StartDate,EndDate=p.EndDate,RunTimeTicks=(p.EndDate-p.StartDate).Ticks});
  }
  return new(){TotalRecordCount=items.Count,Items=items.Skip(query.StartIndex??0).Take(query.Limit??100).ToArray()};
 }
 public async Task<IEnumerable<MediaSourceInfo>> GetChannelItemMediaInfo(string id,CancellationToken cancellationToken)
 {
  var parts=id.Split('|');if(parts.Length!=4||parts[0]!="programme"||!long.TryParse(parts[2],out var start)||!long.TryParse(parts[3],out var end)||start<0||start>DateTime.MaxValue.Ticks||end<=start||end>DateTime.MaxValue.Ticks)throw new PanelException("Invalid archive item.");
  // Ensure the requested time range is an actual retained programme, not an arbitrary URL.
  var entry=await live.Find(parts[1],cancellationToken);var begin=new DateTime(start,DateTimeKind.Utc);var finish=new DateTime(end,DateTimeKind.Utc);
  if(!(await guide.Programmes(entry,begin,finish,cancellationToken)).Any(p=>p.StartDate==begin&&p.EndDate==finish))throw new PanelException("Archive programme no longer available.");
  return [await playback.Archive(entry.Id,begin,finish,cancellationToken)];
 }
}
