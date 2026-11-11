# desktop-02：首次真实导入完成，主应用资源守卫停止本批

日期：2026-11-11（Europe/Helsinki）。批次状态：**STOPPED_RESOURCE_GUARD**。产品验收未通过；本批已停止，后续路径不继续执行。

## 实际完成与停止边界

固定功能源码仍为 a24fac65ae91f3fdef7ff16a58dd462a5b7cbe77，接续交付基线为 6d555cf11de07f7dfe01d23369417f10812cc2ed，研究候选仍为 53e2355d0bad80b364504145780778e165763d94。未重编原生库，未修改 src、tests、worker 或像素处理代码。65 个冻结文件与旧批次账本 hash 均再次核验一致。

用户从真实暗室入口完成 Fuji 导入并确认“已打开”。A-cold/desktop-02 产生且仅产生一次 DecodeReserved，worker PID 30028 正常退出，随后 CacheStored 与 completed=true 的 DarkroomImport 路径结束回执均存在。主进程为 PID 2408。

导入完成后，父应用外部守卫记录 PrivateBytes 超过 2 GiB，写入 ledger/STOP.json 并终止父进程。EXIT.json 为 exitCode=1、resourceGuardTriggered=true。这不是候选原生 worker 的解码失败；它是本轮真实产品进程的资源验收失败，同样触发批次停止。

## 实测资源与时间

| 范围 | 耗时 | WorkingSet 峰值 | PrivateBytes |
|---|---:|---:|---:|
| DarkroomImport 命令（文件对话框提交后，含解码/渲染/元数据） | 9,992.9774 ms | 1167.69 MiB，50 ms 路径采样 | 1155.30 MiB，50 ms 路径采样峰值 |
| 候选 worker，父进程观察 | 6,999.7489 ms | 882.61 MiB | 858.50 MiB，50 ms 采样峰值 |
| 父子同时存在期间，合计采样峰值 | 单列资源；不作为独立命令计时 | 1292.21 MiB | 1265.55 MiB |
| 父应用完整生命周期（含对话框和等待） | 1,357,067.3038 ms；不是冷开耗时 | 1621.85 MiB，外部采样峰值 | **2066.87 MiB，超过 2,048 MiB 守卫** |

本轮“冷开”指诊断缓存未命中；未清空操作系统文件缓存，不作为磁盘冷缓存基准。

完整父进程记录含 26,626 个样本，间隔中位数 50.999 ms。第一个越限样本是最后一个样本：进程采样 elapsed=1,356,635.3285 ms，PrivateBytes=2,167,267,328；对应 OS PeakWorkingSet=1,700,634,624，尚未超过 2 GiB。前一个样本 PrivateBytes=2,006,130,688。故触发指标明确为 PrivateBytes。

采样及 UTC 路径回执足以确认越限在 DarkroomImport 完成后发生。启动 UTC 与单调时钟起点没有原子同步，不据此推定精确的内部阶段时间。现有观测没有分配栈或 GC/图形资源事件，不能断言内存泄漏，也不能将原因归到解码器、恢复窗口、预览、截图工具或某个单一函数。下一步需先定位导入完成后的主进程内存增长与瞬时峰值。

资源曲线位于本地失败现场 PARENT_MEMORY_RESOURCE_STOP.png；PARENT_MEMORY_MAXIMA_1S.json 保留每秒采样最大值，原始 50 ms 数据只读保存在原 attempt 下。曲线的时间横轴包含等待，不能作为产品冷开计时。

## 像素与缓存读回

产品 worker 输出 7752×5184×3 个 U16 通道，共 241,118,208 字节。pixel SHA-256 为 0806D680ACC74FACD093DD2280E1C1A82CD18E40D0C27823D8C6C87CC7E3FC7E。

本次分别完整逐字节比较：产品 worker RGB48 对自身缓存 payload，相同；产品 worker RGB48 对已保存研究候选 MakeMemoryImage RGB16，相同。两项都在读回原文件后检查，不只对比 hash。这证明本次产品解码与相应既有输出一致；只有一次真实产品解码，不能宣称“两次产品冷开一致”或“TIFF 重复性通过”。

冻结候选 native hash 为 157BBA02166BDB78E171154E1F4B03AE58F7DF97A110282B37AB3B5988FDC67F；默认库 hash 仍为 F500C0732FEB21B188D5B52CEA05FD824D5B3C8016EB2CA68D8312ACC9F914B9。缓存来源、完整 options hash、候选身份、尺寸和 payload SHA 与回执一致。解码元数据 Orientation=1、ColorSpace=sRGB 只是解码结果声明；没有 TIFF 文件可核验导出方向或 ICC。

## 各路径结果

| 路径 | 实际结果 | 本次重解码 | TIFF / 位深 / 方向 / ICC |
|---|---|---:|---|
| A-cold | 人工从真实暗室提交导入；worker 与导入完成；随后父进程资源守卫终止 | 1 | NOT_RUN / NOT_MEASURED |
| A-cache / A-rebuild | NOT_RUN_BATCH_STOPPED | 0 | NOT_RUN / NOT_MEASURED |
| B-cold / B-cache / B-rebuild | NOT_RUN_BATCH_STOPPED | 0 | NOT_RUN / NOT_MEASURED |
| control-A / control-B（Sony 默认路径） | NOT_RUN_BATCH_STOPPED | 0 | NOT_RUN / NOT_MEASURED |
| 保存项目 / 两份实际 TIFF 导出 | 未取得相应 ProductPathBegin/End，未执行 | 0 | NOT_RUN / NOT_MEASURED |

这次跟进时诊断窗口已最小化。Sky 未取得截图目标，Open Computer Use 读到最小化窗口；随后资源守卫终止进程，后续报告 appNotFound。未取得实际暗室全图截图，不用旧图、离线渲染或测试截图替代。五处既有固定裁片继续作为旧数据评审材料保留，当前全图细纹、接缝、肤色和缩放观察仍未验收。

## 保留现场与批次结论

FAILURE_SCENE_MANIFEST.json 记录原始 SOURCE_AND_RUN_LOCK、三份计划、一次 consumed 与 worker-entered 标记、STOP、完整内存样本、worker 输出、缓存与日志的长度和 SHA-256。所有原始内容留在本机原路径，未清理或重写；旧失败批次及 timer-fix 2/2 账本保持不变。

本批最大重解码6次，已消费1次，数字上未消费5次。**因 STOP 存在，当前可执行次数为0。** 没有启动 A-cache、重建、B 或 Sony 路径，也未放宽守卫、改写计划或清除 STOP。后续定位与修复须保持本现场；不能直接用剩余5次继续本批。

四项结论分别为：重复性仅取得单次实际产品解码对缓存/研究输出的一致性；摄影观感仍限既有固定裁片；兼容性仍缺真实保存重开和非 X-Trans 对照；资源开销已实测并在父应用生命周期触发失败。候选保持隔离，正式默认解码器继续沿用原库。
