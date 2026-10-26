using System.Text.Json;
namespace XtreamBridge;
public sealed class ProviderSettings
{
 public string Id { get; set; } = Guid.NewGuid().ToString("N");
 public string Name { get; set; } = "Additional provider";
 public bool Enabled { get; set; } = true;
 public string ServerUrl { get; set; } = "";
 public int Port { get; set; }
 public string Username { get; set; } = "";
 public string Password { get; set; } = "";
 public int ConnectionLimit { get; set; } = 1;
 public int ArchiveOffsetMinutes { get; set; }
}
public sealed class ChannelRule
{
 public string Id { get; set; } = "";
 public bool Enabled { get; set; } = true;
 public string Name { get; set; } = "";
 public string Group { get; set; } = "";
 public string Number { get; set; } = "";
 public string Logo { get; set; } = "";
 public string EpgId { get; set; } = "";
 public string GuideSource { get; set; } = "provider";
 public int OffsetMinutes { get; set; }
 public string Format { get; set; } = "";
 public string BackupId { get; set; } = "";
 public string Rating { get; set; } = "";
}
public sealed class CategoryRule
{
 public string ProviderId { get; set; } = "primary";
 public string CategoryId { get; set; } = "";
 public bool Enabled { get; set; } = true;
 public string Group { get; set; } = "";
}
public static class Providers
{
 public static IEnumerable<(string Id,string Name,Settings Config,int Limit,int ArchiveOffset)> All(Settings root)
 {
  if(Panel.Ready(root)) yield return ("primary","Primary provider",root,root.ConnectionLimit,root.ArchiveOffsetMinutes);
  foreach(var p in root.Providers.Where(p=>p.Enabled)) {
   var c=JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(root))!;
   c.ServerUrl=p.ServerUrl;c.Port=p.Port;c.Username=p.Username;c.Password=p.Password;c.ProviderId=p.Id;c.DisabledLiveCategories=[];
   if(Panel.Ready(c))yield return(p.Id,p.Name,c,p.ConnectionLimit,p.ArchiveOffsetMinutes);
  }
 }
 public static string ChannelId(string provider,string id)=>provider=="primary"?id:provider+":"+id;
 public static string Identity(Settings c)=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Panel.Base(c).AbsoluteUri+"\n"+c.Username)));
}
