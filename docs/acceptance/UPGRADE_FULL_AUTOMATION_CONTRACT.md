# UPGRADE_FULL_AUTOMATION_CONTRACT

Product source: `f505a177106a38fc23550edd87949f235238fb79`. Evidence: `artifacts/whole-app-product-polish/peers-final`.

Interim exhaustive structural inventory, not a passed readiness gate. VERIFIED means an exact scoped/type/pattern match in production peer snapshots, not that the formal step transition was replayed. PLAN_BUG includes missing proof/contract coverage; it does not assert a product defect. NOT_APPLICABLE means selector-free only. Native OS dialogs and upgrade execution remain unproven.

| Step | Stage | Action | Selector | Pattern | Status | Peer evidence |
| --- | --- | --- | --- | --- | --- | --- |
| startup-alive | BOOT | assertProcessAlive |  |  | NOT_APPLICABLE |  |
| exit-tutorial | ONBOARDING | invoke | selector-exit-tutorial | Invoke | VERIFIED | ONBOARDING |
| tutorial-gone | ONBOARDING | waitAbsent | selector-exit-tutorial |  | VERIFIED | BOOT |
| planning-enabled | PRIMARY_NAVIGATION | assertEnabled | selector-planning-enabled |  | VERIFIED | ONBOARDING, BOOT, TETHER, ONLINE_SELECTION |
| planning | PRIMARY_NAVIGATION | invoke | selector-planning-enabled | Invoke | VERIFIED | ONBOARDING, BOOT, TETHER, ONLINE_SELECTION |
| planning-list | PLANNING_LIST | assertPresent | selector-planning-list |  | VERIFIED | PLANNING_LIST, CREATE_PLANNING_MODAL, CREATED_PROJECT, TEXT_READING, TEXT_EDITOR, TEXT_SAVED, EMPTY-文字, EMPTY-参考图, EMPTY-情绪板, EMPTY-镜头清单, EMPTY-灯光图, EMPTY-服化道, EMPTY-文件, REFERENCE_IMPORTED, 参考图, QUICK_PREVIEW, 情绪板, 灯光图, 服化道, MODULE-文字, MODULE-参考图, MODULE-情绪板, MODULE-镜头清单, MODULE-灯光图, MODULE-服化道, MODULE-文件, BOOKING_PICKER, EXPORT_DIALOG |
| create-open | CREATE_PLANNING_MODAL | invoke | selector-create-open | Invoke | VERIFIED | CREATED_PROJECT, TEXT_READING, TEXT_EDITOR, TEXT_SAVED, EMPTY-文字, EMPTY-参考图, EMPTY-情绪板, EMPTY-镜头清单, EMPTY-灯光图, EMPTY-服化道, EMPTY-文件, REFERENCE_IMPORTED, 参考图, QUICK_PREVIEW, 情绪板, 灯光图, 服化道, MODULE-文字, MODULE-参考图, MODULE-情绪板, MODULE-镜头清单, MODULE-灯光图, MODULE-服化道, MODULE-文件, BOOKING_PICKER, EXPORT_DIALOG |
| create-heading | CREATE_PLANNING_MODAL | assertPresent | selector-create-heading |  | VERIFIED | CREATE_PLANNING_MODAL |
| create-name | CREATE_PLANNING_MODAL | setValue | selector-create-name | Value | VERIFIED | CREATE_PLANNING_MODAL |
| create-location | CREATE_PLANNING_MODAL | setValue | selector-create-location | Value | VERIFIED | CREATE_PLANNING_MODAL |
| date-value | DATE_PICKER | setValue | selector-date-value | Value | VERIFIED | CREATE_PLANNING_MODAL |
| date-open | DATE_PICKER | invoke | selector-date-open | Invoke | VERIFIED | CREATE_PLANNING_MODAL |
| date-calendar | DATE_PICKER | assertPresent | selector-date-calendar |  | VERIFIED | DATE_PICKER |
| date-next-day | DATE_PICKER | focusKey | selector-date-calendar |  | VERIFIED | DATE_PICKER |
| date-confirm | DATE_PICKER | focusKey | selector-date-calendar |  | VERIFIED | DATE_PICKER |
| date-check | DATE_PICKER | assertDate | selector-date-value | Value | VERIFIED | CREATE_PLANNING_MODAL |
| create-confirm | CREATE_PLANNING_MODAL | invoke | selector-create-confirm | Invoke | VERIFIED | CREATE_PLANNING_MODAL |
| create-closed | CREATE_PLANNING_MODAL | waitAbsent | selector-create-heading |  | VERIFIED | CREATED_PROJECT |
| created-list-item | CREATE_PLANNING_MODAL | assertPresent | selector-created-list-item |  | VERIFIED | CREATED_PROJECT, TEXT_READING, TEXT_EDITOR, TEXT_SAVED, EMPTY-文字, EMPTY-参考图, EMPTY-情绪板, EMPTY-镜头清单, EMPTY-灯光图, EMPTY-服化道, EMPTY-文件, REFERENCE_IMPORTED, 参考图, QUICK_PREVIEW, 情绪板, 灯光图, 服化道, MODULE-文字, MODULE-参考图, MODULE-情绪板, MODULE-镜头清单, MODULE-灯光图, MODULE-服化道, MODULE-文件, BOOKING_PICKER, EXPORT_DIALOG |
| select-created | PLANNING_LIST | select | selector-created-list-item | SelectionItem | VERIFIED | CREATED_PROJECT, TEXT_READING, TEXT_EDITOR, TEXT_SAVED, EMPTY-文字, EMPTY-参考图, EMPTY-情绪板, EMPTY-镜头清单, EMPTY-灯光图, EMPTY-服化道, EMPTY-文件, REFERENCE_IMPORTED, 参考图, QUICK_PREVIEW, 情绪板, 灯光图, 服化道, MODULE-文字, MODULE-参考图, MODULE-情绪板, MODULE-镜头清单, MODULE-灯光图, MODULE-服化道, MODULE-文件, BOOKING_PICKER, EXPORT_DIALOG |
| text-open | TEXT_EDITOR | invoke | selector-text-open | Invoke | VERIFIED | PLANNING_LIST, CREATE_PLANNING_MODAL, CREATED_PROJECT, TEXT_READING, TEXT_EDITOR, TEXT_SAVED, EMPTY-文字, EMPTY-参考图, EMPTY-情绪板, EMPTY-镜头清单, EMPTY-灯光图, EMPTY-服化道, EMPTY-文件, REFERENCE_IMPORTED, 参考图, QUICK_PREVIEW, 情绪板, 灯光图, 服化道, MODULE-文字, MODULE-参考图, MODULE-情绪板, MODULE-镜头清单, MODULE-灯光图, MODULE-服化道, MODULE-文件, BOOKING_PICKER, EXPORT_DIALOG |
| edit | TEXT_EDITOR | invoke | selector-edit | Invoke | VERIFIED | CREATED_PROJECT, TEXT_READING, TEXT_SAVED, EMPTY-文字, MODULE-文字, BOOKING_PICKER, EXPORT_DIALOG |
| edit-body | TEXT_EDITOR | setValue | selector-edit-body | Value | VERIFIED | TEXT_EDITOR |
| edit-verify | TEXT_EDITOR | assertText | selector-edit-body |  | VERIFIED | TEXT_EDITOR |
| save-switch | PERSISTENCE | invoke | selector-save-switch | Invoke | VERIFIED | PLANNING_LIST, CREATE_PLANNING_MODAL, CREATED_PROJECT, TEXT_READING, TEXT_EDITOR, TEXT_SAVED, EMPTY-文字, EMPTY-参考图, EMPTY-情绪板, EMPTY-镜头清单, EMPTY-灯光图, EMPTY-服化道, EMPTY-文件, REFERENCE_IMPORTED, 参考图, QUICK_PREVIEW, 情绪板, 灯光图, 服化道, MODULE-文字, MODULE-参考图, MODULE-情绪板, MODULE-镜头清单, MODULE-灯光图, MODULE-服化道, MODULE-文件, BOOKING_PICKER, EXPORT_DIALOG |
| save-return | PERSISTENCE | invoke | selector-text-open | Invoke | VERIFIED | PLANNING_LIST, CREATE_PLANNING_MODAL, CREATED_PROJECT, TEXT_READING, TEXT_EDITOR, TEXT_SAVED, EMPTY-文字, EMPTY-参考图, EMPTY-情绪板, EMPTY-镜头清单, EMPTY-灯光图, EMPTY-服化道, EMPTY-文件, REFERENCE_IMPORTED, 参考图, QUICK_PREVIEW, 情绪板, 灯光图, 服化道, MODULE-文字, MODULE-参考图, MODULE-情绪板, MODULE-镜头清单, MODULE-灯光图, MODULE-服化道, MODULE-文件, BOOKING_PICKER, EXPORT_DIALOG |
| save-content | PERSISTENCE | assertPresent | selector-save-content |  | VERIFIED | TEXT_SAVED, EMPTY-文字, FRESH_PREVIEW, MODULE-文字, PREVIEW_MODE, BOOKING_PICKER, EXPORT_DIALOG, TETHER |
| restart-close-request | NORMAL_CLOSE | requestClose |  |  | NOT_APPLICABLE |  |
| restart-close-confirm | NORMAL_CLOSE | waitExit |  |  | NOT_APPLICABLE |  |
| restart-close | NORMAL_CLOSE | waitExit |  |  | NOT_APPLICABLE |  |
| upgrade-install | UPGRADE | upgrade |  |  | NOT_APPLICABLE |  |
| restart | PERSISTENCE | restart |  |  | NOT_APPLICABLE |  |
| restart-exit-tutorial | PERSISTENCE | invoke | selector-exit-tutorial | Invoke | VERIFIED | ONBOARDING |
| restart-planning | PERSISTENCE | invoke | selector-planning-enabled | Invoke | VERIFIED | ONBOARDING, BOOT, TETHER, ONLINE_SELECTION |
| restart-select | PERSISTENCE | select | selector-created-list-item | SelectionItem | VERIFIED | CREATED_PROJECT, TEXT_READING, TEXT_EDITOR, TEXT_SAVED, EMPTY-文字, EMPTY-参考图, EMPTY-情绪板, EMPTY-镜头清单, EMPTY-灯光图, EMPTY-服化道, EMPTY-文件, REFERENCE_IMPORTED, 参考图, QUICK_PREVIEW, 情绪板, 灯光图, 服化道, MODULE-文字, MODULE-参考图, MODULE-情绪板, MODULE-镜头清单, MODULE-灯光图, MODULE-服化道, MODULE-文件, BOOKING_PICKER, EXPORT_DIALOG |
| restart-text | PERSISTENCE | invoke | selector-text-open | Invoke | VERIFIED | PLANNING_LIST, CREATE_PLANNING_MODAL, CREATED_PROJECT, TEXT_READING, TEXT_EDITOR, TEXT_SAVED, EMPTY-文字, EMPTY-参考图, EMPTY-情绪板, EMPTY-镜头清单, EMPTY-灯光图, EMPTY-服化道, EMPTY-文件, REFERENCE_IMPORTED, 参考图, QUICK_PREVIEW, 情绪板, 灯光图, 服化道, MODULE-文字, MODULE-参考图, MODULE-情绪板, MODULE-镜头清单, MODULE-灯光图, MODULE-服化道, MODULE-文件, BOOKING_PICKER, EXPORT_DIALOG |
| persisted-body | PERSISTENCE | assertPresent | selector-save-content |  | VERIFIED | TEXT_SAVED, EMPTY-文字, FRESH_PREVIEW, MODULE-文字, PREVIEW_MODE, BOOKING_PICKER, EXPORT_DIALOG, TETHER |
| module-text | CONTENT_NAVIGATION | invoke | selector-text-open | Invoke | VERIFIED | PLANNING_LIST, CREATE_PLANNING_MODAL, CREATED_PROJECT, TEXT_READING, TEXT_EDITOR, TEXT_SAVED, EMPTY-文字, EMPTY-参考图, EMPTY-情绪板, EMPTY-镜头清单, EMPTY-灯光图, EMPTY-服化道, EMPTY-文件, REFERENCE_IMPORTED, 参考图, QUICK_PREVIEW, 情绪板, 灯光图, 服化道, MODULE-文字, MODULE-参考图, MODULE-情绪板, MODULE-镜头清单, MODULE-灯光图, MODULE-服化道, MODULE-文件, BOOKING_PICKER, EXPORT_DIALOG |
| landmark-text | TEXT_EDITOR | assertPresent | selector-save-content |  | VERIFIED | TEXT_SAVED, EMPTY-文字, FRESH_PREVIEW, MODULE-文字, PREVIEW_MODE, BOOKING_PICKER, EXPORT_DIALOG, TETHER |
| capture-text | TEXT_EDITOR | capture |  |  | NOT_APPLICABLE |  |
| module-references | CONTENT_NAVIGATION | invoke | selector-save-switch | Invoke | VERIFIED | PLANNING_LIST, CREATE_PLANNING_MODAL, CREATED_PROJECT, TEXT_READING, TEXT_EDITOR, TEXT_SAVED, EMPTY-文字, EMPTY-参考图, EMPTY-情绪板, EMPTY-镜头清单, EMPTY-灯光图, EMPTY-服化道, EMPTY-文件, REFERENCE_IMPORTED, 参考图, QUICK_PREVIEW, 情绪板, 灯光图, 服化道, MODULE-文字, MODULE-参考图, MODULE-情绪板, MODULE-镜头清单, MODULE-灯光图, MODULE-服化道, MODULE-文件, BOOKING_PICKER, EXPORT_DIALOG |
| landmark-references | TEXT_EDITOR | assertPresent | selector-landmark-references |  | VERIFIED | EMPTY-参考图 |
| capture-references | TEXT_EDITOR | capture |  |  | NOT_APPLICABLE |  |
| module-moodboard | CONTENT_NAVIGATION | invoke | selector-module-moodboard | Invoke | VERIFIED | PLANNING_LIST, CREATE_PLANNING_MODAL, CREATED_PROJECT, TEXT_READING, TEXT_EDITOR, TEXT_SAVED, EMPTY-文字, EMPTY-参考图, EMPTY-情绪板, EMPTY-镜头清单, EMPTY-灯光图, EMPTY-服化道, EMPTY-文件, REFERENCE_IMPORTED, 参考图, QUICK_PREVIEW, 情绪板, 灯光图, 服化道, MODULE-文字, MODULE-参考图, MODULE-情绪板, MODULE-镜头清单, MODULE-灯光图, MODULE-服化道, MODULE-文件, BOOKING_PICKER, EXPORT_DIALOG |
| landmark-moodboard | MOODBOARD | assertPresent | selector-landmark-moodboard |  | VERIFIED | EMPTY-情绪板 |
| capture-moodboard | MOODBOARD | capture |  |  | NOT_APPLICABLE |  |
| module-shots | CONTENT_NAVIGATION | invoke | selector-module-shots | Invoke | VERIFIED | PLANNING_LIST, CREATE_PLANNING_MODAL, CREATED_PROJECT, TEXT_READING, TEXT_EDITOR, TEXT_SAVED, EMPTY-文字, EMPTY-参考图, EMPTY-情绪板, EMPTY-镜头清单, EMPTY-灯光图, EMPTY-服化道, EMPTY-文件, REFERENCE_IMPORTED, 参考图, QUICK_PREVIEW, 情绪板, 灯光图, 服化道, MODULE-文字, MODULE-参考图, MODULE-情绪板, MODULE-镜头清单, MODULE-灯光图, MODULE-服化道, MODULE-文件, BOOKING_PICKER, EXPORT_DIALOG |
| landmark-shots | SHOT_LIST | assertPresent | selector-landmark-shots |  | VERIFIED | EMPTY-镜头清单 |
| capture-shots | SHOT_LIST | capture |  |  | NOT_APPLICABLE |  |
| module-lighting | CONTENT_NAVIGATION | invoke | selector-module-lighting | Invoke | VERIFIED | PLANNING_LIST, CREATE_PLANNING_MODAL, CREATED_PROJECT, TEXT_READING, TEXT_EDITOR, TEXT_SAVED, EMPTY-文字, EMPTY-参考图, EMPTY-情绪板, EMPTY-镜头清单, EMPTY-灯光图, EMPTY-服化道, EMPTY-文件, REFERENCE_IMPORTED, 参考图, QUICK_PREVIEW, 情绪板, 灯光图, 服化道, MODULE-文字, MODULE-参考图, MODULE-情绪板, MODULE-镜头清单, MODULE-灯光图, MODULE-服化道, MODULE-文件, BOOKING_PICKER, EXPORT_DIALOG |
| landmark-lighting | LIGHTING | assertPresent | selector-landmark-lighting |  | VERIFIED | EMPTY-灯光图, MODULE-灯光图 |
| capture-lighting | LIGHTING | capture |  |  | NOT_APPLICABLE |  |
| module-styling | CONTENT_NAVIGATION | invoke | selector-module-styling | Invoke | VERIFIED | PLANNING_LIST, CREATE_PLANNING_MODAL, CREATED_PROJECT, TEXT_READING, TEXT_EDITOR, TEXT_SAVED, EMPTY-文字, EMPTY-参考图, EMPTY-情绪板, EMPTY-镜头清单, EMPTY-灯光图, EMPTY-服化道, EMPTY-文件, REFERENCE_IMPORTED, 参考图, QUICK_PREVIEW, 情绪板, 灯光图, 服化道, MODULE-文字, MODULE-参考图, MODULE-情绪板, MODULE-镜头清单, MODULE-灯光图, MODULE-服化道, MODULE-文件, BOOKING_PICKER, EXPORT_DIALOG |
| landmark-styling | STYLING | assertPresent | selector-landmark-styling |  | VERIFIED | EMPTY-服化道 |
| capture-styling | STYLING | capture |  |  | NOT_APPLICABLE |  |
| module-files | CONTENT_NAVIGATION | invoke | selector-module-files | Invoke | VERIFIED | PLANNING_LIST, CREATE_PLANNING_MODAL, CREATED_PROJECT, TEXT_READING, TEXT_EDITOR, TEXT_SAVED, EMPTY-文字, EMPTY-参考图, EMPTY-情绪板, EMPTY-镜头清单, EMPTY-灯光图, EMPTY-服化道, EMPTY-文件, REFERENCE_IMPORTED, 参考图, QUICK_PREVIEW, 情绪板, 灯光图, 服化道, MODULE-文字, MODULE-参考图, MODULE-情绪板, MODULE-镜头清单, MODULE-灯光图, MODULE-服化道, MODULE-文件, BOOKING_PICKER, EXPORT_DIALOG |
| landmark-files | FILES | assertPresent | selector-landmark-files |  | VERIFIED | EMPTY-文件, MODULE-文件 |
| capture-files | FILES | capture |  |  | NOT_APPLICABLE |  |
| preview-text | PREVIEW_MODE | invoke | selector-text-open | Invoke | VERIFIED | PLANNING_LIST, CREATE_PLANNING_MODAL, CREATED_PROJECT, TEXT_READING, TEXT_EDITOR, TEXT_SAVED, EMPTY-文字, EMPTY-参考图, EMPTY-情绪板, EMPTY-镜头清单, EMPTY-灯光图, EMPTY-服化道, EMPTY-文件, REFERENCE_IMPORTED, 参考图, QUICK_PREVIEW, 情绪板, 灯光图, 服化道, MODULE-文字, MODULE-参考图, MODULE-情绪板, MODULE-镜头清单, MODULE-灯光图, MODULE-服化道, MODULE-文件, BOOKING_PICKER, EXPORT_DIALOG |
| preview-open | PREVIEW_MODE | invoke | selector-preview-open | Invoke | VERIFIED | PLANNING_LIST, CREATE_PLANNING_MODAL, CREATED_PROJECT, TEXT_READING, TEXT_EDITOR, TEXT_SAVED, EMPTY-文字, EMPTY-参考图, EMPTY-情绪板, EMPTY-镜头清单, EMPTY-灯光图, EMPTY-服化道, EMPTY-文件, REFERENCE_IMPORTED, 参考图, QUICK_PREVIEW, 情绪板, 灯光图, 服化道, MODULE-文字, MODULE-参考图, MODULE-情绪板, MODULE-镜头清单, MODULE-灯光图, MODULE-服化道, MODULE-文件, BOOKING_PICKER, EXPORT_DIALOG |
| preview-list-hidden | PREVIEW_MODE | assertAbsent | selector-planning-list |  | VERIFIED | FRESH_PREVIEW |
| preview-nav-hidden | PREVIEW_MODE | assertAbsent | selector-text-open |  | VERIFIED | FRESH_PREVIEW |
| preview-edit-hidden | PREVIEW_MODE | assertAbsent | selector-edit |  | VERIFIED | FRESH_PREVIEW |
| preview-body | PREVIEW_MODE | assertPresent | selector-save-content |  | VERIFIED | TEXT_SAVED, EMPTY-文字, FRESH_PREVIEW, MODULE-文字, PREVIEW_MODE, BOOKING_PICKER, EXPORT_DIALOG, TETHER |
| preview-shot | PREVIEW_MODE | capture |  |  | NOT_APPLICABLE |  |
| preview-escape | PREVIEW_MODE | focusKey | selector-preview-escape |  | VERIFIED | PLANNING_LIST, CREATE_PLANNING_MODAL, CREATED_PROJECT, TEXT_READING, TEXT_EDITOR, TEXT_SAVED, EMPTY-文字, EMPTY-参考图, EMPTY-情绪板, EMPTY-镜头清单, EMPTY-灯光图, EMPTY-服化道, EMPTY-文件, FRESH_PREVIEW, REFERENCE_IMPORTED, 参考图, QUICK_PREVIEW, 情绪板, 灯光图, 服化道, MODULE-文字, MODULE-参考图, MODULE-情绪板, MODULE-镜头清单, MODULE-灯光图, MODULE-服化道, MODULE-文件, PREVIEW_MODE, BOOKING_PICKER, EXPORT_DIALOG |
| preview-restored | PREVIEW_MODE | assertPresent | selector-planning-list |  | VERIFIED | PLANNING_LIST, CREATE_PLANNING_MODAL, CREATED_PROJECT, TEXT_READING, TEXT_EDITOR, TEXT_SAVED, EMPTY-文字, EMPTY-参考图, EMPTY-情绪板, EMPTY-镜头清单, EMPTY-灯光图, EMPTY-服化道, EMPTY-文件, REFERENCE_IMPORTED, 参考图, QUICK_PREVIEW, 情绪板, 灯光图, 服化道, MODULE-文字, MODULE-参考图, MODULE-情绪板, MODULE-镜头清单, MODULE-灯光图, MODULE-服化道, MODULE-文件, BOOKING_PICKER, EXPORT_DIALOG |
| final-close-request | NORMAL_CLOSE | requestClose |  |  | NOT_APPLICABLE |  |
| final-close-confirm | NORMAL_CLOSE | waitExit |  |  | NOT_APPLICABLE |  |
| final-close | NORMAL_CLOSE | waitExit |  |  | NOT_APPLICABLE |  |
