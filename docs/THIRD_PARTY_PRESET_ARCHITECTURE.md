# Third party preset architecture

Stage 3 Adobe foundation is **PARTIAL**. `AdobeXmpPresetParser` maps numeric camera raw fields with explicit exact/approximate/unsupported states, records a source hash, persists parsed presets and supports cancellable batch import with progress. The existing Pixel Tart LUT references and output presets remain separate systems.

The planned boundary is a parser/import service that produces a canonical adjustment snapshot, maps each source field as EXACT, APPROXIMATE or UNSUPPORTED, and commits a `Preset` node through the existing `ColorStudioRenderPipeline`. It must preserve enable state, order, undo/redo, project schemes and batch sync. Unsupported or encrypted Capture One fields remain explicit diagnostics.
