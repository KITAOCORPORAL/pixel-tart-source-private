# Creative Asset Architecture Preparation

日期：2026-09-10
状态：底层契约准备完成；未实现灵感托盘、灵感集、Moodboard、Planning Center 或日历素材联动 UI。

## 当前素材库能力

- `AssetItem` 继续作为素材库内部记录；跨功能引用不持有 `AssetItem`，也不直接依赖文件路径。
- `AssetLibraryStableReference` 使用 `LibraryId + AssetId + ContentHash` 提供可移植身份。
- Library、Query、Smart Folder、Tag、Inspector、Undo、Task Center 和统一 Thumbnail Provider 均作为后续能力的复用入口。
- 当前“查看信息”是 Inspector；独立图片 Viewer 未实现。
- 素材右键动作由 Command 承接，可在后续增加“用于创作”命令组；本轮不显示任何未实现菜单。

## 未来创作资产层设计

创作层采用“引用 + 按需解析”，不复制素材记录或源文件：

1. 来源身份由 `AssetOrigin` 表达。
2. 素材库内容由 `AssetLibraryStableReference` 定位。
3. 外部、客户和 AI 来源由不透明 external reference 定位，不把路径或 URL 当作可信业务身份。
4. `ICreativeAssetReferenceResolver` 在使用时返回 `Resolved`、`LibraryOffline`、`AssetMissing`、`HashMismatch`、`ExternalUnavailable` 或 `Unsupported`。
5. 只有解析并校验成功后才把临时可读路径交给统一 Thumbnail Provider；路径不写回创作引用成为事实源。

## AssetOrigin

`AssetOrigin` 是稳定业务字段，不是 Tag，也不等同于导入方式、文件位置或在线状态：

| 值 | 语义 |
|---|---|
| `SelfCreated` | 我的摄影：客片、个人创作、测试拍摄。 |
| `ExternalReference` | 外部灵感：网页图片、社交平台、电影截图、摄影参考。 |
| `ClientReference` | 客户资料：客户参考、品牌规范、产品参考。 |
| `GeneratedReference` | AI 生成：概念图、测试图、方案预览。 |

`CreativeAssetReference` 同时保存 SourceType 与 AssetOrigin，并校验外部图片、客户上传和 AI 来源的合法组合。素材库中的文件也可以保留其真实 Origin，因此 Origin 与 Reference / Managed Copy 无关。

本轮只建立领域契约，不大规模修改素材库数据库。后续迁移应增加结构化列及索引，并给历史数据提供显式默认值或可审阅的回填流程，禁止自动创建同名 Tag 代替。

## InspirationReference

`InspirationReference` 保存：

- `StableReference`（完整包含 LibraryId、AssetId、ContentHash）
- `ThumbnailReference`
- `CreatedAtUtc`
- 可选 `CollectionId`

它不保存文件路径，也不复制图片；来源身份由 `ThumbnailReference.Source.Origin` 指向的创作资产引用统一表达，不在托盘项中制造重复事实源。`InspirationCollection` 保存命名集合及按 StableReference 去重的 InspirationReference。可用性不作为静态事实固化在集合中，而是在展示时解析：

- 库不可访问：`LibraryOffline`
- 素材记录或源文件消失：`AssetMissing`
- 同一 LibraryId / AssetId 的内容 hash 改变：`HashMismatch`
- 校验通过：`Resolved`

现有 `InspirationTrayService` 已有稳定引用、顺序、批量增删、去重与四态解析基础；下一阶段需把 Origin、Thumbnail Reference、CreatedAt 和 CollectionId 的正式持久化与集合仓储补齐。

## MoodboardReference

`MoodboardItem` 是视觉画布元素，不是普通文件夹。契约支持：

- 素材库引用：`AssetReference`
- 外部图片、客户资料、AI 参考或灵感来源：`ExternalReference`
- 文字：`TextNote`
- 布局：`Position`、`Scale`、`Rotation`、`GroupId`、`IsLocked`、`Order`
- 图片说明：`Note`

图片元素使用 `CreativeAssetReference + AssetThumbnailReference`，不持有 `AssetItem`。本轮没有画布、拖拽、编辑器、分组 UI、仓储或导航入口。

## ProjectPlanningReference

`ProjectPlanningReference` 保存独立 `ReferenceId`、`ProjectId`、来源引用、缩略图引用、用途、备注和创建时间。它可以引用素材库图片、灵感图片、客户资料和 AI 参考，但 Project 不拥有 Asset，策划模块也不复制图片。

项目与素材库的关系使用独立 `ProjectAssetReferenceLink(ProjectId, BookingId?, StableReference, Role, AddedAtUtc, Source)` 表达多对多连接；不向 `AssetItem` 直接增加单值 ProjectId / BookingId。

## Calendar Integration Requirements

目标链路：`工作日历 → Booking → Project → Asset Collection → Asset Library`；反向链路：`Asset → Inspector → 拍摄日期 → 查看日历`。

- 当前 Booking 有稳定 BookingId，并已提供可选 ProjectId；这些是上游连接点。
- 素材端必须通过独立关系仓储保存 ProjectId、可选 BookingId 与 StableReference，允许一项素材属于多个项目或拍摄任务。
- 拍摄日期优先来自 Booking；Asset 的 CaptureTime 只是媒体元数据，不能自动等同于预约日期。
- 反向跳转应先解析关系，再定位 Booking 和日历日期；无关系时只显示不可用状态，不按名称或文件夹猜测。
- 不同项目域的 ID 需要显式映射与来源版本，不能把名称相同视为同一项目。
- 本轮不新增数据库表或迁移；关系仓储、回填和导航命令留到独立阶段。

## 未实现功能列表

- 灵感托盘 UI、灵感集 UI 与正式 InspirationCollection 仓储
- Moodboard 页面、画布、编辑器和持久化
- Planning Center 页面、项目策划模块和持久化
- 创作中心一级导航
- 日历素材页面与双向跳转命令
- `ICreativeAssetReferenceResolver` 的实际库/外部来源解析器
- AssetOrigin 数据库迁移与历史数据回填
- Thumbnail Provider 的 RAW/PSD/视频代理、磁盘缓存及重建任务
- 独立图片 Viewer
- Web Clipper、AI 搜索、MCP、DeliveryBatch、精修流程及在线选片大改

下一阶段 `CP-P01` 应从解析器、来源字段持久化策略和统一缩略图视觉状态开始，再实现灵感托盘与灵感集；不应顺带进入 Moodboard 或 Planning Center UI。
