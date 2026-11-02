# 公司 R5.5：策划—自由画布—固定快照—保存重开

记录时间：2026-11-02T10:38:16+08:00（Asia/Shanghai）。**本轮最小工作流 PASS；总体 PARTIAL_DIAGNOSTIC_NOT_FOR_RELEASE。** 不把合成输入、控件调用、离屏渲染或进程启动当作真实鼠标/多DPI验收。

## 1. 接续与保全
- fetch origin 成功并核实家庭 HEAD 8da827794364da66f52c574aad401078fbda503a；家庭产品源146c0fd254de387ad5ccb9570cff6c54697389d4。已读取根AGENTS、开源先行POLICY、R55_IMPLEMENTATION、HANDOFF、家庭v3数值FINAL_REPORT/NONPASS。
- 实际公司新树 E:/Kitao-Worktrees/r55-workflow-company-20261102；接续 home/r55-canvas-photo-preview-20261101，未从旧company/lighttable树覆盖。公司旧树 E:/Kitao-Worktrees/company-lighttable-lifecycle-20261030 仍在27794d2f86313d7231f235b4fd1104eb4e95c106，4个独有未推提交和未跟踪docs/validation/company-r55-polish-20261030/原样保留。
- 比较发现9299749c、a1a2a8f7、2ed1dc35、27794d2f仅灯台实现/测试；本轮不合入。其他脏树、main和色彩研究分支不动，不reset/clean/stash、不强推、不合main。实际新树与CWD虚拟路径映射分别记录，不使用家庭N盘路径。
- 本轮src相对家庭仅改AssetLibraryWorkspaceHost.cs、FreeCanvasView.cs；灯台、CanvasJpegNativePreview、CanvasPhotoPreviewSession、CanvasPixelRenderer和渲染v3定义均未改。

## 2. 输入和公司环境
- Windows 10专业版10.0.19045，NVIDIA GeForce GTX1650、驱动32.0.15.7270，当前报告3840×2160；登记AppliedDPI144（150%），WPF调试亦返回1.5。另有MuMu虚拟显示适配器，未启用其进行验收。SDK10.0.302/runtime10.0.10。
- 只核对历史报告明确授权的 D:/AI AGENT/PixelTart-TestAssets/CompanyRAW 顶层：5个ARW/RAF/CR3，没有对应本轮标准JPEG/透明PNG。未执行RAW解码或扩大素材扫描，不把缺家庭照片判为产品失败。
- 本轮对应机制使用确定性可分发合成6000×4000标准8位JPEG、64×48透明PNG、文字、画笔、三色色板、隐藏离线占位层。生成器在CompanyCanvasWorkflowTests.CreateSyntheticInputs；输入hash/来源见SYNTHETIC_INPUTS.json。它们不是实拍照片或家庭原素材。

## 3. 实际完整应用路径
发布DLL绑定后的真实App.OnStartup/MainWindow/PlanningCenterView/AssetLibraryWorkspaceHost/FreeCanvasView：
1. 创建公司合成项目，打开策划并保存标题；创建素材库、打开自由画布。
2. 经现有导入API放入大JPEG与透明PNG；现有CanvasEditor依次执行裁切、17°旋转、双向翻转、72%透明度、层序前后、显隐切换；加入文字、画笔、三色色板及隐藏离线层。
3. 实际“保存”按钮事件并等待Flush；实际“插入/更新画布版本”按钮事件产生首版。断言画布全部对象状态与EditableSnapshot一致、CanvasId一致；首版PNG与冻结v3渲染同一文档完全一致，不主张跨解码核逐位等价。
4. 关闭完整宿主，另一进程重开同一资料根，核实Project/Document/Canvas/Revision身份、全部引用和图层序列化状态、首版PNG hash；修改文字与旋转并保存。**编辑后策划仍只有原固定快照，原hash不变。** 点击同一更新按钮才新增第二版，第一版身份与PNG保留。
5. 第三完整宿主进程再次读回两版身份/PNG与当前画布状态。发布绑定实际PID10332 →14520 →22036；见WORKFLOW/UPDATED/THIRD_REOPEN。

以上是生产控件事件与模型操作，不是用鼠标完成导入对话框/拖动/键入；鼠标项目单列NOT_RUN。当前实现不会偷偷把编辑结果同步成历史快照。

## 4. 发现问题与最小修复
### 当前库重复打开自锁（产品问题，已修复）
独立重开后素材库已按设置加载，再从当前库/最近库打开相同库，会在释放原lease前再次获取同一writer.lock，误报“另一个光镜实例”，并有InvalidOperationException未被入口兜底。基线失败见raw/company-reopen-b.trx；等待启动完成后仍失败见raw/company-reopen-b-startup-ready.trx。

修复：显式打开等待正在进行的启动；在切换gate内以相同LibraryId+规范路径确认当前有效page/lease，直接保留原page/画布/未保存内容，不重新申请writer锁；其他实例占用的InvalidOperationException转为行内失败提示，保留当前库。发布回归同时验证当前page对象不变、真实外部占用lease时不切走当前画布，释放后可继续用。

