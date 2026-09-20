# 策划中心摄影提案工作区 Final Product Closure

## 最终状态

**AWAITING LOCAL ACCEPTANCE RUN / 产品尚未达到最终交付门槛。** 2026-09-20 Completion Pass 已补齐独立 Runner、fresh/upgrade 完整计划及自包含一键本机验收包。Release Build 0 warning / 0 error，两份计划离线校验 PASS，23 项离线自测 PASS；没有实机执行，不等于安装版验收通过。

本轮按明确的环境限制采用用户指定的本机包交付路径。产品源码和现有安装器不变；不进入 Stage VI，不制作宣传片，不替用户完成视觉签收。完整本轮记录见 `INSTALLED_ACCEPTANCE_RUNNER_COMPLETION.md`。以下 Recovery Pass 为历史记录，其中“部分实现”已被本轮代码与完整计划替代，实机 NOT_RUN 仍然成立。

## Installed UI Automation — Recovery Pass

2026-09-20：读取恢复指令后复核远端 HEAD `9befb2e`、现有完整报告/索引/视觉审计，确认 `src/`、`installer/` 无后续变化。保持 PRODUCT_SOURCE_SHA `8cb95e6`，安装包 SHA256 再核验一致，没有无意义重建产品。

新增独立 `tools/PixelTart.InstalledAcceptance`：不引用生产 App 项目，采用外部 PID 绑定的 `System.Windows.Automation`，实现唯一元素选择、Invoke/Value/Selection/Expand/Window patterns、焦点检查、屏幕截图、JSON 日志和隔离安装基础代码。Release 编译 0 warning / 0 error；`--validate` 初始计划 schema/path/hash PASS，明确没有 UI 操作。

**该 Runner 仅部分实现且没有 live 执行。** 当前 computer-use 技能要求 Windows 自动化只能通过其 JS API，故未通过 shell 执行自建 UIA、SendKeys、CopyFromScreen 来绕过限制。没有再循环重试已失败的截图/点击接口，也没有据此判定 Microsoft UIA 本身不可用。

- Installed PID / UIA root：本轮无，JSON 中为 null；不复用旧 PID 冒充新运行。
- Installed EXE：仍为前轮隔离安装路径，文件 SHA256 为 `F4FBB25234E253E17229CF3C554B9B19C002E565C62EA4BFABD358C479856EB1`。
- API 异常：本轮未运行直接 UIA，无新 exception；前轮支持接口的 `0x80004002`/coordinate geometry 错误仍是历史证据，不标成本轮重复运行。
- 新 Recovery Fresh Install、创建/七模块/编辑/重启/预览/弹层/PDF/联机/在线选片/正常关闭/完整升级：全部 NOT_RUN。
- Runner 完整工作流尚未实现，初始 smoke 计划仅用于后续验证选择器；不能称验收工具已完整交付。
- 机器可读结果：`docs/implementation-reports/PLANNING_INSTALLED_ACCEPTANCE.json`。
- 安装版视觉记录：`docs/design/PLANNING_INSTALLED_VISUAL_REVIEW.md`，无新截图，NOT REVIEWED。

最终状态仍 **BLOCKED**，不是 READY FOR USER ACCEPTANCE。需要可执行受支持桌面输入/捕获的环境，才能继续运行安装版验收。本轮只新增工具/报告，没有修改产品源码或生成新安装器。

## HEAD 与证据边界

- START_SHA：`be7055285b23fcc5b03bbe2b580493c0e2092bc7`
- PRODUCT_SOURCE_SHA：`8cb95e6d2c4814717dc31a8c6aa19f8b2c89c9cc`
- FINAL_HEAD：本报告所在的文档收尾提交（用 `git log -1 --format=%H -- docs/implementation-reports/PLANNING_PROPOSAL_FINAL_PRODUCT_CLOSURE.md` 解析；最终回传列出实际 SHA）。不能将文档提交 SHA 当产品构建 SHA。
- Branch：`integration/pixel-tart-developer-preview`
- 构建、最终全量测试、截图、PDF、Publish、Installer 均对应 PRODUCT_SOURCE_SHA。冻结后仅新增验收报告与证据索引，没有再改产品/测试/安装脚本。
- 原有未提交 RC12 报告与旧发布目录保留，未纳入本次提交。未 reset、force push、merge main 或改写历史。

