# 手机档期首版交付

2026-11-11，Europe/Helsinki。产品代码 `7edd9a65da2a189fadd615c59063283f90037a98`，分支 `feature/mobile-schedule-v1-20261111` 已推送并回读。本轮后续文档提交不改变产品源码。

**本机 HTTP 双向修改、PC 原生编辑/读回、App 与服务重开：PASS。iPhone 13 Safari：已准备受保护入口，等待用户操作。**

## 统一数据源

从批量采集验收 057fe820 接续，包含冻结产品 1698664d；先 fetch，保留旧树、未推送和未提交成果，在独立工作树实现。核对见 SOURCE_AUDIT.md，旧树状态见 WORKTREE_PRESERVATION.json。

PC 工作日历和手机服务共同读写**同一份** `Data/pixel-tart.db` 的 ShootBookings。不是把预约复制到云端另一张表。手机服务复用 ShootBookingService 的字段/时间校验、BookingConflictDetector 和 SQLite 聚合保存；原联系人、人员、准备清单、金额字段不由手机覆盖。

手机可看当日、近期14天、预约详情，并修改时间与状态。PC 新增“刷新档期”，连同当前所选详情一起读回。PC 编辑器捕获打开时版本，手机 PUT 必须携带 ExpectedRevision 和稳定 OperationId；过期版本 409 提示刷新。版本比较和保存、操作回执在同一 SQLite 写事务中完成。相同修改重试返回原结果；同 Id 改负载拒绝。PC 人员/清单重建产生的时间戳不影响稳定表单指纹。状态工作流使用版本检查，直接状态/归档写入递增 Revision。

日历 schema 8→9 的迁移增加 BookingMutationReceipts、ScheduleDevices，没有重建或另建预约库。迁移时沿用现有备份机制。本轮全部运行根使用合成预约，不连接用户日常数据库。

## 实际运行回执

| 项目 | 结果与证据 |
|---|---|
| 最终冻结服务实际 HTTP | 26 项通过；HTTP_ROUNDTRIP.json、HTTP_REQUESTS.jsonl |
| 手机改时间/状态→PC读回→PC编辑器改→手机读回 | PASS；HTTP_ROUNDTRIP.json、DEPLOYED_MOBILE_JS_MODIFY.json、DEPLOYED_MOBILE_JS_PC_READBACK.json |
| 旧 PC 编辑器与旧手机版本拒绝覆盖 | PASS；HTTP_ROUNDTRIP.json 409；原子并发测试 |
| 保存成功但响应丢失后重试 | PASS，同回执、同版本；LOST_REPLY_RETRY.json |
| 时间结束早于开始 / 时间重叠 | 400 / 409；原规则与冲突提示复用 |
| 无授权、错误令牌、错预约、日期范围、设备撤销 | 401 / 404 / 400；DEVICE_AUTHORIZATION.json |
| 独立 PC 进程恢复、手机重新读取 | 4 项通过；PC_NEW_PROCESS_REOPEN.json |
| 完整 App 原生编辑、保存、HTTPS 读回 | PASS，10:30–11:30，revision 7；NATIVE_PC_TO_MOBILE_HTTP.json |
| 完整 App 正常关闭重开 | PASS；NATIVE_APP_REOPEN.json、NATIVE_CALENDAR_READBACK.json |
| 服务停止后从最终安装包重启 | PASS，设备授权和预约保持；SERVICE_RESTART.json |
| Core 定向回归 | 265/265；含并发重试、旧版本、人员清单保留、旧库迁移 |
| WPF 定向回归 | 307/307；含 PC 编辑器重复保存稳定指纹 |
| 完整包、路径和摘要 | 557 文件校验通过；HOME_INSTALLATION.json、PACKAGE_FILES.json |

HTTP JS 由 Node DOM adapter 执行，校验实际服务下发脚本与源码一致。它不是 Safari。PC 原生操作通过 agent-desktop 1.2.0；首次旧 ref 保存请求被拒绝且未发送，重新读取当前控件后仅执行一次保存，再从 HTTP 验证。期间 UI 字段短暂被用户输入干扰，重新读回并设定合成目标后保存。没有使用 Browser、WebView、Computer Use、Chrome 插件或 CDP。

