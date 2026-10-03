# Pixel Tart 截图问题修正版

Release SourceHead: `8f9a28f3c21bcf41e24e700024d14a8648fde5b5`

EXE：`N:\pixart\pixel-tart-source-private\artifacts\releases\runtime-user-findings-final\publish\win-x64\KitaoPhotoSelector.exe`

Manifest：`artifacts/releases/runtime-user-findings-final/release-manifest.json`。记录 EXE、实际应用 DLL 和全部 287 个发布文件 SHA256。后续仅文档/证据提交不改变此 SourceHead。

本次根据 G:/UI问题/ 的25张截图及完整RUX-001～048指令修正。按你的确认，各批Build/Test完成后由你统一实机验收。所有RUX Runtime仍为NOT_RUN，VisualApproved=false，UserVerified=false。

## 15～20分钟顺序

1. 素材库、智能文件夹和右键菜单（6分钟）。
2. 参考仿色、Filmstrip、发布导出（5分钟）。
3. 参考仿色预览/导出（2分钟）。
4. 3D双向反馈（2分钟）。
5. 自由画布与工作台/日历（3分钟）。

建议优先使用当前150%环境。不需要技术日志，反馈“截图 + 一句话”即可。Observer、Recorder、自动操作关闭。

## 证据边界

- 修复账本：`docs/evidence/runtime-user-findings/RUNTIME_USER_FINDINGS_ROUND_01.md`。25张原始截图的hash见user-before/manifest.json；原截图版本和DPI未知。
- 没有本轮自动After截图；历史DPI截图不能代表新Release。DPI测试中的artifact校验仍读取de4c91a旧证据。
- 原始DOCX和录屏未找到，不声称看过；这次使用你提供的25张截图和完整RUX文字。
- 旧TIFF16/高精度完整parity结论已撤回；本轮发布像素覆盖测试、调整隔离测试不构成完整ICC/RAW语料parity。
- V4仍实验性；GPU/Variant/Film路线不扩展。Physical Tether=WAITING_FOR_HARDWARE。

请从00_START_HERE.md开始，清单不预勾PASS。

自动门：Core1530/0/4，WPF1421/0/11，DPI91/0。Release已启动，主窗口可见，无立即崩溃；实机体验仍由你验收。
