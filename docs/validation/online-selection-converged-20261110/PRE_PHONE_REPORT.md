# 在线选片整合版：机器闭环完成，等待 Safari 真机反馈

2026-11-10，Europe/Berlin；记录 2026-11-10T03:44:21.125685+01:00。状态 **READY_FOR_IPHONE_SAFARI_AWAITING_USER**。本报告是手机操作前的整合验收，不是最终人机闭环回执。用户操作后的PC收回、重开和最终回执尚未执行。完整产品仍为诊断版本，不能发行。

## 版本与保留边界

独立工作树 `E:\Kitao-Worktrees\online-selection-converged-20261110`，分支 `integration/online-selection-v1-20261110`，以 `8e447075301074fd1a3c966b34ec64635dd72eb2` 为祖先，保留该完整包和桌面基础设施。选择性移植 `c36e1f38323bdb11bf4f731421893ff5cabfce00`（产品feef4bc2）的确认回执校验和响应丢失恢复。两条原分支保留，未合并/覆盖其工作树与未提交内容。

最终冻结产品 **`7769b8ee63ad46cd22efb154145766c45b34ba69`**。`c8a2f5cbb3945ea5367445c4f8967639ae543b15` 仅更新schema8测试断言；后续验收工具/文档提交与最终产品src无差异。产品、测试支持、文档HEAD和ZIP身份分别记录，远端精确读回存本地 `PRE_PHONE_PUSH_READBACK.json`。

原候选181de897、测试代理问题和失败回执均保留在本地原目录，不混入最终通过计数。原生重开导航发现真正缺陷：模型反向选片ID未被SQLite仓储存储。最终产品增加schema8的nullable列、唯一索引、读写映射及冲突拒绝；原迁移备份机制保留，普通保存不会清除已知身份。旧无反向身份记录不按名字猜测补绑。

Core原 `ProductionCoreContainsNoHttpLocalhostOrCredentials` 断言原样并通过。Core新增的是纯数据身份持久化；HTTP和DPAPI凭据实现继续位于桌面Services/OnlineSelection。浏览器扩展未启动，PC/手机档期同步不在本轮范围。全部模块、策划15项、教程退役保留，WholeAppDiagnosticBuild=true / PhotographerTrialBuild=false，无安装器。RAW四项调用0，生产raw_r.dll的F500C073…哈希保持。

## 字段合同与行为

详见CONTRACT.md与PORT_PROVENANCE.json。相册素材为 `selectionAssetId`，最终条目沿用本分支 `imageId`，不能混用另一网页的字段/鉴权/媒体假设。新增客户相册的nullable `confirmation`，内容来自同一服务器持久化快照；owner最终读取和客户恢复共享projectId / selectionVersion / revision / confirmedAtUtc / isLocked / items合同。客户文件名仍按规则隐藏。

网页校验项目、版本、安全整数修订、锁定、时间、素材归属、数量、重复ID和选择类型。有效确认POST回执直接显示成功；响应丢失从授权GET恢复，并经过相同校验。HTTP200错误项目回执不显示成功。PC校验后落盘，HTTP客户选择标记只读，None本地模式保持可编辑。

## 最终机器验收

- 发布DLL **30条绑定**逐项一致；最终回归 **222 PASS / 0 FAIL**（Core 183、WPF 39）。原三项旧版本断言7→8单独复验通过，初始失败和重查均保留，不重复计数。
- 完整App原生UIA六阶段 **6 PASS**，最终所选TRX合计 **228 PASS**。包括断网草稿、同ID上传/发布、真实HTTP错误项目拒绝且未写入、PC刷新/归片、独立进程重开/撤销、再次独立进程重开后实际回到同一归片项目。
- 最终SQL只读核验：schema8，选片项目 `4599c543-cc5c-4493-b496-9c27afc4afe9` 对应唯一归片项目 `dc0adaec-477a-4163-9b80-7256639ba5ba`；双向ID在实际SQLite中保留。
- 最终客户JS四项真实HTTP故障测试PASS：正常；错误项目HTTP200拒绝；真实服务器提交后HTTP响应连接丢失、授权GET恢复；收到有效回执后后续刷新失败不抹掉成功。独立HTTP代理只用于验收，不进入应用包。Node DOM adapter不是Safari。
- 最终桌面内置浏览器实际勾选/点击确认，显示服务器v1/r4和锁定控件。截图为单独自动验收相册，不是留给用户的手机相册。
- 最终完整发布EXE/CMD原生UIA启动、11导航和正常退出0通过。控件离屏截图标OFFSCREEN，不冒称物理鼠标、DPI或Safari。

