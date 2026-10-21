# 策划中心15区汇总DOCX产品交付

2026-10-21，Asia/Tokyo。**诊断版，不能发行**。本批次以产品功能为主，实体桌面验收另列NOT_RUN。

## 起点与安全

fetch并独立ls-remote确认公司续接分支仍为25b3087a5213738e781bf428f36f22255fed3f08，无新增提交。新独立工作树 E:/Kitao-Worktrees/planning-summary-docx-r2-20261021，分支 company/planning-summary-docx-r2-20261021。上轮未提交的7个实现/测试文件只读复制并逐hash记录，未覆盖上轮树；最终所有旧树HEAD/脏状态、这7文件内容及旧诊断ZIP哈希保持，PRESERVATION.json可查。不合main、不强推、不reset/stash/clean/rebase；家用Scenario11原说明零diff，未把公司耗时当家用成绩。

## 产品功能已实现

产品提交 **d1a77be4450c5a2cbb3c09b47580b9841280a14f**。在同一PlanningCenterViewModel/PlanningDocument/PlanningResources/ProjectShot和已有保存门禁上建立只读投影，无新数据库、无存储迁移、无第二份策划状态。

入口：策划中心打开项目 -> 右上“更多策划操作” -> **导出15区汇总 DOCX** -> 原生保存路径选择。取消不写文件；保存未完成时可见错误；导出成功明确阅读快照/外链未打包；异常保留原资料与旧输出。新包为完整MainWindow，非暗室专用宿主。

15区按现有顺序：项目概览、文字、镜头清单与分镜、参考图、情绪板、灯位图、服化道与道具、设备、人员、场地、档期、当天时间表、预算、文件、文档内容。标题始终15个，空区写“本区未填写内容”；已有字段和ID保留，资源与引用按存储次序，镜头按界面实际Order排列。正文复用支持的持久RTF/自有图片/基础表格，不降成只有标题；CustomBody作为文档内容，已共用正文不伪造第二正文。预算读同项目实际支出（只读）；读取失败明确写未使用旧缓存，币种不混算。

外链/附件本机存在、缺失、URL/库引用未解析、重解析引用等状态明确输出；仅引用、不自动复制，不称跨机便携。已有文档自有图片可用时嵌入；缺失/变化时在原位置写图片ID/标题与错误类型并记录警告，图注/顺序/位置/Hash另列。陌生/不支持资源类型或旧格式能力不支持时拒绝，不悄悄丢弃。

原摄影DOCX ExportPlanningDocxAsync/Preview/Apply导回和PlanningProposalPdf实现均零改动。汇总没有KitaoShotTableV1往返标记，导回只能按原普通正文合同处理，不宣称15区双向编辑/复杂Word/WPS兼容。页面大小/页眉页脚沿当前支持的布局；不扩大成完整排版工程。

## 本批修复的具体问题

1. 汇总镜头原先沿集合次序，可能不同于实际表格Order；改为稳定按Order投影，不重排源集合。
2. 灯光/造型参考在参考区有记录，但未在对应灯位/服化道分区列出；按已有Category重复引用同ID，不复制源文件。
3. 仅在镜头关联的参考会进入生产AllProjectReferences，但旧投影仅看_document.References；改用同一文档优先+镜头参考聚合/按ReferenceId去重，避免遗漏。
4. 补齐保存失败的UI反馈、读取期间项目/正文/概览变化拒绝覆盖、跨项目文档/镜头/账本隔离和外部引用直接覆盖保护。未改既有存储或像素算法。

## 本轮测试与样例

|组|程序集发现|本轮选择/取得|通过|失败|跳过|漏项/额外|
|---|---:|---:|---:|---:|---:|---:|
|最终受影响Core|1721|25/25|25|0|0|0/0|
|最终策划WPF相关回归|1720|69/69|69|0|0|0/0|
|其中新增Core|1721|4|4|0|0|0|
|其中新增WPF|1720|5|5|0|0|0|

