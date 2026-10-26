# Xtream Bridge 1.0.0

Jellyfin plugin for an Xtream-compatible IPTV panel. Built against **Jellyfin 12.3.0 / .NET 10**. This is a first release with a compiled DLL and automated mock-panel tests; it has not been exercised in a running Jellyfin server or with a real IPTV subscription. It is not a 10.10/10.11-compatible build.

## Included

- Server URL, optional separate port, username and password in Jellyfin's plugin settings.
- Independent Live TV, VOD movies and EPG switches.
- Separate category checkboxes for Live TV and VOD; text filtering and bulk enable/disable of visible categories. New categories default to enabled.
- Live TV channels, names, numbers, groups and channel logos from `player_api.php`.
- TS and HLS live stream options; Jellyfin handles playback/remux/transcoding.
- VOD movie browsing under **Xtream Movies**, grouped by provider category, with artwork and dynamic playback URLs.
- Automatic `xmltv.php` guide retrieval using the same credentials. Matching uses each stream's `epg_channel_id`; no separate EPG URL or manual mapping is needed.
- Guide imports honour timezone offsets, retain overlapping programmes, cache the feed for four hours, and filter out disabled live categories.
- Admin-only connection testing/category discovery, credential-safe panel error messages, cancellation, input bounds and XML external-entity protection.

## Install the compiled plugin

1. Check **Dashboard → General** (or the server About page) shows Jellyfin **12.3**. Back up the server configuration before adding a new plugin.
2. Stop Jellyfin.
3. Copy the entire `install/XtreamBridge_1.0.0.0` folder from this bundle into your Jellyfin **plugins** directory. It contains the DLL and `meta.json`. Do not copy the source or any Jellyfin framework DLLs into plugins.
   - Official Docker image: normally `/config/plugins/XtreamBridge_1.0.0.0/` inside the container. Copy into the matching host-mounted configuration directory.
   - Typical Linux package: `/var/lib/jellyfin/plugins/XtreamBridge_1.0.0.0/`.
   - Windows: use the plugins directory under the server's actual data directory; the location depends on how Jellyfin was installed.
4. Ensure the Jellyfin service user can read the folder, then start Jellyfin.
5. Open **Dashboard → Plugins → My Plugins → Xtream Bridge → Settings**. Hard refresh the browser if the page is stale.
6. Enter the base panel URL, for example `https://panel.example.com`. Do not paste an M3U URL, `player_api.php`, or `get.php`. A panel installation subdirectory is allowed. Enter a separate port, or leave `0` to use the URL/default port.
7. Enter your username/password, select the desired content types and click **Save, test connection & load categories**.
8. Select categories, then click **Save settings**.
9. Run **Refresh Guide** and **Refresh Internet Channels** in **Dashboard → Scheduled Tasks**. Jellyfin's scheduled guide refresh calls the plugin automatically thereafter. Adjust the task schedule in Jellyfin if required.
10. Open **Live TV** for channels/guide; open **Xtream Movies** for VOD. Depending on the client, internet channels appear under Channels, My Media or the home screen. Give users the appropriate Jellyfin Live TV/library permissions.

Do not also add the same account as an M3U tuner unless you intentionally want duplicate channels. This plugin registers a native Live TV service.

## Important behaviour and limits

