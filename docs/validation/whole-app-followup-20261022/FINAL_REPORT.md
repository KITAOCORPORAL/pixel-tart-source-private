# 光镜完整范围续接验证包

2026-10-22，Europe/Berlin。**整体PARTIAL，发行BLOCKED；交付完整Windows x64诊断包，不是已验证正式版，不是Setup。**

## 本轮完成与源码权威

从会话01a1581d-45cf-79d0-85b2-dea07b4c7130的全软件未完成项继续。先fetch origin并核对59个远端ref、43个原工作树HEAD/状态。暗室远端仍为a1e52238616b8d22f09b92595a85e76c7d9c1460，原验证分支仍为46ba7e29d93660f47f836bd4f60169c894f371ca。

选择现有单一后续分支company/planning-summary-images-20261021的冻结HEAD **13ec9ca1c678622e5c7b884d8f69882c409c33a4** 新建独立validation/guangjing-whole-app-followup-20261022工作树；merge-base证明它完整继承原验证、家用续接、公司切换性能、DOCX及图片汇总提交，没有merge/cherry-pick。上次产品提交439e9064..冻结HEAD的src/tests差异为空。其他非祖先/脏owned-resource/desktop-entry/V3成果不计入本包，差异与当前blob对应分别见BRANCH_AUDIT_CURRENT.json和BRANCH_CURRENT_EQUIVALENCE.json。

本轮**未修改产品源码或测试断言**，只修正执行前置变量/驱动配置并保留失败，新增可复现工具和完整证据。未改像素算法、PV4/PV5/PV6合同、性能阈值或60秒逻辑取证门限；未强推/合main/reset/stash/clean，未纳入私人RAW/照片/数据库。

## 可启动的完整新构建

统一入口：Guangjing-FullScope-Followup-Diagnostic-win-x64/Start-Guangjing-Diagnostic.cmd。Release win-x64 self-contained，WholeAppDiagnosticBuild=true，PhotographerTrialBuild=false；真实MainWindow完整导航，没有用暗室替换软件。290应用文件、264声明运行资产、缺失0，EXE PE machine 0x8664；APP_FILES.json/DEPENDENCIES.json/BUILD_IDENTITY.json及PUBLISH_FULL.json记录完整清单、命令和hash。

启动器仅给子进程设置隔离目录，写包内UserData；本轮观察使用delivery内另一个全新desktop-final-isolated，没有覆盖日常数据。最终EXE PID10844实际启动，加载主WPF/Core/模块7项hash与本包一致；桌面树可见10个一级导航及工具箱/设置等。这仅是启动身份PARTIAL，不是业务PASS。原完整ZIP SHA256 c89f44dd95a5086ca863c612f1da550716332bfc2207a513aa8746d92b4cbd3b复验未变。

## 本轮测试 每个成绩有原始发现与TRX

|范围|发现|取得|通过|失败|跳过NotExecuted|漏项/额外|
|---|---:|---:|---:|---:|---:|---:|
|最终完整Core|1723|1723|1718|0|5|0/0|
|完整WPF四片合并|1726|1726|1706|0|20|0/0|
|发布模块|14|14|14|0|0|0/0|
|逻辑DPI合同|91|91|91|0|0|0/0|
|文档原型独立组件|1|1|1|0|0|0/0|
|胶片研究独立组件|25|25|25|0|0|0/0|
|策划合成样例专项补验|1726总发现|15选择/15结果|15|0|0|0/0|

WPF按fixture/指定P1方法独立进程执行；四片同程序集hash、同发现集、(testId,FQN,displayName)多重集合计划=发现=实际。完整Core及其他套件也逐身份核对；测试进程超时0。43个发布DLL绑定最后再hash确认零差异。附加15项已包含在WPF全量中，**不重复相加**。研究及原型PASS不升级为产品/外部应用/真机成绩。

严格runner遇到NotExecuted仍退出FAILED，这不是25项真实Failed；5Core/20WPF明确NOT_RUN逐名列NONPASS.csv，原因包括真实RAW/空间语料、GPU未验证、生产分辨率性能与各类opt-in视觉/诊断条件。不扫描私人资料补齐。全部原始TRX、适配器发现、执行计划、runsettings、stdout/stderr均封存RAW_TEST_EVIDENCE.zip，2861个原始文件逐hash验证。

## 原失败保留 修正后整套复验

- 首轮Core1717 PASS/1 FAIL/5 NOT_RUN：发布同hashDLL复制到宿主，但释放程序集测试需要loaded location，前置变量错指原publish location。只修正变量并更换性能输出目录，整套Core重跑1718/0/5；原失败和一次复验驱动argv启动错误都保留，未改断言或产品。
- 额外策划样例首轮14 PASS/1 FAIL：正斜杠证据根与产品规范Windows附件路径的旧字符串断言不同。以Path.resolve规范新输出根，同测试15/15；不把它回填或覆盖原补验。
- 逻辑取证首轮第106状态AssetLibraryGrid 3840×2160/200%原60秒超时；前105张及日志保留，无完整manifest。完整测试结束后，同EXE/生产器/60秒门限、全新目录**106/106完成**，独立validator通过、DPI91/91。新run不拼接旧图，首轮超时仍为原始失败证据。

