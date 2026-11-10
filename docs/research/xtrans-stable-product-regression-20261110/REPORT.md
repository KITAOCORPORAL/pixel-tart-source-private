# X-Trans stable-input V1 隔离产品回归交接

日期：2026-11-10（Europe/Helsinki）。状态：**PARTIAL_ENVIRONMENT_BLOCKED**。

诊断功能已冻结，已有数据裁片和无 RAW 回归具备可读回证据。真实产品验收停在暗室的导入文件对话框：当前桌面输入返回拒绝访问，导入未提交，未生成 ProductPathBegin，也未消费本批 RAW 额度。候选继续仅供隔离评估。

## 源码、候选与默认库

| 身份 | 固定值 |
|---|---|
| 已交付产品源码 | 7769b8ee63ad46cd22efb154145766c45b34ba69 |
| 产品分支基线 / merge-base | 2661f2d3fc6d993363d1e30c8b0339ae9fcc238b |
| 本轮诊断功能源码 | a24fac65ae91f3fdef7ff16a58dd462a5b7cbe77 |
| 候选研究提交 | 53e2355d0bad80b364504145780778e165763d94 |
| 候选原生库 SHA-256 | 157BBA02166BDB78E171154E1F4B03AE58F7DF97A110282B37AB3B5988FDC67F |
| 正式默认原生库 SHA-256 | F500C0732FEB21B188D5B52CEA05FD824D5B3C8016EB2CA68D8312ACC9F914B9 |

本次 fetch 已完成，相关产品远端基线仍是 2661f2d3。功能工作树在续接前只有既有未跟踪启动脚本；研究工作树与其他分支保持原样。本次整理只增加工具和文档，冻结的 src、tests、worker 和成片裁片生成器未修改。65 个清单文件逐一 hash 通过。诊断 app 内的默认 raw_r.dll 与已交付产品包逐字节 hash 相同；候选原生库只存在于本地独立 worker，不随本交接提交。

候选需同时满足 XTransStableInputV1DiagnosticBuild=true（同时要求 WholeAppDiagnosticBuild=true）编译属性与 PIXEL_TART_XTRANS_STABLE_V1=1；正式构建仅设置环境变量不能启用。只接受冻结输入与运行清单，缓存键含来源 hash、完整 options 与解码器身份；候选关停或非 X-Trans 路径使用默认解码器。正式接入与许可证准入仍未批准。

## 已保存 P6 与成片裁片

本次再次读回两份候选 P6，均为 7752×5184×3 U16，逐块字节相等；SHA-256 为 9266ca59bc012266a7423d4b5538f7250e1ccca92c2129f3218e80fcd5bdf024。两份旧 P6 的 hash 与已有完整比较回执一致，并重新读取 (2501,792,G)：候选 3053、旧输出 2183，绝对差 870。完整旧统计的来源与本次重新计算范围分别见 STORED_P6_READBACK.json。

P6 只用于定位区域。成片裁片来自相应已保存 MakeMemoryImage RGB16，再调用固定产品 RawMatchTiff16ProductPipeline.Render；look、stack、film 均为空，orientation=1，maximumEdge=0。五个区域各 256×256，候选分别与两份旧输出比较。全部 30 张 8/16 位 PNG 均重新校验 CRC、解码像素和 ICC 字节，15 份 RGB48 与回执一致。本次没有重新解码 RAW。

| 裁片区域 / 中心 | 8 位差异像素：旧1 / 旧2 | 8 位最大通道差 | 成片 RGB16 最大通道差 |
|---|---:|---:|---:|
| 最大 P6 差异点 / 2501,792 | 44 / 44 | 32 | 8107 |
| 纵向接缝 / 2491,1864 | 311 / 311 | 28 | 7041 |
| 纵向接缝 / 1995,2376 | 492 / 493 | 13 | 3564 |
| 带外 / 2500,1347 | 83 / 83 | 20 | 5245 |
| 横向接缝 / 4224,2987 | 479 / 479 | 19 | 4804 |

数值域必须区分：870 是 P6 插值域差异，8107 等为现有产品成片渲染后的 RGB16 差异。8 位结果每个区域最多 493 / 65,536 个像素变化，不能将其描述为完全不可见或与旧输出一致。十组完整统计见 FIXED_CROP_VERIFICATION.json。

实际观察使用无缩放的三列对照：旧1、候选、旧2。细纹、发丝边缘与肤色过渡在五处裁片中未见明显新增的连续拼块接缝、成片大片亮边或成片大片假色；局部点状色值差异仍存在。此为当前样本、当前中性设置的裁片观察，尚无真实暗室全图/缩放/编辑观察，也没有一般摄影质量通过结论。原比例拼图和全部照片裁片仅保存在本地证据根，不入 Git。

