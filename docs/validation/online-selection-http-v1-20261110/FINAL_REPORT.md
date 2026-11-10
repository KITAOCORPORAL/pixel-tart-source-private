# 在线选片 HTTP v1：本机完整闭环交付

报告日期：2026-11-10（Europe/Berlin）；Asia/Shanghai 同日；记录时间 2026-11-10T02:33:19.334724+01:00 / 2026-11-10T09:33:19.334724+08:00（Asia/Shanghai）。

状态：**LOCAL_HTTP_V1_READY_FOR_REVIEW_PHONE_PENDING**。本机真实 HTTP 闭环已完成；物理 iPhone Safari / 微信内浏览器为 **NOT_RUN_ENVIRONMENT_NOT_READY**。全模块诊断应用仍是 **PARTIAL_DIAGNOSTIC_NOT_FOR_RELEASE**，不以单项通过代替全产品发行验收。

## 交付身份与范围

- 独立分支 `feature/online-selection-http-v1-20261109`；基线交付 `37ea6c9abaae94a3f4e2ac6b615837eb02e89633`，保留功能源 `02702071e1eda7e8f51c806a7e1aeb8fa9bb18c2` 的收藏/已用角标和完整产品范围。
- 首个功能提交 `34b72f448d1ac094156295b875b6c2d96ec9af66`；最终产品源码 **`c44f800a428ba097e024c91e7691dbf3b6a17355`**，把 HTTP 与凭据选择置于桌面基础设施层，Core 保持无 HTTP、无 localhost 和无凭据实现。
- 桌面/服务均从该产品源码以 Release、win-x64、自包含发布。桌面 `WholeAppDiagnosticBuild=true`、`PhotographerTrialBuild=false`。既有全模块、策划15项、教程退役保持；没有安装器。
- `81bc7be925a0fe7d2600ec275a6049a621e283de` 为启动验收支持提交，与 c44f800a 的 `src` 差异为空。后续只新增验收工具和交接证据；最终提交/远端身份另存本地 `completion-20261110/PUSH_READBACK.json`，不将文档 HEAD 冒称产品源码。
- 本次恢复时重新枚举68个工作树、refs、HEAD、dirty及远端。旧 `feature/online-selection-v1` 当前仍为 `393c1a545bb3d504ac1d3721576dc96f0c220f2f`，有其自己的未提交内容；只读参考固定 `349bf4006faf9b80a3691b7d9fd368271ca58ebd`。没有合并、清理、重置或写入旧树；历史审计中的279条dirty是当时快照，不拿作当前计数。

- 远端刷新发现另一独立实现 `origin/feature/online-selection-http-20261109@c36e1f38`（完整SHA见REMOTE_BRANCH_BOUNDARY.json）。与本会话81bc7be9以37ea6c9a为共同祖先、各4个独有提交，src存在实质差异；本次没有合并或改写它，不能将两条线视作同一产品交付。

## 可见入口与实际行为

正式完整桌面入口：在线选片 → 创建并导入照片 → 保存稳定项目/素材 ID → 生成代理 JPEG → 上传/重试代理 → 等待服务确认 → 带规则发布 → 复制服务签发的受保护链接。

配置由 `PIXELTART_SELECTION_ENDPOINT` 和 `PIXELTART_SELECTION_OWNER_KEY` 在当前进程显式提供。有效时选择桌面 HTTP 适配器；未配置或配置无效时保留 None provider 的可见提示。普通应用启动不自行开服务监听器。包内测试入口单独启动 LocalDev 服务和隔离桌面。

“本地代理就绪”和“服务确认上传”分开显示。未得到云素材身份时不能将本地 JPEG 当成已上传。项目 GUID 在第一次请求前落盘，创建使用稳定幂等键；上传以项目/素材 ID 和代理 SHA256 确认，断网重试不生成第二个项目或素材。

客户网页使用服务签发的相册范围凭证，可查看代理、勾选、确认；确认锁定后选择入口禁用，服务也拒绝修改。分享凭证在 URL fragment 中，HTTP请求路径日志不含该令牌。客户凭证不能调用摄影师接口或读其他相册。撤销删除客户授权，过期由服务强制拒绝。

PC 可见“刷新客户结果”入口校验项目、已知素材、选片版本、修订和锁定状态，映射到本地原文件名后写入现有工作区。结果通过既有“同步归片工作区”入口返回关联项目；独立进程重开后，选择、版本和归片项目 ID 仍在。该同步导入选择和项目关联，不自动复制原图；缺少 RAW 匹配时维持需确认状态。

首片支持目标/最少/最多张数、文件名可见性、收藏和确认锁定。收费/加选、PIN、水印、客户备注和原图下载不在本轮交付；相应发布请求明确拒绝。当前服务 `production=false`、loopback-only、单摄影师 LocalDev，不宣称公网多租户服务。

## 实际验收与请求

冻结产品 DLL 逐项绑定到测试输出，**30条绑定哈希一致**。相关回归 **90 PASS / 0 FAIL**（Core 52、WPF 38；71个测试方法，包含数据行），再加完整 App 四阶段 **4 PASS**，最终所选 TRX 共 **94 PASS**。这不是全仓测试通过声明。