- **VOD means movies in this release.** Xtream series/seasons/episodes, catch-up and provider recording are not implemented. The service does not advertise or implement recording timers.
- No subscription is included. The panel must expose the standard Xtream player API and XMLTV endpoint. A panel with missing or incorrect `epg_channel_id` values cannot be mapped automatically; missing programme data cannot be invented.
- Settings are server-wide. Category filters are not per-user parental controls. Restrict account access appropriately; the plugin does not infer age ratings from category names.
- Catalogue responses are cached for ten minutes. “Save, test connection & load categories” forces a catalogue refresh. Jellyfin has its own VOD media-source cache (up to five minutes); after changing credentials or disabling content, refresh the guide/internet channels and allow caches to expire. Existing streams are not forcibly terminated. Restarting the server clears these caches.
- The plugin's feed cache lasts four hours, but actual imports happen when Jellyfin refreshes the guide. Clicking the connection/category button invalidates the guide cache; it does not itself run a Jellyfin guide scan.
- Stream connections count against the provider's account limit. Multiple household users can exceed a one-connection plan. There is no provider connection pooling or connection-limit enforcement here.
- Provider credentials are stored in Jellyfin's plugin XML configuration. Stream URLs necessarily contain credentials and may appear in server/FFmpeg diagnostics; protect configuration, logs and backups. No credentials are embedded in this download.
- Prefer HTTPS where the panel supports it. TLS validation is not bypassed. API/guide redirects are rejected to avoid forwarding credentials to another host; enter the final panel URL. Media redirects are handled by Jellyfin/FFmpeg.
- HTTP API responses are limited to 128 MiB and XMLTV to 256 MiB of XML characters; very large feeds can exceed these bounds. Guide programmes are retained from the last day to the next 14 days.
- Playback support depends on provider codecs, Jellyfin/FFmpeg and your client. Native TV client behaviour has not been tested in this environment.

## Troubleshooting

**Plugin absent or unsupported:** verify server version, folder location and service permissions. Ensure only one copy is installed. Look for plugin loading errors in the Jellyfin log without sharing raw credential-containing URLs.

**Connection test fails:** verify scheme, final hostname, port, credentials and active subscription. The server itself must be able to reach the panel. Some providers block datacentre IPs. Error messages distinguish HTTP failures, timeouts, malformed responses and rejected accounts.

**Channels absent:** enable Live TV, select categories, save and run Refresh Guide. Check the user's Live TV access.

**Guide empty:** enable EPG, refresh guide, and confirm the provider supplies XMLTV listings whose channel IDs match the live stream `epg_channel_id` values. XMLTV failure does not prevent channel enumeration or playback.

**VOD absent:** enable VOD, select movie categories, run Refresh Internet Channels, then check Xtream Movies and user library access. Some clients expose internet channels differently.

**Playback fails:** try TS/HLS for live content, check concurrent connections and Jellyfin's FFmpeg/transcoding setup. Do not disable TLS certificate checks to solve an HTTPS configuration issue.

## Build and test

Install .NET SDK 10 and run from `source/`:

```sh
dotnet build XtreamBridge/XtreamBridge.csproj -c Release
dotnet run --project Tests/Tests.csproj -c Release
```

The DLL is at `XtreamBridge/bin/Release/net10.0/Jellyfin.Plugin.XtreamBridge.dll`. Copy only that plugin DLL and the provided metadata file for manual installation. The project uses Jellyfin.Controller and Jellyfin.Model **12.3.0** from NuGet; server-provided assemblies are not bundled.

Automated validation: 35 checks against a local mock panel, including category filtering, disabled-content playback rejection, URL encoding, malformed responses, XMLTV UTC conversion and consecutive entries, XXE rejection, cache behaviour, DI registration and embedded settings resources. The settings JavaScript also passed Node syntax validation. These checks are not a substitute for a real Jellyfin/client playback test.

Source references checked for this build:
- https://github.com/jellyfin/jellyfin/releases/tag/v12.3
- https://www.nuget.org/packages/Jellyfin.Controller/12.3.0
- https://github.com/jellyfin/jellyfin/tree/v12.3/MediaBrowser.Controller/LiveTv
- https://github.com/jellyfin/jellyfin/tree/v12.3/MediaBrowser.Controller/Channels
- https://jellyfin.org/docs/general/server/plugins/

## Remove

Stop Jellyfin, remove the installed XtreamBridge folder, and restart. Refresh the guide/internet channels to remove stale entries. Delete the plugin's configuration XML separately if you also want to remove saved provider credentials. Removing the plugin does not cancel your provider subscription.
