# 策划文档编辑器实施记录

## 已实现

新增 `PlanningDocumentTextEditing`，为现有策划正文提供确定性的范围编辑：标题 1/2、加粗、斜体、项目符号、编号、引用和分隔线。`PlanningCenterView` 增加紧凑工具栏，并连接：

- Ctrl+B、Ctrl+I；
- WPF 原生撤销/重做；
- 当前选择或光标位置格式操作；
- 既有自动保存、重新打开和 PDF 导出路径。

源码：

- `src/RAWSelectionAssistant.Core/Services/Planning/PlanningDocumentTextEditing.cs`
- `src/RAWSelectionAssistant/Views/PlanningCenterView.xaml.cs`
- `tests/RAWSelectionAssistant.Core.Tests/PlanningDocumentTextEditingTests.cs`

## 兼容性

保存内容仍为旧版可读取的文本；没有替换 PlanningDocument、ProjectId、ShotId 或现有数据库。没有声称完成字体、表格、分页、DOCX 或审阅功能。

## 验证

- Core 聚焦测试：7 通过；
- WPF 项目构建成功；过滤的 WPF 运行：0 失败、1 跳过；
- 完整 Core/WPF 回归均通过。

Release 桌面操作未执行，因此 CODE=PASS、TEST=PASS、RELEASE_RUNTIME=NOT_RUN、USER_ACCEPTANCE=NOT_APPROVED。
