# P1 全自动验收闭环报告

日期：2026-08-31

仓库：`KITAOCORPORAL/pixel-tart-source-private`

分支：`feature/modular-harness-v1-p1`

指令起始远端 HEAD：`18a8cb2ffe66de68cef12c856730f4309e9631e5`

自动验收代码 HEAD：`27b8811f1911c592483eb3d0eadf209ab13f7940`

交付 HEAD：本报告所属的文档提交；以推送后的 `git ls-remote` 结果为准。

## 1. 最终状态

| 项目 | 状态 |
| --- | --- |
| P1 Automated Acceptance | **PASS** |
| Manual UX Smoke | **OWNER_WAIVED** |
| Historical Manual Gate A | **NOT_CLOSED (superseded as release blocker)** |
| P1 | **CLOSED_FOR_AUTOMATED_ACCEPTANCE** |

这里的 PASS 只表示受控、确定性、可重放的应用内自动验收通过。它不是物理鼠标、物理键盘或真人 UX 烟测证据，也不应被转述为“真人验收通过”。历史 V2/V3 人工 run 仍保持原状，没有被修改、拼接、覆盖或计入本次 PASS。

## 2. 范围和不可越界项

- 素材库仍是像素蛋挞主窗口第 2 个一级模块，route 仍为 `asset-library`；没有创建独立应用、第二窗口或第二套导航。
- 本轮只增加 P1 自动验收 seam、runner、独立 validator、契约和测试；没有开发 P2～P6 产品功能。
- 正式 Schema 5、素材库 SQLite schema v6、表、索引和用户素材行均未修改。
- 没有读取或写入 Eagle `.library`、`metadata.json`、`.info`，没有读取、移动、重命名、覆盖或删除用户素材/源文件。
- 没有桌面级键鼠注入、强制前台、UIAutomation Invoke、坐标盲点或真实显示设置写入。
- 所有运行产物只保存在 ignored `.validation`；本次提交不包含截图、TRX、数据库、WAL/SHM、日志或发布物。

## 3. 自动验收实现

新入口位于 `tools/AssetLibraryP1AutomatedAcceptance/`。其 contract、run manifest 和 evidence 明确声明：

- `validation_mode: automated`
- `owner_manual_ux_smoke: waived`
- `manual_evidence_claimed: false`

runner 使用当前 source HEAD 构建并复制一棵 run-owned 的完整 DevPreview 二进制树；应用通过公开 acceptance seam、WPF Dispatcher 和真实 SQLite v6 repository 执行场景。独立 validator 不信任 driver 写出的 PASS 字段，而是重新核对场景顺序、事件链、证据文件、实际文件哈希、二进制身份、ProductVersion、SQLite 内容、命令次数、清理状态和安全计数。

每轮封存身份一致：

| 对象 | SHA-256 / ProductVersion |
| --- | --- |
| 290 文件二进制树 | `82f93bd1791ceadc5786831993138a797f3edc9265702edcf7cb2386e803d9ae` |
| DevPreview EXE | `5e1feebf00545683a82fc817f95463823a72ad08996d59044ad6512ebe9fa9bb` |
| App DLL | `7f0fe8477dc6a7e98aa04f2433e97363b0acdb54be9be5fe295d774d55fc2375` |
| Asset Library DLL | `55db75e63e63543ebe95bc59b34ef8a96cc595cf4c3ee14354fbf53458551f3a` |
| 三个实际 PE ProductVersion | `2.3.0+27b8811f1911c592483eb3d0eadf209ab13f7940` |

每轮都验证 290/290 文件，无缺失、额外、哈希差异或重解析点；验收前后树哈希一致。

## 4. 场景结果和证据索引

三轮均按下列精确顺序执行 9 个场景、12 个会话，全部 exit 0。每轮共有 518 条事件、12 条 summary journal、18 PNG、18 bounds JSON、12 SQLite，共 48 项场景证据。

以下 `<run-root>` 代表第 6 节列出的任一正式三连 run：