### 画布写入失败只说“请重试”（实际使用反馈缺口，已修复）
已有阻断路径回归只能看到“保存失败，请重试”，无法指导排障；新增指引断言真实失败见raw/save-guidance-before.trx。最小补充：明确未保存修改已保留，提示检查目标路径、写权限、磁盘空间、文件占用后点击保存重试；ToolTip含实际目标目录与异常。修复路径阻断后原内容保存成功，状态恢复“已保存”并清除过期错误。没有改存储原子性或像素算法。

### 非产品失败，原始收据保留
- 新测试初版误用不存在的Document属性（编译失败3项），改为PlanningDocumentId；首轮色板误造两色，按既有3/5/7色合同修正为三色，不放宽产品校验。
- 初版启动夹具没有等待策划在载入，出现“策划正在载入”；补等待后仍复现自锁，二者分开。
- 教程退役首轮多场景共用输出导致复制数量累加；改为每场景新输出、同一资料根，first/upgrade/restart全过，无产品修改。
- OS smoke首轮未及时持有进程句柄，EXE exitCode不可读，CMD为0；原收据保留，再次持有句柄实测EXE/CMD均0，不把不可读值改写为0。

## 5. 回归与资源
发布20共享DLL逐项hash绑定WPF测试host、9共享DLL绑定Core host。权威集合 **51唯一方法PASS / 0FAIL / 0跳过，53次执行**：Core13，WPF一致性/保存/全壳/策划15/图层队列26，离线独立重开与大图取消/篡改/项目切换/锁定保存/生命周期6，完整公司流程4，教程退役1方法3场景，1366/1920完整宿主1。不把开发失败或重复执行加入唯一PASS，不是全仓历史测试全过。见TEST_SUMMARY.json与原TRX。

- 来源变化、取消、并发新请求、项目/文档切换、写入锁定均验证不发布半成品；旧PNG和磁盘文件原字节保留，无未提交快照草稿。
- 合成24MP输出native1500×1000，不完整展开6000×4000；8个新任务每次SourceDecodes1、NativeJpegDecodes1、CacheHits1、PeakCache6,000,000B、RetainedCache0，源句柄每轮可独占重开。耗时212.7–227.6ms；GC仅寿命诊断，不用于放行预算。任务缓存不持续增长不等于整个进程绝无任何分配增长。
- 实际取消请求18.0ms、退场37.9ms；14次同来源cache命中仍检验hash，修改源后拒绝。WIC具体子阶段未知，不声称原生调用可抢占。
- 图层移动/缩放200事件、12对象，11.0ms调度，最多4线程，36次已验证预览读而不是每事件/对象完整解码；无通用缩略缓存回退。显式源篡改清空旧预览。
- 完整App宿主首版捕获采样：WS增59,928,576B，私有增59,105,280B；峰值WS455,688,192B/私有417,325,056B。**256MiB是渲染工作估算而非整应用RSS上限**，未提高预算。

## 6. 数值冻结与未执行边界
不重复JPEG缩小核/色度网格或WindowsCMM/LCMS原因排查，不跑新同类数值报告，不改v3/阈值。家庭docs/validation/home-r55-v3-numerical-20261101/FINAL_REPORT.md与NONPASS.json字节未改；原JPEG max40/mean1.633569/P95=4、ICC max3>1仍FAIL。历史符号链接FAIL、通用LCMS参考NOT_RUN继续保留；本轮PASS不是将其消除或批准科学发行门禁。

NOT_RUN：真实公司标准大JPEG/透明PNG实拍闭环、家庭私照、用户鼠标导入/拖动/裁切交互、多DPI/多屏/ACM/HDR/GPU画质与最终Grok逐像素验收。设备信息登记、1366/1920 DIP离屏图、OS启动分别记录，不互相替代。ComputerUse/Browser/WebView/CDP均未启用；不新增功能、不重画R5.5 UI、灯台主干冻结、色彩研究不合入。

## 7. 包、身份与启动
- 产品源 **fc811b54a4a140543a4c9bead47de8b60e7529f2**；测试支持 **dd71c1f857d105229b4ffb3e3e08438a9670c09b**；文档最终HEAD/REMOTE单列REMOTE_READBACK，不写自引用SHA。
- 完整win-x64自包含包 E:/Guangjing-Company-Workflow-20261102/Guangjing-R55-CompanyWorkflow-Diagnostic-win-x64.zip；294文件/191,963,687B，ZIP79,707,374B，SHA256 B73364132707F19D4B73FAD42A46B22E2ACD7FFB40187AC8CE7871BDD71A0C2C。目录与ZIP每文件流hash读回PASS。
- 全模块DLL/10主导航/策划15/教程退役保留；WholeAppDiagnosticBuild=true / PhotographerTrialBuild=false，不是暗室单模块，无Setup、UserData、私照、RAW或研究程序入包。
- 启动同名目录 **Start-Guangjing-Diagnostic.cmd**。默认相邻UserData-CompanyWorkflow固定隔离根，重开沿用，不使用每次随机新根冒充恢复。当前进程可显式指定绝对GUANGJING_DIAGNOSTIC_DATA_ROOT；不得多实例写同根。复看验收项目使用E:/Guangjing-Company-Workflow-20261102/UserData-release-workflow（本地保留，不在ZIP）。
- 实际CMD/EXE OS启动、标题、日志ProductSourceSha与WM_CLOSE：EXE0/CMD0，无遗留测试EXE；不等于鼠标实测。新时间均Asia/Shanghai，历史收据不改时间。
