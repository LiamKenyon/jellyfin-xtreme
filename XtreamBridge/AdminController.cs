using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace XtreamBridge;
[ApiController]
[Route("XtreamBridge")]
[Authorize(Policy="RequiresElevation")]
public sealed class AdminController : ControllerBase
{
 [HttpPost("Discover")]
 public async Task<IActionResult> Discover(CancellationToken ct)
 {
  try {
   var c=Panel.Config;
   if(!Panel.Ready(c)) return BadRequest(new { Message="Save server URL, username and password first." });
   var account=await Panel.Api(c,"",ct,fresh:true);
   if (!account.TryGetProperty("user_info",out var user) || Panel.S(user,"auth")!="1" || !Panel.S(user,"status").Equals("Active",StringComparison.OrdinalIgnoreCase)) return BadRequest(new { Message="Panel rejected credentials or subscription is inactive." });
   var live=await Panel.Api(c,"get_live_categories",ct,fresh:true);
   var vod=await Panel.Api(c,"get_vod_categories",ct,fresh:true);
   if(c.EnableLiveTv) await Panel.Api(c,"get_live_streams",ct,fresh:true);
   if(c.EnableVod) await Panel.Api(c,"get_vod_streams",ct,fresh:true);
   Panel.Clear();
   object[] Categories(System.Text.Json.JsonElement data)=>Panel.Rows(data).Select(r=>(object)new { Id=Panel.S(r,"category_id"),Name=Panel.S(r,"category_name") }).ToArray();
   return Ok(new { Live=Categories(live),Vod=Categories(vod),Message="Connected. Categories loaded." });
  } catch(PanelException ex) { return BadRequest(new { Message=ex.Message }); }
 }
}