## 实际家中交付位置

可运行完整诊断目录：
`C:/Users/Admin/AppData/Local/Guangjing/Deliveries/MobileScheduleV1-20261111`

主入口：`Start-Guangjing-Diagnostic.cmd` 或 `Start-MobileScheduleApp.cmd`。它们按包内 mobile-schedule-runtime.json 读取实际运行根，并直接打开合成预约的工作日历。服务入口：`Start-MobileScheduleService.cmd`，需要本机 .NET 10 ASP.NET runtime；App 自包含 win-x64。当前二者已运行，重启前不要重复启动服务端口。

ZIP：`C:/Users/Admin/AppData/Local/Guangjing/Deliveries/Guangjing-MobileScheduleV1-Diagnostic-win-x64-20261111.zip`。129.4 MiB，所有 ZIP entry 校验通过；摘要见 PACKAGE_ARCHIVE.json。未包含凭据、测试数据库或手机完整链接。该 ZIP 使用当前本机路径配置，换机器应重新生成配置，不宣称跨机安装通过。

这仍是全模块诊断包，非 Setup。完整模块 App、手机服务、原批量采集扩展和 NativeHost 已打包；没有重新注册浏览器或更改既有扩展加载目录。启动日志已核对 ProductSourceSha=7edd9a65da2a189fadd615c59063283f90037a98。

旧交付记录的 E:/Kitao-Deliveries 在当前本机实查不存在，未沿用该虚构路径。本轮从系统 LocalApplicationData 解析实际位置并生成启动文件，保留所有旧工作树和成果。

## iPhone 13 Safari 两端验收待办

受保护手机入口已经准备，完整链接仅保存在忽略目录：
`artifacts/mobile-schedule/final-runtime/PHONE_URL.txt`。凭据没有提交到版本库。

日历设备授权为48小时，可由电脑 owner 撤销；token hash 存同一日历库。链接 fragment 进入当前 Safari 标签 sessionStorage 后移除地址 fragment。服务响应 no-store/no-referrer，敏感内容只按设备凭据开放。预约字段以 textContent 渲染，不暴露客户联系方式和金额。

手机先连接家中同一 Tailscale 网络：

1. Safari 打开已给出的完整链接，点击“合成预约 · 手机档期测试”。
2. 把时间改为当天 **11:30–12:30**，状态“已确认”，保存；看到“服务器已保存”及版本号后反馈。
3. 收到用户反馈后，PC 点击“刷新档期”读回，再由 PC 调整一次；手机点“刷新”核对电脑修改。

当前主合成预约 Id：bd5abc99-d733-4564-8a7f-e6bba5c5ecdd。交付时状态为10:30–11:30、Postponed、revision 7，只有自动本机流程修改过。PHONE_REAL_DEVICE.json 标为 WAITING_FOR_USER；没有把授权成功、HTTP/Node 或 PC 原生测试算成 iPhone 真机通过。

服务回环地址 127.0.0.1:17662，Tailscale HTTPS 独立端口 8443。原在线选片443→17649路由保持。未启用公网 Funnel 或改防火墙。PC与服务需在线，这是家中测试首版，不包含外部云端多设备离线同步。当前网页不自动新建预约，只能改已有预约。

## 复验与续接

- `tools/MobileScheduleAcceptance/Publish-MobileScheduleDiagnostic.ps1`：按本机 LocalApplicationData 发布全模块包、手机服务、采集 NativeHost，生成实际路径和摘要。
- `Start-MobileScheduleApp.ps1`：打开准确的合成预约和隔离数据根。
- `Start-MobileScheduleService.ps1`：启动同一数据库服务。
- `Get-MobileScheduleStatus.ps1`：通过原 PC 日历 ViewModel 刷新并输出当前时间、状态、版本。
- `Probe-MobileSchedule.mjs`：自动 JS/HTTP 测试，仅限另行准备的合成验收；不要再运行于已发给用户操作的主预约，以免干扰真机往返。

浏览器扩展本轮没有开发变更。后续优先完成用户 iPhone 修改→PC读回→PC再改→Safari刷新的真机回执。
