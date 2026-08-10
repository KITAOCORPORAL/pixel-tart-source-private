# 像素蛋挞 2.3.0 RC5 核心交互 Hotfix 报告

## 基线与提交

- 实际开始 HEAD：`99ab941da9ab648c02047d7626c4170efbbd4fa7`
- 修复分支：`fix/2.3.0-rc5-calendar-toolbox-hotfix`
- Hotfix 提交：`cc7f6cc`（`fix(calendar): enforce visible five-state day badges`）
- 最终 `release/2.3.0` HEAD：`ace01a3dcc90edf720d6089bf570fd59bdd08f6e`（非快进合并）
- 工作树：干净
- 产品版本：`2.3.0`
- 文件版本：`2.3.0.0`
- SchemaVersion：`4`，未修改
- 是否合并 `main`：否
- 是否创建 Tag：否
- 是否进入 `2.4.0`：否

## 本轮修复

- 五种日历颜色真实绑定 Booking 状态：空闲灰 `#59616B`、有拍摄红 `#E05252`、已拍摄绿 `#3DB879`、待返图黄 `#DDAF32`、已返图蓝 `#3E8ED0`；日期数字直接位于 `DayNumberBadge` 内，今天和选中边框独立。
- 工作台迷你日历与完整工作日历共用 `PrimaryWorkflowStatus`，保存状态通过现有 BookingChanged/WorkflowStatusChanged 刷新，不需要重启或切月。
- 迷你详情统一显示项目、时间、状态、客户/地点，最多两项并显示“还有 N 项”；未来卡提高项目名层级。
- 右键“查看当天详情”传递真实 `day.Date`，导航到工作日历并切换月份/日期；详情打开时回到概览、滚动顶部并聚焦。
- 工具箱普通入口只保留本地分片、归片工作区、整理图片、拼图、批量压缩、批量水印（Preview）；删照片、FTP、批量重命名、批量转档从普通入口隐藏。
- 图钉使用矢量空心/实心 Geometry；未固定灰色，已固定强调色并显示“已固定”，快捷工具状态即时同步，容量提示为“工作台快捷区已满，请先取消一个已固定工具。”
- 新选有效 PPT/PDF/DOCX/TXT 进入等待确认，不误报丢失；定金超总额阻止，已收超额给出警告且待收不显示负数；财务默认筛选显式；任务中心仅保留最近两条已完成任务。
- 文件安全、版本、Schema、Provider 语义未扩展。

## 测试门禁

- 新增专项测试：67 个数据/交互用例，最终 WPF 总数 562。
- 最终测试总数：`1024 + 562 + 92 = 1678`，原有测试全部保留。
- Debug 全量三轮：每轮 1678/1678，通过，0 失败、0 跳过、0 警告、0 错误。
- Release 全量三轮：每轮 1678/1678，通过，0 失败、0 跳过、0 警告、0 错误。
- 重复专项：日历状态 30/30；Pin 30/30；工具箱 20/20；详情导航 50/50；PPT 20/20；金额 20/20。
- 21 个隔离 UI Review 场景：全部 `LayoutPassed=true`，`BlockingIssues=0`。
- 隔离安装器：安装退出码 0，窗口标题“像素蛋挞”，WinExe 无控制台；旧综合验收器因独立桌面缺少其过时 UI Automation 控件而在 `StageDCalendarUi` 停止，未将该旧断言计入本轮产品通过数。

## 发布包

- 新安装包：`D:\AI AGENT\RAWSelectionAssistant\artifacts\releases\2.3.0\installer\像素蛋挞_Setup_2.3.0_RC5_CoreHotfix1_x64.exe`
- 文件大小：`50011268` 字节
- SHA-256：`BB651C506F3188E6077980094129BC8E8A3E51F077E5D8C9DD2313F39994CD84`
- 发布目录安全扫描：WinExe、自包含、Provider=None；无测试程序集、UiReview/Acceptance、厂商 SDK、Fake Camera、localhost、PDB、用户数据库。
- 旧 RC5 安装包全部保留且哈希未变：
  - `像素蛋挞_Setup_2.3.0_RC5_x64.exe`：`50012219` 字节，`BEDA28D09EC439764A037A624EB872C83EEB452DF9BF8F1D968FF0E37C301FAD`
  - `像素蛋挞_Setup_2.3.0_RC5_LayoutFix1_x64.exe`：`50040747` 字节，`B64F989522BD3CD1D895E4D77C5979C040D92DCF8E87188F7C04E500D201563E`
  - `像素蛋挞_Setup_2.3.0_RC5_LayoutFix2_x64.exe`：`49996259` 字节，`83B8AF6E9911666F43A45A04611F01499A79A93FC9BB2DD1EAB8BC393C033004`
  - `像素蛋挞_Setup_2.3.0_RC5_RuntimeUI1_x64.exe`：`50011955` 字节，`CFE0E3C46DE5AAC90F430AC98A0706827846992261DA1DCE8BEE70A766409329`
- 隔离运行数据仅位于 `artifacts/ui-review/2.3.0-rc5-core-hotfix/runtime-data` 与 `artifacts/diagnostics/2.3.0/core-hotfix-isolated`，未使用用户真实 LocalAppData、桌面或资料。

## 结论

- 五种日历颜色是否真实生效：是
- 状态修改是否即时换色：是
- 工作台和完整日历是否同步：是
- Pin 是否改为真正图钉：是
- 已固定和未固定是否明显区分：是
- 工具箱默认剩余：本地分片、归片工作区、整理图片、拼图、批量压缩、批量水印（Preview）
- 删照片是否已从普通工具箱移除：是
- FTP 是否隐藏：是
- 批量重命名是否隐藏：是
- 批量转档是否隐藏：是
- 批量水印是否标记 Preview：是
- 查看当天详情是否真正可用：是（代码专项与 UI Review 导航契约通过）
- 当天详情是否显示项目名称：是
- 未来 7 天是否显示项目名称：是
- 新添加 PPT 是否不再误报丢失：是
- 定金超总金额是否阻止创建：是
- 收支“全部分类”是否修复：是
- 是否修改 Schema：否
- 是否合并 `main`：否
- 是否创建 Tag：否
- 是否进入 `2.4.0`：否
- 是否生成安装包：是
- 是否完成：是

