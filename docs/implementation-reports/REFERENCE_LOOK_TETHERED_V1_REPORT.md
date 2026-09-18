# Stage III — Reference Look / Tethered v1

Status: **PARTIAL — code/test closure completed for the implemented v1 path; real-App visual evidence and physical camera acceptance are not complete.**

Baseline began at `f3216f61bc8929a3ebabbe70f77744342a605857` on `integration/pixel-tart-developer-preview`. Pre-existing RC12 report edits remain outside Stage III commits.

## DONE

- Previous visual-intelligence closure: immutable Zone hover frames, HEX/HSL copy feedback, project palette/tone persistence, exact-duplicate three-choice import, visual 3×3 watermark picker.
- Project Look model/store: multiple normalized references, editable parameters, atomic versioned persistence, create/rename/copy/delete/default UI in the existing Asset Library visual panel.
- Reference Match: deterministic CPU Lab distribution mapping, bounded tone mapping, editable strengths, heuristic (honestly labeled) skin/highlight protection, in-memory preview only.
- Color path: Source Preview → Reference Look → optional existing LUT → existing display transform. No second ICC pipeline and no source write.
- Tether shell: existing page reorganized as large image + 360px right Accordion (320–480px) + bottom virtualized horizontal filmstrip. Existing tools remain.
- Reference Mode: simple default controls, advanced folded sliders, original/matched/split/side-by-side, `B` hold for original.
- Split Compare: geometry-only draggable line, double-click 50%, 0–1 position, shared parent zoom/pan.
- Apply to Following: new capture/source is shown first; enabled matching runs asynchronously. Cancellation + asset/revision checks prevent stale publication, and failure keeps original preview.
- Shot core: versioned atomic project Shot store, status, lighting/pose/storyboard/styling/general references, Pose state/advance, Pin state and non-owning references.
- Shot tether bridge: Shot navigation, reference category switching, notes, Pose completion, Pin and Shot Look selection.
- Camera capability boundary: formal detailed capability interface; Watch Folder reports every remote camera-control capability false and UI contains no fake focus/settings controls.
- Source Safety: exact-source preview test and on-disk before/after SHA-256 test pass; Shot and Look stores contain references rather than source bytes.
- Documentation: three product specs and three architecture documents added.

## PARTIAL

- Project Look source entry: asset/board/canvas analysis can create Looks, but v1 does not provide a dedicated per-source weight editor or external-reference import picker.
- Project default Look fallback: core resolution is tested; current Tether Shot bridge selects explicit Shot Look. Folder-monitor launch does not yet expose a project picker, so default lookup is not always available at runtime.
- Exposure evaluation: existing RGB/luminance histogram and clipping warnings are regrouped. Full eleven-zone hover surface inside Tether is not added.
- Shot references: data, navigation, status and Pin are present. Quick Preview, a persistent adjustable side panel and reference second-display window are not delivered.
- Accordion persistence: layout/order is updated, but expanded/collapsed state and right-column width are not persisted across process restarts.
- Performance: cancellation/debounce/latest-wins design is implemented and focused stress is exercised; no physical 45MP RAW or new 10K benchmark was run.
- Test Gate: focused core and WPF gates are green. Repository-wide diagnostics remain separately governed; this report does not relabel them.

## NOT IMPLEMENTED

- Next-capture naming/location/adjustment rule editor.
- Dedicated Reference source picker for recent/library/board/canvas/external items inside Tether.
- Complete Planning Center, planning editor or complex rich text.
- AI semantic match, ONNX skin segmentation, GPU backend, sensor-native RAW rendering, LUT export.
- Canon/Nikon/Sony SDK integration, remote shutter/focus/exposure/battery/storage controls.
- Model Display special screen.
- Publishing task restart recovery; cross-store duplicate crash/power-loss journal; new real 10K acceptance.

## Verification

- Phase A core: 4 passed, 0 failed, 0 skipped.
- Phase A WPF: 2 passed, 0 failed, 0 skipped.
- Stage III core focus: 15 passed, 0 failed, 0 skipped (`stage-iii-core.trx`).
- Tether/Stage III WPF focus: 54 passed, 0 failed, 0 skipped (`stage-iii-wpf.trx`).
- Release x64: 0 warnings, 0 errors.

## Visual Evidence / Physical Machine Status

- Real-App Stage III screenshots: **NOT IMPLEMENTED in this environment checkpoint**; no synthetic screenshot is claimed as physical evidence.
- Physical camera: **NOT TESTED**. The implemented provider remains explicitly “文件夹监看”.
- Physical multi-DPI/monitor acceptance: **NOT TESTED**.
- Therefore this report is an honest implementation/test report, not final release acceptance and not authorization for RC13 or `main` merge.
