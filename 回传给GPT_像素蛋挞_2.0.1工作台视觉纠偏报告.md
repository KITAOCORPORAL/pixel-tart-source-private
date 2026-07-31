# 像素蛋挞 2.0.1 工作台视觉纠偏回传报告

## 交付结论

- 版本：2.0.1。
- 本次已重建工作台首页和全局壳层，没有在 2.0.0 旧首页上继续堆叠卡片。
- 默认主题改为深色，完成左侧导航、中央工作区、右侧任务中心三栏布局。
- 归片、编号解析、索引、匹配、复制、报告、项目历史、授权 Provider=None 等既有业务逻辑未被重写。
- Release 全量自动化测试 350/350 通过，0 失败、0 跳过。
- win-x64 self-contained 发布、正式安装包、隔离安装启动和卸载复验均已完成。

## 1. 2.0.0 未达到参考图效果的原因

2.0.0 仍沿用了传统表单型 WPF 页面思路：顶部和左侧都承载了重复品牌信息，首页以单列卡片和大面积空白为主，导航、工具入口、任务状态和项目卡没有形成统一的摄影工作台层级。浅色背景、较大的控件留白、分散的状态块也削弱了参考图中紧凑、专业、暗房式工具平台的视觉感受。

## 2. 删除或替换的旧工作台结构

- 移除首页重复的软件品牌大标题和说明性头图区。
- 移除以“版本介绍、会员说明、营销提示”为视觉中心的旧首页结构。
- 移除散落在页面中的旧工具卡和大面积无业务内容的空白区。
- 移除工作台内重复的顶部状态条，任务信息集中到右侧任务中心。
- 用完整的新 `WorkbenchShell` 替换原 2.0.0 首页布局；业务页面、命令和导航目标继续复用。

## 3. 新三栏布局尺寸

- 左侧导航：展开 172 DIP，收起 54 DIP。
- 中央工作区：自适应宽度，最小 760 DIP。
- 右侧任务中心：固定 320 DIP。
- 默认窗口：1600 × 920 DIP。
- 中央顶部快捷区高度约 106 DIP；项目概览与处理任务区高度约 230 DIP。
- 窗口小于 1350 DIP 时，右侧任务中心折叠为抽屉入口，中央工作区优先保留。

## 4. 左侧导航重构

- 导航改为“主要 / 工具 / 系统”三组，减少层级和重复入口。
- 使用项目内矢量 `PathGeometry` 图标，不依赖字体 Glyph，避免安装版乱码。
- 当前页使用低饱和深灰底和暖琥珀色左侧指示条。
- 工具箱、使用教程、问题反馈、版本状态与升级入口集中在侧栏底部。
- 收起后只保留稳定图标、Tooltip 和无障碍名称，主内容会真实扩展。

## 5. 顶部快捷工具

- 第一入口为宽幅“开始本地分片”主卡，采用深青绿渐变，承担首页唯一主视觉。
- 其后依次为“归片工作区、批量压缩、批量水印、工具箱”四个紧凑入口。
- 快捷卡使用统一图标尺寸、间距、焦点态和键盘可访问逻辑，不使用大面积促销色。

## 6. 工具箱 Popup

- 点击顶部“工具箱”打开右对齐 Popup，不跳转离开工作台。
- Popup 为 2 列 × 4 行，共 8 项：本地分片、批量压缩、批量水印、删废片、FTP 工具、照片整理、批量重命名、批量转档。
- Popup 使用深灰面板、统一线性图标、明确悬停/焦点边界；按 Esc 或失焦关闭。

## 7. 项目概览

- 项目概览整合为一个面板，显示待处理、待匹配、已匹配、已导出、本地项目、需要确认六项统计。
- “更多”入口保持次级层级，不与主任务竞争视觉焦点。
- 统计值继续绑定现有项目、匹配和输出数据，没有制造新的业务口径。

## 8. 处理任务区

- 与项目概览并排展示，承载扫描、复制、压缩和转档任务摘要。
- 无任务时使用克制的线性时钟空状态；有任务时继续绑定现有任务摘要。
- 任务区和右侧任务中心语义区分：中央用于当前处理概览，右侧用于队列、确认和历史。

## 9. 最近项目区

- 提供“最近项目、本地分片、归片项目、已完成”四个标签。
- 使用横向项目卡展示封面、项目类型、名称、状态、文件数、更新时间和继续处理入口。
- 空项目和已完成为空时均提供正式空状态，不显示调试占位文案。
- UI 验收数据使用独立的 `KitaoPhotoSelector.UiReview` 数据目录，不读取或修改用户项目。

## 10. 右侧任务中心

- 固定宽度 320 DIP，包含处理中、待确认、当前任务、冲突/未找到确认统计、任务历史和清理动作。
- 大屏保持常驻；紧凑宽度下折叠为任务中心抽屉入口。
- 清理已完成任务在无任务时保持禁用，避免误导。

## 11. 深色主题配色

- 主窗口/中央背景：`#0D0E10`。
- 侧栏背景：`#151618`。
- 主卡片：`#17181B`。
- 次级卡片：`#1E2024`。
- 悬停面：`#2A2D32`。
- 边框：`#292C31`。
- 主文字：`#F4F4F5`；次级文字：`#A9ABB1`；弱文字：`#70737B`。
- 品牌强调色：`#E1A73A`；工具辅助色：`#39BFA7`。
- 深色模式同步请求 Windows 原生标题栏使用深色标题、边框与文字；浅色主题仍可在设置或菜单中切换。

## 12. Logo 调整

