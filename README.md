# Xtream Bridge — Jellyfin repository

## Install through Jellyfin

1. Dashboard → Plugins → Repositories (Manage Repositories) → Add.
2. Name: Xtream Bridge.
3. Repository URL: `https://raw.githubusercontent.com/LiamKenyon/jellyfin-xtream/main/manifest.json`
4. Save, open Catalog, select Xtream Bridge, install 1.0.0.0, restart Jellyfin.
5. Open plugin settings, enter panel login, load categories and save.
6. Run Refresh Guide and Refresh Internet Channels in Scheduled Tasks.

Requires Jellyfin 12.3.0.0. VOD movies supported; series, catch-up and recording are not included.
The build passed 35 mock-panel tests but has not been tested on a running Jellyfin server.
