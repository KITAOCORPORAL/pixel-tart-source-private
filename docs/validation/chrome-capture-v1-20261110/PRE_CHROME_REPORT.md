# 光镜 Chrome 单图采集 v1：开发版待加载验收

2026-11-10，Europe/Berlin，记录 2026-11-10T06:27:16.012213+01:00。当前状态 **READY_FOR_CHROME_MANUAL_LOAD_PARTIAL_ACCEPTANCE**。代码、完整配套桌面、本机桥接及合成测试页均已准备；真实Chrome右键→目的地→入库→打开原网页的整条入口验收 **NOT_RUN，等待开发版手动加载**。不能把Native协议探针当作真实Chrome右键证据。

## 身份与范围

- 基线在线选片验收 `2661f2d3fc6d993363d1e30c8b0339ae9fcc238b`、冻结产品7769b8ee；独立分支 `feature/chrome-capture-v1-20261110`，工作树 `E:\Kitao-Worktrees\chrome-capture-v1-20261110`。原整合分支与其他工作树、dirty均保留。
- 本功能冻结产品源码 **`ed5d15d451c5f92dc8ff99d9a4ff91af3e76289c`**。桌面、Native Host、共享合同DLL和扩展源码按此版本绑定。此后只有测试工具/说明更新，src及extensions不得混入未冻结改动。
- 完整桌面win-x64自包含，WholeAppDiagnosticBuild=true / PhotographerTrialBuild=false；全部模块、策划15项、教程退役保持，无安装器，本轮RAW四项调用0、raw_r.dll未换。
- 只做JPEG/PNG单图，16MiB/32MP上限；srcset明确最大候选；批量和侧栏下一轮。Native请求24MiB/响应1MiB，UTF8 JSON四字节小端前缀，当前用户Named Pipe，不为采集开启HTTP服务。另有显式loopback静态合成页服务器，不能执行导入。

## 可运行实现

Chrome图片右键“发送到光镜”打开轻量目的地窗口，选择桌面当前已知且可用的素材库、项目、灵感板。最近目的地仅在实际成功回执后更新；失效时要求重新选择，不能改送第一个项目。当前用户主动图片的匹配DOM节点用于srcset解析，不自动整页采集。

原图字节和页面URL/图片URL/页面标题/采集时间进入本地待处理队列。图片读取失败、最大候选403、歧义srcset、原图过大或格式不支持都会报错，不改用缩略图。已开始发送的图片保持稳定字节；异步旧读取不能覆盖待处理或已完成项。明确改选目的地用同一IndexedDB事务保留新项并标记旧项，避免两次落盘之间丢失图片。

Native Host用manifest限定扩展ID，并核验caller origin；配套桌面入口只设置子进程环境，桥接CurrentUserOnly。正常桌面启动不开桥接。桌面先验证目的地身份和原始内容，再复用既有ImportWithIdentitiesAsync/ManagedCopy、SHA256身份、ProjectAssetLink和InspirationTray。全局CaptureId/指纹journal在重试中固定内容和目的地，完成回执前读回实际AssetId、托管文件hash与关联。相同内容改送同库其他项目复用素材并添加关联，不覆盖备注/链接。

来源URL、图片URL、标题和时间在应用自有journal持久化，以LibraryId+AssetId引用。所选图片标题下有可见的“打开原网页”，不会把内部方法当作入口。首轮检查发现按钮误放在折叠详细信息内，原失败保留，现已移到默认可见位置并实际点击验证。

## 机器侧实际证据

- 源选择与严格回执身份/hash校验 **9 PASS**。
- 发布字节绑定的Core **4条TRX PASS**：三项采集journal持久化/重试/错误指纹拒绝/已提交来源回读，加原 `ProductionCoreContainsNoHttpLocalhostOrCredentials` 断言。Core只增加纯数据journal，不引入HTTP或凭据实现。
- 最终真实Native Host进程→完整桌面→实际SQLite **9项检查PASS**：首图导入与项目/板关联、同CaptureId重放、同图改送项目B复用AssetId、桌面关闭可重试、独立重开相同CaptureId、失效目标拒绝、不完整请求拒绝且桥接继续运行、错误扩展origin拒绝、超限帧拒绝。完整App进程 `8872` → `34428`；普通图实际AssetId **`0773c6e4-3ab2-42ea-b256-c4bb56579084`**，A/B两项目对应同一素材。
- 补充未入库新图在桌面关闭时提交，返回可重试；重开后同CaptureId成功，预置的中断.part文件不阻塞恢复，托管文件与1600×1200原图hash一致；再次重放仍是同一AssetId **`9ffbed93-f403-46bd-b1c4-11eaf88aeda8`**。这也是原生协议探针，不冒称扩展IndexedDB实际端到端已验。
- 原生UIA实际点“打开原网页”，公司Chrome出现该JPEG测试页，见NATIVE_SOURCE_ACTION.json和CHROME_OPEN_SOURCE_PROOF.json。它证明桌面来源入口，不能替代Chrome右键发送入口。
- 发布完整EXE/CMD常规启动、11导航和关闭退出0通过；Native/完整桌面/开发扩展组件无SDK运行依赖。

