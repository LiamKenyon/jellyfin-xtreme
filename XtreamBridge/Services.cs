using MediaBrowser.Controller;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;
namespace XtreamBridge;
public sealed class Services : IPluginServiceRegistrator
{
 public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
 {
  serviceCollection.AddSingleton<LiveEngine>();
  serviceCollection.AddSingleton<GuideEngine>();
  serviceCollection.AddSingleton<Playback>();
  serviceCollection.AddSingleton<NativeDvr>();
  serviceCollection.AddSingleton<Groups>();
  serviceCollection.AddSingleton<SyncService>();
  serviceCollection.AddSingleton<SyncQueue>();
  serviceCollection.AddSingleton<Microsoft.Extensions.Hosting.IHostedService>(p=>p.GetRequiredService<SyncQueue>());
  serviceCollection.AddSingleton<ILiveTvService, LiveTv>();
  serviceCollection.AddSingleton<IChannel, Vod>();
  serviceCollection.AddSingleton<IChannel, Catchup>();
 }
}
