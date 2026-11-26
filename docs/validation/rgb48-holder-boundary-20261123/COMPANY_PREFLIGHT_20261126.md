# 公司 RGB48：第二次申请前的持有者仍未测得（2026-11-26）

**谁持有第一轮的处理预览？在第二次 `ushort[]` 申请前，未知。** 原 `63a` 记录能确认释放前旧 `Editor._matchedImage` 指向处理后的整幅 `CachedBitmap`；本次公司预检在启动宿主之前停止，没有产生第二次申请前的托管根链，不能报告零根或归因给缩略图。

## 本次执行到了哪里

公司 Windows x64 独立工作树，fetch 后研究 HEAD `61ff08bdab3e3abebfc8cdae7ec459935f6e6fc7`，公司集成证据 `63a8633d437ca58966bf8b6bb82acfa287c54caa`。[交接命令](COMPANY_HANDOFF_20261125.md)指向的 `VALIDATION.json` 与 `63a` 是同一个 Git blob。已查公司交付目录、该实验可能使用的两个输出位置、已登记工作树和本地未推送实验提交；在所查范围内未找到已完成的公司捕获或旧半成品。两个相关交付根的六份 `LAUNCH` 中，仅一份匹配原进程、启动时间和 App/Core。

2026-11-26 **07:28:51 UTC**，使用公司已有采集器执行交接 PowerShell；`if (-not $launch.task.iccSha256 -or …)` 的第一项即为真。**原 task 有 `icc` 路径但没有 `iccSha256` 属性**，原始 `LEGACY_TASK.json` 与 LAUNCH 内 task 内容相同。抛错为“原 task 的 ICC 摘要缺失或不匹配；保留现场”；实因是缺字段，未发现文件摘要冲突。原记录未回填，未直接调用内层 Python。预检日志在公司独立新目录，原计划捕获目录执行前后均不存在。

| 本轮证据 | 结果 |
|---|---|
| 原命令入口 / 目标启动 / Heap dump / 离线 `gcroot` / 新 RAW 解码 | 预检 1 次 / 0 / 0 / 0 / 0；未重试 |
| 捕获时刻、边界暂停、GC 0/1/2 前后、采样及峰值 | 均未测；上面的 UTC 是预检停止时刻。没有新 `LAUNCH`、弱槽映射、采集摘要、EXIT 或资源 STOP |
| 守卫 | 方案仍为 Private 与 OS 峰值工作集各 2,147,483,648 B、50 ms、600 s；本轮未启动守卫。未修改等待、缓存或 GC |
| 执行条件 | Python 3.13.13 x64；工作区 .NET 10.0.302 / WindowsDesktop 10.0.10；已部署 `dotnet-dump 10.0.750501`，实测 `--version` 匹配。输出盘预检约 414.95 GiB 可用，要求 >3 GiB；预检时未发现并行产品验收或构建进程 |

## 绑定与 ICC 交叉核对

以下是对公司原回执及其指向文件的实读；App/Core 用原 `legacy` stage 核验，未拿候选包或家庭构建代替。原 stage 的 241 个托管清单条目全部匹配。两程序集 PE ProductVersion 均为 `2.3.0+f3190f4470996232aa63477838b8979d66dd8e45`。

| 对象 | SHA-256 |
|---|---|
| 公司原 `LAUNCH.json` | `518b78bacca0059cfc4fcfd22d4b11e6daa6158f34dc0875b05c9f434fd62d11` |
| 原 `KitaoPhotoSelector.dll` | `80f8ab6cbc562c7c479fc4d9fe0af0d422e07a4b137de21b609710a5df4f59f0` |
| 原 `RAWSelectionAssistant.Core.dll` | `8cf99b0750314cbde3ebdd03d3b7505b3d24e6fac76667172ac4bc91c6ad9187` |
| 原 `LegacyExportMemoryHost.dll`（非新宿主） | `2bcde7076150508c2b7aa3a2a0821b810adc390b65d1405f29b5cb99d50643f9` |
| 获准的 RGB48 缓存，7752 × 5184 × 6 = 241,118,208 B，方向 1 | `0806d680acc74facd093dd2280e1c1a82cd18e40d0c27823d8c6c87cc7e3fc7e` |
| 原 LAUNCH 路径指向的 **当前** ICC，3,144 B | `2b3aa1645779a9e634744faf9b01e9102b0c9b88fd6deced7934df86b949af7e` |
| 本次私有预检回执（原件留公司） | `9bb7ce4f41a55e298bc37620f1b20a7b446795c3e1015006db8a26d82aef622e` |

