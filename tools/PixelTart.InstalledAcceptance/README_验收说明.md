# Pixel Tart 安装版一键验收

状态：AWAITING LOCAL ACCEPTANCE RUN。此包已编译并离线校验，尚未实机运行；选择器仍待真实 UIA 树验证。

适用：Windows 10 2004 或以上的 x64 独立测试机器/测试虚拟机，交互桌面保持解锁，建议 1920×1080、100% 缩放。无需 Visual Studio、.NET 安装、命令输入或 JSON 编辑。

## 只需两步

1. 双击「运行安装版验收.bat」，确认 Windows 管理员权限提示。
2. 运行结束后，把本目录下 `acceptance-result` 文件夹提供给 Codex。不会自动上传。

运行期间不要操作鼠标键盘或切换窗口；不需手动点七模块、导出或截图。文件对话框无法安全定位时会停止并记录 EXTERNAL_DIALOG_BLOCKED，不会要求手工填数据冒充自动验收。

## 自动执行什么

先运行全新安装计划：安装、启动、教程、创建策划、日期、正文编辑、切换保存、正常关闭/重启持久化、七模块、预览、合成参考图导入与大图、右键菜单、关联档期、300 DPI 图片式 PDF 导出及页检查、联机拍摄镜头上下文、返回策划、在线选片、正常关闭。计划要求 16 张真实窗口截图。

然后运行旧版 `8729d17` → 当前 `8cb95e6` 的升级计划：旧版 UI 创建策划和正文、正常退出、Setup 升级、确认数据、七模块和预览、正常退出。

任何必需步骤失败就停止，保留进程以供诊断；不会使用强制结束进程伪造正常退出。只有两个计划都完成才输出 TECHNICAL_ACCEPTANCE_PASS；视觉仍为 NOT_REVIEWED，不等于产品发布通过。

## 安全边界

安装和数据只放在 `%LOCALAPPDATA%\PixelTart-TestAcceptance\InstalledAcceptance_8cb95e6_<模式>_<唯一编号>`。每次是新目录，不删除旧证据，不读写真实素材库、项目和设置；只用自动生成的测试图。旧、新安装包已随包提供并按 SHA256 验证。

安装器需要管理员权限并会写 Windows 安装注册项；原版安装器的 AppId 是共享的。因此检测到测试目录以外的已注册 Pixel Tart 开发预览版，或任何正在运行的 PixelTart 进程时会安全停止。不要在安装了正式使用版本的机器上强行运行或卸载正式版本来避开检查；请用独立测试 Windows/虚拟机。测试安装与文件会保留，不自动清理。

## 输出

`acceptance-result/<时间-编号>/acceptance.json` 为汇总，`fresh/` 和 `upgrade/` 分别包含步骤结果、`uia.jsonl`、安装日志、`environment.txt`、截图以及失败时的 UIA 树和缺口报告；fresh 成功导出后另有 PDF、pdfinfo 和全页渲染。失败前尚未执行的截图/PDF不会被伪造。

截图采用真实桌面像素；遮挡检查标为 MANUAL_REVIEW_REQUIRED。后续由 Codex 检查截图和 PDF 排版，用户视觉确认独立保留。

本包供本次本机验收使用，不是重新制作的产品发布包。`integrity.json` 记录所带文件哈希。Poppler 来自本机已配置依赖，详见 `THIRD_PARTY_NOTICES.md`。
