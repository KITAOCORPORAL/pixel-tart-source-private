# Export Recipes Spec

`ExportRecipe` 是正式 Core model，包含 Name、Format、BitDepth、ColorSpaceProfile、Quality、ResizeMode、边长/宽高、DPI、MetadataPolicy、FilenameTemplate、相对 Destination。TIFF 必须声明 16-bit；Destination 禁止绝对路径，导出不会覆盖原始文件。

`ExportRecipeStore` 提供 Web / Social、Client Full Resolution、TIFF16 Retouch、Print 四个默认模板，并支持自定义保存/删除。现有 PublishingExportService 仍负责实际编码；Recipe UI 多选接线列为下一阶段。

## Phase 1B status

The existing WPF Publishing page remains the production export entry and persists its existing publishing presets. The newer `ExportRecipe` Core model/store is not yet connected to a multi-select recipe manager or one-click multi-recipe task; this remains PARTIAL.

## Phase 1C status

The existing WPF Publishing page now loads the `ExportRecipeStore`, exposes a Recipe selector, saves custom recipes, and deletes custom recipes. Production export still runs through the existing Publishing task coordinator. Full multi-select UI and TIFF16-specific encoder integration remain partial and are reported as such.

The current selector is single-select and acts as a safe apply-to-editor operation. Multi-select execution is intentionally not claimed until the existing task coordinator exposes a per-recipe progress contract.
