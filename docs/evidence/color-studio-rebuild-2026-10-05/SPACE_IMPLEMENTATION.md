# 3D、双直方图及预览映射实现记录

日期：2026-10-05。源码起点 `5427406c4ef3c805ef56fbb814251df69365032d`；最终构建标识由阶段 Release manifest 记录。

## 本次改动

| 文件 | 类型 | 行为 |
|---|---|---|
| `Core/Services/Projects/ColorSpaceSurface.cs` | 新 Core 数学 | sRGB 边界二分、OKLab→归一化球形显示及逆变换、相机 X/Y/Z、表面拾取、切片/色度谓词、柔和选区、分析颜色索引 |
| `Core/Services/Projects/ColorStudioColorSpace.cs` | 修改模型 | 选区携带精确 OKLab 中心；采样排除全透明像素 |
| `Core/Services/AssetLibrary/VisualAnalysis/VisualAnalysisModels.cs` | 兼容扩展 | VisualPixelBuffer 可选 alpha；旧 RGB 构造不变；含 alpha 的 fingerprint 纳入透明度 |
| `Core/Services/AssetLibrary/VisualAnalysis/VisualAnalysisEngine.cs` | 修改统计 | 直方图及影调成员排除全透明像素；非全透明像素按一个可见像素统计（不是面积加权直方图） |
| `Views/ColorSpace3DViewport.cs` | 重做绘制 | 动态 CPU 逆投影连续球面；网格/轴/色域边界；真实样本按深度排列；空图只显示参考球；Orbit/Pan/Zoom/Reset/Fit；球面和样本拾取 |
| `ViewModels/ReferenceColorWorkspaceViewModel.Analysis.cs` | 修改分析 VM | 当前图 revision/取消/缓存保留；复用一个真实云，取消多余 identity Match；共享显示设置、缓存每像素 OKLab、精确色容差与软边动态重算 |
| `ViewModels/ReferenceColorWorkspaceViewModel.cs` | 局部修改 | 图片采样不把中心替换成最近抽样点；保留 alpha 代理；Esc/切图清除软 mask 和颜色中心 |
| `Views/ImageHighlightOverlay.cs` | 修改预览层 | 每像素权重 alpha；继续共享照片的 zoom/pan 和比较视口裁切；不写编辑栈 |
| `Views/StudioHistogramView.cs` | 新 UI 包装 | 复用 HistogramDrawing，双独立实例、通道标签、0–255/线性 Y 刻度和悬停数值 |
| `PixelTart.Modules.AssetLibrary/HistogramDrawing.cs` | 修改共享绘制 | 亮度单通道为灰色填充；RGB 的可选亮度叠加保留线条 |
| `tests/.../ColorSpaceSurfaceTests.cs` | 新 Core 行为测试 | 正逆变换、真实颜色不投球壳、旋转与拾取、软边/alpha/非法值、切片和超色域 |
| `tests/.../ColorSpaceSurfaceWpfTests.cs` | 新 WPF 行为测试 | 连续实际渲染/旋转/透明度、稀有色成员、alpha、直方图读数、左右控件共享设置 |
| `tests/.../RuntimeCorrectionWpfTests.cs` | 更新既有行为断言 | 调容差保留并重算现有精确选区，不再无条件清除 |

## 显示及颜色契约

- 照片样本使用实际已显示的原片或调整后 sRGB 代理，最长边 768；界面明确分析对象和取样尺寸。它不是 RAW 传感器空间。
- 实际颜色距离是 D65 OKLab 欧氏距离。显示球是非线性色域归一化：`y=2L−1`、`r=(C/Cmax(L,h))*sqrt(1−y²)`、`x=r*cos(h)`、`z=r*sin(h)`。原始样本 Lab 永不改写，±a/±b 标签表示方向。
- 球壳色彩为该 L/h 的 sRGB 边界。CPU 每像素逆相机后计算颜色；不是静态渐变图。显示用 129×361 的 Cmax 插值表；拾取和颜色距离使用二分精确边界。
- 默认球面加样本；另有原始 OKLab 仿射点云和仅球面。超色域样本数学保留超球半径，但当前 sRGB 代理及默认 `0..1` 色度过滤不声称展示 RAW 超色域信息。
- 透明度（surface/point）只改变观察；照片 alpha=0 不统计、不采样、不选中。部分透明的统计为一个可见像素，选区 mask 强度乘其 alpha。
- 透明像素规则作用于本次 Studio 的 `AnalyzeHistogram`/tone/proxy/mask 链；素材库完整 `VisualAnalysisEngine.Analyze` 的 palette、均值和相似特征仍为既有 RGB24 契约，不宣称其已经完成 alpha 感知。不要给该完整分析入口传入含隐藏 RGB 的 alpha 图后误称全套统计排除了透明像素。
- 选区半径单位 ΔOKLab；柔和度 0 为硬边，1 为从中心至半径的完整 smoothstep；容差或柔和度更改重算选区。清除、切图及分析代次变化清空。影调暂时覆盖选区；不会复活旧颜色选区。
- 点云与球面的摄像操作均不写 `AdjustmentStack`。预览遮罩不传给导出。具体应用/导出一致性仍由阶段独立实测与像素比较记录。
- CPU 性能边界：静止表面最长边 320，拖动 160；同 `(camera, settings, size)` 缓存；样本色域坐标一次计算；每像素 OKLab 缓存在分析快照。当前 UI 选区距离遍历仍为同步最长 768²，尚须真实高频拖动计时，不宣称帧率达标。

## 自动验证记录

| 当前本地运行 | 结果 | 证据 |
|---|---|---|
| 新 Core 球形/软边/透明度测试 | 5 passed / 0 failed | `artifacts/color-studio-rebuild-2026-10-05/tests/space-surface-core.trx` |
| ColorSpace + EveningFeedbackCore 回归 | 45 passed / 0 failed | `.../space-core-regression.trx` |
| 既有 WPF mapping/viewport/analysis subset | 11 passed / 0 failed | `.../space-wpf-first.trx` |
| 新4项 WPF + viewport3 + analysis1 | 8 passed / 0 failed | `.../space-surface-wpf-final.trx` |

WPF 使用 `RenderTargetBitmap` 检查实际绘制像素、控件绑定及 VM 行为；这是自动验证，不是 Production Release 实机证据。首个新增 WPF 尝试遇到同时开发中的 view handler 未落地而编译失败，未产生测试 PASS；后续 handler 完整后上表运行完成。最终阶段会重建并全量测试。

## 待实机确认

连续球面视觉质量、读取点与球面双向联动、短窗口/DPI、相机手势及控件值反馈、参数拖动性能、完整导出像素隔离，须由同一最终 Release 真实操作。此文件不替代该证据。

CODE：IMPLEMENTED（上列范围）；TEST：PASS（仅上列自动测试范围）；RELEASE_RUNTIME：NOT_RUN；USER_VISUAL_REVIEW：NOT_APPROVED。
