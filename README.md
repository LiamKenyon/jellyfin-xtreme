# Xtream Bridge 2.0 Live TV preview

Targets Jellyfin **12.3** and .NET **10**. This is a development preview, not a claim that every planned improvement is finished. Existing movie library content is unaffected; no new movie or series importer is enabled.

## What this preview implements

- Eight settings tabs: Overview, Connection, Channels, Groups, Guide, Playback, System and Library.
- Multiple Xtream providers, saved connection testing and account status.
- Persistent channel catalogues, atomic cache writes, bounded downloads, retry handling and retained data when a provider fails.
- Review before a provider removes a large percentage of channels; new categories hidden until selected by default.
- Search and pagination; individual enable/disable, display name, group, number, logo, stream format, EPG ID/source and time offset overrides.
- Stable plugin-mode channel IDs, Live TV category tags, optional category collections, and selectable alternative streams where the client supports media-source selection.
- Provider XMLTV, an alternative XMLTV URL, guide coverage checks, missing mapping inspection and cached fallback. Historical listings are retained for up to 14 days.
- A scheduled sync task, manual background sync with progress, rule import/export and a sanitised support report.
- Optional managed M3U/XMLTV tuners for Jellyfin's native recording features, tuner connection allowance and stream sharing. Unrelated tuners are preserved.
- Optional provider catch-up browser, currently restricted to administrators. Availability depends on provider archives and retained guide data.
- CI builds, backend and settings interaction tests, packaging, and optional publication to a separate preview catalogue.

## Install through Manage Repositories

The update bundle contains `packages/xtream-bridge-2.0.0.zip` and `preview/manifest.json`. **Do not use the outer update bundle as a plugin installer.**

1. Extract the update bundle on your computer.
2. Open `https://github.com/LiamKenyon/jellyfin-xtreme` on branch `main`.
3. Upload the inner `packages/xtream-bridge-2.0.0.zip` to the repository's **packages** folder. Preserve older packages.
4. Create a **preview** folder and upload its `manifest.json` there. Its path must be **preview/manifest.json**. Leave the existing root `manifest.json` unchanged while testing.
5. Commit both files to `main`. Open these URLs and check that the first displays JSON and the second downloads the ZIP:
   - `https://raw.githubusercontent.com/LiamKenyon/jellyfin-xtreme/main/preview/manifest.json`
   - `https://raw.githubusercontent.com/LiamKenyon/jellyfin-xtreme/main/packages/xtream-bridge-2.0.0.zip`
6. In Jellyfin, open **Dashboard → Plugins → Repositories → Add**.
7. Enter **Xtream Bridge Preview** as the name and the first URL above as the repository URL; save.
8. Refresh the plugin catalogue and open **Xtream Bridge**. Select **2.0.0.0**, install and restart Jellyfin.
9. Open **Dashboard → Plugins → My Plugins → Xtream Bridge → Settings**. Check your connection, select channels/categories, and choose **Save & Sync**.
10. Follow progress on **Overview**. Channels appear in Jellyfin's **Live TV** area. Configure the recurring task under **Dashboard → Scheduled Tasks → Sync Xtream Live TV**.

Use a test Jellyfin instance first for this preview. Back up the plugin configuration and Live TV configuration before changing DVR mode. This bundle has not been published to your GitHub repository by this session.

## Categories for viewers

In plugin mode, channels carry their category/custom group as a Jellyfin tag. Native client support for grouping/filtering varies. Turning on **Groups → Create category collections** adds `TV · <group>` collections containing the imported channels after a sync. These are normal Jellyfin collections, not a replacement Live TV interface. The plugin cannot add identical custom category navigation to every Jellyfin client. The grouping options are server-wide; use Jellyfin's own user permissions and blocked tags for access control, and verify restrictions with a non-admin account.

## Recording and catch-up

Leave **Native DVR** off until you want to test recording. Enabling it changes the channel backend and imported channel IDs; favourites and recording schedules may need remapping. Configure the recording directory and permissions in Jellyfin. Native tuner connection limits apply only in this mode. DVR mode assigns simple sequential channel numbers unless custom/explicit numbers are enabled.

Catch-up is a separate `Xtream Catch-up` channel and is currently admin-only. It uses provider archives, not local recordings. Configure a provider archive time offset if necessary. Programme data cannot be reconstructed for times that were never present in a downloaded guide. Pause/rewind and buffering remain client-dependent.

## Security and recovery

The custom settings page does not return saved passwords. Blank password fields preserve existing values. Support reports exclude provider URLs, credentials, channel titles and raw logs. Rule exports contain custom channel names, groups and logo URLs; they are not complete configuration backups.

**Credentials are still stored in Jellyfin's plugin configuration and, in DVR mode, in generated local M3U files. This preview does not encrypt credentials at rest.** Generated files are owner-only on Unix; Windows uses the inherited data-directory ACL. Jellyfin's playback machinery necessarily receives credential-bearing provider URLs, so treat server configuration, logs and backups as private. There is no guarantee that Jellyfin or an external proxy will redact every stream URL.

To roll back: turn Native DVR off and sync first if it was enabled; stop Jellyfin; restore your backed-up plugin/Live TV configuration and previous plugin binary; restart and refresh the guide. Reusing older binaries with newer configuration has not been tested. Removing the preview repository prevents future preview catalogue updates but does not downgrade an installed plugin.

## Build and publish

Requirements: .NET 10 SDK, Node 22 or later, Python 3.

```sh
python3 scripts/embed-settings.py
dotnet run --project Tests -c Release
npm ci
npm test
python3 scripts/package.py
```

Upload the source, tests, scripts, package files and `.github/workflows/build.yml` if you want builds in your repository. In GitHub **Actions → Build and verify Xtream Bridge**, a normal run produces a downloadable catalogue artifact. A manual run on `main` with **publish_preview** checked commits the tested package and separate preview feed; it needs repository write permission, and protected-branch rules may prevent the push. This workflow has been supplied, not executed on your repository. Update the project version and workflow artifact path for future releases; never replace a released version with a different binary.

`manifest-base.json` retains the previously known 1.0.1 and 1.0.0 entries. When promoting a preview to the main catalogue, merge it with the current live manifest so later releases are not lost. The manifest checksum is MD5 as required by Jellyfin; `SHA256SUMS.txt` provides an additional package checksum.

See `TEST-REPORT.md` for checks actually run, and `REMAINING-WORK.md` for work that is not yet complete.
