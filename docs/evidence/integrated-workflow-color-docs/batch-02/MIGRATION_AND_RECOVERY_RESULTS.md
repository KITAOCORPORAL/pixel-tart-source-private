# 迁移与恢复验证

本批未增加数据库 schema 版本；继续使用第一批 v7 的 TetherAssetLibraryLinks、CrossModuleOperations。AttemptCount 成为持久执行代次；旧行可直接读取。未操作生产数据库。

## 自动验证

- 三项旧缺口复现：Conflict 被清除、Ready AssetId 被覆盖、Trim 比较不一致，修复前 3 FAIL。
- 16 路领取、12 路 Begin+Claim：只允许一个执行者。
- 中断残留 Running 恢复，旧代次完成被拒绝。
- OS 执行锁排除第二执行者，释放后可重新获取。
- 素材库已打开写租约时可交接。
- 重复交接保留 AssetId，内容改变后冲突。
- 生产 WatchFolder 稳定处理链、RAW/JPEG 两条身份、重复 reconcile 无额外资产。
- 两项目引用同素材，删除一个引用不删除原片。
- 捕获上下文第一次冻结后重启仍保留原 Shot/Revision。
- 摄影师备注合并重复执行不重复追加，客户备注独立。
- 旧策划文档升级、未知字段保留、Studio v1 可选身份字段重开。

## 边界

未做真实断电/网络共享文件锁失效/生产磁盘损坏试验。运行状态 NOT_RUN。文件锁只保护使用该交接实现的执行者；恢复 API 调用方必须遵守先停止旧执行者或取得独占锁的约束。跨库写入采用幂等补偿，不能宣称全局 ACID。
