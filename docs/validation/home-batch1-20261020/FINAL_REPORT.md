# 家里批次1：完整回归、策划合同、R3输出隔离

2026-10-20T21:54:01.857818+08:00（Asia/Shanghai）。**诊断版、不能发行。整体PARTIAL，发行BLOCKED。**

## 起点与范围

Fetch/独立ls-remote核实起点为`03b50d150812fe1b3abaf4e85013ce18537e5b4b`，无远端前进。分支home/guangjing-whole-app-sync-20261020，工作树N:/pixart/guangjing-whole-app-home-20261020。
原有16个工作树及211未跟踪文件HEAD/状态/内容hash复验不变；旧Release与基线新包保留。没reset/clean/stash/强推/合并main或功能分支。
本批仅改三个文件：PlanningRichEditor可访问标识、ProductionPeers合同、R3测试隔离/保护断言。Core源码、像素算法及PV4/PV5/PV6未改。

## 实际测试结果

| 阶段 | 发现 | 计划 | 取得结果 | 通过 | 失败 | 跳过 | 漏项/额外/超时 |
|---|---:|---:|---:|---:|---:|---:|---|
| 家里原始Core |1713|1713|1713|1708|0|5|0/0/0|
| 家里原始WPF |1710|1710|1710|1688|1|21|0/0/0|
| 家里原始发布模块 |14|14|14|14|0|0|0/0/0|
| 家里最终Core |1713|1713|1713|1708|0|5|0/0/0|
| 家里最终WPF |1711|1711|1711|1690|0|21|0/0/0|
| 家里最终发布模块 |14|14|14|14|0|0|0/0/0|
| 最终策划专项 |1711总发现/60选中|60|60|60|0|0|0/0/0|

WPF多出1项是新增未读项目覆盖保护测试，不是漏跑。最终发布DLL同父目录再次运行R3专项15/15通过，原项目hash不变。原始Planning失败与R3旧目录复现失败全部保留。两项前后分析见BEFORE_AFTER.md，原名/结果/原因逐条在ALL_TEST_RESULTS.csv与NONPASS_RESULTS.csv。全部原始TRX、发现JSON、计划runsettings、stdout/stderr、summary已压缩封存并回读hash验证；不含私人图片/RAW/数据库/凭据或发布二进制。

## 发布组件绑定

最终源码冻结`2150e7ba5b3b612de0d128f6996a6c17f66ffdb8`。WholeAppDiagnosticBuild=true、PhotographerTrialBuild=false，Release win-x64 self-contained。
构建新目录：`N:\pixart\Guangjing-Home-Batch1-20261020-210958\Guangjing-FullScope-Diagnostic-batch1-win-x64`；旧目录不覆盖。

| 关键组件 | SHA-256 |
|---|---|
| EXE | EFFAD5147EBB291B3D92644A0ED539EFCFCDC65671447FA20ECE7B47B2947F33 |
| WPF | 349BEAEB3BAD0A2C8CAB5AB3BAD05DA238424864B7073CACA2A7C9D54C7C1F63 |
| Core | BBA360286F039A02E295F31046767B7A4404DA93F5C0EE69A2609BD952F1343E |

实际发布组件复制至新测试目录并逐字节校验，共47条组件绑定，见FINAL_BINDINGS.json；原始组件绑定另留BASELINE_BINDINGS.json。
Core源码与基线未变；由于程序集包含本次SourceRevisionId与构建身份，Core DLL hash变化不等于像素算法变化，不用旧DLL替换新DLL。

## 桌面条件与完整壳

最终诊断EXE独立UserData启动，真实加载DLL的hash匹配发布目录，MainWindow十个主域只读UIA存在且正常关闭。首次教程没有实际退出。
两次只读idle观察约6.8/10.7秒近期输入，其他前台应用活跃，不能确认独占；没有发送输入、激活/强抢桌面或绕过限制。工作台/素材库/归片/策划/暗室的进入及返回均NOT_RUN，详见DESKTOP_RESULT.json。
生产peer测试的Invoke/键盘处理器与RenderTargetBitmap均是进程内自动化，不算真实鼠标/桌面截图。

## 仍开放阻断

- Core5个NotExecuted：明确授权真实RAW/空间语料、生产尺寸benchmark、未验证GPU设备；原名均保留。
- WPF21个NotExecuted：真实RAW/相机/官方候选、性能opt-in、明确照片manifest、当前DPI/截图与视觉批准、P2捕获run环境等。没有伪装成通过；其中P2只读集成探测本机无捕获run，较公司20跳过多1项，是本机条件差异。
- DPI证据17项属于独立DpiTests项目，本批没有把它合并进完整WPF数字；本批未跑，不复制旧截图，仍OPEN/NOT_RUN。
- R3视觉差异、物理DPI/桌面与长流程实操未验；P3性能、Fuji、显示转换、V3更广接线/物理校准、WPS/联机真机/在线服务/跨屏/安装仍各自独立批次，不趁本次大改。
- 原设备目录来源、复杂文档混排/WPS等范围限制不因ProductionPeers通过自动关闭。

修复与证据分开提交；正常push原家用分支并独立查询SHA。最终远端读回见会话回报/本地PUSH_READBACK.json。没有Setup，不提供发行批准。
