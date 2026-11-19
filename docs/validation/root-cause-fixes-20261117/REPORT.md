# 光镜根因修复执行与交付

2026-11-19当前产品源码d07e4f43，整体PARTIAL。cb8c1adc普通连续编辑/撤销再触发2GiB守卫（2227568640bytes/exit1）；失败证据已固定。新修订只修正分析缓存强持有旧整帧，以及预览缓存按实际位深计费。41项相关回归和受控40MP连续编辑/撤销重做/保存/独立ViewModel重开通过，PrivateBytes峰值1321.35MiB；未强制GC、新RAW0。

普通d07完整EXE已启动，停在空暗室；原生自动化持续30s超时，恢复工作文件尚未打开，普通TIFF保存/正常关闭/独立进程重开/两次TIFF导出仍未通过。手机同源码服务及原合成预约/新设备凭据已准备，Tailscale仍NeedsLogin，HTTPS与iPhone未验。最新下一步只以HANDOFF.md及retained-preview-followup-20261119为准。

原433普通JPEG同项目主线PASS继续绑定433，来源路径/数量保持但子行GUID会重建。家中与内存补丁映射已包含。以下为保留的根因修复阶段报告，历史“当前/待办”不作为本轮断点；原完整顶部进展已归档history/REPORT_before_retained_owner_update_20261119.md。

## 决策、基线和源码

本轮用户已批准按B分阶段实施，并确定项目名称必填，不自动创建“未命名项目”。授权记录在EXECUTION.md。起点integration/company-functional-20261116@f6655aff201b157a09ee37c46eb82596ff59028f；原功能源码e8180221937fa4abb578a16faacebe99a5d4ac1b。fetch核对无后续产品修改，原主线与调查树保留。

修复在独立树 E:/Kitao-Worktrees/root-cause-fixes-20261117，分支 fix/root-cause-b0-b1-b2-20261117，分阶段提交：B0=f55825bb；B1=057d58ff；B2=eef3b35b；反证补充及最终功能源码=**433a1d8e88d839839e4f21341e3e2776696d33a5**。后续文档/工具提交不改变产品src/tests；远端SHA与包摘要另见DELIVERY.json和包外REMOTE_READBACK.json。

## B0：按实际互斥量获取，不强杀不明进程

根因是原恢复按进程名/品牌/无窗口/年龄筛选，无法证明实例数据根归属。新实现完全删除枚举与Kill；只获取当前实例名的Mutex，使用WaitOne(0)判断实际所有权，AbandonedMutexException表示实际已接管。createdNew不再决定所有权。

已有实例先请求激活并等待确认；无确认则再次检查Mutex是否已释放，否则返回ExistingInstanceUnresponsive。App给出中文提示，说明可能正在启动/保存/关闭并保留进程。没有引入全局Session、lease或journal。保守结果是无响应的同根实例不会被自动回收，需要等待或由用户判断；这是为未保存数据保全选择的明确行为。

反证覆盖：A/B不同root共存、无窗口且超过旧15s阈值的慢启动/无响应实例、B存在未保存工作、正常同根激活、已有但未持有的kernel mutex、abandoned mutex，以及伪造旧rootA元数据指向B的PID。实现没有PID查询/终止路径，因此PID复用不能授权结束任何进程；没有声称强迫操作系统实际复用了同一个PID。B0_RESULT保留阶段记录，B0_GUARDED_RESULT为补齐50ms采样后的最终轮。

## B1：单次关闭与本作用域资源终结

根因C1是Yield在嵌套消息循环中仍可回到原Closing内；C2/C3是组件独立重发和跨await改用当前画布。I1/I2/I3是取消未下传/缺少终止发布门及画布任务未加入Page释放等待。

MainWindow.OnClosing现在只同步派发参与者取消决定并取消原关闭；全部事件返回后才排队协调。先登记共享Task，重复请求加入同一次流程；捕获最初page/canvas/VM，目标改变则中止，不替换关闭目标。画布不再自行订阅根Closing或重发Close。

准备阶段调用既有保存/取消入口，记录具体画布文档实例；其他参与者保存后暂停并等完画布在途工作，再核对目标、文档和暗室编辑revision。保存r1不代表后来的r2已经保存。取消/准备失败保留对象和编辑状态，恢复页面。进入Releasing后故障留在Faulted，记录实际阶段和保存文件恢复路径，不假装恢复Open。

FreeCanvasSurface明确Suspend/Resume与永久Release。向provider传token，同时用epoch/document/scope拒绝忽略取消的旧结果；登记并等待实际任务后才清本surface图像引用，解除Editor订阅，释放CTS。临时Unloaded不永久释放缓存或编辑器，重新加载可以继续。FreeCanvasView追踪加载/来源刷新/导入/保存任务；Page释放等完画布，不把AsyncThumbnail drain当作画布工作已结束。保存失败前仍保留89b64fd2的可重试槽位；已进入终止期的故障共享原fault。

初始五文件之外有两项最小扩展：App原OnExit同步等待ModuleRegistry.ShutdownAsync，而模块可能需要Dispatcher清理，因此改为最终Close前异步等完；另在ReferenceColorWorkspaceViewModel.R53.cs暴露只读CloseEditRevision用于保存后复核。原OnExit只会遇到已结束的模块任务，没有新UI线程同步等待或嵌套消息泵。

