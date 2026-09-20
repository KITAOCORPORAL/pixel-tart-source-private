# Product accessibility map

Source: `f505a177106a38fc23550edd87949f235238fb79`. Re-run production App.xaml/MainWindow peer evidence: `artifacts/whole-app-product-polish/peers-final/peer-states.json`, 39 behavioral/peer checks in `checks.json`, passing `artifacts/whole-app-product-polish/tests-final-source/contract.trx`. This map does not claim installed native dialog coverage. Historical 4400993 evidence is superseded for this UI source, not relabelled.

```text
MainWindow (Window; owned process)
├ SidebarNavigationScroll (Pane; actual parent)
│ ├ PrimaryNavigationWorkbench (Button / Invoke)
│ ├ AssetLibraryNavigationButton (Button / Invoke)
│ ├ PrimaryNavigationPlanning (Button / Invoke)
│ ├ PrimaryNavigationTether (Button / Invoke)
│ └ PrimaryNavigationOnlineSelection (Button / Invoke)
├ PlanningWorkspace (Custom; real UserControlAutomationPeer)
│ ├ DocumentList (List; ListItems / SelectionItem)
│ ├ PlanningPageText / References / Moodboard / Shots / Lighting / Styling / Files
│ │   (Button / Invoke; fixed IDs, not data IDs)
│ ├ DocumentScroll (Pane)
│ │ └ PlanningTextSurface / ReferencesSurface / MoodboardSurface /
│ │     ShotListSurface / LightingSurface / StylingSurface / FilesSurface /
│ │     PreviewSurface (Pane; mutually exclusive state ID, actual content parent)
│ │     ├ 参考图：<标题> (PlanningReferenceTile, Button / Invoke, focusable)
│ │     └ 镜头 <序号>：<名称> (PlanningShotRow, Button / Invoke, focusable)
│ ├ PlanningCreateDialog (Pane)
│ │ └ 拍摄日期 (DatePicker, Custom / Value)
│ │   ├ PART_TextBox (Edit / Value)
│ │   └ PART_Button (Button / Invoke)
│ ├ PlanningBookingPicker (Pane → 可关联档期 List)
│ ├ PlanningExportDialog (Pane → PDF 导出质量 ComboBox / ExpandCollapse)
│ └ CloseImageButton (Button / Invoke; quick preview only)
├ TetherMonitorView (Custom; real UserControlAutomationPeer)
│ └ Pre-session project/shot context and 返回当前项目策划 (Button / Invoke)
└ 在线选片 (Custom; real UserControlAutomationPeer)
  └ 创建选片项目 (Button / Invoke)
```

SidebarRoot is a sibling marker, never a navigation scope. ContentNavigation is a UniformGrid without its own peer; use typed child button IDs under PlanningWorkspace, not an invented Custom navigation node.

Popup roots are process-owned: DatePicker Calendar, ComboBox ListItems, ContextMenu/MenuItems, ThemedMessageDialog/Window. Reference menus expose seven required Chinese actions, plus existing categorization actions. Enter uses the original-resolution image path; Esc and menu dismissal return focus to the invoking tile. Reference/moodboard/lighting/styling share one real implementation. Noninteractive proposal preview does not expose fake interactive image peers.

Windows Open/Save dialogs are native, not WPF peers. The six related fresh-plan steps remain unverified internally; `1148`/`1` must not be accepted merely by convention. Old-version upgrade selectors retain compatible semantic ancestry rather than requiring IDs absent in old builds.

Other collection review: files have real action Buttons, no fake row selector; Tether references are ListBox items with action Buttons; online selection uses existing buttons/list content, not an invented Custom tile. New scope presets are implementation candidates, not automatically contract-verified merely because listed in code.
