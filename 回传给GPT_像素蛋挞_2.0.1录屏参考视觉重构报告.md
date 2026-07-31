# 像素蛋挞 2.0.1 录屏参考视觉重构报告

## 1. 完成结论

- 完成状态：已完成。
- 产品版本：2.0.1（文件版本 2.0.1.0）。
- 默认形态：Windows WPF WinExe、win-x64、self-contained、默认深色工作台。
- 授权状态：`Provider=None`，Release 不启用 Mock；侧栏明确显示“免费版 / 授权服务准备中”。
- 核心业务：未重写文件索引、JPG/RAW 匹配、冲突处理、复制、报告、授权门控和项目历史服务。

## 2. 参考素材与观看范围

参考素材已保存至：

- `D:\AI AGENT\RAWSelectionAssistant\reference\xiangsuben\bandicam 2026-07-30 17-51-10-620.mp4`
- `D:\AI AGENT\RAWSelectionAssistant\reference\xiangsuben\xiangsuben_v1.9.18.exe`

完整观看了 00:00–02:18 录屏。因电脑未安装 ffmpeg，未下载或安装额外软件；使用 Windows Media Foundation 每秒提取 139 帧，并生成 12 张联系表用于逐段核对：

- 00:00–00:15：工作台、三栏结构、快捷入口、任务区、设置弹窗。
- 00:15–00:35：项目列表、筛选工具栏、项目卡和窗口填充方式。
- 00:35–00:55：完整工具箱、三列卡片密度与状态标记。
- 00:55–01:20：空状态、上传入口、模式选择和中央区比例。
- 01:20–02:18：编辑页、照片列表、参数弹窗、重命名弹窗和分享弹层层级。

详细拆解：`D:\AI AGENT\RAWSelectionAssistant\docs\reference\像素蛋挞_UI参考拆解_2.0.1.md`。

仅借鉴公开界面的通用结构、比例、密度、层级和交互规律；未提取或复制对方 Logo、图标、图片、商标、专有文案、二进制资源，也未运行绕过授权的操作。

## 3. 2.0.0 未达到预期的原因

2.0.0 的主要问题是骨架而非配色：内容集中在左上角；中央区重量不足；右侧存在大片空白；工具长期铺在侧栏或固定区域；缺少稳定的独立任务中心；深色模式只是换色，没有形成统一的工作台层级。自动化测试证明控件存在，但不能代替布局、密度和视觉层级验收。

## 4. 删除的错误 UI

- 从工作台视觉树移除旧浅色首页、暖黄色主卡、大面积白灰背景和固定右侧工具列表。
- 从侧栏移除批量压缩、批量水印、删废片、FTP、照片整理、批量重命名、批量转档七项长菜单。
- 移除侧栏顶部重复品牌 Logo 与名称。
- 不再使用蓝色免费版大卡、橙色粗描边导航、四个独立浅色统计小卡或小型固定 `MaxWidth` 页面。
- 未加入极速选片、预约、收入、橱窗、客资、团队、AI 挑图、会员广告或云端订单。

## 5. 新全局 Shell

应用保持 Windows 原生标题栏和 36px 传统菜单栏，主体重建为：

- 左侧导航：展开 172px、折叠 54px。
- 中央主工作区：自适应占据主要宽度，不设小型固定最大宽度。
- 右侧任务中心：320px；窗口宽度低于 1350 时折叠为抽屉入口。
- 底部状态栏：34px，独立固定。
- 默认窗口：1600 × 920；最小窗口：1180 × 720。

## 6. 左侧导航

导航按“工作台 / 应用 / 底部固定区”分组：工作台、本地分片、归片工作区、项目历史、授权与版本、设置、帮助；底部保留工具箱、使用教程、问题反馈、版本状态和收起侧栏。导航项高度 38px，矢量线性图标，选中态使用 3px 指示条。折叠后仅显示完整图标和 ToolTip，中央区真实释放宽度。

## 7. 中央工作区与顶部快捷区

顶部高度 106px：

- 主入口约占可用弹性空间，使用 `#004B59 → #004523` 深青绿渐变。
- 标题“开始本地分片”。
- 说明“导入 TXT、客户选图 JPG 或照片编号，匹配本地 JPG、RAW 及相关文件”。
- 四个快捷卡顺序：归片工作区、照片整理、批量压缩、工具箱。

1280 宽度下任务中心折叠，快捷区为任务入口保留独立空间，不发生覆盖或文字重叠。

## 8. 工具箱 Popup

- 由顶部快捷卡和侧栏底部工具箱共用。
- 300px 宽、2 列 × 4 行、8 个工具、深色浮层、8px 圆角、弱边框和轻阴影。
- 包含本地分片、批量压缩、批量水印、删废片、FTP、照片整理、批量重命名、批量转档。
- 点击外部、Escape 或再次点击入口可关闭；底部“查看全部工具”进入完整工具箱页。

