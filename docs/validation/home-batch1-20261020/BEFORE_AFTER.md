# 两项问题：家里原始失败与修后证据

日期：2026-10-20T21:54:01.857818+08:00，Asia/Shanghai。主机TRX/日志原时区保留。

## 1. 策划 ProductionPeers

原名：`RAWSelectionAssistant.WpfTests.PlanningAutomationContractTests.ProductionPeers_KeyStatesAndReferenceFamily_ExposeRealContracts`。

- 家里首轮完整WPF在v2-navigation / v1-date / create-transition后失败，旧查询`GetName()==策划正文`找不到控件；原TRX、checks、peer-states留在BASELINE_WPF_0，不覆盖。
- 只读原始peer-states显示实际正文为匿名Document，支持Scroll/Text/SynchronizedInput，没有ValuePattern；实际类型为WPF RichTextBox。因此旧纯文本Value合同过时，而产品确有稳定可访问名称/ID缺失。
- 产品仅补`策划正文`名称与`PlanningRichDocumentBody`AutomationId，保留WPF原生TextPattern；没有虚构ValuePattern、没有换正文实现或改色彩。
- 合同改用实际Document/TextPattern，检查可用/可键盘进入/非只读；通过真实RichTextBox写入、FlushPending、保存、退出项目再重开，验证VM正文、富文本包和TextProvider读回。
- 当前15分区均通过实际导航Button的InvokeProvider执行并核对选中分区。灯位图/服化道与道具使用当前名称；原参考图片预览、Enter/Esc、菜单焦点等断言仍在，不删掉改绿。
- 镜头早期PlanningShotRow按钮已退役，改测真实DataGrid逻辑ItemPeer的SelectionItem和Grid、表格键盘入口；ValueProvider修改名称单元格后提交、保存重开验值。
- 原合同在修订过程中暴露“重开后不是编辑态”“视觉行peer不是DataGrid逻辑peer”等过时预期，PLANNING_AFTER_1/2/3原失败全部留档；没有改产品DataGrid选择行为迎合旧按钮合同。
- 最终`ProductionPeers` **PASS，72个逐项检查**。策划相关专项 **60/60通过**；最终发布DLL完整WPF包含该原名并通过。
- 修复提交：`715beed52d3befd35d40e648ee692e9a61af6c40`。

## 2. 暗室 R3 测试隔离

原名：`RAWSelectionAssistant.WpfTests.R3CommandLineReviewTests.TwoExplicitAuthorizedPhotosExerciseCurrentProductPipeline`。

- 家里基线首次运行新输出父目录时通过。保留同目录再次跑原测试类，**13通过、1失败**，异常为“此工作区未读取该工作文件；请先打开或另存新文件，未覆盖。”与公司旧失败同机制；保存原TRX为REPRO_R3_REUSED_OUTPUT。
- 根因是测试把KITAO_R3_CLI_OUTPUT当可覆盖项目目录，每次同照片名重复写review.ptstudio.json；不是项目保存链应关闭的安全保护。
- 仅测试改为在配置输出父目录下新建`run-UTC时间-GUID`子目录；每次独立照片/项目/导出，不删除旧目录，不改生产SaveSessionAsync / ColorStudioSessionStore。
- 新增`UnreadExistingProjectIsRejectedWithoutChangingItsBytes`：未读保存拒绝、错误含未读取、原字节不变；显式读取后才允许保存。
- 同一原有输出父目录两次独立修后运行 **15/15、15/15**；最终发布DLL完整WPF中两照片和新增保护测试通过。最终发布DLL同父目录再次专项运行15/15，四次独立子目录共8个照片流程。原两个固定目录项目hash保持不变。
- 真实照片为前轮明确授权Canon/Sony输入的768px衍生，两照片各验证相纸/相框变化、最后选择优先、显影等于预览、两个独立变体、切回旧定义/像素、保存重开、PNG16/TIFF16/JPEG导出回读。不是新RAW/Fuji/物理相纸标定，不算物理鼠标。
- 修复提交：`2150e7ba5b3b612de0d128f6996a6c17f66ffdb8`。

## 3. 本次原始与最终全量

| 家里执行 | 发现/执行结果 | 通过 | 失败 | 跳过 |
|---|---:|---:|---:|---:|
| 基线Core |1713/1713|1708|0|5|
| 基线WPF |1710/1710|1688|1|21|
| 基线发布模块 |14/14|14|0|0|
| 修后Core |1713/1713|1708|0|5|
| 修后WPF |1711/1711|1690|0|21|
| 修后发布模块 |14/14|14|0|0|

每轮WPF四片使用适配器发现，再以`(testId,FQN,displayName)`多重集合核对全部计划和实际结果：missing=0、extra=0、timeout=0。新增1项是覆盖保护测试。没有继承公司数字。

fail-closed驱动遇到NotExecuted仍返回FAILED，不能把进程退出码解释为1711项中存在产品Failed；原名与具体原因见NONPASS_RESULTS.csv。整体是PARTIAL，不是全部通过。
