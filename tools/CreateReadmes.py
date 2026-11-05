"""Write README.md (German), README.en.md and README.ru.md from website/content.json and public-release.json."""
from pathlib import Path
import json
root=Path(__file__).resolve().parents[1]
release=json.loads((root/'public-release.json').read_text(encoding='utf8'))
data=json.loads((root/'website/content.json').read_text(encoding='utf8'))
FILES={'de':'README.md','en':'README.en.md','ru':'README.ru.md'}
LINKS={'de':('Komponenten','Bauanleitung','Prüfstand'),'en':('Components','Build','Verification'),'ru':('Компоненты','Сборка','Проверка')}
for lang,d in data.items():
    switch=' · '.join(f'**{name}**' if l==lang else f'[{name}]({FILES[l]})' for l,name in (('ru','Русский'),('de','Deutsch'),('en','English')))
    lines=['# SaveLecture','',switch,'',d['lead'],'',f"**{release['platform']} · {d['status']} {release['version']}**",'',f"[{d['guide']}](https://popovantondev.github.io/SaveLecture/Guide-{lang}.html) · [{d['source']}](https://github.com/popovantondev/SaveLecture) · [{d['help']}](https://github.com/popovantondev/SaveLecture/issues/new)",'',f'![SaveLecture](website/assets/demo-{lang}.png)','',d['caption'],'',f"## {d['steps']}",'']
    if release.get('published') and release.get('release_url'): lines.insert(8,f"[{d['download']}]({release['release_url']})")
    lines.extend(f'{i+1}. {s}' for i,s in enumerate(d['stepList']))
    comp,build,verif=LINKS[lang]
    lines.extend(['',f"## {d['consent']}",'',d['privacy'],'',d['privacyExtra'],'',f"## {d['storage']}",'',d['storageBody'],'',d['statsBody'],'',f"## {d['startup']}",'',d['startupBody'],'',f"## {d['trouble']}",'',d['troubleBody'],'',f"## {d['limits']}",'',d['limitsBody'],'',f"[{d['rights']}](RIGHTS.md) · [{comp}](THIRD_PARTY_NOTICES.md) · [{build}](docs/BUILDING.md) · [{verif}](docs/VERIFICATION.md)"])
    (root/FILES[lang]).write_text('\n'.join(lines)+'\n',encoding='utf8',newline='\n')