| 场景 | 结果 | 主要证据 |
| --- | --- | --- |
| `first-empty-v1` | PASS；SQLite v6、0 项、attempt 1、ready | `<run-root>/app/evidence/screenshots/first-empty-v1/primary/{loading,first-empty}.png`；对应 `bounds/*.bounds.json`；`databases/first-empty-v1-primary.db` |
| `loading-error-retry-recovered-v1` | PASS；loading → 可恢复错误 → Retry 一次 → attempt 2 → repository 查询 → recovered/0 项 | `<run-root>/app/evidence/screenshots/loading-error-retry-recovered-v1/primary/{loading,error,recovered}.png`；对应 bounds；`databases/loading-error-retry-recovered-v1-primary.db` |
| `organization-splitter-v1` | PASS；最小、最大、中间、边界无变化、方向变化和持久值 | `<run-root>/app/evidence/screenshots/organization-splitter-v1/primary/boundaries.png`；对应 bounds 和 DB |
| `inspector-splitter-v1` | PASS；最小、最大、中间、边界无变化、方向变化和持久值 | `<run-root>/app/evidence/screenshots/inspector-splitter-v1/primary/boundaries.png`；对应 bounds 和 DB |
| `pane-collapse-expand-v1` | PASS；组织栏/检查器折叠展开及重启恢复 | `<run-root>/app/evidence/screenshots/pane-collapse-expand-v1/{primary/primary-collapsed,restart/restart-expanded}.png`；对应 bounds 和两份 DB |
| `thumbnail-slider-v1` | PASS；真实 Slider 路由增加一次并重启恢复 | `<run-root>/app/evidence/screenshots/thumbnail-slider-v1/{primary/slider-adjusted,restart/restart-restored}.png`；对应 bounds 和两份 DB |
| `selection-navigation-restart-v1` | PASS；synthetic fixture 选择、离开返回、重启恢复 | `<run-root>/app/evidence/screenshots/selection-navigation-restart-v1/{primary/primary-selected,restart/restart-restored}.png`；对应 bounds 和两份 DB |
| `navigation-ime-v1` | PASS；7 个一级 route、中文输入路径、清空和返回 | `<run-root>/app/evidence/screenshots/navigation-ime-v1/primary/navigation-ime.png`；对应 bounds 和 DB |
| `layout-dpi-buttons-v1` | PASS；模拟布局/DPI 矩阵和 27 个按钮可读性 | `<run-root>/app/evidence/screenshots/layout-dpi-buttons-v1/primary/{1366x768-1.00,1920x1080-1.25,1920x1080-1.50,2560x1440-1.75}.png`；对应四份 bounds；`databases/layout-dpi-buttons-v1-primary.db` |

每轮的公共证据入口：

- `<run-root>/run-manifest.json`
- `<run-root>/build-manifest.json`
- `<run-root>/app/evidence/summary.json`
- `<run-root>/app/evidence/events.ndjson`
- `<run-root>/app/evidence/summary.ndjson`
- `<run-root>/runner/database-consistency-audit.json`
- `<run-root>/logs/validator.result.json`
- `<run-root>/logs/validator.stdout.log`
- `<run-root>/logs/validator.stderr.log`

三轮场景语义签名一致：`41f5eff05e80721829b6bd823b281c399daae178b898fe9633ff23c8908fd7b0`。18 份 bounds 完整状态逐项一致，几何签名为 `c6542bde577519f07742a6cf49d28d899a098ecce417cf03567e7a9fd28f9069`，完整状态签名为 `6a5087e1e8a8c87f029894a94db6b092b9361edce1d19c253eb95e94d1ab63b5`。

截图差异如实记录：18 张 PNG 中 16 张位级一致；两张 loading 图因确定性流程中的进度动画帧采样时刻不同，最大差异 150 像素（约 0.0143%），且仅位于进度动画区域。各轮 DB 二进制哈希因隔离根和 synthetic GUID 不同而不同，但 schema、记录数、选择恢复语义和 validator 结论一致。

## 5. 构建与测试矩阵

最终矩阵根：`.validation/P1-Automated-Matrix-20260831-202516-27b8811`

| 项目 | 精确结果 |
| --- | --- |
| Restore | exit 0 |
| Debug solution build | 0 warning / 0 error |
| Release solution build | 0 warning / 0 error |
| DevPreview diagnostics build | 0 warning / 0 error |
| WPF tests strict build | 0 warning / 0 error |
| Core | 1192 passed / 0 failed / 0 skipped |
| WPF | 1005 passed / 0 failed / 0 skipped |
| Modular Harness | 14 passed / 0 failed / 0 skipped |
| 历史 DPI | 101 total / 75 passed / 26 failed / 0 skipped；命令 exit 1，失败集合与权威历史基线完全一致 |
| 总计 | 2312 total / 2286 passed / 26 historical failed / 0 skipped |

