"""Prepare a local portable candidate from an audited source checkout and build."""
from pathlib import Path
import hashlib,json,shutil,sys,uuid,zipfile

root=Path(__file__).resolve().parents[1]
release=json.loads((root/'public-release.json').read_text(encoding='utf8'))
allowed=set(json.loads((root/'public-files.json').read_text(encoding='utf8'))['files'])
files=['SaveLecture.exe','SaveLecture.ico','NameAliases.txt','assets/brand.png','assets/localization.json',
       'src/teams_caption_reader.ps1','src/caption_observation_state.ps1','src/slide_cleanup.py',
       'reference/SlideFilter/presentation_deduper.py','InstallAutostart.ps1','UninstallAutostart.ps1',
       'README.md','README.ru.md','README.de.md','RIGHTS.md','THIRD_PARTY_NOTICES.md',
       'docs/BUILDING.md','docs/VERIFICATION.md']
files+=sorted(p for p in allowed if p.startswith(('src/ui/','website/')))
destination=root/'publication'/('portable-'+uuid.uuid4().hex[:12])
package=destination/('SaveLecture-'+release['version']+'-windows-x64')
package.mkdir(parents=True,exist_ok=False)
records=[]
for name in files:
    if name!='SaveLecture.exe' and name not in allowed: raise RuntimeError('Unreviewed package path: '+name)
    source=root/name
    if source.is_symlink() or not source.is_file() or not source.resolve().is_relative_to(root): raise RuntimeError('Invalid package source')
    target=package/name; target.parent.mkdir(parents=True,exist_ok=True)
    if name=='NameAliases.txt': target.write_text('',encoding='utf8')
    else: shutil.copyfile(source,target)
    records.append({'path':name,'sha256':hashlib.sha256(target.read_bytes()).hexdigest()})
(package/'PACKAGE-MANIFEST.json').write_text(json.dumps(records,indent=2)+'\n',encoding='utf8')
archive=destination/(package.name+'.zip')
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as output:
    for file in sorted(package.rglob('*')):
        if file.is_file(): output.write(file,file.relative_to(package).as_posix())
with zipfile.ZipFile(archive) as output:
    if output.testzip() is not None: raise RuntimeError('Package integrity failed')
    for item in records:
        if hashlib.sha256(output.read(item['path'])).hexdigest()!=item['sha256']: raise RuntimeError('Package hash mismatch')
digest=hashlib.sha256(archive.read_bytes()).hexdigest()
(destination/'SHA256SUMS.txt').write_text(digest+'  '+archive.name+'\n',encoding='ascii')
print('PORTABLE='+str(archive)); print('SHA256='+digest)
