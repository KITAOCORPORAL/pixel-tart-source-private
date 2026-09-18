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

没有宣称多台普通用户电脑、多 DPI、摄影师体验或在线服务上传已完成。

## 最终结果与安装包

- 源码提交：d6cc4f1c07364203471d064428465b2373a49906。
- Release + WPF tests 构建：0 warning / 0 error。
- 最终 WPF：1262 passed / 0 failed / 1 skipped；跳过项为 ThreeSamplesOfPublicBatchCommandsAgainstFresh10128Fixture（大批量夹具验收），没有计入通过。
- Core：1373 passed；真实 App 集成：1 passed。
- 安装包：PixelTart-DeveloperPreview-2.3.0-dev.d6cc4f1-x64-Setup.exe。
- 大小：51,390,616 bytes；SHA256：7FBE6FE587F5B16202014E02A7ECA7592579A9B4B76E47530617E0BD2A41B49C。
- 安装、卸载、重装：返回码均 0。卸载后仅测试安装程序文件被移除，外置测试数据保留；现已重装，测试安装保留。
- 运行文件比对：285 MATCH；259 dependency probes 通过。
- 安装路径：C:/Users/Administrator/AppData/Local/PixelTart-TestAcceptance/NavigationIA/installed。
- 测试数据：同级 installed-data；未使用用户照片或既有项目。
- 安装版策划总览：STARTUP_OK，实际可访问性树包含搜索、筛选、新建和演示项目卡片。
- 安装版在线选片：STARTUP_OK，实际可访问性树包含项目列表、搜索、状态筛选及原有选片工作区。通过修改隔离测试设置 LastPrimaryPage 启动该页，不冒充鼠标点击。
- 重装后启动：STARTUP_OK，恢复 OnlineSelection，无 ERROR 日志。

## 未通过的完整人工门槛

**Installed App Smoke 的完整点击流程仍为 BLOCKED，而非 PASS。**

computer-use 读取到了真实安装窗口的可访问性树，但截图返回 SetIsBorderRequired / E_NOINTERFACE，点击返回 coordinate input geometry is unavailable。未改用旁路 UI 自动化。已经验证安装版两个页面独立启动；项目/拍摄项及日历/联机跳转由真实 App 集成测试验证。

因此此包仅作为开发预览安装包交付，不宣布正式 RC 或全部 STOP CONDITION 已满足。用户需在安装版补点：策划中心 → 演示项目 → 返回 → 在线选片；真实截图与多 DPI 仍待人工验收。

证据目录：artifacts/navigation-ia（TRX、窗口渲染图、安装/卸载/重装日志、安装版启动日志）；完整构建及三方运行文件比对在 artifacts/stage-v2-installed-startup-failure/builds/2.3.0-dev.d6cc4f1。
