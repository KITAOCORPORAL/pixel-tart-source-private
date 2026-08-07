# 像素蛋挞 2.3.0 RC5 迷你工作日历真实尺寸 Hotfix 报告

## 结论

- 完成状态：已完成。
- 分支：`release/2.3.0`。
- Hotfix 实现提交：`463e3f1fd3bdfd19eee2e0e8f38f2e5a660f1129`。
- 产品版本：`2.3.0`。
- 文件版本：`2.3.0.0`。
- SchemaVersion：`4`，未修改数据库、Schema 或迁移。
- 未修改五种日历业务状态及颜色语义，未修改完整工作日历业务逻辑，未修改任务中心结构。

## 精确根因

安装版运行时右栏宽度约为 278.7 DIP、高度约为 384 DIP。原迷你日历日期网格最小高度为 148 DIP，六行平均只有约 24.67 DIP；但日期格同时包含外层 Margin、Padding，以及固定 24 DIP 高的 Badge，实际所需高度约 34 DIP。UniformGrid 分配的行高小于内容所需高度，导致 Badge 和日期文本在外部 DayCell/网格行边界被裁切，多行视觉上相互挤压。问题不是状态颜色层或文字 ZIndex。

左右翻月按钮的问题来自通用按钮样式继承的全局 MinHeight。原控件只设置 Height，没有在局部覆盖 MinHeight，导致运行时左、右按钮可能被不同布局条件拉伸。

## 最小修复

- CalendarDayCell：`MinHeight=32 DIP`，`Margin=2 DIP`，`Padding=3 DIP`。
- DayNumberBadge：`MinWidth=24 DIP`，`Height=22 DIP`，`Padding=5,0 DIP`。
- 日期文本：`FontSize=12 DIP`，`LineHeight=16 DIP`，水平和垂直居中。
- 日期网格：固定提供 `216 DIP` 布局高度；六行运行时 DayCell ActualHeight 为 `32 DIP`，行间净距为 `4 DIP`。
- 今天状态只改变描边颜色，不再改变 BorderThickness；Normal、Today、Selected 和五种业务状态的 Badge ActualHeight 均保持 `22 DIP`。
- PreviousMonthButton 与 NextMonthButton 使用同一 GhostButton 样式，并同时显式设置 `30×30 DIP`、`MinWidth=30 DIP`、`MinHeight=30 DIP`、相同 Padding 和垂直居中。
- 最后一排日期到当天详情标题的实测净距为 `18 DIP`；真实安装版 150% DPI 物理像素间距为约 31 px（约 20.7 DIP）。
- 未通过关闭 ClipToBounds、负 Margin、TranslateTransform 或 ScaleTransform 掩盖问题。

## 真实尺寸与自动视觉断言

新增真实 WPF 布局断言，覆盖：

1. Badge ActualHeight 至少比 Text ActualHeight 多 4 DIP；
2. DayCell 可完整容纳 Badge 与安全边距；
3. Badge 和 Text 均不越出父级边界；
4. 相邻日期行净距至少 4 DIP；
5. 最后一行与当天详情净距至少 16 DIP；
6. 左右翻月按钮宽高一致且不超过 32 DIP；
7. 42 个日期 Badge 高度一致；
8. 100%、125%、150%、175%、200% DPI 下逻辑尺寸稳定；
9. 禁止负 Margin、TranslateTransform 和 ScaleTransform。

实测典型值：

- 1600×900：DayCell `36×32 DIP`，Badge `27.33×22 DIP`，Text `16 DIP`，按钮 `30×30 DIP`，最后一排间距 `18 DIP`。
- 1280×720：DayCell `32.67×32 DIP`，Badge `24×22 DIP`，Text `16 DIP`，按钮 `30×30 DIP`，最后一排间距 `18 DIP`。
- 200% DPI：逻辑尺寸仍为 DayCell `32.67×32 DIP`、Badge `24×22 DIP`、Text `16 DIP`、按钮 `30×30 DIP`。

## WPF 原尺寸截图

证据目录：

`D:\AI AGENT\RAWSelectionAssistant\artifacts\ui-review\2.3.0-rc5-mini-calendar-hotfix`

