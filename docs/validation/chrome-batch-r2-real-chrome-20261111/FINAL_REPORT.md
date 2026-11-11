# R2 真实 Chrome 采集验收收口

状态：**PASS / READY（本轮 Chrome 采集范围）**。完成日期：2026 年 11 月 11 日（Europe/Berlin）。产品仍是全模块诊断预览，本报告不作全产品发行结论。

冻结产品 / 功能 SHA：`1698664d12913aadea64bad6f2a68cb52d9ee38d`。起点为 `feature/chrome-batch-sidebar-v1-20261111@2e034c4b577276210a3c978e72fc798ffe1b5f70`。本轮没有修改产品源码；桌面、Native Host 与扩展关键文件逐项匹配冻结哈希。

真实环境：Chrome 156.0.8078.81，扩展 0.2.0 / `obgahfdabcpkohkdcnidpmeooceomogk`，后半程使用用户指定的 agent-desktop 1.2.0。桌面正常重开由旧 PID 9476 切换至 34644，使用相同 company01 数据根目录、管道及完整诊断包。

| 验收段 | 实际观察与独立核验 |
|---|---|
| 扫描、筛选与勾选 | mixed.html 明确扫描得到 8 项；扫描前先记录网页自身加载基线，之后扫描、筛选与勾选增加请求为 0。PNG、JPEG 最小宽 1000 排除未知、JPEG 最小宽 2000/高500 保留未知，数量分别 1/4/1。全选/清空8→0，单张1→0。 |
| 批量与失败重试 | 第一次 7 成功、1 HTTP403；实际原生结果为4新入库、3复用。普通图和异URL同字节图指向同一AssetId。恢复合成原图后仅新增1次原图GET和1条实际回执，失败项CaptureId保持 `8088116f-86de-4fdf-a055-3fe201dd91f9`，原7条成功记录未重发或改写，最终8/0/0。 |
| 取消与重开继续 | 第一次尝试6项均完成，没有取消生效证据，保留原批次和6条回执，不算通过。第二次将合成原图临时延时8秒，仅启动1份原图读取、后5项未启动，新增原生提交为0；实际队列6项均cancelledSettled/cancelled。保存6项身份，保留当前未保存Color Studio工作文件并正常退出桌面；同包同根目录重开、在同一Chrome配置的新窗口重建侧栏后，显示原6项已取消。点击继续后6项全部完成，实际6个CaptureId逐一等于关闭前用户导出，原19条回执未改写。首项复用已持久化原图，恢复只读取其余5份。 |
| 新版右键与旧历史 | 在ordinary.html实际图片上右键，使用“发送到光镜”，在新版单图编辑器选择项目B、不加入灵感板，收到 `1fb518ee-7f63-4653-b447-b6ddf3bb2a8d` 的实际回执。项目B关联存在，原图SHA256正确，复用原AssetId。单图历史可见原5条完成记录和本轮新增1条；实际打开唯一的旧“桌面关闭期间新图”记录，Chrome渲染旧回执与Native只读回执相同。 |

取消成功发生在桌面提交之前；本轮没有把“取消后丢失已提交响应”或“所有浏览器视图及service worker均终止”登记成真实Chrome已覆盖项。相关独立引擎/原生机器检查仍保留原证据来源，不替代本轮真实操作。

本轮修正的是部署断点：Chrome实际从 `E:\Kitao-Deliveries\chrome-capture-v1-20261110\Guangjing-ChromeCaptureV1-Diagnostic-win-x64\extension` 加载旧0.1.0，先前交接记录误指11.10目录。已按用户详情页确认的实际目录原位升级，公钥、扩展ID保持不变，升级前源码已备份。之后真实Chrome显示0.2.0，实际加载目录16个文件与冻结包一致。

当前素材库21项，其中包含用户验收期间另外加入的内容。重开前按当前状态做一致性本地备份，重开后7张相关表逐行逐列一致；最终21项素材ID、内容哈希、托管路径均保留。旧5条原生记录未改写；最终26条属于已列明的合成采集ID：旧单图5 + 混合批量8 + 未证明取消的首次尝试6 + 取消后恢复6 + 新单图1。没有清空素材库或队列，没有直接访问Chrome配置/IndexedDB文件。用户提供的两个当前批次只读导出只含本轮身份元数据。

正常退出曾等待“保存 Color Studio 工作文件”对话框。agent-desktop识别并完成独立另存，文件及数据库快照只保留在本地交付目录，不进仓库或可分发证据包。临时8秒测试输入已恢复到原2.5秒，原555个静态包文件在更新本轮交付说明前全部通过原始哈希核对。

最终回执见 [FINAL_RECEIPT.json](FINAL_RECEIPT.json)，精确字节索引见 [EVIDENCE_INDEX.json](EVIDENCE_INDEX.json)。receipts与screenshots分别保留用户导出、HTTP审计、已发布Native Host回执、SQLite/托管原图读回及实际界面截图的各自来源。现有14项独立批量检查、9项共用源选择/回执检查及含空格路径准备脚本通过记录见上一轮证据索引，未将它们重新标成真实Chrome操作。

可运行目录：`E:\Kitao-Deliveries\chrome-capture-batch-v1-20261111\Guangjing-ChromeBatchV1-Diagnostic-win-x64-r2`。浏览器采集桌面入口 `Start-Guangjing-Capture.cmd`；普通独立诊断入口 `Start-Guangjing-Diagnostic.cmd`。本机采集继续使用现有扩展目录和company01根目录。当前R2桌面与合成测试站点保持运行，可直接查看已完成结果。包内 `ACCEPTANCE_R2_REAL_CHROME.json` 是本轮完成状态，`BUILD_IDENTITY.json`保留构建时的原始待验收记录。没有生成安装器。
