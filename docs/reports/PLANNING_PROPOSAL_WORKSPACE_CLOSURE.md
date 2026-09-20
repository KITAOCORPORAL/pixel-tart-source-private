# 策划中心摄影提案工作区验收

日期：2026-09-20。依据：本轮策划中心文档，以及用户明确取代参考截图的完整文字视觉规范。
本轮不进入下一阶段，不重写素材库、联机拍摄、自由画布，不增加 AI、CMS、Office 编辑器。

## 实现与边界

| 项目 | 实现 / 验证 |
| --- | --- |
| 项目列表 | 280 DIP 文档列表、搜索项目/人物/日期/地点、独立策划状态筛选、新建/关联已有项目 |
| 文字 | 默认阅读，按需编辑；940 DIP 正文；标题、副标题、段落、引用、列表、粗体、分隔线；封面参考、视觉方向摘要 |
| 参考图 | 本地、当前素材库、灵感板、自由画布来源；原比例；单击选择/双击大图；分类、封面、关联镜头、移除引用 |
| 情绪板 | 项目视觉摘要、分组、排序；复用自由画布，不实现第二套无限画布 |
| 镜头清单 | 独立内容模块；按需详情抽屉；复用 ProjectShot / ProjectShotReference 与原状态枚举 |
| 灯光图 / 服化道 | 图片分类、服装/妆发/道具分组；可关联当前镜头，重复操作可关联多个镜头 |
| 文件 | 原位置关联，显式打开文档/图片或定位其他文件；移除关系不删除文件 |
| 预览 | 完整正文、情绪板、镜头、灯光、服化道；隐藏策划列表/编辑工具/内容导航，Esc 返回；保留应用全局导航 |
| 导出 | 本地 A4 PDF，图片等比、标题层级、自动分页、页码；144 DPI 图像页，文字不可选择/搜索，不是矢量 PDF |
| 档期 | 使用现有 Booking.ProjectId 和 Planning.BookingId；保留原档期内容，日历与策划互相跳转 |
| 自动保存 | 450ms debounce，按项目/镜头身份快照；切换策划、离开页面、预览、导出、关闭、Ctrl+S flush；正文/镜头恢复草稿 |

数据采用原有 version-1 Planning JSON 的可选 Document 扩展。不建第二套数据库；旧 Summary、Shot、VisualLinks、CapturedAssets、Palette、Tone、ReferenceLook 原身份保留。
保存时仅合并正文相关字段，避免覆盖联机执行写入。单个存储原子提交；跨 Booking/Planning 两个存储不属于同一个数据库事务，不能声称跨存储断电原子性。
草稿在 debounce 后、正式提交前写入；突然崩溃可能丢失最近约 450ms 尚未进入保存的输入。未承诺每个按键同步落盘。
离线图片使用持久派生预览缓存，原文件恢复后刷新；跨素材库原图解析要求该素材库可用，缓存不被当作原图。
演示数据仅在显式隔离验收开关下生成，正常启动不生成项目。

## 本轮发现并修复

1. 验收与正常应用竞争单实例：显式验收按隔离目录区分实例，不关闭用户应用。
2. 关闭时同步保存完成导致重入 Close：延迟到下一次 Dispatcher 调度关闭。
3. 修改镜头后立即改状态/复制/归档/排序可能覆盖编辑：操作前 flush，待保存快照按镜头 ID 合并。
4. 项目正文已提交、镜头提交失败：草稿携带镜头快照，全部完成才清理，可重放恢复。
5. 参考分类每次重复编码：依据源文件时间复用预览缓存。
6. 来源预览错误使用缩略图：可用时解析原位置；离线明确显示缓存提示。
7. “用于参考仿色”只跳页：现在基于选中原图建立既有 ReferenceLook 并选中，未改仿色编辑器。
8. 隐藏页面仍保留旧参考选择：切换内容/项目清理选择，避免 Delete 移除非当前对象。
9. 快捷键焦点在全局侧栏：主窗口转交策划快捷键，避免误触原项目保存。
10. 旧 1×1 黑色验收占位图影响视觉检查：仅在隔离夹具中替换成可见合成图，不修改用户图片。

