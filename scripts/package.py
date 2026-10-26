"""Package a compiled plugin and update the separate preview catalogue."""
import datetime, hashlib, json, zipfile, xml.etree.ElementTree as ET
from pathlib import Path
root=Path(__file__).resolve().parents[1]
version=ET.parse(root/'XtreamBridge/XtreamBridge.csproj').findtext('.//Version')
assert version and len(version.split('.'))==3
assembly=root/'XtreamBridge/bin/Release/net10.0/Jellyfin.Plugin.XtreamBridge.dll'
package=root/'packages'/f'xtream-bridge-{version}.zip'
package.parent.mkdir(exist_ok=True)
with zipfile.ZipFile(package,'w',zipfile.ZIP_DEFLATED) as z:
    entry=zipfile.ZipInfo(assembly.name, (2026,1,1,0,0,0));entry.compress_type=zipfile.ZIP_DEFLATED
    z.writestr(entry,assembly.read_bytes())
checksum=hashlib.md5(package.read_bytes()).hexdigest()
preview=root/'preview/manifest.json'
preview.parent.mkdir(exist_ok=True)
base=json.loads((root/'manifest-base.json').read_text())
plugin=base[0]
plugin['overview']='Xtream Live TV improvements — preview'
plugin['description']='Live TV control centre, persistent catalogue, channel rules, XMLTV mapping, multiple providers and optional native DVR. Preview: validate playback and recording on a test server.'
plugin['versions']=[v for v in plugin['versions'] if v['version']!=version+'.0']
plugin['versions'].insert(0,dict(version=version+'.0',changelog='Live TV preview: organised settings, persistent safe sync, channel/group overrides, EPG mapping, diagnostics, multiple providers, optional native DVR and admin catch-up. Real-server verification pending.',targetAbi='12.3.0.0',sourceUrl=f'https://raw.githubusercontent.com/LiamKenyon/jellyfin-xtreme/main/packages/xtream-bridge-{version}.zip',checksum=checksum,timestamp=datetime.datetime.now(datetime.timezone.utc).strftime('%Y-%m-%dT%H:%M:%SZ')))
preview.write_text(json.dumps(base,indent=2)+'\n')
(root/'SHA256SUMS.txt').write_text(hashlib.sha256(package.read_bytes()).hexdigest()+'  '+str(package.relative_to(root))+'\n')
print(package,checksum)
