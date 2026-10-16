# 即时成像 V3：阶段交付，不是产品放行

日期：2026-10-16（Europe/Berlin）；宿主日志可显示次日亚洲本地时间。分支 `feature/kitao-instant-film-evidence-v3-20261016`，起点 `fe73d02cf85df518c4eaebf4a09f9a5157451eab`，已 fetch 并确认原基准远端未更新。未合并 main、未改旧 Release、未提交原件/图片/数据库/DLL。

## 产品已实现

- Core 增加可测试的证据资料模型：原始提供者字段、来源文献、图像扫描版本、关系、观察、分用途等级和独立许可状态。拒绝重复 ID、悬空关系、错误哈希、来源组冒充配对、未经受控测量的标定/数值拟合声明。
- 已审计 InstantFilm.cs、InstantFilmJpegInput.cs、ReferenceColorWorkspaceViewModel.InstantFilm.cs，以及版本、纸框、ProfessionalRawPipeline、ProfessionalRawExport、WIC row encoder、资源账本和原测试。
- **生产显色、空间、乳剂和 UI 没有升级到 V3**。八种旧预设、V1/V2、PV4/5/6 不暗迁、不改处理版本。新增候选程序集只被研究工具和候选测试引用。产品版本仍 2.3.0。

## 研究证实（限本次可核查事实）

- Grok 原 ZIP 11,952,399 bytes，SHA256 `42a13ac29a9df5ca3627890f81b015dd6f491852fcdbba3f15ed859466031f8b`，中央目录/CRC/10 张完整 JPEG 读取通过。22 条清单，10 图像+12 网页文献，JSON=CSV，10 个不同图片哈希全部匹配。不是 8 MiB 截断件。
- Muse 独立文件未找到，因此双库合并未完成；41 条不能冒充已收到。原始日期 2026-10-17 晚于本次用户日期，保留并标记。用途审核、相反证据与未知项见 LIBRARY_AUDIT.json/INTAKE.md。
- 读到三张 ICC 的实际字节/哈希，但未解析 ICC；未断言偏色原因。许可声明未复核，图像再分发全部 Unknown，不等同禁止，也不等同允许。不入 Release。
- 新 Sony 单独登记：DSC08015.ARW，SONY ILCE-7M4，67,817,472 bytes，SHA256 `1FE22BE78C66AB0DDCCF753D10187F142B980146FFF7F5D188533905B7F38B93`。隔离副本的旧产品全尺寸 TIFF16/快速 PNG16、PV4 TIFF16/PNG16 回读均通过，原件与副本校验未变；不替代 DSC09916 专属验收。
- Canon 原件 SHA256 `B7D358517DB0CD5B5C54215AA089A5A30C79234AEF50E9381931128493E33401`；Fuji 原件 `52D054ED9A90306DB0B7EB5D6C408A2E006532B976AC88ED792692F9F744E865`。新候选只读其已冻结产品 RGB16 TIFF，不以 Fuji 旧解码重复性作基准。本次不重新解码 Fuji。
- 固定版本开源文件和许可证已实际读取、哈希并定位入口，见 PINNED_REFERENCE_READ.json。发现 RawTherapee grain 来自 darktable 移植，不能凑成两个独立参考。**没有执行两个成熟实现的数值对照**。

## 创作假设／可运行研究候选

- 独立 AlgorithmVersion=3：现代 i-Type/600 共用响应；现代中性黑白明确线性亮度；SX70 创作变体；Reclaimed Blue/Blue Duochrome 分家族/映射；其余只做 ResearchOnly 状态，没有空可用入口。
- EOTF -> 线性曝光/趾/肩/黑位/表观密度/饱和色 -> 空间 -> 乳剂 -> OETF；相纸/扫描单独控制。没有真实化学浓度或标定 LUT 声明。
- 空间核 `[1,4,6,4,1]/16` 二维可分离 binomial，半径 2 源像素、夹边、全局 halo 采样；分块 scratch 424,320 bytes 默认计入资源账本。未创建空间节点全图 scratch。平坦场/脉冲/局部与不同分块精确一致。
- 多尺度 keyed noise，固定根种子/全局归一坐标，微结构与显影缺陷/过期分别控制；原创纸基、纸面划痕、扫描尘点分层；默认无坏片缺陷/过期/尘点。
- 20 组真实产品编码对照：Sony、Canon、冻结 Fuji、合成，各五种效果。照片最长边 512 的产品解码预览输入，**不是全尺寸新引擎**。每组有原图、旧版、新候选、完整 JSON/版本/种子、1:1 审阅尺寸局部图、参考 ID/限制、耗时/进程内存及 16-bit 亮暗统计。20 新候选+20 旧版的 TIFF16/PNG16 全部回读码值/尺寸/ICC 检查通过；80 份整图/局部统计。逐次重开 JSON 一致。
- 候选单次渲染约 13.5–215.2 ms（审阅尺寸、非完整任务/非全尺寸性能承诺），20 次合计 1558.9 ms。日志含导出全耗时、分配量、进程 private/peak WS；非进程全程精密峰值采样。留存资料见 REVIEW_OUTPUT_SUMMARY.json。
- 本机目视检查首轮 Sony/合成联系表（最终轮像素算法未改，补固化家族/年代字段与资源记账）：暗图现代候选明显抬黑，SX70 与现代差异偏小，蓝调在低照度图上仍很强。这些需人工审美判断，不是“像真实相纸”的证实。没有因目视调整已验收输出。

