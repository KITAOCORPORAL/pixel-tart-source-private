# 策划文档编辑器功能矩阵

状态基于真实源码与本批增量；工具栏存在不等于 Word 功能完成。

## 文本与段落

| 功能 | 状态 | 证据/差距 |
|---|---|---|
| 加粗、斜体 | IMPLEMENTED | 本批结构化 Markdown 操作、快捷键、撤销重做 |
| 标题 1/2 | IMPLEMENTED | 本批样式命令，保存为兼容文本 |
| 项目符号、编号、引用、分隔线 | IMPLEMENTED | 本批范围操作 |
| 字体、字号、颜色、下划线、删除线 | NOT_IMPLEMENTED | 无富文本格式模型 |
| 上下标、突出显示、字符间距 | NOT_IMPLEMENTED | 无排版运行模型 |
| 缩进、对齐、行距、段前后、制表位 | NOT_IMPLEMENTED | 当前正文为 TextBox 文本 |
| 多级编号 | NOT_IMPLEMENTED | 仅单级编号 |

## 文档结构与对象

| 功能 | 状态 | 证据/差距 |
|---|---|---|
| 标题样式 | PARTIAL | 只有标题 1/2 文本语法，无集中样式管理 |
| 图片引用 | PARTIAL | 策划参考图/附件存在，尚非分页正文内联对象 |
| 表格 | NOT_IMPLEMENTED | 无表格模型和编辑器 |
| 页面设置、分页、分节、多栏 | NOT_IMPLEMENTED | 无分页布局引擎 |
| 目录、页眉页脚、页码 | NOT_IMPLEMENTED | 无文档结构计算 |
| 浮动图片、裁切、环绕、文本框、形状、图表 | NOT_IMPLEMENTED | 无对象布局引擎 |
| 超链接、书签、题注、交叉引用、公式 | NOT_IMPLEMENTED | 无对应模型 |

## 编辑、审阅与输出

| 功能 | 状态 | 证据/差距 |
|---|---|---|
| 撤销、重做 | IMPLEMENTED | WPF TextBox 命令 |
| 自动保存与重开 | IMPLEMENTED | 复用既有策划草稿持久化 |
| PDF 导出 | PARTIAL | 既有导出可用，但不是完整分页 Word 排版保真 |
| 查找替换、拼写检查 | NOT_IMPLEMENTED | 无专用入口/状态 |
| 字数统计 | PARTIAL | 现有界面有基础文本状态，未形成分页文档统计契约 |
| 批注、修订、比较、版本历史、保护 | NOT_IMPLEMENTED | 无审阅模型 |
| DOCX 导入/导出及往返 | NOT_IMPLEMENTED | 未引入 Open XML/排版引擎 |
| 打印预览 | NOT_IMPLEMENTED | 无分页预览 |

## 摄影策划增强

| 功能 | 状态 | 证据/差距 |
|---|---|---|
| Moodboard 与参考图 | PARTIAL | 既有策划引用存在，尚未与正文对象统一 |
| Shot 引用/镜头清单 | PARTIAL | 既有 Shot 数据存在，缺正文稳定引用对象 |
| 拍摄时间线 | PARTIAL | 项目/日历有数据，未嵌入文档 |
| 服化道、设备、预算表 | NOT_IMPLEMENTED | 无结构化表格模板 |
| 灯位图、分镜页 | PARTIAL | 可作为参考图，缺专用编辑对象 |
| 客户审阅版本 | NOT_IMPLEMENTED | 无审阅与发布版本链 |