旧候选3713的界面失败、准备脚本两次自动审批拒绝、旧构建日志全部保留。自动审批只返回blocked by policy，未给具体原因；安装改为只生成可检查的当前用户.reg文件和显式前台测试页入口，**代理没有修改注册表或现有Chrome配置**。

## 公司Chrome手动加载与第一条真实闭环

详细步骤见INSTALL_AND_CHROME_ACCEPTANCE.md和包内README。

1. 固定包目录，运行Prepare-Guangjing-Capture.cmd；检查后手动导入Register-Guangjing-NativeHost.reg。仅注册HKCU下本主机，允许扩展ID **`obgahfdabcpkohkdcnidpmeooceomogk`**。
2. 双击Start-Guangjing-Capture.cmd，配套完整桌面使用独立company01验收资料根。已准备“浏览器采集测试库”“采集项目A/B”“采集灵感板A”；该根初始素材0，和机器协议探针的machine04根分开。
3. Chrome打开chrome://extensions，开发者模式→加载已解压扩展→选择本包extension目录。
4. 在公司Chrome打开 **http://127.0.0.1:51292/ordinary.html**，图片右键“发送到光镜”→测试库/项目A/灵感板A→发送原图。等实际AssetId回执，再到光镜查看并点“打开原网页”。

测试页当前已准备并读回：普通页、srcset页、原图失败页200，1600原图200，失败候选403。测试服务失效时手动运行Start-TestPages.cmd并保留窗口。srcset/失败页的80px声明槽在常见HiDPI下仍显示320候选，采集必须选1600/2400候选；2400候选故意403，不能取其可见缩略图。

加载后继续真实Chrome验收：重复发送到A、改送B（灵感板选不加入）、关闭/重开桌面后重试、目标失效后明确改选。`collect_chrome_receipts.py`只读导出company01中这三张合成页的真实完成记录，不碰其他网页/素材。只有实际Chrome入口及余项都通过，才签发Chrome最终闭环回执。

## 最小权限

必需contextMenus / activeTab / scripting / storage / nativeMessaging，无tabs/history/cookies/downloads/webRequest、无持久全站内容脚本。optional HTTP/HTTPS域名权限只在用户点击读取/重试本次原图时申请该图片源域名，网络读取credentials=omit。扩展manifest/key是稳定开发公钥，无私钥、密钥或遥测。当前Native Host注册文件由本机生成但尚未应用。

## 可交付文件

`E:\Kitao-Deliveries\chrome-capture-v1-20261110\Guangjing-ChromeCaptureV1-Diagnostic-win-x64.zip`，**112,967,892 bytes / 511 files**，SHA256 `B6907CE46E02EE4DE8C26D1D5659254C7709DF0694D61C0ADEAD24A2A0DAB491`。逐文件及ZIP读回通过。包中只有已知合成测试JPEG，运行数据库、原图缓存、测试日志及浏览器数据不入包。

根目录extension为可加载开发版；native-host为自包含桥接；app为全部模块完整桌面；test-pages为合成测试站点；BUILD_IDENTITY和PACKAGE_MANIFEST绑定最终源码。生成的本机config/manifest/.reg包含当前用户安装路径，移动包目录后需重新运行Prepare入口，不能用另一安装位置的旧注册路径。

用户Chrome入口未执行状态见ACCEPTANCE_STATUS.json，原始本地证据根 `E:\Kitao-Deliveries\chrome-capture-v1-20261110`。本轮可以交接开发版进行真实Chrome验收，不能称首版已全部验收或可发行。
