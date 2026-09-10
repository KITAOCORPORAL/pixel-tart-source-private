# PixelTart 素材库交互基线

## 文档地位

这是素材库后续实现和验收的交互契约。录屏 V07（本次 11-32）与 V06（11-11-30）提供场景证据，但不等同当前代码已通过运行验证。功能背景与素材映射见 [01](01_Eagle录屏索引.md)、[02](02_Eagle交互行为规范.md)、[03](03_Eagle功能清单.md)、[04](04_Eagle_to_PixelTart映射.md)；缺口与优先级见 [05](05_PixelTart素材库缺口.md)。摄影字段只沿用 04 的提案，不在此新增 UI。

## 三轴状态模型

- `Reference` / `Managed` 是**存储模式**：Reference 只保存源引用和元数据，Managed 另有库内托管副本。
- `Active` / `Archived` / `Trashed` 是**生命周期**：归档隐藏但可恢复；Trash 默认可恢复；永久删除仅限未来回收站明确确认动作；本轮不开放。
- `Available` / `Missing` / `LibraryOffline` 是**可用性**：文件缺失或当前库离线不改变存储模式和生命周期。

任何卡片、检查器、任务和查询不得把上述状态渲染成四选一。Reference 删除不得删除源文件；Shift+Delete 不能默认为硬删除。原位置不存在时，恢复按“恢复为缺失并允许重新定位/选择新位置”的规则处理，禁止覆盖未知文件。

## 查询、输入和结果发布

