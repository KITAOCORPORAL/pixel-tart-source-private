# P3 UI 回归接续与交付报告

日期：2026-09-07
分支：`feature/asset-library-eagle-parity-p3-query-metadata`
开始时 HEAD：`56e98ff8be711876f9c54a822fa2b8a16aa046f2`
远端：`source-private/feature/asset-library-eagle-parity-p3-query-metadata`

## 1. 接续边界

本次从当前工作区继续，没有清理、重置或覆盖已有成果。开始检查确认：

- 没有正在运行的项目测试宿主或 `dotnet test` 进程；
- 工作区已有的 P3 产品、测试和诊断改动全部保留；
- 既有数据库证据 44 项、Core 1260 项、Modular Harness 14 项作为已通过证据复用，没有从头重跑；
- 既有 `.validation` 目录、历史 TRX 和诊断样本全部保留。

附件图片中的文字被视为随附文档内容，不改变本次用户请求的执行边界。

## 2. 本轮实现内容

本轮接续的改动聚焦于素材库 P3 的界面状态同步和异步操作可观测性：

- 选择列表使用批量替换与受控 reconcile，降低大批量选择的 UI 抖动；
- scope/query 刷新等待任务、组织源加载、inspector 和 selection generation 互相对齐，避免旧刷新覆盖新状态；
- undo/redo、批量标签与 metadata 操作统一纳入 operation timing 与 journal busy 状态；
- 保留 P3 数据库证据、scope 行为和 opt-in 性能诊断测试及 `tools/AssetLibraryP3Diagnostics`；
- P2 只读证据探针仅在没有真实目录证据、而已有目录是搬盘产生的 reparse/junction 时标记 Inconclusive，不放宽验证器对 reparse 根目录的拒绝规则。

## 3. UI 回归结果

最终回归命令（只针对 WPF/UI 项目）：

```text
dotnet test tests/RAWSelectionAssistant.WpfTests/RAWSelectionAssistant.WpfTests.csproj
  --no-restore --configuration Debug
  --logger "trx;LogFileName=ui-regression-20260907-r2.trx"
  --results-directory TestResults/ui-regression-20260907-r2
  -p:UseSharedCompilation=false -nodeReuse:false
```

结果文件：`TestResults/ui-regression-20260907-r2/ui-regression-20260907-r2.trx`

| 计数 | 结果 |
|---|---:|
| 总计 | 1135 |
| 通过 | 1133 |
| 失败 | 0 |
| 跳过 | 2 |

两个跳过项均为有意的环境/诊断门禁：

1. `ValidateExistingRunAcceptsLatestCapturedP2RunWithoutChangingIt`：当前搬盘工作区的历史 P2 根目录是 reparse/junction，安全验证器拒绝该根目录，因此探针不伪造通过结果；
2. `ThreeSamplesOfPublicBatchCommandsAgainstFresh10128Fixture`：P3 性能诊断标记为 opt-in，未纳入普通 UI 回归。

第一次全量回归曾得到 1133 通过、1 失败、1 跳过；唯一失败正是上述 reparse 根目录环境问题。修正探针选择条件后，单测定向复核为 0 失败、1 跳过，随后第二次全量回归得到本节最终结果。

## 4. 保留的既有证据与未宣称事项

本报告不重新计数或重跑数据库 44、Core 1260、Harness 14，也不把本轮 WPF 回归误写成 P3 正式三轮 run-set 关闭。P3 正式验收仍以既有 `P3_QUERY_SMART_FOLDER_AND_TAG_MANAGER_2026-09-02.md` 中的独立 run/validator 规则为准；本报告只记录当前工作区接续后的 UI 回归和交付状态。

## 5. 交付核验

回归结束后再次检查，未发现 `RAWSelectionAssistant.WpfTests`、`testhost` 或项目 `dotnet test` 进程残留。提交前执行 `git diff --check` 无 whitespace error；提交和推送后的最终 SHA 以交付回传中的 `git log -1` 与 `git ls-remote` 为准。
