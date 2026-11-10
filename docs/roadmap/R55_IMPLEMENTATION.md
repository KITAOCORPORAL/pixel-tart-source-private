## 2026-11-10 在线选片收敛与Safari待验（Europe/Berlin；Asia/Shanghai同日）
- 整合分支integration/online-selection-v1-20261110，从8e447075完整桌面基线选择性移植c36e1f38确认回执校验/丢响应恢复，两原分支均保留。最终产品7769b8ee63ad46cd22efb154145766c45b34ba69；c8a2f5cb仅测试支持，后续文档不改src。
- 客户GET增加持久化confirmation，字段imageId/selectionAssetId明确分工；错误项目HTTP200拒绝、真实提交后丢响应可读回有效确认、有效回执不依赖后续刷新。Core无HTTP/凭据原断言保留并通过。
- 实际重开导航暴露原SQLite丢反向选片ID，schema8加法迁移和唯一绑定已修；最终实际PC重开后返回同一归片项目。回归222PASS（Core183/WPF39），加原生6阶段共228所选TRX PASS；四JS真实HTTP故障PASS，最终EXE/CMD启动退出0。旧三项schema断言更新并单独复验，不抹旧失败。
- 独立HTTPS手机相册80d225b5-1644-4842-936e-85abb13172f4已发布，同版服务/JS字节绑定；页面/授权API/JPEG200，未授权401，交接前0选/未确认/未锁定。私链仅本地PHONE_CONTEXT.private.json和phone01-app-evidence/SHARE_LINK.secret.txt，不进Git/包。待用户iPhoneSafari两步反馈后，collect_phone_feedback.py再同版PC收回、独立重开并出最终回执。当前READY_FOR_IPHONE_SAFARI_AWAITING_USER，手机后PC收回NOT_RUN；浏览器扩展继续等待。
- 全模块包E:\Kitao-Deliveries\online-selection-converged-20261110\Guangjing-OnlineSelection-Converged-Final-Diagnostic-win-x64.zip；851文件逐项/ZIP读回，SHA256 508375E57896BAB0F3761C0430FD580E1A15EEE826FF7F148EB3F52274F929DE。WholeAppDiagnosticBuild=true/PhotographerTrialBuild=false，全模块/策划15/教程退役，无Setup，RAW调用0/native不换。旧8e/c36及181候选失败保留。
- 当前报告docs/validation/online-selection-converged-20261110/PRE_PHONE_REPORT.md；本地证据E:/Kitao-Deliveries/online-selection-converged-20261110。最终人机回执未签发，勿将自动化或390px当Safari。家庭旧私链不在本机，单列未复验。

## 2026-11-10 在线选片 HTTP v1（Europe/Berlin；Asia/Shanghai 同日）
- 独立分支feature/online-selection-http-v1-20261109，从37ea6c9a/产品02702071接续；首个功能34b72f44，最终产品c44f800a428ba097e024c91e7691dbf3b6a17355；启动验收支持81bc7be9，之后仅工具/交接。旧349bf400固定只读参考，不合旧树、不改其dirty。
- 正式桌面建项目→生成代理→服务确认上传→带规则发布→复制受保护链接；响应式网页勾选确认锁定；PC新增可见刷新客户结果入口，校验项目/素材/版本/修订后落盘，既有同步入口返回归片项目；独立进程重开保留版本1/修订4和关联。None配置提示保持，HTTP/凭据在桌面基础设施，Core原无传输断言未放宽。
- 最终发布DLL30条绑定；Core52+WPF38回归90PASS，加完整App四独立阶段4PASS，共94条所选TRX通过；最终真实服务请求44条。重试不增项目/素材、跨项目/角色拒绝、锁定409、过期410、撤销页404/API401通过。原生UIA/真实系统对话框与实际浏览器点击，不冒称物理鼠标/手机。
- 全模块自包含包：E:/Kitao-Deliveries/online-selection-http-v1-20261109/Guangjing-OnlineSelectionV1-FullApp-Diagnostic-win-x64.zip；851文件逐项及ZIP读回，158575491 bytes，SHA256 95FDF2C86B2A05AAC9FACCB030ED3BBC75D52EC0B889C61F3B86E88B74E18194。Start-OnlineSelection-LocalTest.cmd启动包内LocalDev和完整桌面并生成合成JPEG；正常EXE/CMD启动退出0。WholeAppDiagnosticBuild=true/PhotographerTrialBuild=false，全部模块/策划15/教程退役，无Setup。
- 交付状态LOCAL_HTTP_V1_READY_FOR_REVIEW_PHONE_PENDING；LocalDev loopback/single-owner/production=false；物理iPhone Safari与微信NOT_RUN_ENVIRONMENT_NOT_READY，缺已授权HTTPS和设备；390px仅桌面响应式。全产品仍PARTIAL_DIAGNOSTIC_NOT_FOR_RELEASE。PC/手机档期同步独立；本轮四项RAW调用0，native未换。
- 唯一当前报告docs/validation/online-selection-http-v1-20261110/FINAL_REPORT.md；本地原始E:/Kitao-Deliveries/online-selection-http-v1-20261109。凭证/链接/库/像素不进Git/包。后续文档HEAD及远端精确读回见completion-20261110/PUSH_READBACK.json，勿与c44f800a产品源混写。旧失败/旧报告均保留。

