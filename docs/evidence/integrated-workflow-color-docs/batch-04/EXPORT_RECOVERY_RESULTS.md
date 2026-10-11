# 导出配方与恢复

新增 ExportRecipeSnapshot v1，原 StudioExportReceipt.Recipe 字符串保留可读。新清单 ManifestVersion=2，包含实际输出格式/位深/尺寸、缩放模式、JPEG 质量、压缩、ICC 名称、元数据策略、有效水印层、DPI、文件模板及调整指纹。未实现锐化明确写 Not implemented/Not applied；无关尺寸字段为 null。Publishing 输出尺寸/位深通过真实 WPF 解码读取。

生产接线：
- Studio 快速导出 → 临时编码文件 → CommitOutputAsync。
- PublishingExportService.ExportAsync（实际成品路径，不仅准备阶段）→ renderer.Render/Verify → CommitOutputAsync。
- Studio 发布准备继续提供上游来源清单；Publishing 先验证上游输出 hash 再继承源身份。

提交顺序：计算临时输出 hash → 原子写 pending 清单 → 原子移入成片（不覆盖）→ 原子写成功 sidecar → 删除 pending。失败和取消不显示整体成功；PublishingItemState/ReferenceExportStatus 新增 Partial。输出已成但清单失败时重试读取 pending，核对输出路径/hash、源身份/EditVersion、配方或调整指纹，补清单而不再渲染。

实际失败注入测试：最终 sidecar 被目录阻止，首次 Partial；去掉测试阻挡后重试 Completed，renderer 调用计数仍为 1。错误版本/hash/同名冲突拒绝。输出和 sidecar 的冲突分别检查，孤立 sidecar 不被覆盖。

限制：进程在 pending 写完但成片尚未移入之间崩溃，会留下缺失输出的 pending；目前安全拒绝，不自动从临时文件续传。图片式 Publishing 非 Studio 源用输入 SHA 派生本地版本 ID，不伪造 LibraryId/AssetId。在线客户选片与交付应用不属于本批完成项。
