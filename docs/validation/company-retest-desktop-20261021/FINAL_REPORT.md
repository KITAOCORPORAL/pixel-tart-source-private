# 公司端完整复测与实体桌面尝试 — PARTIAL，诊断版不能发行

2026-10-21，Europe/Berlin。主机China Standard Time/UTC原日志保留，不改历史时区。

## 起点与旧包核验

Fetch后独立ls-remote：company/guangjing-home-sync-performance-20261021为a79ffc7c05ea0559a142a7ecd06f41b6b0f506fe，本地HEAD相同且干净，无新增远端提交。产品基准0e5c3ea7e25c0c3d1773480cdc351ca9dc94b80f；git diff 0e5c3ea7..HEAD -- src tests为空，未改产品或原测试。既有完整诊断包E:/Kitao-Deliveries/company-home-sync-performance-20261021/Company-SwitchFix-Diagnostic-win-x64.zip SHA256=6b15ef8c94ff4e121f0e8ca1f3125cf4e5327056522d8856431ab459e47f63b3，与封存记录一致。

旧工作树HEAD/所有未提交和未跟踪状态只读保留；404个脏文件逐hash复验，既有ZIP和当前269个app根文件hash均不变。无reset/stash/clean/rebase/merge-main/force-push，无私人照片/RAW/数据库/发布二进制入Git。详见START.json与PRESERVATION_AFTER.json。

## 测试通过：本轮完整重跑，不用先前20项补验

两项已授权manifest在调度前显式设置，逐行读取四个清单引用并校验两个授权照片的SHA256。KITAO_R3_CLI_OUTPUT、CPU性能与Scenario11输出均为本轮新目录；预检先完成，后启动全部套件。此前20/20补验未纳入本次结果。

当前诊断包主WPF/Core/模块DLL原样复制至各测试宿主，逐hash核验，COMPONENT_BINDINGS.json记录43项绑定。没有重新构建或替换当前诊断包。

|本轮套件|发现|选择/取得|通过|失败|跳过NotExecuted|漏项/额外|
|---|---:|---:|---:|---:|---:|---:|
|完整Core|1717|1717/1717|1712|0|5|0/0|
|完整WPF|1715|1715/1715|1695|0|20|0/0|
|发布模块|14|14/14|14|0|0|0/0|

WPF四片适配器发现集合相同、程序集hash相同；planned和结果均以(testId,FQN,displayName)多重集合核对missing/extra=0。Core与模块也逐身份匹配。原TRX、发现、runsettings和stdout/stderr封存RAW_RETEST_TRX.zip及逐文件hash。R3两授权照片流程本次在完整WPF中真实Passed，另见AUTHORIZED_FULL_RUN_READBACK.json，非旧补验拼接。

严格runner因opt-in跳过会退出FAILED，不能把“0 Failed测试”称所有门禁通过。5Core和20WPF未执行原因逐名保留NONPASS.csv：真实RAW/ICC/空间语料、未验证GPU、性能与视觉证据opt-in等。离屏自动构造/图像是自动测试，不是下方实体操作。

Scenario11原10000次切换、30000ms门槛及100k去重位置不变。本次完整Core测得1769.5904ms；每次已有测量原样保留，本轮没有挑最快或重试刷绿。该数字不是家用端新成绩，家用32139.0909ms失败仍未解释。SCENARIO11_CONTRACT_READBACK.json记录源码和测量，家用说明保留同盘TEMP/负载/组件/原始日志要求。

## 实体桌面已验证：仅启动身份PARTIAL，业务0项通过

待全量测试全部结束后，于公司Windows 10 Pro 19045 x64（12逻辑CPU、约32GB内存、GTX1650、报告3840×2160；另有MuMu虚拟显示适配器）启动**当前**隔离诊断包，而非自动化测试宿主。PID20900，UserData=E:/Kitao-Deliveries/company-retest-desktop-20261021/desktop-userdata。实际进程主WPF/Core/三个模块hash与当前包一致。窗口标题仍“完整范围诊断预览 · 未完成验证 / 不能发行”。

可访问性树观察到真实首次教程和10个一级导航；首次教程使业务导航禁用。这只证明窗口/入口存在，不证明双库切换、策划保存或布局通过。

|本轮真实步骤|结果|证据|
|---|---|---|
|启动实际EXE/加载组件核验|PARTIAL|DESKTOP_START/COMPONENT_READBACK/INITIAL|
|点击退出教程|BLOCKED|coordinate input geometry is unavailable|
|刷新窗口后专用截图|BLOCKED|FrameArrived timed out: timed out waiting on channel|
|再次刷新并一次激活/专用截图|BLOCKED|window capture timed out: timed out waiting on channel|
|双库切换/查询|BLOCKED，未执行|合成BMP已准备，未通过UI导入或建库|
|策划15区进入/保存|BLOCKED，15项均未执行|DESKTOP_PLANNING_15.json|
|不同缩放实际点击/窗口布局|BLOCKED，未执行|无输入几何/截图，不以3840×2160推断物理DPI|
|保存重开|NOT_RUN|前置步骤未完成|

无实体业务PASS、无本轮截图/录屏。真实输入失败和截图超时原文保留DESKTOP_OPERATIONS.json；未拿RenderTargetBitmap/HTML代替，未用RaiseEvent/API/手改settings跳过教程冒充实鼠标，也未启用受限回退或变更系统DPI/安全配置。物理桌面条件须用户恢复正常交互/可捕获目标后再续接；当前保留该隔离窗口供用户查看，不影响旧Release。

## 家用复测说明（待家里执行）

HOME_RETEST.md给出同修复源码0e5c3ea7、同物理盘TEMP、原10000/30000门槛、元数据不计入原秒表、独立进程单跑、预热三次、固定WPF负载和发布Core绑定要求。Invoke-HomeScenario11.ps1为PowerShell7一次性包装器，源码/组件身份不符即拒绝，输出新目录、不自动重试；180秒只为包装器挂起保全，不改变30秒测试断言。脚本仅语法验证，未在家用机执行，不冒称家用通过。

若再次超时：先保留原失败，随后独立诊断复跑采集目标PID生命周期与WPR CPU/DiskIO/FileIO/.NET事件；尊重已有录制，不接管别人的会话。物理磁盘等待、CLR contention、SQLite busy/文件锁分别归因，不以总体盘计数或wall-CPU伪造。ETL可能有敏感路径只留本地，提交脱敏分析/原文件hash，不上传私人原始跟踪。没有再次超时不额外开启系统录制。

## 仍阻断发行

- Core5/WPF20条件跳过未关闭，严格全门禁不是PASS。
- 双库/策划15区保存/缩放布局的实体桌面未完成，输入几何和截图工具本轮受阻；不得把自动测试通过当物理鼠标通过。
- 家用32秒失败原因未解释，家用同条件复测和必要关联ETW仍待执行。
- 策划外链/附件/SQLite完整便携、15区汇总导出、复杂Word/WPS、设备目录/相机/物理跨屏等原范围仍真实PARTIAL/NOT_RUN/BLOCKED；Setup/安装升级卸载未验。

因此本轮只交独立复测/桌面阻断证据和家用说明，不修改无关产品、不开main、不标正式发行。正常推送本公司续接分支后独立ls-remote读回，见PUSH_READBACK.json；产品SHA与文档HEAD分开。
