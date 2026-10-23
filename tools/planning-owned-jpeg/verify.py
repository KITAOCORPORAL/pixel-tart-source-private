import pathlib,json,zipfile,hashlib,xml.etree.ElementTree as E,sys
sys.path.insert(0,"E:/Kitao-Deliveries/planning-owned-jpeg-company-20261023/orientation-references/python")
from PIL import Image
import io
r=pathlib.Path("E:/Kitao-Worktrees/planning-owned-jpeg-company-20261023");d=pathlib.Path("E:/Kitao-Deliveries/planning-owned-jpeg-company-20261023");app=pathlib.Path("E:/Kitao-Deliveries/planning-owned-jpeg-company-20261023/Planning-OwnedJPEG-Diagnostic-win-x64/app");sha="4997c3059c5737405921d88ba6fd74eb2be2496e"
def h(p):return hashlib.sha256(pathlib.Path(p).read_bytes()).hexdigest()
s=json.loads((d/'synthetic-final/RESULT.json').read_text(encoding='utf-8-sig'));ns={'w':'http://schemas.openxmlformats.org/wordprocessingml/2006/main'};media=[]
expected={name:Image.open(r/'tests/fixtures/planning-owned-jpeg'/name).convert('RGB') for name in ['pillow-6.png','pillow-8.png','plain.png']}
with zipfile.ZipFile(s['summary']) as z:
 xml=E.fromstring(z.read('word/document.xml'));headings=[];section=None;drawingSections=[]
 for block in xml.find('w:body',ns):
  style=block.find('w:pPr/w:pStyle',ns)
  if style is not None and style.get('{'+ns['w']+'}val')=='Heading1':section=''.join(block.itertext());headings.append(section)
  drawingSections += [section for _ in block.findall('.//w:drawing',ns)]
 for name in z.namelist():
  if '/media/' not in '/'+name:continue
  image=Image.open(io.BytesIO(z.read(name))).convert('RGB');matches=[n for n,e in expected.items() if image.size==e.size and image.tobytes()==e.tobytes()];assert matches,(name,image.size);media.append(dict(path=name,size=image.size,matches=matches,sha256=hashlib.sha256(z.read(name)).hexdigest()))
 assert len(headings)==15 and len(media)==4 and len(drawingSections)==4
with zipfile.ZipFile(s['owned']) as z:
 manifest=json.loads(z.read('manifest.json'))
 for f in manifest['Files']:assert hashlib.sha256(z.read(f['Path'])).hexdigest().lower()==f['Sha256'].lower()
 imported=json.loads((pathlib.Path(s['importedRoot'])/'planning'/(s['project'].replace('-','')+'.planning.json')).read_text());link=imported['Document']['ShotExternalLinks'][0];assert link['ReconnectedFile'] is None;assert h(pathlib.Path(s['importedRoot'])/'planning'/link['ProjectCopy']['RelativePath']).upper()==s['selectedHash']
files=[dict(path=f.relative_to(app).as_posix(),bytes=f.stat().st_size,sha256=h(f)) for f in sorted(app.rglob('*')) if f.is_file()];deps=json.loads((app/'KitaoPhotoSelector.deps.json').read_text());assets=[]
for libs in deps['targets'].values():
 for library,info in libs.items():
  for kind in ['runtime','native','resources','runtimeTargets']:
   for name,v in info.get(kind,{}).items():
    if name.endswith('/_._') or (kind=='runtimeTargets' and v.get('rid') not in [None,'win-x64','win']):continue
    candidates=[app/name,app/pathlib.PurePosixPath(name).name]
    if kind=='resources':candidates.append(app/v.get('locale','')/pathlib.PurePosixPath(name).name)
    assets.append(dict(library=library,path=name,present=any(p.is_file() for p in candidates)))
assert all(x['present'] for x in assets)
b=json.loads((d/'FINAL_BINDINGS.json').read_text());assert all(h(x['published'])==h(x['test'])==x['sha256'] for x in b['bindings'])
for name,value in [('APP_FILES.json',files),('DEPENDENCIES.json',assets),('DOCX_READBACK.json',dict(productSha=sha,headings=headings,media=media,drawingSections=drawingSections,sha256=h(s['summary']),scope='Independent actual OOXML/media pixel readback, NOT Word/WPS/native desktop',attachmentJpegNotEmbedded=True)),('VERIFIED_OUTPUTS.json',dict(productSha=sha,appFiles=len(files),runtimeAssets=len(assets),missing=0,bindingCount=len(b['bindings']),result=s,machineApprovalClearedOnImport=True,isolatedJpegCopyVerified=True))]:(d/name).write_text(json.dumps(value,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(dict(files=len(files),assets=len(assets),media=len(media),headings=len(headings),drawingSections=drawingSections)))
