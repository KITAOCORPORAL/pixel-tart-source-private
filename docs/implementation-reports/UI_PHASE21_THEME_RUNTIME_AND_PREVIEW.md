# Pixel Tart UI Phase 2.1：Theme Runtime Stabilization + Developer Preview

日期：2026-09-11

## Git

- Branch：`feature/online-selection-v1`
- Start HEAD：`0f81d9a`
- Final HEAD：待提交

## GhostButton 根因

`PixelTart.Components.xaml` 中 `Controls.Navigation.xaml` 使用 `BasedOn="{StaticResource GhostButton}"`，但导航字典在按钮字典之前解析，导致 MainWindow 初始化期 `XamlParseException`。

## 修复方式

组件入口调整为先 Buttons、再 Navigation；App 入口显式提供 Buttons/Cards 前置依赖。未将 StaticResource 改为 DynamicResource，未复制页面样式。Tether 检查器只读 `MediaKindText` 改为 OneWay。

## ResourceDictionary 最终依赖顺序

Tokens/Colors/Spacing/Radius/Elevation/Typography → Base Buttons → Base Cards → PixelTart Components → Page resources。Theme 与 Components 无循环引用。

## 自动测试

- ThemeStartupSmokeTests：PASS；真实构造 App、独立 WPF Window、MainWindow、TetherCaptureView。
- DesignSystemAv2LockTests：9/9 PASS。
- Release build：0 warning / 0 error。

## 截图验收

真实 WPF RenderTargetBitmap：`artifacts/ui-review/phase21-theme-runtime/Tether_1920x1080.png`，Layout BlockingIssueCount=0，Theme Passed=true。

## Asset Library视觉问题

PASS：图片优先缩略图、中央预览、克制选中描边。NEEDS TUNING：实体 125%/150% 尚待真实电脑复核。

## Tether Capture视觉问题

PASS：启动、页面打开与运行时渲染稳定。NEEDS TUNING：实体 DPI 与导入流程待真实电脑复核。

## Developer Preview

版本：Pixel Tart Developer Preview 0.1（复用现有 self-contained win-x64 / Inno Setup pipeline）
文件：`artifacts/releases/2.3.0/installer/像素蛋挞_Setup_2.3.0_RC5_x64.exe`
SHA-256：`BB1162B354D7BDBB609042F38CA787879DBF79586F0A15957E50436ADEFC7BC5`
安装测试：Windows 10/11 x64 限制已由现有安装脚本验证；发布内容未包含源码、测试数据库、RAW/JPG、cache、logs、secrets。

## 已知问题

- 当前分支尚未 push；Final HEAD 待提交后填写。
- 完整 2K/4K 截图矩阵与实体显示器验收待后续在真实电脑执行。

## 下一阶段建议

在真实电脑按 100%/125%/150% 复核导入、缩略图、Inspector、布局切换和联机拍摄；不要扩展到其他页面。
