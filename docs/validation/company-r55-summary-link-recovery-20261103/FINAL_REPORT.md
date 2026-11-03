# 公司 DOCX 失效快照关联和警告汇总续接

记录：2026-11-03T11:18:33+08:00（Asia/Shanghai）。**公司当前可取得代码的两项反馈缺口已复现并最小修复；原四份DOCX独立Office验收仍NOT_RUN；家庭昨晚执行/未推成果状态未知，不能笼统报全部通过。**

## 1. 昨晚任务状态与保全
- fetch origin和source-private成功。开工产品分支两端仍372a2e2334a6d06068df2221d4cb875959e651af，实际产品源码5116daa77fdbf17f705497efc6df5b4cdcabd7a0；公司树开工clean、相对上游0/0。全本地refs、worktree HEAD/upstream/dirty、相关文件跨refs历史和产品reflog已核对。
- 公司可访问代码中，相关导出文件最新修改仍5116daa7；未找到本轮失效关联/延迟汇总的公司未推修复。读取当前大纲/HANDOFF、上一轮报告和可见产品会话，不把会话摘要当作家庭执行收据。
- **产品远端没更新只证明远端身份没变，不证明家庭没工作。** 公司未取得家庭实际任务结果、家庭local HEAD/未推diff、独立Office输出或这两项回归收据；家庭未推成果是否存在仍不可观察。精确缺项见OVERNIGHT_STATUS.json；没有访问/猜测家庭N盘。
- 本轮公司独立处理的是当前可取得基线已实测的缺口，不宣称家庭未实施。后续若提供家庭补丁，须与本轮源码/测试身份比较复用，不覆盖、不盲合。
- 旧工作树和未提交资料原样保留；公司旧灯台27794d2f86313d7231f235b4fd1104eb4e95c106的4独有未推提交及未跟踪docs仍在。不合main、不强推、不整合色彩研究；原快照嵌入、切库恢复和JPEG/ICC归因不重做。旧失败/NOT_RUN/时间不改。

## 2. 原四份合成DOCX的独立Office状态
原四份DOCX SHA与上一轮SAMPLES_IDENTITY逐项一致，原有14页产品读回预览证据沿用，未重新生成四份样例或把产品读回当独立Office。

本机发现D:/SOFTWARE/WPS Office/12.1.0.29161/office6/wps.exe，版本12.1.0.29161；不能沿用“所有Office不存在”的笼统说法。单次wpscli --help探查被工具自身签名门禁拒绝：parent process signature verification failed: parent process is not signed by ZhuHai Kingsoft。GUI式启动未取得可靠exitCode，未把它写成0，也未进行DOCX转换、未绕过签名检查。

随后仅做受控COM渲染的安全前置检查：检测到已有用户WPS PID11928，立即返回NOT_RUN_USER_WPS_PROCESS_ACTIVE，**COM引擎创建尝试0、打开文档0、PDF0**；不枚举用户文档、不接管或关闭原进程，不自动重试。ProgID可解析不等于已确认独立Word/WPS隔离实例；本机无soffice命令，未反复空跑旧render_docx失败路径。OFFICE_STATUS、OFFICE_COM_SERVERS及wps-original-four/WPS_RENDER_RECEIPT保留。

因此四份既有DOCX独立排版验收仍**NOT_RUN（现有用户会话占用，安全隔离未满足）**，不是四份文档排版FAIL。最小后续条件：用户结束现有WPS会话并允许一次隔离只读渲染，复用现有四份固定hash样例和本轮受控脚本；不需要重新开发嵌入。

## 3. 真实导出入口的基线复现
用生产PlanningCenterView.ExportPlanningSummaryFromViewAsync处理器、实际保存路径选择接口和原VM导出链执行；不是鼠标/保存对话框实测。两项基线断言均FAIL，raw/baseline-links-and-warnings.trx保留。

1. 正文PlanningBlockSemantic.CanvasRevisionId找不到revision时，原条件直接跳过替换，旧RTF嵌图继续进入DOCX；灯位ID在投影中变为null，失效关系被静默丢掉。原位无说明、UI汇报0警告，DOCX media2（含失效旧图），见baseline/MISSING_LINK_REPRO.json。
2. PNG延迟解码失败时返回Warnings1、文档原位占位1，但Create阶段已先判断warnings.Count，尚未产生的警告不进入末尾汇总；见baseline/DEFERRED_WARNING_REPRO.json：hasAggregate=false。不是解码安全机制失效，而是汇总生成时机错误。

