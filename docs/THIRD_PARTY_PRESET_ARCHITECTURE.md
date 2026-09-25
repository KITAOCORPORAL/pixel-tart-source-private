# Third party preset architecture

Stage 3 is **NOT STARTED**. The existing Pixel Tart LUT references and output presets are separate systems and are not silently treated as Adobe or Capture One preset support.

The planned boundary is a parser/import service that produces a canonical adjustment snapshot, maps each source field as EXACT, APPROXIMATE or UNSUPPORTED, and commits a `Preset` node through the existing `ColorStudioRenderPipeline`. It must preserve enable state, order, undo/redo, project schemes and batch sync. Unsupported or encrypted Capture One fields remain explicit diagnostics.
