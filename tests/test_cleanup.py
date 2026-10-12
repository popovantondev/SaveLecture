import sys, tempfile, json
from pathlib import Path
from PIL import Image, ImageDraw
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'src'))
import slide_cleanup as s

with tempfile.TemporaryDirectory() as tmp:
    folder=Path(tmp)/'Archive'/'11.10.2026'/'scr'; folder.mkdir(parents=True)
    im=Image.new('RGB',(800,450),'white'); d=ImageDraw.Draw(im); d.rectangle((80,80,500,110),fill='black')
    im.save(folder/'one.png'); im.save(folder/'two.png')
    changed=im.copy(); ImageDraw.Draw(changed).text((100,150),'New table text',fill='black'); changed.save(folder/'three.png')
    assert s.guard(folder/'one.png',folder/'three.png') is None
    cursor1=im.copy(); ImageDraw.Draw(cursor1).rectangle((40,30,45,38),fill='black'); cursor1.save(folder/'cursor1.png')
    cursor2=im.copy(); ImageDraw.Draw(cursor2).rectangle((650,30,655,38),fill='black'); cursor2.save(folder/'cursor2.png')
    assert s.guard(folder/'cursor1.png',folder/'cursor2.png') == 'Небольшое перемещение курсора'
    (folder/'cursor1.png').unlink(); (folder/'cursor2.png').unlink()
    service=folder.parent/'_service'; service.mkdir()
    (service/'slides.jsonl').write_text(json.dumps({'image':'scr/two.png','from':'08:00','until':'08:20'})+'\n',encoding='utf-8')
    s.preview(folder,.94)
    plan=json.loads((service/'cleanup'/'preview.json').read_text(encoding='utf-8'))
    assert len(plan['proposals'])==1 and plan['proposals'][0]['source']=='two.png'
    old=(folder/'two.png').read_bytes()
    s.apply(folder); assert not (folder/'two.png').exists() and (folder/'three.png').exists()
    resolved=json.loads((service/'cleanup'/'timeline_resolved.json').read_text(encoding='utf-8'))
    assert resolved[0]['image']=='scr/one.png' and resolved[0]['from']=='08:00'
    assert 'уже' in s.apply(folder)
    s.undo(folder); assert (folder/'two.png').read_bytes()==old
    s.preview(folder,.94); (folder/'two.png').write_bytes(b'changed')
    try: s.apply(folder); raise AssertionError('stale preview accepted')
    except ValueError: pass
    assert (folder/'one.png').exists()
print('PASS cleanup: preview, small text, reversible move, timeline, repeat, stale preview')
