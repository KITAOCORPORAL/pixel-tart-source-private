# Pixel Tart 一级导航 IA 修正验收

日期：2026-09-18。范围：本次导航文档，不扩展下一阶段。

## 已实现

工作台 → 素材库 → 归片工作区 → 工作日历 → 策划中心 → 联机拍摄 → 在线选片 → 摄影收支 → 项目历史。
工具分组仅工具箱；系统仍为授权与版本、设置、帮助。

复用原策划工作区，增加总览卡片、搜索、筛选、新建和返回总览；新增项目历史策划入口、项目/拍摄项深链接、联机返回策划；持久化上次一级页面及策划上下文，损坏资料仅回退不覆盖。
在线选片保留原功能并补封面、数量、状态与搜索筛选。参考仿色仍在工具箱。批量压缩与发布导出不是同一功能，保留独立名称。

## 测试证据

- Release 初步构建：0 warning / 0 error。
- Core：1373 passed，0 failed（navigation-core-final.trx）。
- 首轮 WPF：1261 passed / 1 failed / 1 skipped；唯一失败是状态文案旧断言，已更新。最终回归与安装结果在打包后补录。
- 真实 App 集成：1 passed（navigation-integration-final.trx）。执行生产 App/MainWindow；验证策划总览、日历/历史/项目/拍摄项跳转、策划与联机往返、在线选片导航、新建和搜索、失效项目/拍摄项恢复、损坏 JSON 保留原文件。
- 1080p：1920×1040 窗口（为任务栏留空间）实测 SidebarNavigationScroll.ScrollableHeight < 1。
- artifacts/navigation-ia/screenshots/left-navigation-workflow.png 为真实 WPF 窗口渲染截图，不是桌面物理屏幕截图；黑色参考图是既有 1×1 合成演示夹具，不代表真实照片显示结果。

没有宣称多台普通用户电脑、多 DPI、摄影师体验或在线服务上传已完成。EXE 打包和安装冒烟将在下方追加证据。
