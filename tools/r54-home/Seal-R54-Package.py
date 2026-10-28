"""Prepare or seal a complete already-published R54 diagnostic. Never overwrites a package/ZIP."""
from pathlib import Path
import argparse,hashlib,json,shutil,zipfile
p=argparse.ArgumentParser();p.add_argument('action',choices=['prepare','seal']);p.add_argument('--published',type=Path,required=True);p.add_argument('--validation',type=Path,required=True);p.add_argument('--package',type=Path,required=True);p.add_argument('--source-sha',required=True);a=p.parse_args()
if not all(x.is_absolute()for x in [a.published,a.validation,a.package]):raise ValueError('Absolute paths required')
def h(path):
 with path.open('rb')as stream:return hashlib.file_digest(stream,'sha256').hexdigest()
def js(path,value):path.write_text(json.dumps(value,ensure_ascii=False,indent=2),encoding='utf8')
if a.action=='prepare':
 if a.package.exists():raise FileExistsError(a.package)
 build=json.loads((a.published.parent/'publish-build.json').read_text());assert build['sourceSha']==a.source_sha and build['exitCode']==0
 shutil.copytree(a.published,a.package/'app')
 for name in ['Start-Guangjing-Diagnostic.cmd','Start-Guangjing-Diagnostic.bat','Start-Guangjing-Diagnostic.ps1','Prepare-R54-Synthetic.ps1']:shutil.copy2(Path(__file__).parent/name,a.package/name)
 sample=a.package/'samples/graph-original-evidence';sample.mkdir(parents=True)
 for name in ['synthetic.png','independent.ptstudio.json','expected.json','readback.json']:shutil.copy2(a.published.parent/'independent'/name,sample/name)
 (a.package/'README-DIAGNOSTIC.md').write_text("# R54 家庭完整诊断版 — 不能发行\n产品源码："+a.source_sha+"\nWholeAppDiagnosticBuild=true / PhotographerTrialBuild=false / win-x64 self-contained。全部模块及策划15保留，旧教程停用。CMD每次新建隔离根；要复用根，PowerShell7运行PS1 -DataRoot <绝对目录>。不会打开浏览器或资源管理器。\n合成项目请用Prepare-R54-Synthetic.ps1 -Destination <新的绝对目录>重绑源路径，再在暗室打开生成项目。原始合成证据路径只作追溯，不冒称直接可迁移。\n原UI5.4本机实体缺失，未做最终定稿逐项验收；宝丽来新候选未注册，实测0；物理鼠标/DPI/ACM/HDR未验。内存账本不是进程硬上限。详见validation/FINAL_REPORT.md和MANUAL_RETEST.md。无私人照片/RAW/UserData/研究二进制；不是Setup。\n",encoding='utf8')
 shutil.copy2(a.validation/'MANUAL_RETEST.md',a.package/'MANUAL_RETEST.md')
 print(a.package)
else:
 assert a.package.is_dir()and not(a.package/'UserData').exists()
 for source in a.published.rglob('*'):
  if source.is_file():assert h(source)==h(a.package/'app'/source.relative_to(a.published)),source
 # Check every declared managed/native runtime asset, preserving manifest semantics.
 deps=json.loads((a.package/'app/KitaoPhotoSelector.deps.json').read_text());declared=[];missing=[]
 for target,libs in deps['targets'].items():
  for lib,groups in libs.items():
   for kind in ['runtime','native','resources']:
    for name,metadata in groups.get(kind,{}).items():
     rel=Path(name);candidate=a.package/'app'/rel
     if not candidate.is_file():candidate=a.package/'app'/(rel.name if kind!='resources' else str(Path(metadata.get('locale',rel.parent.name))/rel.name))
     declared.append(dict(library=lib,kind=kind,path=name,resolved=str(candidate.relative_to(a.package))))
     if not candidate.is_file():missing.append(name)
 assert not missing,missing
 assert (a.package/'app/THIRD-PARTY-NOTICES.md').is_file()
 assert (a.package/'app/Licenses/LibRaw-0.21.1-LICENSE.LGPL.txt').is_file()
 assets=dict(declaredCount=len(declared),missing=missing,assets=declared,sourceSha=a.source_sha)
 js(a.validation/'RUNTIME_ASSETS.json',assets)
 excluded={'ZIP_IDENTITY.json','ZIP_FILES.json','PUSH_READBACK.json','FINAL_REMOTE_READBACK.json'}
 if(a.package/'validation').exists():raise FileExistsError('validation already sealed')
 for source in a.validation.rglob('*'):
  if source.is_file()and source.name not in excluded:
   dest=a.package/'validation'/source.relative_to(a.validation);dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source,dest)
 files=sorted(x for x in a.package.rglob('*')if x.is_file())
 for x in files:
  assert x.suffix.lower()not in ['.cr3','.arw','.nef','.db','.sqlite','.pdb','.ptlibrary'],x
  assert not any(t.lower()in ['userdata','research','reference','runtime']for t in x.relative_to(a.package).parts),x
 manifest=[dict(path=x.relative_to(a.package).as_posix(),bytes=x.stat().st_size,sha256=h(x))for x in files]
 js(a.validation/'ZIP_FILES.json',manifest);zpath=a.package.with_suffix('.zip')
 with zipfile.ZipFile(zpath,'x',zipfile.ZIP_DEFLATED,6)as z:
  for path,row in zip(files,manifest):z.write(path,a.package.name+'/'+row['path'])
 with zipfile.ZipFile(zpath)as z:
  assert z.testzip()is None and len(z.namelist())==len(files)
  for row in manifest:assert hashlib.sha256(z.read(a.package.name+'/'+row['path'])).hexdigest()==row['sha256']
 identity=dict(path=str(zpath),bytes=zpath.stat().st_size,sha256=h(zpath),sourceSha=a.source_sha,files=len(files),appFiles=sum(x.is_file()for x in (a.package/'app').rglob('*')),declaredRuntimeAssets=len(declared),missingRuntimeAssets=0,allEntriesReadBack=True,diagnosticOnly=True,isInstaller=False,UserDataIncluded=False,privatePhotosIncluded=False,researchBinaryIncluded=False)
 js(a.validation/'ZIP_IDENTITY.json',identity);print(json.dumps(identity,ensure_ascii=False,indent=2))
