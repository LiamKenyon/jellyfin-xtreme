using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Model.LiveTv;
namespace XtreamBridge;
public sealed class LiveTv : ILiveTvService
{
 private readonly LiveEngine live;
 private readonly GuideEngine guide;
 private readonly Playback playback;
 public LiveTv(LiveEngine? live=null,GuideEngine? guide=null,Playback? playback=null){this.live=live??new();this.guide=guide??new(this.live);this.playback=playback??new(this.live);}
 public string Name => "Xtream Bridge";
 public string HomePageUrl => "";
 public async Task<IEnumerable<ChannelInfo>> GetChannelsAsync(CancellationToken ct)
 {
  var c=Panel.Config;if(!c.EnableLiveTv||c.NativeDvr)return [];
  return (await live.Inventory(ct)).Select(e=>new ChannelInfo{Id=e.Id,Name=e.Name,Number=c.ShowChannelNumbers?e.Number:"",ImageUrl=e.Logo,ChannelGroup=e.Group,Tags=e.Group.Length>0?[e.Group]:[],ChannelType=e.Radio?ChannelType.Radio:ChannelType.TV}).ToList();
 }
 public async Task<IEnumerable<ProgramInfo>> GetProgramsAsync(string channelId,DateTime startDateUtc,DateTime endDateUtc,CancellationToken cancellationToken)
 {
  if(!Panel.Config.EnableLiveTv||!Panel.Config.EnableEpg)return [];
  var entry=(await live.Inventory(cancellationToken)).FirstOrDefault(e=>e.Id==channelId);
  return entry==null?[]:await guide.Programmes(entry,startDateUtc,endDateUtc,cancellationToken);
 }
 public Task<MediaSourceInfo> GetChannelStream(string channelId,string streamId,CancellationToken cancellationToken)=>playback.Selected(channelId,streamId,cancellationToken);

 public Task<List<MediaSourceInfo>> GetChannelStreamMediaSources(string channelId,CancellationToken cancellationToken) => playback.Sources(channelId,cancellationToken);
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