历史 DPI 权威基线为 `.validation/final-head-33ed7b1/dpi/dpi.trx`，SHA-256 为 `0CFBB7E8C0C4C222421C00CDF9F91BD4EBAF78F89B629BD08E902ED50E65AC6C`。最终 `dpi-baseline-comparison.json` 同时证明：总数一致、101 个唯一测试名无新增/移除、测试名与 outcome 映射差异 0、26 个失败名无新增/移除。因此这 26 项继续归类为既有 JSON 证据缺失的历史环境债，不是本轮新增回归，也没有被删除、跳过或改写为通过。

自动 evidence contract 聚焦测试为 41/41，应用 seam 聚焦测试为 18/18。独立 validator 对 contract 中 25 个具名强制故障、全部 9 个场景故障和 3 个专门的按钮/大小写/边界故障均 fail closed；共 37 次负向验证，并证明验证前后输入树不变。

`DryRun` 返回 `ready-for-automated-run`，HEAD 精确匹配且 DevPreview 进程为 0。`RecoveryTest` 通过，环境恢复，桌面输入注入和显示写入均为 0。

## 6. 正式三连 run

只计入下列在相同 clean HEAD `27b8811f1911c592483eb3d0eadf209ab13f7940` 上新建的三轮。此前 `39236c0` 上的三轮仅作诊断，未被拼接或计入最终 PASS。

| Run root / Run ID | run manifest SHA-256 | build manifest SHA-256 | summary SHA-256 | DB audit SHA-256 | validator result SHA-256 |
| --- | --- | --- | --- | --- | --- |
| `.validation/P1-Automated-Acceptance-20260831-204436-20ebfdd2a02a` / `p1-auto-a4ebbf9bf1c14b60aa26d9599acd114f` | `3d5ef3443964151dce7d6fdcce0564112730d0727a8b9b609aa310a9b25c1ab8` | `461aae7e0ba12853738588d9258c27f161937949a6579ed34ad8589dfdf2ad56` | `21edde8a6cc71b041f29341c31ac4fdf2e129ae539ae9af6a482ebb0eebbdc06` | `f54a47229552578689801b52300de14446c0658a4d044d2fe07661c979372ff0` | `e50130ee2f97429dea725536f16cecb9edceda30ee5f2be02b381b10569f0ed5` |
| `.validation/P1-Automated-Acceptance-20260831-204748-de7cc05c1108` / `p1-auto-988aff050f794fb6ba7b2496799388cf` | `8bbbcc4a1e4da71b5123e0469a0531bbdd582eddd8cb7a79b93d95bef275e4c2` | `25f7a7522deb7e15ab7efc0594ca4334fd5180cbadf98856827df97b31236efc` | `bd158527a00c118298f2645f69a3f03e4dd982cd318fc9f879c1875a4507e1c2` | `debaea6d5987b3bcf6e54e88166504db702da9a5d3fa70afe3d0d4cd32920dcb` | `f36a79cd8720cd1581f67e8e86136d97c1bf1b1976ba3400f9f1bc61bcb56131` |
| `.validation/P1-Automated-Acceptance-20260831-205123-a3c87fce246a` / `p1-auto-e1c1c290d9894097be42fb193b01e02f` | `58963b7c3230cbf875d1349285f6dc30bdfa618b16ce14d3a742e6f00599ca24` | `3588e1907ebd338465de33610eb1c0d64d4b4b436d1d90c99d01393f83a3b59a` | `8d958a0ba8f7e80b525686018db4c64b00fe0ebff15fdf5e8637af2702a88412` | `3e827de1f9447b88ab0f159e7ca6954d7d3ee296156d7f34801af87bf6a93eaf` | `a12fa5f417ff8557cfb16d16adc678b6506808fca30194143e1b4b4c8c93cf73` |

每轮 validator 内部执行 exit 0；每轮随后立即进行一次外部 `ValidateExistingRun`，三轮完成后又并行重验一次，因此外部重验为 6/6 exit 0。三轮共同 validator stdout SHA-256 为 `305881470f818ea476866cb5440909d8790ca87b933f5090edf9b9e51475f99b`，stderr 为空。

36 份 SQLite 证据另以 `mode=ro&immutable=1` 独立读取，全部 `quick_check=ok`、schema v6、计数正确，读取前后哈希未变化。

