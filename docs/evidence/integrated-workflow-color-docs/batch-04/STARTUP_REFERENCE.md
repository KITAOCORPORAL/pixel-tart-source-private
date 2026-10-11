# 启动动画参考摘要

本轮权威输入为用户确认的文字摘要，不依赖未落盘的 DESIGN.md，也不把此前未找到文件记为开发失败。

- 纯黑背景；细小空心三角粒子。
- 紫、蓝、青、琥珀为主，少量洋红。
- 中心密集、外围低密度，缓慢漂浮与聚合的抽象星云。
- Kitao Studio 主标题、光镜辅助文字。
- 真实初始化阶段；主窗口 ContentRendered 即淡出，无人为等待完整动画。
- 系统减少动画或 KITAO_REDUCE_MOTION=1 静态降级。
- Closed/Unloaded 停止时钟并解绑 CompositionTarget.Rendering。

本批修改 StudioStartupWindow 的色板、空心绘制及内外密度。没有 Logo 定稿，不宣称逐帧复刻原动画。CODE=PASS，自动构建 PASS；RELEASE_STARTUP、DESKTOP、USER_VISUAL 均 NOT_RUN，用户视觉批准仍 NOT_APPROVED。
