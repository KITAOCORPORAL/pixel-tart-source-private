# 在线选片首版交付 · 2026-11-09

状态：**本机真实 HTTP、HTTPS 接口和桌面命令闭环通过；Safari 真机及完整 App 原生窗口验收待人工完成。**

已先 fetch。远端早期在线选片分支为 LocalDev 原型，37ea6c9a 的交接收据声明尚无真实联网 provider。本轮从指定基线 37ea6c9a 建立独立分支 feature/online-selection-http-20261109；仅恢复原型相关路径，未整体合并旧分支。所有旧工作树及未提交成果保留。

## 已完成

复用原 WPF JPEG 代理生成器、SelectionUploadQueue、JSON workspace、SelectionResultSyncService 和旧 ASP.NET / SQLite 原型。完整 App 通过 GUANGJING_SELECTION_BASE_URL 显式接入 HTTP provider；无配置保持 None 行为。

PC 发布命令用稳定 Guid 和预先保存的 Windows 用户加密凭据创建远端项目，通过队列上传代理、同步规则，再由服务器签发客户链接。CloudAssetId 表示服务确认，代理 Ready 单独存在。

手机页面有受保护相册、勾选、数量提示、确认和确认后锁定。客户凭据与 owner 凭据分离，客户不能调用管理 API。每个项目独立鉴权，媒体会话绑定项目。撤销轮换 publicId 并移除媒体会话；重新发布后旧链接仍失败。服务校验截止和访问过期。

桌面新增刷新客户结果、复制选片链接、撤销发布。刷新校验项目、素材 Id、SourceAssetId、文件名、确认锁定和版本，先保存结果，再按 CollectionProjectId 写入对应 SQLite 归片项目。未绑定的旧选片项目使用自身 Id 创建归片项目，不按名称或当前页面猜测。项目历史可继续归片。

创建、上传、选择、确认均使用稳定重试身份；同 Id 不同内容或操作负载返回冲突。上传摘要与 ready 素材在同一 SQLite 事务保存。重开恢复 JSON、加密凭据和归片结果。

## 实测证据

| 检查 | 结果 | 证据 |
|---|---|---|
| 实际监听端口上的桌面命令闭环、重试、撤销、隔离 | 45 项 PASS | REAL_HTTP.json |
| 独立可执行进程重新读盘并再次回写 | 7 项 PASS | NEW_PROCESS_REOPEN.json |
| 原样交付 JS 在 Node DOM adapter 中勾选三次、确认、重载 | 14 个真实 HTTPS 请求均 200 | CUSTOMER_JS_HTTPS.json |
| 网页结果经桌面刷新命令写入对应归片项目 | PASS | WEB_RESULT_WRITEBACK.json |
| 现有 Tailscale HTTPS 页面和相册接口 | 200 | PHONE_HTTPS.json |
| API 回归，包含服务重建、越权、过期、重试身份重绑拒绝 | 13 / 13 | TEST_SUMMARY.json |
| Core 选片回归 | 37 / 37 | TEST_SUMMARY.json |
| WPF 选片回归 | 21 / 21 | TEST_SUMMARY.json |
| 完整 App Release 发布与服务发布 | PASS | PACKAGE_FILES.json |

使用自行生成的单张测试 JPEG，原片 SHA 未改变。既有结果同步服务已生成归档；单张 JPEG 没有配套 RAW，匹配状态需关注，未宣称真实 RAW 归片通过。WPF 测试及真实命令调用在 STA 进程进行，不能当完整 App 原生窗口操作。

## 手机与完整 App 待验

最终服务监听 127.0.0.1:17648。原有 Tailscale Serve HTTPS 已改为转发此端口，未启用公网 Funnel。手机需连接同一 Tailscale 网络。本机已验证 HTTPS 可达，未声称家中手机已连通。

完整受保护测试链接保存在 `artifacts/online-selection-http/phone/PHONE_URL.txt`，未写入版本库；该项目保持未确认。此文件是本地测试交付资料。

1. 手机连接同一 Tailscale 网络，在 Safari 手动打开完整测试链接，勾选唯一照片，确认提交；记录 iOS / Safari 版本、时间和实际结果。
2. PC 执行 `tools/OnlineSelectionHttpAcceptance/Start-OnlineSelectionApp.cmd`，进入在线选片并打开手机测试项目，点击刷新客户结果。检查已选 1 张、项目历史中对应归片项目的输入。
3. 关闭并重新启动完整 App，确认项目和结果仍存在，重复刷新不多建归片项目。撤销发布后，手机重新加载旧链接应失败。
4. 手机不可达时检查 Tailscale、PC 是否休眠及服务是否仍运行。没有公网测试入口，不把 localhost 当手机地址。

## 运行入口

- 服务：`tools/OnlineSelectionHttpAcceptance/Start-SelectionServer.ps1 -PublicBaseUrl https://desktop-beiweiqu.taile9dfde.ts.net`，终端保持运行。服务已运行时不要重复启动同端口。
- 完整 App：`tools/OnlineSelectionHttpAcceptance/Start-OnlineSelectionApp.cmd`，固定使用手机测试相同的隔离数据，服务地址默认为本机 17648。
- 可复验：先启动服务，再执行 `tools/OnlineSelectionHttpAcceptance/Test-OnlineSelectionHttp.ps1`，每次使用新隔离验收根。
- 完整 App 位于 `artifacts/online-selection-http/app-final/`；服务位于 `artifacts/online-selection-http/service-final/`。App 是自包含 win-x64 完整诊断版，非安装包；服务需要本机 .NET 10 ASP.NET 和 Windows Desktop runtime。

所有脚本均不打开浏览器。凭据绑定原服务 origin，切换服务需独立数据根。服务仍为单机 LocalDev 存储，未提供公网多租户账户、收费、PIN、下载或水印；未支持的规则显式拒绝。浏览器扩展未开工。

## 未执行原因

自动审批拒绝了原生 App 启动，因此未操作原生窗口。自动审批也拒绝了初始服务停止命令，17646 旧测试进程保留，最终使用独立 17648 服务。未绕过自动审批。后续服务构建需使用独立输出目录，避免运行中 DLL 占用。未使用 Browser、WebView、Computer Use 或 CDP。Safari 需要人工操作现成测试链接。


## Git 交付回读

实现提交为 `6f28d5ba5ead64ed5083516d236e2a2271580deb`。本地工作树已提交。推送时 GitHub 443 超时，远端回读同样失败；随后本机已有代理的 HTTPS 请求也失败，因此不能声称提交已推送。

可导入 Git bundle 已生成并通过 git bundle verify：`artifacts/online-selection-http/online-selection-http-20261109.bundle`，包含实现分支，前置提交为指定基线 37ea6c9a。网络恢复后可在本分支执行 git push -u origin feature/online-selection-http-20261109；无须合并或改动旧树。