## 测试证据

证据目录：`artifacts/planning-proposal/`。

- Release solution build：0 warning / 0 error。
- Core：1,376 passed / 0 failed。
- WPF 全量回归：1,258 passed / 0 failed / 1 skipped。跳过项为既有大批量 10,128 素材夹具测试，不计为通过。
- 最终快捷键/离页保存微调后的定向门禁：PlanningDocumentPersistenceTests 3 passed；PlanningWorkspaceLayoutTests + RealAppStartupIntegrationTests 7 passed，0 failed。全量 WPF 结果先于这些微调，最终结果不混淆为全量重跑。
- 真实 App 集成：执行生产 App.OnStartup、真实 MainWindow、真实服务组合；不是替代窗口。
- 覆盖：档期→策划、指定镜头深链、策划→联机→策划、项目历史、新建/搜索、缺失/损坏数据安全回退、演示数据不覆盖修改。
- 新增覆盖：七模块切换/截图、940 DIP 阅读布局、280 DIP 列表/预览隐藏、正文立即切换落盘、镜头编辑后立即状态变更、恢复正文和镜头草稿、附件重开、原图离线缓存、移除引用源文件仍存在、参考仿色来源、PDF 实际导出。
- Core 存储测试验证旧 version-1 兼容、执行/关联数据不被正文覆盖、草稿恢复与清理。
- TRX：`tests/core.trx`、`tests/wpf.trx`、`tests/proposal-integration.trx`；构建日志：`build.log`、`wpf-build.log`。
- 最终门禁 TRX：`tests/planning-persistence-final.trx`、`tests/planning-gate-final.trx`。

## 截图与 PDF 自查

截图位于 `artifacts/planning-proposal/screenshots/`：

- `01_文字.png`、`02_参考图.png`、`03_情绪板.png`、`04_镜头清单.png`
- `05_灯光图.png`、`06_服化道.png`、`07_文件.png`、`08_预览模式.png`
- 原文档英文名称同步保留：`01_planning_overview.png` 至 `10_planning_1080p.png`。
- `策划案-验收.pdf`：Poppler 渲染全部页后检查标题、图片比例、页码和分页；`pdf-contact.png` 为全页检查索引。

这些是实际 1920×1080 MainWindow 的 WPF RenderTargetBitmap 渲染图，使用显式隔离的合成验收数据，不是设计稿、真实摄影作品或物理显示器截屏。
检查结果：正文不横跨屏幕；无永久 Inspector；列表为文档行而非大卡片；导航轻量；顶部仅关联档期/预览/导出/更多；图片无强制裁切；无新增 DataGrid/默认 TabControl。
当前主视觉按数量等列排列，不强制“1大+2小”模板。图片区域适应数量，保留原构图。

## 尚未替代的人工验收

本报告不宣称真实摄影师首次使用、多台物理机器、125%/150%/200% DPI、相机硬件或安装/卸载本轮新版已通过。
素材库/灵感板/画布复用现有来源接口和稳定引用，来源入口代码与既有回归通过；没有把合成夹具冒充任意用户素材库的全流程鼠标验收。
本轮不重新标记早期 RC12 报告为发布通过，不制作正式 RC；之前安装包不能代表本轮策划中心代码。

## 文件清单

- XAML：PlanningCenterView.xaml。
- App C#：App.xaml.cs、MainWindow.xaml.cs、MainViewModel.cs、PlanningCenterViewModel.cs / Overview.cs / Document.cs / References.cs、PlanningCenterView.xaml.cs、PlanningProposalPdf.cs。
- Core C#：PlanningCenter.cs、PlanningDocument.cs。
- Tests：PlanningDocumentPersistenceTests.cs、PlanningProposalAcceptance.cs、RealAppStartupIntegrationTests.cs、StageVPlanningWpfTests.cs、StageV2StartupCompatibilityTests.cs。
- 文档：本报告与 docs/design/PLANNING_PROPOSAL_WORKSPACE.md。
- PNG/PDF/TRX：上述 artifacts 目录。

提交与推送结果在最终交付中列出；不包含工作区既有 RC12 报告改动和旧发布目录。
