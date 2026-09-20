# Navigation Selector Audit — AcceptanceKit v3

2026-09-20. v2 failed at planning-enabled. Product did not crash. SidebarRoot was incorrectly treated as an ancestor; it is a sibling AutomationLandmark. Correct scope: SidebarNavigationScroll / Pane. No product code changed.

Evidence: MainWindow.xaml lines 158–235, existing v2 failure.tree.json in user run 20260920-141549-24c662. The dump records each target once but is flat: parent/ancestor information below is from actual XAML, not falsely claimed as captured live hierarchy. The navigation ScrollViewer and each target have observed UIA peers.

All rows use ScopePreset=PrimaryNavigation → unique SidebarNavigationScroll / Pane. ControlType=Button. Expected match count 1; observed flat-tree count 1 for every row. Live scoped traversal with v3 remains pending.

| UI | AutomationId (or explicit fallback) | Actual XAML parent | Actual containing ancestor | Final scope | Unique evidence |
|---|---|---|---|---|---|
| 工作台 | PrimaryNavigationWorkbench | PrimaryNavigationGroup StackPanel | SidebarNavigationScroll | PrimaryNavigation preset | flat tree 1 |
| 素材库 | AssetLibraryNavigationButton | PrimaryNavigationGroup StackPanel | SidebarNavigationScroll | same | flat tree 1 |
| 归片工作区 | PrimaryNavigationWorkflow | PrimaryNavigationGroup StackPanel | SidebarNavigationScroll | same | flat tree 1 |
| 工作日历 | PrimaryNavigationWorkCalendar | PrimaryNavigationGroup StackPanel | SidebarNavigationScroll | same | flat tree 1 |
| 策划中心 | PrimaryNavigationPlanning | PrimaryNavigationGroup StackPanel | SidebarNavigationScroll | same | flat tree 1 |
| 联机拍摄 | PrimaryNavigationTether | PrimaryNavigationGroup StackPanel | SidebarNavigationScroll | same | flat tree 1 |
| 在线选片 | PrimaryNavigationOnlineSelection | PrimaryNavigationGroup StackPanel | SidebarNavigationScroll | same | flat tree 1 |
| 摄影收支 | PrimaryNavigationFinance | PrimaryNavigationGroup StackPanel | SidebarNavigationScroll | same | flat tree 1 |
| 项目历史 | PrimaryNavigationHistory | PrimaryNavigationGroup StackPanel | SidebarNavigationScroll | same | flat tree 1 |
| 工具箱 | PrimaryNavigationToolbox | unnamed content StackPanel | SidebarNavigationScroll | same | flat tree 1 |
| 授权与版本 | no ID; Name=授权与版本 + Button | unnamed content StackPanel | SidebarNavigationScroll | same | flat tree 1 |
| 设置 | SidebarSettingsButton | unnamed content StackPanel | SidebarNavigationScroll | same | flat tree 1 |
| 帮助 | no ID; Name=帮助 + Button | unnamed content StackPanel | SidebarNavigationScroll | same | flat tree 1 |

No invented IDs for authorization/help. The separate bottom edition-card upgrade button is outside this scope and is NOT the sidebar authorization navigation target. Collapse/expand control is also outside; not part of the 13 requested routes. Navigation audit can include offscreen entries for existence without claiming they are clickable; actual workflow targets still require visible elements.

## Safeguards

- Both full plans use the preset, never handwritten SidebarRoot ancestors. Plan lint rejects SidebarRoot, mixed presets/manual ancestors, missing types, unscoped selectors, duplicate IDs and missing selectors.
- Scope count is checked before target resolution. Zero scope polls for readiness then reports Scope resolution failed; multiple scopes fail immediately. Diagnostic phase distinguishes scope vs target, with all candidates and ancestor chains. No First()/index fallback.
- MainWindow preset uses the PID-bound actual window for tutorial controls. It is not used as a substitute for navigation scope.
- All 13 entries are proactively checked before the main planning flow, in both fresh and upgrade. Global nav helper tests cover planning, online and tether using the same resolver.
- Full fresh step-by-step classification and all warnings: V3_FULL_SELECTOR_AUDIT.md. Machine-readable 124/70-step lint reports shipped inside ZIP root.

## Tests and delivery

Portable gate actually executed in `C:\Users\Administrator\Downloads\PixelTartAcceptanceV3PortableTest-20260920\解压 验收包`: BAT preflight 7/7 PASS, direct self-contained Runner exit 0, both installer hashes and output/diagnostic ZIP PASS. Seven normal/negative portable cases PASS (missing runner/dependency/installer/plan, tampered plan, restored success). No SDK/repository needed by the extracted launcher. Interactive conflict WAIT is implemented and source-reviewed; no live user process was launched or killed to test it. The actual portable test found no conflicting process.

40 offline checks PASS: 23 existing validation cases, 8 selector cases, 9 navigation/lint regressions. Build 0 warnings/0 errors. Fresh lint 0 ERROR / 112 WARNING; upgrade 0 ERROR / 59 WARNING. Warnings are individual scoped-name/generic-label disclosures, not silently waived live uniqueness checks.

v3 ZIP: `artifacts/acceptance-kit-v3/PixelTart-Installed-Acceptance-Kit-8cb95e6-v3.zip`.

Size 213,409,705 bytes. SHA256 `903876446FF28C58F75713EA0F599F221EDD33ED1904990F6F46B3588A2338D9`.

Runner preflight now has 7 checks; existing PixelTart processes cause WAIT and a close/retry message in normal mode, no Kill. Noninteractive preflight-only returns a clear conflict failure instead of blocking forever. Runner separately rechecks before Setup. owned-process.json records PID, InstalledExePath, AcceptanceRoot and start time; normal close verifies stored PID/path/window before touching only its own process.

Product source remains `8cb95e6d2c4814717dc31a8c6aa19f8b2c89c9cc`; Setup unchanged with SHA256 `C2E5F26A1D00BFE2A466450C0BB9D015CDD35D6733E909FE8ADCB5BAEFA6247D`. v1 date input fix retained; not yet verified by a user live run. No product build, no new installer, no live UIA invocation in this agent session (computer-use restriction respected). Preserve v1/v2 archives and evidence. Formal v3 acceptance starts fresh; no resumed/spliced PASS.