## 4. 最小产品修复
仅改PlanningReadableSummary.cs、PlanningSummaryDocument.cs、PlanningDocxAdapter.cs：
- 只读汇总投影保留灯位请求的CanvasRevisionId，即使查找结果为空也不丢掉身份，不改持久策划/项目包模型。
- 失效正文在原段落/块位置输出明确的版本不存在占位并加入Warnings；递归清除该快照位置的旧RTF图片，保留前后文字、表格/列表文字和有独立PlanningDocumentImage身份的普通图片。有效关联仍读原冻结PNG，不能用当前画布、源照片或其他版本替代。
- 失效灯位在该灯位条目位置显示同样的说明与警告；有效灯位和参考图行为保持。
- 警告汇总改成末尾的轻量延迟投影，在DOCX已处理所有图片之后读取同一Warnings集合；不提前解码、不重渲染、不一次性收集大图缓冲。UI数量、返回列表、原位说明和文档汇总使用同一事实。
- 实际新反馈页检查发现汇总标题可能落单，补KeepNext保留到第一条警告；没有重画UI或改原图版面。

没有改快照解码预算、v1/v2/v3、画布数学、原固定PNG、当前画布、切库恢复、默认recipe、RAW或色彩研究。没有修改.ptplan v1。导出仍是只读阅读文档，不是可编辑自由画布或跨机项目包。

## 5. 必要验证与证据
最终发布20共享DLL绑定WPF、9共享DLL绑定Core，逐项hash一致。仅选定 **25唯一方法PASS / 25执行，0FAIL/0跳过**：相关导出WPF14＋全壳3/策划15构造1/教程退役2＝20，Core汇总投影4，新增反馈DOCX产品读回1。不是全仓或独立Office全部通过；未重复上一轮完整闭环、切库恢复专项或JPEG/ICC研究。

- 失效关联夹具覆盖嵌套Span段落、真实块级图片、表格图片及灯位；正文前后/表格文字/有效灯位保留。最终只嵌入1张有效冻结PNG，解码像素与保存版本一致；4个失效位置均有说明，UI警告数4，源策划JSON字节不变，OpenXML校验通过。
- 延迟失败夹具返回Warnings1，原图位置1条说明，末尾汇总同一条说明1次。JSON中的placeholderCount=2统计的是“原位＋汇总”两处文本，不是两个坏版本或2项警告；UI仍1项。汇总标题KeepNext断言通过。
- 既有有效正文/灯位定位、自有图片、富文本、表格图、原子失败/已有输出保全等必要回归通过。
- 只对新增两份反馈DOCX做产品适配器读回并查看最终7页（失效关联4、延迟警告3）；占位可读、旧图未冒充、汇总完整、标题不落单。它们仍非独立Office证据。
- 开发阶段一次Block.KeepWithNext类型误用编译失败已修正，原log保留；初版额外表格夹具默认窄列使说明过窄，改用明确600DIP列宽，不为这个夹具修改产品表格规则。初版预览/初次发布目录保留，本次最终source/测试/发布身份另分。

## 6. 完成 未执行 缺项
|项|状态|边界|
|---|---|---|
|公司失效关联反馈|完成并回归验证|当前公司基线实测修复，不声称家庭此前未做|
|延迟警告与汇总一致性|完成并回归验证|原位/返回/UI/汇总一致，无重复计警告|
|新增反馈DOCX产品读回|完成|7页已查看，不替代独立Office|
|原四份独立Office渲染|NOT_RUN|用户WPS占用/安全隔离不满足，未反复重试|
|家庭昨晚执行及未推成果|UNKNOWN/缺回执|需实际HEAD、diff、产物与测试/渲染收据，不以远端未更新推断|
|鼠标/DPI/多屏/ACM/HDR|NOT_RUN|禁用ComputerUse/Browser/WebView/CDP未启用|

整体PARTIAL_DIAGNOSTIC_OFFICE_NOT_RUN_HOME_OVERNIGHT_UNCONFIRMED，不宣称整个策划中心验收完成。

## 7. 交付身份
- 最终产品源码 **3bf5d9a6a10124793434a43d782b22978d8707d0**；最终测试支持 **73c3f33fe988697c1c0277e2cf02bdc490f7252e**。初次修复65a53774/测试50a59972及其构建/预览保留，最终包只绑定最终身份。
- 完整自包含win-x64：E:/Guangjing-Summary-LinkRecovery-20261103/Guangjing-R55-SummaryLinkRecovery-Final-Diagnostic-win-x64.zip；294文件，ZIP79,719,434B，SHA256 **B537F368065C016F975AB341F7F85DD9A19933EDCBFC4CAE46885442AED3CF56**。目录/ZIP逐文件SHA读回通过。
- 同名目录Start-Guangjing-Diagnostic.cmd启动，固定相邻UserData-SummaryLinkRecovery隔离根保存重开，当前进程可指定绝对GUANGJING_DIAGNOSTIC_DATA_ROOT，勿多实例写同根。
- WholeAppDiagnosticBuild=true / PhotographerTrialBuild=false；全模块、10主导航、策划15、教程退役保留，无单模块包/Setup/私人素材/研究引擎。实际CMD/EXE启动标题、产品SHA日志与WM_CLOSE均通过，EXE0/CMD0，仅OS烟测。
- 大纲/HANDOFF按Asia/Shanghai追加；历史报告和失败收据不改。最终LOCAL_HEAD/REMOTE_HEAD另记REMOTE_READBACK，文档提交不冒充产品源码身份。