## 2026-11-09 R5.5 拼贴素材“已用”角标（Asia/Shanghai）
- 从收藏交付42476a1c/功能623d0a8c独立接续feature/r55-collage-used-20261109，产品源码02702071e1eda7e8f51c806a7e1aeb8fa9bb18c2。只改FreeCanvasView模板及Usage partial两产品文件，复用CanvasEditor.Changed，从当前文档LibraryId+AssetId派生，不增持久字段/导入身份/收藏存储，隐藏锁定和重复实例计入。
- 最终发布DLL29项绑定；Core16+WPF14+完整App独立原生UIA3=33PASS。PID27932→4680→13068走真实素材栏/编辑移出/撤销重做/保存/新建和重开画布/最近库切换/取消收藏/系统导出；最后实例移除清标、有其它实例保留、跨库同ID不交叉、取消收藏对象仍在均通过。不是物理鼠标或完整DPI验收。
- 透明PNG1920×1080，透明2006100/非透明67500，源hash不变，工程不含派生状态。发布EXE/CMD真实启动读回10主导航+工具箱，正常关闭退出0。早期构建/模板x命名空间失败、冒烟工具就绪/退出码读取失败保留；产品模板在冻结前修复，后续只修工具不重建包。
- 全模块诊断ZIP E:/Kitao-Deliveries/r55-collage-used-20261109/Guangjing-R55-CollageUsed-Diagnostic-win-x64.zip，77197962bytes、296文件逐项ZIP SHA读回，SHA7CEC005C3E3455960059A341A6C115C479783CBB2F8187BD6A9FEDF884BF9FED。WholeAppDiagnosticBuild=true/PhotographerTrialBuild=false，策划15/教程退役/全模块保持，无安装器；native仍F500C073…，本轮RAW四API0，不宣称修复A问题。
- 在线provider只读核对完成：正式App已接真实代理生成与JSON本地持久化，默认仍None；远端create/get、生产上传队列、结果刷新未完整接线。首片为单项目/单张代理/单客户受保护入口/确认回写，需固定API v1、鉴权/幂等/规则/发布凭证/最终确认合同及HTTPS测试部署。未替换provider、未部署、服务请求0，不把mock当联网验收。
- 报告docs/validation/company-r55-collage-used-20261109/FINAL_REPORT.md；接口及下一片ONLINE_PROVIDER_NEXT_SLICE.md。本地根E:/Kitao-Deliveries/r55-collage-used-20261109；所有既有树/脏文件/未推成果与失败证据保留，最终远端HEAD普通推送后另读回。
## 2026-11-09 R5.5 拼贴素材收藏（Asia/Shanghai）
- 从产品38dd3f92建独立feature/r55-collage-favorites-20261109；最新产品f27f4409相对基线src零差异。保全65旧树/11脏树/未推与全部失败，不改A。
- 最小复用现有FreeCanvas/素材身份/导入：独立引用收藏、取消收藏、收藏筛选、加入画布；修正切来源时旧列表选择被异步清空。收藏不借用照片评分、不缓存像素，当前库原子保存，非法/跨库引用明确拒绝。
- 产品源码623d0a8ccab6305320fd2d93eda2bcdcc45711f3；测试221f5c9bcebfb1e1b1d28c6435b057f83d9b645d（只修UIA就绪等待）。最终发布DLL绑定Core16/WPF12/完整App原生UIA三进程3，共31PASS。真实工程菜单重开/系统另存为/PNG alpha和源hash读回；非物理鼠标、非完整DPI或设备验收。
- 完整自包含包E:/Kitao-Deliveries/r55-collage-favorites-20261109/Guangjing-R55-CollageFavorites-Diagnostic-win-x64.zip；296文件ZIP逐项hash通过，ZIP SHA256 C591420060C2EA55DC169244A6AA76B32A0A011B27C8B24A8086929328EEA6DF。EXE/CMD真实全壳启动，CMD退出0。WholeAppDiagnosticBuild=true/PhotographerTrialBuild=false，全模块/策划15/教程退役，无安装器。
- 本轮RAW全部0；生产raw_r.dll F500C073…未替换；不整合A的观测库。各板块证据状态和下一项“素材已用角标”见docs/validation/company-r55-collage-favorites-20261109/MODULE_STATUS.md；完整报告同目录FINAL_REPORT.md。旧失败与最终推送读回留交付根。

