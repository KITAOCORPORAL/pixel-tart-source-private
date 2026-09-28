# Export Recipes Spec

`ExportRecipe` 是正式 Core model，包含 Name、Format、BitDepth、ColorSpaceProfile、Quality、ResizeMode、边长/宽高、DPI、MetadataPolicy、FilenameTemplate、相对 Destination。TIFF 必须声明 16-bit；Destination 禁止绝对路径，导出不会覆盖原始文件。

`ExportRecipeStore` 提供 Web / Social、Client Full Resolution、TIFF16 Retouch、Print 四个默认模板，并支持自定义保存/删除。现有 PublishingExportService 仍负责实际编码；Recipe UI 多选接线列为下一阶段。
