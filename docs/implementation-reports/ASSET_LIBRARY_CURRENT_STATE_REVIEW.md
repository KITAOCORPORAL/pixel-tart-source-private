# Asset Library Current State Review

日期：2026-09-10
范围：依据 `docs/product/eagle-reference/` 的交互基线、Eagle 映射、缺口文档和最新实施报告，对当前素材库做代码与自动测试审计。本轮未开发创作中心 UI、新一级导航或完整日历素材页。

## Git

- Branch：`feature/asset-library-eagle-shell-context-p36`
- Audit baseline HEAD：`85edd19408bcca2f49f65bce9e53bc3eed5e5214`
- Origin：`https://github.com/KITAOCORPORAL/pixel-tart-source-private.git`
- Origin HEAD：`3d6225f40b335ded39e535a5eb01d7c967a180be`（`source-private/main`）
- 审计开始时本地分支与 `source-private/feature/asset-library-eagle-shell-context-p36` 一致；`git fetch --all --prune` 成功。
- 工作树已有 17 个与本轮无关的未提交源码修改。本轮不覆盖、不清理，也不纳入提交。
- 开始审计时有 7 份未跟踪的 `docs/design/` 文档，属于用户的并行设计成果；本轮不修改、不纳入提交。未发现未跟踪的数据库、测试图片、视频、缓存、Token 或临时文件。

## 已完成

| 能力 | 审计结论 | 真实实现依据 |
|---|---|---|
| 导入 | 已实现基础闭环 | Reference / Managed Copy、SHA-256、重复策略和库隔离均由仓储实现；UI 默认请求内容 hash。 |
| 查询 | 已实现 | `AssetLibraryQuery`、版本化 `AssetQueryDocument`、组合条件、搜索建议、历史和 Smart Folder 共用查询链路。 |
| 标签 | 已实现 | Tag / TagGroup、搜索、批量增加删除、移动、合并、归档和用量汇总均有仓储与命令入口。 |
| 多选 | 已实现 | 共享选择状态、右键选择策略、跨页选择校验和批量动作真实连接 ViewModel 命令。 |
| Inspector | 已实现信息检查器 | 支持零选、单选、多选状态和批量元数据入口；它不是图片 Viewer。 |
| 缩略图 | 栅格基础闭环已实现 | `AsyncThumbnail` 统一调用 `IAssetThumbnailProvider`；Provider 负责规范化、尺寸限制、文件指纹、冻结位图和 64 MiB 有界缓存，并区分可用、缺失、离线结果。 |
| 布局 | 已实现 | 网格、瀑布流、详情和列表布局及虚拟化路径存在，并复用相同选择、查询与缩略图能力。 |
| Undo | 已实现 | 素材元数据、文件夹和标签写操作继续通过 durable journal / token 机制撤销、重做。 |
| Task Center | 部分实现 | 视觉分析及既有长任务接入统一任务中心；普通素材导入尚未完整接入任务生命周期。 |
| Library | 已实现基础能力 | `.ptlibrary`、`LibraryId`、会话切换、写锁、离线库、`.ptpack` 和合并映射已有实现。 |

右键菜单的素材动作绑定到 ViewModel Command，并按查看、整理、复制、导出、工作流和危险操作分域。当前真实能力“加入灵感托盘”保留；未实现的灵感集、情绪板和策划入口没有显示。现有“查看信息”只打开 Inspector，没有伪装成 Viewer。

## 未完成

- 独立 Viewer、双击大图预览、缩放/平移、前后项导航及 RAW/PSD/视频代理预览尚未实现。
- 普通素材导入没有完整接入 Task Center 的阶段、取消、失败/部分完成、LibraryId 绑定和切库 drain 契约。
- 缩略图特殊格式代理、磁盘缓存、失效清理与重建入口尚未实现。
- `ICreativeAssetReferenceResolver` 仍是接口；活动库、离线库、缺失素材、hash 变化和包合并映射的实际解析器未实现。
- InspirationCollection 仓储与交互未实现；Moodboard 和 Project Planning 只有数据契约，无 UI、仓储和数据库迁移。
- Trash 仍没有恢复闭环；永久删除不应开放。

## 风险

- 历史素材可能没有完整 SHA-256，无法形成可靠的 `LibraryId + AssetId + ContentHash` 引用，后续需要可取消且绑定 LibraryId 的 hash 回填任务。
- 素材包合并会出现 AssetId 重映射；只按 ID 或路径解析会产生错误引用，必须结合映射与 hash 校验。
- 当前 `InspirationTrayService` 已保存稳定引用和解析状态，但它与新的来源身份、缩略图引用及正式灵感集仓储尚未完成持久化对齐。
- `AssetOrigin` 已成为显式契约，但尚未迁移到素材库数据库；在迁移完成前不得用普通 Tag 代替来源身份。
- 当前 WPF Provider 能统一处理常规栅格图；特殊格式不能通过旁路加载建立第二套缓存。
- 历史 P1 / DPI 证据测试绑定旧分支名和旧产物目录，可能因环境证据缺失失败，需与产品回归分开报告。

## 测试结果

| 验证 | 结果 |
|---|---|
| Release solution build | 通过；0 warning，0 error |
| Core tests | 1293/1293 通过 |
| WPF 产品测试（排除旧分支专用 P1 evidence fixture） | 1103 通过，2 skipped，0 失败 |
| Modular harness tests | 14/14 通过 |
| 创作引用 + 灵感托盘定向测试 | 7/7 通过 |
| Thumbnail + 右键菜单定向测试 | 12/12 通过 |
| 历史 P1 evidence fixture | 2 通过，40 失败；失败原因是当前分支不在旧夹具允许列表 |
| DPI evidence tests | 75 通过，26 失败；失败原因是缺少历史 `artifacts/automated-dpi-review/2.0.4/` 证据文件 |

产品代码回归均通过；上述两组失败是历史证据门禁的分支/产物依赖，不修改门禁伪造通过。