## 架构、模块及功能

Architecture Frozen：YES。Two Column Workspace：PASS（真实 MainWindow 集成/软件布局范围）。保留 280 DIP 策划列表、约 940 DIP 正文、七模块固定顺序、按需 360 DIP 抽屉，未重写 Planning/Shot 模型。

| 项目 | 源码/真实 App 集成 | 安装版实际操作 |
|---|---|---|
| 文字、参考图、情绪板、镜头清单、灯光图、服化道、文件（各项） | PASS，生产视图切换、截图及布局测试 | BLOCKED |
| Preview | PASS，隐藏列表/编辑导航，Esc 回归 | BLOCKED |
| Autosave | PASS，切换/即时状态变更后保存、重新载入 | BLOCKED |
| Draft Recovery | PASS，正文和镜头快照恢复，成功后才清草稿 | BLOCKED |
| Legacy Planning | PASS，version-1 JSON optional Document 与旧关系回归 | 未单独人工验证 |
| Calendar / 关联档期 | PASS，生产组合导航和关联回归 | BLOCKED |
| Planning → Tether → Planning | PASS，ProjectId/ShotId 上下文往返 | BLOCKED |
| Reference Color | PASS，所选源建立既有 ReferenceLook，源图保留 | BLOCKED |
| 自由画布 / 灵感板 | 生产引用、离线缓存、视图组合测试通过；不等同完整鼠标工作流 | BLOCKED |
| Global IA / 在线选片导航 | PASS，真实 App 导航，不新增一级参考仿色 | BLOCKED |

具体回归入口：`PlanningDocumentPersistenceTests`、`PlanningWorkspaceLayoutTests`、`PlanningProposalAcceptance.RunAsync`、`RealAppStartupIntegrationTests.ProductionCompositionAndToolCatalog_RealAppLoadedAndNavigated`。
离线源回退缓存、移除只删引用且源文件仍存在均在集成断言中验证。

## 已修内容

- 主视觉按 1/2/3/4+ 数量布局，图片原比例且主次明确；正文空行/空章节收敛。
- 新建、更多、关闭改共享向量图标；弹层圆角、阴影、焦点约束及中文名称统一。
- 七模块导航单行，窄宽度自适应间距；顶栏动作在窄屏分行。
- 参考菜单统一样式/图标/分组，图片选择状态、键盘入口收尾；菜单截图仍有缺口，不宣称视觉验收通过。
- 文件展示类型、来源、更新时间；灯光/造型显示关联镜头；服化道具名组顺序与历史未分组参考兼容。
- PDF 默认 300 DPI，可选 216 DPI；优先原图、防止小缓存超像素放大、显式黑色说明文字、原子写入。
- 升级清理限定安装目录内已发布 runtime 文件，不清用户项目/素材库/策划数据。
- 全量测试发现的旧断言已按实际产品范围修正：现有联机横向底片带及具名 ProposalDatePicker。全局日历保留原生布局，策划日期输入保留三个原生命名部件。没有删除失败测试或临时跳过。

## PDF

- 类型：RASTER，默认 DPI：300，可选：216；Selectable Text：NO。
- 没有引入新 PDF 依赖，没有声称专业矢量输出。
- 测试文件：`artifacts/planning-proposal-final/screenshots/策划案-验收.pdf`
- 23 页；5,617,473 bytes；所有页面图像 2482×3510；提取文字 0 字符。
- 全页联系表和 200% 首张渲染人工查看，无空白页/图片变形/页码中断。实际摄影印刷质量仍需用户照片验证。
- 安装版 `策划案-安装验收.pdf`：未生成，BLOCKED；不得将上述集成文件冒充安装版导出。

## UI 与证据

