# 策划中心 r5 实施拆分表

本轮只交付拆分表，不声称已实现双模式或完整 Word。基线为 3c5a5ff；Grok 51b3f4f 的支持标签不是验收依据。以下所有批次保留在总范围，按独立可验收提交推进。人日为单名熟悉代码的工程师含自动测试的粗估，不是工期承诺；桌面与 WPS 人工验收另排。

## 当前代码证据索引

所有路径位于 N:/pixart/kitao-canvas-toolbar-20261013。

|代号|实际代码与限制|
|---|---|
|P1|src/RAWSelectionAssistant.Core/Services/Projects/ProjectShotExecution.cs:40-68：ProjectShot 有身份、顺序、名称、状态、Notes、References、EstimatedMinutes、Scene；无景别/机位/镜头/道具/灯光独立字段。Catalog 只有 Version，无并发 Revision。|
|P2|src/RAWSelectionAssistant.Core/Services/Projects/PlanningDocument.cs：Title/Subtitle/Body/CustomBody/Status/Location/People/References/Attachments 与 Extensions。无 PlanningProp/Gear/Crew/Venue/Schedule/Budget 强类型结构。|
|P3|src/RAWSelectionAssistant.Core/Services/Projects/PlanningCenter.cs：Summary、BookingId、PlanningCanvasRole.Lighting、CapturedAssets 与 Revision；有链接基础，不等于灯位编辑器。|
|P4|src/RAWSelectionAssistant.Core/Services/Projects/PlanningDocumentPackage.cs：DocumentId、ProjectId、RTF、ShotIds、CanvasRevisions；快照与引用已有，双模式同步未实现。|
|P5|src/RAWSelectionAssistant.Core/Services/Projects/PlanningRichLayout.cs：BlockIndex/Style/PageBreak/CanvasId；缺 BlockKind/SourceIds、分节、页眉页脚、复杂Word语义。|
|P6|src/RAWSelectionAssistant/Views/PlanningRichEditor.cs:26-75：FlowDocument+RTF、RichDocumentHistory、标题/基础格式/表格/图片/页面设置、350ms保存；DOCX导入有损失确认。无完整Word功能区和结构化复合撤销。|
|P7|src/RAWSelectionAssistant/Services/PlanningDocxAdapter.cs：OpenXML受限导入导出；合并单元格、嵌套列表等拒绝或降级；页眉页脚、域、修订不完整；报告A4但导入仍取源pgSz。|
|P8|src/RAWSelectionAssistant/ViewModels/PlanningCenterViewModel.Document.cs：待保存文档/镜头、FlushAsync、恢复草稿、RTF存储；缺预期Revision并发与完整三方合并。|
|P9|src/RAWSelectionAssistant/Services/PlanningProposalPdf.cs：图片式PDF；src/RAWSelectionAssistant/Services/PlanningDocumentLayout.cs：FlowDocument布局。不是文本PDF或完整Word分页。|
|P10|src/RAWSelectionAssistant/Views/PlanningCenterView.xaml.cs：已有编辑/预览/导入导出和搜索接线；没有规范所述完整双模式导航。|

## 分批开发：简单版、身份和同步基础

