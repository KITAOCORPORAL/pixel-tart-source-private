"""Assemble one-source functional/memory diagnostics with explicit native and guard gaps."""
from pathlib import Path
from datetime import datetime,timezone,timedelta
from collections import Counter
import argparse,csv,json,hashlib,shutil,sqlite3,subprocess,urllib.request,urllib.error,xml.etree.ElementTree as ET
p=argparse.ArgumentParser();p.add_argument('--context',required=True);a=p.parse_args()
c=json.loads(Path(a.context).read_text('utf-8-sig'));r=Path(c['worktree']);b=Path(c['deliveryRoot']);d=r/c['evidenceDirectory'];rec=d/'receipts';rec.mkdir(parents=True,exist_ok=True);prior=r/'docs/validation/product-functional-closure-20261112'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def save(p,o):Path(p).parent.mkdir(parents=True,exist_ok=True);Path(p).write_text(json.dumps(o,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def copy(p,q):Path(q).parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,q)
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def git(*args):return subprocess.check_output(['git',*args],cwd=r,text=True,encoding='utf-8').strip()
now=datetime.now(timezone(timedelta(hours=1))).isoformat();ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
if git('diff','--name-only',c['currentProductSource'],'--','src','extensions'):raise RuntimeError('Product inputs differ from frozen source')
for entry in read(prior/'EVIDENCE_INDEX.json')['artifacts']:
 f=prior/entry['file']
 if sha(f)!=entry['sha256']:raise RuntimeError('Prior receipt integrity mismatch: '+entry['file'])
# Copy the prior bounded evidence with its old source labels intact.
legacy_copy=rec/'reused-r2'
if legacy_copy.exists():
 if legacy_copy.resolve().parent!=rec.resolve():raise RuntimeError('Unexpected generated evidence root')
 for f in legacy_copy.rglob('*'):
  if f.is_file():
   original=prior/'receipts'/f.relative_to(legacy_copy)
   if not original.is_file() or sha(f)!=sha(original):raise RuntimeError('Preserve changed evidence: '+str(f))
 for f in legacy_copy.rglob('*'):
  if f.is_file():f.unlink()
 for folder in sorted((f for f in legacy_copy.rglob('*') if f.is_dir()),key=lambda p:len(p.parts),reverse=True):folder.rmdir()
 legacy_copy.rmdir()
reused_map={}
for f in (prior/'receipts').rglob('*'):
 if f.is_file() and f.name!='.gitattributes':
  dest=rec/'r2'/(sha(f)[:12]+'-'+f.name);copy(f,dest);reused_map[f.relative_to(r).as_posix()]=dest.relative_to(r).as_posix()
save(rec/'R2_REUSE_MAP.json',{'scope':'Exact byte-preserving R2 receipts; short hash paths avoid Windows extraction/index limits','files':reused_map})
expected={'booking':61,'navigation':9,'memory':5,'detail':2,'tiff-contract':3,'raster-cache':5,'overlay-history':1};qa=[]
for name,count in expected.items():
 f=b/c.get('finalQaFolder','final-qa')/name/'results.trx';results=ET.parse(f).findall('.//t:UnitTestResult',ns)
 if len(results)!=count or any(x.get('outcome')!='Passed' for x in results):raise RuntimeError('Final regression mismatch: '+name)
 dest=rec/'final-qa'/name/'results.trx';copy(f,dest);qa.append({'name':name,'passed':count,'file':dest.relative_to(r).as_posix(),'sha256':sha(f)})
for folder in ['final-memory-cycle-natural','final-memory-minimized-natural']:
 root=b/folder;guard=root.with_name(root.name+'-guard');exit=read(guard/'EXIT.json')
 if exit['exitCode']!=0 or exit['stop'] is not None:raise RuntimeError('Cache replay did not finish: '+folder)
 for f in root.glob('*.json'):copy(f,rec/'cache-replay'/folder/f.name)
 for name in ['LAUNCH.json','EXIT.json']:copy(guard/name,rec/'cache-replay'/folder/name)
resource_rows=[]
for label,folder in [('缓存完整周期','final-memory-cycle-natural'),('缓存最小化210秒','final-memory-minimized-natural'),('真实TIFF非视觉导出','final-tiff-model-before'),('真实TIFF独立重开导出','final-tiff-model-reopen'),('真实TIFF视口导出失败','rendered-tiff-export-reuse-before')]:
 value=read(b/(folder+'-guard')/'EXIT.json');resource_rows.append({'case':label,'privateMiB':round(value['peakPrivateBytes50ms']/1048576,2),'osPeakWorkingMiB':round(value['osPeakWorkingSetBytes']/1048576,2),'durationSeconds':round(value['elapsedMs']/1000,2),'stop':value['stop'],'exitCode':value['exitCode'],'newRawDecodes':0})
save(rec/'RESOURCE_SUMMARY.json',{'guardBytes':2**31,'rows':resource_rows,'authority':'Whole-process50ms samples; cache replay and nonvisual real-TIFF are not native/visual entry acceptance.'})
with (d/'RESOURCE_SUMMARY.csv').open('w',encoding='utf-8',newline='') as output:
 writer=csv.DictWriter(output,fieldnames=list(resource_rows[0]),lineterminator='\n');writer.writeheader();writer.writerows(resource_rows)
copy(b/'TIFF_PIPELINE_COMPARISON.json',rec/'TIFF_PIPELINE_COMPARISON.json');tiff=read(b/'TIFF_PIPELINE_COMPARISON.json')
if not all(tiff['checks'].values()) or not tiff['inputToOutputRgb48PixelMatch']:raise RuntimeError('Real TIFF file consistency failed')
# The visual guard stops remain failures, independently of successful nonvisual IO.
visual=[]
for folder in ['rendered-tiff-before','rendered-tiff-fixed-before','rendered-tiff-deferred-before','rendered-tiff-export-reuse-before']:
 root=b/folder;guard=root.with_name(root.name+'-guard');exit=read(guard/'EXIT.json');visual.append({'run':folder,**exit})
 for name in ['LAUNCH.json','EXIT.json','STOP.json']:
  if (guard/name).is_file():copy(guard/name,rec/'visual-guard-stops'/folder/name)
 if (root/'events.jsonl').is_file():copy(root/'events.jsonl',rec/'visual-guard-stops'/folder/'events.jsonl')
save(rec/'VISUAL_TIFF_GUARD_FAILURES.json',{'status':'BLOCKED_2GIB_DURING_VISUAL_TIFF_EXPORT','runs':visual,'limitBytes':2**31,'notPromotedByNonvisualPass':True})
for folder in ['final-tiff-model-before','final-tiff-model-reopen']:
 copy(b/folder/'RENDERED_TIFF_RESULT.json',rec/'real-tiff-model'/folder/'RESULT.json')
 copy(b/(folder+'-guard')/'EXIT.json',rec/'real-tiff-model'/folder/'EXIT.json')
for name in ['input','before','reopen']:copy(b/('final-tiff-probe-'+name)/'TIFF_PROBE.json',rec/'real-tiff-model'/('PROBE_'+name+'.json'))
for native_file in (b/'native-resume').glob('*.json'):
 value=read(native_file)
 if isinstance(value,dict) and value.get('authority')=='Normal published EXE via Open Computer Use supported CLI':copy(native_file,rec/'native'/native_file.name)
worker=Path('E:/Guangjing-Product-Memory-Closure-20261113');copy(worker/'tests-memory/memory.trx',rec/'coordination/worker-memory.trx');copy(worker/'tests-workflow/workflow.trx',rec/'coordination/worker-workflow.trx');copy(worker/'native-ui-01/LAUNCH.json',rec/'coordination/worker-native-launch.json')
memory_paths=['src/RAWSelectionAssistant/Services/RawDisplayBitmapAdapter.cs','src/RAWSelectionAssistant/Services/StudioQuickExport.cs','src/RAWSelectionAssistant/ViewModels/ReferenceColorWorkspaceViewModel.cs','src/RAWSelectionAssistant/ViewModels/TetherReferenceModeViewModel.cs','src/RAWSelectionAssistant/Views/PhotoGradePageView.xaml.cs','src/RAWSelectionAssistant/Views/ReferenceColorWorkspaceView.xaml.cs']
if git('diff','--name-only','4c3503c9',c['currentProductSource'],'--',*memory_paths):raise RuntimeError('Memory evidence requires revalidation: relevant code changed')
save(rec/'MEMORY_SOURCE_BINDING.json',{'evidenceSource':'4c3503c90fc16d51d267a6edff90108c6cb93fb9','packageSource':c['currentProductSource'],'paths':memory_paths,'relevantDiffEmpty':True,'scope':'Later linked-calendar inspector correction changes planning only. Cache/TIFF4c evidence is reused at its exact controlled boundaries.'})
save(d/'COORDINATION.json',{'recordedAtBerlin':now,'workerThread':'01a1c4e4-698c-7be1-b838-4215ce046dc4','workerTitle':'继续独立色彩引擎研究','workerHandoffSource':c['memoryMergedSource'],'workerLaterStatus':'context_window_exhaustion','workerWorktreeUnmodified':True,'reusedWorkerChecks':6,'remainingChecksTakenOverHere':True,'memoryMinimumCommits':['17981934','2d20b327'],'followupAllocationRepairs':c['memoryFollowupRepairs'],'newRawDecodes':0,'guardBytes':2**31})
with sqlite3.connect((Path(c['workflowQaRuntime'])/'Data/pixel-tart.db').resolve().as_uri()+'?mode=ro',uri=True) as db:
 db.row_factory=sqlite3.Row;booking=dict(db.execute('select Id,ProjectId,Revision from ShootBookings where Id=?',(c['workflowBookingId'],)).fetchone());sources=db.execute('select count(*) from ProjectSources where ProjectId=?',(c['workflowProjectId'],)).fetchone()[0]
if booking['ProjectId']!=c['workflowProjectId']:raise RuntimeError('Original identity changed')
native_names=['939-history','939-planning','939-linked-calendar-actual','939-calendar-to-same-planning','939-original-project-loaded','939-source-picker-prepared']
for name in native_names:
 value=read(b/'native-resume'/(name+'.json'))
 if value.get('productSource')!=c['currentProductSource'] or value.get('pid')!=c['workflowAppPid']:raise RuntimeError('Native source/pid binding mismatch: '+name)
calendar_text=read(b/'native-resume/939-linked-calendar-actual.json')['accessibilityText']
if '拍摄排期详情' not in calendar_text or 'PlanningBookingPicker' in calendar_text:raise RuntimeError('Native linked action did not reach calendar')
save(rec/'NATIVE_MAIN_ROUTE_RECEIPT.json',{'status':'PASS_CURRENT_NORMAL_EXE_HISTORY_PLANNING_CALENDAR_RETURN','productSource':c['currentProductSource'],'pid':c['workflowAppPid'],'projectId':c['workflowProjectId'],'bookingId':c['workflowBookingId'],'revision':booking['Revision'],'snapshots':[str((rec/'native'/(name+'.json')).relative_to(r)) for name in native_names],'sourcePickerPrepared':True,'mainWorkflowAfterIndex':'NOT_RUN','authority':'Normal published EXE with supported native UIA actions; physical pointer not claimed'})
save(rec/'MAIN_PROJECT_CHECKPOINT.json',{'recordedAtBerlin':now,'projectId':c['workflowProjectId'],'booking':booking,'sourceRows':sources,'nativeAppSource':c['workflowAppProductSource'],'unifiedPackageSource':c['currentProductSource'],'step':'SOURCE_DIRECTORY_PICKER_PENDING','fullWorkflowCompleted':False,'next':'User selects the prepared synthetic-inputs/source-jpegs directory; then agent scans/saves and resumes on the unified binary without a replacement project.'})
mobile=read(c['mobileContext']);private=read(Path(mobile['runtimeRoot'])/'PHONE_DEVICE_PRIVATE.json');base=mobile['localBaseUrl'].rstrip('/')
def http(path,token=None):
 req=urllib.request.Request(base+path,headers={'Authorization':'Bearer '+token} if token else {})
 try:
  with urllib.request.urlopen(req,timeout=8) as response:return response.status,json.load(response)
 except urllib.error.HTTPError as e:return e.code,json.loads(e.read())
code,session=http('/v1/schedule/session',private['token']);bc,phone=http('/v1/schedule/bookings/'+mobile['bookingId'],private['token']);uc,_=http('/v1/schedule/session')
if code!=200 or bc!=200 or uc!=401 or phone['revision']!=mobile['prePhoneBooking']['revision']:raise RuntimeError('Existing phone preflight needs attention')
tail=json.loads(subprocess.check_output(['C:/Program Files/Tailscale/tailscale.exe','status','--json'],text=True,encoding='utf-8'))
save(rec/'PHONE_CURRENT.json',{'recordedAtBerlin':now,'status':'PASS_LOOPBACK_READONLY_PHONE_TRUE_DEVICE_PENDING','backendState':tail['BackendState'],'selfDns':tail.get('Self',{}).get('DNSName'),'phoneBookingId':mobile['bookingId'],'revision':phone['revision'],'existingCredentialAccepted':True,'anonymousRejected':True,'credentialExpiresAt':mobile['phoneDeviceExpiresAt'],'phoneTrueDeviceRoundtrip':'NOT_RUN','publicHttpsVerified':bool(mobile.get('phoneHttpsConfigured',False)),'requestsWereGetOnly':True})
entry=read(prior/'ENTRY_INVENTORY.json');entry.update(productSource=c['currentProductSource'],recordedAtBerlin=now);save(d/'ENTRY_INVENTORY.json',entry)
status=read(prior/'FUNCTION_STATUS.json');status.update(baseProductSource=c['currentProductSource'],inheritedProductSource='ea5b421b03329535a8834545ac3196dc5e4295e4',recordedAtBerlin=now)
for row in status['rows']:
 evidence=row.get('evidenceFiles',[])
 evidence=[reused_map.get(name,name) for name in evidence]
 row['inheritedEvidenceFiles']=evidence
 row['evidenceFiles']=[(rec/'MAIN_PROJECT_CHECKPOINT.json').relative_to(r).as_posix()] if row['id'] in ['CAL-01','PLAN-00','HIST-01','WF-02'] else []
 if row['id'] in ['CAL-01','PLAN-00','HIST-01']:row['evidenceFiles'].append((rec/'NATIVE_MAIN_ROUTE_RECEIPT.json').relative_to(r).as_posix())
 if row['id']=='DARK-01':row.update(status='存在阻塞',verifiedBoundary='真实授权TIFF非视觉自动管线完成导出和独立重开，RGB像素/16位每通道/ICC一致；保留视口的40MP TIFF导出仍触发2GiB守卫',remaining='普通EXE入口及完整视口资源验收未完成，不以非视觉结果替代');row['evidenceFiles'] +=[(rec/x).relative_to(r).as_posix() for x in ['TIFF_PIPELINE_COMPARISON.json','VISUAL_TIFF_GUARD_FAILURES.json']]
 if row['id'] in ['CANVAS-01','FILM-01','TETH-01','TETH-06','TOOL-01']:row['evidenceFiles'] +=[x['file'] for x in qa if x['name'] in ['memory','detail','navigation']]
 if row['id']=='CAL-02':row['evidenceFiles']=[(rec/'PHONE_CURRENT.json').relative_to(r).as_posix()]
 row['evidence']=';'.join(row['evidenceFiles']+row['inheritedEvidenceFiles'])
save(d/'FUNCTION_STATUS.json',status)
fields=['id','module','entry','status','verifiedBoundary','sourcePath','evidence','remaining']
with (d/'FUNCTION_STATUS.csv').open('w',encoding='utf-8',newline='') as stream:
 w=csv.DictWriter(stream,fieldnames=fields,extrasaction='ignore',lineterminator='\n');w.writeheader();w.writerows(status['rows'])
counts=dict(Counter(x['status'] for x in status['rows']));lines=['# 统一功能/内存状态表','',f'源码{c["currentProductSource"]}；{now}。按子流程和明确证据范围统计。','', '|模块 / ID|入口或子流程|状态|验证范围与剩余|','|---|---|---|---|']
for x in status['rows']:lines.append('|'+ '|'.join([x['module']+' / '+x['id'],x['entry'],x['status'],x['verifiedBoundary']+'；'+x['remaining']]).replace('\n',' ')+'|')
(d/'FUNCTION_STATUS.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
latest={}
for f in rec.rglob('*.trx'):
 if 'attempts' in f.parts:continue
 for x in ET.parse(f).findall('.//t:UnitTestResult',ns):
  key=x.get('testName');time=datetime.fromisoformat(x.get('endTime')).timestamp()
  if key not in latest or time>latest[key][0]:latest[key]=(time,x.get('outcome'))
save(rec/'FINAL_AUTOMATIC_CHECKS.json',{'productSource':c['currentProductSource'],'frozenCurrentPasses':sum(expected.values()),'runs':qa,'latestUniqueOutcomes':dict(Counter(v[1] for v in latest.values())),'scope':'Automatic/controlled checks; original native main project and real phone remain separately scoped.'})
save(d/'REMAINING_BLOCKERS.json',{'status':'PARTIAL','items':[{'id':'SOURCE_PICKER','action':'在已打开的选择照片来源目录中选择E:/Kitao-Deliveries/product-functional-closure-20261112/synthetic-inputs/source-jpegs，随后由代理继续索引、筛选、编辑、测试相册、收回、导出和独立重开。'},{'id':'TAILNET','state':tail['BackendState'],'action':'本人确认公司机入原网络，Running后执行Continue-CompanyMobile.py并核验HTTPS和原设备，再准备iPhone双端修改及最终重开。'},{'id':'VISUAL_TIFF_2GIB','action':'保留完整显示视口时的40MP高位深TIFF导出仍触发2GiB，不能标为全程通过。'},{'id':'CAMERA_AND_RAW_SCOPE','action':'厂商SDK/机身控制未实现，真实相机连接及RAW科学门禁仍按原缺项，不扩大自动检查结论。'}]})
receipt={'status':'PARTIAL_UNIFIED_CODE_AND_DIAGNOSTIC','recordedAtBerlin':now,'productSource':c['currentProductSource'],'functionalBase':'ea5b421b03329535a8834545ac3196dc5e4295e4','memoryMerge':c['memoryMergedSource'],'classificationCounts':counts,'frozenAutomaticPassed':sum(expected.values()),'latestUniqueAutomaticOutcomes':dict(Counter(v[1] for v in latest.values())),'newRawDecodes':0,'uiRedesignWithdrawn':True,'approvedGeneratedDrafts':0,'sameMainProject':c['workflowProjectId'],'sameBooking':c['workflowBookingId'],'mainSourceStep':'PENDING_DIRECTORY_SELECTION','phoneTrueDeviceRoundtrip':'NOT_RUN','realTiffModelFileConsistency':'PASS','realTiffVisual2GiBFlow':'BLOCKED','package':c['package'],'fullTaskCompleted':False};save(d/'CURRENT_RECEIPT.json',receipt)
text=f'''# 同一版本功能与内存诊断 · R3

{now}（Europe/Berlin）。状态 **PARTIAL**。统一功能源码 {c['currentProductSource']}，基线43781bfe/ea5b421b；保留预约跳转和历史刷新，整合内存17981934/2d20b327，并修复真实TIFF验证暴露的整帧分配/提前解码/导出重复读取，以及策划检查器关联档期按钮的错误命令路由。UI仍留待Claude，两张生成稿未采用；像素公式与既有recipe未改。

42个子流程：{counts}。10个主入口、策划15区均按原入口图保留；实际入口、自动宿主、缓存回放和真实文件IO分开。统一源码定向86项通过；原项目 {c['workflowProjectId']} 和预约 {c['workflowBookingId']} revision2保持。

## 主线接续

原workflow-runtime已在当前统一源码程序恢复，来源目录选择器仍待完成；字段仅pane，未强行注入来源或替换项目。选择synthetic-inputs/source-jpegs后由代理继续扫描并保存来源，在统一程序核验索引→筛选→编辑→发布测试相册→客户确认→PC收回→导出→独立重开。此前建项、排期关联策划、基础重开和历史入口按R2真实回执复用；原有MainWindow/MainViewModel预约/历史修复相对ea逐字节不变，本轮另修复策划检查器的关联日历路由。当前后半段未完成，不能拼接旧Safari相册为此新项目闭环。

## 内存与真实TIFF

内存Codex在独立树生成最小整合提交并留下5项内存测试及完整App回归回执；后续上下文上限后，本线保留其树并接手未完成部分。统一源码预约/历史和暗室/联机视口自动回归分别运行，新增RAW解码0。既有RGB48缓存回放在2GiB守卫下完成显式100%/缩放/保存关闭重开，以及210秒最小化自然空闲恢复，均按缓存注入边界解释，不当真实入口。

既有授权DSCF0347 TIFF已在非视觉生产ViewModel管线实际导出，并在另一个进程从保存工作文件重开再导出。两次RGB样本、尺寸、位深、ICC一致，输入文件未改；输入RGB48、输出RGBA64均16位/通道，附加alpha，归一RGB48像素与输入完全一致，ICC3144字节及SHA一致。固定文件对话框替代不等于普通EXE入口验收。

保留显示视口的40MP TIFF过程仍在导出附近触发2GiB。所有STOP与阶段数据保留，未增加限额、未把非视觉成功升为完整窗口通过。DARK-01更新为阻塞。未合并高光1bfdfbe9候选、新RAW后继授权或颜色算法研究项。

## 手机与硬件

公司Tailscale最后状态{tail['BackendState']}；现有手机凭据只读可用、匿名拒绝、预约revision4未变。本人入网后才按Continue-CompanyMobile.py核对原tailnet及公司Self DNS，配置HTTPS并验证，再给iPhone两端修改/最终App服务重开步骤。本轮尚未发生真机修改。

联机仍为WatchFolder；SD卡/WPD与固定提供器选项不等于真实机身连接。厂商SDK、取景/快门/对焦/曝光/电量存储未实现，旧RAW科学门禁和未运行项保留。

## 包与证据

Start-FullProduct-LocalTest.cmd使用独立R3启动根；原项目数据根及手机根均保留在包外。统一包的App、档期服务、选片服务、原生桥接均绑定本源码。PACKAGE_FILES.json逐文件校验，外部PACKAGE_DELIVERY.json记录zipSHA；实际照片、缓存像素、数据库、owner key、设备token和本人授权链接均不入包。完整表见FUNCTION_STATUS；剩余动作见REMAINING_BLOCKERS。此为诊断交付，非发行安装器。
'''
(d/'CURRENT_REPORT.md').write_text(text,encoding='utf-8');print(json.dumps({'status':'UNIFIED_DIAGNOSTIC_ASSEMBLED','counts':counts,'frozenPasses':sum(expected.values()),'latest':receipt['latestUniqueAutomaticOutcomes']},ensure_ascii=True))
