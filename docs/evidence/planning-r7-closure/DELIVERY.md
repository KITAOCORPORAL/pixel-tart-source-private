# Kitao Studio R7 修复闭环交付

日期2026-10-14。**PARTIAL：两个目标回归已修复；严格全量门禁因30项未执行而FAILED；新公司Release未构建。**

## 身份
- 基线远端：origin/feature/kitao-planning-r7-20261014，fetch前后均为8e3e203d71c10f8f89db650a8fabd516a597c957。
- 独立工作树：D:/AI AGENT/kitao-planning-r7-closure-20261014。
- 修复分支：fix/kitao-planning-r7-closure-20261014。
- 冻结产品/测试源码：1793265990902aa54e0f62befc663e6b04ee83a8；a202374仅移除Python生成缓存并加忽略，src/tests零差异。其后为交付文档。
- 无适用AGENTS.md；已读R7 DELIVERY、FINAL_WPF_FAILURES及TEST_EVIDENCE.zip内原始失败记录，原文件未改。

## 修复位置与根因
1. `src/RAWSelectionAssistant/Services/PlanningDocxAdapter.cs`：已有目标采用File.Replace，不再用File.Move(overwrite=true)做rename-overwrite；新目标采用File.Move。Windows delete-sharing读者可导致旧原语0x80070005，受控读句柄已确定复现；自然失败时目标非只读、ACL/身份未变，失败后独占打开成功，File.Replace同目标成功。未识别短暂自然读者的进程归属，不猜成杀毒/索引器。详见ROOT_CAUSE和DOCX_DIAGNOSTICS。
2. `tests/RAWSelectionAssistant.WpfTests/RuntimeCorrectionWpfTests.cs`：旧夹具迁移root/product.db，却让页面默认连接AppDataPaths/Data/pixel-tart.db。修复注入product/online/tray独立路径；真实迁移、真实项目写入及CanvasProjectsAsync读回ID。原保存、两次关闭、延迟关闭、无异常断言保留。产品启动迁移链路已核查，不是已证实的产品启动缺表。
3. `tools/VerifiedTestRunner`及`tools/planning-r7/verified_runner.py`：使用VSTest适配器发现真实TestCase；精确FQN等值筛选；每轮/每类独立目录；核对发现/实际多重集和TRX计数；非零退出/超时/缺TRX/未执行全部失败。SourceDirectory不再用于正则发现。3个独立运行器合同测试通过。

## 证据结果
|阶段|结果|
|---|---|
|修复前直接原两例|2失败；本次复现，不只历史记录|
|修复前Start-Process原两例|2失败|
|旧R7发布组件受控读者|1失败；旧原语拒绝；页面合同单例偶发通过|
|修复后直接单例/安全项|4通过0失败0跳过|
|修复后适配器隔离相关类|12通过0失败0跳过|
|相关策划/文档/画布回归|64通过0失败2未执行；原记录保留|
|冻结完整Core|1682通过0失败7未执行；严格总状态FAILED，退出1|
|冻结完整WPF|1587通过0失败23未执行；严格总状态FAILED，退出1|
|发现/实际结果核对|Core1689、WPF1610；逐项一致，0遗漏、0额外、0超时|

WPF适配器发现208个有实际测试的类，和旧源码正则数量不同；以适配器1610项与TRX逐项核对为准，不以类数猜覆盖。
`UNEXECUTED.json`列出30个未执行测试及原因，包括授权RAW、GPU、性能/视觉授权等。未伪造数据或把Inconclusive改成Pass。

## 安全合同
- 继续覆盖原DOCX，原页面合同测试未弱化；受控允许删除共享读者下，新文件正确、旧读者仍读旧内容。
- FileShare.None锁定、只读、目录ACL拒绝时保留旧文件完整字节；临时文件清理；明确IOException/UnauthorizedAccessException向上传递。ACL只改本次Guid临时目录，finally恢复。
- 文件属性/目录ACL/运行身份/框架/组件hash在TRX标准输出；失败诊断在DOCX_DIAGNOSTICS。数据库路径与schema证据为DATABASE_LINEAGE，只读检查合成数据库，不提交DB。
- 临时资源/子进程仅限本任务；未reset/stash/clean、未结束其他工作树进程。

## Release决定
你要求“通过后构建新的公司Release”，同时“未执行项令总结果失败”。当前严格门禁未通过，因此 **NOT_BUILT_GATE_NOT_PASSED**，没有新的Release目录、EXE身份或发布组件复验可声称。
`RELEASE_STATUS.json`记录此决策及用于对照的旧R7发布DLL身份。旧发布不是本批修复交付，不把旧目录当新Release。若批准在明确未执行项下制作审阅构建，才可另行发布；本轮没有擅自放宽。

## 未关闭
- 30项条件/授权未执行，阻断严格全绿和新Release。
- 自然DOCX失败瞬间短暂读者的程序归属未定位；已确定并修复的操作语义缺陷及受控复现详见根因，不做额外归因。
- R7原功能边界（结构字段批量替换、复杂表格/页眉等）不在本次闭环扩展，原PARTIAL继续保留。
- Windows实操、物理DPI、WPS：NOT_RUN。既有WPF合成HWND测试不是人工实机批准。
- Color Studio PV4/PV5/PV6及历史色彩算法零diff。

## 目录索引
ROOT_CAUSE.md、REPRODUCE.md、FILES_AND_TYPES.json、SOURCE_AUDIT.json、DATABASE_LINEAGE.json、DOCX_DIAGNOSTICS.json、TEST_SUMMARY.json、TEST_RUNS.json、FROZEN_CORE/WPF_DISCOVERY与SUMMARY、UNEXECUTED.json、RAW_TEST_EVIDENCE.zip、RELEASE_STATUS.json、EVIDENCE_MANIFEST.json。

正常推送fix分支，不强推、不合并main。LOCAL_HEAD/REMOTE_HEAD在最终回复核对，发布源码与文档HEAD分开。
