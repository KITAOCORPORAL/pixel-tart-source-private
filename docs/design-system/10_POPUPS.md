# Pixel Tart Studio UI — 弹层

沿用暗色 ContextMenu/Submenu/Combo/Calendar/Filter/Tooltip。RadiusPopup 10、行高 32、图标 16–18。不能复制系统浅色面；白文字、源图、打印页面不算白色泄漏。弹层键盘与 Esc 不能退化。

实施资源：`src/RAWSelectionAssistant/Resources/DesignSystem/Studio.Controls.xaml`、`Studio.Symbols.xaml` 及现有兼容字典。
状态：v1 实施中，用户确认待完成。全应用推广为 PARTIAL；本文件不是验收 PASS。

## Popup v2 prototype

Elevated popups use a 10–12 DIP radius, one soft edge, restrained shadow, and 8–10 DIP vertical item padding. Selection is an accent wash rather than a saturated row. Combo boxes share the 30–32 DIP compact surface and preserve keyboard/Esc behavior.
