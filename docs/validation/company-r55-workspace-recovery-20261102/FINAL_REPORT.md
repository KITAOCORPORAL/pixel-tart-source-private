# 公司 R5.5：保存失败后的工作区退出与切库恢复

记录：2026-11-02T14:36:30+08:00（Asia/Shanghai）。**本轮保存失败恢复 PASS；完整诊断应用仍 PARTIAL_DIAGNOSTIC_NOT_FOR_RELEASE。** 未重复上一轮完整公司混合大照片闭环，不扩功能。

## 1. 基线与保全
- fetch origin成功，开工HEAD/远端均7c8da76139c098666f90636d3d2e34449fb8de0a，无该产品线新增提交；实际产品源fc811b54a4a140543a4c9bead47de8b60e7529f2。已读AGENTS.md、POLICY、R55_IMPLEMENTATION、HANDOFF和company-r55-workflow-20261102报告。
- 继续使用公司实际树E:/Kitao-Worktrees/r55-workflow-company-20261102，分支home/r55-canvas-photo-preview-20261101；工具CWD E:/Kitao-Worktrees/r55-workflow-company-20261102为同树映射，不使用家庭N盘路径。
- 开工枚举refs/worktrees/HEAD/dirty。公司旧灯台树27794d2f86313d7231f235b4fd1104eb4e95c106、4个独有未推提交及docs/validation/company-r55-polish-20261030/原样保留；其他脏树/色彩研究分支未改、不整合。未reset/clean/stash、未合main、未强推。

## 2. 先复现：不是把静态风险当结论
### 页面释放任务被保存失败永久污染
生产链：最近库“打开 / 切换”事件 → SwitchToContainerAsync → 候选B初始化/获lease → A页DisposeAsync → FlushAsync失败 → IOException → SwitchToDescriptorAsync的finally释放B候选页/lease → 行内报告保留A。

锁住已保存A画布JSON（FileShare.Read，不允许替换），编辑保留在内存。首轮失败A页、CanvasEditor文档对象、IsReady与A写lease均保留，B候选repository正常释放。但页面_disposeTask缓存为faulted；恢复写入并实际点“保存”读回成功后，第二次切B仍立刻重抛第一次异常。

原产品源码实测：raw/baseline-recovery-v4.trx 两项FAIL，baseline/SWITCH_SAVE_FAILURE与SWITCH_AFTER_REPAIR记录cachedTaskFaulted=true、switchedToB=false。最终测试另绑定旧完整发布DLL从最近库菜单复跑，raw/baseline-release-menu.trx仍2FAIL；该次严格teardown再次遇到旧fault导致主断言被清理异常遮蔽，因此精确根因以上一份逐阶段JSON为准，不将该遮蔽伪装为另一产品问题。

### 宿主释放第二次假成功
生产Host.DisposeAsync在清理成功前Interlocked.Exchange(_disposed,1)。首轮A保存失败后仍持有效lease/可编辑；第二次调用却直接CompletedTask。baseline/CLOSE_RETRY_BLOCKED记录firstFaulted=true、secondStatus=RanToCompletion、pageReady=true。此时并未释放工作区，是实际复现。

### 真正窗口关闭链的边界
CanvasOwnerClosing会先取消窗口关闭，调用既有PrepareDocumentChangeAsync；用户选择“保存”而Flush失败则return，尚未进入不可逆释放。因此不能把DisposeAsync缺陷等同“每次X关闭都会丢数据”。本轮用原保存确认机制的Yes回调验证失败拒绝退出、恢复后保存成功；没有原生鼠标点击确认弹窗。未修改这条确认语义。

## 3. 最小实现及四种状态
仅改src/PixelTart.Modules.AssetLibrary/AssetLibraryPage.cs与AssetLibraryWorkspaceHost.cs。