|批次/项目|当前证据与缺口|依赖|验收用例|预计人日|
|---|---|---|---|---:|
|P0 数据迁移/保存门禁|P1-P5；扩展结构版本、稳定对象ID；P8失败异常需归一|无|旧文件往返未知字段不丢；空镜头/IO失败不切页；关闭前flush|4-6|
|P1a 项目概览/文字/文件/文档内容|P2-P3已有字段；CustomBody并入内容索引|P0|15分区路由；正文/附件重开；引用原件只读|2-3|
|P1b 镜头清单与分镜|P1无景别、机位镜头、人物动作与备注拆分；11列及自定义列|P0|旧Notes迁动作；排序/复制/归档；列增删重开；ShotId稳定|6-9|
|P1c 参考图/情绪板|P2 References/IsMoodboard/Group已有；需分类入口|P0|同素材多引用不复制客户照片；分组、缺图重定位、保存重开|2-3|
|P2a 服化道与道具|P2缺PlanningProp；状态/来源必需|P0,P1b|待准备/已备齐/待确认/已约；自有/租借/购买/现场/模特自备；与镜头关联|3-4|
|P2b 设备|P2缺GearItem，工具箱导入非现有接线|P0|手动设备与工具箱导入去重、来源追踪、关联镜头|2-4|
|P2c 人员|P2仅People文字；需CrewMember|P0,P1b|角色/联系方式/到场时间/ShotIds；旧文字不丢|2-3|
|P2d 场地|P2仅Location文字；需Venue/许可/时段|P0,P1b|主场地迁移、多个场地、地址与镜头关联、外部地图需明确用户动作|2-3|
|P2e 档期与当天时间表|P3 BookingId已有；ScheduleSlot缺失|P0,P1b|档期日期改动、跨日时间、镜头时长、负责人；手动覆盖保留|3-5|
|P2f 预算与实际收支|P2缺BudgetLine；r5保留收支联动|P0|预算不覆盖账本；按项目读取实际支出；权限/离线/重复刷新|3-5|
|P2g 灯位图|P3 Lighting与P4快照已有；无灯具元数据/编辑器|P0,P1b|位置方向功率修饰器、撤销、画布快照、重开、引用原图|5-8|
|P2h 地图/日落/潮汐|P2/P3无服务合同|P2d,P2e|地点日期时区；超时离线手动值；来源/许可/缓存；不静默替换|3-5|
|P3a 同文档双模式|P4身份已有；缺LastMode/全局默认、块SourceIds|P0,P1,P2|切换先flush，失败不切；同ProjectId/DocumentId；按文档优先于默认|4-6|
|P3b 摄影模块块与复合撤销|P5缺BlockKind/SourceIds；P6历史只存RTF+布局|P3a|同ShotId字段双向改；删除块不删镜头；取消零写入；复合一步撤销|7-10|
|P3c 首次提案与模板|P6基础文档已有；缺摄影模块模板|P3b|首次自动排版可一步撤销；再切换不重生；6类摄影模板与自定义模板|3-5|
|P3d 文档内容保真|P2 CustomBody与P4RTF已有；无自由块索引|P3b|普通段落/表格/图片保留位置与格式；解除结构确认；往返不丢|3-4|

## 进阶版 Word：全部保留，逐个功能验收

7个功能区页签为文件、开始、插入、布局、引用、审阅、视图；不是一次提交全部重排。