## 2026-11-03 公司当前：旧灯台四提交远端保全与最小交互收回（Asia/Shanghai）
- 四个真实旧提交9299749c87833b4b637b83d0f086d06ac6e1025f、a1a2a8f751b83bfce23982784d2d7f3cae86fb2d、2ed1dc358cb6f7acaaaa670db25c8233f873f415、27794d2f86313d7231f235b4fd1104eb4e95c106已先普通push独立远端分支 **preserve/company-lighttable-four-20261103**，HEAD27794d2f；完整链接/父链/逐提交审查见docs/validation/company-r55-lighttable-reclaim-20261103/FINAL_REPORT.md，不再仅本地保留。
- 旧树E:/Kitao-Worktrees/company-lighttable-lifecycle-20261030不reset/clean/stash、HEAD/tracked源码不改；相对旧上游仍4ahead但现已可远端审查。49个未跟踪文件（30PNG/19文档收据）只列路径/hash，均未自动上传，旧私人图片/输出/受限资源不入Git。
- 当前8d3d8753基线实测保存清选择和旋转工具条重叠2项FAIL。仅一个产品文件最小收回a1a2/277的交互意图：同工作区保存保留有效选择，Targets真正Reset仍清空防同ID跨项目泄漏；工具条用实际变换照片+标签union和实际可用视口定位，不改当前独立Image/标签卡片结构。
- 929大部分热点已有2174da07后续替代，不移植profiling/旧样式/任务状态/机械光影，不用旧成绩宣称新提速。2ed仅复用变换照片必须被明确选中的测试意图。未整树覆盖、未cherry-pick四提交，四旧SHA不作为当前产品祖先。
- 产品源码 **26c034873a469d428b4e1975d1772a55f0128b66**；测试 **918d33025a45b528da417a4cd5f7638a6112e7db**。最终发布绑定15唯一PASS，含独立进程重开、同ID项目隔离/UndoRedo、渐进选择/任务取消/缓存句柄边界；800/1366/1920×3角度×3缩放27组通过，布局无新增解码。鼠标/DPI仍NOT_RUN，离屏不冒充实机。
- 完整自包含诊断ZIP E:/Guangjing-Lighttable-Reclaim-20261103/Guangjing-R55-LighttableReclaim-Diagnostic-win-x64.zip，294文件逐项hash读回，SHA256 CAB9C4743D86472ED8BBE14C3C8FB9A082E7D9023B61CB87AB7386F9FC7D9F70；同名目录Start-Guangjing-Diagnostic.cmd隔离启动，产品SHA日志及EXE/CMD退出0。WholeAppDiagnosticBuild=true/PhotographerTrialBuild=false；全模块/策划15/教程退役，无Setup/单模块。
- DOCX失效关联/警告修复保持，本轮DOCX专项/Office探测/WPS操作0；独立Office继续NOT_RUN。色彩研究不整合，渲染数学/default/PV/RAW/任务桥/项目包范围不改，不合main、不强推。总体PARTIAL_DIAGNOSTIC_NOT_FOR_RELEASE。新记录Asia/Shanghai，历史收据保持。