- REAL APP SCREENSHOTS：YES（生产 MainWindow WPF 视觉渲染）；INSTALLED DESKTOP SCREENSHOTS：NO。
- 1080p、125%、150%、200% 软件布局：PASS；PHYSICAL DPI：NO / NOT TESTED。
- 截图：`artifacts/planning-proposal-final/screenshots/01_文字.png` 至 `15_200dpi.png`。
- `10_参考图右键菜单.png` 未完整呈现菜单，INCOMPLETE；其他 Hover/子菜单/日期弹层/Quick Preview 的完整 DPI 矩阵仍待补证据。
- 编码前审计：`docs/design/PLANNING_PROPOSAL_FINAL_UI_AUDIT.md`。
- 最终逐张自审：`docs/design/PLANNING_FINAL_VISUAL_REVIEW.md`。
- SHA256 索引：`docs/implementation-reports/PLANNING_PROPOSAL_FINAL_EVIDENCE.json`。
- USER VISUAL ACCEPTANCE：PENDING USER。

## 最终源码全量测试

所有下面结果对应同一 PRODUCT_SOURCE_SHA `8cb95e6d2c4814717dc31a8c6aa19f8b2c89c9cc`，不拼接旧 SHA 结果。

| Gate | Passed | Failed | Skipped | 证据 |
|---|---:|---:|---:|---|
| Core Full | 1376 | 0 | 0 | tests/core.trx |
| WPF Full（排除需要单独 Application 的真实启动类） | 1259 | 0 | 1 | tests/wpf.trx |
| 真实 App 启动/组合/完整导航/策划集成（专用 testhost） | 1 | 0 | 0 | tests/real-app.trx |
| WPF 合计 | 1260 | 0 | 1 | 上述两个互补测试进程，同源码 |
| DPI | 90 | 0 | 0 | tests/dpi.trx |

相对路径均位于 `artifacts/planning-proposal-final/`。唯一跳过为 `AssetLibraryP3PerformanceDiagnosticsTests.ThreeSamplesOfPublicBatchCommandsAgainstFresh10128Fixture`，明确 opt-in diagnostic；其他正式 Gate 无跳过。
Release solution Build：0 warning / 0 error。Publish：Release、win-x64、自包含、全新 SHA 命名输出目录，没有覆盖旧 staging。
Core 最终使用项目默认平台完整重建运行；之前 x64 缓存程序集只发现 1331 项，该结果未作为最终全量证据。DPI 也在其项目默认平台重新构建运行。

## 安装包（内部待验收产物，不作为已通过交付）

- 文件名：`PixelTart-DeveloperPreview-2.3.0-dev.8cb95e6-x64-Setup.exe`
- 完整路径：`D:\AI AGENT\worktrees\pixel-tart-developer-preview-rc6\artifacts\planning-proposal-final\builds\2.3.0-dev.8cb95e6\installer\PixelTart-DeveloperPreview-2.3.0-dev.8cb95e6-x64-Setup.exe`
- 大小：51,404,699 bytes（约 49.02 MiB）。
- SHA256：`C2E5F26A1D00BFE2A466450C0BB9D015CDD35D6733E909FE8ADCB5BAEFA6247D`
- Publish 禁止内容扫描：无 Tests DLL、TestHost、Acceptance DLL、PDB、TRX、C#/XAML 源文件。
- 安装日志：`fresh-install-final.log`；安装退出码 0，无需重启。

## Installed Smoke

安装位置：`C:\Users\Administrator\AppData\Local\PixelTart-TestAcceptance\PlanningFinal8cb95e6\installed`。
数据：同级 `fresh-data`，通过明确环境覆盖隔离，不访问正式项目。

| 步骤 | 结果 |
|---|---|
| Setup 真安装 | PASS，退出码 0 |
| 安装目录 PixelTart.exe 启动并存活 ≥10 秒 | PASS，Responding=True，标题含 2.3.0-dev.8cb95e6 |
| MainWindow | PASS，可访问性树有真实全局 Shell；启动日志 STARTUP_OK 含完整源 SHA |
| 再次启动 | PASS；不能把外部关闭算作 Codex 正常关闭验收 |
| 策划七模块、编辑保存重启持久化 | BLOCKED |
| Preview / Esc | BLOCKED |
| 安装版导出 PDF | BLOCKED |
| 联机拍摄往返 / 在线选片 | BLOCKED |
| 正常关闭 | NOT VERIFIED |

