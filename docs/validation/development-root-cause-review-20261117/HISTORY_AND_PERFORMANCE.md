# 14天历史与性能复盘

统计窗口：2026-11-04T00:00:00+01:00至2026-11-18T00:00:00+01:00不含。当前功能源码e8180221，交付起点fdf01831。全refs135个唯一提交对象，按相关主题得到71个候选，另沿调用链补入a9932b73等。候选不是唯一Bug数量。GitHub只读API成功，窗口内updated PR为0；因此没有可排名的近期PR耗时，不把提交冒充PR。Git合并提交0，未找到明确Revert标题；两笔标题含rollback的缓存提交是运行时失败回滚，不是Git撤销。

## 架构演变与返工

|链路|证据|判断|
|---|---|---|
|HTTP选片入口→层次收敛|34b72f44→c44f800a；6f28d5ba/feef4bc2→181de897|网络实现移到桌面基础设施、Core保留契约是有意设计；IHttp接口名不等于Core仍含网络实现。确认回执、服务身份与丢响应恢复有独立职责。|
|同一项目/相册身份|7769b8ee→0776b988→6f9f4027→be2f9c70|互反身份要跨SQLite、普通保存、编辑工作文件和回写贯通，不能只在VM增加字段。保护当前来源能力与同项目回写是不同层的修复。|
|原始来源与派生交付路径|be2f9c70使用Concat追加selected LocalSourcePath的父目录；a9d1bb96改用SourcePolicy|这是明确的历史回归与后续修正：LocalSourcePath在交付素材中可指向渲染中间文件，不能等同原片源目录。旧污染数据需要明确重定位，不能自动删除用户目录。|
|大栅格/工作文件|edd6d25e=17a95b5a；e28074f1→14eda5c7|第一对稳定patch-id相同，是移植；第二对保留缓存转移并合入WorkflowProjectId保存/重开/失败回滚，不应称完全相同补丁，也不是第二次Git回滚。|
|sRGB恢复与隐藏预览|a9932b73；b1352e64=62191dff=e8180221|分别管瞬态编辑通知/快照恢复和不可见视图排队/持有，不互相替代。三个隐藏预览提交patch-id相同，不算三次独立修复。|
|关闭补偿来源|206051eb（2026-10-04，窗口外因果追溯）；89b64fd2（2026-11-02）|Yield/pending/allow早已存在，不能再以追加Yield称新根治。保存失败可重试有本轮基线支持，应保留；何时与所有参与者同意仍缺协议。|
|包/诊断绑定|增量构建陈旧DLL、原guard强杀后exit=null；5bc4672c修等待/Scope标签|是取证和构建边界，不能计作像素公式性能变化。编译时间戳不替代方法/版本/字节校验，空exit不等于成功。|

精确SHA、稳定patch-id与祖先关系见PATCH_EQUIVALENCE；原始135条历史和19项变更细节保留本轮原始根。fdf、e818、621、证据提交与包SHA各有不同身份，不强行合成一个“版本号”。

## 性能证据与限度

可比较的已有7752×5184 TIFF“系统目录确认→Completed侧车”区间是14.655s、14.174s、14.268s。包含UI派发、编码和文件IO，是端到端区间，不是纯编码CPU时间；样本3且环境不同，不推断统计提升。充分像素/ICC证据已在fdf，不重复40MP/RAW。

根恢复夹具区间3.447s包含生产固定3秒激活ACK等待；16.5秒老化是人为必要条件准备，不是产品慢操作。普通App的42分钟/4分钟或旧数小时lifetime包含调查、空闲、对话框和操作，不能当导入/导出延迟。主UIA前后全树快照、祖先/Pattern读取和截图超时是自动化成本；实际阶段需要单独计时。构建publish日志没有明确elapsed timer，本轮不能据文件mtime虚构构建耗时排行。

静态可确认的候选：

- SqliteProjectRepository.ListAsync一次Projects查询后，对每个项目另查Sources和Inputs，1+2N；结果回写路径可能先SaveCurrentProject+Reload，再LoadVisible查身份，再Upsert+Reload。用户规模耗时未量测，不直接改存储。
- 同样出现两次ScanAsync调用点，不等于正常流程重复索引：另一处在教程恢复分支。当前新闭环的重扫是明确验收操作；未证实自动重复索引。
- StudioQuickExport.ReadEncodedInfo已用DelayCreation/OnDemand读头；现有immutable raster/prepared cache减少重复解码。整文件内容哈希读取与整帧像素解码是不同成本，不能为减少IO去掉身份校验。
- 两张合成JPG的空名称失败在拒绝前已经产生并持久复制两张PNG，普通界面可复现；新作业GUID造成另一组未引用中间文件。已保留失败与成功作业，未清理或夸大成用户照片丢失。
- 服务重复启动在当前闭环没有复现：新隔离选片服务只建一个，手机复用已有53528；旧当前日历已退出后才恢复新40724。已有启动脚本按路径/创建身份或实例健康复用，不把不同运行根的服务计为重复。

## 新工程师接手的认知成本

主要成本来自同词跨域和隐式副作用：PhotoProjectRecord.Id是归片身份，SelectionProject.Id是相册身份，WorkflowProjectId互指；LibraryId/AssetId属于库，ActiveEditVersionId/SourceHash约束像素来源；path既可能是原片、代理、中间文件或最终输出。SaveCurrentProject还写SQLite并刷新整份历史，CreateForWorkflow的Task正常返回却可能业务拒绝，Unloaded/取消/Dispose也不是同一终态。

短期简化：先读HANDOFF及身份/完成条件表，按证据ID再看调用链；在触及范围将请求/尝试/文档revision/源角色写清，少用全域Busy、pending和flushed代替凭据。不全库批量改名，不将现有存储/缓存统一进大框架。当前旧selection.js/css已经不在树中，不提出清理不存在的客户端；None provider、根哈希单例、session.lock和互反ID约束都有实际职责，不能当历史包袱删除。
