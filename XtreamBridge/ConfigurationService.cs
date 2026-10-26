using System.Text.Json;
using System.Text.RegularExpressions;
namespace XtreamBridge;
public static class ConfigurationService
{
 public static Settings Redacted(Settings c)
 {
  var copy=JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(c))!;copy.Password="";foreach(var p in copy.Providers)p.Password="";return copy;
 }
 public static void Save(Settings incoming)
 {
  Validate(incoming);
  var old=Panel.Config;
  if(string.IsNullOrEmpty(incoming.Password))incoming.Password=old.Password;
  foreach(var p in incoming.Providers){if(string.IsNullOrEmpty(p.Password))p.Password=old.Providers.FirstOrDefault(x=>x.Id==p.Id)?.Password??"";}
  Validate(incoming);Plugin.Instance.UpdateConfiguration(incoming);
 }
 public static void Validate(Settings c)
 {
  if(c==null)throw new PanelException("Configuration is required.");
  RequireText(c);
  if(c.Providers==null||c.ChannelRules==null||c.CategoryRules==null||c.DisabledLiveCategories==null||c.DisabledVodCategories==null)throw new PanelException("Configuration arrays cannot be null.");
  if(c.Providers.Length>10||c.ChannelRules.Length>100000||c.CategoryRules.Length>20000)throw new PanelException("Configuration exceeds supported bounds.");
  if(c.CatalogueRefreshMinutes<15||c.CatalogueRefreshMinutes>10080||c.GuideRefreshMinutes<15||c.GuideRefreshMinutes>1440||c.RemovalThresholdPercent<1||c.RemovalThresholdPercent>100)throw new PanelException("Refresh interval or removal threshold outside supported range.");
  if(c.ConnectionLimit<1||c.ConnectionLimit>100||Math.Abs((long)c.ArchiveOffsetMinutes)>1440)throw new PanelException("Invalid connection limit or archive offset.");
  if(c.ServerUrl.Length>0)Panel.Base(c);
  var ids=new HashSet<string>();
  foreach(var p in c.Providers){RequireText(p);if(!Regex.IsMatch(p.Id,"^[a-zA-Z0-9_-]{1,64}$")||p.Id=="primary"||!ids.Add(p.Id))throw new PanelException("Provider IDs must be unique and contain only letters, numbers, dashes or underscores.");if(p.ConnectionLimit<1||p.ConnectionLimit>100||Math.Abs((long)p.ArchiveOffsetMinutes)>1440)throw new PanelException("Invalid provider limits.");Panel.Base(new Settings{ServerUrl=p.ServerUrl,Port=p.Port});}
  if(c.AlternativeEpgUrl.Length>0 && (!Uri.TryCreate(c.AlternativeEpgUrl,UriKind.Absolute,out var uri)||uri.Scheme is not ("http" or "https")||uri.UserInfo.Length>0))throw new PanelException("Alternative EPG must be an HTTP(S) URL without embedded user information.");
  ids.Clear();foreach(var r in c.ChannelRules){RequireText(r);if(string.IsNullOrWhiteSpace(r.Id)||!ids.Add(r.Id))throw new PanelException("Channel override IDs must be unique.");if(r.Format is not ("" or "ts" or "m3u8")||r.GuideSource is not ("provider" or "alternative")||Math.Abs((long)r.OffsetMinutes)>1440)throw new PanelException("Invalid channel playback or guide override.");if(r.Logo.Length>0 && LiveEngine.SafeImage(r.Logo).Length==0)throw new PanelException("Logo overrides must be HTTP(S) URLs without credentials or query strings.");if(r.Name.Length>300||r.Group.Length>200||r.EpgId.Length>300)throw new PanelException("Override text is too long.");}
  ids.Clear();foreach(var r in c.CategoryRules){RequireText(r);if(!ids.Add(r.ProviderId+"|"+r.CategoryId))throw new PanelException("Duplicate category rule.");}
 }
 private static void RequireText(object? value){if(value==null||value.GetType().GetProperties().Any(p=>p.PropertyType==typeof(string)&&p.GetValue(value)==null))throw new PanelException("Configuration text and rule objects cannot be null.");}
}
public sealed class RuleExport
{
 public int Schema { get; set; }=1;
 public ChannelRule[] Channels { get; set; }=[];
 public CategoryRule[] Categories { get; set; }=[];
}
