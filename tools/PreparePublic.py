"""Copy only reviewed paths to a new candidate. Never publish or copy a working tree."""
from pathlib import Path
import json,sys,shutil,hashlib,zipfile,uuid,subprocess

root=Path(__file__).resolve().parents[1]
manifest=json.loads((root/'public-files.json').read_text(encoding='utf8'))
destination=root/'publication'/('candidate-'+uuid.uuid4().hex[:12])
destination.mkdir(parents=True,exist_ok=False)
hashes=[]
for name in manifest['files']:
    source=root/name
    if source.is_symlink() or not source.is_file() or source.resolve().is_relative_to(root)==False: raise RuntimeError('Invalid public path: '+name)
    if name in {'.gitignore','NameAliases.txt'}:
        # Personal aliases and local development ignores never enter the candidate.
        target=destination/name; target.write_text('' if name=='NameAliases.txt' else 'Archive/\n*.exe\n*.log\n__pycache__/\n*.pyc\npublication/\n',encoding='utf8')
    else:
        target=destination/name; target.parent.mkdir(parents=True,exist_ok=True); shutil.copyfile(source,target)
        if source.suffix.lower() not in {'.png','.ico'}:
            target.write_bytes(target.read_bytes().replace(b'\r\n',b'\n'))
    hashes.append({'path':name,'sha256':hashlib.sha256(target.read_bytes()).hexdigest()})
subprocess.run([sys.executable,str(destination/'tools/CheckPublic.py'),str(destination),'--candidate'],check=True)
(destination/'SOURCE-MANIFEST.json').write_text(json.dumps(hashes,indent=2)+'\n',encoding='utf8')
archive=destination.parent/(destination.name+'-source.zip')
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as output:
    for file in sorted(destination.rglob('*')):
        if file.is_file(): output.write(file,file.relative_to(destination).as_posix())
with zipfile.ZipFile(archive) as check:
    if check.testzip() is not None: raise RuntimeError('Source ZIP integrity failed')
print('CANDIDATE='+str(destination))
print('SOURCE_ZIP='+str(archive))
print('SHA256='+hashlib.sha256(archive.read_bytes()).hexdigest())
