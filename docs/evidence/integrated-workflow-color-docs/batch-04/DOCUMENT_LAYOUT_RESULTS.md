# 文档布局收尾

## 实际产品能力

PlanningRichEditor 增加 Body/Heading1/Heading2/Heading3 语义样式与大纲导航；语义、显式分页、CanvasId/RevisionId 和导入损失报告存入 PlanningRichLayout v1。RTF 继续保存字符、图片、列表与表格内容，旧正文和未知字段不丢弃。

表格工具提供插入/删除行列、列宽切换、对齐及边框；页面提供 A4/Letter、横竖向、页边距和显式分页。当前列宽/边距入口是有限档位，尚不是任意数值输入器。复杂合并、嵌套表格、环绕、批注修订、目录域仍为后续项。

画布选择器选择需要插入或更新的 CanvasId。同一正文对象保留 RevisionId；显式更新替换该位置，不重复追加，包内历史 CanvasRevisions 保留。原图离线仍可查看已嵌入图。

## 统一布局与输出

PlanningDocumentLayout.Load/Apply 是编辑恢复、分页预览、打印与富文本 PDF 的共同定义。PDF 不再固定 A4：从 FlowDocument 读取页面宽高，MediaBox 与栅格尺寸对应。富文本 PDF 输出为正文布局；旧纯文本策划汇总仍沿用旧报告路径。PDF 为图片式页面，不声称文字可选或 DOCX 版面与 Word 相同。

DOCX 适配器补齐语义标题 ID、显式分页、页面宽高/边距、基础表格列宽和边框。导入确认前提示不支持内容，报告保存在 RichLayout.ImportLosses。原文件只读；导出通过临时文件替换。Open XML schema 验证仅说明结构合法，不等于版面保真。

## 测量与优化

合成 500 段、512×512 RGB 图片，本机一次发布前测量：纯文本 RTF 77,633 字节，保存 31ms、恢复 57ms、分页 72ms；内嵌图后 1,656,518 字节，保存 60ms；编辑/撤销低于毫秒计时分辨率（记录 0ms），14 页。详见 DOCUMENT_MEASUREMENTS.txt。不是性能 SLA；大图、字体替代及极长表格未充分覆盖。

根据实测，正文序列化采用 350ms 防抖，Ctrl+S、导出、预览、隐藏/卸载前 FlushPending。测试验证 RTF+语义恢复、横向页面 PDF MediaBox、图片内嵌、分页器与 DOCX 子集。实机键盘/拖动/打印/DPI 为 NOT_RUN。

剩余：所有语义/页面命令与原生富文本撤销栈的一致性仍需专项加固；当前不声称这些操作均可撤销。PDF 与屏幕像素逐页视觉对照未执行。限定文档子项 CODE=PARTIAL、TEST=PARTIAL。
