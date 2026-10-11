# 文档包与画布

PlanningDocument.Package 是加法字段。包版本 1 包含 DocumentId、ProjectId、Body、Assets、ShotIds、CanvasRevisions；旧文本/旧 sidecar 继续读取。顶层文档、包和 sidecar 的未知 JSON 字段保留。

现有 PlanningProjectStore 原子临时写入/替换和 draft 恢复继续使用。策划情绪板中“插入/更新画布版本”读取最新画布，保存独立对象树和 PNG。再次更新追加新 RevisionId，旧快照不改写。原图离线时拒绝更新，已存 PNG 仍可查看。支持基础图片裁切、翻转、黑白、旋转、文字和色块；尚未验证与自由画布全部绘制细节逐像素一致。

## Word 对照

| 范围 | 产品状态 | 本批证据 |
|---|---|---|
| 基础正文/结构化文本 | PARTIAL | 第一批工具栏仍保留；本批版本化数据包 |
| 分页编辑 | PARTIAL | FlowDocument DocumentPaginator 实验通过，未替换实际正文编辑器 |
| 表格 | PARTIAL | DOCX 试验可序列化/重开单元格，产品无表格编辑 UI |
| 图片与环绕 | PARTIAL | 画布快照可嵌入，正文复杂环绕未实现 |
| DOCX 往返 | PARTIAL | 合成段落、加粗、表格、页面设置+schema 验证；不代表任意 Word 保真 |
| 打印/PDF | PARTIAL | 保留现有 PDF；打印预览未实现 |
| 批注/修订 | NOT_RUN | 未实现，未伪造按钮 |

## 技术验证与许可证

官方 GitHub releases/latest 在本轮返回 Open XML SDK v3.5.1（2026-03-18）。官方 NuGet nuspec 声明 MIT，含 net10.0 目标。MIT 要求分发时保留版权与许可文本。独立 tests/PixelTart.DocumentPrototype.Tests 固定 3.5.1，仅用于实验，不进入产品依赖、不需要 WPS 或 Office。

核对来源：
- https://github.com/dotnet/Open-XML-SDK/releases/tag/v3.5.1
- https://raw.githubusercontent.com/dotnet/Open-XML-SDK/main/LICENSE
- https://api.nuget.org/v3-flatcontainer/documentformat.openxml/3.5.1/documentformat.openxml.nuspec

Open XML 是包与结构编辑 SDK；本批没有将它当作分页/排版引擎。完整 Word、复杂环绕、批注修订和任意 DOCX 往返仍未实现。
