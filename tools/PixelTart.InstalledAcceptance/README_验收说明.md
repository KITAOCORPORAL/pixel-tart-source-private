# Pixel Tart 安装版一键验收

AcceptanceKit v3：修复全局导航作用域，保留日期输入修复，加入计划 lint、作用域失败诊断和第七项进程冲突检查。产品源码及安装器未修改。请先正常关闭所有 Pixel Tart 窗口，并解压到新的文件夹；旧失败证据不要删除或覆盖。每次正式验收均创建时间戳和随机编号的新结果目录，从 Fresh Install 重跑，不拼接旧结果。

完整交付文件：`PixelTart-Installed-Acceptance-Kit-8cb95e6-v3.zip`。不要只下载 BAT，也不要在压缩包预览窗口内运行。

适用：Windows 10 2004 或以上的 x64 独立测试机器/测试虚拟机，交互桌面保持解锁，建议 1920×1080、100% 缩放。无需 Visual Studio、.NET 安装、命令输入或 JSON 编辑。

## 使用方法

1. 下载完整 ZIP。
2. 将 ZIP 全部解压到可写目录。
3. 双击「运行安装版验收.bat」；检查通过后按提示开始。
4. 如果 Windows 询问是否允许运行，确认。
5. 测试完成后把同目录的 `acceptance-result.zip` 发给 Codex。不会自动上传。

运行期间不要操作鼠标键盘或切换窗口；不需手动点七模块、导出或截图。文件对话框无法安全定位时会停止并记录 EXTERNAL_DIALOG_BLOCKED，不会要求手工填数据冒充自动验收。

自动完成全新安装和旧版升级两条验收链；失败会停止并保留日志，不伪造截图或 PDF。启动时显示七项检查及具体错误；第七项发现已有实例会等待你正常关闭后重试，不强制关闭。结束前保留窗口。启动日志位于 `acceptance-result/acceptance-launch.log`；不可写时会提示临时目录备用日志。仅预检通过不代表产品实机通过。

## 安全边界

安装和数据只放在 `%LOCALAPPDATA%\PixelTart-TestAcceptance\InstalledAcceptance_8cb95e6_<模式>_<唯一编号>`。每次是新目录，不删除旧证据，不读写真实素材库、项目和设置；只用自动生成的测试图。旧、新安装包已随包提供并按 SHA256 验证。

请用没有正式使用版本的独立测试 Windows/虚拟机。原安装器需要管理员权限并共用安装注册项；检测到正式安装或正在运行的 PixelTart 会停止保护数据。预检不请求提升，开始安装时才请求；Runner 与它启动的 Pixel Tart 继承相同权限。测试安装和证据保留，不自动删除。

`KIT_MANIFEST.json` 记录相对路径、大小和 SHA256；启动前会验证包内文件。旧、新安装器均已放在 `installer/`，无需找文件、SDK 或编辑 JSON。本包用于本次本机验收，不是产品 Release；视觉验收仍待真实截图审查。依赖说明见 `THIRD_PARTY_NOTICES.md`。