|批次/功能|当前代码证据/缺口|依赖|验收用例|预计人日|
|---|---|---|---|---:|
|P4a 基础文字/功能区|P6可复用标题、格式和表格；字体字号颜色等入口不全|P3|选择范围操作、列表、行距、字数、键盘焦点、RTF往返|3-5|
|P4b F-WORD-003 页眉页脚页码|P5/P9无统一模型；PDF旧固定页码不能叠加|P3|首页独立、奇偶页、起始页码；预览/打印/图片PDF/DOCX四路径一致|4-6|
|P4c F-WORD-007 查找替换及校对|P10 Ctrl+F目前列表搜索；结构化替换缺复合历史|P3b|正文焦点查找；否则搜列表；全字/大小写/跨run；替换Shot并一步撤销；拼写字典及许可|4-6|
|P4d 表头重复与视图|P6基础表格有；P9分页缺统一表头|P4a|跨页重复表头，预览/打印/PDF/DOCX对应；缩放不改文档|2-3|
|P5a F-WORD-001 样式与模板|P5只接受Body/Heading1-3|P4a|样式继承/应用/更新；旧样式迁移；保存重开|3-5|
|P5b F-WORD-008 合并/嵌套表格|P7当前不完整或拒绝|P4a|gridSpan/vMerge、拆分、表内图片/嵌套表格、往返无静默丢失|4-6|
|P5c 嵌套列表多级编号|P7当前降级/拒绝；D-PL-21要求保留|P4a|numbering/ilvl、起始号、嵌套项内非段落块、DOCX重开|3-4|
|P6a F-WORD-002 分节|P5/P7无完整分节模型|P4b|按节方向/页边距/页眉页脚；连续/下一页；迁移和撤销|5-8|
|P6b F-WORD-009 图文环绕与浮动|P6仅基础图片块|P6a|锚点与文字重排、页边界、尺寸/裁图、DOCX导入损失对照|4-7|
|P6c F-WORD-010 域与公式|P7缺保真域/公式|P6a|域更新不执行外部代码；保留原表达式；数学表示往返|4-7|
|P6d F-WORD-004 目录与交叉引用|P6仅标题大纲|P5a,P6c|更新目录页码、镜头引用重排、断链提示、导出再导入|3-5|
|P6e F-WORD-005 脚注尾注题注|P5/P7缺模型|P6a,P6c|连续编号、删除/重排、引用恢复、跨页分页|3-5|
|P7a F-WORD-006 批注/修订|P7默认丢失并报告；无协作模型|P6|批注回复/解决，修订接受/拒绝，作者时间，WPS往返|6-9|
|P7b F-WORD-011 邮件合并|P6/P7无数据源或模板合并|P6c|绑定字段、预览、缺值、批量输出、取消、路径安全|3-5|
|P7c F-WORD-012 权限保护|P4有包校验，不是Word文档权限|P7a|只读/区域编辑、保护状态导回；清楚区分UI限制和加密|3-5|
|P8a 版本历史/恢复点|P8有草稿，非可浏览版本历史|P0,P3b|恢复前保存当前版本；差异；另存新策划；快照不可变|3-5|
|P8b 外部修改/冲突副本|P3有Revision，P1无；缺expectedRevision|P0,P8a|双进程、自写watch过滤、temp+rename、冲突只写草稿；新PlanId副本|4-7|
|P8c 三方逐项合并|P8无字段级合并|P8b|基准/本地/磁盘逐字段选择；镜头/Extensions；复合撤销|4-6|
|P9a 文本PDF|P9目前图片式PDF|P4b,P4d,P6|可选择文字、字体子集、分页/页眉/表头一致；旧图片PDF保留|4-7|
|P9b F-WORD-013 高保真往返|P7受限；RTF不能承载全部Word语义|P5-P8|未知OOXML安全保留/显式损失，关联关系稳定，外部资源不自动取回|6-10|
|P9c F-WORD-014 兼容性与分页基准|P7损失字符串不是兼容性矩阵|P9b|WPS版本/字体/分页差异；每项原生/转换/丢失/拒绝；用户确认后写入|4-7|
|P10 DOCX结构回写|P7普通表格导入，不回写Shot|P3b,P8a,P9b|表头+镜号/稳定标记识别；逐字段差异勾选；恢复点；一步撤销；重复导入幂等|8-12|

## WPS 往返单独门禁

1. 使用合成/获授权测试文档，记录本机 WPS 完整版本、字体、系统/DPI、输入输出SHA。
2. 光镜导出：段落/样式/列表/合并表/图片/分节/页眉页脚/批注修订/摄影块逐项 fixture。
3. 用户在 WPS 打开、实际编辑、保存，不可用纯OpenXML读写代替这一步。当前 NOT_RUN。
4. 导回先建不可变恢复点；扫描不支持项和安全限制；报告原生保留、转换、有损、拒绝四类；取消零写入。
5. Letter纵向、A3横向、A4三例核对A4转换及方向、页边距和提示一致；目前 P7 文案/行为有差异，不能宣布已支持。
6. r5 D-PL-22：本期总范围包含结构回写，不沿用“第二版再做”；差异预览确认才改镜头，支持一步复合撤销。
7. r5 D-PL-23：载入磁盘前以新PlanId保存冲突副本、刷新列表；不得只留不可发现文件。
8. 再切回简单版：自由文本、表格、图片全部在文档内容保留；格式不显示不等于格式丢失。

## r5 范围与决定纠正

D-PL-17/21-26保留：预算读取摄影收支、设备导入、地图/日落潮汐、全部Word扩展、嵌套列表无损、结构回写、冲突副本、16项完整交互、首页独立页眉。手机APP与浏览器扩展仍不在本期（D-PL-25）。每批必须提供数据迁移、真实处理、保存重开、取消/失败、导出和人工验收证据；无实际执行的一律NOT_RUN。估算按各行相加141–223人日，存在交叉依赖和Word引擎选型风险，不能照抄Grok约139-185为当前承诺。

