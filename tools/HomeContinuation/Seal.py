"""Seal a source-bound diagnostic package without runtime data or credentials."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,re,shutil,subprocess,zipfile
repo=Path(__file__).resolve().parents[2];a=repo/'artifacts/home-resume-final-20261122';d=repo/'docs/validation/home-acceptance-20261122';c=json.loads((a/'BUILD_CONTEXT.json').read_text('utf-8-sig'));package=Path(c['package']);archive=package.parent/(package.name+'-win-x64.zip')
def load(p):return json.loads(p.read_text('utf-8-sig'))
def save(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+chr(10),encoding='utf-8')
def sha(p):
 with p.open('rb') as stream:return hashlib.file_digest(stream,'sha256').hexdigest()
assert not archive.exists(),'Preserve an already sealed archive'
assert not subprocess.check_output(['git','-C',str(repo),'diff','--name-only',c['productSource'],'--','src','tests']).strip(),'Product differs from frozen source'
assert load(d/'REGRESSION.json')['passed']==60 and all(load(d/'HTTP_RESULT.json')['checks'].values())
for row in load(a/'COMPONENT_BINDINGS.json'):
 assert row['version']=='2.3.0+'+c['productSource'] and sha(package/row['path'])==row['sha256']
shutil.copytree(repo/'extensions/guangjing-chrome',package/'extension',dirs_exist_ok=True)
embedded=package/'diagnostics-reports/home-acceptance-20261122';embedded.mkdir(parents=True,exist_ok=True)
for path in d.iterdir():
 if path.is_file() and path.name not in ['DELIVERY.json','PACKAGE_VALIDATION.json','EVIDENCE_INDEX.json','REMOTE_READBACK.json'] and not path.name.startswith('.'):shutil.copyfile(path,embedded/path.name)
(package/'README.md').write_text('''# 光镜家庭接续完整诊断包

产品源码：'''+c['productSource']+'''

运行 Start-FullProduct-LocalTest.cmd，启动完整桌面及独立的本地选片服务。默认数据根位于 LOCALAPPDATA/KitaoDiagnostic/OnlineHttpV1-'''+c['productSource'][:8]+'''；可用 PowerShell 脚本的 -DataRoot 参数指定独立测试根。无需安装器，不自动打开网页、文件管理器或注册扩展。App、SelectionService、ScheduleService、NativeHost 与原有扩展均保留。

60 项定向测试、真实 HTTP25 项、补名/空白零副作用的受控宿主检查通过。家庭合成40MP普通EXE通过仍绑定 d07，最终源码普通EXE JPEG全流程与 iPhone双向操作待人工验收。详见 diagnostics-reports/home-acceptance-20261122/REPORT.md。此包是诊断预览，不能标为可发行版。

家庭旧预约和新手机凭据位于包外。运行数据、授权链接、token、原始照片、工作文件和数据库均未打包。
''',encoding='utf-8')
privateFiles=[repo/'artifacts/home-20261122/PRIVATE_SELECTION.json',repo.parent/'guangjing-product-functional-home-20261115/artifacts/home-20261115/PRIVATE_CONNECTION.json',a/'phone-private/DEVICE_PRIVATE.json']
secret=[]
for f in privateFiles:
 if not f.exists():continue
 def collect(v):
  if isinstance(v,dict):
   for k,x in v.items():
    if isinstance(x,str) and any(t in k.lower() for t in ['token','ownerkey']) and len(x)>=24:secret.append(x.encode())
    if isinstance(x,str) and '#token=' in x:secret.append(x.split('#token=',1)[1].split('&',1)[0].encode())
    collect(x)
  elif isinstance(v,list):
   for x in v:collect(x)
 collect(load(f))
home=load(repo.parent/'guangjing-product-functional-home-20261115/artifacts/home-20261115/CONTEXT.json');secret.append((Path(home['runtime'])/'schedule-owner.key').read_bytes().strip())
files=[];texttypes={'.json','.jsonl','.md','.txt','.ps1','.py','.cmd','.csv','.trx','.xml','.js','.cjs','.mjs','.css','.html','.config','.log'}
for file in sorted(package.rglob('*')):
 if not file.is_file() or file.name=='PACKAGE_FILES.json':continue
 rel=file.relative_to(package).as_posix();assert file.suffix.lower() not in {'.db','.sqlite','.sqlite3','.key','.raf','.arw','.cr3','.rgb48','.nettrace','.dmp','.ptstudio'},'Unexpected runtime file: '+rel
 assert not any(k in file.name.upper() for k in ['_PRIVATE','PHONE_URL','PRIVATE_CONNECTION']),'Private artifact in package'
 if file.suffix.lower() in texttypes:
  data=file.read_bytes();assert not any(value and value in data for value in secret),'Credential in packaged text: '+rel
  assert not re.search(rb'https://login[.]tailscale[.]com/a/[A-Za-z0-9_-]{8,}',data),'Private login URL in package'
 if file.suffix.lower() in {'.cmd','.ps1'}:
  text=file.read_text('utf-8-sig');assert '--open' not in text and not re.search(r"Start-Process\s+(?:https?://|['\"]https?://)|start\s+http",text,re.I),'Automatic browser launch in package'
 files.append({'path':rel,'bytes':file.stat().st_size,'sha256':sha(file)})
for path in (repo/'extensions/guangjing-chrome').rglob('*'):
 if path.is_file():assert sha(path)==sha(package/'extension'/path.relative_to(repo/'extensions/guangjing-chrome'))
manifest={'productSource':c['productSource'],'fullModuleDiagnostic':True,'privateRuntimeBundled':False,'credentialsBundled':False,'installer':False,'files':files};save(package/'PACKAGE_FILES.json',manifest)
with zipfile.ZipFile(archive,'x',zipfile.ZIP_DEFLATED,compresslevel=6) as zipped:
 for file in sorted(package.rglob('*')):
  if file.is_file():zipped.write(file,package.name+'/'+file.relative_to(package).as_posix())
with zipfile.ZipFile(archive) as zipped:
 assert zipped.testzip() is None and len(zipped.infolist())==len(files)+1
 assert json.loads(zipped.read(package.name+'/PACKAGE_FILES.json'))==manifest
 for row in files:
  assert sha(package/row['path'])==row['sha256']
  with zipped.open(package.name+'/'+row['path']) as stream:assert hashlib.file_digest(stream,'sha256').hexdigest()==row['sha256']
delivery={'atUtc':datetime.now(timezone.utc).isoformat(),'clientTimeZone':c['clientTimeZone'],'status':'SEALED_FULL_DIAGNOSTIC_MANUAL_ACCEPTANCE_PENDING','productSource':c['productSource'],'branch':'home/d07-ordinary-acceptance-20261122','package':str(package),'entry':str(package/'Start-FullProduct-LocalTest.cmd'),'archive':str(archive),'archiveBytes':archive.stat().st_size,'archiveSha256':sha(archive),'manifestFiles':len(files),'archiveFiles':len(files)+1,'fourCompiledComponentsSourceBound':True,'regressionPassed':60,'actualHttpChecksPassed':25,'ordinaryHomeSynthetic40MpSource':'d07e4f4370c8e7d8505a46e3085cc6a069fb4bd4','ordinaryHomeSynthetic40Mp':'PASS_ORIGINAL_SOURCE','ordinaryFinalJpegWorkflow':'NOT_RUN','actualIPhone':'NOT_RUN','phoneHttpsAndDevice':'READY','credentialsAndRuntimeExcluded':True,'installer':False,'releaseReady':False,'newRawDecodes':0,'forcedGc':False,'embeddedReportIsPreSealSnapshot':True}
validation={'status':'PASS_ARCHIVE_BYTES_VERSIONS_AND_KNOWN_SECRET_EXCLUSION','productSource':c['productSource'],'archiveSha256':delivery['archiveSha256'],'checks':{'allEightComponentVersionsMatch':True,'everyArchivedMemberSha256Verified':True,'manifestCoverageVerified':True,'zipCrcVerified':True,'extensionMatchesSource':True,'knownCredentialsExcluded':True,'runtimeExcluded':True,'noAutomaticBrowserLauncher':True},'ordinaryFinalJpegPassed':False,'physicalIPhonePassed':False}
for p in [d/'DELIVERY.json',repo/'docs/validation/root-cause-fixes-20261117/DELIVERY.json',a/'DELIVERY.json']:save(p,delivery)
save(d/'PACKAGE_VALIDATION.json',validation);save(a/'PACKAGE_VALIDATION.json',validation)
print(json.dumps({'status':delivery['status'],'manifestFiles':len(files),'archiveBytes':delivery['archiveBytes'],'archiveSha256':delivery['archiveSha256']}))
