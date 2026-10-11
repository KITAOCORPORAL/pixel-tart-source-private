# 第二批：需求追踪

基线：de6faa156ff4e93995be43bdd0d6da527ec8b198。日期：2026-10-11（Europe/Berlin）。状态只覆盖本行描述的范围；用户验收统一 NOT_APPROVED。

| 项目 | 实现及真实调用链 | CODE | TEST | RELEASE_RUNTIME | USER_ACCEPTANCE |
|---|---|---|---|---|---|
| 操作并发/恢复 | IdentityBridgeRepository.Recovery；CAS 领取、代次、OS 独占锁、显式失败重试 | PASS | PASS | NOT_RUN | NOT_APPROVED |
| A4 稳定文件交接 | App → WatchFolderCameraAdapter → CompleteStableAssetAsync → TetherLibraryHandoffService → ImportWithIdentitiesAsync | PASS | PASS | NOT_RUN | NOT_APPROVED |
| RAW/JPEG 独立身份 | 原配对服务保留；各内容独立入库/桥表 | PASS | PASS | NOT_RUN | NOT_APPROVED |
| 捕获上下文 | 发现时持久冻结；交接写入 ProjectAssetLinks/BookingAssetLinks/ShotCaptureRelation | PASS | PASS | NOT_RUN | NOT_APPROVED |
| A5 策划/画布引用 | MainWindow.ReferencePathResolver 和 FreeCanvasSurface 调用统一 Resolver；显式参考→成片关系 | PASS | PARTIAL | NOT_RUN | NOT_APPROVED |
| 工作台 | 沿用项目/排期到素材库入口与 ProjectAssetLinks，未新增独立工作台文件解析器 | PARTIAL | PARTIAL | NOT_RUN | NOT_APPROVED |
| 移动/离线 | 素材库已重新定位后按 AssetId 解析；离线/变更拒绝；不全盘猜测迁移路径 | PARTIAL | PARTIAL | NOT_RUN | NOT_APPROVED |
| A6 标注 | LinkedTetherAnnotationService；素材库摄影师字段权威、客户字段独立；三种冲突动作 | PASS | PARTIAL | NOT_RUN | NOT_APPROVED |
| A7 Studio | 素材库/联机路径+身份入口、v1 可选 LibraryId/SourceHash、重开/导出校验 | PASS | PASS | NOT_RUN | NOT_APPROVED |
| EditVersion | 仍为现有 Target 调整快照；独立版本目录及导出清单未实现 | PARTIAL | PARTIAL | NOT_RUN | NOT_APPROVED |
| 文档包 | PlanningDocument.Package；DocumentId/ProjectId/正文/素材/Shot/画布版本/未知字段；复用原子 sidecar 与 draft | PASS | PASS | NOT_RUN | NOT_APPROVED |
| 画布插入/更新 | 策划情绪板真实入口，保存 PNG+可编辑对象快照，更新追加版本 | PASS | PARTIAL | NOT_RUN | NOT_APPROVED |
| DOCX/分页试验 | 独立 Open XML 测试工程、WPF DocumentPaginator 验证；未接产品编辑器 | PASS | PASS | NOT_RUN | NOT_APPROVED |
| 色彩版本基准 | v1/v2 字节黄金；v3 独立候选线性曝光；正式栈仍拒绝 v3 | PASS | PASS | NOT_RUN | NOT_APPROVED |

不能据此声称整个软件、专业 RAW 管线或完整 Word 功能验收通过。
