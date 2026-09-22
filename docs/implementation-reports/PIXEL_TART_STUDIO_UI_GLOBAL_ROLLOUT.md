# Pixel Tart Studio UI global rollout

Status: READY FOR USER VISUAL REVIEW

- START_BRANCH_HEAD: `4c7eaa8fbd870ac2fa67584609fe84deb5e29b28`
- START_PRODUCT_SOURCE: `9bcce6098ce876096bdd444030f554b968384fa0`
- NEW_PRODUCT_SOURCE_SHA: `1c22ee20f893f3cfcfab43740a9aa015fe6ada0b`
- FINAL_HEAD: `9970b4d0506841358f6479e5613c6a9c7a08ce53` (evidence/report commit)
- Installer: NOT GENERATED
- Physical DPI: NOT TESTED

## Route audit before implementation

Each row records layout/task/visual, header/actions, controls/borders/type, popup/scroll,
and loading/empty/error/DPI risk. These are findings, not completed gates.

| Route | Layout / primary task / visual | Header / primary / secondary | Legacy / borders / typography | Popup / scroll | States / DPI risk / intended change |
| --- | --- | --- | --- | --- | --- |
| Workbench | Overview + recent covers + task rail; resume photography work | Quick tools / new task / history | DashboardPanelCard and metric outlines; 15/16/17/22/26 hardcodes; glyph buttons | Quick tools popup; project horizontal and task vertical scroll | Existing empty tasks/projects; reduce outlines, token hierarchy, real symbols; minimum height rail risk |
| AssetLibrary | Tree + image grid + inspector; find and inspect photos | Import / search / filters | Core Studio baseline; lower inspector density | Context/submenus/filter/loupe; inspector scroll | Existing delayed import/analysis; preserve structure, verify lower controls and DPI |
| Workflow | Four steps + source rail + result table; match and deliver | Step context / match or copy / save/report | Five metric cards, framed sections, glyph separators | Combo / result grid / source scroll | Scan/match/copy state; narrow step row and source action collision; status strip and responsive header |
| WorkCalendar | Calendar + details drawer; schedule sessions | New booking / archive | Framed filters/legend; existing status semantics | DatePicker, filters, booking modal | Empty calendar/details; preserve calendar area, flatten outer sections |
| Planning | Project list + document; author proposal | Export / preview / booking | Studio sample; retain document structure | Reference menus, date modal; document scroll | Autosave/PDF/empty; regression and spacing only |
| Tether | Large image + inspector; capture review | Session/capture controls | Studio sample; preserve image-first structure | Inspector expanders and scroll | Proxy/reference busy; expanded lower inspector reachability |
| OnlineSelection | Project covers and selection content; deliver proof gallery | Create project / project actions | CardSurface; existing themed menus | Create modal, project menus, list scrolling | API busy/error/empty; preserve protocol and original-ratio photography |
| Finance | Monthly ledger + editor; record project money | Income / expense / export | Six outlined metrics; fixed filter columns; raw 17/18/20 type | More filters, date picker; table/editor scroll | Existing empty/error; compact status strip, structural header, table focus |
| History | Project table; resume old jobs | Open / remove / refresh | Tabular data retained for useful columns; theme upgrade | Grid scrolling | Empty history; retain business actions; frozen identity column |
| Toolbox | Catalog tiles; choose existing tools | Title / tool navigation / pin | Tile outlines and hardcoded pin gold | Quick-tool menus; wrapping catalog | Availability stays authoritative; reduce border density |
| ReferenceColor | 21/61/18 image-first workspace | Existing responsive actions | Locked Studio baseline | Existing themed popups/scroll | Regression only; all states and proxy preserved |
| Publishing | Parameters / image / output | Publish / input / presets | ToolPanelCard pile, fixed sidebars | Presets / both rail scrolls | Delayed preview/export; narrow image collapse risk |
| RawToJpeg | Source list + options | Convert / add / output | Themed modal sections | List and settings; native picker external | Known-total conversion; minimum-height risk |
| PhotoGrouping | Source/results + options; organize copies | Analyze / copy / input | PanelBorder and table | Option combos and scroll | Progress/error/result; preserve copy semantics |
| Collage | Large composition + inspector | Export / add / layout | PanelBorder; glyph swap; raw caption type | Template combos; inspector scroll | Preview/export progress; image size and narrow rails |
| Settings | Modal/tab sections; configure preferences | Close / reset | Nested PanelBorder, hardcoded labels | Combos; settings vertical scrolling | Save/error; final action reachability, one dark theme |
| Activation | License summary + form | Activate / device / deactivate | Large PanelBorder | Inputs / content scroll | License errors; danger only deactivation |
| Help | Readable content stack | Tutorial / feedback / logs | Framed text sections | Reading scroll | No busy work except launching existing help; readable width |

## Tool catalog scope

Production: LocalSplit, Workflow, PhotoGrouping, Collage, BatchCompress, Publishing,
ReferenceColor. RAW→JPG is also a formal route outside ToolRegistry. Watermark is
Preview; DeleteRejects, FtpTool, BatchRename and BatchConvert are Hidden. Their disabled
status and route guards must remain intact; they are not reclassified as completed tools.

## Completed closure gates

PageHeader, buttons, typography, symbols, popup, DataGrid, border density, loading,
empty/error, geometry, scroll interaction, accessibility, minimum window/DPI and
Reference/Film regression are covered by the source-bound evidence under
`docs/implementation-reports/studio-global-rollout-evidence/`.

- 113/113 screenshot records are `FULL_SIZE_REVIEWED`.
- 4,960 control text observations: 0 clipped/collision findings.
- 60 close-safe-area states: 0 collisions.
- 244 scroll viewers across 57 states: 0 reachability failures.
- Release build: 0 warnings / 0 errors; Core 1,387 passed + 1 intentional skip;
  DPI 90/90; Modular 14/14; process-isolated WPF 1,295/1,295.

The evidence is source-bound to `1c22ee20f893f3cfcfab43740a9aa015fe6ada0b`.
Physical DPI and hands-on installed acceptance remain outside this in-process gate.

## Final handoff

- State: `READY FOR USER VISUAL REVIEW`
- Routes: 20 production routes exercised (18 formal pages plus formal tool routes)
- Tool pages: RAW→JPG, Organize, Collage, Publishing, Local Split and Batch Compress
- Reference / Film: regression PASS; no new effect or algorithm added
- Installer: NOT GENERATED
- Physical DPI: NOT TESTED
- Next step: GPT + user review the full-app Studio UI, then stop this rollout.
