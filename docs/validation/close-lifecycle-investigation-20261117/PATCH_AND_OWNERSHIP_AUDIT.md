# 既有补丁、职责及接手成本

## 当前分支包含关系

本表按实际Git差异及当前调用点核对，不按提交标题推断“合并”。关闭Canvas文件在0a45ee0e、e8180221、fdf01831是同一blob；新的内存补丁没有改掉旧关闭协议。edd6d25e已由17a95b5a整合，e28074f1已由14eda5c7整合并保留WorkflowProjectId差异，不能再cherry-pick一次。

|补丁|仍存在的具体职责|与其他补丁关系/裁决|
|---|---|---|
|206051eb|CanvasOwnerClosing中Yield、pending、allow，缓解同步保存后递归Close|确有历史用途，但本轮nested pump推翻其“已离开Closing”的充分性；在B迁移根关闭协议时替换整条独立重发链，不能仅再叠优先级。|
|fc811b54 / 89b64fd2|画布保存失败提示、Page/Host共享释放Task；准备失败可重试，进入不可逆释放后保留fault|真实保护仍有效；锁文件/解锁重试复验正常。不是已失效补偿，不删除。|
|17981934|隐藏旧显示视口不请求原尺寸细节，释放显示所有者|作用于显示/UI，不替代解码分配或会话缓存回收。保留，需与新照片页所有者区分。|
|a3bc3127|StudioQuickExport预览用独立CachedBitmap(OnLoad)脱离线程相关decoder，减少全尺寸托管staging|解决decoder持有和临时复制；不等于资源作用域结束。与后续复用/转移互补。|
|4c3503c9|按内容身份复用不可变栅格，避免导出重复读取|源身份/不可变契约仍必要，不可简化成仅path命中；不是关闭补偿。|
|edd6d25e→17a95b5a|渲染输入复用冻结栅格；导出信息读header，不重新整帧解码|减少另一路重复读/分配，和a3bc的单次解码脱离职责不同。保留。|
|e28074f1→14eda5c7|prepared会话转移缓存、清空staged所有者、失败回滚旧缓存/项目身份|涉及事务与所有权，不能删除清空/回滚当“重复代码”。后续可封装成明确Take/Adopt操作，不改变语义。|
|a9932b73|恢复prepared快照时抑制同步属性通知所触发的临时处理|保障暂态不被当新编辑；不改曝光公式。是重入状态保护，尚无证据可删。|
|e8180221|隐藏胶片页不启动LightPrint预览；可见性与功能启用分开|是显示作用域门禁，不与原尺寸视口/源缓存同责。验证了“隐藏仍在工作”的同类历史问题，但未覆盖FreeCanvasSurface。|

源头脱离、内容复用、prepared转移、显示可见性是四种不同职责，未发现可直接删除的一组重复内存补丁。明显重复的是每个模块自建关闭/取消/发布状态协议；正确方向是明确少量生命周期合同，不是还原已经消除的重复整帧读取。

## 当前所有者地图

|对象/任务|拥有者与终止边界|发现|
|---|---|---|
|_saved / Editor.Document / _saveGate|FreeCanvasView保存事务|_saved只在持久写成功后推进；保留。ReferenceEquals依赖不可变文档语义，不是通用“版本号”。|
|_canvas / CanvasWorkspace.Content|AssetLibraryPage|HideCanvas丢引用但没统一drain；跨await读取新_canvas会丢原关闭目标。|
|_images / _widths / _loading / CTS|FreeCanvasSurface显示会话|局部generation不含Active/Terminal条件；取消未传provider，终止后仍能发布。|
|_sourceCache|ReferenceColorWorkspaceViewModel当前会话冻结源|与staged会话转移、失败回滚相关；不是全局显示缓存。当前128MiB是逐出估计且允许单个活动大项，不是硬内存上限。估计按4B/像素，是否覆盖高位深实际成本需独立测量，未升级为本轮实测根因。|
|PreparedSourceState|编辑器prepared快照|持有精确源/互动源/master等不可变引用；转移及回滚需要同一实例，不应一律Clone。|
|导出取消源、冻结输入、临时输出租约|正在执行的导出/发布操作|关闭可以发取消；释放仍由操作finally完成。不得用UI Unloaded直接清全局所有者或删除发布目录。|
|ViewingFrames/缓存/资源预留|ViewingTable会话|ReleaseViewing同时取消、移除帧并释放预留；与CanvasSurface的纯发取消不同，不能当已统一。|
|_disposeTask / _disposed|Page/Host释放操作|Page的_disposed表示已跨过保存边界并开始清理，而非所有任务已经完成。HasStartedDisposal命名更准确；不可逆阶段fault与可重试保存失败必须分开。|

静态源码还显示MainWindow在取消后可能残留某模块flushed状态或IsEnabled=false，App.OnExit同步等待模块异步Shutdown也可能依赖Dispatcher工作。本轮未复现完整Shell这两条，不把它们列成已证实故障；B实施必须检查。

## 接手工程师的认知负担

一次关闭至少要理解MainWindow的6个模块标志、Canvas的pending/allow、文档_saved/_observed/_discardPending、Page/Host的Task缓存和不可逆边界、Surface的CTS/generation/已发布图像，再回看206051eb与89b64fd2，才能区分“已保存”“允许最终关闭”“已经开始释放”“释放任务已完成”。SOURCE_BINDINGS列出本调查实际读过的12个产品文件，是可核对的最小阅读入口；不虚构理解所需小时数。

|当前名称/模式|可简化方向|不能抹掉的区别|
|---|---|---|
|_planningCloseFlushed等永久布尔量|CloseAttempt中的参与者准备结果+文档revision|一次保存成功不等于后续编辑也保存。|
|_allowCanvasOwnerClose|根窗口单次最终关闭许可|允许关闭不等于已释放；不能继续由每页独立设置。|
|_disposed / _disposeTask|显式释放阶段、一次在途Task及失败阶段|开始清理与结束清理不同；失败前后是否可重试不同。|
|CancelLightPrintTask(true/false)|采用有意义命名参数/操作名，如是否恢复草稿|“取消后台计算”“恢复草稿”“终止会话”不是同一个动作。|
|_sourceCache|SessionFrozenRasterCache；把staged转移封装为Take/Adopt|活动session与prepared staging所有权不同；rollback不是冗余。|
|_lightPrintEnabled / _lightPrintPreviewVisible|FeatureEnabled与SurfaceActive|功能允许与当前应不应该产生工作不同；不要合并成一个bool。|
|activation revision / preview generation / edit version|分别命名为ActivationEpoch、PreviewEpoch、PersistedEditVersionId|临时结果失效与持久编辑身份不同，不能复用同一计数器。|

本轮仅提出这些命名/职责建议，不批量改名、抽象新框架或删除旧兼容逻辑。