## 7. 失败证据 → 修复 → 回归

1. 历史 V3 人工 run 在旧脚本 line 1340 读取不到字段：生产端 `Wait-KeyTransitionStep` 返回 `New-ProbeResult` 时丢失已验证 match 的 `Attempt` / `Transition`，消费端 `Get-NewTransitionWidth` 随后读取 `Transition.after_persisted_value`。`18a8cb2` 统一生产/消费契约，要求两个字段成对存在并保留原对象；新增正向、缺失/畸形负向和旧字段兼容性测试：`KeyTransitionResultCarriesTheValidatedMatchIntoTheWidthConsumer`、`KeyTransitionWidthContractRejectsMissingOrMalformedRequiredFields`、`LegacyProbeAndRawMatchFieldShapesRemainCompatible`。旧 run root 和证据未修改。
2. 自动 runner 初版依赖可变的仓库构建输出，无法证明证据引用的是整棵不变二进制树。修复为每轮 290 文件 run-owned 快照，拒绝重解析点，验收前后重新计算全树和三个关键 PE 身份；增加封存二进制身份、篡改、缺失、额外和版本错误负例。
3. 独立 validator 曾只信任 manifest 中的 ProductVersion。修复为直接读取封存 PE 的实际 ProductVersion，并要求三个二进制都与 source HEAD 完整后缀及 manifest 一致；新增伪造 manifest 和实际 PE 版本不匹配负例。聚焦 evidence 测试 41/41。
4. `39236c0` 的首次完整矩阵发现 WPF 1004/1005：测试 seam 的 required fault 名单遗漏两个新版本故障名。`27b8811` 只对齐强制故障列表，seam 聚焦 18/18；随后重新执行完整矩阵和全新三连 run，最终 WPF 1005/1005。旧矩阵和旧 run 继续保留为诊断，不计入最终 PASS。

本轮提交序列：

1. `314f7be` `feat(test-harness): add automated P1 acceptance gate`
2. `c6defd6` `test(acceptance): enforce automated P1 evidence contract`
3. `33ed525` `fix(acceptance): harden automated run aggregation`
4. `ae022c0` `test(acceptance): cover runner aggregation failures`
5. `83328d2` `fix(acceptance): complete automatic text composition once`
6. `68e33ee` `fix(acceptance): align live button evidence contract`
7. `741a474` `test(acceptance): cover WAL evidence immutability`
8. `9565576` `test(acceptance): align source revision seam contract`
9. `115cc33` `feat(test-harness): seal automated acceptance runtime`
10. `a96b3e5` `test(acceptance): cover run-owned binary identity`
11. `39236c0` `fix(acceptance): independently verify sealed product versions`
12. `27b8811` `test(acceptance): align required version fault list`

## 8. 安全、清理和副作用审计

| 项目 | 三轮结果 |
| --- | --- |
| 桌面输入注入 | 0 |
| 真实显示设置写入 | 0 |
| Eagle 读/写 | 0 / 0 |
| 用户源文件读/写 | 0 / 0 |
| 直接宽度 mutation 绕过命令 | 0 |
| 直接 settings mutation 绕过命令 | 0 |
| 直接 SQLite row edit 绕过命令 | 0 |
| 残留 DevPreview / dotnet 进程 | 0 / 0 |
| 残留运行时 DB / WAL / SHM / journal | 0 |
| 环境变量残留 | 0 |
| 显示变化 | 0；每轮前后均为 2560×1440 @ 144 DPI |

静态审计文件为 `.validation/P1-Automated-Matrix-20260831-202516-27b8811/static-safety-audit.json`：危险桌面/显示调用、机器绝对路径、强密钥、密钥赋值、tracked runtime artifacts、runner/validator AST 错误、DevPreview process/CIM 查询和环境残留均为 0；`git diff --check` 和工作树卫生均通过。

## 9. 关闭决定

自动验收三连、独立 validator、9 类场景、构建与 Core/WPF/Harness、故障注入、清理和数据边界全部满足附件关闭条件。因此：

> `P1 = CLOSED_FOR_AUTOMATED_ACCEPTANCE`

历史 DPI 26 项仍保留为历史环境债；Manual UX Smoke 由 owner 明确豁免；历史人工 Gate A 未闭合但不再是 release blocker。下一步可以另行创建 P2 分支，本任务没有创建或开发 P2。
