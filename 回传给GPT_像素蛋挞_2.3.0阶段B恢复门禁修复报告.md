# 像素蛋挞 2.3.0 阶段B恢复门禁修复报告

生成日期：2026-08-05
修复范围：仅处理 `RecoveredAssociation_AbandonPersistsAndNeverDeletesFile` 暴露的恢复持久化竞态；阶段B功能开发尚未开始。

## 1. 是否修复

已修复。恢复门禁的最终回归矩阵全部通过，原867项测试全部保留，并新增8项恢复、持久化、幂等、文件安全、并发、通知与隐私专项测试，最终测试总数为875项。

## 2. 精确根因

根因位于产品代码的通用任务完成时序，而不是文件删除逻辑：

1. `BookingDocumentWorkflowService.CopyAndAssociateAsync` 通过 `TaskOperationBridge.RunAsync` 执行安全复制和数据库关联。
2. 数据库关联故意失败时，复制输出、`OperationItems=Completed`、`UndoJournals=Pending` 和内存中的 `PendingDocumentAssociation` 均已形成。
3. 旧实现的 `TaskOperationBridge.ExecuteAsync` 在处理器刚产生 `TaskResultSummary` 时就完成 `pending.Completion`。
4. `TaskOperationBridge.RunAsync` 只等待该处理器摘要，因此会先返回调用方。
5. `TaskEngine.RunAsync` 在此之后才把任务从 `Running` 转换为正式终态 `PartiallyCompleted`，保存 `CompletedAt`，并发布完成通知。
6. 重启恢复查询只接受 `PartiallyCompleted` 或 `NeedsAttention`。如果新连接恰好在桥接返回与终态提交之间读取，数据库中仍是最后已提交的 `Running`，查询结果为空，原测试在 `.Single()` 处失败。
7. 文档复制进度旧实现还使用 `_ = context.ReportProgressAsync(...)`。这不是首次失败的主触发点，但属于同一持久化边界风险：未等待的进度保存可能延后运行。最终修复将所有进度任务记录并排空后，才允许处理器返回。

首次门禁失败发生在调用 `AbandonAssociationAsync` 之前，即复制操作返回后立即执行重启恢复查询的阶段。失败测试的临时目录按既有测试生命周期清理，因此该次失败数据库没有被保留用于事后直接查询；调用链审计确认该窗口可读到的最后已提交任务状态为 `Running`。修复后的新连接专项断言已确认返回时状态为 `PartiallyCompleted`，`CompletedAt` 非空，待恢复关联立即可见。

## 3. 是否为产品代码问题

是。产品代码缺少“任务处理器完成”与“任务终态持久化完成”之间的明确等待合同，并存在未排空的异步进度写入。

## 4. 是否为测试基础设施问题

否。测试使用独立临时目录和独立SQLite文件，未访问用户数据库。此前修复的进程级SQLite连接池竞争没有复发。本轮新增显式并行与单工作线程运行配置用于分别验证，不通过关闭全局并行掩盖问题。

修改前复现记录：

- 恢复门禁Debug全量：867项中866通过、1失败。
- 失败单项连续30轮：30/30通过，0失败。
- 所属测试类连续10轮：10/10轮通过，0失败。
- Core全量补充连续10轮：10/10轮通过，0失败。

该结果说明问题是低概率全量时序竞态，不是单项确定性业务逻辑错误。修复依据来自已发生的全量失败、数据库过滤条件和可证明的调用返回顺序，而不是先修改后猜测。

## 5. 修改文件

- `src/RAWSelectionAssistant.Core/Services/Bookings/BookingDocumentWorkflowService.cs`
- `src/RAWSelectionAssistant.Core/Services/Tasks/TaskContracts.cs`
- `src/RAWSelectionAssistant.Core/Services/Tasks/TaskEngine.cs`
- `src/RAWSelectionAssistant.Core/Services/Tasks/TaskOperationBridge.cs`
- `tests/RAWSelectionAssistant.Tests/Version220DocumentWorkflowTests.cs`
- `tests/RecoveryGate.Parallel.runsettings`
- `tests/RecoveryGate.NonParallel.runsettings`

未修改版本文件、Schema迁移、数据库表、联机拍摄代码、安装脚本或发布配置。

## 6. 修复方式

- 为 `ITaskEngine` 增加明确的 `WaitForCompletionAsync` 合同。
- `TaskEngine.WaitForCompletionAsync` 等待对应 `ExecutionControl.Execution` 完整结束。
- `TaskOperationBridge.RunAsync` 在处理器摘要完成后继续等待整个任务执行结束，且终态持久化阶段不被调用方取消令牌截断。
- 文档复制进度改为受控的 `AwaitableProgress<T>`；所有已提交的进度保存均在处理器返回前通过 `DrainAsync` 等待完成。
- 没有使用 `Thread.Sleep`、无限重试、固定延时、测试跳过、删除断言或全局关闭并行。

## 7. Abandon持久化顺序

最终顺序如下：

1. 使用现有文件操作执行器创建副本，刷新到磁盘并完成长度/SHA-256验证。
2. 保存 `OperationItems=Completed` 和 `UndoJournals=Pending`。
3. 数据库关联失败后形成 `PendingDocumentAssociation` 和部分完成摘要。
4. 排空所有已发出的任务进度持久化。
5. `TaskEngine` 将任务从 `Running` 转为现有正式状态 `PartiallyCompleted` 并等待落库。
6. 保存 `CompletedAt` 和最终摘要。
7. 等待完成通知发布结束。
8. `TaskOperationBridge.RunAsync` 才返回调用方。
9. 用户执行“保留文件但放弃关联”时，`AbandonFileAsync` 将对应UndoJournal由 `Pending` 更新为 `Rejected`，并等待SQLite写入完成。
10. 审计写入完成后命令返回；应用重启后的恢复查询不再返回该动作。

