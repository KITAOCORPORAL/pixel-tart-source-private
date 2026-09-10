# Asset Library Foundation Review

日期：2026-09-10
范围：Eagle 基础能力闭环复核与创作资产层接口准备；未进入灵感托盘、情绪板、策划中心或日历素材页面的新 UI 开发。

## Git

- Branch：`feature/asset-library-eagle-shell-context-p36`
- Review baseline HEAD：`73530b9cbe2510e5e1266af8115a4e50eb4bc000`
- Origin HEAD：`source-private/main`
- Tracking：复核开始时本地分支与 `source-private/feature/asset-library-eagle-shell-context-p36` 一致。
- Fetch：2026-09-10 已成功执行 `git fetch --all --prune`。
- 最近一次已推送实现报告：`73530b9 docs(asset-library): finalize p36 report metadata`，其历史包含 P3.6 报告、验收和回滚说明。
- 复核开始时存在一组未提交续作。与本报告直接相关的 Viewer 命名、菜单收口、创作引用契约、统一缩略图 Provider 和测试纳入本次交付；其余既有格式化改动不混入本次提交。
- 未跟踪项审计未发现临时数据库、测试图片、缓存、视频或 API Token；仓库中已有的 `artifacts/` 历史证据均为既有受版本控制内容，不是本轮新增污染。

## 当前完成

### Eagle 基础能力闭环

- 导入：现有 Reference/Managed 导入、SHA-256 去重和库隔离实现可复用；素材库 UI 导入默认计算内容 hash，可产生稳定引用所需的内容身份。
- 查询与 Smart Folder：继续复用 `AssetLibraryQuery`、版本化 `AssetQueryDocument`、建议、历史、锁定、组合条件和引用校验，没有建立第二套查询模型。
- 文件夹与标签：继续复用无限层级文件夹、Tag/TagGroup、批量关系变更、合并预览和持久 Undo。
- 多选与 Inspector：继续复用共享选择状态、右击选择策略、零选/单选/多选三态 Inspector 和跨页选择校验。
- Undo：继续复用素材库 durable journal；本轮没有引入绕过 journal 的元数据写入。
- Task Center：视觉分析和既有长任务仍接入统一任务中心；普通素材导入尚未完全接入，保留为明确缺口。
- Library：继续复用 `.ptlibrary`、`LibraryId`、会话切换、写锁、`.ptpack` 和合并映射。

### Viewer 与右键菜单

- 当前没有独立 Viewer。素材卡片没有双击 Viewer 路由；原“打开大图预览”实际只展开 Inspector，现已准确改名为“查看信息”，AutomationId 同步为 `AssetContextShowInfo`。
- 独立大图、缩放/平移、前后项导航、RAW/PSD/视频代理 Viewer 仍是后续功能，不以 Inspector 冒充完成。
- 右键菜单继续保留查看、整理、工作流、导出、复制、更多和危险操作域；未实现的“加入灵感集”“选择灵感集”“加入当前策划”“创建情绪板”等入口本轮不显示。
- 已存在并有真实命令的“加入灵感托盘”继续保留；本轮没有新增托盘 UI 或创作页面。
- 素材库按钮角色审计已同步既有 P3.6 托盘按钮，56 个按钮全部仍使用素材库本地五类显式样式和可访问名称契约。

### 统一 Thumbnail Provider

- 新增 `IAssetThumbnailProvider` / `WpfAssetThumbnailProvider`，把路径规范化、解码宽度、文件指纹、冻结 Bitmap 和 64 MiB 有界 LRU 缓存集中到单一 Provider。
- 现有 `AsyncThumbnail` 已改为调用该 Provider；后续灵感托盘、灵感集和情绪板在稳定引用解析到可读路径后应复用同一 Provider，不再建立第二套 WPF 图片加载与缓存。
- 文件长度或最后写入时间变化会生成新指纹并失效旧缓存；重复请求同一文件版本返回同一冻结缩略图。

## 当前缺口

