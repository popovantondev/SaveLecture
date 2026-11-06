"""Write README.md (German), README.en.md and README.ru.md from website/content.json and public-release.json."""
from pathlib import Path
import json
root=Path(__file__).resolve().parents[1]
release=json.loads((root/'public-release.json').read_text(encoding='utf8'))
data=json.loads((root/'website/content.json').read_text(encoding='utf8'))
SITE={'de':'Webseite','en':'Website','ru':'Сайт'}
ICON_ALT={'de':'SaveLecture Symbol','en':'SaveLecture icon','ru':'Значок SaveLecture'}
FILES={'de':'README.md','en':'README.en.md','ru':'README.ru.md'}
LINKS={'de':('Komponenten','Bauanleitung','Prüfstand'),'en':('Components','Build','Verification'),'ru':('Компоненты','Сборка','Проверка')}
for lang,d in data.items():
    switch=' · '.join(f'**{name}**' if l==lang else f'[{name}]({FILES[l]})' for l,name in (('ru','Русский'),('de','Deutsch'),('en','English')))
    # README standard: icon, title, language line (RU · DE · EN, current bold), what, platform, buttons, preview.
    site='https://popovantondev.github.io/SaveLecture/'+{'de':'','en':'index-en.html','ru':'index-ru.html'}[lang]
    buttons=[]
    if release.get('published') and release.get('release_url'): buttons.append((d['download'],release['release_url']))
    buttons+=[(SITE[lang],site),(d['guide'],f'https://popovantondev.github.io/SaveLecture/Guide-{lang}.html'),(d['help'],'https://github.com/popovantondev/SaveLecture/issues/new')]
    row=' · '.join(f'**[{a}]({u})**' for a,u in buttons)
    lines=['<div align="center">','  <img src="website/assets/icon.png" width="128" alt="'+ICON_ALT[lang]+'">','</div>','','# SaveLecture','',switch,'',d['lead'],'',f"**{release['platform']} · {d['status']} {release['version']}**",'',row,'',f'![SaveLecture](website/assets/demo-{lang}.png)','',d['caption'],'',f"## {d['steps']}",'']
    lines.extend(f'{i+1}. {s}' for i,s in enumerate(d['stepList']))
    comp,build,verif=LINKS[lang]
    lines.extend(['',f"## {d['consent']}",'',d['privacy'],'',d['privacyExtra'],'',f"## {d['storage']}",'',d['storageBody'],'',d['statsBody'],'',f"## {d['startup']}",'',d['startupBody'],'',f"## {d['trouble']}",'',d['troubleBody'],'',f"## {d['limits']}",'',d['limitsBody'],'',f"## {d['rights']}",'',f"[{d['rights']}](RIGHTS.md) · [{comp}](THIRD_PARTY_NOTICES.md) · [{build}](docs/BUILDING.md) · [{verif}](docs/VERIFICATION.md)"])
    (root/FILES[lang]).write_text('\n'.join(lines)+'\n',encoding='utf8',newline='\n')
