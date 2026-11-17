"""Prepare an isolated managed-only replay host. Never copies RAW native decoder assets."""
from pathlib import Path
import argparse,json,shutil,struct,hashlib
p=argparse.ArgumentParser();p.add_argument('--product-app',required=True,type=Path);p.add_argument('--host-build',required=True,type=Path);p.add_argument('--resources',required=True,type=Path);p.add_argument('--output',required=True,type=Path);a=p.parse_args();app=a.product_app.resolve();out=a.output.resolve()
if out.exists():raise RuntimeError('Use a fresh stage; preserve prior run/binary identity')
def managed(b):
 if len(b)<256 or b[:2]!=b'MZ':return False
 pe=struct.unpack_from('<I',b,60)[0]
 if pe+24+232>len(b) or b[pe:pe+4]!=b'PE\0\0':return False
 opt=pe+24;magic=struct.unpack_from('<H',b,opt)[0];dirs=opt+(112 if magic==0x20b else 96 if magic==0x10b else len(b));clr=dirs+14*8
 return clr+8<=len(b) and struct.unpack_from('<II',b,clr)[0]!=0
j=json.loads((app/'KitaoPhotoSelector.deps.json').read_text('utf-8-sig'));assets={'KitaoPhotoSelector.dll','RAWSelectionAssistant.Core.dll'}
for target in j['targets'].values():
 for lib in target.values():
  assets.update(Path(x).name for x in lib.get('runtime',{}))
  assets.update(Path(x).name for x,v in lib.get('runtimeTargets',{}).items() if v.get('assetType')=='runtime')
rows=[]
for n in sorted(assets):
 f=app/n
 if not f.is_file() or f.suffix.lower()!='.dll':continue
 b=f.read_bytes()
 if not managed(b):continue
 rows.append((n,f,hashlib.sha256(b).hexdigest(),len(b)))
assert all(x[0].lower() not in ['raw_r.dll','libraw.dll'] for x in rows)
assert {'KitaoPhotoSelector.dll','RAWSelectionAssistant.Core.dll'}.issubset({x[0] for x in rows})
out.mkdir(parents=True,exist_ok=False)
for n,f,h,length in rows:shutil.copyfile(f,out/n)
for suffix in ['.dll','.deps.json','.runtimeconfig.json']:shutil.copyfile(a.host_build/('ColorZeroRawResourceHost20261117'+suffix),out/('ColorZeroRawResourceHost20261117'+suffix))
shutil.copyfile(a.resources,out/'ProductAppResources.xaml')
(out/'MANAGED_BINDING.json').write_text(json.dumps({'managedOnlyStage':True,'rawNativeCopied':False,'productSourceMustBeVerifiedSeparately':True,'files':[{'name':n,'source':str(f),'sha256':h,'bytes':length} for n,f,h,length in rows]},indent=2)+chr(10),encoding='utf-8');print(json.dumps({'stage':str(out),'managedAssets':len(rows)}))
