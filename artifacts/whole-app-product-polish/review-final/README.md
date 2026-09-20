# Pixel Tart review evidence (partial)

Source: `f505a177106a38fc23550edd87949f235238fb79`.
70 actual production WPF renders, 17 requested routes, 23 popup states.
Eight whole-app sheets (9,9,9,9,9,9,9,7 images) plus one global popup sheet.
All sheets were opened and visually inspected. Manifest review is conservatively PARTIAL.

This is **not** installed desktop, photography-fidelity or full-state acceptance.
App/MainWindow/services/resource dictionaries are production; synthetic images are test data.
1920×1080 DIP window, 1906×1043 px client render, 96 DPI; popups have local size.
State filenames describe the exercised render, not proof of all interaction transitions.

No Candidate or installer is included. See repository reports:

- `docs/implementation-reports/WHOLE_APP_PRODUCT_POLISH_CLOSURE.md`
- `docs/implementation-reports/WHOLE_APP_VISUAL_REVIEW.md`
- `docs/implementation-reports/WHOLE_APP_PRODUCT_LOGIC_AUDIT.md`

Producer: `WholeAppVisualAcceptanceTests.ProductionApp_AllRoutes_RenderReviewInventory`.
Use unique isolated acceptance and evidence roots; never run two production App tests concurrently.
The source-bound TRX is in `../tests-final-source/visual.trx`.