所有 PNG 的 ICC SHA-256 为 2B3AA1645779A9E634744FAF9B01E9102B0C9B88FD6DECED7934DF86B949AF7E。这是继承现有产品 profile 的字节身份检查；启动日志还出现 libpng 的 known incorrect sRGB profile 提示。未在本轮独立证明该 ICC 的通用色彩准确性。

## 无 RAW 回归

TRX 已读回，结果是 **61 passed / 1 NotExecuted / 0 failed**，不重复消耗真实 RAW 授权。

| 套件 | 通过 | 未执行 |
|---|---:|---:|
| XTransStableDiagnosticTests：缓存隔离、污染拒绝、旧 recipe、正式构建禁用 | 5 | 0 |
| PV4/5/6 所选核心回归 | 43 | 0 |
| BatchSevenRawProductTests | 4 | 1 |
| BatchTenLegacyRawProductTests | 2 | 0 |
| ProfessionalRawWorkspaceTests | 4 | 0 |
| RawMatchTiff16ProductWpfTests | 3 | 0 |

跳过项为 AuthorizedRawQuickExportAllFormatsNativeDetailAndPublishingRecordRealResults，原因为 Authorized local corpus opt-in required。它不计为真实 RAW 成功。TEST_RECEIPTS_VERIFIED.json 保留原结果、hash 和跳过原因；本次只是读回既有结果，未重跑这些套件。

## 真实入口、TIFF 与资源回执

观察到的可见操作路径为 暗室 → 导入 / 导出 → 导入照片 → 源文件只读对话框。文件名已填写；点击、Return 与 Alt+O 未能提交。Sky 截图错误为 FrameArrived timed out: timed out waiting on channel；模态窗口 Raise 返回 GetCursorPos failed: 拒绝访问 (0x80070005)。系统查询显示 console / Administrator / session 1 / Active，此事实不能判定桌面是否锁定，也不能证明输入桌面可访问。

| 路径 | 状态 | 新消耗解码 | 路径耗时 / 峰值 |
|---|---|---:|---|
| A-cold 首次打开 | BLOCKED_BEFORE_PRODUCT_COMMAND | 0 | UNAVAILABLE |
| A-cache 保存重开 / 缓存命中 | NOT_RUN | 0 | UNAVAILABLE |
| A-rebuild 缓存失效后重建 | NOT_RUN | 0 | UNAVAILABLE |
| B-cold / B-cache / B-rebuild | NOT_RUN | 0 | UNAVAILABLE |
| control-A / control-B 非 X-Trans 默认对照 | NOT_RUN | 0 | UNAVAILABLE |
| 各路径 16 位 TIFF 导出与同设置重复比较 | NOT_RUN | 0 | UNAVAILABLE |

真实 TIFF 的解码像素一致性、ICC、位深均为 NOT_MEASURED。PNG 裁片编码通过不能替代真实 TIFF 导出。原启动进程因等待界面而存活很久；它的 EXIT.json / RESOURCE_SAMPLES.json 仅代表含大量空闲时间的进程生命周期，不作为冷开、缓存重开或导出性能。已核对 PID 24024、启动命令、空 path-evidence、0 consumed 后退出该隔离进程；未关闭其他光镜进程。

## 四项独立结论

- **重复性：** 已存研究 P6 候选两次逐位相同；真实产品冷开、缓存重开、重建和导出重复性尚未执行。
- **摄影观感：** 五处固定裁片可供评审，未见明显新增连续接缝；局部变化与全图/多样本摄影验收仍未消除。
- **兼容性：** 合成缓存/recipe/正式候选关停与选定 PV 回归有通过证据；真实保存重开、关停回退和非 X-Trans 对照未验收。
- **资源开销：** 本轮真实产品路径数据不可用；旧原生研究资源数据仍只是原生研究测量，不能转作产品测量。

## 批次与恢复条件

旧失败批次及 timer-fix 的 2/2 账本均未修改。新任务 RAW-XTRANS-STABLE-V1-PRODUCT-REGRESSION-20261110 总额度 6、已消费 0、剩余 6。分配为 Fuji A/B 各 cold 与 rebuild 共4次，非 X-Trans 两次；cache 与 export 不增加解码。不得清理 ledger、解除 STOP、复用已消费 step 或改写冻结清单来重试。

当前阻塞是可交互桌面访问。桌面输入恢复后，按 RUNBOOK.md 在原冻结批次中创建 A-cold 新 attempt；运行身份和设置不变。发生实际解码失败则停止本批。候选保持隔离，评估结果返回后再决定后续产品接入。
