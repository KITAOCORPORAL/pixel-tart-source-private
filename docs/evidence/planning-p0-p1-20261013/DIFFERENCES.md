# 差异清单与限制

生产源码以28642bc71bb09dcc7b0def84f9cda02bc8483b1a构建，后续提交仅测试适配与交付证据。

|文件组|改动|
|---|---|
|Core/Projects/PlanningDocument.cs|FoundationVersion、稳定文档ID、自定义列与校验、叶节点扩展字段|
|Core/Projects/PlanningCenter.cs|迁移接入、正文包同步、持久草稿取消、原字节备份、Summary/视觉链接/拍摄关系扩展字段|
|Core/Projects/ProjectShotExecution.cs|新镜头字段、Notes一次迁移、列值、catalog Revision、未知字段、批量原子保存|
|Core/Projects/PlanningMigrationBackup.cs|首次迁移原JSON字节备份，不覆盖已有备份|
|Core/Projects/PlanningDocumentPackage.cs、PlanningRichLayout.cs|保留额外未知字段，不替换原RTF/画布快照|
|VM/PlanningCenterViewModel.Document.cs|恢复草稿优先、校验、两文件失败重试、UI提交回调、同文档保护|
|VM/PlanningCenterViewModel.cs、Foundation.cs、Overview.cs|镜头操作统一入草稿、稳定列接口、失败门禁|
|VM/PlanningShotEditRow.cs|IEditableObject行事务、Esc取消、无效输入原文恢复|
|VM/MainViewModel.cs|离开策划页/返回前等待保存；失败不改变路由；其余页面布局不动|
|Views/PlanningCenterView.xaml.cs、Foundation.cs|项目概览、文档内容、11列编辑表、自定义列、切换/关闭前提交|
|Tests/PlanningFoundationTests.cs|3项Core合成迁移/原字节备份/未知字段/取消/列合同|
|Tests/PlanningFoundationWpfTests.cs|3项WPF集成：行编辑/取消/排序复制归档/列往返；校验/文件锁/跨文件恢复；UI提交失败及无效文本|
|Tests/StageVPlanningWpfTests.cs|更新真实9分区路由断言，不伪造15模块已实现|
|Tests/PlanningProposalAcceptance.cs|可选视觉夹具索引适配新增分区；未启用实机门禁|
|Tests/NavigationSafety204Tests.cs|保留工作台导航断言并验证新保存门禁先于路由，不再限定旧表达式方法语法|

上表路径前缀分别为src/RAWSelectionAssistant.Core/Services、src/RAWSelectionAssistant、tests对应项目；完整文件及SHA见FILE_INVENTORY。

## 已知边界

- P0提供单进程安全写入和跨文件恢复日志，不是多进程并发合并；P8的期望Revision/hash/watch和冲突副本仍未实现。
- 严格的任何未来未知类型无限保真不作保证；当前JSON扩展字段保留，且迁移原字节备份可恢复。未来Foundation/PlanningFields版本拒绝。
- P1a本批完成项目概览/文档内容/旧正文附件保全；15个摄影分类中P2新模块不提前画假入口。
- 自定义列仅文本类型，删除为可恢复隐藏；尚无自定义列重命名/类型设计和任意列拖拽布局持久化。
- 镜头顺序通过上移/下移；原有重排接口保留。11核心列不包括参考图缩略列；参考图对象和原有页面保留。
- 完整Word、WPS往返和双模式块同步NOT_IMPLEMENTED_THIS_BATCH。
- 无桌面、DPI、导航实操和WPS测试。后台WPF对象/程序集测试不是实机批准。

## 本轮发现及纠正

1. Core全量首次发现旧正文包Body未同步，新EnsureIdentity只保证身份不更新正文；在Store显式同步Package.Body，原回归不放宽。
2. 同一工作树全量WPF运行时重建专项输出导致文件锁失败；没有杀其他进程，等本轮全量结束后复跑成功，失败日志保留。
3. 实际发布Core的静态导航测试限定旧单行ReturnToWorkbench语法，新增门禁后失败。修改测试检查保存门禁先于相同工作台路由，保留旧禁止恢复向导断言。