- Viewer：没有独立 Viewer，也没有双击 Viewer 行为；特殊格式能力矩阵仍需单独阶段实现。
- 导入任务：普通素材导入还未完整接入 Task Center 的阶段、LibraryId、取消、失败/部分完成和切库 drain 契约。
- 缩略图：统一内存 Provider 已建立，但 RAW 内嵌预览、视频首帧、PSD/PSB/SVG/GIF 代理、磁盘缓存和重建入口仍未实现。
- 创作引用解析：已定义 resolver 接口和结果状态，但活动库/离线库、包合并映射与外部来源的实际 resolver 尚未实现。
- InspirationCollection：已定义命名集合与稳定引用契约，尚无集合仓储、排序、共享或 UI。
- Moodboard/Planning：只有无 UI、无数据库写入的合同模型；没有画布、编辑器、策划中心或项目策划模块。
- Trash：当前仍是禁用入口；没有恢复闭环，不开放永久删除。

## 已确认可复用模块

| 能力 | 复用入口 | 边界 |
|---|---|---|
| 素材身份 | `AssetItem`、`AssetLibraryStableReference` | 外部集合只保存 `LibraryId + AssetId + ContentHash`，不持有 `AssetItem` |
| 库 | `AssetLibraryContainerService`、workspace/session、`.ptlibrary`、`.ptpack` | 解析和写入必须绑定目标 `LibraryId` |
| 查询 | `AssetLibraryQuery`、`AssetQueryDocument`、Smart Folder | 创作层不复制搜索/筛选实现 |
| 组织 | Folder、Tag、TagGroup、批量 metadata command | 业务状态不能伪装成自由标签 |
| 选择与检查器 | 共享 selection、`AssetLibraryContextSelectionPolicy`、三态 Inspector | UI 临时选择不等于客户最终选择 |
| Undo | durable v2 journal、素材库命令服务 | 跨文件操作仍需逐项 journal |
| 任务 | `TaskOperationBridge`、Task Center | 普通导入接入仍待完成 |
| 缩略图 | `IAssetThumbnailProvider`、`WpfAssetThumbnailProvider`、`AsyncThumbnail` | 先解析稳定引用，再交给统一 Provider |
| 灵感临时集合 | `IInspirationTrayService` | 只存稳定引用，不复制原图 |

## 后续创作资产层接口

### InspirationCollection

- `InspirationCollection(CollectionId, Name, Assets, CreatedAtUtc, UpdatedAtUtc)`。
- `Assets` 是去重后的 `AssetLibraryStableReference[]`，每项完整保存 `LibraryId`、`AssetId`、`ContentHash`。
- 构造时拒绝空集合 ID、空名称和倒置时间；集合不复制素材记录或源文件。

### MoodboardItem

- `CreativeAssetReference` 支持素材库、外部图片、客户上传、AI 生成参考和灵感引用。
- 素材库来源必须且只能提供 `AssetLibraryStableReference`；外部/客户/AI 来源必须且只能提供不透明外部引用；禁止混合两个身份制造歧义。
- `MoodboardItem` 包含 `Source`、`Thumbnail`、`Position`、`Scale`、`Rotation`、`Note`；不依赖 `AssetItem`，并拒绝非有限坐标、非正缩放及来源不一致的缩略图。

### ProjectPlanningReference

- 以 `CreativeAssetReference` 支持素材库图片、灵感图片、客户资料和 AI 参考。
- 只持有稳定/外部引用与缩略图引用，不绑定 `AssetItem`；本轮不写数据库。

### 解析接口

- `ICreativeAssetReferenceResolver` 是下一阶段统一解析入口。
- `CreativeAssetResolutionState` 明确区分 `Resolved`、`LibraryOffline`、`AssetMissing`、`HashMismatch`、`ExternalUnavailable` 和 `Unsupported`。
- 素材库实现只有在核对 `LibraryId + AssetId + ContentHash` 后才能返回可读路径；解析出的路径再交给统一 Thumbnail Provider。

## 日历素材联动准备情况

