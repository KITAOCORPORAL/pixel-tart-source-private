# 在线选片首版续接交付

2026-11-09，Asia/Shanghai。本轮产品源码提交：`feef4bc25a13d26a297bad39af99f3332bf9ce25`。分支 `feature/online-selection-http-20261109` 已推送，远端 ls-remote 回读相同 SHA。后续文档提交不改变产品源码。

**本机闭环：PASS，包含完整 App 原生操作。iPhone 13 Safari：等待用户操作与反馈。**

先 fetch 后发现本机未推送实现 6f28d5ba 与交接 8652a02c，故接续现有独立工作树，未新建重复实现。基线仍是用户指定的 37ea6c9a。主树原未提交路径和其他旧工作树均保留，见 WORKTREE_PRESERVATION.json。

## 三个操作入口

| 入口 | 实际位置与操作 |
|---|---|
| PC 发布 | `tools/OnlineSelectionHttpAcceptance/01-PC-Publish.cmd` 打开完整诊断 App 的在线选片页。创建选片项目，填写名称、客户和目标数量，点击创建并导入照片，在原生文件框选择 JPEG，再点击发布项目和复制选片链接。 |
| 手机网页 | 已有受保护 HTTPS 相册。完整链接仅在本地忽略目录 `artifacts/online-selection-http/iphone13-safari/PHONE_URL.txt`。Safari 打开、勾选、点击确认并接受提示。成功显示“服务器已确认”及版本、修订和服务器确认时间。 |
| PC 收回结果 | `tools/OnlineSelectionHttpAcceptance/03-PC-Collect.cmd` 定位同一手机测试项目。点击刷新客户结果，查看客户只读勾选标记和服务器回执。项目历史打开对应归片项目；关闭后重开仍恢复。 |

两个 PC 入口目前均默认定位已准备的手机测试相册；发布新项目使用“创建选片项目”。诊断数据根为 `artifacts/online-selection-http/iphone13-safari/IsolatedApp`，使用同一数据根和服务 origin 才能恢复凭据及结果。完整 App 可执行文件在 `artifacts/online-selection-http/resume-app/KitaoPhotoSelector.exe`，必须通过带隔离配置的入口启动。

## 本轮修正

网页确认不再只看 HTTP 成功状态。它验证服务器回执的项目 Id、版本、修订、锁定、素材归属和时间，再显示成功。确认响应丢失时从服务器重读持久化回执。错误项目的 HTTP 200 回执会被拒绝；完整回执到达后成功显示不再依赖额外刷新请求。

客户相册接口可用客户权限读取当前确认回执，PC 持久化服务器修订号并显示。PC 客户勾选改为只读，避免本地临时状态被当成客户结果。启动参数 `--online-selection --selection-project=<Guid>` 可直接打开准确的项目；凭据仍按当前 Windows 用户及服务 origin 加密。

## 本轮真实操作收据

- `REAL_HTTP.json`：47 项真实 HTTP 和桌面命令检查，包括稳定 Id 创建/上传重试、选择确认幂等、原片不变、错项目拒绝、撤销链接及旧媒体会话失效、服务器修订回写。
- `NEW_PROCESS_REOPEN.json`：8 项独立进程读盘恢复和归片写回检查。
- `NATIVE_PC_PUBLISH.json`：完整 App 原生创建窗口填写项目与目标，原生文件选择框选一张 JPEG，点击发布按钮；代理及云确认和受保护链接实际落盘。首次原生文件框 UIA pattern 不可用，后改用校验该进程控件句柄的标准文件框消息完成选择；未调用浏览器或 CDP。
- `NATIVE_PC_COLLECT.json`：原生刷新客户结果按钮返回已选 1/1、客户只读勾选和服务器 v1/r5 回执，显示写入对应归片项目。
- `NATIVE_APP_REOPEN.json`：原生 App 优雅关闭，PID 37928 → 10556，新进程恢复原选片和同一回执。
- `NATIVE_COLLECTION_SQLITE.json` 与 `NATIVE_BOUND_WORKFLOW.json`：SQLite 精确项目 Id 包含唯一选中文件，原生项目历史打开对应归片后显示 PHONE_TEST.JPG。
- `NATIVE_LINK_REVOCATION.json`：点击原生撤销后，旧 HTTPS 页面与带客户凭据的接口均 404。该原生专用测试相册已撤销，不是发给用户的 iPhone 相册。
- 四份 `CUSTOMER_JS_*.json`：实际部署 JS 字节与源码相同，使用 Node DOM adapter 通过真实 HTTPS 验正常提交、确认响应丢失恢复、错误回执拒绝、确认后额外网络失败。Node 测试不算 Safari 真机。
- `TEST_SUMMARY.json`：API 13、Core 37、WPF 21，共 71 项通过。JS / PowerShell 语法检查通过。Release App 及服务构建发布通过。二进制与源文件摘要见 PACKAGE_IDENTITY.json。固定原构建 SourceRevisionId 后，从当前源码重建的服务 DLL 和合同 DLL 与正在运行的版本逐字节 SHA-256 一致；未固定时的 Git 元数据差异也保留原值。

## iPhone 13 Safari 待验

iPhone 专用项目 `4a3648e4-5696-41b5-9932-18e776c4943b`，仅含 `PHONE_TEST.JPG`。没有对这个项目自动勾选或确认。`IPHONE_TEST_READINESS.json` 已验证 HTTPS 页面、授权接口、可编辑状态、受保护 JPEG 200 及解码有效。

手机先连接家里同一 Tailscale 网络，再做两步：

1. Safari 手动打开本地链接文件中的完整 URL，勾选唯一照片。
2. 点击确认选片，接受二次提示，看到服务器已确认和回执后反馈实际结果。

收到用户操作后，通过 PC 原生刷新、确认回执与准确归片项目记录继续验收。服务器确认本身不能证明设备一定为 iPhone 13，因此真机结果同时需要用户反馈；状态见 PHONE_REAL_DEVICE.json。未得到反馈前保持 WAITING_FOR_USER，不标通过。

原有 Tailscale Serve 已从 17648 改指向本轮 17649，未启用公网 Funnel、未修改防火墙。服务运行根 `artifacts/online-selection-http/resume-server`。如 PC 休眠或重启，手动执行 `Start-SelectionServer.ps1 -PublicBaseUrl https://desktop-beiweiqu.taile9dfde.ts.net` 保持终端运行。`Get-PhoneSelectionStatus.ps1` 可只读查看进度和服务器回执，不代替手机提交。

本轮完整 App 启动与原生窗口操作成功，已补齐上轮此项待验。上轮 Git 推送失败已解除，历史报告保留为历史事实，本轮以本目录收据为准。原测试服务仍保留，不修改旧树。浏览器扩展未开始。