任务终态复用 `PartiallyCompleted`，没有新增相近枚举。

## 8. 是否使用事务

没有新增跨表事务。原因是任务终态在Abandon命令开始前已经完整持久化；Abandon只需对单条UndoJournal执行原子的参数化 `UPDATE`。SQLite单条写入自动提交且被完整等待。若该写入失败，异常明确返回，UndoJournal保持 `Pending`，恢复上下文不会被假装清除。

原有文件执行器、操作项和UndoJournal写入策略保持不变。

## 9. 是否仍存在后台未等待任务

本调用链不存在未等待的终态、进度或完成通知持久化任务：

- TaskEngine执行任务由 `WaitForCompletionAsync` 等待。
- 文档复制进度由 `AwaitableProgress<T>.DrainAsync` 排空。
- 终态、`CompletedAt` 和最终摘要均已等待仓储保存。
- 完成通知由TaskEngine等待发布。
- Abandon的UndoJournal更新和审计均被等待。

测试确认命令返回时活动仓储保存数和活动通知发布数均为0。

## 10. 文件是否始终保留

是。专项测试验证：

- 源文件始终存在且未修改。
- 已复制目标文件始终存在。
- 未创建 `BookingDocuments` 关联。
- Abandon后重启不再显示相同恢复动作。
- 重复执行和8路并发执行均保持幂等，只得到一条 `Rejected` 最终UndoJournal状态。
- 数据库写入失败时源文件、目标副本和待恢复上下文均保留。

## 11. 是否调用任何删除逻辑

否。Abandon路径只调用 `AbandonFileAsync`，将UndoJournal标记为 `Rejected`；不调用 `UndoAsync`、`UndoFileAsync`、`File.Delete`、`File.Move`、回收站、目录删除或源文件修改逻辑。

UndoJournal服务中其他由用户明确触发的安全撤销功能仍保留原有删除已创建输出能力，但本次Abandon命令不会进入这些分支。

## 12. 单项50轮结果

- 测试：`RecoveredAssociation_AbandonPersistsAndNeverDeletesFile`
- 最终代码连续50轮：50/50通过
- 失败：0
- 跳过：0

## 13. 测试类20轮结果

- 测试类：`Version220DocumentWorkflowTests`
- 每轮：46/46通过
- 连续20轮：20/20轮通过
- 失败：0
- 跳过：0

## 14. Core并行3轮结果

使用 `tests/RecoveryGate.Parallel.runsettings`，方法级并行、自动工作线程数：

- 第1轮：776/776
- 第2轮：776/776
- 第3轮：776/776
- 失败：0
- 跳过：0

## 15. Core非并行3轮结果

使用 `tests/RecoveryGate.NonParallel.runsettings`，单工作线程：

- 第1轮：776/776
- 第2轮：776/776
- 第3轮：776/776
- 失败：0
- 跳过：0

## 16. Debug全量3轮结果

Debug完整构建：0警告、0错误。

- 第1轮：Core 776 + WPF 61 + DPI 38 = 875/875
- 第2轮：Core 776 + WPF 61 + DPI 38 = 875/875
- 第3轮：Core 776 + WPF 61 + DPI 38 = 875/875
- 失败：0
- 跳过：0

## 17. Release全量3轮结果

Release完整构建：0警告、0错误。

- 第1轮：Core 776 + WPF 61 + DPI 38 = 875/875
- 第2轮：Core 776 + WPF 61 + DPI 38 = 875/875
- 第3轮：Core 776 + WPF 61 + DPI 38 = 875/875
- 失败：0
- 跳过：0

## 18. 最终测试总数

875项。原867项全部保留并通过，新增8项；测试总数没有减少，没有禁用或跳过测试。阶段C文档恢复和文件安全测试均包含在Core全量测试中并通过。

边界确认：Provider仍为None；Release未注册Mock或Fake Camera；输出类型仍为WinExe；产品源码无localhost或127.0.0.1；未开始任何Watch Folder实现。

## 19. 修复提交哈希

`3570db5c693a024f636a541c6bf0d490a11b6655`

提交信息：`fix(recovery): persist abandoned association terminal state`

## 20. 当前分支和HEAD

- 分支：`release/2.3.0`
- 修复及全部门禁完成、报告生成前HEAD：`3570db5c693a024f636a541c6bf0d490a11b6655`
- 本报告将单独提交；报告提交后的最终HEAD以Git状态和最终回传为准。

## 21. 工作树状态

修复提交后工作树干净。报告文件单独创建并单独提交，最终交付前再次确认工作树干净。

## 22. 产品版本

- ProductVersion：2.2.0
- FileVersion：2.2.0.0

未修改版本。

## 23. SchemaVersion

SchemaVersion仍为2。默认迁移仍只有 `InitialSchemaMigration` 和 `CalendarSchemaMigration`，未新增表、未修改迁移。

## 24. 是否开始阶段B

否。没有新增Watch Folder、相机合同、联机UI、Schema 3或任何联机拍摄实现。

## 25. 是否生成安装包

否。未Publish，未生成安装包。

## 26. 是否合并main

否。

## 27. 是否创建Tag

否。未创建v2.3.0 Tag，现有v2.2.0 Tag未修改。

## 28. 是否允许重新启动阶段B

允许。恢复门禁修复、专项压力测试、Core并行/非并行验证以及Debug/Release全量门禁均已通过，可在人工确认本报告后重新下达阶段B开发指令。本轮不会自动开始阶段B。
