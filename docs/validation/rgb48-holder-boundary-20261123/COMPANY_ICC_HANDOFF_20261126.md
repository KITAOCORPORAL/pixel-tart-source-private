# 公司下一次 RGB48 观测：先固定本次输入

上次公司运行在启动宿主前因原 `task.iccSha256` **缺字段**停止（[914ee867 的现场回执](COMPANY_PREFLIGHT_20261126.md)）。该停止保留。以下是本次批准的新观测入口，未执行公司捕获。

在公司 Windows 的 `research/rgb48-second-open-holders-20261123` 更新后，从独立工作树根只执行一次：

```powershell
python tools/Rgb48HolderBoundary20261123/observe_company.py --company-63a
```

命令从 [原 VALIDATION](../company-precision-integration-20261123/VALIDATION.json) 的 `legacyCandidateGuard.path` 找到原 `LAUNCH.json`，用其 `stage`、`task.payload`、`task.icc`。只在该 run 的 `export-first` 目录取唯一 TIFF 和相邻的 `<TIFF>.kitao-export.json`；不搜索其他项目。使用 PATH 或用户 `.dotnet/tools` 中已部署的 `dotnet-dump.exe`（10.0.750501），非标准工具位置可在同一命令追加 `--dump-tool` 及公司部署回执中的绝对路径。Windows x64、普通 Python ≥3.11、.NET 10 SDK/WPF，盘上须有 >3 GiB 空间。

输出为原公司交付根旁的新目录 `rgb48-holder-boundary-company-once-20261126`。若已存在，先读现有结果，命令拒绝覆盖。资料或校验不足时保留回执，不改证据、不更换输入重试。

| 原 task | 放行条件 | 新回执 `iccBinding` |
|---|---|---|
| 有 `iccSha256` | 指定 ICC 全文件 SHA-256 必须相等；空、null、格式错误或不符均停，不转去查导出。 | `LAUNCH_TASK_SHA256` |
| 没有该键 | 仅限原 LAUNCH 文件字节摘要 `518b78ba…`：核现场固定 App/Core 及各自 LAUNCH 摘要、两份 f319 PE 版本、授权 0806D680 缓存全摘要、7752×5184×6 字节和方向1。首导 TIFF 全文件摘要须为 `12b98366…` 且等于原完成侧车 `OutputHash`，侧车 `OutputPath` 指该文件。首个 IFD 唯一 34675 标签须为类型7、3144字节，摘要为 `2b3aa164…`，与当前指定 ICC 长度和内容相同。 | `CURRENT_INPUT_FIXED_BY_SAME_RUN_EXPORT` |

两条路径都实读 App/Core、缓存并核对各自 LAUNCH 的摘要。**`--company-63a` 另强制匹配公司原 LAUNCH 字节、固定二进制与缓存摘要**，不可给它添加一个“正确”的字段。完整固定值见[公司原回执](COMPANY_PREFLIGHT_20261126.md)和[预检实现](../../../tools/Rgb48HolderBoundary20261123/company_preflight.py)。仅读标签元数据，不解码首导 TIFF。新宿主只沿用 PV2 `points` 原配方。

输入通过后才编译小宿主。首次预检在新目录写 `INPUT_PREFLIGHT.json`；构建后、启动守卫前再核实际暂存的 App/Core 和证据，将刚验过的 ICC 字节保存为新目录里的 `observation-input.icc`，仅新任务引用它。其 `observationIccSha256` 是**本次**绑定，不补写历史字段。若证据变化，保留 `PRELAUNCH_STOP.json`，不启动目标。捕获摘要也附 `inputPreflight`。

**结论边界：当前输入已固定、由同次导出交叉核对。** `points` 的导出可能使用相同 sRGB 配置；这条证据链无法反推旧宿主实际读取过哪个 ICC 文件，也不证明历史输入完整复现。公司旧处理图在第二次申请前的持有者仍未知。

公司目标仍只到第二次 `ushort[]` 申请前：保持 Private/OS 峰值工作集双 2 GiB、50 ms 采样、600 s 上限，采一次 Heap dump 后结束，再离线查根。不以预检、退出0或暂停期间无根作为大图重开通过。四项任务完成仍不能覆盖缩略图，普通 TIFF prepared 的原 STOP 另列。只回传脱敏的 `INPUT_PREFLIGHT`、`run/CAPTURE_SUMMARY`、`run/ANALYSIS_SUMMARY`、实际 GC/暂停及 EXIT/STOP；若提前停止，回传已产生的回执和原因即可。完整 dump、图片、ICC、原路径留公司。

[家庭夹具回执](HOME_ICC_PREFLIGHT_TESTS_20261126.json)：只验证预检逻辑及命令接线，未构建产品或宿主、未启动守卫、未采集、未新增 RAW 解码。
