# 公司 R5.5 策划汇总 DOCX 固定画布快照

记录：2026-11-02T17:04:32+08:00（Asia/Shanghai）。**本轮阅读交付改进完成，自动回归和产品读回布局通过；独立Office排版验收未完成，整体仍PARTIAL_DIAGNOSTIC_NOT_FOR_RELEASE。不是整个策划中心全部验收。**

## 1. 接续与保全
fetch origin成功，产品线无新增提交，基线6e01f26f2fd491155aad3ccb4b2c8dac604030bf/产品89b64fd220bcb806065a13a061f0c392a2fdb050。读取AGENTS、开源先行POLICY、R55大纲、HANDOFF和company-r55-workspace-recovery-20261102报告。实际公司树E:/Kitao-Worktrees/r55-workflow-company-20261102，分支home/r55-canvas-photo-preview-20261101。

开工refs/worktrees/dirty已记录；旧树、未提交资料、失败收据和公司旧灯台27794d2f的4独有未推提交原样保留。研究分支只fetch获知，不整合；不合main、不强推、不reset/clean/stash。上一轮保存恢复src未改，仅沿用必要回归。

## 2. 缺口实测
真实入口是策划导出菜单“导出15区汇总 DOCX”→PlanningCenterView.ExportPlanningSummaryFromViewAsync→PlanningCenterViewModel.ExportPlanningSummaryDocxAsync→PlanningReadableSummaryBuilder→PlanningSummaryDocument→PlanningDocxAdapter。

基线用已实际保存的两版CanvasRevision生成DOCX：解包media0、drawing0、图片关系0，尽管原PNG有效且源照片离线。原始断言FAIL见raw/baseline-missing-snapshots.trx，检查baseline/DOCX_INSPECTION.json。DOCX由产品适配器重新导入并WPF分页，查看第3页只列两版修订ID/画布ID/时间和“元数据引用”，没有拼贴。baseline/metadata-only-two-revisions.docx与缺图页PNG保留，不篡改原收据。

## 3. 实现范围和只读保证
- 仅使用已保存revision.PreviewPngBase64；不调用Canvas渲染器、不读取EditableSnapshot.SourcePath，不访问原照片或当前画布。测试将当前画布保存为后来修改的空对象版、保持源照片不存在，导出仍为两份历史PNG；策划JSON、当前画布JSON、旧Base64及revision集合前后不变。
- 无明确关联的revision沿原保存集合顺序，在“15 文档内容—已有画布快照”逐版显示图片、版本序号、版本ID和保存时间；不按时间重新排序、不猜测情绪板归属，不新增可编辑画布工程。
- 检查发现模型已有明确关系：PlanningBlockSemantic.CanvasRevisionId及PlanningLight.CanvasRevisionId。正文显式段落位置以该revision冻结PNG替换RTF旧嵌图，灯位显式关联在06灯位图呈现；15文档内容保留全部版本索引并说明已在明确位置呈现，不再无意义重复嵌图。只作用于导出投影，不回写原RTF/布局。
- 复用现有安全PNG解码/页尺寸与DOCX图片编码；保持比例，最大宽min(520,有效页宽)、最大高min(360,有效页高×0.42)，不放大超过原像素、不裁切/拉伸。图注KeepWithNext与图片成组，内部SHA不堆进新增快照正文。
- 缺失、无效Base64、非PNG、损坏PNG、16位/动画PNG和尺寸/编码超预算在原位置产生清楚占位，同时进入返回Warnings；不取别版/当前画布替换。损坏Base64转中文原因。DOCX写入错误本身不被误吞成占位，仍失败并清理staging。
- 原15区、富文本、参考图片和普通图片行为保留；DOCX仅阅读，不是可编辑自由画布/跨机项目包；.ptplan v1源码/协议不改。

## 4. 内存与安全边界
新固定快照出口限32MiB Base64字符、24MiB解码前编码字节；33字节最小PNG，8位静态单帧，最长边16384、4194304总像素，16B/像素保守工作估算64MiB（不含编码输入/编码器/完整进程）。当前v2/v3最多1400边合法快照处于范围内；老v1异常超范围有显式占位，不静默降精度。

所有快照先仅存轻量投影；适配器逐个读取PNG→编码入ImagePart→解除Source引用，返回前不把全部版本解码bitmap留在FlowDocument。8版小夹具验证创建投影和写出后所有slot.Source均null；没有一次性为每版创建大图缓冲，没有新增跨任务图像缓存。GC/OS回收时间不可保证，不宣传整个进程硬内存上限。画布原256MiB预算与渲染v1/v2/v3未改。

