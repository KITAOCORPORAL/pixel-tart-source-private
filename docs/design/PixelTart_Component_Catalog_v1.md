# Pixel Tart Component Catalog v1.0

## 1. 基础资源

- Colors：主题色、文本、边界、语义色、日历状态色。
- Typography：PageTitle、Hero、Section、Card、Body、Secondary、Caption、NumericLarge。
- Spacing：4、8、12、16、24、32、48。
- Radius：4、6、8、10、12。
- DesignTokens：控件高度、行高、动效、侧栏和页面兼容指标。

## 2. Buttons

- PrimaryButton：当前工作区唯一主要动作。
- SecondaryButton：常规操作。
- GhostButton：工具栏、标签切换和低权重操作。
- DangerButton：归档以外的破坏性或高风险动作。
- IconButton：20–22 DIP 线性图标，必须提供 AutomationProperties.Name 和 Tooltip。

## 3. Inputs

TextBox、ComboBox、DatePicker、CheckBox、RadioButton 和 ToggleButton 统一高度、6 DIP 圆角、BorderSubtle 默认边界、BorderStrong 或 Primary 聚焦边界。占位文本不是默认值。

## 4. Cards and panels

PanelSurface 用于页面内稳定分组；CardSurface 用于可独立操作的对象；ElevatedSurface 仅用于浮层。语义状态不能通过整卡高饱和填色表达。

## 5. Navigation

SidebarNavItem 使用唯一线性图标、明确选中指示和 40 DIP 最小高度。一级导航不超过七个业务模块。辅助入口归入系统区或帮助。

## 6. Calendar

- CalendarDayCell：日期格。
- CalendarDayNumberBadge：五色实心日期标记。
- TodayOutline：与业务状态独立。
- SelectedOutline：与业务状态、今天状态独立。
- ClosedOverlay：锁图标和降亮，不引入第六色。
- CalendarBookingItem：只显示项目、时间和必要状态；编辑进入菜单或详情。

状态来源必须是 CalendarDayVisualState；颜色解析必须集中，不允许迷你日历和完整日历各自维护另一套业务色。

## 7. Floating components

- ModalSurface：12 DIP 圆角、固定 Header/Footer。
- DrawerSurface：10 DIP 圆角、固定 Header/Footer。
- TooltipSurface：短解释。
- ContextMenuSurface：对象动作。
- ToastSurface：非阻塞结果与错误提示。

## 8. Empty states

EmptyState 由图标、标题、说明和可选单一动作组成。不能使用大型插画抢占摄影内容；不得伪造统计或示例业务数据。

## 9. Icons

图标默认 20–22 DIP、统一圆角线帽和线宽。禁止 emoji、字体符号混用、同一图标复用为不同语义。PhotographyGold 只用于 Pin 或摄影身份提示。

## 10. 状态

所有可交互组件必须提供 Default、Hover、Pressed、Focused、Disabled；选择类组件还需 Selected；异步组件还需 Busy、Success、NeedsAttention 和 Error。
