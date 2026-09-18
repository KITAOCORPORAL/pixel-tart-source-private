# Stage V.2 Installable Acceptance Report

## 结论

`READY FOR USER INSTALL SMOKE / REAL_USER_VISUAL_ACCEPTANCE = PENDING USER`

## 构建

- ProductSourceSha：`ad6706b32c787a76f0162932765c27b8c8a8d8bf`
- Branch：`integration/pixel-tart-developer-preview`
- Final package metadata：见 `artifacts/stage-v2-installable-acceptance/installer/INSTALLER_PROVENANCE.json`
- Installer：`PixelTart-DeveloperPreview-2.3.0-dev-x64-Setup.exe`
- Publish：Release、self-contained、win-x64、clean staging
- Release build：PASS，0 warnings / 0 errors

## 自动验证

- Startup style compatibility：PASS，3/3
- Planning Core：PASS，8/8
- Color core / Reference Look / LUT：PASS，19/19
- Tether source safety and cancellation：代码契约 PASS
- Forbidden runtime files：PASS，Acceptance/TestHost/Tests/PDB/TRX/source/XAML absent
- Source safety：PASS（核心测试覆盖源文件哈希不变）

## 安装验收状态

| 项目 | 状态 |
|---|---|
| Fresh Install | PENDING USER |
| Installed Launch | PENDING USER |
| Planning Launch | PENDING USER |
| Toggle Filter | PENDING USER |
| Tether Launch | PENDING USER |
| Reference Mode | PENDING USER |
| Upgrade Install | PENDING USER |
| Orphan runtime cleanup | PENDING USER |
| Uninstall | PENDING USER |
| Reinstall | PENDING USER |
| User Data Preservation | PENDING USER |
| Real user visual acceptance | PENDING USER |

## 参考仿色架构

- 独立完整编辑器：PASS（现有工具箱/核心服务）
- 联机折叠编辑器：PASS
- 联机高级调整：PASS
- 共享仿色引擎：PASS
- 共享参数模型：PASS
- 共享左右对比：PASS（现有 TetherReferenceSplitView）
- 结果一致性：PASS（核心测试）
- Session 临时调整：PARTIAL，完整人工 round-trip 待验收
- 恢复色彩方案：PENDING USER
- 显式保存：PENDING USER
- 完整编辑器跳转：PENDING USER
- Round Trip：PENDING USER

## 阻塞修复

`ToggleButton`/`Button` TargetType 不兼容导致的 `XamlParseException` 已修复；启动失败后的清理空引用已保护。旧 `KitaoPhotoSelector.Acceptance.dll` 不进入新安装包。

## 证据位置

- 安装器：`artifacts/stage-v2-installable-acceptance/installer/`
- 安装文件清单：`INSTALL_FILE_MANIFEST.json`
- 安装器 provenance：`INSTALLER_PROVENANCE.json`
- SHA256：`SHA256SUMS.txt`
- 安装说明：`artifacts/stage-v2-installable-acceptance/README_安装测试.md`
- 反馈模板：`artifacts/stage-v2-installable-acceptance/USER_FEEDBACK_TEMPLATE.md`

不要自动运行安装器；完成后停止，等待用户双击安装测试。
