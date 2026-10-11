# 标注撤销与旧 API

## 生产路径

PhotographerAnnotationStore.CompareExchangeAsync 在同一个 SQLite 立即事务中执行三字段 CAS、读取提交后 Revision、追加 annotation-cas-v3 日志及 AnnotationUndoVersions。批量共享操作 ID，只把成功项加入日志；Conflict/Failed 不进入撤销。无变化写入不生成空历史。

素材库 ApplyUserAnnotationsAsync 将返回 token 交给现有 BrowserCommandService；Undo/Redo 按既有持久日志执行。Studio 与联机提供摄影师标注 Undo/Redo，使用同一素材库日志，客户备注保持独立。冲突后刷新权威值并提示，没有覆盖他人编辑。

撤销/重做先核对提交后的 Revision 和字段值。执行后只推进该历史链对应的版本栅栏，支持连续撤销/重做；测试确认本地撤销不会复活跨进程修改前的旧历史。旧无 after-state 日志仍拒绝不安全恢复。

## 历史接口策略

- UpdateAssetMetadataAsync / UpdateAssetsMetadataAsync 有先前读取快照时按原快照提交。
- 无快照调用是后台兼容填充，仅允许 revision=0 且原字段为空；已有用户值或已清空但 revision>0 时返回 BackgroundProtected，不伪造期望版本。
- AssetPresentationMetadataStore.SaveAsync 的无快照颜色同样只填未编辑空值；用户设置/清除颜色使用 CAS。URL 独立。
- 重复导入不重置标注；后台联机交接继续使用显式 CAS。

## 测试

BatchFiveTests 覆盖单项、成功/失败混合批次、连续撤销重做、后台重复调用、ABA、他人修改后拒绝旧撤销。测试曾复现连续撤销版本推进失败，修复后通过。

Kitao.ProcessProbe 使用两个真实进程竞争，保留第四批 CAS/ABA/锁释放/素材失败重启测试；新增 undo-prepare→undo-other→undo-stale：其他进程修改后旧 token 被拒绝。脚本和脱敏输出随本批提交。

限制：历史最多保留 100 个操作；历史裁剪后不能撤销更早操作。Studio/联机当前页面内栈用于入口提示，持久日志仍在库内；不声称客户备注也进入摄影师撤销链。桌面快捷键/视觉反馈未实测。
