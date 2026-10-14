# R6 合成摄影策划连续流程交付

日期：2026-10-14（Asia/Shanghai；合成拍摄时间表使用 Asia/Shanghai）。状态：**代码修复、针对性回归、公司 Release 完成；实机验收 PARTIAL / NOT_RUN**。

## 身份与产物
- 基线：指定远端分支最新提交 `6ece64274853c48ce5e205ea5c13e480be446762`。结束前再次 fetch，远端未前进。
- 独立分支：`fix/kitao-planning-rich-flow-r6-20261014`。
- 独立工作树：`D:/AI AGENT/kitao-planning-rich-flow-r6-20261014`。已存在的 rich-flow 工作树没有复用或覆盖。
- 冻结源码：`526490439dfc4cad26b98627f6b7cbbfd7290c8e`。文档提交可在其后，不改变此发布源码身份。
- 公司 Release：`artifacts/rich-flow/release/win-x64/KitaoPhotoSelector.exe`。
- 完整运行目录：`artifacts/rich-flow/release/win-x64`（必须整体保留，不只复制 EXE）。
- 产品版本：`2.3.0-planning-rich-flow+526490439dfc4cad26b98627f6b7cbbfd7290c8e`。
- EXE SHA256：`548c5946b257d8845d8d2cb9868c4a64a690b9322a43a4ed655c89cdc69738f8`。
- Release win-x64、自包含、非单文件；没有生成安装器，没有启动产品 EXE。

## 同一份合成策划
24 镜，三个场景，2026-10-20 至 22 日；长名称、三行动作、3 张自绘几何参考 PNG、逐镜头引用身份；2 道具、2 设备、3 人员、3 场地、5 时间段（跨午夜夜景拆成两个日期）、3 预算条目合计 CNY 7,800、4 灯具，共22资源。所有人物/联系方式/设备/地点/预算为合成。
进阶正文有标题、图片、表格、48段正文、4显式分页；真实 paginator 得到5页。
固定内容配方位于 `tests/RAWSelectionAssistant.WpfTests/PlanningRichFlowFixture.cs`，通过生产新建策划及新建镜头命令建立。每次身份和时间戳重新生成以隔离测试，内容/数量/参考图像可重现，不声称所有 JSON 字节恒定。
`fixture/fixture-snapshot.json` 是便携数据快照（合成参考路径已相对化），不是能直接投放正式数据库的安装包。`synthetic-recovery.json` 保留原始恢复文件，含当次临时路径，供诊断；不建议直接导入生产项目。

## 连续流程和修复
1. 新建、表格+检查器编辑 → 简单/进阶往返 → 真实分页预览1/3页 → DOCX 导出、修改导回 → 取消/确认 → 重复导回 → 一步撤销 → 保存、新 VM 重开。
2. 表格旧行只合并本事务改变的字段，避免整份快照覆盖检查器新字段；取消行编辑不提交。
3. 重绘前捕获草稿；load/import/undo 使用编辑器代次隔离，退休编辑器停止定时持久化。旧富文本、旧检查器、旧结构编辑回调不能回写新代文档。
4. DOCX 导出支持 RTF 重载产生的 InlineUIContainer 图片，避免有图文档导出失败。
5. 相同导回在当前会话且文档基线未变时不重复生成恢复点/撤销步骤；修改基线后必须重新预览。该幂等标记是会话内的，不宣称跨重启去重。
6. 导回保存失败仍显示/保留新草稿，旧文件不替换，恢复点可读，重试后可撤销。未改造为跨文件数据库事务。
7. 进阶及简单文字编辑器里的 DOCX 按钮改走策划差异/恢复路径，避免通用导入绕开该门禁。
8. 长名称/动作多行、检查器动作支持回车，表格行高度增加；跨日表显示日期和时区；编辑器高度有界并内滚；缩放值重开正确显示；预览白纸底色避免黑字落在黑底。
9. 结构块身份往返保持；移除后的块不会切模式再次生成；DOCX 摄影投影表导回时不叠入普通正文。

## 证据与边界
| 项目 | 结果 |
|---|---|
| 冻结源码 WPF 针对性 | 34 passed / 0 failed / 0 skipped |
| 冻结源码 Core 针对性 | 81 / 0 / 0 |
| 实际发布组件 WPF 同组复验 | 34 / 0 / 0 |
| 实际发布 Core 同组复验 | 81 / 0 / 0 |
| 原 r5 160 逻辑布局组合 | 在 PlanningR5UiTests 中执行；不是160项原生视觉批准 |
| 合成内容截图 | 24工作区 + 2真实主壳XAML离屏，均96dpi |
| 完整主壳 | 生产 MainWindow XAML、10项主导航；合成 ShellFixtureContext；窗口句柄0、无 Show；非完整运行 MainViewModel |
| Windows启动/原生输入/物理DPI/WPS实操 | **NOT_RUN** |
| 全仓全量 Core/WPF | 本批未重跑，不引用上一批数字作为本批通过 |
| 产品视觉批准 | PARTIAL_OFFSCREEN_REVIEW；不是 WINDOWS_VISUAL_APPROVED |

发布7个产品组件逐项哈希与隔离 testhost 内组件一致；测试并不证明 EXE 启动、WPS兼容或真实鼠标命中。测试使用临时合成仓库。最终冻结/发布测试设置进程局部 isolated-runtime；早期诊断未设置该全局覆盖，参考预览/日志可能落入默认应用缓存，未读入用户项目，不将其宣称为完全隔离轮次。

## r5 差异保留
74张 r5 原图当前全部哈希复核一致；`R5_VISUAL_DIFFERENCES.csv` 每张均有状态、差异和证据。继承原16分区状态单独保存，未把旧OPEN改成完成。
状态在左冻结、较高多行行密度、主导航10项、四工具组而非完整Word七页签、无页缩略侧栏/完整页眉页脚/修订批注/协作/完整版本合并、自动地图/天象/设备目录等差异继续OPEN。灯位/道具/预算等无独立r5参考页仍 REFERENCE_GAP。
8张并排图在本地 `artifacts/rich-flow/comparisons`；原型媒体不提交。尺寸/内容/主壳范围不同，不报告像素相似率。

## 重现
在本工作树运行 `tools/planning-rich-flow/Invoke-Regression.ps1`（可指定 Dotnet 与 OutputRoot）。会生成合成策划、DOCX、恢复文件、截图、TRX，不启动产品窗口。发布命令与组件测试方法见 `REPRODUCE.md`。
原失败TRX和后续每轮索引见 `TEST_RUNS.json`，基线失败、冻结及发布通过的TRX另附在 `tests/`。历史诊断图不得代替 `offscreen/` 中发布组件图。

未改 main，未强推，未 reset/stash/clean 或处理其他工作树改动。改动范围为策划实现、DOCX适配、针对性测试及证据。
