using System.Text.Json;
using System.Text.RegularExpressions;
namespace XtreamBridge;
/// <summary>Shared eligibility rules for channel listing, guide mapping and playback.</summary>
public static class LiveContent
{
 public static bool IsLive(Settings c, JsonElement row, string category)
 {
  if (!Panel.Allowed(c,row,true) || !long.TryParse(Panel.S(row,"stream_id"),out var id) || id < 0) return false;
  var type=Panel.S(row,"stream_type").Trim().ToLowerInvariant();
  // Some panels omit stream_type; retain the live endpoint's rows unless other
  // explicit metadata identifies on-demand content. Never guess from the title.
  if (type.Length > 0 && type is not ("live" or "radio" or "live_radio")) return false;
  foreach(var field in new[]{"direct_source","stream_url","url"}) {
   if (Uri.TryCreate(Panel.S(row,field),UriKind.Absolute,out var uri) &&
       Regex.IsMatch(uri.AbsolutePath,@"/(movie|series|vod)/",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)) return false;
  }
  if (c.HideOnDemandCategories && Regex.IsMatch(category,@"\b(vod|series|tv\s*shows?|on[\s-]*demand)\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)) return false;
  return true;
 }
 public static string Name(JsonElement row)
 {
  var name=Regex.Replace(Panel.S(row,"name"),@"\s+"," ").Trim();
  // Remove only long, known provider IDs/indexes, not meaningful numbers in names
  // such as Channel 4, 5 Star, 1000-lb Sisters or 24/7.
  foreach(var field in new[]{"stream_id","num"}) {
   var prefix=Panel.S(row,field);
   if (prefix.Length < 4 || !prefix.All(char.IsAsciiDigit)) continue;
   var cleaned=Regex.Replace(name,@"^"+Regex.Escape(prefix)+@"(?:\s*[-:|]\s+|\s+)","").Trim();
   if(cleaned.Length>0) name=cleaned;
  }
  return name;
 }
 public static string Number(Settings c,JsonElement row) => c.ShowChannelNumbers ? Panel.S(row,"channel_number") : "";
 public static async Task<(JsonElement[] Rows,Dictionary<string,string> Categories)> Catalogue(Settings c,CancellationToken ct)
 {
  var categories=Panel.Rows(await Panel.Api(c,"get_live_categories",ct)).GroupBy(r=>Panel.S(r,"category_id")).ToDictionary(g=>g.Key,g=>Panel.S(g.First(),"category_name"));
  var rows=Panel.Rows(await Panel.Api(c,"get_live_streams",ct)).Where(r=>IsLive(c,r,categories.GetValueOrDefault(Panel.S(r,"category_id"),""))).ToArray();
  return (rows,categories);
 }
}
