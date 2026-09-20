# AcceptanceKit v3 — Full Selector Audit

Static source/recorded-tree audit, not live verification. Fresh: 124 steps, 0 ERROR, 112 WARNING. Upgrade: 70 steps, 0 ERROR, 59 WARNING. Date fix retained; SidebarRoot is forbidden as a search ancestor. All global navigation uses ScopePreset=PrimaryNavigation → SidebarNavigationScroll / Pane.

## Scope evidence and limitations

- MainWindow.xaml: SidebarRoot is AutomationLandmark (sibling of navigation ScrollViewer); SidebarNavigationScroll contains all 13 requested navigation buttons. Do not infer ancestry from the flat UIA dump.
- PlanningCenterView.xaml: actual Custom workspace, DocumentScroll Pane, DocumentList List observed in the supplied tree. Modal heading parent follows the explicit RenderModal/Export_Click StackPanel construction. Date scope is its Custom DatePicker and native PART_TextBox / Edit.
- ContentNavigation UniformGrid is not exposed in the recorded tree; typed module buttons are scoped to the real workspace. No fabricated navigation-container peer.
- Detached ContextMenu: unique visible Menu of tested PID, then MenuItem; not a child of the underlying image. Calendar: unique visible Calendar rather than an invented PART_Calendar ID/name. Both require live state proof.
- File dialogs: exact Window name/type, then native ID 1148 Edit / 1 Button. PDF quality: ComboBox name/type then ListItem. Native provider and popup ancestry remain unverified.
- ThemedMessageDialog: explicit Window name and MessageTextBlock / YesButton. Reference StackPanel Custom peer and Tether/Online Custom roots are source-derived and require live evidence; no declaration of all 124 live selectors passing.
- Scope absence in positive assertions polls then reports Scope resolution failed; ambiguous scopes fail immediately. For explicitly negative assertions, absent scope proves absent target. Optional confirmations may skip an absent dialog; duplicate matches never skip.
- Screenshot membership uses the formal typed selector when determinable. Legacy bounds fallback remains a strict unique query, never index selection; state-specific popup verification is still required.

## Fresh plan — all 124 steps

