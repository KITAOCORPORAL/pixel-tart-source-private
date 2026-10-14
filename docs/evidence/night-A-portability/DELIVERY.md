# 夜间A最终交付

基线远端13b0efe689c0114f6f76b70c59a1059cabeddbef核验一致；本地更晚ec20c77保全并在独立A树继承。B在A新增源码前已从13b0efe独立建立，共同祖先13b0efe，未自动合并A修改。

A本轮源码ed1280f031ca8772a9bdda1b938c392ecccd31bf；两阶段912fa82/ed1280f扩大全放置、JSON-only、junction及RichEditor历史清单测试。上轮搬迁实现/失败/受阻push/性能BLOCKED证据全部继承，未改旧报告；本轮严格复验不等把旧运行改绿。

实际实现：受控相对路径+精确文档/图片ID/hash、旧绝对兼容与安全迁移、原根撤走的JSON+images工程搬迁；缺图/篡改明确图片标题和ID；无猜图。封面/情绪板/指定镜头/表内/同图多次，模式/分页/两种历史/DOCX五ImagePart保存重开；只搬JSON拒绝；junction测试拒绝越界；主锁原字节/草稿重放。清单排除正式/VM undo-redo/RichEditor undo-redo/草稿/DOCX恢复点，始终DeletionAllowed=false。

|测试|实际结果|
|---|---|
|A1 Core专项|4Passed，无skip|
|A2 文档/搬迁专项|12Passed，无skip|
|完整源码Core|1686Passed/7NotExecuted，共1693，strict FAILED|
|完整源码WPF|1602Passed/23NotExecuted，共1625，strict FAILED|
|实际发布Core完整|1687Passed/6NotExecuted，共1693，strict FAILED|
|实际发布WPF专项|28Passed（精确6类），无失败/漏跑|
|发布Core身份|1Passed|
|审阅门禁|errors0，29独立条件仍OPEN，REVIEW_BUILD_WITH_OPEN_GATES|

上轮必需素材库性能失败保留历史；本轮严格原门槛未改，无真实Failed/timeout/nonzero，源/发布Core顺序执行。不是全绿正式验收。代码安全约定见继承PlanningImageStorage合同及A1/A2，JSON扩展/旧RTF不批量重写、身份不变。

Release完整目录：N:/pixart/kitao-night-A-portability-20261014/artifacts/A-review-release/win-x64
EXE KitaoPhotoSelector.exe，源码ed1280f，ProductVersion 2.3.0-night-A-review+ed1280f031ca8772a9bdda1b938c392ecccd31bf。
EXE SHA256 D587A18B68AF5FA356A7CF81962A333EABA9E48AD4AE4B03CD9B5B113CDA3544。
组件DLL与测试host逐项hash相同，完整文件清单见RELEASE_FILES，不复用旧包。发布二进制不提交。

RAW_TEST_RESULTS.zip为本轮原始discover/TRX/runsettings/stdout/stderr；samples为合成迁移前后结构（根前缀脱敏，ID/hash不改），合成DOCX/PNG留artifacts。其他树与研究原件不动。Windows真实启动/临时收集/选图/保存重开/导出/物理DPI/WPS NOT_RUN，Computer Use限制不绕过。

仍OPEN：跨文件ACID未实现、无自动GC、外部条件/人工门禁、完整Word/WPS/Grok视觉与跨库自动发现；只读报告不是可删文件批准。旧峰值失败的性能根因仍未证明，只记录本轮不同运行结果。
