# 手机档期首版 · 公司真机验收准备

2026-11-12（Europe/Berlin）。状态 **PARTIAL：公司本地发布、真实PC/HTTP与重开已通过；尚无iPhone真机结果**。

从 `feature/mobile-schedule-v1-20261111@c666b1f4035de50cbdf5f8c2b44e297287e1a34d` 接续。功能源码仍为 `7edd9a65da2a189fadd615c59063283f90037a98`。本轮只调整发布、机器配置与验收工具：冻结输入检查保证src、extensions及构建输入与7edd9a65相同；实际App及服务ProductVersion都为 `2.3.0+7edd9a65da2a189fadd615c59063283f90037a98`。家中路径和主机未当公司现场使用。

## 已完成的公司环境检查

| 项目 | 结果 |
|---|---|
| 运行环境 | 系统仅.NETCore/WindowsDesktop8.0.11，无SDK或ASP.NET10；隔离SDK10.0.302用于构建。App、ASP.NET服务和NativeHost自包含.NET10.0.10。服务在未传DOTNET_ROOT的环境冷启动，未替换系统.NET。 |
| 共享库 | App显式隔离根与服务Schedule:Root同为 `E:\Kitao-Deliveries\mobile-schedule-company-20261112\runtime`，数据库为该根下Data/pixel-tart.db；只包含3条新建合成预约，不连接日常公司库。 |
| HTTP | 现有版本冲突、同操作重试、丢响应重试、负载复用拒绝、冲突/日期校验、设备授权等26项真实发布服务检查通过。它们是本地自动预检，不是Safari。 |
| 实际PC改→HTTP | agent-desktop1.2.0对发布版WPF快速编辑器输入并保存，2026-11-12 10:30–11:30、已延期；HTTP与SQLite读回revision4且只递增一次。首次保存ref校验超时未投递，刷新ref后一次有效Invoke；没有重复保存。 |
| 服务重启 | 仅停止已核对路径的自有服务进程，再从本包自包含EXE重启；预约完整字段、版本、设备授权及owner key保持。 |
| App正常重开 | 旧PID35996正常退出、新PID3516；原生日历与详情仍显示11月12日10:30–11:30、已延期，HTTP仍revision4。 |
| 本轮手机授权 | 已签发本轮独立iPhone Safari设备，48小时有效，明文仅存包外。重启服务后以该凭据进行PC只读请求仍有效；不登记为真机操作。 |
| 探针暂停 | 运行根暂停标记已启用，C# seed/roundtrip/pc-adjust与JS修改/丢响应模式被拒绝；负向检查确认预约没有写入。原自动化设备已撤销，未遗留修改探针进程。 |
| 浏览器时区 | 保持预约Asia/Shanghai，另设无预约写入能力的loopback诊断观察器。只做过独立本地自检，其User-Agent明确非iPhone；实际Safari IANA时区仍待记录。原手机页面和服务src未改。 |
| 完整包 | 872文件ZIP逐entry核验，含完整模块App、手机服务、现有采集扩展/NativeHost、机器配置、运行时和诊断工具；无数据库、owner key或手机完整链接。 |

主预约 `a08ae657-84ce-43e5-948c-a5f39aae32e9`，预约时区 **Asia/Shanghai / UTC+8**（库存Windows Id为China Standard Time）。浏览器时区另记，不能用电脑时区、预约时区或本地Node自检代替。当前数据为10:30–11:30、Postponed、revision4，尚无手机修改。

## 私网前置与下一段

公司机没有已有Tailscale安装、服务或授权状态。已按用户指定的既有私网方式准备并安装官方1.104.6客户端，官方SHA256与Tailscale Inc. Authenticode有效，未重启电脑。初始状态NeedsLogin，需要本人将 **PC-20250309WEIZ** 加入原已授权Tailnet。未启用Funnel、exit node、子网路由接收或系统DNS接管；没有复制家中节点私钥，没有拿家中desktop-beiweiqu地址当公司入口。

账号入网确认链接只存交付目录network-prerequisites私有文件，并直接交给用户。确认Running和正确Tailnet后，再按公司Self DNS配置独立私网HTTPS端口，保留原有Serve路由；验证可信TLS，配置/qa浏览器时区观察器，随后给出手机完整授权链接和两步操作。手机改11:30–12:30、已确认后由PC刷新核对；PC再明确改一次，由Safari刷新确认；最终还要重开App和服务核对数据与该手机授权。本节均为后续步骤，未标为通过。

## 公司实际入口

包：`E:\Kitao-Deliveries\mobile-schedule-company-20261112\Guangjing-MobileScheduleV1-Diagnostic-win-x64`。
ZIP：`E:\Kitao-Deliveries\mobile-schedule-company-20261112\Guangjing-MobileScheduleV1-Company-Diagnostic-win-x64-20261112.zip`。
入口：Start-MobileScheduleApp.cmd / Start-Guangjing-Diagnostic.cmd；服务Start-MobileScheduleService.cmd。当前App3516、服务端口17662就绪。配置中的回环地址只用于PC预检，不能给iPhone冒充可达HTTPS链接。

完整机器绑定和已核验证据见COMPANY_READINESS.json、EVIDENCE_INDEX.json与receipts。运行根MOBILE_COMPANY_CONTEXT.json记录继续点；PHONE_DEVICE_PRIVATE.json及PHONE_BROWSER_OBSERVER_PRIVATE.json位于包外。功能产品不变，批量采集继续沿用057fe820的已完成结果，不重复验收、不重新注册既有浏览器扩展。
