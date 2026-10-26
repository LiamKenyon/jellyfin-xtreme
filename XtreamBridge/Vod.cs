using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.MediaInfo;
namespace XtreamBridge;
public sealed class Vod : IChannel,IRequiresMediaInfoCallback
{
 public string Name=>"Xtream Movies";
 public string Description=>"On-demand movies from your Xtream panel";
 public string DataVersion=>"1-"+Panel.Key(Panel.Config);
 public string HomePageUrl=>"";
 public ChannelParentalRating ParentalRating=>ChannelParentalRating.Adult;
 public bool IsEnabledFor(string userId)=>Panel.Config.EnableVod && Panel.Ready(Panel.Config);
 public InternalChannelFeatures GetChannelFeatures()=>new() { MediaTypes=[ChannelMediaType.Video],ContentTypes=[ChannelMediaContentType.Movie],SupportsContentDownloading=false,AutoRefreshLevels=1 };
 public IEnumerable<ImageType> GetSupportedChannelImages()=>[];
 public Task<DynamicImageResponse> GetChannelImage(ImageType type,CancellationToken cancellationToken)=>throw new NotSupportedException();
 public async Task<ChannelItemResult> GetChannelItems(InternalChannelItemQuery query,CancellationToken cancellationToken)
 {
  var c=Panel.Config;if (!c.EnableVod || !Panel.Ready(c)) return new();
  List<ChannelItemInfo> items;
  if (string.IsNullOrEmpty(query.FolderId)) {
   items=Panel.Rows(await Panel.Api(c,"get_vod_categories",cancellationToken)).Where(r=>Panel.Allowed(c,r,false)).Select(r=>new ChannelItemInfo { Id="cat:"+Panel.S(r,"category_id"),Name=Panel.S(r,"category_name"),Type=ChannelItemType.Folder }).ToList();
  } else {
   if (!query.FolderId.StartsWith("cat:",StringComparison.Ordinal)) return new();
   var cat=query.FolderId[4..];if (c.DisabledVodCategories.Contains(cat)) return new();
   items=Panel.Rows(await Panel.Api(c,"get_vod_streams",cancellationToken)).Where(r=>Panel.S(r,"category_id")==cat && Panel.Allowed(c,r,false)).Where(r=>long.TryParse(Panel.S(r,"stream_id"),out _)).Select(r=>new ChannelItemInfo { Id="movie:"+Panel.S(r,"stream_id"),Name=Panel.S(r,"name"),ImageUrl=Panel.S(r,"stream_icon"),Type=ChannelItemType.Media,MediaType=ChannelMediaType.Video,ContentType=ChannelMediaContentType.Movie }).ToList();
  }
  items=items.OrderBy(x=>x.Name,StringComparer.OrdinalIgnoreCase).ToList(); if(query.SortDescending) items.Reverse();
  return new ChannelItemResult { TotalRecordCount=items.Count,Items=items.Skip(query.StartIndex??0).Take(query.Limit??int.MaxValue).ToList() };
 }
 public async Task<IEnumerable<MediaSourceInfo>> GetChannelItemMediaInfo(string id,CancellationToken cancellationToken)
 {
  var c=Panel.Config;if (!c.EnableVod || !id.StartsWith("movie:",StringComparison.Ordinal)) throw new PanelException("VOD disabled or invalid item.");
  var sid=id[6..];var row=Panel.Rows(await Panel.Api(c,"get_vod_streams",cancellationToken)).FirstOrDefault(r=>Panel.S(r,"stream_id")==sid && Panel.Allowed(c,r,false));
  if (row.ValueKind!=System.Text.Json.JsonValueKind.Object) throw new PanelException("Movie disabled or unavailable.");
  var extension=Panel.S(row,"container_extension");if (extension.Length==0) extension="mp4";
  return [new MediaSourceInfo { Id="xtream-movie-"+sid,Path=Panel.Stream(c,"movie",sid,extension),Container=extension,Protocol=MediaProtocol.Http,IsRemote=true,SupportsDirectPlay=false,SupportsDirectStream=true,SupportsTranscoding=true }];
 }
}
