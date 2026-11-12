"""Build the current R2 status tables without promoting scoped evidence to full acceptance."""
from pathlib import Path
from datetime import datetime,timezone,timedelta
from collections import Counter
import argparse,csv,json,shutil,subprocess
p=argparse.ArgumentParser();p.add_argument('--context',required=True);a=p.parse_args()
c=json.loads(Path(a.context).read_text('utf-8-sig'));r=Path(c['worktree']);b=Path(c['deliveryRoot']);d=r/'docs/validation/product-functional-closure-20261112';rec=d/'receipts'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def save(p,o):Path(p).write_text(json.dumps(o,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
now=datetime.now(timezone(timedelta(hours=1))).isoformat();auto=read(rec/'R2_FROZEN_AUTOMATIC_CHECKS.json');native=read(rec/'R2_NATIVE_PROJECT_BOOKING_AND_REOPEN.json');tail=read(rec/'TAILSCALE_CURRENT.json')
mobile=read(c['mobileContext']);shutil.copyfile(Path(mobile['runtimeRoot'])/'PRIVATE_HTTPS_CONTINUATION_PREFLIGHT.json',rec/'PRIVATE_HTTPS_CONTINUATION_PREFLIGHT.json')
status=read(d/'FUNCTION_STATUS.json');status.update(baseProductSource=c['currentProductSource'],inheritedProductSource=c['baseProductSource'],observedAtBerlin=now)
native_path=(rec/'R2_NATIVE_PROJECT_BOOKING_AND_REOPEN.json').relative_to(r).as_posix()
for row in status['rows']:
 evidence=row.get('evidenceFiles',row['evidence'].split(';'))
 if row['id'] in ['CAL-01','PLAN-00','HIST-01','WF-02']:evidence.append(native_path)
 if row['id']=='CAL-01':row['verifiedBoundary']='正常R2 EXE保存原项目预约、关联策划及独立进程重开；原有手机服务本机HTTP/SQLite、凭据保持回执按原范围沿用'
 if row['id']=='PLAN-00':row['verifiedBoundary']='原项目创建已实测；R2原生预约与策划JSON关联同一项目及预约，保存跳转不再被旧弹窗遮挡'
 if row['id']=='HIST-01':
  row.update(status='已实际跑通',verifiedBoundary='R2正常EXE显示原项目历史并打开归片；原项目/预约/策划独立重开身份保持',remaining='仅基础项目元数据重开；完整客户选片/编辑/导出后的新主线尚未完成')
 if row['id']=='WF-02':row['verifiedBoundary']='原项目建项、预约、策划关联和基础重开已实测；当前在来源目录选择处待补';row['remaining']='Windows原生目录选择器路径框不可由现有通道编辑，未建立照片索引；后续筛选/客户/导出不冒称已过'
 if row['id']=='TETH-02':
  row['verifiedBoundary']='最新Windows枚举发现WPD SD_Card存储设备，未验证真实机身会话'
  evidence.append((rec/'CAMERA_OS_DISCOVERY_CURRENT.json').relative_to(r).as_posix())
 if row['id']=='ONLINE-02':
  row['verifiedBoundary']='R2完整App和自包含选片服务实际冷启动；PS7与Windows PowerShell重复复用实例'
  row['remaining']='修复时间字符串误判、早退误报及空public-base环境值；此项只证明部署启动'
  evidence.extend((rec/name).relative_to(r).as_posix() for name in ['FULL_PACKAGE_BOOTSTRAP_VERIFIED.json','LAUNCHER_POWERSHELL_REUSE.json'])
 if row['id'] in ['TETH-01','TETH-06','TOOL-01']:evidence.append((rec/'R2_FROZEN_AUTOMATIC_CHECKS.json').relative_to(r).as_posix())
 row['evidenceFiles']=list(dict.fromkeys(evidence));row['evidence']=';'.join(row['evidenceFiles'])
 if not (r/row['sourcePath']).is_file() or any(not (r/name).is_file() for name in row['evidenceFiles']):raise RuntimeError('Missing source/evidence: '+row['id'])
save(d/'FUNCTION_STATUS.json',status)
fields=['id','module','entry','status','verifiedBoundary','sourcePath','evidence','remaining']
with (d/'FUNCTION_STATUS.csv').open('w',encoding='utf-8',newline='') as out:
 writer=csv.DictWriter(out,fieldnames=fields,extrasaction='ignore',lineterminator='\n');writer.writeheader();writer.writerows(status['rows'])
lines=['# 全模块功能状态表','',f'更新时间（Europe/Berlin）：{now}。产品源码 {c["currentProductSource"]}。','',
 '按42个子流程统计；旧实测维持原项目、设备和来源范围。正常EXE的UIA操作、自动宿主、真人手机和物理相机证据分开。','',
 '|模块 / ID|主入口或子流程|状态|已验证范围与剩余事项|','|---|---|---|---|']
for row in status['rows']:lines.append('|'+ '|'.join([row['module']+' / '+row['id'],row['entry'],row['status'],row['verifiedBoundary']+'；'+row['remaining']]).replace('\n',' ')+'|')
(d/'FUNCTION_STATUS.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
entry=read(d/'ENTRY_INVENTORY.json');entry.update(observedAtBerlin=now,productSource=c['currentProductSource'],latestNativeEvidence=native_path)
changed=subprocess.check_output(['git','-C',str(r),'diff','--name-only',c['baseProductSource'],c['currentProductSource'],'--','src/RAWSelectionAssistant/MainWindow.xaml'],text=True)
entry['primaryNavigationSourceUnchanged']=not changed.strip();save(d/'ENTRY_INVENTORY.json',entry)
workflow=read(d/'MAIN_WORKFLOW_BREAKPOINTS.json');workflow.update(productSource=c['currentProductSource'],updatedAtBerlin=now,entireNewProjectWorkflowCompleted=False)
for step in workflow['steps']:
 if step['step'] in ['排档期','关联策划']:
  step.update(status='已实际跑通',evidence=native_path,boundary='正常R2 EXE保存同一预约；SQLite与策划JSON的ProjectId/BookingId一致；原生跳转遮挡修复并复验')
 if step['step']=='导入/联机入片':step.update(status='存在阻塞',evidence='receipts/native/r2-add-synthetic-source-dialog.json',boundary='同一原项目已打开归片；Windows目录选择器路径框仅暴露pane，尚未选入来源并建立索引；监视文件夹与相机分别按既有范围')
 if step['step']=='重开项目':step.update(status='已实际跑通',evidence=native_path,boundary='仅原项目/预约/策划元数据及历史入口的独立进程重开；客户选片、编辑和导出后的完整新主线仍未完成')
save(d/'MAIN_WORKFLOW_BREAKPOINTS.json',workflow)
tether=read(d/'TETHER_SUPPORT_AND_REAL_DEVICE_GAPS.json');tether['observedPnPDevices']=read(rec/'CAMERA_OS_DISCOVERY_CURRENT.json');tether['cameraImageClassCount']=sum(x['Class'] in ['Camera','Image'] for x in tether['observedPnPDevices']['devices']);save(d/'TETHER_SUPPORT_AND_REAL_DEVICE_GAPS.json',tether)
remaining=read(d/'REMAINING_BLOCKERS.json');remaining['updatedAtBerlin']=now
for item in remaining['items']:
 if item['id']=='NATIVE-INPUT':item.update(id='NATIVE-SOURCE-PICKER',condition='WPF原生按钮通道已恢复，预约/策划/历史修复已复验；当前Windows目录选择器的路径框只暴露pane，点击和Alt+D未得到可编辑焦点。',next='在当前选择器选择synthetic-inputs/source-jpegs后，从同一项目建立索引并继续后续主线。',evidence='receipts/native/r2-add-synthetic-source-dialog.json')
save(d/'REMAINING_BLOCKERS.json',remaining)
counts=dict(Counter(row['status'] for row in status['rows']));receipt=read(d/'CURRENT_RECEIPT.json')
receipt.update(recordedAtBerlin=now,productSource=c['currentProductSource'],sourceBase=c['startingSource'],baseProductSource=c['baseProductSource'],classificationCounts=counts,
 functionStatusRows=len(status['rows']),automaticUniqueOutcomes=auto['latestUniqueOutcomes'],r2FrozenChecksPassed=71,newAutomaticChecksPassed=71,newNativeBookingCreated=True,
 normalNativeWorkflowProjectCreated=c['workflowProjectId'],normalNativeBookingId=c['workflowBookingId'],normalNativeBookingRevision=native['booking']['Revision'],
 interactiveDesktop='NATIVE_WPF_ACTIONS_RECOVERED_WINDOWS_SOURCE_PICKER_PENDING',nativeWorkflowCompleted=False,
 fullPackageBootstrap='PASS_R2_COLD_START_TWO_SHELL_REUSE',updatedPackage=c['package'],srcOrXamlChangesThisTask=True,
 changedProductFiles=['src/RAWSelectionAssistant/MainWindow.xaml.cs','src/RAWSelectionAssistant/ViewModels/MainViewModel.cs'],xamlOrStylesChanged=False,pixelAlgorithmsChanged=False,
 productRepairCommits=['64c87a88c3cbcb4a71915ddb2ef1686f0fbcbe06',c['currentProductSource']],
 phoneRealDevice='NOT_RUN_NEEDS_COMPANY_TAILNET_SIGNIN',currentPnPDeviceCount=tether['observedPnPDevices']['deviceCount'],verifiedRealCameraModels=0,
 remainingTopPriority=['本人确认公司机加入原Tailscale网络','Windows目录选择器选入合成来源后补原项目第4步起主线','真实iPhone双向改/刷新与最终App服务重开','真实相机条件及尚未集成SDK边界'])
save(d/'CURRENT_RECEIPT.json',receipt)
report=f'''# 全模块功能诊断 · R2 接续交付

更新时间：{now}（Europe/Berlin）。状态：**PARTIAL**。接续点 {c['startingSource']}；当前产品源码 {c['currentProductSource']}。UI改版撤回，两张生成稿批准数为0，后续视觉设计由Claude负责。产品只修操作遮挡和项目历史入口刷新；XAML、样式及像素算法未改。

## 当前实际结果

- 42个子流程：已实际跑通{counts['已实际跑通']}，仅自动检查{counts['仅自动检查']}，存在阻塞{counts['存在阻塞']}，尚未实现{counts['尚未实现']}。状态表覆盖工作台、日历、素材归片、联机、暗室、策划15区、自由画布、即时胶片、在线选片、浏览器采集及收支。ENTRY_INVENTORY.json单列10个主入口和15个策划区；入口可见不等于业务完成。
- 原项目 {c['workflowProjectId']} 的预约 {c['workflowBookingId']} 已由正常EXE保存。SQLite与策划JSON关联同一项目及预约；R2重开、历史显示和打开归片已实测，预约revision为{native['booking']['Revision']}。这些操作通过原生UIA完成，不冒充物理鼠标验收。
- 修复：关联项目后“继续完善策划”已打开目标页但旧快速编辑器仍遮挡；现仅在目标策划成功打开且编辑器已保存时关闭旧层，未保存内容不丢弃。另修复策划新建项目后进入历史页不刷新，以及读取未完成前清空旧列表的问题。完整App回归和R2正常EXE复验均通过。
- 冻结R2源码新增定向检查71项（排期/联机61、导航9、完整App遮挡/同一身份/历史1）通过。结合有效保留回执，最新唯一自动检查215通过，性能测量opt-in 1项未执行。首次暗室配置失败/连带未执行和新宿主首次定位失败记录保留，未算作当前通过。
- 完整R2包自包含App、选片服务、档期服务及浏览器原生宿主。实际冷启动、PS7和Windows PowerShell重复复用通过。启动器修复日期对象/字符串比较误判、早退误报和PS7空public-base环境值；仅当前进程配置，原数据及其他服务保留。
- 现有手机凭据、本机HTTP/SQLite、匿名拒绝、revision和暂停变更标记只读复验通过，手机预约仍revision4。1e6bcf30的App正常重开与服务凭据保持按原范围沿用。本轮未改手机预约。

## 当前阻塞及主线边界

Tailscale当前NeedsLogin，必须本人确认公司机加入原已授权网络。本人入网链接只在对话和本地等待中给出，不进入仓库或包。入网后Continue-CompanyMobile.py验证原网络后，用本公司Self DNS配置独立8444 HTTPS，核验证书、同一设备、匿名拒绝、同一预约及观察轮次，再给手机两步操作。它不重置其他路由，不启用Funnel，不改预约或轮换凭据。

手机改→PC刷新→PC再改→Safari刷新及最后App/服务重开尚未运行。新项目主线已实际通过建项目、排期、关联策划及基础重开；当前在Windows原生目录选择器处，路径框只暴露pane，现有通道未取得编辑焦点，来源/索引尚未建立。已给出owned synthetic-inputs/source-jpegs目录供选择，未向数据库注入来源绕过用户入口。后续筛选、编辑、新相册客户反馈、收回、导出和最终全链重开仍按自动或旧实测范围列出，不拼接成新主线完成。详见MAIN_WORKFLOW_BREAKPOINTS.json和REMAINING_BLOCKERS.json。

## 相机及旧回执复用

最新Windows设备枚举发现WPD存储设备SD_Card，Camera/Image类为0；没有可验证的机身连接。现有产品是None/WatchFolder固定提供器，支持稳定文件监视、配对、安全复制、项目关联及错误分支。没有已验证Canon/Nikon/Sony等机型清单或厂商SDK握手回执，RAW后缀识别不代表机型解码批准。

文件夹实机验收还需可用机身、线或网络、正确相机模式，以及向指定目录写入完成文件的外部联机软件。机身发现、取景、快门、对焦、曝光、电量/存储还缺SDK集成、许可评估和实机验证。TETHER_SUPPORT_AND_REAL_DEVICE_GAPS.json把环境条件和未实现产品能力分开列出。

Chrome采集沿用057fe820的真实四段回执；在线选片沿用7769b8ee的真实Safari反馈、服务确认、PC回收和重开。已核对相应专用功能源码无差异及旧回执哈希，保留原项目/设备范围。共享MainViewModel/MainWindow已有本轮修复，不能把旧整包字节标签套到R2。fetch发现的de0b04ff RAW内存/视口旁支未合并或借用通过结论；旧JPEG/ICC数值和RAW科学门禁仍保留。

## 可运行包与证据

默认入口Start-FullProduct-LocalTest.cmd使用独立full-product-localtest-r2；原生主线使用保留的workflow-runtime，手机仍使用原company runtime。三个根不混用预约或数据库。压缩包SHA见仓库中的PACKAGE_DELIVERY.json及交付目录外部收据；压缩包内以PACKAGE_FILES.json逐文件核验，证据索引见EVIDENCE_INDEX.json。SQLite、owner key、设备token、本人入网链接和未批准生成稿均在包外。该包为功能诊断，非发行安装器。
'''
(d/'CURRENT_REPORT.md').write_text(report,encoding='utf-8')
(r/'tools/FunctionalClosureAcceptance/README.md').write_text('''# 功能诊断工具

产品源码与状态见CURRENT_RECEIPT.json。保持原项目、预约和手机变更暂停标记，不把自动检查或浏览器元数据当真人手机或相机验收。

- Refresh-FullProductDiagnostic.py --context <本地上下文>：先核对既有索引，再只读检查原凭据、SQLite、服务、Tailscale和包完整性。已知回执更新须先保留原字节，再按明确版本更新索引。
- Continue-CompanyMobile.py --context <手机上下文>：默认只读；本人入网后加--apply，核对原tailnet、公司Self DNS、独立端口和可信HTTPS。完整凭据链接只写runtime/PHONE_URL.txt。
- Native-UiStep.ps1：通过已安装OCU的文档化CLI操作指定正常App，逐步获取新状态并核对PID，不直接使用PowerShell UIA。只保存目标App AX文本，不保存可能混入其他窗口的桌面图。
- Update-R2Evidence.py、Assemble-FunctionalDocumentation.py：分别绑定冻结检查/正常EXE回执，生成42行表及主线边界。
- Finalize-FullProductDiagnostic.py：补全独立服务和启动入口；Verify-CompanyRuntime.py验证冷启动后的双shell复用和7项合成时区观察HTTP分支。
- Seal-FunctionalDiagnostic.py、Verify-FunctionalDiagnostic.py：封包及逐字节验证；封包后校验/远端读回收据不嵌入自己的压缩包。

原生主线未完成、手机和相机外部条件必须明确交接。不要提交或打包PRIVATE文件、数据库或本人授权链接。
''',encoding='utf-8')
print(json.dumps({'status':'R2_DOCUMENTATION_ASSEMBLED','rows':len(status['rows']),'classificationCounts':counts,'automaticLatest':auto['latestUniqueOutcomes']},ensure_ascii=True))
