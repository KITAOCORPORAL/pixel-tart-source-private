# Color Studio Computer Use 实机巡检

## 本轮结论

- 状态：**NOT_READY_FOR_USER_RETEST**
- `USER_VISUAL_REVIEW`：`NOT_APPROVED`
- `VisualApproved`：`false`
- `UserVerified`：`false`
- 生产代码/测试：本轮未修改。
- 本轮只归档同一 Release 的实机巡检证据；不把人工验收项改成 PASS。

## 构建与仓库身份

- 仓库：`KITAOCORPORAL/pixel-tart-source-private`
- 分支：`integration/pixel-tart-developer-preview`
- 本轮归档前仓库 HEAD：`e16c084184e5453718c84306be44740eb0b0a96e`
- 该 HEAD 与 `origin/integration/pixel-tart-developer-preview` 一致；归档目录在提交前为未跟踪内容。
- Release SourceHead：`31d3ac3ae56d6ccd1d9b4c608bf522dbb735d591`
- Release：`color-studio-rebuild-2026-10-05-r1`，Release / x64 / win-x64 / self-contained
- EXE（仓库相对路径）：`artifacts/releases/color-studio-rebuild-2026-10-05-r1/publish/win-x64/KitaoPhotoSelector.exe`
- EXE SHA256：`C9841FB530ABF0E209B8E9BB9B6870C917D54DED985702B2EF41F36EE6DB28CA`
- Application DLL SHA256：`DD5BFB88219EC853DD328AA00743BE4519C0A259E19C2016467239948420BCD3`
- Application DLL ProductVersion：`2.3.0+31d3ac3ae56d6ccd1d9b4c608bf522dbb735d591`
- 发布 manifest：287/287 文件匹配；manifest SHA256：`DDFE6381BD897F2BF7A997144E71E143A10CD26ED2F3CA38A10D4BAA7A9BAE21`

## 执行范围

- 7 个 r1 进程会话；343 张原始窗口截图；87 张公开脱敏完整窗口 PNG。
- RT01–RT14 均有实际动作记录；95 个子项中 15 PASS、7 FAIL、61 PARTIAL、12 NOT_RUN。
- 汇总：RT01 PARTIAL；RT02 FAIL；RT03 PARTIAL；RT04 FAIL；RT05 PARTIAL；RT06 PARTIAL；RT07 PARTIAL；RT08 FAIL；RT09 PARTIAL；RT10 PARTIAL；RT11 FAIL；RT12 PARTIAL；RT13 PARTIAL；RT14 FAIL。
- 公开 PNG 均来自同一 r1 Release；原始截图、私有输入、UI 导出和工作文件留在本机私有证据根。

## 已登记问题

- **CU-01 / P1**：1180×720 DIP、125%/200% DPI 时右侧编辑栏关键数值、复位和 Creative 内容不可达。
- **CU-02 / P2**：展开 3D 浮窗覆盖中央照片和右编辑栏，mini/expanded 相机状态独立。
- **CU-03 / P2**：同步弹层显示 9 张，实际排除源图后执行 8 张，文案未说明源图/目标数。
- **CU-04 / P2**：切换 English/繁體后仍有大量简体中文硬编码。
- **CU-05 / P1**：非法 `abc` 输入被拒后，重新聚焦按 Escape 未清除草稿并随后回到 Home。
- **CU-06 / P3**：影调区间需要内滚才能发现；映射和 Escape 清除已观察，发现性仍差。

问题复现步骤、源码线索和证据链接见 [ISSUES.md](ISSUES.md)。旧 CUI-001 已撤回，不在本轮问题清单中。

## 文档索引

- [RUNTIME_CHECKLIST.md](RUNTIME_CHECKLIST.md)：RT01–RT14 逐项结果和缺口。
- [VISUAL_COMPARISON.md](VISUAL_COMPARISON.md)：按用户布局图八区域对照及窗口/DPI矩阵。
- [ISSUES.md](ISSUES.md)：六项实机问题。
- [SCREENSHOT_MANIFEST.json](SCREENSHOT_MANIFEST.json)：87 张公开图片的 Release、窗口、DPI、操作、SHA256 和脱敏审查标志。
- [BUILD_IDENTITY.json](BUILD_IDENTITY.json)：Release、哈希、manifest 和历史测试边界。
- [SESSION_IDENTITIES.json](SESSION_IDENTITIES.json)：匿名会话、实际 DPI 和窗口物理尺寸。
- [INPUT_IDENTITIES.json](INPUT_IDENTITIES.json)：公开合成输入哈希和私有输入代号。
- [EXPORT_COMPARISON.json](EXPORT_COMPARISON.json)：UI 导出像素/格式比较摘要。
- [PRIVATE_EVIDENCE_INDEX.md](PRIVATE_EVIDENCE_INDEX.md)：本机私有证据脱敏索引。
- [FILES_AND_TYPES.md](FILES_AND_TYPES.md)：本目录公开文件类型、字节数和 SHA256。

## 隐私边界

公开提交只包含脱敏 PNG、脱敏 JSON 和 Markdown。私人 RAW、完整原始截图、UI 导出、冻结工作文件、账户资料和原始动作日志不进入 Git。公开图片已逐张复核为完整窗口，导出目录区域已遮盖且未裁切或缩放。

本轮停止在问题归档和证据交付；不制作 Installer，不推进下一阶段，不声明用户批准。
