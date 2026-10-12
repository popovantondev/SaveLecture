from pathlib import Path
import json
root=Path(__file__).resolve().parents[1]
data=json.loads((root/'website/content.json').read_text(encoding='utf8'))
for lang,d in data.items():
    lines=['# SaveLecture','','[Русский](README.ru.md) · [Deutsch](README.de.md) · [English](README.en.md)','',d['lead'],'',f"**Windows 11 · x64 · {d['status']} 0.1.0-preview.1**",'',f"[{d['guide']}](https://popovantondev.github.io/SaveLecture/Guide-{lang}.html) · [{d['source']}](https://github.com/popovantondev/SaveLecture) · [{d['help']}](https://github.com/popovantondev/SaveLecture/issues/new)",'',f'![SaveLecture](../website/assets/demo-{lang}.png)','',d['caption'],'',f"## {d['steps']}",'']
    lines.extend(f'{i+1}. {s}' for i,s in enumerate(d['stepList']))
    lines.extend(['',f"## {d['consent']}",'',d['privacy'],'',d['privacyExtra'],'',f"## {d['startup']}",'',d['startupBody'],'',f"## {d['limits']}",'',d['limitsBody'],'',f"[{d['rights']}](../RIGHTS.md) · [Components](../THIRD_PARTY_NOTICES.md) · [Build](BUILDING.md) · [Verification](VERIFICATION.md)"])
    (root/'docs'/f'README.{lang}.md').write_text('\n'.join(lines)+'\n',encoding='utf8')
    top='\n'.join(lines).replace('(README.ru.md)','(README.ru.md)').replace('(README.de.md)','(README.de.md)').replace('(README.en.md)','(README.md)').replace('../website/','website/').replace('../RIGHTS.md','RIGHTS.md').replace('../THIRD_PARTY_NOTICES.md','THIRD_PARTY_NOTICES.md').replace('(BUILDING.md)','(docs/BUILDING.md)').replace('(VERIFICATION.md)','(docs/VERIFICATION.md)')
    (root/('README.md' if lang=='en' else f'README.{lang}.md')).write_text(top+'\n',encoding='utf8')
