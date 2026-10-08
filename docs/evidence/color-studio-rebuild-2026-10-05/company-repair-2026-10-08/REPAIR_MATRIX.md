# Company repair scope and acceptance boundary

Status: NOT_READY_FOR_USER_RETEST. VisualApproved=false. UserVerified=false.
The old home issue report and all failure screenshots remain unchanged.
Automated layout/logic tests are not Windows pointer or DPI acceptance.

| Issue | Code change | Automated evidence | Company runtime verdict |
|---|---|---|---|
| CU-01 | Remove inherited Inspector minimum-width overflow; account for margin; wrap modes/actions without shrinking text; bound histogram height | Baseline 950 DIP assertion failed; repaired 950/1020/1180/1440/1760 DIP bounds pass | FAIL: three real window sizes × four actual system scales not completed |
| CU-05 | Text-only numeric draft, finite/range/combined validation; Enter commits, valid blur commits, invalid blur restores; Esc restores before shell navigation; target identity prevents cross-photo draft; curve cancel restores prior stack and redo | Invalid/empty/NaN/out-of-range, decimal, focus/Esc, undo/redo and target-change tests pass | FAIL: native Enter/Tab/blur/Esc full sequence not completed |
| CU-02 | Expand the existing auxiliary dock; bottom dock at narrow widths; reuse the same viewport, camera and settings; cancel captured pointer without later accidental pick | Same viewport/settings/camera and non-overlap bounds tests | FAIL: real continuous rotation/pan/zoom/pick and paired captures pending |
| CU-03 | Freeze source adjustment and exact destination objects when dialog opens; render counts from that request; execution uses the same request despite filter/selection/current-photo changes | Source-only zero, hidden selection exclusion, source not selected, active/selection/filter change tested | FAIL: native boundary workflow pending |
| CU-04 | Resource-bind static labels, hints, menus, analysis/filmstrip/sync/export text and texture/profile choices in three languages; retain stable IDs and user names | Resource coverage/duplicates, live view labels and parameter keys tested | FAIL: known remaining composed error/status text and complete native language/restart/long-text audit pending |
| CU-06 | Keep tone entry outside the histogram inner scroll; preserve RGB/luma at top; collapsing tone clears inspection | First-screen entry and editor-space bounds tested; existing pixel mapping regressions pass | FAIL: native hover/leave/Esc/export-invariance retest pending |

## Known unclosed details

- Some composed errors/statuses in the legacy shared editor, range-selection errors and shell-level menus still require the full language sweep. Do not label CU-04 complete.
- The company baseline was built and launched before editing, but the complete six-issue baseline pointer replay was not achieved. Only CU-01 also has a failing automated reproduction. This does not satisfy the requested six paired runtime proofs.
- Home binaries/private originals/workfiles are not assumed available. Original CC0 mathematical charts are provided by a generator; no sensor/portrait-quality approval is inferred.
- Computer Use capture repeatedly timed out; alternative capture returned unrelated content and was rejected. Native file-dialog actions intermittently timed out or did not submit. PrintWindow captures the actual app but is not an input workaround.
- Native/user-driven changes occurred between observations. Such actions are not attributed to the agent or counted as reproducible acceptance.
