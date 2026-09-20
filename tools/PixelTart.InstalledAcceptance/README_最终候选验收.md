# Pixel Tart 最终候选验收

该验收包只能在内部 selector/accessibility readiness 全部通过后生成。用户运行用于最终候选验收，不用于调试验收工具本身。

1. 正常关闭 Pixel Tart。
2. 完整解压 Candidate ZIP，勿单独复制 BAT。
3. 双击「运行最终候选验收.bat」。
4. 等待验收结束；如有 Windows 安装确认，请核对 Pixel Tart 安装器。
5. 回传包内的 `acceptance-result.zip`。程序不会自动上传。

数据保存在独立验收目录，不使用真实素材库。遇到现有非测试安装时会停止，避免改动你的正式安装。任何失败都会保留诊断和已生成的证据，不要求你调试 selector。

技术流程通过不等于视觉体验已获认可。最终人工视觉验收仍由你确认。
