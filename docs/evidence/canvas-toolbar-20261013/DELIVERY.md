# 第一段交付总表

源码已提交：ea8c437bf44fbaef20556fd5a073e93c4053452a。
基线：origin/integration/kitao-color-startup-20261013 的 3c5a5ff178198fcedbdfd580a807c68bd5ee1b78。
工作树：N:/pixart/kitao-canvas-toolbar-20261013。
工作分支：feature/kitao-canvas-toolbar-20261013。不合并主分支。

|交付项|结果|证据|
|---|---|---|
|N盘/实际仓库/独立工作树/用户改动保全|完成|START_AND_REFERENCES|
|真实代码/整合证据/Grok指定资料核对|完成|REFERENCE_MANIFEST、TOOL_AUDIT、PLANNING_IMPLEMENTATION_BATCHES|
|选择、平移缩放、取样、比较、撤销重做|代码完成，有限域明确|CanvasToolbarTests、TOOL_AUDIT|
|PV4/PV5/PV6与导出合同|未改变生产处理器|Core无源码diff；完整回归及发布组件测试|
|新像素工具|未交付，不显示假可用功能|逐工具DEFERRED/字段缺口|
|策划中心实施拆分表|完成；实现工作后续分批|15摄影分类、14类Word能力、同文档双模式、WPS与r5全范围|
|Release win-x64自包含|构建完成|RELEASE_IDENTITY、RELEASE_FILES|
|实机启动/导航/物理DPI/视觉|NOT_RUN|MANUAL_ACCEPTANCE|
|WPS实际编辑往返|NOT_RUN；本轮拆分而非实现|PLANNING_IMPLEMENTATION_BATCHES|

## 入口行为

画布上方：选择V、抓手H、缩放Z、颜色取样I、原图比较，以及适合/100%/缩放比例、旧预览模式、撤销重做。取样需要已选颜色范围节点，不偷偷创建空节点。独立RAW/光片不支持旧调整栈取样和撤销，明确禁用；观察工具可用。按住B临时比较、松开恢复；失焦/切源/卸载清除临时状态。显示比例不冒充RAW细节覆盖率。

剩余完整工具栏菜单、折叠、空间/笔触/蒙版/白平衡取样工具不在本次第一段交付内，逐项列明，不用一个PARTIAL代替。未修改其他Grok页面。

## 测试真实性

首次完整WPF发现本地化硬编码门禁1项失败，随后按项目资源约定补三语，复跑完整通过。保留首次失败的摘要及TRX哈希，原始日志在ignored artifacts。最终20项专项包含真实按钮事件、焦点范围、真实像素变化、同栈重开导出、取消/卸载，不能上升为桌面视觉批准。

测试自动改写的三份历史ui-guardian报告已单独保留生成副本，并恢复此工作树原跟踪版本。没有碰其他工作树或用户研究原件。

## 交付文件

DIFFERENCES列出7个源码/资源/测试文件；其他全部为当前证据目录内的Markdown/JSON/CSV。未提交图片、压缩包、数据库、凭据或二进制。发布EXE和所有组件保留在本地artifacts/canvas-release/win-x64，组件身份需以完整RELEASE_IDENTITY为准，而非仅凭EXE文件名。
