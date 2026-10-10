# 跨模块身份桥实施记录

## 本批范围

本批实际实施 A1–A3。A4–A7 保持后续任务，未以接口或文档存在冒充完成。

## A1 稳定素材解析

新增 `AssetLibraryIdentityResolver`。输入为 `LibraryId + AssetId + ExpectedContentHash?`，输出严格区分：

- `Available`：素材存在，必要时内容散列相符；
- `Offline`：库本身不可访问；
- `Changed`：路径存在，但内容散列与期望不符；
- `Ambiguous`：身份或内容匹配不唯一；
- `Missing`：库可访问，但素材不存在。

Resolver 只读，不隐式导入、不复制文件、不修改数据库或原文件。

源码：

- `src/RAWSelectionAssistant.Core/Services/AssetLibrary/AssetLibraryIdentityResolver.cs`
- `src/RAWSelectionAssistant.Core/Services/AssetLibrary/AssetLibraryContracts.cs`

## A2 导入结果身份

`SqliteAssetLibraryRepository.ImportWithIdentitiesAsync` 返回逐项 `AssetId` 和导入处置。原 `ImportAsync` 保持兼容，并委托到新实现后返回原有聚合结果。相同内容出现多个候选时返回 `Ambiguous`，不任意选择一个素材。

源码：

- `src/RAWSelectionAssistant.Core/Models/AssetLibraryModels.cs`
- `src/RAWSelectionAssistant.Core/Services/AssetLibrary/SqliteAssetLibraryRepository.cs`

## A3 加法迁移与幂等操作

数据库版本 7 仅新增：

- `TetherAssetLibraryLinks`
- `CrossModuleOperations`
- 支撑查询和唯一性的索引

既有 `TetherId`、`ProjectId`、`AssetId` 和已有表未被重命名或替换。`IdentityBridgeRepository` 对相同操作键幂等写入，对冲突映射保留原关系并显式失败。

源码：

- `src/RAWSelectionAssistant.Core/Services/Database/IdentityBridgeSchemaMigration.cs`
- `src/RAWSelectionAssistant.Core/Services/Database/IdentityBridgeRepository.cs`
- `src/RAWSelectionAssistant.Core/Services/Database/DatabaseServices.cs`

## 未实施

| 工作项 | 状态 | 原因 |
|---|---|---|
| A4 Watch Folder 到素材库交接 | NOT_IMPLEMENTED | 第二批；需连接真实联机事件、重试和 RAW/JPEG 配对 |
| A5 策划与工作台稳定身份 | NOT_IMPLEMENTED | 第二批；需在既有 ProjectAssetLinks 与侧车之上接桥 |
| A6 联机标注冲突处理 | NOT_IMPLEMENTED | 第二批；需定义摄影师备注与客户备注的冲突 UI |
| A7 Studio 持久 Target | NOT_IMPLEMENTED | 第二批；需保持 v1 `.ptstudio.json` 完整兼容 |

## 状态

| 工作项 | CODE | TEST | RELEASE_RUNTIME | USER_ACCEPTANCE | PUSH |
|---|---|---|---|---|---|
| A1 | PASS | PASS | NOT_RUN | NOT_APPROVED | 见推送记录 |
| A2 | PASS | PASS | NOT_RUN | NOT_APPROVED | 见推送记录 |
| A3 | PASS | PASS | NOT_RUN | NOT_APPROVED | 见推送记录 |
| A4–A7 | NOT_IMPLEMENTED | NOT_RUN | NOT_RUN | NOT_APPROVED | NOT_APPLICABLE |