|阶段|行为|
|尚未释放|_disposeTask为空，页面正常交互/lease有效|
|正在释放|同一TCS.Task缓存，所有并发调用共同等待，不返回虚假CompletedTask|
|保存失败、仍能编辑|尚未越过page.HasStartedDisposal边界；将本次任务明确fault给等待者，清掉可重试槽位；保留页、修改、repository、写lease|
|成功释放|缓存成功任务，重复调用安全；page后台任务drain和repository释放先于host lease释放，清空当前页/descriptor/content引用|

- 页面只在Flush通过后、第一次事件解绑前标记不可逆清理开始；若此后清理失败，保留原fault task，不重新解绑、重复Dispose或假装恢复编辑。
- 宿主复用原切库SemaphoreSlim串行化切换/退出；关闭过程中不初始化竞争候选页。已入队切库待释放结束后被行内拒绝。保留gate对象以便排队者安全退场，不在有等待者时Dispose gate。
- 候选页清理与候选lease释放/gate释放嵌套finally：即使候选清理本身抛错，仍执行lease和gate的释放。没有自动重启、强制丢弃、通用任务系统或大型状态框架。
- 既有当前库相同LibraryId+规范路径的no-op保留；失败后的原库重复打开不自锁。异常仍向DisposeAsync调用者传播，切库沿原行内错误通道提示，不吞掉保存结果。

## 4. 最终发布绑定实测
20个共享DLL与完整发布逐项SHA一致，最终测试支持a03d990702c169570111d87ef73ac2b7087ac511。最终 **37唯一方法 / 37执行 PASS，0FAIL、0跳过**，仅选定恢复和保全集合，不是全仓历史测试全过。

### A/B恢复与资源
1. 隔离A/B素材库，A上160×120文字画布已保存初版后再编辑（无私照/RAW/20MP输入）。
2. 文件锁真实阻断原子替换；通过最近库菜单实际Click事件尝试B。保留相同A页、画布和文档对象，IsReady=true；磁盘原JSON字节不变，无残留tmp；B候选页repository退出、B lease可重新取得，A lease仍排他。
3. 解除文件锁，实际保存按钮Click事件→Flush并从store读回。再次最近库菜单打开B成功；A后台任务drain至0、A lease和数据库文件可独占打开。
4. 切回A读回画布，再次打开当前A保留原page对象、不自锁；成功Dispose和重复Dispose安全。
5. PID25948建立恢复收据，另一测试进程PID25428重开A画布，标题、CanvasId、文字内容精确读回；随后切B并退出，A/B租约均可重新取得。

release/RECOVERY_RESOURCE_RELEASE：创建4个页面（A初页、失败B候选、成功B页、回A页），全部repository已Dispose、P3PendingOperationCount=0，A/B writer lease可再获、数据库文件可FileShare.None打开，无保存tmp。它是明确对象/句柄检查，不宣称进程全局永无任何缓存分配。

### 并发、关闭、不可逆边界
- 原P3 tracked operation作可控在途阻挡；两个Host.DisposeAsync为同一Task、两个page.DisposeAsync为同一Task，未drain前均不完成、repository未Dispose且A lease仍有效；释放阻挡后共同完成，tracked实际执行1次，重复释放返回同一成功任务。
- 保存gate阻挡叠加文件锁，失败期间两个host和两个page调用均等待同一在途尝试；失败后page仍可编辑，恢复保存后再退出成功。
- 文件继续阻断时第二次关闭仍IOException，不再假成功；实际保存后退出，A/B锁释放。
- 人为在不可逆P3 drain中注入IOException：保留同一page/host fault，lease不被提前释放，不自动重启清理；这是注入的清理故障，不是自然保存失败。测试结束用显式测试专用repository/lease teardown清理夹具；不能将其宣传成产品可恢复或自动丢弃。
- 额外注入B候选清理自身drain失败：仍释放候选写lease与switch gate，保留A；该注入造成的B数据库清理故障仍原样fault，测试专用teardown后再检查B数据库独占。普通A保存失败的候选页/lease/数据库则已全部正常释放，两种情形分开记录。