原因：Windows Computer Use 截图 API 返回 `SetIsBorderRequired failed: 不支持此接口 (0x80004002)`；点击已观察到的“退出教程”按钮返回 `coordinate input geometry is unavailable`。重新选择窗口后仍失败，Tab 未改变焦点。前轮 Esc 停止后按用户“继续”重新尝试，问题仍存在。没有借助私有 UIA/Win32 操作绕过，也没有将 testhost 的操作伪称安装版操作。

## Upgrade

隔离旧版 `d6cc4f1c07364203471d064428465b2373a49906` → `8cb95e6d2c4814717dc31a8c6aa19f8b2c89c9cc`：**文件级 PASS，完整安装后工作流 BLOCKED**。
位置：`C:\Users\Administrator\AppData\Local\PixelTart-TestAcceptance\PlanningFinal8729d17\upgrade`；测试数据：同级 `upgrade-data`。

- 新安装器退出码 0。
- 升级前后 93 个合成数据文件 SHA256 不变；包含策划、镜头、项目视觉方案、参考色彩方案、画布、设置及缓存等。没有拿真实用户数据做破坏性验证。
- 新 publish 与安装文件逐一哈希匹配，0 mismatch；根目录 0 orphan DLL。
- 日志：`artifacts/planning-proposal-final/upgrade-final.log`；核验脚本：同目录 `verify-upgrade.ps1`。
- 完整人工项目/素材库使用、正常关闭重启及真实用户升级仍不算通过。此轮未把历史 RC12 卸载测试混入当前验收。

## Commits

1. `3e965f0ccd41af16096a1e3db853849728760629`：策划 UI 与高质量 PDF。
2. `8729d17fd0b19c1f2a984f8e12d1c4a8f8189d51`：DPI 联机已发布布局契约。
3. `9936a453fb2ea3e667e2fb30a7c6d43120325a6e`：旧造型参考分组兼容。
4. `1d2f8d3922a40e1cc5f10cea58913c398ec8fa06`：PDF 图片说明对比度。
5. `8cb95e6d2c4814717dc31a8c6aa19f8b2c89c9cc`：日期控件测试范围及原生部件，PRODUCT_SOURCE_SHA。
6. 后续仅文档收尾提交，FINAL_HEAD 见最终回传及 Git。

## 文件清单

- `.cs`：PlanningProposalPdf、PlanningCenterViewModel.Document/References、PlanningCenterView.xaml.cs、PlanningProposalAcceptance、StageVPlanningWpfTests、Version230Rc6RegressionContractTests、Version230StageCLiveMonitorDpiTests、Version230TetherDpiGateTests。
- `.xaml`：PlanningCenterView、Controls.Inputs、Icons.Navigation。
- `.iss`：installer/PixelTartDeveloperPreview.iss。
- `.md`：编码前审计、最终视觉自审、本报告。
- `.json`：证据 SHA256 索引；artifacts 内 planning-dpi.json。
- `.png`：15 张中文命名最终截图及辅助英文截图；PDF 全页渲染/联系表。
- `.pdf`：策划案-验收.pdf（集成测试导出）。
- `.trx`：core、wpf、real-app、dpi。
- `.exe`：上述唯一最终 SHA 安装包；旧迭代包不得代表本轮。
- 其他：build/publish/installer/test/install/upgrade 日志、PDF 复核脚本、升级核验脚本。

## 下一步（不扩展功能）

在独立测试 Windows 上双击 `artifacts/installed-acceptance-kit/运行安装版验收.bat` 并确认 UAC；将生成的 acceptance-result 回传后再检查实际截图和 PDF。若实际证据证明必须改产品源码，则重新 commit/freeze、完整重跑同 SHA Gate 和打包，不能沿用旧证据。
