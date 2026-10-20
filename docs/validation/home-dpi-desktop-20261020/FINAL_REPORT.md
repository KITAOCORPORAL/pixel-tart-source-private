# 家里 DPI 与桌面批次：逻辑证据闭合，物理/点击仍未验收

2026-10-20T22:38:05.613434+08:00（Asia/Shanghai）。**诊断版、不能发行。逻辑DPI自动证据PASS；真实桌面与物理DPI为NOT_RUN；发行门禁BLOCKED。**
主机日志/元数据/TRX保留原时区，本报告不重写原始日志时间。

## 基线与保留

fetch并独立ls-remote确认起点为`4f0850b27b41db0529f4936c5e8e4f6a14f7e629`，远端无前进。
工作树N:/pixart/guangjing-whole-app-home-20261020，分支home/guangjing-whole-app-sync-20261020。
原16工作树、211未跟踪文件的HEAD/状态/内容hash复验不变，批次1诊断ZIP逐字节hash不变；无覆盖旧Release/reset/clean/强推/合并功能分支。

## 91项 DPI 前后真实数字

| 本轮执行 | 发现 | 计划 | 实际结果 | 通过 | 失败 | 跳过 | 缺失/额外/超时 |
|---|---:|---:|---:|---:|---:|---:|---|
| DPI_BEFORE |91|91|91|74|17|0|0/0/0|
| DPI_AFTER |91|91|91|91|0|0|0/0/0|
| RELATED_WPF（完整WPF中定向6类） |1711总发现|29|29|29|0|0|0/0/0|

使用同一DpiTests测试DLL，SHA-256 `387cb351f268563d963f520855f9c52845f1c102d9a75a2c7137275d01e70f78`。适配器原始发现与测试ID/FQN/显示名多重集合一致，断言一字未改。
17项逐项状态见DPI_17_BEFORE_AFTER.csv。具体为4个DPI八状态行、8个页面四DPI行，以及生产器身份/截图与元数据/106状态完整性/合成源不变/物理未测试标记5项。**最后一项通过仅证明仍诚实标physicalDpiManuallyTested=false，不代表物理测试通过。**
原失败全部为manifest或dpi-current路径缺失，保留原始TRX。没有复现产品布局/交互缺陷，不为全绿修改产品。未改src/tests；本批完整WPF未重跑，按“产品缺陷修复才跑完整WPF”条件，仅跑相关29项。批次1完整WPF结果只作历史，不冒充本轮新成绩。

## 本轮生产流程与截图

本轮从起点源码重新publish Release win-x64、自包含：UiReviewBuild=true、WholeAppDiagnosticBuild=true、PhotographerTrialBuild=false，使用既有真实App/MainWindow取证通路。
取证构建单独位于 `N:\pixart\Guangjing-Home-Dpi-20261020-220746\logical-producer-app`；它是带编译期开关的取证构建，**不是批次1普通诊断EXE的字节副本**。共享组件也因SourceRevisionId/编译身份不同而hash不同，完整身份对照见BUILD_IDENTITY.json，不替换批次1DLL。

| 构建 | EXE SHA-256 | WPF SHA-256 |
|---|---|---|
| 本轮逻辑生产器 | 40D04906B752AD1FD744A01BBAF8187D4277CBFA9E1E8627257F9CFE39CF66A5 | 38A8FA811C8A11A3C5AFF126672B6765EFFAE5E85782477A1DBA5CDFC1AF53A3 |
| 桌面指定批次1完整包 | EFFAD5147EBB291B3D92644A0ED539EFCFCDC65671447FA20ECE7B47B2947F33 | 349BEAEB3BAD0A2C8CAB5AB3BAD05DA238424864B7073CACA2A7C9D54C7C1F63 |

106张本轮新状态图：产品12、简化10、素材库交互11、视觉分析7、重复图5、发布10、比例1、画布10、上下文检查器2、逻辑DPI32、分辨率6。
32张DPI图=8状态×100/125/150/200，每张独立进程/独立UserData，上一进程退出后才开始下一张；106张元数据全部passed，合成输入前后hash一致。独立生产器校验通过106/106。

生产器为RenderTargetBitmap；所有图/manifest明确标OFFSCREEN_LOGICAL，physicalDpiManuallyTested=false。主矩阵保持1920×1080逻辑视口，像素尺寸按比例变化，**不是相同1920×1080物理屏切换Windows缩放的人眼测试**。另外6个分辨率组合也只是自动逻辑条件。
本轮没有使用公司/旧截图，没有拍摄用户屏幕。截图完整ZIP在SCREENSHOT_ARCHIVE.json，Git保留全部106元数据、原图索引、32图联系表与原始测试归档；不含UserData/数据库/私人照片/RAW/凭据/发布二进制。
现有DpiTests固定读取artifacts/current-run-visual：在此前不存在的路径镜像本轮新run的manifest与dpi-current，manifest绝对路径仍指原始新run，hash记录于GATE_PATH_MIRROR.json。不是复制历史截图，也没改测试寻址断言。

## 桌面导航实际矩阵

按请求尝试初始化授权Computer Use接口，在只读list_apps即失败：
`Computer Use native pipe is unavailable: failed to connect native pipe: 系统找不到指定的文件。 (os error 2)`。
随后没有换其他输入接口绕过。只读主机空闲观察同时见Chrome刚有输入，后续Weixin空闲约1.7秒；前台仍被用户使用。

因此停止自动输入：退出首启教程、工作台/素材库/归片/策划/暗室进入并返回、工具箱/设置/帮助检查 **全部NOT_RUN**。未以UIA树存在或生产器自动场景导航代替鼠标点击。未新启动批次1EXE抢占桌面；其EXE/包hash保持不变。逐步入口/动作/结果/构建hash/空证据列见DESKTOP_MATRIX.json；人工清单见人工操作清单.md。

## 本批改动及验证

只改 `tools/RC12ProductVisualHarness/Invoke-RC12ProductVisualHarness.ps1` 与同目录README：
- 支持显式已发布EXE路径+SkipBuild，不依赖旧本地构建目录。
- 新输出目录先决条件；拒绝覆盖旧证据，移除删除旧截图/合成图的路径；同目录复跑非零拒绝且manifest hash不变。
- 各进程显式隔离环境，完成后恢复；记录发布组件/生产器hash和离屏/物理未验标签。
- README修正旧84张描述为当前106张，明确逻辑和物理边界。

取证脚本提交 `28c428c69f6639304c8fee4f5210ad6588a95533`；生成时HEAD为起点4f0850b2、产品源不变，实际运行脚本hash与提交脚本一致。证据另行提交。没有产品/测试断言改动，Core像素和PV4/PV5/PV6未动。

## 下一批仍开放

物理100/125/150/200观察、跨屏实际DPI、桌面输入通路、退出教程及八入口实操仍NOT_RUN；等待用户人工反馈/接口恢复后逐步验收，不自动后台重试。
策划15分区深入业务留下一批；R3视觉、Fuji/显示色彩/真机/WPS/外部服务/安装等既有门禁未因此关闭。逻辑91项全绿不等于正式发行通过。
