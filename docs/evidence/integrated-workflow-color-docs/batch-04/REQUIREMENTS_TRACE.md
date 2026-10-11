# 第四批范围与需求追踪

基线：144de200a563c3648caa70b49373932d0df25f57。源码：4e894e5badb0ef0ac0923c63698d90b7ccebd548。日期：2026-10-11（Asia/Shanghai）。本表评价第四批限定范围，不以完整 Word/专业 RAW 尚未完成来判定本批数据工作失败。

| 需求 | 真实实现/证据 | CODE | TEST | CLI_PROCESS | RELEASE_STARTUP | DESKTOP | USER_VISUAL |
|---|---|---|---|---|---|---|---|
| 单张/批量/快捷键/菜单评分 | 页面读取 AssetItem.AnnotationRevision；统一 ApplyUserAnnotationsAsync，逐项结果 | PASS | PASS | PASS | NOT_RUN | NOT_RUN | NOT_RUN |
| Inspector 备注/色标、Studio 元数据 | 使用读取快照 CAS；刷新权威状态；自己的 FIFO 写入链可衔接 | PASS | PASS | PASS | NOT_RUN | NOT_RUN | NOT_RUN |
| P3 批量与历史接口 | 预览指纹包含版本；旧接口缓存读快照；缺 after-state 的旧撤销拒绝 | PARTIAL | PASS | PASS | NOT_RUN | NOT_RUN | NOT_RUN |
| 后台交接/客户字段 | 后台已有值冲突保护+CAS，客户字段独立 | PASS | PASS | PASS | NOT_RUN | NOT_RUN | NOT_RUN |
| 跨会话目录 | LibraryId/AssetId/hash；外部照片 SHA256；CAS 文件仓储、默认版本幂等、命名/选择/复制/归档 | PASS | PASS | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN |
| 会话/目录恢复 | 会话先留快照，目录修订冲突拒绝；同内容重试接受 | PARTIAL | PASS | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN |
| 导出结构配方/恢复 | 快速导出及实际 Publishing；待完成记录→成片→成功清单；hash 验证补清单 | PASS | PASS | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN |
| 语义标题/大纲/页面/分页 | PlanningRichLayout + PlanningDocumentLayout；RTF 与语义元数据分别持久化 | PASS | PASS | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN |
| 表格行列/列宽/边框/对齐 | 实际编辑按钮与 DOCX 基础读写 | PASS | PARTIAL | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN |
| PDF/预览/打印布局 | 统一 FlowDocument 与页面大小，PDF MediaBox/分页器验证 | PASS | PASS | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN |
| 画布指定对象更新 | CanvasId/RevisionId，选择器更新同一正文位置，保留包历史 | PASS | PARTIAL | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN |
| 大文档实测 | 500 段、512×512 内嵌图、保存恢复分页撤销计时，350ms 防抖 | PASS | PASS | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN |
| 启动参考摘要 | 黑底、空心三角、限定色板、内密外疏；保留真实状态与降级 | PASS | PARTIAL | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN |
| RAW 独立边界 | v3 仍为实验，历史正式管线不变，后续缺口列明 | PASS | PASS | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN |

限定批次结论：PARTIAL。主要剩余是历史兼容 API 无读取快照的调用策略、会话/目录冲突人工合并界面、语义/表格操作完整撤销一致性与桌面验收。USER_VISUAL_REVIEW=NOT_APPROVED。
