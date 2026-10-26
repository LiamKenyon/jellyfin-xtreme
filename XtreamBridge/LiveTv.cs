using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Model.LiveTv;
namespace XtreamBridge;
public sealed class LiveTv : ILiveTvService
{
 public string Name => "Xtream Bridge";
 public string HomePageUrl => "";
 public async Task<IEnumerable<ChannelInfo>> GetChannelsAsync(CancellationToken ct)
 {
  var c=Panel.Config;if (!c.EnableLiveTv || !Panel.Ready(c)) return [];
  var (rows,categories)=await LiveContent.Catalogue(c,ct);
  return rows.Select(r=>new ChannelInfo { Id=Panel.S(r,"stream_id"),Name=LiveContent.Name(r),Number=LiveContent.Number(c,r),ImageUrl=Panel.S(r,"stream_icon"),ChannelType=Panel.S(r,"stream_type").Contains("radio",StringComparison.OrdinalIgnoreCase)?ChannelType.Radio:ChannelType.TV,ChannelGroup=categories.GetValueOrDefault(Panel.S(r,"category_id"),"Uncategorised") }).ToArray();
 }
 public async Task<IEnumerable<ProgramInfo>> GetProgramsAsync(string channelId,DateTime startDateUtc,DateTime endDateUtc,CancellationToken cancellationToken)
 {
  var c=Panel.Config;if (!c.EnableLiveTv || !c.EnableEpg || !Panel.Ready(c)) return [];
  var guide=await Panel.Epg(c,cancellationToken);
  return guide.GetValueOrDefault(channelId,[]).Where(p=>p.StartDate<endDateUtc && p.EndDate>startDateUtc).ToArray();
 }
 public async Task<MediaSourceInfo> GetChannelStream(string channelId,string streamId,CancellationToken cancellationToken)
 {
  var c=Panel.Config;
  if (!c.EnableLiveTv || !(await GetChannelsAsync(cancellationToken)).Any(x=>x.Id==channelId)) throw new PanelException("Channel disabled or unavailable.");
  var format=c.LiveFormat=="m3u8" ? "m3u8" : "ts";
  return new MediaSourceInfo { Id="xtream-live-"+channelId,Path=Panel.Stream(c,"live",channelId,format),Protocol=MediaProtocol.Http,IsRemote=true,Container=format,SupportsDirectPlay=false,SupportsDirectStream=true,SupportsTranscoding=true,IsInfiniteStream=true,ReadAtNativeFramerate=false };
 }
 public async Task<List<MediaSourceInfo>> GetChannelStreamMediaSources(string channelId,CancellationToken cancellationToken) => [await GetChannelStream(channelId,"",cancellationToken)];
 public Task CloseLiveStream(string id,CancellationToken cancellationToken)=>Task.CompletedTask;
 public Task ResetTuner(string id,CancellationToken cancellationToken)=>Task.CompletedTask;
 public Task<IEnumerable<TimerInfo>> GetTimersAsync(CancellationToken cancellationToken)=>Task.FromResult<IEnumerable<TimerInfo>>([]);
 public Task<IEnumerable<SeriesTimerInfo>> GetSeriesTimersAsync(CancellationToken cancellationToken)=>Task.FromResult<IEnumerable<SeriesTimerInfo>>([]);
 public Task<SeriesTimerInfo> GetNewTimerDefaultsAsync(CancellationToken cancellationToken,ProgramInfo? program=null)=>Task.FromResult(new SeriesTimerInfo());
 public Task CancelTimerAsync(string timerId,CancellationToken cancellationToken)=>throw new NotSupportedException("Provider recording is not supported.");
 public Task CancelSeriesTimerAsync(string timerId,CancellationToken cancellationToken)=>throw new NotSupportedException("Provider recording is not supported.");
 public Task CreateTimerAsync(TimerInfo info,CancellationToken cancellationToken)=>throw new NotSupportedException("Provider recording is not supported.");
 public Task CreateSeriesTimerAsync(SeriesTimerInfo info,CancellationToken cancellationToken)=>throw new NotSupportedException("Provider recording is not supported.");
 public Task UpdateTimerAsync(TimerInfo updatedTimer,CancellationToken cancellationToken)=>throw new NotSupportedException("Provider recording is not supported.");
 public Task UpdateSeriesTimerAsync(SeriesTimerInfo info,CancellationToken cancellationToken)=>throw new NotSupportedException("Provider recording is not supported.");
}
