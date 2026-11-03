# 公司R5.5：项目—策划—日历—实际支出—15区汇总闭环

记录：2026-11-03（Asia/Shanghai）。状态：**本轮业务修复与针对性验证完成；整体 PARTIAL_DIAGNOSTIC_NOT_FOR_RELEASE**。

## 1. 接续与范围

从基线 3d7184102dda8d8c75f4eb8d052affb08d5510ca 接续。本会话因原会话上下文耗尽而继续：产品修复 38dd3f92bb2db7238f7f058008887929ec971c93、测试 d0c88e27291c432ace5dace5db8f1b3a6a8c5772 已在原会话提交，本地领先远端2笔且工作区干净；本会话审核后未再改产品或测试源码，完成发布绑定回归、包封存、文档和推送。

origin/source-private均重新fetch，产品远端无新增。盘点60工作树、全部本地/远端refs；其余59树的HEAD/dirty状态与原轮开工记录一致。旧灯台树HEAD27794d2f及49个未跟踪文件逐个SHA256和长度未变；保全分支仍远端可读。本轮不移植旧灯台四提交、不合main/研究引擎、不强推、不reset/clean/stash，日常账本及无关素材未访问。原四份独立Office验收不接管、不重开WPS；旧JPEG/ICC失败和历史收据不改。

## 2. 先审查已有生产入口，不另建模型

- 策划创建/保存：CreatePlanningCommand、现有PlanningProjectStore；预算仍为PlanningBudgetLine，不写入实际收支。
- 项目打开：MainViewModel.OpenProjectPlanningCommand；没有以直接改数据库代替项目命令。测试查询repository仅用于身份读回。
- 日历创建/改期：WorkCalendarPage现有编辑入口、生产BookingEditor及SaveCommand；LinkBookingCommand把既有BookingId写入现有策划状态。
- 日历返回：OpenBookingDetailsAsync和OpenFullPlanningCommand，走生产OpenPlanningRequested接线回到相应项目/策划。
- 收支创建：现有FinancePage.NewExpense/NewIncome、SaveCommand；ReadPlanningExpenses委托实际绑定FinancePage.ReadProjectExpensesAsync，再按项目和Expense查询。
- 预算页读取：真实PlanningCenterView预算按钮Click处理器和同一ReadPlanningExpenses接线。
- 汇总：既有ExportPlanningSummaryDocxAsync，保持15区和原快照/DOCX警告处理；不新建财务/日历/项目模型、不迁移或重置数据库。

证据层级：完整App/MainWindow进程内生产命令和控件事件、独立进程重开、另行CMD/EXE启动各自单列；均不是物理鼠标验收。可分发合成A/B资料仅写E:/Guangjing-BusinessFlow-20261103隔离根，Weather关闭。未扫描或依赖公司/家庭私人实拍素材。

## 3. 原基线真实失败与最小修复

raw/baseline-business-host.trx主链末尾FAIL：切B后PlanningExpenses仍保留A的2条记录，ResourceStatus也保留A成功状态。**该次预算视图的oldExpenseVisible=false，不能据此宣称实际看到了A的旧行**；是状态对象与当前项目不一致。此前创建/策划/关联/改期/导出及baseline独立重开已通过，持久身份关系并未坏，不重写。

raw/baseline-expense-scope.trx三项FAIL：
1. A→B→A后，旧A成功结果覆盖新A读取（STALE_A_BEFORE_SWITCH覆盖CURRENT_A）。
2. A的迟到IOException把B成功状态改成A失败。
3. 空结果没有明确“暂无”状态；同一方法后续权限/断开断言在原基线未到达，不能把未到达断言算成另外已复现的FAIL。

仅3个产品文件：
- PlanningCenterViewModel.LoadAsync在切项目时使读取世代失效并清除旧集合/状态。
- RefreshPlanningExpensesAsync按读取revision、ProjectId和reader引用共同校验；开始时不展示旧成功集合，迟到成功/失败均不能回写。断开、读取中、成功暂无、成功有项和失败各有明确文案。可恢复异常沿既有IsRecoverableProjectError（含SQLite/权限/IO）处理，不使用旧缓存冒充本次成功。
- SummaryExport明确空结果/读取失败；null结果视为失败。仍按项目/Expense去重，预算与实际分开，各币种分组，不跨币种求总和。

没有改Core/Views/AssetLibrary产品文件、数据库schema、快照/包协议、灯台修复、保存失败恢复、recipe/default、RAW/ICC阈值或渲染数学。

## 4. 最终发布绑定结果

发布命令保持Release、win-x64、自包含、WholeAppDiagnosticBuild=true、PhotographerTrialBuild=false、SourceRevisionId=38dd3f92bb2db7238f7f058008887929ec971c93。沿用原会话成功publish，不因接续再重编产品。原CS0067/CS0649警告保留，未隐藏。20个共享DLL逐一与发布目录及原绑定SHA核对，测试宿主SHA见FINAL_RELEASE_TEST_IDENTITY。

**最终13唯一方法/13执行PASS，0FAIL/0跳过**，准确列表见TEST_SUMMARY.json。开发复跑不累计成新的覆盖数量。

