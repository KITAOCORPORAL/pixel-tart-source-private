# Pixel Tart Photographer OS Roadmap

更新时间：2026-09-28。以源码、测试和可运行 Core 为准。

## 本轮状态

- **Implemented**：Tether 选片已有 0–5 星、Color Label、Pick/Favorite、Reject、前后导航、Fit/Fill/实际尺寸、筛选/排序、代理缓存优先；2-Up/Overlay 状态、同步缩放/平移、Swap、评分/挑选/拒绝命令已存在。
- **Implemented foundation**：Face Lock 几何契约、ExportRecipe 持久化模型、Booking 缓冲冲突规则、同步 envelope/幂等/高风险冲突、3D camera/renderer contract。
- **Partial**：WPF Face Lock/2-Up 视觉控件接线、Rapid Compare UI、Recipe 批量 UI、真实 Windows 3D renderer。
- **Spec only**：Pocket、真实云服务、Online Selection 云图库、Browser Extension、AI Culling、Skin Studio、Look DNA、Tether 新功能。

Planning Center、RAW/TIFF/Match v3/v4 与现有产品线保留，不在本轮重写。

## Phase 1B implementation status (2026-09-28)

- **Implemented in WPF**: Tether compare toolbar now exposes Fit/100%/200%, Swap A/B, Rapid Compare actions, and keyboard J/N/PageUp/PageDown routing without changing 1–5, Reject, Pick, or navigation keys.
- **Implemented in persistence**: Booking schema v6 stores pre/post buffers, HOLD expiry, payment state, revision, device id, and tombstone; repository round trips these fields and service increments revision.
- **Partial**: Face Lock UI toggle and fallback status are wired, but no production detector/landmarks are available; ordinary synchronized compare remains the safe path. Recipe UI still uses existing Publishing presets; new ExportRecipe manager/multi-recipe execution is not yet wired.
- **Not run**: Native pointer walkthrough, production screenshots, full WPF/3D visual evidence.
