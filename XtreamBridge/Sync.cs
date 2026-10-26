using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Hosting;
namespace XtreamBridge;
public sealed class SyncState
{
 public bool Running { get; set; }
 public string Stage { get; set; }="Idle";
 public string Error { get; set; }="";
 public DateTime? LastComplete { get; set; }
 public double Progress { get; set; }
}
public sealed class SyncService(LiveEngine live,GuideEngine guide,NativeDvr dvr,Groups groups,ITaskManager tasks)
{
 private readonly SemaphoreSlim gate=new(1,1);
 public SyncState State { get; }=new();
 public async Task Run(bool approve,CancellationToken ct)
 {
  if(!await gate.WaitAsync(0,ct))return;
  State.Running=true;State.Error="";State.Progress=0;
  try{
   State.Stage="Importing provider catalogues";if(Panel.Config.EnableLiveTv)await live.SyncAll(approve,ct);State.Progress=30;
   State.Stage="Refreshing programme guide";if(Panel.Config.EnableLiveTv&&Panel.Config.EnableEpg)await guide.Refresh(ct);State.Progress=55;
   State.Stage="Updating managed DVR tuners";await dvr.Apply(ct);State.Progress=65;
   var worker=tasks.ScheduledTasks.FirstOrDefault(w=>w.ScheduledTask.Key.Equals("RefreshGuide",StringComparison.OrdinalIgnoreCase));
   if(worker!=null){State.Stage="Refreshing Jellyfin Live TV";await tasks.Execute(worker,new TaskOptions());}
   else State.Error="Jellyfin Refresh Guide task not found; run it manually.";
   State.Progress=90;State.Stage="Updating category collections";await groups.Apply(ct);State.Progress=100;State.LastComplete=DateTime.UtcNow;
   State.Stage=live.Status.Any(s=>s.State is not ("Ready" or "Cached"))||guide.Errors.Count>0?"Completed with provider warnings":"Complete";
  }catch(OperationCanceledException){State.Stage="Cancelled";throw;}catch(Exception){State.Stage="Failed";State.Error="Sync failed. Review provider status and Jellyfin task logs; no credentials are included here.";}
  finally{State.Running=false;gate.Release();}
 }
}
public sealed class SyncTask(SyncService sync):IScheduledTask
{
 public string Name=>"Sync Xtream Live TV";public string Key=>"XtreamLiveSync";public string Description=>"Refresh providers, guides, managed tuners and category collections.";public string Category=>"Live TV";
 public Task ExecuteAsync(IProgress<double> progress,CancellationToken cancellationToken)=>sync.Run(false,cancellationToken);
 public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()=>[new(){Type=TaskTriggerInfoType.IntervalTrigger,IntervalTicks=TimeSpan.FromMinutes(Panel.Config.CatalogueRefreshMinutes).Ticks}];
}
// A single bounded in-process job. Request disconnects do not terminate a saved sync.
public sealed class SyncQueue(SyncService sync):BackgroundService
{
 private readonly System.Threading.Channels.Channel<bool> jobs=System.Threading.Channels.Channel.CreateBounded<bool>(1);
 public bool Enqueue(bool approve)=>jobs.Writer.TryWrite(approve);
 protected override async Task ExecuteAsync(CancellationToken stoppingToken){await foreach(var approve in jobs.Reader.ReadAllAsync(stoppingToken))await sync.Run(approve,stoppingToken);}
}
