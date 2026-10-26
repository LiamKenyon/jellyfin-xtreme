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
  serviceCollection.AddSingleton<ILiveTvService, LiveTv>();
  serviceCollection.AddSingleton<IChannel, Vod>();
 }
}