## 2026-11-03 公司当前：DOCX失效关联反馈与警告一致性（Asia/Shanghai）
- 两远端fetch后开工产品仍372a2e2334a6d06068df2221d4cb875959e651af/源码5116daa7，公司产品树clean/0未推；已查相关refs、工作树、reflog、交接记录及可见会话。家庭昨晚执行/未推成果仍缺实际HEAD/diff/测试或独立渲染回执，**状态未知，不以远端未更新判未执行**；未访问家庭N盘。后续家庭补丁须比较复用，不覆盖。
- 原四份DOCX哈希不变，原已完成产品读回证据沿用。公司有WPS12.1.0.29161，但CLI帮助被签名门禁拒绝；受控COM前置检查遇到用户WPS PID11928，未创建COM/打开文档，未接管/关闭用户进程，未反复空跑。原四份独立Office仍NOT_RUN，不宣称排版FAIL或通过。
- 公司当前可取得基线真实导出入口复现2项FAIL：失效正文继续导出旧RTF残图/灯位静默失链；延迟解码返回Warnings1但末尾汇总漏掉。最小修复保留请求ID、原位置明示不存在并加入Warnings、移除残图并保留正文/有效图片；末尾警告在全部图片处理后生成，补标题KeepNext。
- 最终源码 **3bf5d9a6a10124793434a43d782b22978d8707d0**，测试 **73c3f33fe988697c1c0277e2cf02bdc490f7252e**。最终发布绑定25唯一/25执行PASS（WPF20/Core4/新增反馈页读回1），只相关集合；新增两份反馈7页查看通过，不等于原四份独立Office完成。初始失败/构建/预览保留，不重复快照嵌入、切库专项或JPEG/ICC研究。
- 完整诊断ZIP E:/Guangjing-Summary-LinkRecovery-20261103/Guangjing-R55-SummaryLinkRecovery-Final-Diagnostic-win-x64.zip，294文件逐项SHA/ZIP读回，SHA256 B537F368065C016F975AB341F7F85DD9A19933EDCBFC4CAE46885442AED3CF56。同名目录Start-Guangjing-Diagnostic.cmd固定隔离根，OS启动身份/EXE和CMD退出0。WholeAppDiagnosticBuild=true/PhotographerTrialBuild=false；全模块/策划15/教程退役，无Setup/单模块。
- 公司旧灯台4独有提交和dirty资料原样保留，不合main/色彩研究、不强推。原画布数学/v1-v3/预算/default/PV/RAW/项目包协议不改；鼠标/DPI等未验。整体PARTIAL：公司反馈修复完成、独立Office未执行、家庭成果状态未知。当前报告docs/validation/company-r55-summary-link-recovery-20261103/FINAL_REPORT.md，所有新记录Asia/Shanghai，旧收据不改。

## 2026-11-02 公司当前：15区汇总DOCX固定快照（Asia/Shanghai）
- 从6e01f26f/产品89b64fd2接续，fetch无产品线新增；旧灯台4独有未推提交、旧树/未提交资料/历史失败原样保留，不合main/研究、不强推。保存失败恢复专项冻结，只跑必要保全。
- 产品源码 **5116daa77fdbf17f705497efc6df5b4cdcabd7a0**；测试支持 **c5222046aa7705ccf1adcd93b44e2fa37b91b518**。基线两版快照DOCX media0缺图已复现；现15区汇总仅从已保存PreviewPngBase64逐版嵌入，版本/时间图注，缺失损坏超预算原位占位+Warnings，不重渲染或访问源照片。
- 沿已保存顺序，无明确关系放15文档内容；尊重正文PlanningBlockSemantic/灯位PlanningLight显式revision关联，不猜情绪板。只读输出，原JSON/画布/PNG/revision不变；.ptplan v1不扩，可读DOCX不称可编辑画布/项目包。
- 单图32MiB Base64字符/24MiB编码字节/8位静态PNG/4194304像素，复用既有PNG和DOCX编码，逐图延迟解码后解除Source引用；不是进程硬上限，不改Canvas v1/v2/v3及256MiB预算。
- 发布20共享DLL绑WPF/9绑Core，58唯一PASS（45+独立/渲染3+Core10），所选0FAIL/0跳过；四份可分发合成DOCX及14页产品读回PNG逐页查看通过。canonical render_docx.py因无soffice.exe失败日志保留，独立Word/WPS/LibreOffice排版仍NOT_RUN；鼠标/DPI/ACM/HDR未验，禁用工具未启用。
- 完整诊断包E:/Guangjing-Summary-Snapshots-20261102/Guangjing-R55-SummarySnapshots-Diagnostic-win-x64.zip，294文件逐项SHA/ZIP读回通过，SHA256 81B2F834DDB2D37BDFAB3A7078540767BF615F39CB6DBF095F74C38FBDCACD5F；同名目录Start-Guangjing-Diagnostic.cmd固定隔离根，OS启动身份和EXE/CMD退出0。WholeAppDiagnosticBuild=true/PhotographerTrialBuild=false，全模块/策划15/教程退役，无Setup/单模块/私照。
- 仅阅读交付改善，不宣称整个策划中心全验收；总体PARTIAL_DIAGNOSTIC_NOT_FOR_RELEASE。原JPEG/ICC FAIL、六图/20MP研究边界未改未复跑。当前报告docs/validation/company-r55-summary-snapshots-20261102/FINAL_REPORT.md；能力边界另见CAPABILITY_BOUNDARY和current清单。新时间Asia/Shanghai，不改历史收据。

