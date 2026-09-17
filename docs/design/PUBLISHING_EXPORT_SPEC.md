# Publishing Export v1

Status: core export is implemented; full control-surface, restart recovery and WPF release gate remain partial.

Publishing Export prepares finished photographs as new social/client/portfolio versions. Sources are JPG/JPEG/PNG; TIFF is explicitly unsupported. Files and a top-level folder may be dropped into the page. Independent switches control dimensions/compression and watermark layers. Long-edge and exact-box sizing preserve aspect ratio, and disabling sizing retains source dimensions even when watermarking is enabled.

Multiple image (transparent PNG or JPEG) and text watermark layers can be saved in presets. Image layers support output-width percentage, opacity, nine named positions, margin/offset and HSL/invert adjustments. Text controls expose content, font, weight, size, color and spacing; spacing affects rendered output. Image/color adjustments have a restore-original-color control. Preview renders a new version on a background worker and coalesces changes during an in-flight render. It navigates previous/next photographs. Named presets persist locally. Saving while a project is active assigns that preset as the project's default, and re-entering publishing selects it. The nine positions are a named chooser rather than the requested visual 3×3 picker.

Every export checks the source fingerprint before and after rendering, writes and verifies a temporary file, then moves it to a collision-free destination. Same-directory empty suffix becomes `_social`; existing names are numbered. Task Center provides progress/cancel/failure state, though the publishing page itself has no dedicated cancellation button. Application source bytes are never intentionally overwritten. A test must compare source hashes for each processing combination; logical simulation alone is insufficient for physical-machine acceptance.

Evidence: `artifacts/creative-intelligence-v1/publishing/` contains real App/MainWindow captures; individual titles must be inspected before treating a picture as a specific functional proof.