- 保留“像素 + 蛋挞 + 照片”识别逻辑，不进行与产品无关的品牌重做。
- 小尺寸图标改为更少细节、更高对比的暖色蛋挞/照片符号，16–48 像素下不再糊成色块。
- 重新生成 ICO 多尺寸帧、PNG 和工作台项目封面资源。

## 13. 响应式布局

- 1600 × 920：完整三栏、展开侧栏和 320 DIP 任务中心。
- 1280 宽度：右侧任务中心折叠，中央区域获得更多空间，提供任务中心抽屉入口。
- 侧栏可独立收起到 54 DIP；图标、Tooltip、键盘导航继续可用。
- 布局使用 WPF DIP、布局取整和设备像素对齐，适配 100%、125%、150% DPI。

## 14. UI 截图路径

截图目录：`D:\AI AGENT\RAWSelectionAssistant\artifacts\ui-review\2.0.1`

1. `01-dark-workbench.png`：深色工作台，1600 × 920 DIP。
2. `02-toolbox-open.png`：工具箱 Popup 展开。
3. `03-recent-projects-data.png`：存在最近项目数据。
4. `04-completed-empty-state.png`：已完成项目空状态。
5. `05-light-workbench.png`：浅色工作台。
6. `06-compact-1280.png`：1280 紧凑布局。
7. `07-sidebar-collapsed.png`：侧栏收起状态。

当前 Windows 桌面捕获接口返回 `0x80070005`，并且窗口捕获不支持边框扩展。为避免伪造截图，使用仅在 `UiReviewBuild` 中编译的 WPF 视觉树渲染器，从实际启动的同一 MainWindow、真实绑定数据和真实 Popup 生成 PNG；未使用效果图、网页重绘或后期拼接。正式 Release 不包含该验收控制器。截图内容不含原生标题栏，窗口尺寸和 DPI 仍由实际 WPF 窗口状态驱动。

## 15. 自动化测试结果

- 命令：`dotnet test RAWSelectionAssistant.sln -c Release --no-restore`。
- 结果：350 通过，0 失败，0 跳过。
- 2.0.1 新增/更新测试覆盖：默认深色主题、三栏结构、快捷入口、工具箱 8 项、项目概览、处理任务、最近项目、右侧任务中心、侧栏宽度、响应式断点、版本号、安装包命名和品牌资源。
- Debug Build：0 警告、0 错误。
- Release Build：0 警告、0 错误。

## 16. 原有业务测试结果

- 编号解析、文件索引、JPG/RAW 联合匹配、冲突处理、复制、报告、项目历史、设置兼容、授权门控、新手教程、安全删除和单实例测试继续通过。
- 授权 Provider 状态未改，仍按现有 `Provider=None` 配置运行。
- win-x64 self-contained 发布成功，主程序 `FileVersion=2.0.1.0`、`ProductVersion=2.0.1`。

## 17. 安装包路径与安装复验

- 正式安装包：`D:\AI AGENT\RAWSelectionAssistant\artifacts\installer\像素蛋挞_Setup_2.0.1_x64.exe`。
- 文件大小：48,740,169 字节。
- SHA-256：`DDCABC1883766C8533792F1C2997B3A331270FC5A4D5BEA283AEEFB74C70E21F`。
- 隔离测试安装：安装退出码 0；安装后版本 2.0.1；主窗口标题“像素蛋挞”；WinExe 无控制台；卸载退出码 0；测试安装目录已清理。

## 18. 修改文件清单

- 版本/发布：`Directory.Build.props`、`README.md`、`installer/RAWSelectionAssistant.iss`、`src/RAWSelectionAssistant/RAWSelectionAssistant.csproj`、`src/RAWSelectionAssistant/app.manifest`。
- 主界面：`src/RAWSelectionAssistant/MainWindow.xaml`、`src/RAWSelectionAssistant/MainWindow.xaml.cs`、`src/RAWSelectionAssistant/ViewModels/MainViewModel.cs`。
- 外观服务：`src/RAWSelectionAssistant/Services/AppearanceService.cs`、`src/RAWSelectionAssistant/Services/NativeWindowTheme.cs`。
- 设计系统：`Theme.Dark.xaml`、`Theme.Light.xaml`、`Theme.HighContrast.xaml`、`Controls.Navigation.xaml`、`Controls.Cards.xaml`、`Controls.AppMenu.xaml`。
- 品牌资源：`Assets/AppIcon.Small.svg`、`Assets/AppIcon.ico`、`Assets/AppIcon.png`、`Assets/WorkbenchProjectCover.png`。
- 核心配置：`Branding`、`AppearanceSettings`、`AppDataPaths` 相关文件。
- 测试：新增 `WorkbenchVisualCorrection201Tests.cs`，同步更新 2.0.0/1.4.x 旧视觉断言和版本断言。
- 验收工具：`tools/generate_brand_assets.ps1`、`tools/prepare_ui_review.ps1`、`tools/capture_window.ps1`。

## 19. 已知问题

1. 正式安装包尚未配置商业代码签名，Windows SmartScreen 可能显示“未知发布者”；不影响安装、启动和功能。
2. 当前测试桌面的系统截图权限受限，因此 UI 验收 PNG 不含 Windows 原生标题栏；正文区域、菜单、Popup、数据绑定、主题和响应式布局均为实际 WPF 渲染结果。
3. 紧凑宽度下右侧任务中心按设计折叠为抽屉入口，不与完整三栏状态同时展示。
4. 截图中的 `Wedding Selection` 仅为隔离验收项目，不会写入正式用户项目历史。
