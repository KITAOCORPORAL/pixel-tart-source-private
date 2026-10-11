# 跨模块实施

## 身份操作

第一批 BeginOperationAsync 会清除 Conflict、重置 Running；CompleteOperationAsync 可改写 Ready.AssetId；比较未 Trim。先添加三项测试，三项均失败，再修复。旧接口继续可用，但 Begin 不再暗中重试。生产交接必须 TryClaimOperationAsync，完成/失败带 Attempt 代次。

生产交接在执行期间持有按产品库+幂等键生成的 OS 独占文件锁；崩溃释放句柄后，残留 Running 才能恢复。CAS 代次拒绝旧完成写入。Conflict 不自动清除。

## Watch Folder

保留 FileStabilityProbe、TetherPairingService、TetherSafeCopyService。只有安全复制服务落库的 ProjectCopyPath 被使用，失败目标路径不被推断为可用。导入为引用模式，不复制 RAW。

发现回调冻结 ShootExecutionContext 到产品数据目录；稳定后桥接现有 LibraryId/AssetId。产品库、素材库、策划 sidecar 是分步提交；操作记录用于失败定位和重试补偿，不是单一事务。重试导入命中原 AssetId。

素材库宿主已持有写租约时，交接借用同进程租约并引用计数；其他进程仍受独占文件约束。

## 策划与画布

素材库 ResolveStableAssetAsync 复用 AssetLibraryIdentityResolver。策划参考和自由画布预览均接入。参考→成片使用用户明确选择的 ReferenceId/LibraryId/AssetId，未按同 Shot 猜测。ProjectAssetLinks 仍为多对多，不改已有主键。

## 标注

入库前 TetherAnnotations 保存摄影师和客户字段。入库后摄影师星级、色标、备注由素材库读取；客户备注和客户收藏继续保留在产品库。检测到不同已有值时提示冲突；提供保留素材库、采用联机、合并摄影师备注。客户备注不参与合并。失败可能留下已保存的产品侧副本，界面说明部分完成并允许重试。

## Studio

素材库 QuickTools 增加真实 Studio 入口；联机完整编辑入口传递文件路径及可用桥接身份。ColorStudioSessionTarget 增加可选 LibraryId/SourceHash，继续读取 Version 1。载入、重开、快速导出和发布准备均验证内容。历史 ProcessingVersion 未改变。

剩余：跨库切换后的统一重新定位向导、导出结果独立身份清单、EditVersion 多版本管理、标注多进程冲突的版本戳比较仍需继续加固。
