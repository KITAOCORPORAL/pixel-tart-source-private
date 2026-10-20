# 首轮原始失败及复现，未覆盖原结果

冻结起点 f6adf8674bf9c3b7159caeb086c04a0d1eadb923。原始完整Core/WPF/DPI结果保留。

- WPF1709：1682通过、7失败、20未执行，0缺失。完整适配器发现+逐类隔离，见FULL_WPF。
- ComparisonScheduling：旧测试FindName(ComparisonMode)为null；当前产品已改成真实菜单。独立REPRO_COMPARISON_FAILURE再次失败。修复测试通过当前CreateComparisonMenu真实MenuItem.Click驱动同一VM，不恢复隐藏旧控件，不删像素/历史断言。
- PhotographerTrialPage：同一旧ComboBox合同及旧页签索引。更新为当前光片页签及实际菜单内容，保留缩略图/历史/撤销/导出业务断言。
- NavigationWorkbenchClosure：已改名暗室而旧文本期待参考仿色；路由ReferenceColor保持。同步可访问名为暗室，旧测试期待实际已批准名称；不改变导航数目/命令。
- StudioDesignSystem：RailButton未分类、缩放Unicode符号。真实产品改为已分类IconButton及矢量放大缩小，不放宽测试。
- RuntimeDialogs：本轮启动护栏及旧暗室方案切换使用直接MessageBox。改为已有ThemedMessageDialog，保存/放弃/取消保留三选语义，Enter/Esc保留；不改像素。
- PlanningAutomationContract：最后成功v2-navigation/v1-date/create-transition，随后策划正文旧ValuePattern查询失败。旧脚本还期待灯光图/服化道/PlanningShotRow等早期结构，当前为15分区与DataGrid。保持FAIL，不把简单页面打开取代真实自动化合同；需新的完整可访问性合同复验。
- DPI合同91项：74通过、17失败，原因缺少当前运行截图manifest及dpi-current目录。未复制历史截图伪造本轮；物理DPI实机BLOCKED。
- 桌面真实输入/激活：GetCursorPos拒绝访问0x80070005。完整EXE已启动并核对模块，首次教程尚未退出；没有逐模块鼠标闭环。

## 本轮修复后专项

比较菜单8、导航9、设计系统6、RC3运行UI143、完整壳2专项均通过；摄影页在旧ComboBox修复后进一步暴露历史效果恢复提示不含可执行撤销路径，产品文案已明确“可撤销返回普通编辑”；摄影页2及新增三选消息框/完整入口3通过。最终完整发布与全量回归另跑，不把这些专项总和冒充全量。

产品修复提交：96e84b14a0cbb040b97cc531efeb31b1f4144367；诊断护栏起点f6adf8674bf9c3b7159caeb086c04a0d1eadb923。没有像素算法改动，没有降低阈值、删失败或把规划能力变成按钮。策划自动化与DPI失败保持开放。

## 最终包全量复验新增原始失败

R3CommandLineReviewTests.TwoExplicitAuthorizedPhotosExerciseCurrentProductPipeline复用首轮固定KITAO_R3_CLI_OUTPUT，试图覆盖已有review.ptstudio.json，被产品已有未读取文件覆盖保护正确拒绝。原全量Failed保留；不关闭保护、不删原输出，改用新目录final-r3-fresh-output独立重跑同一最终DLL，结果另列。若复验通过只能称环境隔离修正，不把原全量失败抹掉。

R3全新输出目录复验同一最终包DLL：14/14通过，0跳过。原最终全量中的Failed继续保留，明确为测试输出目录复用，不提升真实桌面成绩。
