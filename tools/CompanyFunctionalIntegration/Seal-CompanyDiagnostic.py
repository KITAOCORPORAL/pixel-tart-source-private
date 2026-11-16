"""Seal one frozen company diagnostic and verify every archive member.
Credentials are read solely for exclusion checks and are never serialized.
"""
from pathlib import Path
from datetime import datetime,timezone
import argparse,hashlib,json,re,shutil,subprocess,urllib.parse,zipfile
p=argparse.ArgumentParser();p.add_argument('--repo',type=Path,required=True);p.add_argument('--delivery-root',type=Path,required=True);p.add_argument('--source',required=True);p.add_argument('--refresh',action='store_true');a=p.parse_args()
repo=a.repo.resolve();base=a.delivery_root.resolve();source=a.source
package=base/('Guangjing-Company-Integrated-'+source[:8]+'-win-x64');docs=repo/'docs/validation/company-functional-integration-20261116';archive=base/(package.name+'-20261116.zip')
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def save(p,o):Path(p).write_text(json.dumps(o,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
def git(*args):return subprocess.check_output(['git',*args],cwd=repo,text=True,encoding='utf-8').strip()
assert len(source)==40 and git('rev-parse',source)==source
assert git('diff','--name-only',source,'HEAD','--','src','tests')==''
assert git('diff','--name-only',source,'--','src','tests')==''
if archive.exists():
 assert a.refresh,'Preserve existing sealed archives unless explicit verified refresh'
 previous_receipt=read(base/'PACKAGE_DELIVERY.json');assert sha(archive)==previous_receipt['archiveSha256']
 previous_manifest=read(package/'PACKAGE_FILES.json')
 for row in previous_manifest['files']:
  target=(package/row['path']).resolve();assert target.is_relative_to(package.resolve()) and sha(target)==row['sha256']
 backup=archive.with_name(archive.stem+'-pre-canonical-text.zip')
 assert archive.resolve().parent==base and backup.resolve().parent==base and not backup.exists()
 archive.rename(backup)
# Canonical LF makes committed text receipts reproducible from the recorded evidence hashes.
for folder in [docs,repo/'tools/CompanyFunctionalIntegration']:
 for file in folder.rglob('*'):
  if file.is_file() and (file.suffix.lower() in {'.json','.trx','.md','.py','.ps1','.txt'} or file.name=='.gitattributes'):
   data=file.read_bytes();canonical=data.replace(b'\r\n',b'\n')
   if data!=canonical:file.write_bytes(canonical)
assert read(package/'BUILD_IDENTITY.json')['productSourceSha']==source
assert read(docs/'CURRENT_RECEIPT.json')['source']==source
assert read(docs/'TESTS.json')['passed']==39 and read(docs/'TESTS.json')['failed']==0
for row in read(docs/'COMPONENT_BINDINGS.json'):
 assert row['productVersion']=='2.3.0+'+source and sha(package/row['path'])==row['sha256']
assert (package/'App/KitaoPhotoSelector.exe').read_bytes()==(package/'App/GuangjingCompanyBatch.exe').read_bytes()==(package/'App/GuangjingCompanyCalendar.exe').read_bytes()
for row in read(docs/'ACTUAL_WORKFLOW.json')['outputs']:
 assert sha(row['file'])==row['sha256'] and sha(row['source'])==row['sourceHash']
postseal={'PACKAGE_DELIVERY.json','PACKAGE_VALIDATION.json','POSTSEAL_INDEX.json','REMOTE_READBACK.json','EVIDENCE_INDEX.json'}
entries=[]
for file in sorted(docs.rglob('*')):
 if file.is_file() and not(file.parent==docs and file.name in postseal):entries.append(dict(file=file.relative_to(docs).as_posix(),bytes=file.stat().st_size,sha256=sha(file)))
save(docs/'EVIDENCE_INDEX.json',dict(productSource=source,atUtc=datetime.now(timezone.utc).isoformat(),authority='Historical49 native sequence and final-source observed/read-only checks retain separate source labels',artifacts=entries))
for folder in [docs,repo/'docs/validation/tiff-srgb-company-20261116']:
 target=package/'diagnostics-reports'/folder.name
 assert a.refresh or not target.exists(),'Preserve a prior staged report'
 target.mkdir(parents=True,exist_ok=a.refresh)
 for file in folder.rglob('*'):
  if not file.is_file() or (file.parent==docs and file.name in postseal-{'EVIDENCE_INDEX.json'}):continue
  dest=target/file.relative_to(folder);dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(file,dest)
home=package/'diagnostics-reports/functional-closure-home-20261115';home.mkdir(parents=True,exist_ok=True)
for name in ['CURRENT_REPORT.md','REMAINING_BLOCKERS.json']:shutil.copyfile(repo/'docs/validation/functional-closure-home-20261115'/name,home/name)
helper=package/'diagnostics/company-acceptance';assert a.refresh or not helper.exists();shutil.copytree(repo/'tools/CompanyFunctionalIntegration',helper,dirs_exist_ok=a.refresh)
for name in ['Run-AppGuard.py','Run-Service.py']:shutil.copyfile(repo/'tools/CompanyFunctionalIntegration'/name,package/'diagnostics'/name)
for folder,name in [('FunctionalClosureAcceptance','Continue-CompanyMobile.py'),('MobileScheduleAcceptance','BrowserTimeZoneObserver.py')]:
 dest=package/'diagnostics'/folder;dest.mkdir(exist_ok=True);shutil.copyfile(repo/'tools'/folder/name,dest/name)
(package/'Continue-CompanyPhone.cmd').write_text('@echo off\n"C:\\Program Files\\Python313\\python.exe" "%~dp0diagnostics\\FunctionalClosureAcceptance\\Continue-CompanyMobile.py" --context "E:\\Kitao-Deliveries\\mobile-schedule-company-20261112\\MOBILE_COMPANY_CONTEXT.json" --apply\n',encoding='utf-8',newline='\r\n')
identity=read(package/'BUILD_IDENTITY.json');identity.update(hiddenPreviewFix=source,ordinaryCompany40MpTiff='PREVIOUS_A993_STOP_FINAL_NATIVE_ACCEPTANCE_PENDING',completeFinalNativeSequence='PARTIAL_SEE_CURRENT_REPORT',previousCompleteNativeWorkflowSource='49fbc2f6d47e3bacabb34878e89e37e33523939e',iPhone='NOT_RUN_NEEDS_PERSONAL_TAILSCALE_LOGIN',targetedChecksPassed=39);save(package/'BUILD_IDENTITY.json',identity)
(package/'README-COMPANY.md').write_text('# Company full-module diagnostic\n\nFrozen functional source: '+source+'\n\nStart-FullProduct-LocalTest.cmd reuses the existing company synthetic batch. Original-company, schedule-service and phone-calendar entries keep their separate external runtimes. Paths in company-paths.json are specific to this company machine.\n\nFinal39 targeted tests passed. Historical49 complete native JPEG flow, final-source checks, source-rescan receipts and remaining native/iPhone/40MP gaps are recorded separately in diagnostics-reports/company-functional-integration-20261116/CURRENT_REPORT.md. This is a diagnostic preview and is not release-ready.\n\nPhone service, original appointment and current device are prepared. Personally sign into Tailscale with the fresh link supplied outside this package, then Continue-CompanyPhone.cmd verifies the original tailnet/company host, configures the dedicated private HTTPS endpoint, and writes the private device URL only to the existing runtime. No iPhone roundtrip has yet passed.\n\nNo credentials, login links, runtime databases or source photographs are bundled. No installer is generated.\n',encoding='utf-8',newline='\n')
secret=[]
for path in [base/'batch-01/PRIVATE_CONNECTION.json',Path('E:/Kitao-Deliveries/product-functional-closure-20261112/SELECTION_CONNECTION_PRIVATE.json')]:
 d=read(path)
 for k,v in d.items():
  if k.lower() in ('ownerkey','token') and isinstance(v,str) and v:secret.append(v.encode())
m=read('E:/Kitao-Deliveries/mobile-schedule-company-20261112/MOBILE_COMPANY_CONTEXT.json');runtime=Path(m['runtimeRoot'])
secret.extend([read(runtime/'PHONE_DEVICE_PRIVATE.json')['token'].encode(),(runtime/'schedule-owner.key').read_bytes().strip()])
for path in [base/'phone-private-backup/PHONE_DEVICE_PRIVATE.json']:
 if path.exists():secret.append(read(path)['token'].encode())
if (runtime/'PHONE_BROWSER_OBSERVER_PRIVATE.json').exists():secret.append(read(runtime/'PHONE_BROWSER_OBSERVER_PRIVATE.json')['nonce'].encode())
for path in [base/'batch-01/CUSTOMER_URL_PRIVATE.txt']:
 parts=urllib.parse.urlsplit(path.read_text('utf-8-sig').strip())
 for segment in [parts.query,parts.fragment]:
  for values in urllib.parse.parse_qs(segment).values():secret.extend(v.encode() for v in values if len(v)>15)
texttypes={'.json','.jsonl','.md','.txt','.ps1','.py','.cmd','.csv','.trx','.xml','.js','.mjs','.css','.html','.config'}
files=[]
for file in sorted(package.rglob('*')):
 if not file.is_file() or file.name=='PACKAGE_FILES.json':continue
 rel=file.relative_to(package).as_posix()
 assert file.suffix.lower() not in {'.db','.sqlite','.sqlite3','.rgb48','.nettrace','.dmp','.key','.raf','.arw','.cr3'},'Runtime artifact in package: '+rel
 assert not any(term in file.name.upper() for term in ['_PRIVATE','PHONE_URL','LOGIN_PRIVATE']),'Private file in package: '+rel
 if file.suffix.lower() in texttypes:
  data=file.read_bytes();assert not re.search(rb'https://login[.]tailscale[.]com/a/[A-Za-z0-9_-]{8,}',data) and not any(value and value in data for value in secret),'Sensitive value in package: '+rel
 files.append(dict(path=rel,bytes=file.stat().st_size,sha256=sha(file)))
for file in (repo/'extensions/guangjing-chrome').rglob('*'):
 if file.is_file():assert sha(file)==sha(package/'extension'/file.relative_to(repo/'extensions/guangjing-chrome'))
for file in (repo/'tools/CompanyFunctionalIntegration').glob('*.py'):compile(file.read_text('utf-8-sig'),str(file),'exec')
manifest=dict(productSource=source,homeBase='0a45ee0e7971c18c6bb4b7e287bced86c448263b',fullModuleDiagnostic=True,privateRuntimeBundled=False,credentialsBundled=False,installer=False,files=files);save(package/'PACKAGE_FILES.json',manifest)
with zipfile.ZipFile(archive,'x',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
 for file in sorted(package.rglob('*')):
  if file.is_file():z.write(file,package.name+'/'+file.relative_to(package).as_posix())
with zipfile.ZipFile(archive) as z:
 assert z.testzip() is None and len(z.infolist())==len(files)+1
 assert json.loads(z.read(package.name+'/PACKAGE_FILES.json'))==manifest
 for row in files:
  assert sha(package/row['path'])==row['sha256']
  with z.open(package.name+'/'+row['path']) as stream:assert hashlib.file_digest(stream,'sha256').hexdigest()==row['sha256']
delivery=dict(atUtc=datetime.now(timezone.utc).isoformat(),clientTimeZone='Europe'+'/'+'Helsinki',status='SEALED_FULL_MODULE_DIAGNOSTIC_PRODUCT_PARTIAL',productSource=source,package=str(package),entry='Start-FullProduct-LocalTest.cmd',archive=str(archive),archiveBytes=archive.stat().st_size,archiveSha256=sha(archive),manifestListedFiles=len(files),archiveFiles=len(files)+1,fourCompiledComponentsSourceBound=True,homeSourcePolicyInPackage=True,memoryCommitsInPackage=['edd6d25e via17a95b5a','e28074f1 via14eda5c7'],hiddenPreviewFixInPackage=source,companyPathsRegenerated=True,targetedRegressionPassed=39,previousCompleteNativeWorkflowSource='49fbc2f6d47e3bacabb34878e89e37e33523939e',finalCompleteNativeWorkflow='PARTIAL_SEE_REPORT',actualIPhonePassed=False,ordinaryCompany40MpTiffPassed=False,credentialsAndRawRuntimeExcluded=True,setupInstaller=False,releaseReady=False,postSealReceiptNotEmbeddedInOwnArchive=True)
for path in [docs/'PACKAGE_DELIVERY.json',base/'PACKAGE_DELIVERY.json',base/'CURRENT_DELIVERY.json']:save(path,delivery)
save(docs/'PACKAGE_VALIDATION.json',dict(atUtc=delivery['atUtc'],source=source,status='PASS_ARCHIVE_COMPONENT_VERSION_DIGEST_AND_SECRET_EXCLUSION',archiveSha256=delivery['archiveSha256'],checks=dict(crcVerified=True,everyArchivedMemberSha256Verified=True,manifestCoverageVerified=True,compiledExeDllVersionsVerified=True,apphostAliasesIdentical=True,extensionMatchesFrozenSource=True,tests39Passed=True,sourceFixAndMemoryIdentityRetained=True,privateRuntimeAndCredentialsExcluded=True),nativeFinalSequence='PARTIAL_SEE_CURRENT_REPORT',actualIPhonePassed=False,ordinaryCompany40MpTiffPassed=False,releaseReady=False))
print(json.dumps(delivery))
