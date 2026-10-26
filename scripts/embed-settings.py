from pathlib import Path
root=Path(__file__).resolve().parents[1]
folder=root/'XtreamBridge/Configuration'
template=(folder/'config.template.html').read_text()
assert template.count('__SCRIPT__')==1
(folder/'config.html').write_text(template.replace('__SCRIPT__',(folder/'settings.js').read_text()))
