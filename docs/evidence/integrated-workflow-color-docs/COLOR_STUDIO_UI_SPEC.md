# Color Studio UI 增量规范

## 目标布局

- 左侧：项目、照片集合、预设、历史；
- 中央：大图、前后对比、Fit/100%/缩放和平移；
- 右侧：基础、曲线、HSL/颜色编辑器、三路调色、细节/校准、局部调整；
- 底部：可折叠胶片条、多选、筛选和同步；
- 顶部：模式、视图、缩放和主要导入/导出操作。

每组支持折叠、单组复位和持久化；分隔条可拖动；拖动参数时允许节流低分辨率预览，松开后必须以同一调整状态准确重算。一个 Zoom 状态驱动所有倍率显示。

## 本批真实状态

现有 Color Studio 已包含大量调色、直方图、3D、胶片条和导出能力，但最近 Release 的人工验收仍有失败项。本批没有用 XAML 存在来升级这些状态，也没有做新的布局重构。

本批仅完成色彩数学基础审计。由于全局 AGENTS 规则禁止 Computer Use、浏览器和 WebView，本分支没有进行 100/125/150/200% DPI 的 Release 实机巡检。

| 项目 | CODE | TEST | RELEASE_RUNTIME | USER_ACCEPTANCE |
|---|---|---|---|---|
| 现有 Color Studio UI | PARTIAL | PARTIAL | NOT_RUN | NOT_APPROVED |
| 新专业工具分组 | PROPOSED | NOT_RUN | NOT_RUN | NOT_APPROVED |
| 新色彩基础接线 | NOT_IMPLEMENTED | PASS（纯数学） | NOT_RUN | NOT_APPROVED |
