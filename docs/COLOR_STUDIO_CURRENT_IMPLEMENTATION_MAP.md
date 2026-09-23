# Color Studio 当前实现地图

本文件按当前源码记录真实实现入口，不创建第二套 Color Studio 架构。

| 能力 | 真实实现 |
|---|---|
| Stack model / Scheme v2 | `src/RAWSelectionAssistant.Core/Services/Projects/ColorStudioModels.cs`：`ColorAdjustmentStack`, `ColorStudioSchemeV2` |
| Legacy migration | `ColorStudioLegacyMigration.Migrate(ReferenceLook)` |
| Headless renderer | `src/RAWSelectionAssistant.Core/Services/Projects/ColorStudioRenderPipeline.cs`：`ColorStudioRenderPipeline` |
| WPF preview/export adapter | `src/RAWSelectionAssistant/Services/ColorStudioBitmapRenderer.cs`；Preview 与 batch export 共用此适配器 |
| Professional editor state | `src/RAWSelectionAssistant/ViewModels/TetherReferenceModeViewModel.cs` |
| Professional workspace view | `src/RAWSelectionAssistant/Views/ReferenceColorWorkspaceView.xaml` |
| Target snapshot / batch entry | `src/RAWSelectionAssistant/ViewModels/ReferenceColorWorkspaceViewModel.cs`：`ColorAdjustmentStackSnapshot`, `ProcessForExportAsync` |
| Reference storage | `ReferenceLookStore` 与现有 `ReferenceLook` 模型 |
| Film | 现有 `PixelTartFilmSettings` / `PixelTartFilmPipeline`，由 `ColorStudioRenderPipeline` 的 Film 节点调用 |
| Selection presentation | `ColorStudioRenderPipeline.ShowSelection`，仅由编辑器预览入口调用，不进入导出 `Render(...).Pixels` |

## 当前边界

- 工作色彩空间为 `OKLabD65`；颜色范围节点的取样与保持原始明度均在共享处理核心中执行。
- 历史 RC12 证据不属于当前 Photography Run；见同轮 manifest 的 `historical_rc12=EXCLUDED`。
- 3D 色彩空间与 3D cluster visualization 延后到 Phase 2。
