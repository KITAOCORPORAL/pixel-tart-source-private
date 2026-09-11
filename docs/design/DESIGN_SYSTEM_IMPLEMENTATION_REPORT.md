# Pixel Tart UI Phase 1：Design System Engineering Migration

日期：2026-09-11  
提交目标：`refactor(ui): implement Pixel Tart design system foundation`

## 已迁移 Token

- Colors：Dark、Light、HighContrast 三套语义色板，包含 App/Surface/Border/Text、Primary 交互色、PhotographyGold、Success/Warning/Danger/Info 与日历五色。
- Typography：PageTitle、Hero、Section、Card、Body、Secondary、Caption、NumericLarge；中文使用 Microsoft YaHei UI，拉丁字符和数字使用 Segoe UI Variable。
- Spacing：4、8、12、16、24、32、48 DIP，并提供 Thickness、水平/垂直兼容别名。
- Radius：4、6、8、10、12 DIP。
- Elevation：ElevationNone、ElevationLow、ElevationFloating；浮层阴影保持低对比，不抢占摄影内容。
- DesignTokens：控件高度、行高、页面内边距、侧栏尺寸、抽屉/模态宽度与 120–180ms 动效时长。

统一资源入口：`Resources/DesignSystem/PixelTart.Theme.xaml`。该入口保留旧主题键作为兼容别名，页面可渐进切换。

## 已迁移组件

- 统一入口：`Resources/DesignSystem/PixelTart.Components.xaml`。
- Button：Primary、Secondary、Ghost、Danger、Icon，并覆盖 Normal、Hover、Pressed、Focused、Disabled；新增 `PixelTart*` 语义别名。
- Panel/Card/Border：PanelSurface、CardSurface、ElevatedSurface 及语义信息/警告/危险/成功变体。
- Inputs：TextBox、ComboBox、PasswordBox、ListBox、CheckBox、RadioButton、ToggleButton、DatePicker 统一高度、圆角、焦点边界和禁用态。
- Menu：Menu、ContextMenu、MenuItem、TabItem、Separator 统一表面、悬停、打开、键盘焦点和危险层级。
- Navigation、Calendar、Modal、Drawer、Tooltip、Status、Tables、Icons、ScrollBars、EmptyState 均由组件入口集中合并。

未删除旧字典；`App.xaml` 仍保留兼容合并清单，以满足现有页面和审计工具，后续按页面迁移后再收缩。

## 未迁移页面

以下页面仍存在局部 Margin/Padding 或少量历史语义键，暂不在 Phase 1 大规模改写：

- `MainWindow.xaml`
- `Views/ArchivedBookingsView.xaml`
- `Views/BatchCompressionModal.xaml`
- `Views/BookingDocumentsPanel.xaml`
- `Views/BookingRemindersPanel.xaml`
- `Views/BookingWeatherPanel.xaml`
- `Views/CandidateSelectionWindow.xaml`
- `Views/ClientMonitorWindow.xaml`
- `Views/CollageView.xaml`
- `Views/DayCalendarView.xaml`
- `Views/DaySchedulePanel.xaml`
- 其余 `Views/*.xaml` 页面保持业务逻辑不变，后续以页面为单位替换为 `PixelTart*` 语义键。

## 剩余硬编码统计

统计范围：排除 `bin/obj`，页面 XAML 与 DesignSystem XAML 分开统计。

| 项目 | 数量 | 说明 |
|---|---:|---|
| 页面 XAML 文件 | 37 | 含 App/MainWindow 与 35 个视图/窗口 |
| DesignSystem 资源文件 | 36 | Token、Theme、组件与兼容字典 |
| 页面硬编码颜色 | 15 | 主要为历史局部覆盖，后续替换为语义 Brush |
| 页面字面量 Margin | 992 | 优先按页面/容器迁移到 Spacing* |
| 页面字面量 Padding | 133 | 优先按 Button/Input/Card 迁移 |
| 资源层颜色字面量 | 151 | 集中在 Colors/Theme/Accent；组件层不再新增任意 Hex |

## 风险

- 旧页面仍引用兼容键；直接删除旧字典会导致运行时资源缺失，因此本阶段采用双入口与渐进迁移。
- 资源字典重复合并会增加维护成本，但可避免一次性切换造成视觉回归；下一阶段应按页面完成后移除兼容清单。
- 自动化 DPI 证据目录未随当前工作树提供，DPI 证据测试会因缺少历史 JSON 失败；这不是本次 UI 代码编译错误。
- 未在本机执行真实图片导入、缩略图浏览和物理显示器验收；业务服务、Asset Model、Repository、Database、Import、Query 均未修改。

## 测试与验收

- `dotnet build RAWSelectionAssistant.sln --no-restore -p:Platform=x64 -p:Configuration=Debug`：通过，0 警告、0 错误。
- Design System 锁定与 UI 资源测试：通过（8/8）。
- 全量测试：业务/API/WPF 常规测试通过；26 个 DPI 证据测试因缺少 `artifacts/automated-dpi-review/2.0.4` 历史证据文件失败。
- 启动/图片浏览/缩略图/导入：代码路径未改动，需在安装版执行最终人工冒烟。

