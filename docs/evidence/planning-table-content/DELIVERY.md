# Kitao Studio 修复审阅版与摄影表格内容交付

日期：2026-10-14。**REVIEW_BUILD_WITH_OPEN_GATES**。不是全绿、正式发布批准、Windows/WPS验收通过。

## 基线与独立工作树
基线origin/fix/kitao-planning-r7-closure-20261014，经fetch核验仍为1dcbaf03f49be5cf1528acf5688564fef6a450f1。
独立工作树：D:/AI AGENT/kitao-planning-table-content-20261014。
工作分支：feature/kitao-planning-table-content-20261014。
其他工作树、原R7/closure改动和旧Release均保留；未强推、未合并main。没有适用AGENTS.md；已读真实代码、ROOT_CAUSE/TEST_SUMMARY/UNEXECUTED/RELEASE_STATUS和Grok本地对应规格。

## 按顺序完成
1. ca83ee1：独立审阅分类/门禁；严格运行器与原结果不变。完整FQN逐项分类，无通配豁免。
2. 5e1e372：先封存包含DOCX原子替换与画布夹具修复的公司审阅构建；发布组件26项和实际Core身份1项通过，产品EXE未启动。
3. 5c98321：单元格图片/项目符号/十进制编号的编辑、撤销、RTF重开、原生DOCX往返及分页。
4. 355ecbe：明确拒绝跨单元格共享续号/实例级编号覆盖，不静默重启；最终产品源码冻结。
5. ebc68b8 / 1f8f6b6：强化审阅门禁Release身份依赖与缺必需测试阻断；产品src/tests与355ecbe无差异。

## 最终审阅版（使用此目录）
- 产品源码：355ecbe4da09d921ef67ead8cffc05cf28bc1459。
- 完整目录：`D:/AI AGENT/kitao-planning-table-content-20261014/artifacts/table-content/final-review/win-x64`。
- EXE：`D:/AI AGENT/kitao-planning-table-content-20261014/artifacts/table-content/final-review/win-x64/KitaoPhotoSelector.exe`。
- 版本：`2.3.0-table-review+355ecbe4da09d921ef67ead8cffc05cf28bc1459`。
- EXE SHA256：`c381d5b82ec8f88c43796e8a4d78eee4241b346b99a2896a492ef67f0c39f238`。
- Release配置、win-x64、自包含、非单文件；保留完整目录，不只复制EXE。未生成安装器，不提交发布二进制，不自动启动EXE。
- 290个发布文件逐个SHA/大小见FINAL_RELEASE_FILES.json；7个实际产品组件和隔离测试host哈希一致，见FINAL_RELEASE_IDENTITY。

第一阶段修复审阅版仍保留：`artifacts/table-content/closure-review/win-x64`，源码ca83ee1215d421300570e7bb5636477e3055c4eb，EXE SHA256 `c57d8f8b60a3bd7cc4b0b72c22b294f7b74c5a882188995f8aea193b9f52929c`。不是最终表格版。旧R7 Release未覆盖。

## 功能与合成验证
- 图片按当前单元格光标插入；正文/画布快照原有行为保留；可撤销/重做/保存重开。DOCX图片为原生drawing与ImagePart，不是表格截图。合并格图片往返像素逐字节相同。
- 项目符号与编号列表使用真实WPF命令；独立numId、起始序号、表内列表恢复；多组abstractNum顺序通过OpenXML验证。
- 合成24镜/22资源沿用，增加24行三列摄影表格：参考图、三行动作、执行列表；24图、项目符号与3起编号交替。共用分页器得到3页，真实续表头、图片/列表不丢。
- 预览/XPS/图像PDF共用分页器；XPS页数与图片资源检查，PDF明确IMAGE_PDF，不声称可编辑文本PDF或打印机实操。
- gridSpan/vMerge仍原生写读；R6连续策划、R7页面/查找/表格/页眉与两个原失败安全项复验。

## 完整测试与门禁（原始状态不改）
|阶段|通过|失败|未执行|严格状态|
|---|---:|---:|---:|---|
|最终源码完整Core|1682|0|7|FAILED|
|最终源码完整WPF，209类/1616项|1593|0|23|FAILED|
|最终实际发布组件策划专项|32|0|0|PASSED|
|最终实际发布Core完整|1683|0|6|FAILED|
|单独实际Release身份|1|0|0|PASSED|

发现/结果逐项一致，无超时/漏跑/额外测试/缺TRX。严格结果保留NotExecuted；审阅门禁基于2633个唯一完整测试名（含数据驱动用例共享FQN）逐项判定。
29个已核查外部数据、GPU或单独测量/视觉人工条件缺口继续OPEN；原预发布30个NotExecuted之一是实际Release身份，发布后真实Passed满足依赖，不篡改原skip。FINAL-POSTPUBLISH-GATE.JSON中重复出现的Core条件记录来自两轮原始结果，唯一未关闭名仍29个。
新增测试登记为required_automatic；新的未执行项不会继承豁免。实际失败/超时/非零退出/缺TRX/漏跑/额外/缺必需项全部BLOCKED。4项审阅门禁单元测试通过。

## 支持边界
复杂嵌套表格、重复表头+纵向跨行合并、超高单行继续明确拒绝并保留原件。不支持跨单元格共享续号/实例级level override，不能静默改编号。未知列表样式/非段落列表子块及内容控件不伪造支持。详见MIGRATION_SUPPORT。
这是普通文档表格内容能力，不自动把表内自由文字回写摄影结构字段；原有镜头投影表按原合同处理。
Grok入口接真实行为，但不声称像素级复刻；原r5 OPEN差异完整保留。未增加Excel公式/工作簿。
Windows实操、物理DPI、WPS、真实打印机：NOT_RUN。静态截图来自实际发布组件的96DPI离屏渲染。Color Studio PV4/PV5/PV6及历史色彩源码零diff。

## 交付索引
- 分类/门禁：TEST_CLASSIFICATION.json、REVIEW_GATE_CONTRACT、FINAL-PREPUBLISH-GATE.JSON、FINAL-POSTPUBLISH-GATE.JSON。
- 测试：TEST_SUMMARY、TEST_RUNS、原始严格SUMMARY/DISCOVERY、UNEXECUTED_ORIGINAL、TEST_EVIDENCE.zip（包含失败迭代）。
- 发布：CLOSURE_REVIEW_IDENTITY/FILES、FINAL_RELEASE_IDENTITY/FILES。
- 数据/UI：MIGRATION_SUPPORT、VISUAL_REVIEW、R5_DIFFERENCES_PRESERVED、REFERENCE_IDENTITY、offscreen/、synthetic/（原生DOCX、XPS、图像PDF）。
- 清单/复现：FILES_AND_TYPES、SOURCE_AUDIT、FAILURES_AND_REPAIRS、REPRODUCE、EVIDENCE_MANIFEST。