已生成并逐张检查：

- `01_MiniCalendar_Default.png`
- `02_MiniCalendar_AllStates.png`
- `03_MiniCalendar_DoubleDigits.png`
- `04_MiniCalendar_Today.png`
- `05_MiniCalendar_Selected.png`
- `06_MiniCalendar_LastRow.png`
- `07_MiniCalendar_6Weeks.png`
- `08_MiniCalendar_1280x720.png`
- `09_MiniCalendar_1280x768.png`
- `10_MiniCalendar_1600x900.png`
- `11_MiniCalendar_1920x1080.png`
- `12_MiniCalendar_Dpi150.png`
- `13_MiniCalendar_Dpi200.png`
- `14_MonthNavigation.png`

14 张截图的自动布局结果均为 `LayoutPassed=true`、`MiniCalendarPassed=true`。空闲灰、有拍摄红、已拍摄绿、待返图黄、已返图蓝均保持原业务语义；单双位日期数字完整，今天描边和选中外框不改变 Badge 尺寸。

## 测试门禁

- 最终测试总数：`1535`，高于原基线 `1525`。
- Debug 构建：通过，0 警告、0 错误。
- Debug 全量：连续 3 轮，每轮 `1535/1535`，0 失败、0 跳过。
- Release 构建：通过，0 警告、0 错误。
- Release 全量：连续 3 轮，每轮 `1535/1535`，0 失败、0 跳过。
- 迷你日历布局专项：连续 30 轮，每轮 `10/10`，全部通过。
- 1280×720 紧凑布局专项：连续 20 轮，每轮 `4/4`，全部通过。
- 150% DPI 专项：连续 20 轮，每轮 `1/1`，全部通过。
- 200% DPI 专项：连续 20 轮，每轮 `1/1`，全部通过。

## Publish 与安装包

- Release 自包含 win-x64 Publish：成功。
- WinExe：确认，PE Subsystem=`2`。
- ProductVersion：`2.3.0`。
- FileVersion：`2.3.0.0`。
- 发布目录测试程序集：0。
- 发布目录 PDB：0。
- 最终安装包：

`D:\AI AGENT\RAWSelectionAssistant\artifacts\releases\2.3.0\installer\像素蛋挞_Setup_2.3.0_RC5_LayoutFix2_x64.exe`

- 文件大小：`49,996,259` 字节。
- SHA-256：`83B8AF6E9911666F43A45A04611F01499A79A93FC9BB2DD1EAB8BC393C033004`。
- 原 RC5 安装包仍存在，大小和 SHA-256 未改变。
- LayoutFix1 安装包仍存在，大小和 SHA-256 未改变。

## 隔离安装版真实启动验证

为避免覆盖电脑中现有的正式安装记录，使用与最终包完全相同的 Release Publish 载荷、独立 Inno AppId 和独立运行数据目录完成隔离安装。安装成功、真实启动成功、主窗口标题为“像素蛋挞”，验收后正常关闭并卸载；没有使用用户真实 LocalAppData 数据库。

真实安装版在系统 150% DPI 下的 UI 自动化实测：

- 左右翻月按钮物理尺寸均为 `45×45 px`，即 `30×30 DIP`，完全一致；
- CalendarDaysGrid 为 42 个日期项，六行每项物理高度 `54 px`，即 `36 DIP`，行间无叠加；
- 日期 1、7、8、10、13、14、15、21、28、30、31 均完整显示；
- 日期文本在每个日期项内上下留有安全空间，无下半部裁切；
- 最后一排底部到当天详情标题约 `31 px`，大于 16 DIP 要求；
- 五色状态图例完整，今天和选中日期正常；
- 任务中心保持上一轮布局，Header、Summary、中央任务列表和 Footer 均存在，未改变结构。

## 禁止项确认

- 是否修改 Schema：否。
- 是否合并 main：否。
- 是否创建 Tag：否。
- 是否进入 2.4.0：否。
- 是否修改完整工作日历：否。
- 是否修改任务中心结构：否。
- 是否修改天气、收支或联机功能：否。
