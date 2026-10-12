"""Inspect an explicit public candidate without printing suspected secrets."""
import json,re,sys,hashlib
from pathlib import Path
from html.parser import HTMLParser
from urllib.parse import urlsplit, unquote

root=Path(sys.argv[1]).resolve() if len(sys.argv)>1 else Path(__file__).resolve().parents[1]
manifest=json.loads((root/'public-files.json').read_text(encoding='utf8'))
allowed=set(manifest['files'])
issues=[]
patterns=[r'(?i)C:[/\\]Users[/\\](?!Public(?:[/\\]|$))[^\s"<>]+',r'(?i)(?:ghp_[A-Za-z0-9]{32,}|github_pat_[A-Za-z0-9_]{30,}|sk-proj-[A-Za-z0-9_-]{25,})',r'-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----',r'(?i)teams\.microsoft\.com/l/meetup-join']
for name in allowed:
    path=root/name
    if not path.is_file(): issues.append(name+': missing'); continue
    if path.is_symlink(): issues.append(name+': symlink'); continue
    if path.suffix.lower() not in {'.png','.ico'}:
        text=path.read_text(encoding='utf-8-sig')
        if any(re.search(pattern,text) for pattern in patterns): issues.append(name+': sensitive or unwanted text pattern')
    else:
        # Images must come from the artificial demo pipeline. Their metadata is checked separately.
        raw=path.read_bytes()
        if b'Exif\x00\x00' in raw or b'tEXt' in raw or b'iTXt' in raw or b'zTXt' in raw: issues.append(name+': unexpected image metadata')

class Links(HTMLParser):
    def __init__(self): super().__init__(); self.refs=[]
    def handle_starttag(self,tag,attrs):
        attrs=dict(attrs)
        for attr in ('href','src'):
            if attr in attrs: self.refs.append(attrs[attr])

for path in (root/'website').glob('*.html'):
    parser=Links(); text=path.read_text(encoding='utf8'); parser.feed(text)
    for ref in parser.refs:
        if ref.startswith(('https:','http:','mailto:','#')): continue
        target=unquote(urlsplit(ref).path)
        if not target: continue
        if not (path.parent/target).is_file(): issues.append(str(path.relative_to(root))+': broken local link')
    if 'class="theme"' in text: issues.append(str(path.relative_to(root))+': unexpected theme switch')

# In an isolated candidate the full filesystem, not just ignored files, must match the list.
if root.parent.name=='publication' or '--candidate' in sys.argv:
    actual={p.relative_to(root).as_posix() for p in root.rglob('*') if p.is_file()}
    extra=actual-allowed-{'SOURCE-MANIFEST.json','SHA256SUMS.txt'}
    if extra: issues.extend(name+': unlisted file' for name in sorted(extra))
if issues:
    print('\n'.join(issues)); raise SystemExit(1)
print(f'PASS public contents: {len(allowed)} explicit files; local page links; no matched private patterns')