## 测试与构建（原始失败保留）

- 原专项基线：14/14 通过；资料模型专项 2/2；最终旧引擎+证据+空间专项 16/16；Python 资料防错 2/2；最终候选 6/6。
- 完整 solution 回归：Core 1703 通过/7 跳过；WPF 1604 通过/24 跳过；ModularHarness 14 通过；DPI 74 通过/**17 失败**。总计 3395 通过、17 失败、31 跳过。完整回归不是全绿。
- DPI 17 个均为新工作树缺失既有视觉验收资料文件：current-run-visual manifest，以及旧手工验收资料。独立重跑仍同样 17 失败。没有复制别的工作树资料伪装本轮验证，也未修改/跳过这些断言。
- Core 最终完整独立重跑 1703 通过/7 跳过。WPF 完整运行 9m12s 正常结束。首次 solution 多项目共用 TRX 名称，最终 TRX 只含 WPF，原 console 保留；DPI 独立 TRX 与最终 Core 单独 TRX 补足可追溯性，不声称被覆盖 TRX 已保留。
- Release x64 产品构建 0 警告/0 错误；候选/审阅工具构建通过。早期候选 lambda 遮蔽与审阅工具初始化语法两次构建失败均已修复，原日志保留。不把后续成功抹掉首错。

## 未运行／未完成

1. Muse 接收/跨库语义去重/独立来源和许可页核验。
2. 两个真正独立成熟实现执行、真实 RAW/ICC/负值/超范围逐阶段数值比较、许可证复用批准。当前只完成源代码阅读，不满足仓库生产准入。
3. V3 生产管线/现有 UI 参数接线、项目保存重开、原尺寸预览和 1:1 同配方、新节点流式导出一致性。
4. 新候选全尺寸 Sony/Canon/Fuji 性能、运行中取消/压力峰值、整图后缩放的产品预览一致性。测试仅覆盖前置取消/异常释放/预算拒绝，不能冒充运行中取消。
5. 真实灰卡/色卡或色管受控相纸闭环、物理相纸定标、真实化学参数、真实光学 PSF。
6. 当前桌面物理 DPI/原生交互、人工审美批准。只读图像检查不等同最终人工评价。

## 发行阻断

- V3 两成熟实现准入仍 OPEN，故不接产品、不发布候选二进制。
- 完整回归 DPI 17 失败未关闭。
- 原有 P3 性能与 Fuji 原生解码重复性继续 BLOCKED，本分支测试不能替代或放行。
- 当前任务的“产品可运行新引擎”目标**未全部达成**；已交付可运行研究候选和真实产品编码审阅输出，不宣称完成生产升级。

## 本地审阅及复现

工作树 `N:/pixart/kitao-instant-film-evidence-v3-20261016`。
本地输出 `.validation/instant-v3/visual-review-final`：四个输入子目录各含 comparison.jpg、五张局部 detail.jpg、TIFF16/PNG16、配方；review.json 与 crop-statistics.json 为完整原始数据。私人照片不进 Git。

- `dotnet test tools/InstantFilmCandidate.Tests/InstantFilmCandidate.Tests.csproj -c Release`
- `python tools/instant-film-v3/test_audit_library.py`
- `dotnet run --project tools/PhotoEngineHomeAudit/PhotoEngineHomeAudit.csproj -c Release -- instant-review <严格输入清单> <全新输出目录>`
- `python tools/instant-film-v3/review_sheets.py <输出目录>`

输入清单含 Id/FrozenTiff/Original/ExpectedOriginalSha256/ExpectedFrozenSha256。为防覆盖输出必须新目录；原图与冻结文件 SHA 必须匹配。无浏览器/WebView/CDP、无自动打开浏览器/资源管理器。研究引擎没有发布到旧 Release。
