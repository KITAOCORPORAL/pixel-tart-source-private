# 像素蛋挞 ProductRedesign RC3 核心可用性抢救报告

## 基线

- 开始 HEAD：`174e56b4170d4c1fdf97ff1a6f4cecdcf3fab043`
- 分支：`feature/pixel-tart-product-redesign`
- ProductVersion：2.3.0
- SchemaVersion：5（未修改）
- 自动测试基线：1943/1943
- UserVerified：false

## 精确根因

用户真实 RAW 与批量压缩并非解码器不兼容，而是输入与输出目录相同时，被通用文件操作校验器在解码前错误拒绝。RAW 与压缩实际生成新的 `.jpg`，并使用 CreateNew + AutoNumber，不会覆盖、移动或删除源文件。修复仅允许这两类转换计划使用相同根目录，仍禁止目标文件与源文件为同一个文件。

## RAW

- Decoder：LibRaw runtime 0.21.1-Release / wrapper 0.21.1.7
- 用户真实文件：仅本机只读验证，不进入仓库、安装包或 Handoff
- 相机：Sony ILCE-7M4
- RAW：7028×4688，sRGB，Orientation 1
- 输出：7028×4688 JPG，3,293,768 bytes，WPF 再解码通过
- TaskEngine：Completed / 100%，同一 TaskId
- 源长度、LastWriteTime、SHA-256：全部不变
- RealFileVerified：true
- InstalledUiVerified：false（待 DevValidation 前台验证）

## 批量压缩

- 真实输入：3 张本机 JPG，仅只读
- 输出：3 张 JPG，均 2400×1802，可重新解码
- 质量：75；冲突策略 CreateNew + AutoNumber
- 源长度、LastWriteTime、SHA-256：全部不变
- RealFileVerified：true
- InstalledUiVerified：false

## 本地分片 / 归片

- 输入选择：3
- 匹配：3 JPG + 3 RAW
- Executor：成功 6，失败 0
- 磁盘实际输出：6
- CSV / JSON / TXT 报告：存在
- 项目重开：恢复同一项目，3 条选择仍存在
- 源文件：全部不变
- RealFileVerified：true
- InstalledUiVerified：false

## 拼图

- 输入：3 张真实 JPG
- 输出：1800×1800 JPG，1,046,203 bytes
- 输出再解码：通过
- 源文件：全部不变
- RealFileVerified：true
- InstalledUiVerified：false

## 状态与诊断

- RAW 与 Batch 失败对象包含 FileName、Stage、ErrorCode、UserMessage、TechnicalMessage、Retryable、OutputOwned。
- TaskRecord 持久化结构化脱敏失败信息；Modal、Task Center、History 读取同一 TaskId。
- 用户第一层不显示英文异常；技术信息位于折叠区域。
- Task Center 提供查看原因、重试失败项、复制诊断。

## Golden Path

- A Local Split：NOT_RUN（Installed UI）
- B RAW → JPG：NOT_RUN（Installed UI）
- C Batch Compress：NOT_RUN（Installed UI）
- D Collage：NOT_RUN（Installed UI）

## 当前构建判断

真实文件四项通过，但普通前台安装版验证尚未完成。当前不得称为 RC3，只允许先生成 CoreReliability DevValidation Build。
