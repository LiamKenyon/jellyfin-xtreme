using System.Diagnostics;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.MediaInfo;
namespace XtreamBridge;
public sealed class Playback(LiveEngine live)
{
 public async Task<MediaSourceInfo> Source(string id,CancellationToken ct)
 {
  if(!Panel.Config.EnableLiveTv)throw new PanelException("Live TV is disabled.");
  var e=await live.Find(id,ct);var p=Providers.All(Panel.Config).First(x=>x.Id==e.ProviderId);
  var rule=Panel.Config.ChannelRules.FirstOrDefault(r=>r.Id==id);var format=rule?.Format.Length>0?rule.Format:Panel.Config.LiveFormat;format=format=="m3u8"?"m3u8":"ts";
  return new MediaSourceInfo{Id="xtream-live-"+id,Path=Panel.Stream(p.Config,"live",e.StreamId,format),Protocol=MediaProtocol.Http,IsRemote=true,Container=format,SupportsDirectPlay=false,SupportsDirectStream=true,SupportsTranscoding=true,IsInfiniteStream=true};
 }
 public async Task<List<MediaSourceInfo>> Sources(string id,CancellationToken ct)
 {
  var primary=await Source(id,ct);primary.Name="Primary";var sources=new List<MediaSourceInfo>{primary};
  var backup=Panel.Config.ChannelRules.FirstOrDefault(r=>r.Id==id)?.BackupId;
  if(!string.IsNullOrWhiteSpace(backup)&&backup!=id){try{var alternative=await Source(backup,ct);alternative.Name="Alternative";sources.Add(alternative);}catch(PanelException){}}
  return sources;
 }
 public async Task<MediaSourceInfo> Selected(string id,string streamId,CancellationToken ct)
 {
  var sources=await Sources(id,ct);return sources.FirstOrDefault(s=>s.Id==streamId)??sources[0];
 }
 public async Task<object> Probe(string id,CancellationToken ct)
 {
  var watch=Stopwatch.StartNew();var source=await Source(id,ct);
  try{using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(12));using var r=await Panel.Send(new Uri(source.Path),timeout.Token);await using var stream=await r.Content.ReadAsStreamAsync(timeout.Token);byte[] buffer=new byte[4096];int count=r.IsSuccessStatusCode?await stream.ReadAsync(buffer,timeout.Token):0;
   return new{Success=r.IsSuccessStatusCode&&count>0,HttpStatus=(int)r.StatusCode,Milliseconds=watch.ElapsedMilliseconds,BytesRead=count,Note="Short network probe only; not a codec or full playback test. Uses a provider connection briefly."};
  }catch(Exception e) when(e is HttpRequestException or OperationCanceledException or IOException){return new{Success=false,Milliseconds=watch.ElapsedMilliseconds,Note="Stream unavailable or timed out. No credential-bearing URL is included."};}
 }
 public async Task<MediaSourceInfo> Archive(string id,DateTime start,DateTime end,CancellationToken ct)
 {
  if(!Panel.Config.EnableCatchup)throw new PanelException("Catch-up disabled.");
  var e=await live.Find(id,ct);var p=Providers.All(Panel.Config).First(x=>x.Id==e.ProviderId);
  var now=DateTime.UtcNow;if(e.ArchiveDays==0||start<now.AddDays(-e.ArchiveDays)||start>=now||end<=start||end-start>TimeSpan.FromHours(12))throw new PanelException("Programme outside provider archive window.");
  var duration=(int)Math.Ceiling((new[]{end,now}.Min()-start).TotalMinutes);
  var date=start.AddMinutes(p.ArchiveOffset).ToString("yyyy-MM-dd:HH-mm",System.Globalization.CultureInfo.InvariantCulture);
  var path=new Uri(Panel.Base(p.Config),$"timeshift/{Panel.E(p.Config.Username)}/{Panel.E(p.Config.Password)}/{duration}/{date}/{e.StreamId}.ts").AbsoluteUri;
  return new MediaSourceInfo{Id="archive-"+id+"-"+start.Ticks,Path=path,Protocol=MediaProtocol.Http,IsRemote=true,Container="ts",RunTimeTicks=TimeSpan.FromMinutes(duration).Ticks,SupportsDirectPlay=false,SupportsDirectStream=true,SupportsTranscoding=true};
 }
}
