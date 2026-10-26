using System.Text.Json;
namespace XtreamBridge;
public static class Store
{
 private static readonly object Gate=new();
 public static void Save<T>(string name,T data)
 {
  lock(Gate){var dir=Plugin.Instance.StoragePath;Directory.CreateDirectory(dir);var path=Path.Combine(dir,name);var temp=path+".tmp";
   File.WriteAllText(temp,JsonSerializer.Serialize(data));
   if(!OperatingSystem.IsWindows())File.SetUnixFileMode(temp,UnixFileMode.UserRead|UnixFileMode.UserWrite);
   File.Move(temp,path,true);
  }
 }
 public static T? Read<T>(string name)
 {
  lock(Gate){try{var path=Path.Combine(Plugin.Instance.StoragePath,name);return File.Exists(path)?JsonSerializer.Deserialize<T>(File.ReadAllText(path)):default;}catch(JsonException){return default;}catch(IOException){return default;}}
 }
}
public sealed class LiveEntry
{
 public string Id { get; set; }="";
 public string StreamId { get; set; }="";
 public string ProviderId { get; set; }="primary";
 public string Name { get; set; }="";
 public string CategoryId { get; set; }="";
 public string Group { get; set; }="";
 public string Number { get; set; }="";
 public string Logo { get; set; }="";
 public string EpgId { get; set; }="";
 public bool Radio { get; set; }
 public int ArchiveDays { get; set; }
}
public sealed class LiveSnapshot
{
 public string Identity { get; set; }="";
 public DateTime Updated { get; set; }
 public List<LiveEntry> Entries { get; set; }=[];
 public Dictionary<string,string> Categories { get; set; }=[];
 public string[] AcceptedCategories { get; set; }=[];
}
public sealed class ProviderStatus
{
 public string Id { get; set; }="";
 public string Name { get; set; }="";
 public string State { get; set; }="Not synced";
 public string Error { get; set; }="";
 public DateTime? LastSuccess { get; set; }
 public DateTime? LastAttempt { get; set; }
 public int Imported { get; set; }
 public int Added { get; set; }
 public int Removed { get; set; }
 public int Renamed { get; set; }
 public string[] PendingCategories { get; set; }=[];
 public string AccountStatus { get; set; }="Unknown";
 public string Expiry { get; set; }="";
 public string ReportedConnections { get; set; }="";
 public string ReportedLimit { get; set; }="";
}
