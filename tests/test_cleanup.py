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
    removed=folder.parent/'Removed'; removed.mkdir()
    (folder/'one.png').rename(removed/'one.png')
    (service/'slides_relocations.jsonl').write_text(json.dumps({'source':'scr/one.png','destination':'Removed/one.png'})+'\n',encoding='utf-8')
    with (service/'slides.jsonl').open('a',encoding='utf-8') as log:
        log.write(json.dumps({'image':'scr/one.png','from':'08:21','until':'08:30'})+'\n')
    (service/'slides_best.json').write_text(json.dumps({'items':[{'image':'scr/three.png','earlier_versions':['scr/one.png']}]}),encoding='utf-8')
    s.resolve(folder,[])
    relocated_timeline=json.loads((service/'cleanup'/'timeline_resolved.json').read_text(encoding='utf-8'))
    relocated_best=json.loads((service/'cleanup'/'slides_best_resolved.json').read_text(encoding='utf-8'))
    assert relocated_timeline[-1]['image']=='Removed/one.png'
    assert relocated_best['items'][0]['earlier_versions']==['Removed/one.png']
    assert 'scr/one.png' in (service/'slides.jsonl').read_text(encoding='utf-8')
print('PASS cleanup: preview, small text, reversible move, timeline, repeat, stale preview')
