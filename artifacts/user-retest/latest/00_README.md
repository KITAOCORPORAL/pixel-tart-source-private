# Pixel Tart · 晚间12图修复候选

以 `00_START_HERE.md` 为当前 Release 入口。
本轮只处理 R01–R12；图8/9/10为参考界面。
详细记录：`docs/evidence/human-review/2026-10-04_EVENING_FEEDBACK_REPAIR.md`。
当前未完成同一候选的完整实机验证，NOT_READY_FOR_USER_RETEST；用户未批准。

---

## 以下整体为历史记录，不作为本轮结论
# Pixel Tart · 四组人工验收返工候选

当前 Release：`N:/pixart/pixel-tart-source-private/artifacts/releases/runtime-correction-2026-10-04-r9/publish/win-x64/KitaoPhotoSelector.exe`。
Production SourceHead：`dc2ba3d47e9937e6ab41395a589ae1c44a2b1ed7`。
先读 `00_START_HERE.md` 和 `docs/evidence/human-review/RUNTIME_CORRECTION_2026-10-04.md`。
本轮已授权并执行受限桌面操作；After按实际EXE分别存放，四组仍未全部实机通过。VisualApproved=false；UserVerified=false；NOT_READY_FOR_USER_RETEST。

## 以下为上一轮历史记录

Release SourceHead: `8457e9022ac8278df5401c6c1842f70a39c98129`

EXE：`N:/pixart/pixel-tart-source-private/artifacts/releases/manual-acceptance-2026-10-03/publish/win-x64/KitaoPhotoSelector.exe`

Manifest：`artifacts/releases/manual-acceptance-2026-10-03/release-manifest.json`。
已记录EXE、应用DLL及全部发布文件SHA256。后续文档提交不改变此SourceHead。

本轮依据`G:/UI问题/1/`截图1–14、24、25及用户A–I直接指令。
优先阅读`07_MANUAL_ACCEPTANCE_16_SCREENSHOTS.md`，其余清单保留作回归参考。
按已确认安排，Build/Test后由用户统一实机操作；不会启动Observer、Recorder或桌面自动操作。

## 15–20分钟顺序

1. 素材库：滑杆、Inspector、文件夹、智能文件夹、回收站（7分钟）。
2. 参考仿色：导航、胶片条、双导出路径（4分钟）。
3. 3D：取色、点选、清除、展开（2分钟）。
4. 自由画布与日历（4分钟）。

优先保持现有150%环境。问题反馈只需截图＋一句话。

## 真实边界

- 16项Runtime=NOT_RUN、After缺失、USER_ACCEPTANCE=NOT_APPROVED。
- 照片手动排序模型不存在；恢复覆盖现有字段排序，不能声称恢复手动照片位置。
- 旧DPI artifact来自de4c91a，不能证明本轮；本轮不采集桌面After截图、不改缩放。
- 旧TIFF16/高精度完整parity结论仍撤回；本轮未扩展ICC/RAW完整parity结论。
- V4实验性；GPU/Variant/Film等路线不启动；Physical Tether=WAITING_FOR_HARDWARE。

VisualApproved=false · UserVerified=false

最终测试摘要见`artifacts/manual-acceptance/latest/TEST_SUMMARY.md`。
最终状态：NOT_READY_FOR_USER_RETEST。

本轮自动回归：Core1533/0/4；WPF1429/0/11；DPI91/0。Release已生成，尚未启动或实机验收。