| 验证 | 实际结果 |
|---|---|
| 无档期 | 新策划BookingDate为“未安排日期”，不伪装已排期 |
| A/B创建、策划保存、关联档期 | 项目名/正文不同，ProjectId/DocumentId/BookingId逐项对应 |
| 改期和日历返回 | A改至2026-11-12，BookingId/ProjectId不变，返回正确策划和日期 |
| 实际支出隔离 | A为CNY125.50与USD10.00；B为EUR83.00；A的CNY999.00收入不计入支出 |
| 预算/实际/币种 | 预算500/700保留；DOCX按最小货币单位核对，不跨币种混算、不跨项目出现支出正文 |
| A/B瞬时状态 | 切B即空集合+尚未读取；A→B→A旧成功和A迟到失败均不回填 |
| 空/失败/断开 | 明确成功暂无、失败未取得/未用旧缓存、未连接；权限错误为受控reader故障注入，未改真实账本权限 |
| 只读15区汇总 | A/B分别导出并核对项目/策划/档期身份、正文、预算/支出；已有15区编辑/保存/重开、切项目拒绝发布和旧文件保护回归通过 |
| 独立进程重开 | 生产进程15040结束后，30868重开同一隔离根，重新从日历进入A/B并重新读取/导出，关联和正文仍正确 |
| 账本不变 | 导出前后和独立重开后全部FinanceTransaction按Id排序序列化的SHA256一致；这是逻辑账本行摘要，不是SQLite/WAL文件字节恒等证明 |
| 保全/全壳 | 既有快照取消/项目切换不发布、15区构造、全模块导航/内容模板、教程退役均通过 |

完整App主链、独立进程重开和状态故障注入分别见observations。测试未通过修改断言掩盖失败。

## 5. 失败/未执行与下一项具体缺口

历史失败和执行边界见NONPASS.md：原基线4FAIL保留；开发集合中附件路径“/”与Windows规范“\”字面断言失败保留，改用GetFullPath规范化隔离输出根后同一未改测试PASS；接续复跑曾复用已有合成资料触发档期冲突，保留release-final-business-host FAIL，采用全新final2隔离根并增加拒绝复用门禁后13项PASS。没有放宽冲突检测、重置或删除旧夹具。

- 物理鼠标完整操作、每显示器DPI/缩放、多屏/ACM/HDR：NOT_RUN；离屏构造和进程运行不替代。
- 独立Office/WPS实际排版读回：NOT_RUN_UNCHANGED；无WPS探测/进程接管/关闭，不阻断本轮业务身份链。
- 自然照片/RAW/ICC/20MP性能、新算法和数值研究：本轮未做、不重启旧专项。
- 环境：Windows10专业版10.0.19045，GTX1650驱动32.0.15.7270，CIM当前模式3840×2160/32bit；另有MuMu虚拟显示，未取得模式。有效每屏DPI未测，不从分辨率猜缩放。

**下一项具体缺口**：在公司允许的原生鼠标/DPI验收中，实际操作A/B切换→日历改期返回→预算状态→15区导出。没有为通过的主链制造新功能；Office验收继续独立列账。

## 6. 交付身份与启动

- 产品源码：38dd3f92bb2db7238f7f058008887929ec971c93
- 测试支持：d0c88e27291c432ace5dace5db8f1b3a6a8c5772
- 程序ProductVersion：2.3.0+38dd3f92bb2db7238f7f058008887929ec971c93
- 当前分支：home/r55-canvas-photo-preview-20261101；最终文档LOCAL_HEAD/REMOTE_HEAD须从最后远端读回取得，不冒充源码SHA、不自引用。
- 完整诊断ZIP：E:/Guangjing-BusinessFlow-20261103/Guangjing-BusinessFlow-Diagnostic-win-x64.zip
- ZIP SHA256：357D0E9D30BA30CC9C157FE40D66371F6DE7A978E81E8730497286CBA3AFA1A2
- 293文件，ZIP 79709421字节；目录及ZIP每条长度/SHA256读回PASS。所有运行模块/许可文件仍在，不含Setup、测试宿主、合成数据库、私人照片或研究二进制。
- 公司启动：E:/Guangjing-BusinessFlow-20261103/Guangjing-BusinessFlow-Diagnostic-win-x64/Start-Guangjing-Diagnostic.cmd
- 默认固定相邻隔离根：E:/Guangjing-BusinessFlow-20261103/UserData-BusinessFlow；正常关闭重开继续同根。仅需隔离切换时可为当前进程设置绝对GUANGJING_DIAGNOSTIC_DATA_ROOT，不改系统环境，不用日常账本根。
- CMD父进程11292/自有EXE19240启动后日志回读产品SHA正确，CloseMainWindow正常关闭，EXE0/CMD0。只针对新建自有诊断进程，不操控用户应用；这是OS烟测，不是用户点击闭环。

开发输出与合成DOCX留在本地隔离根，Git只提交报告、收据、原始TRX/log及可审查脚本。历史报告/失败不改。大纲和HANDOFF仅前置追加本轮新状态。
