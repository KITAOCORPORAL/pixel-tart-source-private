# 公司 Windows 输入环境（只读）

2026-11-27，Windows PowerShell 的 `Get-WinUserLanguageList` 返回 `zh-Hans-CN` 和 `en-US`，含 3 个 `0804` 中文输入法配置和一个美式键盘配置；用户键盘预加载含 `00000804`、`00000409`。交互会话 1 中 `ctfmon` 与 `TextInputHost` 均在运行。没有切换输入法、操作当前活动窗口、读取剪贴板、改动系统设置或采集输入历史。

此检查证明工作站配置了中文输入环境；它不能证明哪一项当时正在使用，更不能代替人用实体键盘唤起候选窗、选字、退回重组并提交的验收。本轮工作台[主链路](BROWSER-REPORT.md)中的浏览器 CDP composition 单独计数，补验的 renderer paste 也不计作系统中文输入法实测。