### 保全回归范围
恢复8方法；保全29方法：现有P3并发drain2、最近库1、策划画布一致性17（含另一进程离线源重开）、画布保存/取消3、完整壳3、策划15项1、教程退役结构/文案2。策划旧PNG/固定revision身份、显式更新保留旧版、取消/新请求/项目切换、源hash篡改、原子保存继续通过。没有重复上一轮混合大照片完整App测试、JPEG/ICC归因、六图研究或20MP。

## 5. 原始失败与未执行项
- baseline-recovery.log：测试最初AssetLibraryPage同名类型歧义编译失败；加明确alias。v2缺独立product.db的Projects迁移、v3未挂窗的视觉树查不到保存按钮，分别补夹具迁移和直接真实HeaderPanel按钮。它们未到产品故障阶段，原log/TRX全保留。
- targeted-recovery-v2.trx额外候选清理故障测试初版把“候选drain明确失败后的数据库句柄”误当应已释放；拆分lease释放与不可逆database清理，并显式测试teardown，不放宽正常保存失败场景的句柄断言。后续7项和最终发布37项均通过。
- 基线FAIL和固定后PASS分别存放；旧company-r55-workflow报告/收据和家庭v3数值NONPASS不改。JPEG/ICC原FAIL、历史符号链接FAIL和LCMS参考NOT_RUN原样保留，不声称本次通过消除了它们。
- NOT_RUN：用户鼠标、实际关闭确认弹窗点击、多DPI/多屏、ACM/HDR、真实大照片本轮重测；ComputerUse/Browser/WebView/CDP未启用。公司设备登记沿当前CIM读取Windows10.0.19045/GTX1650/3840×2160/AppliedDPI144，与本轮恢复行为测试分开。
- 未改Canvas渲染数学、v1/v2/v3、256MiB预算、默认recipe、RAW、灯台或色彩研究引擎。生产不可逆非保存清理故障仍显式失败、不会自动恢复，这是边界而非已通过所有退出故障。

## 6. 交付与身份
- 产品源码 **89b64fd220bcb806065a13a061f0c392a2fdb050**；初始测试4129fc7ab6dec1bfcb89c3156158410780691a2b，最终测试 **a03d990702c169570111d87ef73ac2b7087ac511**。文档HEAD/远端另记REMOTE_READBACK，不写自引用SHA。
- 完整自包含win-x64诊断ZIP：E:/Guangjing-Workspace-Recovery-20261102/Guangjing-R55-WorkspaceRecovery-Diagnostic-win-x64.zip；294文件/191,965,288B，ZIP79,709,031B；SHA256 **F922E56BDE16E5D334351572EC95589813A124DB1DD8FCD2E0918690B068426E**。目录与ZIP每文件流hash读回PASS。
- 最终菜单事件补测只改测试支持及包内身份/README；产品DLL没有再次改动。先前测试身份包与ZIP_IDENTITY_INITIAL_TESTS在本地原样保留，最终ZIP重新逐项校验，不混用hash。
- 启动同名目录Start-Guangjing-Diagnostic.cmd；默认固定独立UserData-WorkspaceRecovery根，保存重开沿用，不访问日常数据库；可仅当前进程指定绝对GUANGJING_DIAGNOSTIC_DATA_ROOT。不要多个实例写同一根。
- WholeAppDiagnosticBuild=true / PhotographerTrialBuild=false，全部模块、10主导航、策划15与教程退役保留；无单模块包、Setup、用户数据库、私照、RAW或研究程序。
- 实际CMD/EXE OS启动、诊断标题、启动日志ProductSourceSha读回及WM_CLOSE均通过，EXE0/CMD0；这是启动烟测，不是用户鼠标故障恢复。新记录均Asia/Shanghai，历史收据时间不改。