## 9. 完整工具箱页面

完整工具箱采用 3 列深色工具卡，共 9 项（额外包含归片工作区）。卡片含统一矢量图标、标题、单行说明和“打开 / 预览”状态。未完成工具明确标记“预览”，不伪装为已可执行。

## 10. 项目概览与处理任务

- 项目概览为一张大卡，2 行 × 3 列统计：待处理、待匹配、已匹配、已导出、本地项目、需要确认。
- 处理任务为独立大卡，空态显示“暂无待处理任务”，说明扫描、复制、压缩和转档任务会显示在此。
- 数字直接排布在大卡中，没有恢复浅色小卡阵列。

## 11. 最近项目

最近项目保留“最近项目 / 本地分片 / 归片项目 / 已完成”四个标签，右侧提供刷新和查看全部。卡片使用项目封面、状态、名称、匹配文件数、更新时间和继续处理入口。验收项目名称为 `[Demo] Wedding Selection`，明确标记演示数据；没有项目时使用完整空状态和创建入口。

## 12. 右侧任务中心

固定 320px，分为处理中/待确认统计、当前任务、等待确认、历史入口。验收中的非空状态明确标记“演示 · 正在扫描”“演示 · 正在复制”和“以上为界面验收演示数据”，没有伪造真实订单。空态显示“暂无待处理任务”。不包含预约、订单、收入或经营数据。

## 13. 设置、反馈与通用弹层

- 设置改为 720 × 620 的居中深色模态窗口，使用半透明遮罩、8px 圆角、轻阴影和自定义深色页签。
- 分类：常规、外观、分片与归片、输出与报告、工具默认值、授权与版本、检查更新。
- Escape、右上关闭图标和“完成”均可关闭。
- 反馈窗口继续使用真实 WPF 对话框，修复深色标题前景，保留复制邮箱和撰写邮件。
- 浏览文件/目录仍使用必要的 Windows 系统选择器。

## 14. Logo 与品牌资源

保留 2.0.1 已生成的像素蛋挞原创图标系列：石墨色外壳、暖金色蛋挞内芯、简化像素和照片暗示；小尺寸不包含复杂文字或多重图案。资源包括：

- `AppIcon.svg`
- `AppIcon.Small.svg`
- `AppIcon.Dark.svg`
- `AppIcon.Light.svg`
- `AppIcon.png`
- `AppIcon.ico`

标题栏保留应用名称和图标；侧栏不再重复品牌。

## 15. 深色主题 Token

- WindowBackground `#0B0C0E`
- ContentBackground `#0E0F11`
- SidebarBackground `#141518`
- ShellTop `#1B1C20`
- CardSurface `#18191C`
- RaisedSurface `#202226`
- ToolTile `#24262A`
- Hover `#2C2F34`
- Border `#2A2D32`
- Divider `#222429`
- TextPrimary `#F4F4F5`
- BrandGold `#E3A93B`
- BrandCream `#F5D27A`
- ToolAccent `#20C985`

浅色主题提供相同的语义资源和完全一致的结构。

## 16. 响应式规则

- 1920 × 1080：中央区扩展，任务中心保持 320px。
- 1600 × 920：172px 侧栏 + 自适应中央区 + 320px 任务中心。
- 1280 × 720：右侧任务中心折叠为独立按钮，中央内容不被挤压。
- 小于 1100 的自动侧栏逻辑仍保留；产品窗口最小宽度为 1180，手动折叠始终可用。
- 最近项目使用横向滚动；工具和菜单入口不会因窄窗口消失。

## 17. UI 截图与人工验收

截图目录：`D:\AI AGENT\RAWSelectionAssistant\artifacts\ui-review\2.0.1`。

1. `01_Workbench_Dark_1600x920.png`：默认三栏深色工作台。
2. `02_Workbench_Dark_1920x1080.png`：宽屏扩展结构。
3. `03_Toolbox_Popup.png`：锚定式 2 × 4 工具浮层。
4. `04_Toolbox_FullPage.png`：3 列完整工具箱。
5. `05_RecentProjects.png`：标签与演示项目卡。
6. `06_TaskCenter_WithTasks.png`：明确标注演示的扫描/复制任务。
7. `07_TaskCenter_Empty.png`：真实空任务状态。
8. `08_Settings_Dark.png`：深色设置模态和遮罩。
9. `09_Workbench_Light.png`：浅色同构布局。
10. `10_Compact_1280.png`：任务中心折叠且不重叠。
11. `11_Sidebar_Collapsed.png`：54px 图标侧栏。
12. `12_Feedback_Dialog.png`：真实反馈对话框。

