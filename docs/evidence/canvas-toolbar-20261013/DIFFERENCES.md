# 源码与设计差异

源码提交 ea8c437bf44fbaef20556fd5a073e93c4053452a；基线 3c5a5ff178198fcedbdfd580a807c68bd5ee1b78。

|文件|类型|本轮修改|
|---|---|---|
|src/RAWSelectionAssistant/Views/ReferenceColorWorkspaceView.CanvasToolbar.cs|新增生产源码|四个鼠标模式、原图锁定/按住、焦点范围、生命周期清理、禁用条件|
|src/RAWSelectionAssistant/Views/ReferenceColorWorkspaceView.xaml.cs|修改生产源码|接入真实鼠标、键盘、取样revision失效、旧工作区卸载清理|
|src/RAWSelectionAssistant/Views/ReferenceColorWorkspaceView.xaml|修改界面|工具带移到画布上方；五个真实入口；100%；旧撤销重做条件|
|src/RAWSelectionAssistant/Resources/Studio/zh-CN.json|修改资源|简体中文工具栏文案|
|src/RAWSelectionAssistant/Resources/Studio/zh-TW.json|修改资源|繁体中文工具栏文案|
|src/RAWSelectionAssistant/Resources/Studio/en-US.json|修改资源|英文工具栏文案|
|tests/RAWSelectionAssistant.WpfTests/CanvasToolbarTests.cs|新增测试|按钮路由、快捷键范围、缩放/比较不改像素、真实样本像素、撤销重做、会话往返、取消/卸载|
|docs/evidence/canvas-toolbar-20261013/|新增文档证据|逐项状态、参考哈希、coverage映射、策划拆分、测试与发布身份、人工验收|

Core、RAW处理器、InstantFilm、快速导出、Publishing、主窗口与策划中心生产代码均无diff。其他Grok页面不重排。无新包、图像素材或原型JS。照片处理版本保持原合同，不把独立RAW定义接到旧调整栈。

## 已知限制

- 当前工具带是第一段真实功能，不是完整五组菜单视觉还原；菜单/折叠/新像素工具后续验收。
- 颜色取样必须选中颜色范围节点；未选节点不自动生成空编辑。独立PV4/5/6禁用此旧栈功能。
- undo历史不保存到会话；保存的是处理结果定义，符合现有格式。重开后新的观察工具默认选择。
- 100%不是细节覆盖率；全图HQ/RAW代理继续使用已有状态报告。
- 首次WPF全量本地化门禁失败1项，已通过三语资源修复，不删失败记录。后续全量通过。
- 完整源WPF通过后，追加旧DataContext取样revision清理及像素变更断言；最终源码专项20项及发布组件完整回归覆盖最终版本，不把前次二进制冒称最终产物。
- 测试重新生成三个历史ui-guardian文件：本轮生成副本留在ignored artifacts/canvas-generated-reports，原受跟踪文件恢复到本轮基线。未改其他工作树文件。
