# Xtream Bridge — Jellyfin repository

Prepared for the public GitHub repository `LiamKenyon/jellyfin-xtream`, branch `main`.
These URLs do not work until the files have been published there.

## Install through Jellyfin

1. Dashboard → Plugins → Repositories (Manage Repositories) → Add.
2. Name: Xtream Bridge.
3. Repository URL: `https://raw.githubusercontent.com/LiamKenyon/jellyfin-xtream/main/manifest.json`
4. Save, open Catalog, select Xtream Bridge, install 1.0.0.0, restart Jellyfin.
5. Open plugin settings, enter panel login, load categories and save.
6. Run Refresh Guide and Refresh Internet Channels in Scheduled Tasks.

Requires Jellyfin 12.3.0.0. VOD movies supported; series, catch-up and recording are not included.
The build passed 35 mock-panel tests but has not been tested on a running Jellyfin server.

## Hosting

Upload the contents of this repository folder to the public repo root, retaining the packages directory.
The installer ZIP contains the DLL directly at its root, as required for repository installation.
The manifest checksum is the MD5 of the exact downloadable ZIP (Jellyfin's package checksum format).
If you use another GitHub owner, repository name or branch, change sourceUrl in manifest.json and the repository URL above.
No GitHub Pages setup is needed: both files are served through raw.githubusercontent.com.

See MANUAL-INSTALL.md for configuration details, limitations and build/test instructions (source is at this repository root).
