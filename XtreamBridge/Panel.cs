using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using MediaBrowser.Controller.LiveTv;
namespace XtreamBridge;
public sealed class PanelException(string message) : Exception(message);
public static class Panel
{
 // Dedicated client avoids the host's URL logging handlers (URLs contain provider credentials).
 private static readonly HttpClient Client = new(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All, AllowAutoRedirect = false }) { Timeout = TimeSpan.FromMinutes(3), MaxResponseContentBufferSize = 128 * 1024 * 1024 };
 private static readonly System.Collections.Concurrent.ConcurrentDictionary<string,SemaphoreSlim> Gates = new();
 private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (DateTime Until, JsonElement Data)> Cache = new();
 public static Settings Config => Plugin.Instance.Configuration;
 public static bool Ready(Settings c) => !string.IsNullOrWhiteSpace(c.ServerUrl) && c.Username.Length > 0 && c.Password.Length > 0;
 public static string Key(Settings c) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(c))));
 public static Uri Base(Settings c)
 {
  if (!Uri.TryCreate(c.ServerUrl.Trim(), UriKind.Absolute, out var uri) || (uri.Scheme != "https" && uri.Scheme != "http") || uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0) throw new PanelException("Enter an http:// or https:// panel URL without credentials, query or fragment.");
  if (c.Port < 0 || c.Port > 65535) throw new PanelException("Port must be 0 (use URL/default) or 1–65535.");
  var b = new UriBuilder(uri); if (c.Port > 0) b.Port = c.Port; b.Path = b.Path.TrimEnd('/') + "/"; return b.Uri;
 }
 public static string E(string s) => Uri.EscapeDataString(s);
 public static Uri Url(Settings c, string file, string extra = "") => new(Base(c), file + "?username=" + E(c.Username) + "&password=" + E(c.Password) + extra);
 public static string Stream(Settings c, string kind, string id, string extension)
 {
  if (!long.TryParse(id, out var n) || n < 0) throw new PanelException("Invalid stream ID.");
  if (!System.Text.RegularExpressions.Regex.IsMatch(extension, "^[a-zA-Z0-9]{1,8}$")) extension = "mp4";
  return new Uri(Base(c), kind + "/" + E(c.Username) + "/" + E(c.Password) + "/" + id + "." + extension).AbsoluteUri;
 }
 public static string S(JsonElement e, string key) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var v) && v.ValueKind != JsonValueKind.Null ? v.ToString() : "";
 public static IEnumerable<JsonElement> Rows(JsonElement e) => e.ValueKind == JsonValueKind.Array ? e.EnumerateArray().ToArray() : [];
 public static bool Allowed(Settings c, JsonElement row, bool live) => !(live ? c.DisabledLiveCategories : c.DisabledVodCategories).Contains(S(row, "category_id"), StringComparer.Ordinal);
 public static async Task<JsonElement> Api(Settings c, string action, CancellationToken ct, string extra = "", bool fresh = false)
 {
  var key = Key(c);
  var gate=Gates.GetOrAdd(key,_=>new(1,1)); await gate.WaitAsync(ct);
  try {
   foreach(var expired in Cache.Where(x=>x.Value.Until<DateTime.UtcNow).Select(x=>x.Key)) Cache.TryRemove(expired,out _);
   var cacheKey = key + action + extra;
   if (!fresh && Cache.TryGetValue(cacheKey, out var hit) && hit.Until > DateTime.UtcNow) return hit.Data;
   try {
    using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromMinutes(3));
    using var response = await Send(Url(c, "player_api.php", (action.Length > 0 ? "&action=" + E(action) : "") + extra),timeout.Token);
    if (!response.IsSuccessStatusCode) throw new PanelException("Panel request failed (HTTP " + (int)response.StatusCode + ").");
    await response.Content.LoadIntoBufferAsync(128L*1024*1024,timeout.Token);
    using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
    var data = doc.RootElement.Clone();
    if (action.Length > 0 && !action.Contains("info") && data.ValueKind != JsonValueKind.Array) throw new PanelException("Panel returned an unexpected catalogue response. Check credentials and subscription.");
    Cache[cacheKey] = (DateTime.UtcNow.AddMinutes(10), data); return data;
   } catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new PanelException("Panel request timed out."); }
   catch (HttpRequestException) { throw new PanelException("Cannot reach panel. Check URL, port and TLS certificate."); }
   catch (JsonException) { throw new PanelException("Panel returned invalid JSON."); }
  } finally { gate.Release(); }
 }
 public static async Task<HttpResponseMessage> Send(Uri uri,CancellationToken ct)
 {
  for(int attempt=0;;attempt++){
   try {var r=await Client.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,ct);
    if(attempt<2 && ((int)r.StatusCode==429 || (int)r.StatusCode>=500)){r.Dispose();await Task.Delay(TimeSpan.FromSeconds(1<<attempt),ct);continue;}return r;
   }catch(HttpRequestException) when(attempt<2){await Task.Delay(TimeSpan.FromSeconds(1<<attempt),ct);}
  }
 }
 public static async Task Download(Uri uri,string destination,long maxBytes,CancellationToken ct)
 {
  try {
   using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromMinutes(3));
   using var r=await Send(uri,timeout.Token);if(!r.IsSuccessStatusCode)throw new PanelException("Feed request failed (HTTP "+(int)r.StatusCode+").");
   await using var input=await r.Content.ReadAsStreamAsync(timeout.Token);await using var output=File.Create(destination);
   if(!OperatingSystem.IsWindows())File.SetUnixFileMode(destination,UnixFileMode.UserRead|UnixFileMode.UserWrite);
   byte[] buffer=new byte[65536];long size=0;int read;
   while((read=await input.ReadAsync(buffer,timeout.Token))>0){size+=read;if(size>maxBytes)throw new PanelException("Feed exceeds the configured safety bound.");await output.WriteAsync(buffer.AsMemory(0,read),timeout.Token);}
  }catch(HttpRequestException){throw new PanelException("Feed network request failed.");}catch(OperationCanceledException) when(!ct.IsCancellationRequested){throw new PanelException("Feed request timed out.");}
 }
 private static readonly SemaphoreSlim GuideGate = new(1,1);
 private static string GuideKey = "";
 private static DateTime GuideUntil;
 private static Dictionary<string,List<ProgramInfo>> Guide = new();
 public static async Task<Dictionary<string,List<ProgramInfo>>> Epg(Settings c, CancellationToken ct)
 {
  await GuideGate.WaitAsync(ct);
  try {
   var key = Key(c); if (GuideKey == key && GuideUntil > DateTime.UtcNow) return Guide;
   var (rows, _) = await LiveContent.Catalogue(c,ct);
   var mapping = rows.Where(r=>S(r,"epg_channel_id").Length>0).GroupBy(r=>S(r,"epg_channel_id")).ToDictionary(g=>g.Key,g=>g.Select(r=>S(r,"stream_id")).ToArray());
   var next = new Dictionary<string,List<ProgramInfo>>();
   try {
    using var response = await Client.GetAsync(Url(c,"xmltv.php"), HttpCompletionOption.ResponseHeadersRead, ct);
    if (!response.IsSuccessStatusCode) throw new PanelException("XMLTV guide request failed (HTTP " + (int)response.StatusCode + ").");
    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromMinutes(3));
    using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
    // DTDs and external entities are disabled. Bound input size for untrusted providers.
    using var reader = XmlReader.Create(stream,new XmlReaderSettings { Async=true, DtdProcessing=DtdProcessing.Prohibit, XmlResolver=null, MaxCharactersInDocument=256L*1024*1024 });
    while (await reader.ReadAsync().WaitAsync(timeout.Token)) {
     if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "programme") continue;
     using var sub = reader.ReadSubtree(); var el = await XElement.LoadAsync(sub,LoadOptions.None,timeout.Token);
     var epgId = (string?)el.Attribute("channel") ?? "";
     if (!mapping.TryGetValue(epgId,out var ids) || !Date((string?)el.Attribute("start"),out var start) || !Date((string?)el.Attribute("stop"),out var stop) || stop <= start || stop < DateTime.UtcNow.AddDays(-1) || start > DateTime.UtcNow.AddDays(14)) continue;
     foreach(var id in ids) {
      if (!next.TryGetValue(id,out var list)) next[id] = list = [];
      list.Add(new ProgramInfo { Id=id+"_"+start.Ticks,ChannelId=id,Name=(string?)el.Element("title") ?? "Untitled", Overview=(string?)el.Element("desc"),EpisodeTitle=(string?)el.Element("sub-title"),StartDate=start,EndDate=stop,ImageUrl=(string?)el.Element("icon")?.Attribute("src"),Genres=el.Elements("category").Select(x=>x.Value).ToList() });
     }
    }
   } catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new PanelException("Guide request timed out."); }
   catch (HttpRequestException) { throw new PanelException("Cannot fetch the XMLTV guide."); }
   catch (XmlException) { throw new PanelException("Panel returned invalid XMLTV guide data."); }
   Guide=next; GuideKey=key; GuideUntil=DateTime.UtcNow.AddHours(4); return Guide;
  } finally { GuideGate.Release(); }
 }
 public static bool Date(string? value,out DateTime date)
 {
  date=default; if (string.IsNullOrWhiteSpace(value)) return false;
  var s=value.Trim(); if (s.Length==14) s += " +0000";
  if (s.Length==20) s=s.Insert(18,":");
  if (!DateTimeOffset.TryParseExact(s,"yyyyMMddHHmmss zzz",CultureInfo.InvariantCulture,DateTimeStyles.None,out var dto)) return false;
  date=dto.UtcDateTime;return true;
 }
 public static void Clear() { GuideUntil=DateTime.MinValue; /* catalogue refresh uses fresh=true */ }
}
