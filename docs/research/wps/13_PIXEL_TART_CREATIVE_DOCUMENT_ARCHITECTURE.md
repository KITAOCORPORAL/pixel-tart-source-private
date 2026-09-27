# Pixel Tart Creative Document Architecture

## Document surface

`PlanningDocument` contains ordered `DocumentPage` records. Each page contains ordered `DocumentBlock` records. Blocks are typed, stable-ID objects rather than an untyped HTML blob.

Core blocks: `Text`, `Heading`, `List`, `Table`, `Image`, `Shape`, `Divider`, `Quote`, `Checklist`, `Link`, `PageBreak`, `Header`, `Footer`.

Photography blocks: `ImageReference`, `ShotCard`, `ExifSmartBlock`, `LightingBlock`, `LocationBlock`, `TalentBlock`, `EquipmentBlock`, `CallSheetBlock`.

## Interaction contract

Use compact toolbar + context toolbar + command palette (`Ctrl+K`) + slash commands. Every edit is a command; a drag gesture is one undo transaction. Autosave writes a journal entry and periodic snapshot. Selection and clipboard operations must sanitize external content.

## Rendering contract

The canonical model renders to editor, print/PDF, client preview, and DOCX export. Asset blocks retain `AssetId`, source link, crop, focal point, and rendition policy. Export may embed a rendition but must not replace the canonical asset reference.