| Step | Category | Action | Scope | ID / Name / Type | Expected count |
|---|---|---|---|---|---|
| startup-alive | LIFECYCLE_OR_EVIDENCE | assertProcessAlive | N/A |  /  /  | N/A |
| exit-tutorial | PLANNING_CONTENT | invoke | MainWindow | TutorialExitButton /  / Button | 0 or 1 |
| tutorial-gone | PLANNING_CONTENT | waitAbsent | MainWindow | TutorialExitButton /  / Button | 0 |
| planning-enabled | GLOBAL_NAV | assertEnabled | PrimaryNavigation | PrimaryNavigationPlanning /  / Button | 1 |
| planning | GLOBAL_NAV | invoke | PrimaryNavigation | PrimaryNavigationPlanning /  / Button | 1 |
| planning-list | PLANNING_CONTENT | assertPresent | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false}] | DocumentList / 策划案列表 / List | 1 |
| home-shot | LIFECYCLE_OR_EVIDENCE | capture | N/A |  /  /  | N/A |
| create-open | PLANNING_MODAL | invoke | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false}] |  / 新建策划 / Button | 1 |
| create-heading | PLANNING_MODAL | assertPresent | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false}] |  / 新建策划案 / Text | 1 |
| create-name | PLANNING_MODAL | setValue | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false},{"Name":"新建策划案","AutomationId":null,"ControlType":"Text","Heading":true}] |  / 新策划名称 / Edit | 1 |
| create-location | PLANNING_MODAL | setValue | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false},{"Name":"新建策划案","AutomationId":null,"ControlType":"Text","Heading":true}] |  / 拍摄地点 / Edit | 1 |
| date-value | DATE_PICKER | setValue | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false},{"Name":"新建策划案","AutomationId":null,"ControlType":"Text","Heading":true},{"Name":"拍摄日期","AutomationId":null,"ControlType":"Custom","Heading":false}] | PART_TextBox / 拍摄日期 / Edit | 1 |
| date-open | DATE_PICKER | invoke | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false},{"Name":"新建策划案","AutomationId":null,"ControlType":"Text","Heading":true},{"Name":"拍摄日期","AutomationId":null,"ControlType":"Custom","Heading":false}] | PART_Button / 选择拍摄日期 / Button | 1 |
| date-calendar | DATE_PICKER | assertPresent | [{"Name":null,"AutomationId":null,"ControlType":"Calendar","Heading":false}] |  /  / Calendar | 1 |
| date-shot | DATE_PICKER | capture | N/A |  /  /  | N/A |
| date-next-day | DATE_PICKER | focusKey | [{"Name":null,"AutomationId":null,"ControlType":"Calendar","Heading":false}] |  /  / Calendar | 1 |
| date-confirm | DATE_PICKER | focusKey | [{"Name":null,"AutomationId":null,"ControlType":"Calendar","Heading":false}] |  /  / Calendar | 1 |
| date-check | DATE_PICKER | assertDate | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false},{"Name":"新建策划案","AutomationId":null,"ControlType":"Text","Heading":true},{"Name":"拍摄日期","AutomationId":null,"ControlType":"Custom","Heading":false}] | PART_TextBox / 拍摄日期 / Edit | 1 |
| create-confirm | PLANNING_MODAL | invoke | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false},{"Name":"新建策划案","AutomationId":null,"ControlType":"Text","Heading":true}] |  / 新建策划 / Button | 1 |
| create-closed | PLANNING_MODAL | waitAbsent | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false}] |  / 新建策划案 / Text | 0 |
| created-list-item | PLANNING_MODAL | assertPresent | DocumentList |  / {project} / ListItem | 1 |
| select-created | PLANNING_CONTENT | select | DocumentList |  / {project} / ListItem | 1 |
| text-open | PLANNING_CONTENT_NAV | invoke | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false}] |  / 文字 / Button | 1 |
| edit | PLANNING_CONTENT | invoke | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false},{"Name":null,"AutomationId":"DocumentScroll","ControlType":"Pane","Heading":false}] |  / 编辑 / Button | 1 |
| edit-body | PLANNING_CONTENT | setValue | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false},{"Name":null,"AutomationId":"DocumentScroll","ControlType":"Pane","Heading":false}] |  / 策划正文 / Edit | 1 |
| edit-verify | PLANNING_CONTENT | assertText | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false},{"Name":null,"AutomationId":"DocumentScroll","ControlType":"Pane","Heading":false}] |  / 策划正文 / Edit | 1 |
| save-switch | PLANNING_CONTENT_NAV | invoke | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false}] |  / 参考图 / Button | 1 |
| save-return | PLANNING_CONTENT_NAV | invoke | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false}] |  / 文字 / Button | 1 |
| save-content | PLANNING_CONTENT | assertPresent | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false},{"Name":null,"AutomationId":"DocumentScroll","ControlType":"Pane","Heading":false}] |  / Installed Acceptance Persistence {shortsha} / Text | 1 |
| restart-close-request | LIFECYCLE_OR_EVIDENCE | requestClose | N/A |  /  /  | N/A |
| restart-close-confirm | PLANNING_CONTENT | invoke | 像素蛋挞消息对话框 |  / 保存 / Button | 0 or 1 |
| restart-close | LIFECYCLE_OR_EVIDENCE | waitExit | N/A |  /  /  | N/A |
| restart | LIFECYCLE_OR_EVIDENCE | restart | N/A |  /  /  | N/A |
| restart-exit-tutorial | PLANNING_CONTENT | invoke | MainWindow | TutorialExitButton /  / Button | 0 or 1 |
| restart-planning | GLOBAL_NAV | invoke | PrimaryNavigation | PrimaryNavigationPlanning /  / Button | 1 |
| restart-select | PLANNING_CONTENT | select | DocumentList |  / {project} / ListItem | 1 |
| restart-text | PLANNING_CONTENT_NAV | invoke | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false}] |  / 文字 / Button | 1 |
| persisted-body | PLANNING_CONTENT | assertPresent | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false},{"Name":null,"AutomationId":"DocumentScroll","ControlType":"Pane","Heading":false}] |  / Installed Acceptance Persistence {shortsha} / Text | 1 |
| module-text | PLANNING_CONTENT_NAV | invoke | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false}] |  / 文字 / Button | 1 |
| landmark-text | PLANNING_CONTENT | assertPresent | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false},{"Name":null,"AutomationId":"DocumentScroll","ControlType":"Pane","Heading":false}] |  / Installed Acceptance Persistence {shortsha} / Text | 1 |
| capture-text | LIFECYCLE_OR_EVIDENCE | capture | N/A |  /  /  | N/A |
| module-references | PLANNING_CONTENT_NAV | invoke | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false}] |  / 参考图 / Button | 1 |
| landmark-references | PLANNING_CONTENT | assertPresent | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false},{"Name":null,"AutomationId":"DocumentScroll","ControlType":"Pane","Heading":false}] |  / 还没有参考图。关联已有参考，或从本地添加；原图保持不变。 / Text | 1 |
| capture-references | LIFECYCLE_OR_EVIDENCE | capture | N/A |  /  /  | N/A |
| module-moodboard | PLANNING_CONTENT_NAV | invoke | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false}] |  / 情绪板 / Button | 1 |
| landmark-moodboard | PLANNING_CONTENT | assertPresent | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false},{"Name":null,"AutomationId":"DocumentScroll","ControlType":"Pane","Heading":false}] |  / 还没有情绪板。关联已有参考，或从本地添加；原图保持不变。 / Text | 1 |
| capture-moodboard | LIFECYCLE_OR_EVIDENCE | capture | N/A |  /  /  | N/A |
| module-shots | PLANNING_CONTENT_NAV | invoke | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false}] |  / 镜头清单 / Button | 1 |
| landmark-shots | PLANNING_CONTENT | assertPresent | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false},{"Name":null,"AutomationId":"DocumentScroll","ControlType":"Pane","Heading":false}] |  / 0 / 0 已拍 / Text | 1 |
| capture-shots | LIFECYCLE_OR_EVIDENCE | capture | N/A |  /  /  | N/A |
| module-lighting | PLANNING_CONTENT_NAV | invoke | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false}] |  / 灯光图 / Button | 1 |
| landmark-lighting | PLANNING_CONTENT | assertPresent | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false},{"Name":null,"AutomationId":"DocumentScroll","ControlType":"Pane","Heading":false}] |  / 还没有灯光图。关联已有参考，或从本地添加；原图保持不变。 / Text | 1 |
| capture-lighting | LIFECYCLE_OR_EVIDENCE | capture | N/A |  /  /  | N/A |
| module-styling | PLANNING_CONTENT_NAV | invoke | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false}] |  / 服化道 / Button | 1 |
| landmark-styling | PLANNING_CONTENT | assertPresent | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false},{"Name":null,"AutomationId":"DocumentScroll","ControlType":"Pane","Heading":false}] |  / 还没有服化道。关联已有参考，或从本地添加；原图保持不变。 / Text | 1 |
| capture-styling | LIFECYCLE_OR_EVIDENCE | capture | N/A |  /  /  | N/A |
| module-files | PLANNING_CONTENT_NAV | invoke | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false}] |  / 文件 / Button | 1 |
| landmark-files | PLANNING_CONTENT | assertPresent | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false},{"Name":null,"AutomationId":"DocumentScroll","ControlType":"Pane","Heading":false}] |  / 项目文件 / Text | 1 |
| capture-files | LIFECYCLE_OR_EVIDENCE | capture | N/A |  /  /  | N/A |
| preview-text | PLANNING_CONTENT_NAV | invoke | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false}] |  / 文字 / Button | 1 |
| preview-open | PLANNING_HEADER | invoke | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false}] |  / 预览策划案 / Button | 1 |
| preview-list-hidden | PLANNING_HEADER | assertAbsent | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false}] | DocumentList / 策划案列表 / List | 0 |
| preview-nav-hidden | PLANNING_CONTENT_NAV | assertAbsent | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false}] |  / 文字 / Button | 0 |
| preview-edit-hidden | PLANNING_HEADER | assertAbsent | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false},{"Name":null,"AutomationId":"DocumentScroll","ControlType":"Pane","Heading":false}] |  / 编辑 / Button | 0 |
| preview-body | PLANNING_HEADER | assertPresent | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false},{"Name":null,"AutomationId":"DocumentScroll","ControlType":"Pane","Heading":false}] |  / Installed Acceptance Persistence {shortsha} / Text | 1 |
| preview-shot | PLANNING_HEADER | capture | N/A |  /  /  | N/A |
| preview-escape | PLANNING_HEADER | focusKey | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false}] |  / 摄影策划案工作区 / Custom | 1 |
| preview-restored | PLANNING_HEADER | assertPresent | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false}] | DocumentList / 策划案列表 / List | 1 |
| fixture | LIFECYCLE_OR_EVIDENCE | fixture | N/A |  /  /  | N/A |
| import-page | PLANNING_CONTENT_NAV | invoke | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false}] |  / 参考图 / Button | 1 |
| import-open | REFERENCE_GRID | invoke | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false}] |  / 关联本地图片 / Button | 1 |
| import-dialog | FILE_DIALOG | waitPresent | PID window |  / 关联参考图（不复制原图） / Window | 1 |
| import-file | FILE_DIALOG | setValue | 关联参考图（不复制原图） | 1148 /  / Edit | 1 |
| import-submit | FILE_DIALOG | invoke | 关联参考图（不复制原图） | 1 /  / Button | 1 |
| reference-loaded | REFERENCE_GRID | waitPresent | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false},{"Name":null,"AutomationId":"DocumentScroll","ControlType":"Pane","Heading":false}] |  / 参考图：验收参考 / Custom | 1 |
| quick-open | REFERENCE_GRID | focusKey | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false},{"Name":null,"AutomationId":"DocumentScroll","ControlType":"Pane","Heading":false}] |  / 参考图：验收参考 / Custom | 1 |
| quick-landmark | REFERENCE_GRID | assertPresent | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false}] | CloseImageButton / 关闭参考大图 / Button | 1 |
| quick-shot | REFERENCE_GRID | capture | N/A |  /  /  | N/A |
| quick-close | REFERENCE_GRID | invoke | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false}] | CloseImageButton / 关闭参考大图 / Button | 1 |
| menu-open | CONTEXT_MENU | focusKey | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false},{"Name":null,"AutomationId":"DocumentScroll","ControlType":"Pane","Heading":false}] |  / 参考图：验收参考 / Custom | 1 |
| menu-item-0 | CONTEXT_MENU | assertPresent | Menu |  / 打开来源 / MenuItem | 1 |
| menu-item-1 | CONTEXT_MENU | assertPresent | Menu |  / 加入情绪板 / MenuItem | 1 |
| menu-item-2 | CONTEXT_MENU | assertPresent | Menu |  / 用于当前镜头 / MenuItem | 1 |
| menu-item-3 | CONTEXT_MENU | assertPresent | Menu |  / 用于灯光 / MenuItem | 1 |
| menu-item-4 | CONTEXT_MENU | assertPresent | Menu |  / 用于造型 / MenuItem | 1 |
| menu-item-5 | CONTEXT_MENU | assertPresent | Menu |  / 用于参考仿色 / MenuItem | 1 |
| menu-item-6 | CONTEXT_MENU | assertPresent | Menu |  / 从策划中移除 / MenuItem | 1 |
| menu-shot | CONTEXT_MENU | capture | N/A |  /  /  | N/A |
| menu-escape | CONTEXT_MENU | focusKey | Menu |  / 打开来源 / MenuItem | 1 |
| booking-open | BOOKING_PICKER | invoke | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false}] |  / 关联档期 / Button | 1 |
| booking-landmark | BOOKING_PICKER | assertPresent | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false}] |  / 可关联档期 / List | 1 |
| booking-shot | BOOKING_PICKER | capture | N/A |  /  /  | N/A |
| booking-close | BOOKING_PICKER | invoke | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false},{"Name":"关联工作日历档期","AutomationId":null,"ControlType":"Text","Heading":true}] |  / 取消 / Button | 1 |
| export-open | PLANNING_MODAL | invoke | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false}] |  / 导出策划案 PDF / Button | 1 |
| quality-expand | PLANNING_MODAL | expand | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false},{"Name":"导出策划案 PDF","AutomationId":null,"ControlType":"Text","Heading":true}] |  / PDF 导出质量 / ComboBox | 1 |
| quality-select | PLANNING_MODAL | select | PDF 导出质量 |  / 高质量 · 300 DPI / ListItem | 1 |
| export-confirm | PLANNING_MODAL | invoke | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false},{"Name":"导出策划案 PDF","AutomationId":null,"ControlType":"Text","Heading":true}] |  / 导出 PDF / Button | 1 |
| save-dialog | FILE_DIALOG | waitPresent | PID window |  / 导出策划案 PDF / Window | 1 |
| save-path | FILE_DIALOG | setValue | 导出策划案 PDF | 1148 /  / Edit | 1 |
| save-submit | FILE_DIALOG | invoke | 导出策划案 PDF | 1 /  / Button | 1 |
| export-message | PLANNING_MODAL | assertPresent | [{"Name":"像素蛋挞消息对话框","AutomationId":null,"ControlType":"Window","Heading":false}] | MessageTextBlock / 策划案 PDF 已导出（图片式，文字不可选择）。 / Text | 1 |
| export-shot | PLANNING_MODAL | capture | N/A |  /  /  | N/A |
| export-dismiss | PLANNING_MODAL | invoke | [{"Name":"像素蛋挞消息对话框","AutomationId":null,"ControlType":"Window","Heading":false}] | YesButton / 确定 / Button | 1 |
| pdf-exists | LIFECYCLE_OR_EVIDENCE | assertFileSize | N/A |  /  /  | N/A |
| pdf-pages | LIFECYCLE_OR_EVIDENCE | assertPdfPages | N/A |  /  /  | N/A |
| shot-page | PLANNING_CONTENT_NAV | invoke | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false}] |  / 镜头清单 / Button | 1 |
| shot-new | PLANNING_CONTENT | invoke | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false}] |  / 新建镜头 / Button | 1 |
| more | PLANNING_HEADER | invoke | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false}] |  / 更多策划操作 / Button | 1 |
| shoot | CONTEXT_MENU | invoke | Menu |  / 开始拍摄 / MenuItem | 1 |
| tether-workspace | TETHER | assertPresent | [{"Name":"联机拍摄现场监看工作区","AutomationId":null,"ControlType":"Custom","Heading":false}] |  / 联机拍摄现场监看工作区 / Custom | 1 |
| tether-context | TETHER | assertEnabled | 联机拍摄现场监看工作区 |  / 返回当前项目策划 / Button | 1 |
| tether-shot-context | TETHER | assertPresent | 联机拍摄现场监看工作区 |  / Shot 01 / 01 · 未命名拍摄 / Text | 1 |
| tether-shot | TETHER | capture | N/A |  /  /  | N/A |
| back-planning | GLOBAL_NAV | invoke | PrimaryNavigation | PrimaryNavigationPlanning /  / Button | 1 |
| back-text | PLANNING_CONTENT_NAV | invoke | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false}] |  / 文字 / Button | 1 |
| back-body | PLANNING_CONTENT | assertPresent | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false},{"Name":null,"AutomationId":"DocumentScroll","ControlType":"Pane","Heading":false}] |  / Installed Acceptance Persistence {shortsha} / Text | 1 |
| online | GLOBAL_NAV | invoke | PrimaryNavigation | PrimaryNavigationOnlineSelection /  / Button | 1 |
| online-landmark | ONLINE_SELECTION | assertPresent | 在线选片 |  / 创建选片项目 / Button | 1 |
| online-not-toolbox | ONLINE_SELECTION | assertAbsent | [{"Name":"摄影策划案工作区","AutomationId":null,"ControlType":"Custom","Heading":false}] |  / 摄影策划案工作区 / Custom | 0 |
| online-shot | ONLINE_SELECTION | capture | N/A |  /  /  | N/A |
| final-planning | GLOBAL_NAV | invoke | PrimaryNavigation | PrimaryNavigationPlanning /  / Button | 1 |
| final-close-request | LIFECYCLE_OR_EVIDENCE | requestClose | N/A |  /  /  | N/A |
| final-close-confirm | PLANNING_CONTENT | invoke | 像素蛋挞消息对话框 |  / 保存 / Button | 0 or 1 |
| final-close | LIFECYCLE_OR_EVIDENCE | waitExit | N/A |  /  /  | N/A |