## 2026-11-02 公司当前：保存失败后的退出与切库恢复（Asia/Shanghai）
- 基线7c8da76139c098666f90636d3d2e34449fb8de0a/产品fc811b54，fetch无产品线新增；实际公司树E:/Kitao-Worktrees/r55-workflow-company-20261102，继续home/r55-canvas-photo-preview-20261101。旧灯台27794d2f的4独有未推提交/未跟踪资料原样保留，不合main/研究、不reset/clean/stash。
- 已真实复现：A画布保存失败将page释放任务永久fault，修复写入并保存后仍切不了B；host首轮失败后第二次释放CompletedTask假成功。原失败TRX/逐阶段JSON保留，非静态猜测。
- 产品源 **89b64fd220bcb806065a13a061f0c392a2fdb050**；最终测试 **a03d990702c169570111d87ef73ac2b7087ac511**（初始4129fc7a）。仅改AssetLibraryPage/AssetLibraryWorkspaceHost：保存前失败可再试、并发共享原任务；不可逆清理后原fault不重置；候选页失败仍finally释放候选lease/gate；成功释放后清除引用，保留既有保存确认机制。
- 发布20共享DLL绑定后 **37唯一/37执行PASS，0FAIL/0跳过**；最近库菜单→失败保留A→实际保存→再切B→回A→独立进程重开，A/B lease/数据库可独占，4页repository和后台任务释放；并发/第二次退出不假成功、不可逆故障边界和旧快照/取消/源校验/原子保存回归通过。没有重复上一轮完整大照片闭环或数值研究。
- 完整自包含诊断包E:/Guangjing-Workspace-Recovery-20261102/Guangjing-R55-WorkspaceRecovery-Diagnostic-win-x64.zip；294文件逐项hash读回，SHA256 F922E56BDE16E5D334351572EC95589813A124DB1DD8FCD2E0918690B068426E。同名目录Start-Guangjing-Diagnostic.cmd固定隔离根，OS启动身份和EXE/CMD退出0。
- WholeAppDiagnosticBuild=true / PhotographerTrialBuild=false；全模块/策划15/教程退役，无Setup/单模块。Canvas数学/v1/v2/v3/256MiB/recipe/RAW/色彩研究/灯台均不改。旧JPEG/ICC FAIL、历史门禁收据保留；鼠标/DPI/ACM/HDR仍NOT_RUN。总体PARTIAL_DIAGNOSTIC_NOT_FOR_RELEASE。
- 当前报告docs/validation/company-r55-workspace-recovery-20261102/FINAL_REPORT.md；产品/测试/包/文档HEAD与远端分记。新记录Asia/Shanghai，历史收据不改。

## 2026-11-02 公司当前：完整画布固定快照闭环（Asia/Shanghai）
- 从家庭8da827794364da66f52c574aad401078fbda503a/产品146c0fd254de387ad5ccb9570cff6c54697389d4接续；实际树E:/Kitao-Worktrees/r55-workflow-company-20261102，分支home/r55-canvas-photo-preview-20261101。公司灯台27794d2f的4个未推独有提交及原未跟踪资料原样保留，不合main/研究，不reset/clean/stash。
- 产品源码 **fc811b54a4a140543a4c9bead47de8b60e7529f2**；测试支持 **dd71c1f857d105229b4ffb3e3e08438a9670c09b**。仅修当前素材库重开自锁/占用异常行内提示，以及画布写入失败路径/权限/空间/占用的可操作指引；不改v3/解码核/ICC/预算/灯台，不扩功能或重画R5.5。
- 完整App以可分发合成24MP JPEG、透明PNG、文字、画笔、三色色板验证保存→固定快照→独立进程重开→修改→手动更新→第三进程；两版身份与旧PNG保留、编辑不偷偷更新快照。实际公司已授权CompanyRAW仅5个RAW，未扫描其他目录，合成输入不冒充私照/实拍验收。
- 发布20共享DLL绑定WPF/9绑定Core：**51唯一方法PASS、53执行、所选最终0FAIL/0跳过**；含全壳10主导航、策划15、教程first/upgrade/restart、异常/取消/源变/项目切换和1366/1920离屏宿主。开发失败和原收据保留，非全仓测试全过。
- 新完整自包含win-x64诊断ZIP E:/Guangjing-Company-Workflow-20261102/Guangjing-R55-CompanyWorkflow-Diagnostic-win-x64.zip，294文件逐项hash读回，SHA256 B73364132707F19D4B73FAD42A46B22E2ACD7FFB40187AC8CE7871BDD71A0C2C。同名目录Start-Guangjing-Diagnostic.cmd固定隔离资料根可保存重开；实际CMD/EXE启动/日志产品SHA/退出0。WholeAppDiagnosticBuild=true / PhotographerTrialBuild=false；无Setup/单模块/私照/研究程序。
- 数值归因已阶段结束，家庭原JPEG/ICC FAIL/阈值及NONPASS字节保留，本轮没有新同类数值报告。鼠标、多DPI/多屏、ACM/HDR/真实素材闭环仍NOT_RUN；ComputerUse/Browser/WebView/CDP未启用。总体 **PARTIAL_DIAGNOSTIC_NOT_FOR_RELEASE**。
- 当前权威报告docs/validation/company-r55-workflow-20261102/FINAL_REPORT.md；产品/测试/包/文档HEAD与远端另见IDENTITY/REMOTE_READBACK。新记录Asia/Shanghai，历史时间不改。