最终测试加载实际发布包原样DLL并hash匹配，FINAL_BINDINGS.json。按adapter发现与原TRX逐(testId,FQN,displayName)核验；不是源码计数。原始TRX/日志/发现/执行计划见RAW_TESTS.zip。范围是**受影响策划/导出回归**，本轮没有完整重跑全Core/全WPF，不继承旧全量成绩。UI测试通过真实视图方法/对话服务与生产存储逻辑，不是实体鼠标。

已完成合成项目创建/真实保存 -> 富文本、镜头、七类资源、档期、引用、实际支出 -> 汇总DOCX -> 新VM重开 -> 再导出逐文本一致；原planning JSON、外部图、附件hash不变。缺失外链/附件/自有图片、空区、账本读失败、输出失败、取消及旧文件保留均有断言。首轮本批Core4/WPF5通过，审查后新增聚合修复又重跑；上轮未完成测试历史不当作本批成绩。

实际导出样例：samples/synthetic-15-section-summary.docx、samples/synthetic-missing-reference-summary.docx。直接打开OOXML核对15标题顺序、全部合成内容、1个嵌入图片、缺失说明，OpenXML validator零错误；重新打开项目再次导出内容一致。SAMPLE_INSPECTION.json和samples/identity.json记录身份与hash。

DOCX回读后用产品共享分页器生成9页图片式版面审阅PDF，逐页联系图目视检查中文/内容/边界；这是**文档渲染，不是EXE桌面截图或Word/WPS实体打开通过**。独立LibreOffice/WPS渲染不可用/未运行，不能据产品回读声称全互通。没有偷偷省略不支持格式；后续复杂Word功能仍原范围。

## 中文清单已修正

MANUAL_DESKTOP_CHECKLIST.pdf为单页A4，使用嵌入SimHei TrueType子集（FontFile2）和ToUnicode，不再仅依赖未嵌入CID替代字体。逐字符字形覆盖检查零缺字，渲染目视确认中文可读无方框；CHECKLIST_FONT_VALIDATION.json含字体文件hash（字体原文件不入Git）。

交包前版本S07=NOT_READY保留在manual-prepackage；最终清单明确“使用本批次新包验证”，并写“交包前NOT_READY / 交包后未执行NOT_RUN”。本批功能包具备该入口，但清单本身不等于实体操作已执行，旧诊断包没有此入口。

## 新诊断包

Planning15Summary-Diagnostic-win-x64完整Windows x64自包含，290应用文件，264声明运行依赖全部存在。双击Start-Guangjing-Diagnostic.cmd，隔离UserData；ZIP不含UserData/数据库/私人照片/RAW/凭据。仍是诊断版，不是Setup。源码、构建命令、EXE/WPF/Core hash见BUILD_IDENTITY.json、RELEASE_FILES.json，完整ZIP逐文件读回身份见ZIP_IDENTITY.json。

## 实体桌面与剩余缺陷

本批不再进行Computer Use输入或截图排查，上一轮用户Escape停止后没有新增鼠标验证。双库切换/策划15区进入保存重开/缩放点击均NOT_RUN，保留此前几何不可用/捕获超时记录作为已知阻断，不写PASS。简短人工清单随包交付。

仍未完成：Windows实体点击、Word/WPS实机、跨机外链/附件/数据库完整便携、15区双向DOCX导回、完整情绪板/灯位可编辑版面导出、复杂分节/表格等原支持范围、Setup安装升级卸载与真实相机/外部服务。现有资源有内容汇总，不代表独立画布引擎版面可编辑交付。家用32139.0909ms问题仍待原说明在家执行，未改阈值/循环。

发行门禁BLOCKED，整体PARTIAL；本次最小汇总功能在合成自动范围通过，不能标正式发行。产品提交与证据提交分开，正常推送独立分支后ls-remote读回。

## 推送读回

产品提交d1a77be4450c5a2cbb3c09b47580b9841280a14f与证据提交c9922c42b5f04e64d3eecd07f88c56eade4dea5b已正常推送，独立ls-remote本地远端一致且工作树干净。后续仅封存此读回及最终ZIP身份，不改变产品组件。