原始回归 `regression01`、三项复验 `schema-recheck01`；最终原生目录final-*；浏览器故障 `customer-cases-final01`；主链工作区 `actual02-app-*`。HTTP请求和故障边界日志已脱敏复制到本报告目录。

## 手机专用 HTTPS 相册

使用**同一最终冻结包**的服务和桌面，从真实原生入口建立独立 `phone01` 项目并经HTTPS发布。服务进程1936的实例与HTTPS /health响应相同；部署JS SHA256和本地冻结字节相同。页面/授权API/受保护JPEG全部200，未带凭证的API和图片均401，JPEG实际解码为96×72。详情PHONE_READINESS.json。

- 项目 `80d225b5-1644-4842-936e-85abb13172f4`，素材 `5696d77c-7ca8-46d2-ace9-24f064573563`。
- 交接前 **已选0、未确认、未锁定，confirmation=null**。代理没有勾选或确认此相册。
- 当前等待用户在iPhone Safari：打开本会话提供的完整HTTPS链接并勾选橙色测试图；点击确认选择并锁定，回报页面v/r及是否成功。不能把本地浏览器和Node测试记作手机验收。
- 完整分享链接及创建/owner凭据仅在本地 `PHONE_CONTEXT.private.json`、`phone01-app-evidence/SHARE_LINK.secret.txt` 和 `phone-service/PRIVATE_HTTPS_CONNECTION.json`，不进Git或ZIP。
- 当前为临时HTTPS入口和独立合成测试资料，进程日志/身份在phone-tunnel、phone-service；未变更防火墙、系统服务或持久全局环境配置。

家庭c36e1f38相册的私有链接不在当前机器，未实际复验；FAMILY_ALBUM_SEPARATE_RECEIPT.json单列，不能混入整合版结果。

## 收到用户反馈后

保持冻结产品和phone01资料根。把用户真实反馈原文记录到本地PHONE_HUMAN_FEEDBACK.json；未确认或打不开时不得写成功。`tools/online-converged/collect_phone_feedback.py`核对服务器已保存回执与部署字节，然后从同版本原生PC入口刷新/同步、再独立进程重开并回到唯一对应归片项目。全部通过后才产生PHONE_FINAL_RECEIPT，并提交绑定本整合版本的最终报告。当前 **手机后PC收回NOT_RUN，最终人机回执尚未签发**。

## 完整包

`E:\Kitao-Deliveries\online-selection-converged-20261110\Guangjing-OnlineSelection-Converged-Final-Diagnostic-win-x64.zip`，**158,579,526 bytes / 851 files**，SHA256 `508375E57896BAB0F3761C0430FD580E1A15EEE826FF7F148EB3F52274F929DE`。逐文件清单及ZIP逐项读回通过，24个已知凭据值扫描未进入包；无测试数据库、私人连接或照片。产品与已发布JS/DLL身份见BUILD_IDENTITY.json。

解压运行Start-OnlineSelection-LocalTest.cmd可启动独立本机合成图闭环；该本机入口与正在等待用户的手机相册数据根分开。整个本地证据根 `E:\Kitao-Deliveries\online-selection-converged-20261110`，旧包/旧失败不覆盖。
