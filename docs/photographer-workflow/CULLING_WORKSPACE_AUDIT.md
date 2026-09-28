# Culling Workspace Audit

以 `TetherCaptureViewModel` 为事实来源：代理/缓存预览先行，后台可加载全分辨率；不会在选片阶段删除源文件。评级被限制在 0–5，颜色标签为红/黄/绿/蓝/紫，支持 Reject、Favorite、筛选、排序、上一张/下一张、Fit/Fill/实际尺寸。多选由现有 Asset Library 选择状态承担。

2-Up/Overlay 已有比较状态、同步缩放/平移、Swap 和标注命令。Face Lock 几何规划与无脸/低置信度 fallback 已加入 Core；WPF 人脸检测和真实指针验收仍是下一阶段。

证据：`PhotographerWorkflowFoundationTests` 5/5 通过；原有 Tether/Photography 测试需随完整 WPF 回归执行。