## 2026-11-01 v3两项数值归因收尾（Europe/Helsinki）
- 本轮仅tests/docs，产品源码仍146c0fd254de387ad5ccb9570cff6c54697389d4；家庭基线17e9788f，测试支持fa171e47f5d3b3921676fb11afa0bb5a996338a6。不改src/灯台/UI/研究引擎，不再次发布/打ZIP；原完整294文件ZIP d394e3ab463531f66eb4ae74ef571becb26bec20754965ef07251a35c560cf5d逐项读回保持。
- JPEG已用分量/系数因果闭环：FFmpeg lowres2 2x2核只保留低阶，与WIC/IJG area型含AC3/5/7不同；WIC420 native输出Cb/Cr1500×1000，FF750×500。BT601全范围/通道正确，色度重建反事实max36–39，原max40不靠舍入/“不同解码器”空解释。
- ICC同native/源目标ICC，独立MSCMS BEST_MODE BGRA与WIC原照片/保留夹具max0，方向max0；LCMS参数/精度排查后，受限独立matrixTRC oracle与LCMS0，WIC3，属于系统CMM颜色/量化阶段差异；专有CMM内部BPC/适配值不可读，保留未知。
- 事前冻结阶段合同6关系通过，原max24/mean1.5/P95=3、ICCmax1门槛不改，旧照片脚本仍exit1（三项数值FAIL）。能力verification_status保留FAIL，diagnostic归因完成；不将新诊断关系通过当旧验收通过，不无限重做同一归因。
- 仅原授权RAW/Real与closure两根支持栅格12文件无已有ICC+方向匹配，继续NOT_RUN；生成profile/方向不能冒充真实原始性。真实鼠标/DPI/ACM/HDR未验。
- 相关发布DLL回归34＋独立重开2＋阶段探针1唯一方法通过，能力一致性1通过（合计38唯一方法）；原符号链接FAIL/通用LCMS参考NOT_RUN保留，非全套测试。权威报告docs/validation/home-r55-v3-numerical-20261101/FINAL_REPORT.md，原失败/探针错误/holdout/源码与包身份分记。