反证结果：16项最终受控矩阵全部通过（真实MainWindow+生产画布类）；另1项注入释放失败也通过，确认共享fault、不可再编辑及保存恢复文件可读。包括嵌套循环独立重复、别的参与者取消、连续关闭、保存gate阻塞、保存中继续产生r2、关闭目标变化、工作区先关、取消、写失败再重试、撤销/重做/存储重开/PNG编码、忽略token晚到、临时离开返回、永久终止不重启。最终17项PrivateBytes峰值约304.71MiB（先前B1阶段峰值320.33MiB的原记录亦保留），无2GiB STOP、无强制GC、新RAW0。此为受控宿主，不是普通EXE全流程。

## B2：名称必填、校验在渲染前、明确创建结果

根因是工作流先渲染/复制PNG，再在相册创建时校验UI空名称；项目记录还另补“未命名项目”，调用方把Task正常返回误当创建成功。

现在发送前先校验名称与当前工作流，所选目标身份/输出版本有效后才建立中间目录和编码；渲染后再次核对身份/编辑revision，变化则拒绝交接。MainViewModel的实际项目保存拒绝空/纯空白，不补默认名；其他表单值保留。CreateForWorkflowAsync与创建核心返回SelectionCreationResult，拒绝带明确消息，成功带ProjectId；Main与编辑页只在明确成功后导航或报告准备成功。

范围仍为当前项目与相册关系，不改变ID，不引入多相册迁移。为落实所有项目保存入口必填，除最初三个VM文件外，最小修改了MainViewModel.cs中的SaveCurrentProjectAsync、输出目录默认提示及现有委托绑定。

B2_RESULT和B2_REFINED_RESULT：空字符串、空格、制表/换行三类均拒绝且相册/业务记录0；其他字段保留；前置拒绝无新增Publishing lease/PNG；空Guid工作流拒绝；名称补齐后明确创建成功；有效素材可实际渲染并进入结果感知的交接。该受控测试未被标为普通UI完成。

## 集成、TIFF和内存边界

最终源码自动回归 **52/52**：内存/快照/sRGB/隐藏预览与选片40、来源/工作文件身份5、保存失败恢复7。TRX、编译告警及结果均保留；告警未伪装为零。最终源码对终止期画布编辑禁用作了补充，已重跑全部17项生命周期场景；旧栅格复用、缓存交接及重试行为没有被删除。

StudioQuickExport、RenderedSrgbInput、RawDisplayBitmapAdapter、prepared Session及ProfessionalRaw核心文件相对e818 blob不变。先前普通40MP TIFF的三份实际导出及像素/ICC一致性证据继续保留原source标签；本轮相关小型栅格/曝光回归通过。**新关闭协议下的普通40MP退出/重开没有再验收**，不把历史结果改标为新源码通过。无新RAW批次。

## 普通EXE、手机及未完成项

原433普通EXE同项目JPEG闭环已完成：两张索引、编辑0.5、真实Chrome锁定v1/r6、PC收回、实际JPEG2/ICC3144、正常退出0、独立进程重开和原来源重扫。此前eef/433阶段的系统对话框待办均归历史，不再列为当前未完成。

433首次40MP曝光和cb8c1adc连续曝光/撤销都触发2GiB守卫；两次失败保留。当前d07受控连续操作已通过，但普通PID56424尚为空暗室：Sky及Open Computer Use出现截帧、几何和30s超时，CUA报AX不可用。没有把工具调用超时推断为新的产品死锁；线程栈/资源记录保留。待普通打开恢复文件后继续连续编辑、保存、正常关闭、独立进程重开、实际两次TIFF导出/像素/位深/方向/ICC。普通空名发送、关闭边界、双root和实际iPhone也未全部完成。

手机已验证旧服务owned路径/创建FILETIME/runtime，换为同源码d07服务PID50824；新设备授权签发、本地200/匿名401、受保护原合成预约revision4全行不变。Tailscale仍NeedsLogin，HTTPS和真实iPhone双向修改未完成；登录链接与token仅存本地私有文件。新凭据到期UTC2026-11-21T06:17:20Z。

## 交付与接手

完整自包含win-x64产品包含App、选片服务、档期服务、浏览器原生桥接和扩展；非暗室单模块。默认入口Start-FullProduct-LocalTest.cmd，配套可指定独立DataRoot的PowerShell启动器；EXE本体App/KitaoPhotoSelector.exe。版本2.3.0+最终功能源码，逐组件哈希和全包清单检查。

包不含SQLite、真实或合成源图、缓存、owner key、token、客户私有链接、个人入网链接，不生成安装器。报告、规则与工具可复放，原始采样和夹具错误留包外。下一步是恢复普通EXE控制后从ORDINARY_EXE_STATUS断点完成真实闭环及新关闭路径TIFF复验；手机仍需本人入网。修复已经实施，无需重新审三方案，但验收缺口保持PARTIAL。


## 迭代中发现并保留的失败

- B0首轮源码断言把注释中的createdNew也当作旧实现残留；收窄为实际out参数，运行记录保留。后补采样器首轮的局部变量C覆盖ctypes别名，使采样线程提前退出；该轮采样不列为最终守卫证据，修正变量并增加存活/非零采样断言后完整重跑。
- B1开发轮中，旧诊断字段已删除仍被夹具读取，修正为当前关闭Task；另一次根关闭实现先调用协调再登记Task、并以IsLoaded判关闭，嵌套循环反例暴露了竞态与错误完成判断。正式B1提交前改为先登记TCS任务、以实际Closed事件确认，随后16项通过。旧运行目录v1/v2保留，不改标为通过。
- 所有产品阶段提交后的反例补充继续单独提交433a1d8e，最终再跑完整生命周期和52项回归。原始日志不因最终通过而删除。
