using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Controller.Channels;
using Microsoft.AspNetCore.Authorization;
using XtreamBridge;
using Microsoft.Extensions.DependencyInjection;
using MediaBrowser.Controller.LiveTv;
int passed=0;
void Check(bool ok,string name) { if(!ok) throw new Exception("FAIL: "+name);Console.WriteLine("PASS: "+name);passed++; }
async Task Reject(Func<Task> action,string name) { try{await action();}catch(PanelException){Check(true,name);return;}throw new Exception("FAIL: "+name); }
var tmp=Path.Combine(Path.GetTempPath(),"xtream-tests-"+Guid.NewGuid());Directory.CreateDirectory(tmp);
var paths=DispatchProxy.Create<IApplicationPaths,Fake>();((Fake)(object)paths).Directory=tmp;
var serializer=DispatchProxy.Create<IXmlSerializer,Fake>();
var plugin=new Plugin(paths,serializer);
plugin.UpdateConfiguration(new Settings());
var c=plugin.Configuration;
var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();int port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();
using var http=new HttpListener();http.Prefixes.Add($"http://127.0.0.1:{port}/");http.Start();
using var stop=new CancellationTokenSource();int guideHits=0;int apiHits=0;string guideMode="normal";
var start=DateTime.UtcNow.Date.AddHours(12);var end=start.AddHours(1);
var server=Task.Run(async()=>{
 while(!stop.IsCancellationRequested) {
  HttpListenerContext context;try{context=await http.GetContextAsync().WaitAsync(stop.Token);}catch(OperationCanceledException){break;}
  var req=context.Request;string data;
  if(req.Url!.AbsolutePath=="/xmltv.php") {
   Interlocked.Increment(ref guideHits);
   data=guideMode=="dtd" ? "<!DOCTYPE tv [<!ENTITY x SYSTEM 'file:///etc/passwd'>]><tv>&x;</tv>" : $"<tv><programme channel='guide.one' start='{start:yyyyMMddHHmmss} +0000' stop='{end:yyyyMMddHHmmss} +0000'><title>First &amp; Second</title><desc>Details</desc></programme><programme channel='guide.one' start='{end:yyyyMMddHHmmss} +0000' stop='{end.AddHours(1):yyyyMMddHHmmss} +0000'><title>Next show</title></programme><programme channel='guide.two' start='{start:yyyyMMddHHmmss} +0000' stop='{end:yyyyMMddHHmmss} +0000'><title>Hidden show</title></programme></tv>";
  } else {
   Interlocked.Increment(ref apiHits);
   data=req.QueryString["action"] switch {
    "get_live_categories"=>"[{\"category_id\":1,\"category_name\":\"UK\"},{\"category_id\":\"2\",\"category_name\":\"Other\"}]",
    "get_vod_categories"=>"[{\"category_id\":\"3\",\"category_name\":\"Movies\"},{\"category_id\":\"4\",\"category_name\":\"Hidden\"}]",
    "get_live_streams"=>"[{\"stream_id\":101,\"name\":\"Channel One\",\"num\":1,\"category_id\":\"1\",\"epg_channel_id\":\"guide.one\"},{\"stream_id\":102,\"name\":\"Channel Two\",\"category_id\":\"2\",\"epg_channel_id\":\"guide.two\"}]",
    "get_vod_streams"=>"[{\"stream_id\":201,\"name\":\"Movie\",\"category_id\":\"3\",\"container_extension\":\"mkv\"},{\"stream_id\":202,\"name\":\"Hidden\",\"category_id\":\"4\",\"container_extension\":\"mp4\"}]",
    "bad"=>"<html>Error</html>","wrong"=>"{}",
    _=>"{\"user_info\":{\"auth\":1,\"status\":\"Active\"}}"
   };
  }
  var bytes=Encoding.UTF8.GetBytes(data);context.Response.ContentLength64=bytes.Length;await context.Response.OutputStream.WriteAsync(bytes);context.Response.Close();
 }
});
try {
 c.ServerUrl="http://127.0.0.1";c.Port=port;c.Username="user+name";c.Password="p&ss word";c.DisabledLiveCategories=["2"];c.DisabledVodCategories=["4"];
 Check(Panel.Base(c).Port==port,"Separate port overrides URL");
 Check(Panel.Url(c,"player_api.php").Query.Contains("p%26ss%20word"),"Query credentials are encoded");
 Check(Panel.Stream(c,"movie","201","mkv").Contains("user%2Bname/p%26ss%20word/201.mkv"),"Path credentials are encoded");
 await Reject(()=>Task.FromResult(Panel.Stream(c,"movie","../x","mkv")),"Reject stream path injection");
 Check(Panel.Date("20261025130000 +0100",out var date) && date.Hour==12 && date.Kind==DateTimeKind.Utc,"XMLTV positive offset converted to UTC");
 Check(Panel.Date("20261025130000 -0530",out date) && date.Hour==18 && date.Minute==30,"XMLTV negative half-hour offset");
 Check(Panel.Date("20261025130000",out date) && date.Hour==13,"XMLTV missing timezone defaults UTC");
 Check(!Panel.Date("invalid",out _),"Malformed XMLTV date skipped");
 var live=new LiveTv();var channels=(await live.GetChannelsAsync(default)).ToList();
 Check(channels.Count==1 && channels[0].Id=="101" && channels[0].ChannelGroup=="UK","Live category filtering with numeric/string IDs");
 var before=apiHits;await live.GetChannelsAsync(default);Check(apiHits==before,"Catalogue requests cached");
 var source=await live.GetChannelStream("101","",default);Check(source.Path.EndsWith("101.ts") && source.IsInfiniteStream && !source.SupportsDirectPlay,"Live playback source");
 await Reject(()=>live.GetChannelStream("102","",default),"Disabled live channel cannot start playback");
 c.LiveFormat="m3u8";source=await live.GetChannelStream("101","",default);Check(source.Path.EndsWith("101.m3u8"),"HLS selection");
 var guide=(await live.GetProgramsAsync("101",start,end.AddHours(1),default)).ToList();
 Check(guide.Count==2 && guide[0].Name=="First & Second","XMLTV imports consecutive programmes and decodes XML entities");
 Check(!(await live.GetProgramsAsync("102",start,end,default)).Any(),"Guide excludes disabled categories");
 Check(guideHits==1,"Full XMLTV feed fetched once across channels");
 Check((await live.GetProgramsAsync("101",start.AddMinutes(30),end,default)).Count()==1,"Programme overlap filtering");
 c.EnableEpg=false;Check(!(await live.GetProgramsAsync("101",start,end,default)).Any(),"EPG off toggle");c.EnableEpg=true;
 c.EnableLiveTv=false;Check(!(await live.GetChannelsAsync(default)).Any(),"Live TV off toggle");c.EnableLiveTv=true;
 var vod=new Vod();var folders=await vod.GetChannelItems(new(),default);Check(folders.Items.Count==1,"VOD category filtering");
 var movies=await vod.GetChannelItems(new(){FolderId="cat:3"},default);Check(movies.Items.Count==1 && movies.Items[0].Id=="movie:201","VOD category browsing");
 var media=(await vod.GetChannelItemMediaInfo("movie:201",default)).Single();Check(media.Path.EndsWith("201.mkv"),"VOD container and playback URL");
 await Reject(()=>vod.GetChannelItemMediaInfo("movie:202",default),"Disabled VOD cannot start playback");
 c.EnableVod=false;Check((await vod.GetChannelItems(new(),default)).Items.Count==0,"VOD off toggle");await Reject(()=>vod.GetChannelItemMediaInfo("movie:201",default),"VOD off blocks playback");c.EnableVod=true;
 var paged=await vod.GetChannelItems(new(){FolderId="cat:3",StartIndex=1,Limit=10},default);Check(paged.Items.Count==0 && paged.TotalRecordCount==1,"VOD pagination total");
 c.Password="changed";media=(await vod.GetChannelItemMediaInfo("movie:201",default)).Single();Check(media.Path.Contains("/changed/"),"Playback uses changed credentials");
 await Reject(()=>Panel.Api(c,"bad",default),"Malformed panel JSON produces sanitised error");
 await Reject(()=>Panel.Api(c,"wrong",default),"Wrong catalogue shape rejected");
 guideMode="dtd";Panel.Clear();await Reject(()=>Panel.Epg(c,default),"External XML entities rejected");
 Check(typeof(AdminController).GetCustomAttribute<AuthorizeAttribute>()?.Policy=="RequiresElevation","Management endpoint requires administrator policy");
 Check(typeof(Plugin).Assembly.GetManifestResourceNames().Contains("XtreamBridge.Configuration.config.html"),"Configuration page embedded");
 var services=new ServiceCollection();new Services().RegisterServices(services,null!);
 using var provider=services.BuildServiceProvider();
 Check(provider.GetServices<ILiveTvService>().Single() is LiveTv,"Live TV registered with Jellyfin DI");
 Check(provider.GetServices<IChannel>().Single() is Vod,"VOD registered with Jellyfin DI");
 var admin=await new AdminController().Discover(default);
 Check(admin is Microsoft.AspNetCore.Mvc.OkObjectResult,"Admin connection discovery succeeds with mock account");
 Console.WriteLine($"{passed} checks passed.");
} finally { stop.Cancel();await server;http.Stop();Directory.Delete(tmp,true); }
public class Fake : DispatchProxy {
 public string Directory="";
 protected override object? Invoke(MethodInfo? method,object?[]? args) => method!.ReturnType==typeof(string) ? Directory : method.ReturnType==typeof(bool) ? true : null;
}