## 2026-11-01 家庭当前：大照片画布预览 v3（Europe/Helsinki；优先于下文历史）
- 当前产品源码 **146c0fd254de387ad5ccb9570cff6c54697389d4**；分支 home/r55-canvas-photo-preview-20261101，工作树 N:/pixart/guangjing-r55-canvas-photo-preview-20261101；从40919e48/产品69bbf229接续。测试支持 **5d94691b34f8762532199e991d7b2b44852b1b47**，文档HEAD与远端读回另记。灯台冻结，研究分支不合入，旧树/未提交资料保留。
- 新固定快照RenderVersion3，CanvasJpegNative1024.ArtboardCenters.v3。大baseline 8位JPEG从原图已校验读lease进行WIC native IDCT缩小，源ICC→sRGB后方向；24MP/33MP照片派生输入实际native为1500×1000 / 1752×1168，不能称原图细节或固定1024输出。小图走原v2数学，旧v1/v2保存PNG字节不重算，旧导出不暗改。
- 256MiB是渲染工作估算，不是整应用上限；最多32MiB/8来源任务缓存，每次命中重验来源hash，结束/取消清空。渐进大JPEG、大PNG超预算、RGB48+ICC明确拒绝，不放宽RAW/16位支持。
- 真实授权既有TIFF派生24MP/33MP JPEG＋透明PNG，基线0快照预算拒绝；发布版首次631.7ms，续接复核657.4ms/1快照。完整宿主渲染增量工作集87,908,352B/私有125,042,688B，进程峰值412,651,520B/416,169,984B。八轮新任务cache残留0、PNG hash相同、源句柄可独占重开；强制GC仅寿命诊断，不是预算准入手段。
- 相关自动测试去重 **119PASS/1既有符号链接权限FAIL/1缺LCMS参考NOT_RUN**；新发布绑定60PASS及独立新进程重开/手动更新通过，历史配置/重复夹具失败保留。独立数值有 **ICC max3>1码、4:2:0 FFmpeg max40/mean1.63357/P95=4超预设**，门槛未放宽；不能宣称开源/通用ICC科学门禁全过。Pillow native相同、float64合成/64行边界最大1码；CalibrationEligible=false。
- 完整win-x64自包含诊断包 N:/pixart/Guangjing-R55-CanvasPhoto-20261101/Guangjing-R55-CanvasPhoto-Diagnostic-win-x64.zip；同名目录Start-Guangjing-Diagnostic.cmd。294文件逐项SHA256与ZIP读回通过；全部模块/策划15/教程退役保留，WholeAppDiagnosticBuild=true / PhotographerTrialBuild=false，无Setup/私照/研究程序。
- 唯一当前报告 docs/validation/home-r55-canvas-photo-20261101/FINAL_REPORT.md，原始TRX、数值失败、绑定、ZIP身份已归档；私人照片/预览/截图只留本地。OFFSCREEN不是鼠标/DPI/ACM/HDR验收；Browser/WebView/ComputerUse/CDP未启用。总体 **PARTIAL_DIAGNOSTIC_NOT_FOR_RELEASE**，独立数值门禁及物理验收仍未完成。
- 输入补核：两原TIFF无嵌入ICC，JPEG测试profile/方向为新写入，原有真实ICC+方向源验证NOT_RUN；不以生成元数据冒充原生来源。

## 2026-10-30 家庭当前：策划画布一致性（优先于以下历史）
- 当前产品源码 **69bbf22935f3471013ad6bb3728f95b8b562acfc**；新分支 `home/r55-planning-canvas-20261030`，独立树 `N:/pixart/guangjing-r55-planning-canvas-20261030`。上游灯台eae800e7/产品1ec06eb5冻结，色彩研究未整合；测试支持f720f1a8、文档HEAD另记。
- 旧策划Snapshot独立合成器已替换为CanvasPixelRenderer有界公共入口：Hidden/Opacity/画笔/文字/色板/方向/crop/flip/rotate同合同，固定画板透明裁切。1400边/1.96MP/256MiB估算/1000层，STA串行取消，不为预览分配最大画板float。
- 新快照RenderVersion2；旧缺省1保留PNG不重算。实际落盘原子追加后才发布VM，手动更新保留旧版；项目/策划/操作代次拒绝迟到，缺源/来源变化/保存失败不发布半成品。原.ptplan v1范围、全部模块/策划15/教程退役保持。
- 同尺寸混合快照与旧DLL导出像素和PNG字节相同；缩小采样独立oracle0码；Pillow/FFmpeg两个固定实现alpha最大1码。发布绑定WPF42+独立2+完整宿主1+范围1、Core50+PV6 4通过；去重100PASS/1旧符号链接权限FAIL/1LCMS参考矩阵NOT_RUN，不能称全套PASS或实机。
- 家庭最终完整包：`N:/pixart/Guangjing-R55-PlanningCanvas-20261030/Guangjing-R55-PlanningCanvas-Final-Diagnostic-win-x64.zip`；同名目录Start-Guangjing-Diagnostic.cmd，WholeAppDiagnosticBuild=true / PhotographerTrialBuild=false，win-x64自包含，不Setup。
- 唯一当前收据：`docs/validation/home-r55-planning-canvas-20261030/FINAL_REPORT.md`。6张真实1366/1920生产控件离屏图已逐张打开；实际EXE/CMD OS启动与正常退出0通过。真实鼠标/DPI/ACM/HDR、通用16位/ICC未验，WIC既有RGB48+ICC拒绝保持。总体PARTIAL_DIAGNOSTIC_NOT_FOR_RELEASE。