- 当前 `ShootBooking` 已有可选 `ProjectId`，预约事实有稳定 `BookingId`；项目、预约、资料、财务、联机拍摄和 RAW→JPG 任务已有各自明确的 Project/Booking 入口。
- `AssetItem` 本身没有 `ProjectId`/`BookingId`，本轮不向素材表增加外键，避免一张素材只能属于一个项目。
- 新增 `ProjectAssetReferenceLink(ProjectId, BookingId?, StableReference, Role, AddedAtUtc, Source)` 作为独立多对多关系层合同；拒绝空项目/预约 GUID，并保留预约可选，以支持先收集参考、后安排拍摄。
- 未新增数据库表或迁移。未来应以独立关系仓储实现 `工作日历 → 拍摄任务 → 项目 → 素材集合 → 素材库`，反向由 stable reference 解析拍摄日期后提供“查看日历”。
- `SelectionProject.ProjectId`、`PhotoProjectRecord.Id` 与摄影业务项目 ID 不是天然同一外键；接入时必须保留来源域与映射版本，禁止按名称静默关联。

## 风险

- 历史素材可能没有完整 SHA-256，不能生成可靠 stable reference；下一阶段需要可取消、绑定 LibraryId 的 hash 回填任务。
- `.ptpack` 合并可能重映射 AssetId；resolver 必须依据合并映射和 hash 校验恢复，hash 不符时返回 `HashMismatch`，不得猜测替换。
- 外部引用是不透明身份，不应直接当本地路径或 URL 执行；resolver 必须实施来源白名单和可用性检查。
- 当前统一缩略图 Provider 是 WPF 栅格解码层；特殊格式需通过同一 Provider 前的代理/解析扩展，不能旁路成第二套缓存。
- 全量解决方案包含绑定旧验收证据和分支名的历史测试。缺少证据文件或处于新分支时会失败，不能把这些环境门禁误报为当前产品回归。
- 工作树在本轮开始前已有与本交付无关的格式化修改；本次提交不覆盖、不丢弃，也不把它们混入功能提交。

## 测试结果

| 验证 | 结果 |
|---|---|
| `Release` solution build | 通过；0 warning，0 error |
| Core tests | 1293/1293 通过 |
| WPF 产品测试（排除旧分支专用 `AssetLibraryP1AutomatedEvidenceContractTests`） | 1103 通过，2 skipped，0 失败 |
| Modular harness tests | 14/14 通过 |
| 创作引用 + 灵感托盘定向测试 | 7/7 通过 |
| Thumbnail + Viewer 命名 + 右键菜单 + 按钮审计定向测试 | 16/16 通过 |
| 历史 P1 evidence fixture | 2 通过，40 失败；统一原因是当前分支不在该旧夹具允许列表，不修改门禁伪造通过 |
| DPI evidence tests | 75 通过，26 失败；统一原因是缺少 `artifacts/automated-dpi-review/2.0.4/` 下五个历史证据 JSON，不是当前素材库断言失败 |

素材库导入、查询、标签、Smart Folder、跨页多选、三态 Inspector、缩略图、durable Undo、Task Center 桥接和切库隔离分别由现有 Core/WPF 全量测试覆盖；本轮不是以截图代替测试。

## 下一阶段建议

进入 `CP-P01 灵感托盘 + 灵感集 + 图片缩略图视觉系统` 时按以下顺序推进：

1. 实现 `ICreativeAssetReferenceResolver` 的活动库/离线库/hash mismatch/包合并映射闭环。
2. 为缺失内容 hash 的历史素材提供绑定 LibraryId、可取消、可恢复的后台回填任务。
3. 在统一 `IAssetThumbnailProvider` 上增加代理来源与磁盘缓存策略，先覆盖 JPG/PNG/常见 RAW 代理，再扩特殊格式。
4. 实现 InspirationCollection 仓储与托盘转集合，不复制素材，不新增情绪板画布或策划中心。
5. 独立排期图片 Viewer；在真正具备缩放、导航与格式能力前继续使用“查看信息”名称。

本阶段到此停止，不开始情绪板、策划中心、项目策划模块或完整日历素材页开发。
