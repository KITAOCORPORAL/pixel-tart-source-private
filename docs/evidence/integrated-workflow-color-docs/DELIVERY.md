# 第一批交付

## 已完成

1. 从最新远端集成基线创建独立功能分支；
2. 实施 A1 稳定素材解析；
3. 实施 A2 可返回确定 AssetId 的兼容导入；
4. 实施 A3 加法数据库迁移、映射与幂等操作；
5. 增加线性 sRGB、ProPhoto RGB、ACEScg 和 Bradford 适配基础；
6. 在既有策划中心增加结构化文本工具栏、快捷键和测试；
7. 完成全量 Core/WPF 测试和 Release x64 publish。

## 未完成

- A4–A7 跨模块实际交接；
- 专业 RAW 新 ProcessingVersion、相机特征化、ICC/DCP/OCIO 和显示变换；
- Color Studio 新布局的 DPI/实机验收；
- 分页富文本、表格、DOCX 往返、打印预览、批注和修订；
- Release 实机运行和用户视觉批准。

## 风险与下一批

1. 用合成临时数据库执行 A4 Watch Folder、RAW/JPEG 配对和重复事件重试；
2. 建立新 ProcessingVersion 的黄金图和导出容差，旧版本保持原貌；
3. 为 Studio Target 加入可选 LibraryId/SourceHash 并验证 v1 会话；
4. 选定合法文档引擎前，先实现版本化文档包和稳定 AssetId/ShotId 引用；
5. 在允许的桌面验收环境中运行 Release 和 DPI 检查。

## 结论

自动测试和 Release 构建通过。由于实机未运行、用户未验收、第二批与第三批明确未实施，本批结论为 `NOT_READY_FOR_USER_RETEST`，`USER_ACCEPTANCE=NOT_APPROVED`。

推送分支和最终 SHA 见 `GIT_PUSH_VERIFICATION.md` 及最终交付回复。