## 2026-10-30 家庭当前产品（优先于下方历史）
- 当前产品源码 **1ec06eb5edcb0343d8587304e0536b554c3002d1**；家庭分支 `home/r55-lighttable-20261030`，独立树 `N:/pixart/guangjing-r55-lighttable-20261030`。测试支持5ceb1c55（src无差异），文档HEAD另记。
- 公司b1443ed4/产品e0ff14ab为本批上游，家庭旧7a6b98e9为其祖先（公司ahead13/无分叉）；旧树与dirty保全，研究引擎未整合。
- 照片/版本/状态/选框/上下文分层、明确22DIP展开、胶片数值/复位/真实任务、轻量镜边和幻灯条已修；列表/信息控件复用、通知去重、几何操作避免完整配方序列化。渲染数学/RAW/X-Trans/内存门禁不变；灯箱主干冻结。
- 家庭既有100自然RAW同机公司基线→最终94/6不变；warm366→134ms，冷全完成64.805→65.060s，取消退场400→560ms，部分内存上升，不称全面提速。Leica公司已修、六X3F仍OpenFile/-2/FileUnsupported；两Fuji旧native像素重复性OPEN。原片与自然截图只留本地。
- 发布7项目/20共享DLL绑定，84去重方法PASS（86场景执行），非全量/实机。22张完整宿主+8生产控件合成截图逐张查看；OS EXE及实际CMD正常启动退出0。物理鼠标/DPI/ACM/HDR未验，逐像素定稿仍有余项。
- 家庭完整包：`N:/pixart/Guangjing-R55-Home-20261030/Guangjing-R55-Home-Refresh-Diagnostic-win-x64.zip`；同名目录 `Start-Guangjing-Diagnostic.cmd`。WholeAppDiagnosticBuild=true / PhotographerTrialBuild=false，完整模块/策划15与教程退役保留，无Setup/私照/研究二进制。
- 唯一家庭收据：`docs/validation/home-r55-lighttable-20261030/FINAL_REPORT.md`（源码/测试/发布DLL/ZIP/文档HEAD分开）。总体PARTIAL_DIAGNOSTIC_NOT_FOR_RELEASE。下文公司和旧家庭身份仅历史，不覆盖本节。

# 光镜R5.5当前实施大纲
2026-10-30 Europe/Helsinki。当前产品e0ff14ab57316fe47d112743f56add7d6ba26e0e，company/lighttable-lifecycle-20261030，测试支持dd191d41。来源fefd53f2/产品f7b53078，未回退家庭、未合研究分支/主线。

## 本批完成
完成通知只消费一次；幻灯Loading/Black/Arriving阶段暂停所有权、重复Space/停止/凑近切换；两选/单选比较与冻结来源；实际Uniform映射与等比圆镜；LostCapture/焦点/退出/Esc/异常手势收尾、稳定Z和跨项目history；可见反馈、右栏来源/预检/筛选；已载入卡选择不终止渐进工作。
实际长任务由真实Generate按钮启动、计算后受控边界，右栏开关/取消按钮参与；关闭不取消，显式取消无半成品。满100项不丢旧项。
灯板10–160%有实际显示结果，灯光不进proxy/比较/重算比较/剧照/输出PNG。复杂RAW/胶片域没有扩张，原异步、身份、缓存/内存预算不回退。

## 逐项移植状态与门禁
同一中央宿主/导航/托盘/22侧栏、台面几何/视口/撤销、SDR板材曲线/柔光、代理镜及机械播放阶段/合成音已接；完整玻璃折射散射、真实机械角度/快门美术仍部分。不得再总称PORT_MAP A-D全部完成。用户非商业许可及MIT署名保留，不代表商业发布审查完成。
透光叠底、原图ROI、EDR、色温、Spektra仍后续；Fuji重复性/家庭缺源/超大RAW安全支持OPEN。物理鼠标/设备DPI/ACM/HDR未验。

## 当前证据
最终7程序集绑定：68唯一PASS（Core32/WPF30/独立3/完整宿主1/性能2）；不是旧38加重复次数。完整宿主20张1366/1920截图OFFSCREEN合成输入，已查看并记视觉差异。实际CMD隔离启动/退出0/日志SHA通过。
完整自包含公司包与全部源/绑定/TRX/截图/ZIP/同机性能见../validation/company-r55-interactions-20261030/FINAL_REPORT.md。全模块/策划15保留、旧教程退役、无Setup，诊断PARTIAL不发行。

## 设计保全
原设计包../design/r55/R55-final.zip，相对索引R55_FILE_INDEX.json；aafe6fe6567bf33c609d76eda3f46e0d18e68f8a8af2de42defdd049454a014c。全局规则→第10章+00-16（01/15/16最新视觉）→第9章节点→其余R5.3。设计内20261022分支仅历史参考。指定DOCX字节缺件与Google另hash保持区分。
