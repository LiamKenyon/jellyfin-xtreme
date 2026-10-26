using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
namespace XtreamBridge;
public sealed class Settings : BasePluginConfiguration
{
 public string ServerUrl { get; set; } = "";
 public int Port { get; set; }
 public string Username { get; set; } = "";
 public string Password { get; set; } = "";
 public bool EnableLiveTv { get; set; } = true;
 public bool EnableVod { get; set; } = true;
 public bool EnableEpg { get; set; } = true;
 public string LiveFormat { get; set; } = "ts";
 public string[] DisabledLiveCategories { get; set; } = [];
 public string[] DisabledVodCategories { get; set; } = [];
}
public sealed class Plugin : BasePlugin<Settings>, IHasWebPages
{
 public static Plugin Instance { get; private set; } = null!;
 public Plugin(IApplicationPaths paths, IXmlSerializer serializer) : base(paths, serializer) { Instance = this; }
 public override string Name => "Xtream Bridge";
 public override string Description => "Xtream Live TV, automatic XMLTV guide and on-demand movies.";
 public override Guid Id => Guid.Parse("d62c56bb-348d-4ec5-8689-080dc5d3ce68");
 public IEnumerable<PluginPageInfo> GetPages() => [new() { Name = "XtreamBridge", EmbeddedResourcePath = "XtreamBridge.Configuration.config.html" }];
}
