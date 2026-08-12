# Pixel Tart Visible Feature Audit — RC3 Core Reliability

审计基线：`feature/pixel-tart-product-redesign`，SchemaVersion 5。状态只允许 `ProductionReady`、`NeedsVerification`、`PreviewDisabled`、`Hidden`。`ProductionReady` 必须同时有真实文件与普通前台安装版证据。

| 可见功能 | 当前状态 | 真实动作 / 失败说明 | RealFileVerified | InstalledUiVerified |
| --- | --- | --- | --- | --- |
| 本地分片 / 归片 | NeedsVerification | 工作台进入；匹配、FileOperationExecutor、磁盘文件、报告和项目重开必须一致 | 是：3 条选择、3 JPG + 3 RAW、磁盘 6 文件 | 否，等待 DevValidation 前台验收 |
| RAW 转 JPG | NeedsVerification | LibRaw → sRGB → JPEG → CreateNew/AutoNumber → Flush → SHA → 再解码；失败卡显示阶段和脱敏技术信息 | 是：真实 Sony ARW 生成 7028×4688 JPG，源长度/时间/SHA 不变 | 否，等待 DevValidation 前台验收 |
| 批量压缩 | NeedsVerification | 真实 JPG 解码、缩放、JPEG Encode、安全提交与再解码；同目录允许生成新文件但绝不覆盖源 | 是：3 输入、3 输出，均 2400×1802，源不变 | 否，等待 DevValidation 前台验收 |
| 拼图 | NeedsVerification | 导入、模板、调整、CreateNew/AutoNumber 导出 | 是：3 张真实 JPG 导出 1800×1800，重新解码通过 | 否，等待 DevValidation 前台验收 |
| Task Center 失败卡 | NeedsVerification | 第一层中文原因、失败/成功数；展开显示文件名、阶段、源/输出安全、可重试性；技术信息单独折叠 | 结构化专项通过 | 否，等待前台制造安全失败并检查 |
| 批量水印 | PreviewDisabled | 标题明确“预览功能”；添加和导出均禁用并说明尚未开放 | 不适用 | 页面待前台复核 |
| 在线选片 | NeedsVerification | 本轮冻结；Provider=None 为合法状态；独立 TXT/CSV 导出明确禁用，不伪装可用 | 本轮不扩展 | 不纳入四条 Golden Path |
| 联机拍摄 | NeedsVerification | 本轮仅防回归，不新增 Provider 或相机能力 | 本轮未运行真实文件门禁 | 不纳入四条 Golden Path |
| 工作日历 / 排期 | NeedsVerification | 本轮不改状态机；只执行全量回归 | 不适用 | 不纳入四条 Golden Path |
| 批量重命名、删废片、FTP、永久删除 | Hidden | 普通生产目录不暴露；保留兼容代码不得产生文件或网络副作用 | 不适用 | 不适用 |

## 本轮精确根因

真实 `DSC09403.ARW` 与真实批量压缩任务在解码前即失败。两项都选择了源文件所在目录作为输出目录；通用 `FileOperationValidator` 把“源根目录与输出根目录相同”一律视为 `SourceAndDestinationSame`。这条规则适用于 Copy/Move，但不适用于 RAW/压缩这种生成新 `.jpg` 的转换任务。修复为由转换计划显式声明允许同根目录，同时继续执行每项“源文件与目标文件不能相同”、`CreateNew`、`AutoNumber`、Flush、验证和 UndoJournal 所有权检查。

## Zero Dead Control

- RAW / 批量压缩的失败操作为 `查看原因`、`重试失败项`、`复制诊断`，绑定同一 TaskRecord。
- 在线选片结果区原有无 Command 的“导出 TXT / CSV”已改成明确的禁用准备中入口，并提示使用同步归片工作区。
- 批量水印维持 `PreviewDisabled`，不会让用户误认为可正式导出。
- 其余 Sidebar、Workbench、Toolbox、Modal、Drawer、ContextMenu、Menu 由既有命令测试与全量门禁复核；安装版仍需前台真实点击。

## 当前发布判断

四项 RealFileVerified 已完成，但 InstalledUiVerified 尚未完成，因此当前只能生成 `DevValidationBuild`，不得先命名为 RC3。