搜索框、筛选更多和 Smart Folder 均生成 `AssetLibraryQuery` 或版本化 P3 `AssetQueryDocument`；不要拼接字符串冒充组合。现有 P3 composer 已处理防抖、建议、历史、规范化和 IME 组合态：[`AssetLibraryViewModel.P3QueryComposer.cs`](../../../src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.P3QueryComposer.cs#L188)。IME composition 期间不执行提交、评分、归档、删除或全局快捷键；完成 composition 后再发布一次查询。每次刷新取消旧 generation，旧结果不得覆盖新结果。

V07 00:25/00:28 的名称/文件夹名勾选、02:13 的宽高范围、02:35 的秒时长、02:40 的 KB 大小、02:48 的注释有/无/关键字应进入同一条件模型；当前代码没有时长字段，不能把录屏控件展示写成已支持。V07 02:19 只证明语义搜索输入框出现，当前不启用无 provider 的语义搜索。

## 选择、检查器和批量范围

右键卡片选择遵循 [`AssetLibraryContextSelectionPolicy`](../../../src/PixelTart.Modules.AssetLibrary/AssetLibraryContextSelectionPolicy.cs#L10)：右击已选项保留完整集合，右击未选项收敛为单项。检查器有三态：零选显示查询摘要；单选显示路径、尺寸、时间、评分、标签等安全元数据；多选显示共同值、混合值和批量入口。改变查询范围后沿现有实现清除不再属于新查询的隐选；同一查询跨页选中必须重新查询并验证当前 query 成员，不能以旧缓存证明仍在范围内（[`ReconcilePersistedSelectionAsync`](../../../src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.cs#L1553)）。批量命令显示“已选择 N 项”，并在提交前给出新增、已存在、冲突和部分失败预览；不能把当前视口、已加载页和当前查询混用。

## 四布局、密度和锚点

Grid、Masonry、Justified、List 的真实布局在 [`AssetLayoutEngine`](../../../src/PixelTart.Modules.AssetLibrary/AssetLayoutEngine.cs#L23) 与 [`VirtualizingAssetPanel`](../../../src/PixelTart.Modules.AssetLibrary/VirtualizingAssetPanel.cs#L28)。布局切换已有首个可见 AssetId 锚点、选中项回退和按布局持久化（[`AssetLibraryPage.cs`](../../../src/PixelTart.Modules.AssetLibrary/AssetLibraryPage.cs#L454)、[`AssetLibraryViewModel.P2Browser.cs`](../../../src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.P2Browser.cs#L598)）。密度拖动必须保留选择；锚点不在新结果时回退同排序方向的最近邻；改变排序字段会废弃旧游标。V07 04:22–04:43 的 5/2/1 列和选中保持是场景证据，不能宣称精确像素锚点已经验收。

## 标签、文件夹与菜单

标签浮层按 TagId 提供左键包含、右键排除和 Esc 分层关闭；标签管理支持字母分区、A→Z/Z→A、数量升降、分组依据、组内联重命名、近期/推荐/组分区。录屏中的这些项有展示证据，尚需运行确认命令提交和取消。文件夹 F 快捷键必须真正聚焦分类器：方向键移动，Space 多选，Enter 确认，Esc 取消；当前 [`FocusFolderClassifier`](../../../src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.cs#L1737) 只是状态文字。

菜单/子菜单必须在触边时向左展开或在长列表内滚动；键盘 Esc 先关闭最上层浮层，再决定是否清空输入。焦点位于 TextBox、ComboBox 或 IME 组合态时，F/T/数字评分/Shift+D 等命令不得抢按键。关闭浮层后焦点回到发起控件，避免下一次输入落入隐藏菜单。

## 生命周期、Viewer 与外部安全打开

“归档”与“移出当前文件夹”不是删除；归档卡片应有对称“恢复”命令（VM 已有 `RestoreContextCommand`，但当前右键菜单未绑定）。Trash 设计必须保留原集合、标签、删除时间、操作 ID、原路径/托管路径和恢复状态；批量反馈同时报告成功、失败和未处理数量，并纳入持久 undo。V07 05:13–05:25 证明删除 Toast、回收站和永久删除确认场景；未证明恢复，因此不能提前声称恢复 UI 完成。历史隔离 API 的 TXT 恢复记录只作为背景。

检查器信息、独立 Viewer、默认应用、资源管理器定位、复制路径是不同命令。图片优先 Viewer 支持缩放/平移、原比例、上一项/下一项、返回选择；PSD/PSB、SVG、GIF、视频、PDF、字体按能力表显示“可索引/缩略图/查看/分析”状态。RAW 优先已有代理或内嵌预览，不承诺完整 RAW 解码；不支持时说明原因并提供受控外部打开。外部应用启动只能使用已验证路径和显式用户命令，不拼接任意参数。用户主动点击普通“默认应用打开”无需每次额外确认；批量打开、可执行内容等存在相应风险时才提供匹配的范围说明或确认。

## 删除、恢复与批量提交的具体契约

| 发起场景 | 默认命令 | 状态变化 | 文件动作 |
|---|---|---|---|
| 当前文件夹 | 移出当前文件夹 | 只删成员关系 | Reference/Managed 原文件均不动；Shift+Delete 如启用，仅采用此作用域语义 |
| 全部素材或明确“丢到回收站” | 移入回收站 | Active/Archived → Trashed，记录原生命周期 | Reference 不动原文件；Managed 文件处理交由有 journal 的库内文件事务，不能先删文件再写记录 |
| 已归档集合 | 恢复归档 | Archived → Active | 保留文件、标签、集合关系 |
| 回收站 | 恢复素材 | Trashed → 原生命周期 | 校验原路径与库内关系；冲突按以下规则 |
| 回收站未来清理 | 永久删除/清空 | 明确不可恢复 | 显示实际数量/空间/失败与引用影响，单独确认；本轮不实现、不默认映射快捷键 |

恢复分支应可直接测试：①原物理文件存在且身份符合，恢复记录并复用文件；②Reference 源不在，先恢复为 Missing，不创建同名空文件、不静默改指向；③Managed 原路径空闲且库内可写，以可恢复文件事务恢复，失败保留 Trash；④同名位置被不同文件占用，不覆盖，提供保留两份或更换目标的明确选择；⑤原逻辑文件夹仍在且可用则恢复成员关系，已删除则素材恢复到未归类并报告，已归档则保留关系但按可见规则隐藏；⑥原标签已删除不得凭同名重新绑定其他 TagId。每项只在对应文件步骤和库状态一致后报告成功。

批量提交使用操作时冻结的 `(LibraryId, AssetId[])`，先展示 N 项影响。纯数据库元数据操作沿现有单事务与预览指纹契约，失败不部分提交；跨文件操作按项记 journal，允许部分成功但必须报告“成功 S / 失败 F / 未处理 U”，满足 N=S+F+U。失败项仍留在原状态，用户可重试失败项；不得因刷新失败再次执行已经提交的操作。Undo 逆转已成功项，不能恢复永久删除，也不能以整批“成功”掩盖部分失败。
## 异步任务与切库

导入、缩略图生成、视觉分析和重联都应进入统一任务中心，报告阶段、目标 LibraryId、总量、成功、失败、取消和部分完成。当前视觉批量分析已调用 [`TaskOperationBridge.RunAsync`](../../../src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.cs#L1368)；普通导入仍需补齐任务契约。V06 00:33 显示后台任务可允许“强制切换资源库”，这不能推出 Pixel Tart 必须提供危险强切。Pixel Tart 只允许等待、取消后切换、经过验证的安全后台完成，或在无法保证安全时明确禁止。旧库任务历史应保留。

库切换先取得写租约并初始化新页，再替换旧页（[`AssetLibraryWorkspaceHost.cs`](../../../src/PixelTart.Modules.AssetLibrary/AssetLibraryWorkspaceHost.cs#L228)）。关闭先取消并 drain 活跃操作，再释放 repository。异步回调、任务、undo token 和选择恢复都必须绑定 LibraryId，旧库结果不得发布到新库。图片/当前选择优先只影响视觉分析排队顺序，不能跳过失败或破坏导入事务。

## 验收场景

1. **搜索与 IME**：名称/文件夹范围、宽高、时长、大小、注释条件组合；中文 composition 不触发快捷键，完成后只发布一次。
2. **标签与菜单**：包含/排除、A↔Z/数量排序、分组和重命名；长浮层滚动，触边子菜单向左，Esc 分层关闭并恢复焦点。
3. **多选与布局**：三态检查器；右击已选保持集合；分页/筛选后只保留当前 query 成员；四布局与密度变化保持选择并验证锚点回退。
4. **归档/Trash**：归档和恢复对称；Reference 不改源文件；Trash 批量数量、部分失败、undo、重启一致；永久删除只在回收站二次确认。
5. **Viewer**：JPG 缩放返回；RAW 代理/不可用原因；特殊格式能力状态；外部打开与定位均走显式命令。
6. **任务/切库**：导入或分析运行时显示 LibraryId；取消/强制切库/关闭后无跨库写；旧库任务历史不消失。

本文是实施和验收基线，当前没有声称已执行运行时测试。
