# 外部审查处置（先核实完整当前源码，不直接照旧提交意见修改）

基准远端b40bccfd459902d8761fd53a6f05c0894350a148，fetch/ls-remote未更新。本地52efba2d含本轮色彩/即时/原尺寸后续工作，全部保留，不退回。

|审查项|本地核实结论|已有保护或修复|验证证据|剩余边界|
|---|---|---|---|---|
|普通正式导出缺安全发布|已有保护：Encode只写temp→CommitOutputAsync→无覆盖move→receipt→success|不全局改Encode；固定output.tmp改每次同目录UUID|ReviewSafetyTests源hash/已有输出/racing final/编码后故障/取消/重试/receipt hash；旧BatchExportProcessedPixels28项|WIC单调用不可抢占；人为断电/真实满盘未硬件实验|
|临时文件清理误碰他人|已确认固定tmp有真实风险|新UUID仅自己TryDelete；RAW/V3与receipt恢复不改|foreign output.tmp字节不变；无自己staging残留|外部恶意进程猜测UUID不是本机权限隔离|
|源路径与覆盖|已有正式输出名及CommitOutput无覆盖，项目保存源路径需显式拒绝|源照片不能作为session输出；新旧final均不覆盖|源hash、existing/racing final、失败无成功receipt|硬链接/网络存储非本机对抗测试未全覆盖|
|重试成功文件覆盖|已有：Retry只Failed/Partial，恢复需version/source/output hash|保留；补真实两照片成功/失败/重试|成功第1张hash不变、失败第2张成功|未授权更改配方不恢复旧receipt|
|会话并发|已确认文件层无CAS；仓储版本已有Revision冲突机制|session跨进程锁+SHA预期值；未读旧文件API拒绝，UI明确替换后仍CAS|双实例后保存拒绝且file hash不变；command明确失败非成功|网络共享锁语义依平台，不声称分布式事务|
|两次session写夹repo写|已确认有半完成窗口|单次嵌入版本snapshot原子保存；新project权威标记防旧repo同ID覆盖；共享repo独立显式SaveEditVersions命令|项目重开/历史；保存无隐式repo写|不是跨项目与所有版本仓储的全局事务；共享仓储命令独立|
|损坏/缺源/激活加载|已确认先清空旧workspace风险|隔离完整准备后提交；commit失败恢复参数/图像/engine/layout/filter/session/history|badJSON/缺源/坏bitmap/commit故障/准备后替换；undo/redo/再次保存|staging当前重解码，原尺寸内存/延迟必须再测；不称零成本|
|版本冲突逃逸|已有SaveEditVersionsCommand针对IOException/InvalidOperationException捕获，repoRevision+恢复snapshot已有|不制造第二套repo冲突处理；扩展session同类catch|既有ConflictHandlerPreservesLocalAndRejectsSecondConcurrentUpdate|多资产目录逐项存储不全局原子|
|PixelTartPublishing生命周期|已确认创建后无统一owner释放协议|owner清单/状态/创建者锁；handoff保留；preview/export读锁；用户显式release逐文件hash校验|活跃锁拒绝、改写拒绝、未知文件保留、exact owned删除|重启不自动扫删，显式重新加入后释放；旧无owner目录保留|
|RAW/V3导出|已有条带临时+验证，外层CommitOutput|保留原保护，仅外层UUID与临时生命周期增强|旧RAW/V3 tests+最终交付专项|Fuji原生/ICC物理DPI仍独立|

工具StudioQuickExport.Encode仍保留受控临时/测试/明确覆盖使用，src正式调用仅工作区temp与RAW中间temp；发布普通路径使用独立task目录编码且receipt在之后。Core PublishingExportService已有unique publishing temp/Verify/source前后SHA/CommitOutput，非本轮新造保护。私有未使用EncodeJpeg不属正式链。
