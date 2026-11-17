# 关闭重入与图像资源生命周期调查

2026-11-17；逐项时间以原始UTC回执为准。状态：**根因调查与最小方案已交付，产品修复待审核**。

## 目标、现场及分工

目标是：关闭意图只作一次可追踪决定；取消或保存失败时保留可编辑内容；保存后才释放本会话资源；异步结果不能写回已结束的会话。降低内存的手段服从这些数据不变量，不以强制GC、提前释放共享图像或强杀另一运行根替代所有权管理。

已与产品聊天“完成产品线整合与TIFF导出验证”（01a1e355-64ea-74e0-b7b5-15ec557fdc30）核对：起始统一HEAD为 fdf01831a65065b574e6a320aba0cb5a4425148f；功能源码为 e8180221937fa4abb578a16faacebe99a5d4ac1b。fetch后远端同SHA。

独立调查树 E:/Kitao-Worktrees/close-lifecycle-investigation-20261117，分支 research/close-lifecycle-investigation-20261117；只写本目录与 tools/CloseLifecycleInvestigation。产品src/tests完全不改，Git树与e818一致。产品Codex维护根AGENTS和主线报告；本调查已读更新后的十条原则，不覆盖根文件。完整归属/哈希见COORDINATION.json和SOURCE_BINDINGS.json。

本轮不重跑已有充分证据的40MP/TIFF/选片闭环，不变更RAW批次、像素算法、产品包或原工作区。窗口只在独立合成根中离屏创建。夹具用反射访问私有状态门、替换提示选择和延迟provider，明确是受控WPF宿主，不宣称真实鼠标或完整Shell验收。

## 已证实的直接机制

|编号|必要条件与结果|证据与限度|
|---|---|---|
|C1|已保存活动画布订阅owner.Closing；其Yield延续在另一个Closing处理器的嵌套DispatcherFrame中执行；原Closing未退栈时再次Close，触发VerifyNotClosing。|最终close-nested-frame和repeat两个独立进程同栈，v1也重复；普通已保存关闭不出现。脏数据、保存失败不是必要条件。|
|C2|另一处理器取消一次，画布仍独立重发Close，窗口随后关闭。|close-cancel-other-handler两轮观察到；不能由此断言旧现场有人取消后丢数据。|
|C3|已取消第一次Closing后，Yield前HideCanvas使_canvas为空，回调return，关闭意图丢失。|close-hide-pending两轮窗口仍开、pending/allow复位。跨await读取的是可变“当前画布”，不是捕获的关闭目标。|
|I1|Unloaded只取消CTS；provider没收到该token，返回后只验generation/document，仍可写入_images。|preview-after-unload两轮：canceled=true、providerReceivedCancelableToken=false、imagesAfterUnloaded=1。|
|I2|Unloaded未解除Editor.Changed订阅、无停用门禁；编辑事件重启debounce/provider。|preview-editor-change-after-unload两轮。隐藏是否保留已有缓存是产品策略，隐藏后继续新工作则需明确契约。|
|I3|Page.DisposeAsync没有drain画布自己的预览任务；释放返回后仍在途，随后发布图像。|preview-workspace-dispose最终及repeat-resource两个独立进程：disposedBeforePreview=true、imagesAfterDispose=1。此处是终止边界，而非暂时隐藏。|

2026-11-16 12:11:43.597旧日志启动源码为0a45ee0e；旧/当前CanvasOwnerClosing的Git blob完全相同。206051eb（2026-10-04）早已加入Yield、pending和allow保护。C1复现与旧日志一致的核心完整栈，因此“再加Yield/更换Dispatcher优先级”不足以修复。

旧日志没有CanvasId、dirty revision、其他Closing处理器栈和消息循环来源，**具体哪个对话框/处理器制造了旧现场嵌套循环仍UNKNOWN**。也不能把保存失败、文件损坏或内存不足当作旧异常的已知原因。

旧日志之后正常退出不证明原画布所有修改保存。找到的 preserved-window-before-restart-20261116.ptstudio.json 是暗室工作文件备份，不等同于旧画布未保存revision回执。本调查只读读取，两次哈希相同并保留原路径；见OLD_LOG_AND_PRESERVATION和PRESERVATION_FINAL。无法从缺失记录重建旧画布revision，不断言数据已丢失或已全部保存。

## 调用链和共同设计缺口

MainWindow.Window_Closing分别处理智能文件夹、策划、预约、暗室、联机；策划/暗室/联机各有saving/flushed/disposed标志及再次Close排队。画布另在AssetLibraryPage中订阅同一窗口Closing并自行保存/Close。双方不知道另一方取消的是哪次尝试。

FreeCanvasView._saveGate和CanvasDocumentStore._gate负责串行保存，tmp写完后替换，_saved在写入成功后推进。89b64fd2允许保存失败且未进入不可逆释放时重试释放任务；本轮锁文件、解锁重试中正常，应该保留。

图像链是Editor.Changed→debounce→LoadPreviewsCoreAsync→provider→_images。HideCanvas只移除Content和_canvas。Unloaded停止计时器/发取消，却没有终止epoch与可等待任务槽。Page.DisposeCoreAsync等待AsyncThumbnail和ViewModel，**不等同于等待FreeCanvasSurface自己的任务**。

共同缺口是：关闭意图、文档保存revision、异步终结和图像所有者分属不同状态域，缺少明确协调边界。这是调用链和多场景支持的设计归纳；**不是证明所有内存问题都由一次关闭重入引起**。不可变快照、共享图像、预算缓存和取消仍各有职责，不应一起删除。