## 全模块与策划15区

本轮重新扫描src XAML与动态控件候选：**1122条源码动作候选、24模块/交付域、122业务操作、15策划分区**。新增相比旧121操作的一项为15区汇总DOCX及自有图像导出，真实菜单位置PlanningCenterView.xaml.cs:572。候选包括事件/隐藏/条件/动态项，不机械当可见按钮，模块fixture regex仅作相关索引，不能证明每条动作已操作。

MODULE_TOTAL.md、MODULES.csv、OPERATIONS.csv、PLANNING_15.csv逐项包含构建、实际操作、自动测试、人工实机、证据、问题与阻断原因。原策划ProductionPeers和R3 fixed-output失败已由继承提交修复，本轮PlanningAutomationContractTests 1/1、R3CommandLineReviewTests 15/15、策划业务/汇总/keepNext15项等重新执行，不继承历史成绩。

当前合成策划业务用真实JSON stores与隔离SQLite验证15区创建/编辑/关联/草稿恢复/资源和自有图片搬迁/重开；资源ShotIds、只读账本与附件原位置边界仍保留。当前图文汇总实际DOCX有15标题、9 drawing/9 media、缺图/拒绝原因与稳定ID、图像尺寸及顺序、保存重开一致。产品回读分页为14页，独立Poppler渲染检查联系图无空白/明显重叠；**这不是Word/WPS或实体桌面通过**。render_docx.py独立应用渲染本轮仍缺LibreOffice soffice.exe，BLOCKED。汇总是阅读快照，不是15区双向导回或完整项目便携包；外链/库原件/普通附件不自动打包。

两张明确授权照片本轮在完整WPF执行768px派生流程：快速相纸/相框、latest-wins、显影精确、变体/锁定、保存重开及PNG/TIFF/JPEG导出断言；自动流程非原尺寸RAW/真实鼠标。只提交结果摘要/输出hash，不把输入或生成照片进Git/诊断ZIP。

公司原Scenario11本轮完整Core复验10000切换为1916.0877ms，原30000ms门槛及100k元数据去重位置不改。家用32139.0909ms历史失败和家用复测说明未关闭，不能用公司数字冒称家里通过。

## 真实桌面 仍不能发行

本轮原包、新EXE分别复查：原生点击退出教程均报 **coordinate input geometry is unavailable**；新选窗口后的专用截图报 **FrameArrived timed out: timed out waiting on channel**。没有用RaiseEvent/API/手改settings跳过首次教程冒充鼠标验收；未绕过安全限制，没有本轮真实截图或录屏。

122业务操作：**PASS0 / FAIL0 / BLOCKED118 / NOT_RUN4**（后4项为安装相关）；策划15区人工均BLOCKED。启动/加载身份仅PARTIAL。相机/真实服务三客户端/WPS/跨屏/物理DPI/显示色彩、设备目录、完整项目跨机便携、原A-BC附件等分别保留NOT_RUN/BLOCKED。详细R3同尺寸真实参考对照、动效、物理DPI和原生保存/导出长流程仍开放。

## 保全与提交

全部43原工作树HEAD/status未变，423个原有脏文件hash复验零变化，旧诊断ZIP未变（PRESERVATION_AFTER.json）。新工具/文档分阶段提交和正常push，仅推本轮独立validation分支，LOCAL_HEAD/REMOTE_HEAD读回另列PUSH_READBACK.json。产品源码身份固定为13ec9ca1c678622e5c7b884d8f69882c409c33a4，后续文档提交不改变发布组件。

最终门禁：**BLOCKED**。可交付完整诊断包和完整范围证据；不能宣称已完成全软件发行验收。

## 最终ZIP独立逐文件读回

包：E:\Kitao-Deliveries\guangjing-whole-app-followup-20261022\Guangjing-FullScope-Followup-Diagnostic-win-x64.zip；365文件，123037311字节（约117.3MiB）。SHA256 a235f4deed71d39ac5cf78605a0b85c6ef6ad08eb23fb3a4e9236eb24b2ef82b。全部文件解压式读取逐hash一致，包含完整app/原始TRX摘要/106张明确离屏图归档/合成文档，不含UserData、数据库或私人照片/RAW；不是Setup。ZIP内报告是打包时封存快照，外部最终报告后附ZIP自身身份，避免自引用hash。

## 已验证正常推送

本轮独立分支已正常push，69ae68244466b5ef40226236f3580ddb3267f510独立ls-remote读回与LOCAL_HEAD相同，工作树干净。PUSH_READBACK.json保留这次已确认读回；后续仅文档封存，不改变冻结源码与发布组件。最终HEAD在本轮交互读回再次给出，不尝试把提交自身SHA写入自身。