| 阶段 | 实际 App 进程 | 已验证结果 |
|---|---:|---|
| 断网创建/导入 | 28716 | 原生UIA创建入口和系统文件选择器；合成 PNG 生成代理，localReady=true、cloudConfirmed=0，草稿和稳定ID保存 |
| 重开上传/发布/复制 | 29136 | 同一项目和素材ID；真实服务确认1张；规则随发布请求提交；复制服务签发链接 |
| 刷新/回写/同步归片 | 26828 | 浏览器确认后，原生UIA点刷新和同步入口；保存选择版本1/修订4；关联归片项目并明确选择归档目录 |
| 再重开/撤销 | 29096 | 与前进程不同；未先刷新HTTP即读到版本1/修订4、已选1张及同一归片项目；通过原生入口撤销 |

这些桌面阶段运行真实 App/MainWindow 和生产控件的独立 TestHost；操作使用原生 UIA，系统文件/文件夹对话框使用已观察控件与确认消息。截图标为 OFFSCREEN。它们不能当作物理鼠标、完整 DPI 或真机手机验收。

客户网页在桌面内置浏览器真实点击可见 checkbox 和确认按钮，服务返回后显示“已确认并锁定”，勾选和确认入口禁用。390px 是历史同网页源码的响应式检查：`34b72f44..c44f800a` 的 wwwroot 无差异；该图不冒称新手机实测。

最终冻结批次保留 **44条服务请求**，逐条时间、method、path、status 见 `HTTP_REQUESTS.jsonl`。其中有客户选择/确认、PC最终结果读取、摄影师撤销、跨项目拒绝、锁定拒绝、过期和撤销拒绝。开发阶段旧服务及失败请求仍在原目录，不混入44条最终计数。

- 创建和上传各重放两次：同一项目/素材/修订；最终对应项目记录1、素材记录1。
- 错误项目、客户调用摄影师API、客户读取另一本相册：401。
- 确认后尝试修改：409 `SelectionLocked`，前后确认快照相同。
- 到期页面及API：410；由3秒过期的独立合成相册验证。
- 原生桌面撤销后：客户页面404、API401，客户授权已移除；确认版本仍为4。
- `OnlineHttpResultGuardTests` 的5组异常结果只验证拒绝错误项目/素材/版本/修订/锁定；其fake不计作真实HTTP证据。

额外通过三个包内启动检查：配置服务的测试启动器、正常 EXE、正常 CMD，均核对11个导航控件（10主导航加素材库入口），关闭退出0。正常 EXE/CMD 本次 App PID为20060/32660。配置入口的既有实际包验收PID7476；未将其冒充完整四阶段业务验收。

## 可运行的完整包

- ZIP：`E:\Kitao-Deliveries\online-selection-http-v1-20261109\Guangjing-OnlineSelectionV1-FullApp-Diagnostic-win-x64.zip`
- 大小：**158,575,491 bytes**；**851 files**；展开 **391,268,504 bytes**。
- SHA256：`95FDF2C86B2A05AAC9FACCB030ED3BBC75D52EC0B889C61F3B86E88B74E18194`。
- `PACKAGE_MANIFEST.json`逐项列出路径、长度与SHA256，ZIP逐项读取后全部一致。扫描已排除本轮已知的owner/client凭证、私人连接、测试工作区/服务数据库和照片；包不包含RAW或本地像素缓冲。

解压后运行 **`Start-OnlineSelection-LocalTest.cmd`**。脚本生成独立私有测试根、合成JPEG、创建凭据，并启动包内自包含服务及完整桌面，不需要另装SDK。桌面走上述入口，复制链接后在本机浏览器确认，再回PC刷新/同步；关闭重开同一测试桌面验证持久化。停止测试服务用 `Stop-OnlineSelection-LocalTest.cmd`，保留数据。普通 `Start-Guangjing-Diagnostic.cmd` 保留未配置模式。

测试资料根默认 `%LOCALAPPDATA%/KitaoDiagnostic/OnlineHttpV1-c44f800a`；脚本只把配置传给子进程，不改全局环境变量、系统服务、防火墙或DNS。`PRIVATE_CONNECTION.json`及分享链接留本地，不共享到仓库或包。

## 源码复用、保留问题与后续

固定旧源码只复用 API DTO/路由合同、JPEG变体和对象存储，以及HTTP/DPAPI访问存储模式；没有导入旧preview shell、微信小程序或未提交内容。为了稳定ID、owner/client权限分离、确认快照和撤销/过期，新增独立带进程锁的原子JSON LocalDev仓储。详情见 `SOURCE_REUSE_REVIEW.json`。

原 `ProductionCoreContainsNoHttpLocalhostOrCredentials` 断言未放宽，最终相关回归通过。初期发现的浏览器勾选状态问题和Core传输层边界问题已修正；开发失败收据保留，本报告只引用最终冻结批次。

已授权HTTPS入口未提供，当前设备面也没有物理iPhone会话：**iPhone Safari / 微信内浏览器真实联网验收待做**。PC与手机档期同步继续是独立任务。本轮只用合成图/代理JPEG，新增RAW Open/Unpack/Process/MemoryImage调用均为0；生产 `raw_r.dll` SHA256仍为 `F500C0732FEB21B188D5B52CEA05FD824D5B3C8016EB2CA68D8312ACC9F914B9`。既有RAW和色彩研究问题未在本轮重判或宣称修复。

## 本地原始证据

全部开发失败与早期结果：`E:/Kitao-Deliveries/online-selection-http-v1-20261109`。

最终实际服务：`final-http02/server-state/requests.jsonl`；最终桌面：`final02-app-evidence` / `final02-app-runtime`；最终回归：`final-regression-02`；本次补完：`completion-20261110`及`completion-app-reopen-20261110`。含凭证的原始根仅留本地。仓库内本报告目录包含脱敏回执、阶段TRX、包身份和合成图截图。
