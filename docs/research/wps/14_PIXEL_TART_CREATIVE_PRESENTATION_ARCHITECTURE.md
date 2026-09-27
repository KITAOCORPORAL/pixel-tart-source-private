# Pixel Tart Creative Presentation Architecture

## Canvas model

`Presentation` contains ordered `Slide` records. A slide owns `SlideElement` objects with stable IDs, bounds, z-order, and optional group membership. Elements include `TextElement`, `ImageElement`, `ShapeElement`, `TableElement`, `PhotoGrid`, `ContactSheet`, `StoryboardFrame`, `ShotCard`, `LightingDiagram`, and `ReferenceImage`.

## Photo-first behavior

On drop, inspect orientation, aspect ratio, dimensions, and available EXIF. Default to no distortion. Offer `Fill`, `Fit`, `Crop`, and `Original Ratio`; persist the chosen policy. Keep a link to Asset Library and generate a controlled rendition only for export.

## Editing aids

Guides, grid, snap, align, distribute, group, layer order, selection pane, master, theme, notes, and a stable undo stack are core. Transitions, animations, video/audio, complex charts, SmartArt, and enterprise collaboration are deferred until a concrete photography use case and compatibility test exist.