12 张均由实际启动的 UI Review WinExe 渲染，不是设计稿或拼图；所有截图已逐张人工检查。当前捕获方式记录 WPF 客户区（系统原生标题栏不在位图内），标题栏名称和图标由真实窗口属性及发布程序验证。

人工硬性验收 20/20 通过：默认深色、紧凑侧栏、主入口+4 快捷工具、2×4 Popup、3 列工具箱、两张中央大卡、最近项目、独立任务中心、窗口填充、无错误旧 UI、无禁用业务模块、菜单保留、原创 Logo、深色设置、1280 不重叠、深色菜单清晰，并确认不是仅换黑色背景。

## 18. 自动化与业务测试

- Release Build：通过，0 警告、0 错误。
- 全部测试：356/356 通过，0 失败，0 跳过。
- 工作台 2.0.1 视觉结构专项：40/40 通过。
- UI 回归覆盖：三栏 Shell、顶部主入口、4 个快捷工具、Popup 2×4/8 项、完整工具箱 3 列、项目概览、处理任务、最近项目、任务中心、1280 折叠、侧栏折叠、设置深色资源、菜单和浅色同构、禁用模块缺失检查。
- 业务回归覆盖：本地分片、归片工作区、JPG/RAW/自定义格式、客户 JPG 安全模式、冲突、复制、报告导出、项目历史、教程和授权门控。
- 安全形态：Provider=None、Release Mock 禁用、WinExe、无本地 Web 地址、无独立后台服务器。

## 19. 发布与安装包

- self-contained 发布目录：`D:\AI AGENT\RAWSelectionAssistant\artifacts\publish\win-x64`
- 安装包：`D:\AI AGENT\RAWSelectionAssistant\artifacts\installer\像素蛋挞_Setup_2.0.1_x64.exe`
- 安装包大小：48,765,008 字节。
- SHA-256：`5255D10DCAAE6E5EA4E7916E62AD07B0C38487BA86EBF73BC8DC5C0FCE00241C`
- Inno Setup 7.0.2 编译成功。

## 20. 新增和修改文件

本工作目录没有 `.git` 元数据，无法提供 Git diff；以下根据本轮文件审计记录：

### 新增

- `docs\reference\像素蛋挞_UI参考拆解_2.0.1.md`
- `tools\VideoFrameExtractor\VideoFrameExtractor.csproj`
- `tools\VideoFrameExtractor\Program.cs`
- `tools\create_video_contact_sheets.py`
- `tools\capture_ui_review_set.ps1`
- `reference\xiangsuben\bandicam 2026-07-30 17-51-10-620.mp4`
- `reference\xiangsuben\xiangsuben_v1.9.18.exe`
- `artifacts\ui-reference\frames-1s\`（139 帧）
- `artifacts\ui-reference\contact-sheets\`（12 张）
- `artifacts\ui-review\2.0.1\`（12 张本轮验收图）
- 本报告。

### 修改

- `src\RAWSelectionAssistant\MainWindow.xaml`
- `src\RAWSelectionAssistant\MainWindow.xaml.cs`
- `src\RAWSelectionAssistant\ViewModels\MainViewModel.cs`
- `src\RAWSelectionAssistant\Resources\DesignSystem\Theme.Dark.xaml`
- `src\RAWSelectionAssistant\Resources\DesignSystem\Theme.Light.xaml`
- `src\RAWSelectionAssistant\Resources\DesignSystem\Controls.Navigation.xaml`
- `src\RAWSelectionAssistant\Views\FeedbackDialog.xaml`
- `src\RAWSelectionAssistant\Views\FeedbackDialog.xaml.cs`
- `tests\RAWSelectionAssistant.Tests\WorkbenchVisualCorrection201Tests.cs`
- `tests\RAWSelectionAssistant.Tests\UiPolish142Tests.cs`
- `tests\RAWSelectionAssistant.Tests\UiSimplification144Tests.cs`
- `tools\prepare_ui_review.ps1`

2.0.1 既有原创 Logo 和项目封面资源继续由安装包打包，没有引入参考软件资产。

## 21. 已知问题

- 当前授权 Provider 仍为 None，因此“升级”仅进入授权说明，不会伪造购买或激活成功。
- 批量压缩、水印、删废片、FTP、照片整理、重命名和转档中的未完成功能仍标为“预览”，不会假装可执行。
- 内置验收截图捕获 WPF 客户区，不包含 Windows 原生标题栏；真实发布窗口仍通过 `Title`、`Icon` 和深色原生标题栏 API 显示品牌。
- 参考安装包仅保存用于合规观察，没有打包、加载或引入本项目。
