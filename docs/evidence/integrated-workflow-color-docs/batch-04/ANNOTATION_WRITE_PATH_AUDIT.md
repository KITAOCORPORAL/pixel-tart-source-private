# 标注写入路径审计

## 用户入口

| 入口 | 调用链 | 期望状态来源 |
|---|---|---|
| 素材库键盘/评分按钮 | AssetLibraryViewModel.RateSelectionAsync → ApplyUserAnnotationsAsync → PhotographerAnnotationStore.ApplyBatchAsync | 已显示 AssetItem 的 Rating/Color/Comment/AnnotationRevision |
| 右键评分 | RateContextAsync → 同上 | ContextIds 对应图卡快照 |
| Inspector 单张备注 | SaveInspectorDetailsCommand → 同上 | 选中素材快照，不提交前刷新 |
| Inspector 批量色标 | ApplyInspectorColorCommand → 同上 | 每张读取快照，逐项 Success/Conflict/Failed |
| Studio 单张/批量评分色标 | OnTargetMetadataChanged → FIFO → CompareExchangeAsync | 点击时捕获 AnnotationSnapshot；仅衔接本进程已成功写入的版本链 |
| 联机编辑 | LinkedTetherAnnotationService → 已持久 ExpectedLibraryRevision → CAS | 读取时版本；客户字段不写素材库 |
| P3 高级批量 | PreviewBatchMetadataAsync → ApplyBatchMetadataAsync | 原预览状态指纹新增 AnnotationRevision，事务内复核；整批冲突拒绝，并提供逐项结果 |

## 后台和兼容入口

- ImportWithIdentitiesAsync 的重复导入不重置 Rating/Comment；后台交接先检查不同已有值，再 CAS，不使用用户“采用另一侧”语义。
- UpdateAssetMetadataAsync/UpdateAssetsMetadataAsync 保留接口。仓储已有读取快照时必须匹配 revision；没有旧读取快照时保留兼容的后台写入语义。这些无显式期望版本接口不再用于生产用户评分/备注入口，但任意外部调用方仍可能误用，列 PARTIAL。
- AssetPresentationMetadataStore 的 URL 不属于摄影师字段；生产色标用户入口已改 CAS。低层 SaveAsync 仍供兼容/测试使用，不能称为强制全局只读版本 API。
- 现代 metadata-v2/P3 撤销记录保存 AnnotationUndoVersions；撤销/重做前核对版本，执行后更新版本。缺少 after-state 的旧 v1 元数据日志拒绝撤销，避免覆盖后来编辑；其他旧关系数据保持可读。
- 图卡读取版本来自同一次 SELECT，未在提交前读取最新值掩盖冲突。

## 两个真实 OS 进程

代码：tests/Kitao.ProcessProbe/Program.cs；复跑脚本 Run-ProcessChecks.ps1。

实际启动独立 dotnet 进程，临时 SQLite 与合成字节文件：
1. 同一 expected.json，两进程并发 CAS，恰好一个成功。
2. ABA 改回原值后旧 revision 拒绝。
3. 子进程持有 SQLite 写事务，强制结束该测试进程后新进程成功写入。
4. 产品侧写入成功、注入素材侧 IOException；客户备注与草稿保留。
5. 新进程读取草稿重试成功；再次旧进程状态提交被拒绝。

PROCESS_RESULTS.json 为脱敏结果；原始数据库留在 artifacts，不提交。测试不会访问生产图库。

## 剩余边界

旧无读取快照 API 仍有后台兼容语义；P3 复杂批量保持整批事务而非逐项部分提交；新增直接 CAS 用户操作未统一接入旧撤销栈，不能声称所有标注操作的撤销 UX 完整。新写入的并发保护和逐项结果已实现并测试。
