using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
namespace XtreamBridge;
public sealed class Settings : BasePluginConfiguration
{
 public ProviderSettings[] Providers { get; set; } = [];
 public ChannelRule[] ChannelRules { get; set; } = [];
 public CategoryRule[] CategoryRules { get; set; } = [];
 public bool AutoEnableNewCategories { get; set; } = false;
 public int CatalogueRefreshMinutes { get; set; } = 360;
 public int GuideRefreshMinutes { get; set; } = 240;
 public int RemovalThresholdPercent { get; set; } = 20;
 public bool EnableCollections { get; set; } = false;
 public bool EnableCatchup { get; set; } = false;
 public bool NativeDvr { get; set; } = false;
 public string AlternativeEpgUrl { get; set; } = "";
 public int ConnectionLimit { get; set; } = 1;
 public int ArchiveOffsetMinutes { get; set; }
 [System.Xml.Serialization.XmlIgnore] public string ProviderId { get; set; } = "primary";
 public string ServerUrl { get; set; } = "";
 public int Port { get; set; }
 public string Username { get; set; } = "";
 public string Password { get; set; } = "";
 public bool EnableLiveTv { get; set; } = true;
 public bool EnableVod { get; set; } = false;
 public bool EnableEpg { get; set; } = true;
 public bool ShowChannelNumbers { get; set; } = false;
 public bool HideOnDemandCategories { get; set; } = true;
 public string LiveFormat { get; set; } = "ts";
 public string[] DisabledLiveCategories { get; set; } = [];
 public string[] DisabledVodCategories { get; set; } = [];
}
public sealed class Plugin : BasePlugin<Settings>, IHasWebPages
{
 public static Plugin Instance { get; private set; } = null!;
 public Plugin(IApplicationPaths paths, IXmlSerializer serializer) : base(paths, serializer) { Instance = this; }
 public string StoragePath => System.IO.Path.Combine(ApplicationPaths.DataPath,"xtream-bridge");
 public override string Name => "Xtream Bridge";
 public override string Description => "Xtream Live TV, automatic XMLTV guide and on-demand movies.";
 public override Guid Id => Guid.Parse("d62c56bb-348d-4ec5-8689-080dc5d3ce68");
 public IEnumerable<PluginPageInfo> GetPages() => [new() { Name = "XtreamBridge", EmbeddedResourcePath = "XtreamBridge.Configuration.config.html" }];
}
