"""Write README.md (German), README.en.md and README.ru.md from website/content.json and public-release.json.

The header zone and the zone with buttons, preview and divider come from docs/readme/top.<lang>.md and
body.<lang>.md (written by the README kit); they are inserted here as they are.
"""
from pathlib import Path
import json
root=Path(__file__).resolve().parents[1]
release=json.loads((root/'public-release.json').read_text(encoding='utf8'))
data=json.loads((root/'website/content.json').read_text(encoding='utf8'))
FILES={'de':'README.md','en':'README.en.md','ru':'README.ru.md'}
LINKS={'de':('Komponenten','Bauanleitung','Prüfstand'),'en':('Components','Build','Verification'),'ru':('Компоненты','Сборка','Проверка')}
RELEASE={'de':'Release','en':'Release','ru':'Релиз'}
for lang,d in data.items():
    switch=' · '.join(f'**{name}**' if l==lang else f'[{name}]({FILES[l]})' for l,name in (('ru','Русский'),('de','Deutsch'),('en','English')))
    zone=lambda name:(root/'docs/readme'/f'{name}.{lang}.md').read_text(encoding='utf8').replace('\r\n','\n').rstrip()
    # README standard: header zone, title, language line (RU · DE · EN, current bold), zone with buttons + preview + divider, what, platform, screen note.
    lines=[zone('top'),'','# SaveLecture','',switch,'',zone('body'),'',d['lead'],'',f"**{release['platform']} · {d['status']} {release['version']}**",'',d['caption'],'',f"## {d['steps']}",'']
    lines.extend(f'{i+1}. {s}' for i,s in enumerate(d['stepList']))
    comp,build,verif=LINKS[lang]
    lines.extend(['',f"## {d['consent']}",'',d['privacy'],'',d['privacyExtra'],'',f"## {d['storage']}",'',d['storageBody'],'',d['statsBody'],'',f"## {d['startup']}",'',d['startupBody'],'',f"## {d['trouble']}",'',d['troubleBody'],'',f"## {d['limits']}",'',d['limitsBody'],'',f"## {d['rights']}",'',f"[{d['rights']}](RIGHTS.md) · [{comp}](THIRD_PARTY_NOTICES.md) · [{build}](docs/BUILDING.md) · [{verif}](docs/VERIFICATION.md)"+(f" · [{RELEASE[lang]}]({release['release_url']})" if release.get('published') and release.get('release_url') else '')])
    (root/FILES[lang]).write_text('\n'.join(lines)+'\n',encoding='utf8',newline='\n')