## Fresh warnings — individually retained and explained

Warnings count steps/checkpoint occurrences, not unique controls. Each row is accepted for static packaging only; actual provider uniqueness is not waived.

| Step/checkpoint selector | Code | Explanation / disposition |
|---|---|---|
| create-open | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| create-heading | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| create-name | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| create-location | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| create-confirm | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| create-closed | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| text-open | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| edit | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| edit | GENERIC_LABEL | Generic label must stay in its declared content/modal scope; live checkpoint verifies uniqueness. |
| edit-body | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| edit-verify | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| save-switch | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| save-return | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| save-content | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| restart-close-confirm | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| restart-close-confirm | GENERIC_LABEL | Generic label must stay in its declared content/modal scope; live checkpoint verifies uniqueness. |
| restart-text | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| persisted-body | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| module-text | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| landmark-text | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| module-references | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| landmark-references | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| module-moodboard | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| landmark-moodboard | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| module-shots | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| landmark-shots | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| module-lighting | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| landmark-lighting | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| module-styling | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| landmark-styling | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| module-files | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| landmark-files | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| preview-text | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| preview-open | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| preview-nav-hidden | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| preview-edit-hidden | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| preview-edit-hidden | GENERIC_LABEL | Generic label must stay in its declared content/modal scope; live checkpoint verifies uniqueness. |
| preview-body | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| preview-escape | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| import-page | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| import-open | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| import-dialog | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| reference-loaded | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| quick-open | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| menu-open | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| menu-item-0 | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| menu-item-1 | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| menu-item-2 | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| menu-item-3 | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| menu-item-4 | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| menu-item-5 | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| menu-item-6 | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| menu-escape | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| booking-open | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| booking-landmark | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| booking-close | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| export-open | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| quality-expand | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| quality-select | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| export-confirm | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| save-dialog | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| export-dismiss | GENERIC_LABEL | Generic label must stay in its declared content/modal scope; live checkpoint verifies uniqueness. |
| shot-page | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| shot-new | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| more | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| shoot | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| tether-workspace | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| tether-context | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| tether-shot-context | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| back-text | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| back-body | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| online-landmark | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| online-not-toolbox | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| final-close-confirm | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| final-close-confirm | GENERIC_LABEL | Generic label must stay in its declared content/modal scope; live checkpoint verifies uniqueness. |
| check-create-name | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| check-create-location | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| check-create-confirm | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| check-module-text | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| check-module-references | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| check-module-moodboard | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| check-module-shots | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| check-module-lighting | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| check-module-styling | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| check-module-files | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| check-edit | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| check-edit | GENERIC_LABEL | Generic label must stay in its declared content/modal scope; live checkpoint verifies uniqueness. |
| check-preview-open | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| check-booking-open | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| check-export-open | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| check-more | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| check-edit-body | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| check-finish-edit | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| check-preview-body | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| check-quick-open | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| check-menu-open | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| check-menu-item-0 | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| check-menu-item-1 | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| check-menu-item-2 | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| check-menu-item-3 | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| check-menu-item-4 | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| check-menu-item-5 | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| check-menu-item-6 | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| check-booking-landmark | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| check-booking-close | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| check-quality-expand | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| check-export-confirm | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| check-quality-select | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| check-shoot | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| check-online-landmark | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| nav-system-0 | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| nav-system-1 | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |

