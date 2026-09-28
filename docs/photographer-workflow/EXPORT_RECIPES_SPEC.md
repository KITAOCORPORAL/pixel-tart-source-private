# Export Recipes Spec

`ExportRecipe` 是正式 Core model，包含 Name、Format、BitDepth、ColorSpaceProfile、Quality、ResizeMode、边长/宽高、DPI、MetadataPolicy、FilenameTemplate、相对 Destination。TIFF 必须声明 16-bit；Destination 禁止绝对路径，导出不会覆盖原始文件。

`ExportRecipeStore` 提供 Web / Social、Client Full Resolution、TIFF16 Retouch、Print 四个默认模板，并支持自定义保存/删除。现有 PublishingExportService 仍负责实际编码；Recipe UI 多选接线列为下一阶段。

## Phase 1B status

The existing WPF Publishing page remains the production export entry and persists its existing publishing presets. `ExportRecipe` is now carried on `PublishingExportRequest`; the service expands the asset × recipe plan, applies format/resize/metadata settings, creates recipe subfolders, uses filename templates and reports aggregate progress.

## Phase 1C status

The existing WPF Publishing page now loads the `ExportRecipeStore`, exposes a multi-select Recipe list, saves custom recipes, and deletes custom recipes. Production export still runs through the existing Publishing task coordinator and preserves atomic output/collision safety. TIFF16 uses the existing WPF TIFF encoder and remains subject to the existing metadata/ICC limitations.

The UI submits all selected recipes as one task. Built-in recipes cannot be overwritten through custom Save; create a new identity for customized copies. Corrupt recipe JSON is moved to a timestamped `.corrupt-*` backup and BuiltIns are restored.
