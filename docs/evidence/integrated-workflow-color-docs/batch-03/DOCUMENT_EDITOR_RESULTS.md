# 文档编辑器结果

产品入口：策划文字页→编辑→PlanningRichEditor。RichTextBox/FlowDocument 提供加粗、斜体、下划线、标题外观、正文、颜色、段落对齐、项目符号、图片、3×3 基础表格、撤销/重做。正文以 RTF Base64 加法字段保存到既有 PlanningDocumentPackage，旧 Body 和未知字段保留；复用自动保存/draft。

分页预览使用克隆 FlowDocument，打印使用同一分页器；原图片式 PDF 改为复用富文本正文块。画布快照插入正文时嵌入 PNG，原 CanvasId 和追加历史仍在文档包，更新为显式动作。

## DOCX 支持子集

固定 DocumentFormat.OpenXml 3.5.1，官方 NuGet nuspec 明确 MIT 与 net10.0。许可文本随发布输出 ThirdPartyNotices/OpenXML-LICENSE.txt；不需要 Office/WPS，不采购。

来源：
- https://api.nuget.org/v3-flatcontainer/documentformat.openxml/3.5.1/documentformat.openxml.nuspec
- https://github.com/dotnet/Open-XML-SDK/releases/tag/v3.5.1
- https://learn.microsoft.com/dotnet/desktop/wpf/controls/richtextbox-overview

支持直接字符格式、段落对齐、基础项目符号、单层表格、独立内嵌 PNG。导入前报告未知结构、页眉页脚、样式继承、多节、表格属性、表内图片、同段图文拆分和编号降级。导出遇到不支持对象/合并单元格/嵌套表格拒绝写入，保留原文件。Schema 校验曾发现 numPr 顺序与 TableGrid 缺失，已修复并测试通过。

| 能力 | 状态 |
|---|---|
| 字符格式、基础段落、列表、图片、表格 | 已实现，自动测试 PARTIAL，实机 NOT_RUN |
| 标题样式体系/大纲/目录 | PARTIAL：仅标题外观，无完整样式树 |
| 分页预览与打印 | 已实现；实际打印 NOT_RUN |
| DOCX 受限子集往返 | PASS（固定测试），任意文档保真 PARTIAL |
| 图片环绕、浮动对象、复杂表格 | 未实现，损失报告/拒绝导出 |
| 批注、修订、版本比较 | 未实现 |

不能称作完整 Word。RTF/FlowDocument 与 DOCX 的复杂排版仍有差异；文档很大时每次保存成本和分页一致性尚需专项测试。
