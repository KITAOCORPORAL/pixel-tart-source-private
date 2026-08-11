# ProductRedesign RC1 已知限制

- `UserVerified=false`；视觉 90/100 仅为 Codex 预检查。
- 隐藏桌面安装版自动验收无法可靠向 WPF 日期格发送右键输入，因此日历右键、关闭档期及重启保持尚未获得 `InstalledUiVerified=true`；对应代码和自动化专项均通过。
- 隐藏桌面的系统文件对话框在完成 synthetic JPG 路径输入后未可靠关闭。安装二进制实际写入了隔离 Workspace、Asset 和代理 JPG，但在线项目页打开、四页签和结果同步未取得完整 `InstalledUiVerified=true`。
- RAW 实际验证只覆盖本轮三份公开样本的 ARW、CR2、NEF，不代表所有相机型号或候选扩展。
- EXIF 为有限重建，不是完整标签透传。
- 在线选片生产云、小程序生产发布、服务器、域名、HTTPS、对象存储、数据库、微信 AppID 和生产凭证未配置。
- 候选安装包未代码签名。