**同一次旧运行的独立锚点：**`63a` 首轮 legacy 导出的 TIFF 全文件 SHA-256 为 `12b98366044307542befef302625bf654ace400937e979bb03f0d8e2e2ff3034`，匹配当时已完成的导出侧车 `OutputHash`。从首个 IFD 单独读取 ICC 标签 34675（类型 7）：3,144 B，摘要与上表相同，无需解码图像。另一组公司历史任务也记录了相同 ICC 摘要，但其 stage 的 App/Core 不同。两项交叉证据均不是原 `LAUNCH.task.iccSha256`；尤其是 `points` 回放声明 `sRGB`，导出配置一致不能反推出旧宿主当时读取了指定 ICC 文件。

配方来自[诊断宿主 `Program.Stack()`](../../../tools/Rgb48HolderBoundary20261123/Program.cs)，和 [`63a` 原宿主](../../../tools/LegacyExportMemory20261122/Program.cs)的定义逐字相同：`points`、PV2，白平衡 temperature 9 / tint −4 → BasicTone exposure +0.5 / highlights −20 / saturation +7。不是从输入 task 或普通 TIFF 的 +0.1 配方推断。

## 已知持有者与证据边界

- **释放前已证实。** [原公司事件](../company-precision-integration-20261123/LEGACY_EVENTS.json)中，首轮 100%（2026-11-23 05:55:25.918 UTC）、导出完成和释放前任务快照（05:56:01.824 UTC）均有 `Editor._matchedImage → CachedBitmap`，旧日志身份号 `44070913`，7752 × 5184、24 bpp，最少 120,559,104 B。这是直接字段的观测区间，不是 Dispose 后的强根寿命。
- **第二次申请前未证实。** 同一旧运行在 05:56:01.889 UTC 记录 `before-ushort-allocation-2`。此时记录的是**新** VM 的空字段；旧弱引用的 `IsAlive`、或此时旧日志的 GC `[19,9,4]` 都不是旧图的根查询。该次[历史守卫](../company-precision-integration-20261123/legacy-STOP.json)在这个阶段采到 Private 2,176,217,088 B、OS 峰值工作集 1,642,942,464 B 后停止。家庭捕获的暂停、GC 和零根结果均不移作公司数据。
- **缩略图盲点仍在。** [宿主 `KnownTasks`](../../../tools/Rgb48HolderBoundary20261123/Program.cs)用 `GetField("Completion")`；[产品 `ThumbnailJob.Completion`](../../../src/RAWSelectionAssistant/ViewModels/ReferenceColorWorkspaceViewModel.Thumbnails.cs) 是属性。取消可以先从字典移除，异步渲染到 `finally` 才 `TrySetResult()`；四项 `analysis/detail/lightPrint/framePhotos` 已完成不证明所有缩略图作业都结束，也不证明其中某个作业持有 `_matchedImage`。原等待和 700 ms settling 保持，未为补证延时。

**普通 TIFF 单列。** [公司普通 TIFF 回执](../company-precision-integration-20261123/REPORT.md)的首份 +0.1 曝光导出逐行比较 5,184 行、120,559,104 个 RGB 样本零差；同进程打开保存的快照时，[另一次守卫](../company-precision-integration-20261123/ordinary-STOP.json)采到 Private 2,203,717,632 B。它走 WIC / prepared 工作区替换，此次 legacy 预检不能解释或解除该失败。`f319` 继续不准入，稳定入口仍为 `f84f8b2d`。

**只剩的决策：**是否允许以“原 LAUNCH 摘要固定 + 同次首导侧车校验及内嵌 ICC 摘要 + 当前指定 ICC 全摘要一致”作为缺字段的替代前置条件，原 LAUNCH 依旧只读。未获此决定前，下一步停在 `observe_company.py` **之前**；未申请第二次数组，也未取得三轮重开验收。
