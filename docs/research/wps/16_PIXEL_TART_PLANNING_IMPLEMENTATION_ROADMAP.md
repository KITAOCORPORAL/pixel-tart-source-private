# Pixel Tart Planning Implementation Roadmap

Status: **historical concept roadmap, not current implementation or acceptance status**. The existing Planning Center v1 and proposal workspace are described in the [Stage V report](../../implementation-reports/STAGE_V_PLANNING_CENTER_V1_REPORT.md) and [proposal closure](../../implementation-reports/PLANNING_PROPOSAL_FINAL_PRODUCT_CLOSURE.md). The [borrowing plan](12_PIXEL_TART_PLANNING_CENTER_BORROWING_PLAN.md) gives the current V1 recommendation. Numbered phases below are design candidates; they are neither WPS-verified findings nor a mandate to rebuild implemented modules.

1. **Planning Document Core (P0):** stable model, selection, commands, undo/redo, autosave journal, recovery.
2. **Rich Text + Image + Table (P0):** headings, paragraphs, lists, typed tables, linked assets, page breaks.
3. **Moodboard (P0/P1):** free canvas, crop, layers, align/distribute, Asset Library drag/drop.
4. **Presentation Editor (P0/P1):** slide canvas, photo-first placement, text/shape/image/group/layer, guides/grid.
5. **Shot List / Storyboard (P1):** typed workflow objects, statuses, storyboard frame model.
6. **PDF / DOCX / PPTX Export (P0/P1):** render preview, aspect-ratio policy, compatibility report.
7. **DOCX / PPTX Import (P1/P2):** tiered support, warnings, unsupported-object report.
8. **AI Writing (P2):** explicit selection/project context, no implicit full-library upload.
9. **Asset Library Integration (P1):** AssetId links, renditions, EXIF Smart Block.
10. **Tether / Project Integration (P2):** Shot List status updates and capture references.

Performance gate: test 100-page documents, 500 images, 100-slide decks, and 1,000 text blocks with virtualization before expanding feature scope.