## 5. 最终证据
产品源码5116daa77fdbf17f705497efc6df5b4cdcabd7a0；测试支持c5222046aa7705ccf1adcd93b44e2fa37b91b518。WPF host绑定完整发布20共享DLL、Core host9共享DLL逐项SHA一致。

权威发布回归 **58唯一方法PASS / 58执行，0FAIL/0跳过**：WPF主集合45（新DOCX6、既有汇总/图片10、上一轮恢复7、画布快照保全16、全壳3、策划15构造1、教程退役2），另一进程恢复/旧PNG重开2＋导出页渲染1，Core汇总/ptplan10。TEST_SUMMARY列精确方法，不把重复开发试跑加入唯一数，不是所有历史测试通过。

具体覆盖：
- 已保存v3横向透明PNG与v2竖图，输出2 ImagePart/2 Drawing，图片关系一一指向资源；解码BGRA像素逐项相等，包括alpha，不要求PNG容器字节相同。15个Heading1保持，版序/图注ID/时间正确。
- 当前画布后来修改和原照片离线仍读取冻结版本；策划/画布JSON、旧PNG不变。
- 原位4种异常占位+Warnings、已保存v3截断PNG、16位拒绝及Base64长度提前拒绝。超出模型32MiB保存合同的编码通过直接只读投影验证，不伪造可合法存盘状态。
- 窄页实际500×700/边距80,100,90,110，图片尺寸和比例不越有效宽高；8版懒解码写出不留图像Source。
- 明确富文本02和灯位06位置精确使用两版冻结像素，不用RTF原嵌图冒充revision；15索引保留，media只有2张。
- 已有DOCX、目标为目录、staging失败保留原输出/无tmp；读取账本期间同项目revision变化和切换项目拒绝输出，不串项目。沿用既有富文本/参考图/表格图、项目包原范围及上一轮保存失败恢复保全测试。

开发失败仍保留：新测试窄页布局构造漏参数编译失败；异常夹具最初未先保存策划导致Append拒绝；首次保全命令漏KITAO_PLANNING_CANVAS_ROOT导致1配置FAIL，补齐隔离根后24通过。基线media0是真产品缺口，其余这些是夹具配置错误，不能混为产品故障。raw日志/TRX不删除、不放宽断言。

## 6. 实际渲染与人工逐页检查
四份最终可分发合成DOCX：samples/saved-two-revisions.docx、snapshot-placeholders.docx、narrow-portrait.docx、explicit-placements.docx。产品适配器导入实际ZIP内DOCX，恢复OOXML真实纸张/边距后WPF分页；逐页打开14张PNG（3+3+5+3）检查：图片比例/透明背景/分页/图注/边距正常，无裁图、拉伸或覆盖正文。见preview和VISUAL_REVIEW.json。原底层DOCX容器验证OpenXmlValidator通过。

按documents技能实际调用render_docx.py，但本机无soffice.exe，canonical-render-baseline.log保留FileNotFoundError；已有Kingsoft目录仅wpscloudsvr.exe，Office目录无可用Word。未联网安装/擅启Browser/ComputerUse。产品导入回读会报告既有keepNext未知属性和A4转换诊断，本测试随后从OOXML恢复实际纸张；不把产品读回当独立Word/WPS/LibreOffice排版签收。

NOT_RUN：独立Office/团队终端打开签收、真实鼠标导出/多DPI/多屏/ACM/HDR、私人项目。合成样例无私人照片/数据库；没有JPEG/ICC归因、六图研究或20MP。原数值FAIL和历史NOT_RUN保持。

## 7. 完整诊断包与启动
- E:/Guangjing-Summary-Snapshots-20261102/Guangjing-R55-SummarySnapshots-Diagnostic-win-x64.zip：294文件/191,971,594B，ZIP79,711,795B，SHA256 **81B2F834DDB2D37BDFAB3A7078540767BF615F39CB6DBF095F74C38FBDCACD5F**；目录和ZIP逐文件流hash读回PASS。
- 同名目录Start-Guangjing-Diagnostic.cmd启动；固定相邻UserData-SummarySnapshots隔离根可保存重开，当前进程可指定绝对GUANGJING_DIAGNOSTIC_DATA_ROOT，不多实例写同根。
- WholeAppDiagnosticBuild=true / PhotographerTrialBuild=false，全模块/10主导航/策划15/教程退役保持，无单模块、Setup、私照/用户数据库或研究程序入包。合成DOCX与预览在源码证据目录及本地样例，不混入应用用户数据。
- 实际CMD/EXE启动标题、日志ProductSourceSha读回、WM_CLOSE均通过，EXE0/CMD0，仅OS烟测不冒充鼠标流程。文档最终HEAD/REMOTE另记REMOTE_READBACK；产品、测试、包身份不混用。新时间Asia/Shanghai，历史收据不改。