## 隔离结果和自然内存

|场景|最终观察|
|---|---|
|已保存后关闭|2次Closing事件、1次真正关闭，无异常。|
|嵌套消息循环关闭及独立重复|同生产栈异常，原关闭未完成。|
|另一处理器取消一次|取消被画布独立重发覆盖，最终关闭。|
|重复关闭请求|共享pending过程，最终正常关闭。|
|保存gate未完成再重复关闭|持gate期间不关；释放后正常关闭。|
|Yield前隐藏画布|关闭意图丢失，窗口保持打开。|
|工作区先Dispose再退出|正常，无重入。|
|取消关闭准备|dirty对象保留、返回false，自动保存恢复。|
|保存失败/解锁后保存再释放|旧文件不变、dirty保留、失败不进入不可逆释放；重试成功。|
|保存、撤销/重做、存储重开、真实PNG编码|文字恢复、160×120导出成功；是当前基线保全，不是新修复验收。|
|卸载后provider晚到|取消后仍发布1张4MiB图像。|
|卸载后编辑事件|重新启动预览工作。|
|工作区Dispose时provider在途及独立重复|Dispose先完成，晚到图像仍入字典。|
|已加载图像后Unloaded并自然空闲|被显式保留的surface仍持16MiB图像；不单独判为无界泄漏。|

最终15项加额外1项在独立PID运行，2GiB/50ms守卫不变。PrivateBytes峰值145.00MiB；完整WorkingSet峰值、自然GC计数见RUN_SUMMARY及逐项RESULT。无强制GC、新增RAW=0。采样含启动、故意等待和空闲，不是产品操作耗时。小合成图片不替代40MP/设备验收，不要求释放后OS立即返还全部WorkingSet。

首轮缺CanvasId和fake library id的两次夹具失败均标为NOT_PRODUCT_FAILURE，保留于fixture-history。首次编译的内部属性访问错误日志留在包外运行根；修正后完整重跑，未放宽根因断言。

## 三方案及审核边界

详细比较见OPTIONS_AND_MINIMAL_PROPOSAL；补丁职责、所有权和接手成本见PATCH_AND_OWNERSHIP_AUDIT。

|方案|跨模块影响|释放时机与数据风险|维护成本|
|---|---|---|---|
|A 局部guard/排队/取消检查|最小，保留各Closing处理器|可减少晚到发布；多方取消、嵌套循环仍无统一保证；隐藏就全清会误伤恢复及共享导出|短期低，叠加状态和返工成本高|
|**B 单一根窗口关闭尝试 + 画布可等待生命周期（推荐）**|根窗口与画布5个现有文件；保留宿主、像素及缓存架构|先保存和全体同意，再终止发布、drain、释放本会话；取消/保存失败仍可编辑|中等；状态与所有者明确，适合增量交接|
|C 全模块Session/事务/租约统一重构|暗室、联机、画布、选片、任务、退出、缓存全面迁移|理论一致性强；迁移中易提前释放共享缓冲、错删发布中间文件、改变旧身份|最高，不适合当前最小范围|

最强反例：B若保存r1后后台又提交r2，或导出仍持共享图像，或另一Closing处理器仍能取消并嵌套pump，单一“关闭Task”也会丢修改、误释放或重入。必须限制变更/发布revision、只释放自身租约、汇总关闭参与者并区分失败阶段；否则拒绝B。不能仅新增协调器却保留独立Close链。

按用户第5项，**本轮没有实施产品修复**。审核B的范围及验收门禁后，再改产品并验证保存、撤销重做、独立重开、导出和自然内存。现有回执证明当前机制和基线保护，不构成修复后通过。

## 证据与参考

- RUN_SUMMARY.json、receipts/：最终与重复运行。
- SOURCE_BINDINGS、FRAMEWORK_BINDING：源码blob、编译组件及实际WPF版本。
- OLD_LOG_AND_PRESERVATION、PRESERVATION_FINAL：旧栈与只读保全。
- PRIMARY_REFERENCES：微软线程模型/协作取消、WPF v10.0.0固定提交参考。实测WPF为10.0.10-servicing；按ProductVersion提交查询源文件得到HTTP404，未把参考源码伪称为测试DLL的精确源码。根因证明以实际运行栈为准。
- RULES.md：重复复现导出的短规则，未改产品AGENTS。
- SKILL_PROPOSAL.md：限定流程提议；未创建Skill、未写持久记忆。

原始运行根 E:/Kitao-Deliveries/close-lifecycle-investigation-20261117。数据库、源图、原始采样及工作文件原地保留，不进Git，不移动或删除用户资料。


## 产品Codex独立确认的外部反例

已读取主线RUNTIME_OWNERSHIP_REPRODUCTION和原运行RESULT：仅请求rootA恢复时，未改生产SingleInstanceManager的同名进程回收同时结束自有rootA和rootB夹具。两个owner分别持不同实例锁，均无窗口且超过16.5s；recoverA=0，ownerA/B被终止。真实产品进程和用户root没有参与。副本为PRODUCT_CODEX_OWNERSHIP_EVIDENCE.json，本调查未重跑该实验。

因此“进程名相同、无窗口、时间较长”不是同root所有权证明。关闭期无窗口/慢保存与外部恢复的交互是B必须考虑的风险；该跨root缺陷独立于CanvasOwnerClosing重入，不能宣称实施B会自动修好它。主线负责对应最小修复建议。
