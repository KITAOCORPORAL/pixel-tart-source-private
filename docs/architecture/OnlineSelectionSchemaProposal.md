# Online Selection V1 Schema Proposal

状态：并行开发分支评审稿（不改变正式 Pixel Tart 数据库 SchemaVersion 5）。本轮只使用独立本地工作区或测试临时目录，不执行产品数据库迁移。

## 身份边界

- 现有 `SelectionAsset.Id` 是选片项目内稳定的 `SelectionAssetId`；代码提供同名只读别名，避免产生第二套照片身份。
- `SourceAssetId` 只是可选的素材库引用。它不是云 URL，也不是 Tether 会话 ID。
- `OriginalFileName` 与 `OriginalStem` 是结果回收的最小快照。RAW 匹配仍交给既有文件名匹配合同。
- `CloudAssetId` 只表示云端副本；删除云端副本不得删除本地源文件、代理 JPG 或素材库记录。

## Desktop 本地工作区

当前工作区快照保留：

| 概念 | 作用 | V1 存储 |
|---|---|---|
| `SelectionProject` | 项目名、客户显示名、状态、目标数、截止日期 | 既有 `ISelectionWorkspaceStore` |
| `SelectionAsset` | 代理图、本地源引用、稳定身份、状态 | 既有 `ISelectionWorkspaceStore` |
| `SelectionRule` | 目标/最小/最大、收藏、备注、锁定规则 | 既有 `ISelectionWorkspaceStore` |
| `SelectionChoice` | 本地客户 Mock 的选择/收藏 | 工作区 `Choices` |
| `SelectionComment` | 本地客户 Mock 备注 | 工作区 `Comments` |
| `FinalSelectionSnapshot` | 确认时的版本化结果 | `SelectionFinalResult` 的快照属性 |

代理图默认 2560px 长边、JPEG Quality 85、sRGB。RAW 只读解码，永不进入上传队列；代理编码不携带 EXIF、GPS、本地路径、电脑用户名或其他非业务元数据。

## 服务端 V1 合同

现有 canonical 路径继续使用 `/v1/selection-projects` 与 `/v1/client/selection`，不并行维护第二套 `/api/v1` 身份。`PixelTart.SelectionApi.Contracts` 提供分页、稳定身份、选择版本和最终快照 DTO；`PixelTart.SelectionApi.Server` 仅提供合同与 Local Dev Storage 抽象，不启动监听器。

生产需要另行提供：HTTPS、合法小程序域名、数据库、对象存储、部署环境和凭据。`Provider=None` 仍是 Desktop Release 默认状态。

## Local Dev Storage

`ISelectionObjectStorage` / `LocalSelectionObjectStorage` 只接受相对对象键，在调用方提供的临时根目录下原子写入代理字节。它拒绝 `..` 越界，不包含管理密钥，不代表生产对象存储。

## 微信小程序 Mock

`clients/wechat-mini-program/` 只有五页：`project`、`gallery`、`photo`、`selected`、`confirm`。所有请求经 `services/api.ts`，状态集中在 `services/selection-store.ts`，网络失败时选择保留在本地待重试。

微信登录边界：`wx.login` 只取得临时 code；正式服务端才可调用 `code2Session`。AppSecret、SessionKey、长期 Access Token 不进入小程序代码。正式网络必须使用 HTTPS 与微信后台配置的合法域名。本目录只是 Mock/Local Dev，不宣称在线选片或小程序已上线。

## 迁移与回滚

本轮不注册 SchemaVersion 6，也不修改现有正式 SQLite 表。后续若进入数据库实现，应先建立独立迁移：新增 selection workspace 表、唯一 `(ProjectId, SelectionAssetId)` 约束、Choices/Comments/FinalSnapshots 索引；迁移失败时回滚新表，不触碰既有媒体、日历、RAW 或生产数据。
