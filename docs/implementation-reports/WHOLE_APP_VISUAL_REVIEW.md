# Whole-app visual review

Product source: `f505a177106a38fc23550edd87949f235238fb79`.
Overall: **PARTIAL / final visual gate FAIL**. Candidate: **NOT GENERATED**.

## Reference and evidence

User's written planning reference: **USED AS DESIGN DENSITY / LAYOUT REFERENCE**.
Eagle references inform search width, compact tools, image priority and grouped menus;
no brand, icon artwork, code or algorithm was copied.

Evidence is real `App.xaml` / `MainWindow` / production services/resources rendered
in process, not HTML, a recreated window or an installed desktop capture. Synthetic
images are explicitly labelled SYNTHETIC and vary in orientation/aspect ratio. Global
navigation is kept visible through the existing test-workspace preference only.

Window request is 1920 × 1080 DIP at 96 logical DPI. The rendered client content is
1906 × 1043 pixels (non-client chrome excluded); popups retain their own dimensions.
Do not describe these as full-desktop 1920 × 1080 captures. No physical 125/150/200%
or 2K/4K screenshot claim is made.

The ninth working-tree review covered all 70 images through all eight contact sheets,
plus full-size checks of datepicker, reference loading, publishing and recent libraries.
The final `review-final` rerun passed and all eight contact sheets plus the global popup
sheet were opened and inspected. Its 70 manifest rows are conservatively PARTIAL,
recording CONTACT_SHEET_VISUAL_INSPECTION rather than blanket whole-product PASS.
Full-size final checks additionally covered planning text, shot list and selected asset.
That check revealed the far-right toolbar overflow icon partly outside the initial viewport.
Fixed in f505a17 by moving horizontal inset from content margin into ScrollViewer padding;
the full-size final selected-state re-capture shows the complete icon inside the viewport.
Contact sheets are a navigable review index; small text still needs full-size images.

## Review criteria

For each recorded image check light leak, overexposure, text overflow, border collision,
density, whitespace, alignment, image dominance, popup theme, radius, loading,
unexpected native WPF chrome, scrollbar and truncation. PASS below refers only to
the **recorded state and viewport**. Timing/hover/focus/other widths are not inferred.

| Module / state family | Visual review | Findings and scope |
| --- | --- | --- |
| Workbench default | PARTIAL | Quiet dark surfaces, empty work area; populated cards and menus not traversed |
| Asset empty/default/content | PASS (recorded) | Dark library header; wide search; source ratios retained; 12 distinct fixtures |
| Asset selected/multiselect/filter | PARTIAL | Filter overlays instead of pushing gallery; expanded inspector remains dense |
| Asset viewer/loupe | PASS (recorded) | Tall image preserved; original-source path used by production viewer; no photographic fidelity claim from synthetic data |
| Asset context/submenus | PASS (recorded) | Compact root, dark popup, readable action groups; workflow/export/manage moved to submenus |
| Recent library menu | PASS (recorded) | Short display name; long path/date/status moved into tooltip |
| Ingest default | PARTIAL | Layout readable; empty-only evidence, no processing/recovery state |
| Calendar default | PARTIAL | Month/schedule layout dark; long booking content untested |
| Planning default / 文字 | PASS (recorded) | Narrow document rail, quiet header, seven modules, responsive hero proportions, reading-first text |
| Planning reference/moodboard/lighting/styling | PARTIAL | Original ratios retained; lower content scrolls; mixed repeated menu icons remain |
| Planning shots / files | PARTIAL | Shot rows retain hierarchy; files empty; no populated attachment proof |
| Planning preview | PASS (recorded) | Planning rail/tools hidden; global app navigation unchanged |
| Planning create / datepicker | PASS (recorded) | Dark elevated modal; seven weekday labels restored; no white calendar blocks |
| Tether default | PARTIAL | Pre-session state only; no live camera, right inspector or second-display visual acceptance |
| Online selection / finance / history | PARTIAL | Actual empty/default layouts; content and error states missing |
| Toolbox default | PASS (recorded) | Existing Chinese routes and low-density tool cards retained |
| Reference empty | PASS (recorded) | Separate 待调色照片 / 参考图片 entries; no target/reference role ambiguity |
| Reference loaded / four comparisons | PASS (recorded) | Target dominates; source-reference thumbnail separate; split and side-by-side labels identify 原片 / 仿色结果 |
| Reference loading | PARTIAL | Busy status/progress visible, dark disabled controls; latency thresholds and all recovery states unmeasured |
| Publishing empty/content/preset | PARTIAL | Large real preview; header/footer no collision at recorded size; narrow/DPI output states missing |
| RAW / organize / collage | PARTIAL | Empty/default surfaces only; populated output/error states missing |
| Settings six tabs/dropdowns | PASS (recorded) | Dark inputs/toggles, no bright selected items; fake unbound preference removed |
| License default | PARTIAL | Actual free-state UI; long identity/key/error strings untested |
| Main menu / reference combo / settings / preset popups | PASS (recorded) | Shared dark surfaces; no white block in captured inventory |

## Fix / re-capture log

1. Initial one-pixel fixtures deduplicated into a single black asset: replaced test data
   with 12 distinct synthetic images, not a product import fix.
2. Global context-menu style unification, explicit dark dropdown/check/radio/toggle/
   progress/calendar templates removed visible native bright surfaces.
3. Calendar day title resource belongs in CalendarItem template resources. First dark
   re-capture exposed missing weekdays; corrected and added runtime heading validation.
4. Reference collection reset erased scheme selection; fixed and re-captured with real
   selected scheme, thumbnail, all comparisons and asynchronous busy state.
5. Publishing native toggles and disabled reference arrows were bright; dark ToggleButton
   template corrected both. Publishing footer/header separated to avoid overlap.
6. Narrow asset toolbar accessibility regression fixed by retaining bounded horizontal
   scrolling; tests assert trailing redo can be reached.
7. Final sheets do not enlarge small popups; global popup sheet retains original scale.

## Remaining gates

TEXT_OVERFLOW_GATE: PARTIAL. Existing viewport regressions and recorded 1080p states
pass their checks; no exhaustive visible-text rectangle audit at all four scale factors.
Border density: PARTIAL (asset inspector, reference parameter panel, some legacy tools).
Loading: PARTIAL. Whole-app state coverage: PARTIAL. Physical display acceptance: NOT RUN.
The native capture interface failed twice with unsupported interface; no alternate
desktop capture was used to misrepresent this limitation.

Known toolbar clipping was fixed and re-captured. Global P0 count is **not certified zero**. No remaining white popup block was
seen in the recorded inventory, but unvisited states cannot be certified. Open P1s are
listed in the logic audit. Do not promote to READY FOR GPT REVIEW until the requested
full-state and interaction gates are genuinely complete.
