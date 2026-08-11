# 像素蛋挞 RAW 转 JPG 实现报告

## 结论

- RAW 转 JPG 已从占位功能升级为真实 LibRaw 解码、安全输出和可恢复任务链。
- 实际公开样本验证格式：Sony `.ARW`、Canon `.CR2`、Nikon `.NEF`。
- 其他候选扩展只能称为“候选”，未在本轮宣称已验证。
- 源 RAW 始终只读，不移动、不覆盖、不删除。

## 解码与编码

- RAW 解码：Sdcb.LibRaw 0.21.1.7 + LibRaw runtime 0.21.1。
- 输出：WPF/WIC JPEG 编码，8-bit sRGB。
- 默认：原尺寸、质量 90、相机白平衡、SHA-256 校验、有限 EXIF 重建、自动旋转。
- 已修复真实大尺寸图片被错误作为 JPEG thumbnail 导致 WIC `FileFormatException` 的问题；正式编码不再附带全尺寸 thumbnail。
- 许可证和第三方声明随应用发布，包括 LibRaw、Sdcb.LibRaw、libjpeg-turbo、Little CMS、zlib。

## 文件安全

- 统一 TaskEngine，不建立第二套任务中心。
- 输出复用 FileOperationPlan、FileOperationValidator、FileOperationExecutor、FileVerificationService、UndoJournal、NotificationCenter 和 AuditLog。
- 目标使用 Copy、CreateNew、AutoNumber、WriteThrough、Flush；复制后验证长度、JPEG 可解码性和 SHA-256。
- 只有 FileOperationExecutor 返回匹配 ItemId 的 Completed 项才视为本任务拥有输出；竞争产生的陌生文件不会被认领或删除。
- 失败和取消只清理本任务拥有的临时文件；源 RAW 从不进入删除、移动或覆盖路径。

## 恢复与取消

- 请求检查点使用 DPAPI CurrentUser 加密后原子写入本地恢复目录。
- 每个文件安全提交后先持久化模块检查点，再进入 TaskEngine SafeBoundary。
- 终态完整持久化后才清理检查点；终态数据库保存失败时保留恢复数据。
- 取消命令作用于 TaskEngine 的真实运行任务并等待完成，不是只取消入队。
- Interrupted 任务经现有 RecoveryCoordinator 回到 RAW handler；已稳定输出不重复生成。
- NeedsAttention、PartiallyCompleted、Retry 和 UndoJournal 均有确定性测试。

## 输出规则

- 支持多文件。
- 输出目录由用户选择。
- 冲突自动编号，不覆盖既有文件。
- 质量和尺寸参数受模型范围校验。
- 转换完成后，源文件长度、修改时间和 SHA-256 再验证。
- 日志、通知和 Task 输入摘要不记录完整路径或文件名。

## EXIF 与旋转边界

- 当前“保留 EXIF”是有限重建：Make、Model、拍摄时间、Orientation。
- 不宣称完整透传 ISO、快门、光圈、焦距、镜头、版权、GPS 等全部标签。
- 自动旋转已有 90° 和关闭旋转的合成测试；三份真实样本 Orientation 均为 1，因此不宣称覆盖所有镜像方向的实拍验证。

## 真实样本结果

| 格式 | 解码尺寸 | 输出大小 | 输出 SHA-256 |
|---|---:|---:|---|
| ARW | 7968×5320 | 4,379,562 bytes | `88B020BE3798B08FE5EBC599584C8ECF9A6877A3BDAC9FBA4CE4AB14917225F` |
| CR2 | 3906×2602 | 2,819,081 bytes | `0BCE076CBA4504E3CFD63A7938F5231DF0C0E008E544025C250B8727A245CC1F` |
| NEF | 4284×2844 | 3,321,637 bytes | `92981E31D7ACE1E4E2E53BCA1E190F05A1DB3C18BDEC270F7AEACCBE355D684B` |

三份源文件在解码和完整转换前后，长度、LastWriteTimeUtc 与 SHA-256 均完全一致；每个输出均能由 WPF 再解码，尺寸一致，并产生一条匹配 UndoJournal。

真实探针报告位于：

`C:\Users\Administrator\AppData\Local\Temp\pixel-tart-raw-output\actual-chain-fixed-20260811T060342Z\raw-probe-report.json`

公开样本和探针输出未进入仓库、Publish 或安装包。

## 性能说明

- 三个真实相机文件均在默认原尺寸设置下完成全链转换。
- 本轮没有建立统一硬件、冷缓存、多轮统计的性能基准，因此不虚构每秒张数或耗时指标。

## 验证

- RAW Core Debug/Release 专项最终均通过。
- RAW WPF Debug/Release 专项最终均通过。
- Debug 全量：1922/1922。
- Release 全量：1922/1922。
- Publish 含 LibRaw 与全部许可材料，不含真实 RAW、探针产物或测试程序集。
- 安装候选已在隐藏隔离桌面真实点击打开 RAW 转 JPG Modal，`InstalledUiVerified=true`；真实 RAW 文件转换仍以隔离服务链探针和专项测试作为证据，未把用户真实照片带入安装验收。

## 状态

- `CodeVerified=true`
- `AutomatedVerified=true`
- `InstalledUiVerified=true`（Modal 打开与入口）
- `UserVerified=false`
