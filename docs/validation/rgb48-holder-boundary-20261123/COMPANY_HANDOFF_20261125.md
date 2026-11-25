# 公司一次观测交接 · 2026-11-25

**接续 f5c7f1c4；fetch 后远端公司证据仍为 63a8633d，未发现更新结果。本次复用家庭捕获，新增运行/转储/RAW 解码均为 0；f319 保持 HELD，稳定入口不变。公司未推送现场在家不可见。**

**家庭已回答。** f319 同源、不同二进制、7752×5184 合成 RGB48：第二次 ushort 申请前，首 reader 的 WeakShort 槽为 0；旧 workspace/editor/master 包装/owned 数组/原始及处理 CachedBitmap 六个对象均 `gcroot=0`，旧 editor 四主字段为 null。master→数组不是外部强根。≥100MB 的三类数组仅一份 241118232B UInt16[]，无同量级 Byte[]/Single[]。见 [REPORT](REPORT.md)、[持有时序](OWNERSHIP_TIMELINE.md)、[家庭摘要](HOUSEHOLD_OBSERVATION.json)。

**限制与缺证。** 采集窗口 **16.144049 秒**，GC 前后 `[12,7,4]`，其他线程/原生释放可推进；未申请第二次数组，exit0 不是容量 PASS。公司仍缺旧处理图的根链及 task/closure/continuation/结果对象关系，未量化 WPF/WIC 原生、不可达托管对象与 LOH 提交占比。`KnownTasks` 将 `ThumbnailJob.Completion` **属性当字段读取**；取消/移除不等于完成，**四项任务完成不足以确认全部缩略图结束**。保留原等待与 700ms settling。第二次边界的 null 来自新外层 VM，不能代表旧对象。

**运行要求。** 在本研究分支的**独立工作树根**执行；Windows x64、Python ≥3.11、.NET 10 SDK/WPF、已有 `dotnet-dump 10.0.750501`、输出盘空闲 >3GiB。下列公司路径由原回执解析；若迁移，依迁移回执调整。采集器不在 PATH 时按公司部署回执设置 `$dumpTool` 绝对路径，缺项即停；输出目录必须全新。

```powershell
$ErrorActionPreference = 'Stop'
$receipt = Get-Content -Raw -LiteralPath 'docs/validation/company-precision-integration-20261123/VALIDATION.json' | ConvertFrom-Json
$baseline = Join-Path $receipt.legacyCandidateGuard.path 'LAUNCH.json'
$launch = Get-Content -Raw -LiteralPath $baseline | ConvertFrom-Json
$dumpTool = (Get-Command dotnet-dump.exe -CommandType Application -ErrorAction Stop).Source
if (-not $launch.task.iccSha256 -or (Get-FileHash -LiteralPath $launch.task.icc -Algorithm SHA256).Hash -ne $launch.task.iccSha256) { throw '原 task 的 ICC 摘要缺失或不匹配；保留现场' }
$output = Join-Path (Split-Path $receipt.legacyCandidateGuard.path -Parent) 'rgb48-holder-boundary-company-once-20261125'
python tools/Rgb48HolderBoundary20261123/observe_company.py `
  --baseline-launch $baseline --dump-tool $dumpTool --output $output
if ($LASTEXITCODE -ne 0) { throw '本次观测未完整结束；保留输出及 STOP，不自动重试' }
```

**精确绑定。** 原 LAUNCH 的 `stage/appSha256/coreSha256` 绑定实际 App/Core，核 App PE 为 f319 完整 SHA；不能用候选包摘要替代。输入沿原 `task` 的授权 **0806D680 缓存**、尺寸/方向/ICC，核 payload 全摘要及 `width*height*6`，上面另核 ICC 摘要。**配方来自宿主 Stack()，不是 task**：原63a PV2、WB 9/−4 → BasicTone 0.5/−20/7、`points`。只构建宿主，不重建产品/加载 RAW；双2GiB（2147483648B）Private/OS峰值工作集、50ms、600s不变。第二次申请前采一次 Heap dump即结束，再离线查根。参数及完整SHA见 [README](../../../tools/Rgb48HolderBoundary20261123/README.md) 与 [PROVENANCE](PROVENANCE.json)。

**回传。** 审阅脱敏 `run/ANALYSIS_SUMMARY.json`、CAPTURE_SUMMARY、WEAK_HANDLE_MAP、必要根链/旧editor字段、GC前后/暂停时长、LAUNCH/managed绑定、EXIT/STOP与采样阶段。dump/全SOS/图像/payload/私人路径留公司；全部旧失败保留。

| 结果 | 能说明什么、下一步 |
|---|---|
| 旧处理图有可定位强根 | 有效WeakShort/地址、根非观察器：支持捕获时刻的具体持有者，沿字段/任务/事件链区分合法与错误寿命，不直接称泄漏。 |
| 有效捕获、找不到强根 | 映射/SOS完整、GC一致：仅说明该时刻无可定位托管强根；不排除暂停前引用/未标记图/原生滞留，不将Private差额全归WIC或宣布资源PASS。 |
| 暂停、任务缺项或守卫中止影响 | 明示暂停与缩略图覆盖缺口；GC变化/映射或根查询不全/STOP为受扰动或不充分。不自动重试、等回落、强制GC、清缓存或增预算。 |

**普通TIFF另列。** `LoadSourcePreviewAsync→WIC/_sourceCache→staged prepared→AdoptPreparedSource`，为失败回滚保留旧工作区；与legacy交汇于Editor/视口/分析，非Replay第二次ushort入口。63a首导逐样本正确、prepared重开STOP仍有效，本命令不观测或解释该失败。
