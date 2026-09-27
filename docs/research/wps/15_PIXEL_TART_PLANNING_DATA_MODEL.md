# Pixel Tart Planning Data Model

```text
PlanningProject
├─ documents: PlanningDocument[]
├─ presentations: Presentation[]
├─ moodboards: Moodboard[]
├─ shotLists: ShotList[]
├─ storyboards: Storyboard[]
├─ callSheets: CallSheet[]
├─ lightingPlans: LightingPlan[]
├─ templates: TemplateRef[]
└─ assets: AssetRef[]
```

Every entity has `id`, `type`, `schemaVersion`, `createdAt`, `updatedAt`, and `revision`. References use stable IDs rather than array positions.

## Core entities

| Entity | Required fields |
|---|---|
| PlanningDocument | pages, blocks, settings, assetRefs |
| DocumentPage | blocks, size, orientation, margins |
| TextBlock | text/runs, style, links |
| ImageBlock | assetId, rendition, crop, fitPolicy |
| TableBlock | columns, rows, merge map, style |
| ShotBlock | shotId, referenceAssetIds, status |
| LightingBlock | fixtures, camera, talent, annotations |
| Presentation | slides, theme, master, notes |
| Slide | elements, background, layoutRef |
| ImageElement | assetId, bounds, crop, fitPolicy |
| Moodboard | freeform elements, viewport |
| ShotList | typed columns, shots, status transitions |
| Shot | scene, talent, lens, camera, lighting, pose, action, notes, status |

## Status vocabulary

Shot statuses: `Planned`, `Ready`, `Shot`, `Selected`, `Delivered`. Import/export status is separate and never mutates the creative status.

## Storage

JSON metadata plus asset references. Do not duplicate hundreds of megabytes of source media inside each planning file. Export creates renditions and embeds only when the target format requires it.
