"""SlideFilter integration: preview, reversible moves, resolved timeline.
Never invoke its destructive move_rejected helper. Original journal stays intact.
"""
import sys, json, os, hashlib, html, uuid
from pathlib import Path
import numpy as np
from PIL import Image
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'reference' / 'SlideFilter'))
import presentation_deduper as sf

def durable(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_suffix(path.suffix + '.tmp')
    with tmp.open('w', encoding='utf-8') as f:
        json.dump(value, f, ensure_ascii=False, indent=2); f.flush(); os.fsync(f.fileno())
    os.replace(tmp, path)

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def guard(first, second):
    # Full-resolution evidence prevents the robust SlideFilter score alone
    # from discarding small writing. Cursor must remove AND add small marks.
    with Image.open(first) as im: a = np.asarray(im.convert('L'), dtype=np.int16)
    with Image.open(second) as im: b = np.asarray(im.convert('L'), dtype=np.int16)
    if a.shape != b.shape: return None
    delta = b-a; mask = abs(delta)>35; count = int(mask.sum())
    if not count: return 'Практически одинаковый кадр'
    ys, xs = np.where(mask)
    if count < a.size*.0008 and (delta>35).sum() > count*.25 and (delta < -35).sum() > count*.25:
        span_x, span_y = int(xs.max()-xs.min()+1), int(ys.max()-ys.min()+1)
        if (span_x > 20 and len(np.unique(xs)) < span_x*.6) or (span_y > 20 and len(np.unique(ys)) < span_y*.6):
            return 'Небольшое перемещение курсора'
    return None

def locations(folder):
    folder = folder.resolve()
    if folder.name != 'scr' or folder.parent.parent.name != 'Archive':
        raise ValueError('Выберите папку scr внутри Archive/дата')
    return folder.parent / '_service' / 'cleanup', folder / '_УДАЛЕННЫЕ'

def preview(folder, threshold):
    service, trash = locations(folder)
    files = sorted(sf.find_images(folder), key=lambda p:p.name)
    frames=[]; errors=[]
    for p in files:
        try:
            # SaveLecture JPGs already contain the shared-content crop.
            with Image.open(p) as im:
                feature, sharp, content = sf.build_feature(im.convert('RGB'), (160,100))
            frames.append((p, feature))
        except Exception as e: errors.append(p.name+': '+str(e))
    kept=[]; proposals=[]; uncertain=[]
    for p, feature in frames:
        duplicate=None
        for old, previous in reversed(kept):
            score, shift=sf.comparison_score(previous, feature, 12)
            if score < threshold: continue
            reason=guard(p, old)
            if reason:
                duplicate=(old,score,reason); break
            if len(uncertain)<100: uncertain.append({'first':old.name,'second':p.name,'score':round(score,4)})
        if duplicate:
            old,score,reason=duplicate
            proposals.append({'source':p.name,'keep':old.name,'reason':reason,'score':round(score,4),'hash':digest(p),'keep_hash':digest(old)})
        else: kept.append((p,feature))
    plan={'id':uuid.uuid4().hex,'folder':str(folder.resolve()),'threshold':threshold,'proposals':proposals,'uncertain':uncertain,'errors':errors,'scanned':len(files)}
    durable(service/'preview.json',plan)
    rows=[]
    for item in proposals:
        pics=''.join('<a href="'+(folder/item[k]).as_uri()+'"><img width="440" src="'+(folder/item[k]).as_uri()+'"><br>'+html.escape(item[k])+'</a>' for k in ('source','keep'))
        rows.append('<div>'+html.escape(item['reason'])+' · '+str(item['score'])+'<br>'+pics+'</div><hr>')
    report=service/'preview.html'
    report.write_text('<meta charset="utf-8"><title>Очистка скриншотов</title><style>body{font:16px Segoe UI;margin:24px}a{display:inline-block;margin:10px}img{object-fit:contain}</style><h1>Предварительный просмотр</h1><p>Файлов: '+str(len(files))+'; предложено переместить: '+str(len(proposals))+'</p><p>Слева — перенос в _УДАЛЕННЫЕ, справа — оставляем. Неуверенные совпадения сохранены ('+str(len(uncertain))+'). Новые надписи и заполнение таблиц автоматически не удаляются.</p>'+''.join(rows)+'<pre>'+html.escape('\n'.join(errors))+'</pre>',encoding='utf-8')
    return str(report)

def resolve(folder, batches):
    service,_=locations(folder); mapping={}
    relocations=folder.parent/'_service'/'slides_relocations.jsonl'
    if relocations.exists():
        for line in relocations.read_text(encoding='utf-8-sig').splitlines():
            try:
                item=json.loads(line); source=item['source']; destination=item['destination']
                if not (folder.parent/source).exists() and (folder.parent/destination).exists():
                    mapping[source]=destination
            except (ValueError,KeyError):
                continue
    for batch in batches:
        if batch.get('undone'): continue
        for item in batch['items']:
            if not (folder/item['source']).exists() and Path(item['destination']).exists():
                mapping['scr/'+item['source']]='scr/'+item['keep']
    def mapped(value):
        visited=set()
        while value in mapping and value not in visited:
            visited.add(value); value=mapping[value]
        return value
    timeline=[]
    raw=folder.parent/'_service'/'slides.jsonl'
    if raw.exists():
        for line in raw.read_text(encoding='utf-8-sig').splitlines():
            try: row=json.loads(line)
            except ValueError: continue
            if 'image' in row:
                row['original_image']=row['image']; row['image']=mapped(row['image'])
            timeline.append(row)
    durable(service/'image_map.json',mapping)
    durable(service/'timeline_resolved.json',timeline)
    best=folder.parent/'_service'/'slides_best.json'
    if best.exists():
        data=json.loads(best.read_text(encoding='utf-8-sig'))
        for row in data.get('items',[]):
            row['original_image']=row['image']; row['image']=mapped(row['image'])
            row['earlier_versions']=[mapped(p) for p in row.get('earlier_versions',[])]
        durable(service/'slides_best_resolved.json',data)

def apply(folder):
    service,trash=locations(folder)
    plan=json.loads((service/'preview.json').read_text(encoding='utf-8'))
    if Path(plan['folder']) != folder.resolve(): raise ValueError('Папка изменилась')
    history=service/'history.json'
    batches=json.loads(history.read_text(encoding='utf-8')) if history.exists() else []
    if any(b['id']==plan['id'] for b in batches): return 'Этот просмотр уже применён'
    items=[]
    for item in plan['proposals']:
        if Path(item['source']).name!=item['source'] or Path(item['keep']).name!=item['keep']: raise ValueError('Некорректный путь')
        src,keep=folder/item['source'],folder/item['keep']
        if not src.exists() or not keep.exists() or digest(src)!=item['hash'] or digest(keep)!=item['keep_hash']: raise ValueError('Файлы изменились — повторите просмотр')
        dest=trash/plan['id']/item['source']
        if dest.exists(): raise ValueError('Назначение уже существует')
        items.append(dict(item,destination=str(dest)))
    batch={'id':plan['id'],'items':items,'undone':False}
    batches.append(batch); durable(history,batches) # write intent BEFORE first move
    try:
        for item in items:
            dest=Path(item['destination']); dest.parent.mkdir(parents=True,exist_ok=True)
            os.rename(folder/item['source'],dest)
    finally: resolve(folder,batches)
    return 'Перемещено: '+str(len(items))+'; исходный таймлайн сохранён'

def undo(folder):
    service,_=locations(folder); history=service/'history.json'
    batches=json.loads(history.read_text(encoding='utf-8')) if history.exists() else []
    batch=next((b for b in reversed(batches) if not b.get('undone')),None)
    if not batch: return 'Нет очистки для отмены'
    for item in batch['items']:
        src,dest=folder/item['source'],Path(item['destination'])
        if dest.exists() and (src.exists() or digest(dest)!=item['hash']): raise ValueError('Конфликт восстановления: '+src.name)
    for item in batch['items']:
        dest=Path(item['destination'])
        if dest.exists(): os.rename(dest,folder/item['source'])
    batch['undone']=True; durable(history,batches); resolve(folder,batches)
    return 'Последняя очистка отменена'

if __name__=='__main__':
    sys.stdout.reconfigure(encoding='utf-8')
    mode,folder=sys.argv[1],Path(sys.argv[2])
    if mode=='preview': print(preview(folder,float(sys.argv[3])))
    elif mode=='apply': print(apply(folder))
    elif mode=='undo': print(undo(folder))