## Upgrade warnings — individually retained and explained

Warnings count steps/checkpoint occurrences, not unique controls. Each row is accepted for static packaging only; actual provider uniqueness is not waived.

| Step/checkpoint selector | Code | Explanation / disposition |
|---|---|---|
| create-open | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| create-heading | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| create-name | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| create-location | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| create-confirm | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| create-closed | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| text-open | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| edit | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| edit | GENERIC_LABEL | Generic label must stay in its declared content/modal scope; live checkpoint verifies uniqueness. |
| edit-body | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| edit-verify | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| save-switch | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| save-return | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| save-content | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| restart-close-confirm | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| restart-close-confirm | GENERIC_LABEL | Generic label must stay in its declared content/modal scope; live checkpoint verifies uniqueness. |
| restart-text | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| persisted-body | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| module-text | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| landmark-text | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| module-references | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| landmark-references | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| module-moodboard | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| landmark-moodboard | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| module-shots | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| landmark-shots | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| module-lighting | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| landmark-lighting | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| module-styling | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| landmark-styling | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| module-files | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| landmark-files | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| preview-text | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| preview-open | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| preview-nav-hidden | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| preview-edit-hidden | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| preview-edit-hidden | GENERIC_LABEL | Generic label must stay in its declared content/modal scope; live checkpoint verifies uniqueness. |
| preview-body | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| preview-escape | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| final-close-confirm | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| final-close-confirm | GENERIC_LABEL | Generic label must stay in its declared content/modal scope; live checkpoint verifies uniqueness. |
| check-create-name | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| check-create-location | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| check-create-confirm | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| check-module-text | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| check-module-references | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| check-module-moodboard | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| check-module-shots | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| check-module-lighting | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| check-module-styling | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| check-module-files | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| check-edit | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| check-edit | GENERIC_LABEL | Generic label must stay in its declared content/modal scope; live checkpoint verifies uniqueness. |
| check-preview-open | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| check-edit-body | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| check-finish-edit | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| check-preview-body | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| nav-system-0 | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |
| nav-system-1 | SCOPED_NAME | No stable ID supplied; typed and scoped semantic name used. Framework exposure/locale still requires live exactly-one verification; no first-match fallback. |

## Historical regressions and acceptance boundary

v1 date-value matched two writable peers; v2 planning-enabled looked under a sibling landmark. Both have named offline regression tests. Source/installer remain unchanged; no actual UI automation is executed in this agent pass. New v3 evidence must start at Fresh Install, pass all 124 steps plus upgrade, and cannot be stitched from history. Live hierarchy failures now carry scope and candidate details. Machine-readable reports with full selectors are shipped at ZIP root as planning-full.selector-lint.json and upgrade-full.selector-lint.json.

