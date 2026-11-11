# Chrome 当前页批量采集恢复交付

状态：**PARTIAL — 功能源码、全模块诊断包、自动检查及原生机器证据已备齐；真实 Chrome 批量交互与新版右键入口仍待验收。** 本次接续的是原会话最后明确的 Eagle 式批量采集任务。

单图验收基线 0eda778636a4c2359eefd4611450dcf180f863c1，单图冻结产品 ed5d15d451c5f92dc8ff99d9a4ff91af3e76289c。批量功能提交 69bcf65a5c8fd4525470d65d57bba372c69b5c5a；本轮修复与验收工具提交 1698664d12913aadea64bad6f2a68cb52d9ee38d。独立分支 feature/chrome-batch-sidebar-v1-20261111。

本轮修正：已知宽度或高度低于阈值时，即使另一维度未知并勾选保留未知，也不能绕过尺寸筛选。新增真实边界回归。补齐上轮中断时的原生测试适配器与辅助工具，并为机器重放增加运行根目录检查，拒绝将已经准备为 company01 的宿主拿去执行 batch-machine 测试。增加合成 403 恢复/重置入口。

| 验证 | 结果与范围 |
|---|---|
| 批量 DOM / IndexedDB / engine | 14/14 PASS，含尺寸边界、迁移、串行、同 ID 重试、取消、回执丢失、恢复、失效目的地和 64MiB 预算；不是 Chrome 操作 |
| 共用单图源选择与回执断言 | 9/9 PASS；新版实际右键点击仍待执行 |
| 完整桌面与 Native Host | 1698664d 两项 Release、自包含 win-x64 发布退出码 0；WholeAppDiagnosticBuild=true、PhotographerTrialBuild=false；没有生成安装器 |
| 含空格路径准备脚本 | R2 私有副本真实路径运行成功退出 0，故意缺身份文件失败退出非 0；保留 dataRoot 与 pipeName，测试未写注册表 |
| 原生机器证据 | 原 69bcf65a 引擎通过真实 HTTP、已发布 Native Host、全模块桌面和 SQLite 进行混合 8 项、403 同 ID 重试、取消、离线暂停、重开恢复和失效目的地重选；最终 7 个素材、9 条项目关联。Chrome 传输和 IndexedDB 是适配器，不能当真实浏览器验收 |
| 原生证据适用边界 | 69bcf65a 到 1698664d 的 Native Host、Contracts、App.BrowserCapture、BrowserCaptureJournal 源码差异为空。旧机器证据保留原 SourceSHA；本轮改动的筛选由新增检查验证，没有声称重新进行整个原生机器轮次 |
| 当前旧单图回执 | 实际只读查询仍为 Completed；不存在的 CaptureId 返回 CaptureNotFound，没有发起导入。R2 宿主也能读取同一已保存回执 |
| 数据保留 | company01 的项目、BrowserCaptures、AssetItems、ProjectAssetLinks 以及三张灵感表共 7 表与升级前备份逐行逐列相同：5 个历史采集记录、3 个素材、4 条项目关联。独立 batch-machine01 的 7 个托管原图重新计算 SHA-256，全部匹配 |
| 参考与许可证 | 固定 Image Harvest c87e23059ac153c7bf166ad677182b3ac5afb4d0 的 6 个 Git blob；已补充 Git 规范字节 SHA-256 与 Windows CRLF 检出 SHA-256 的区别。许可全文随扩展分发 |
| 实际 Chrome 与全产品发行 | 本轮未执行新 Chrome 轮次。Computer Use 无法可靠识别当前浏览器 URL，工具安全检查停止界面控制。当前保持诊断预览，不能发行 |

可运行包：E:\Kitao-Deliveries\chrome-capture-batch-v1-20261111\Guangjing-ChromeBatchV1-Diagnostic-win-x64-r2。采集桌面入口 Start-Guangjing-Capture.cmd，普通全模块诊断入口 Start-Guangjing-Diagnostic.cmd。两个入口的数据根目录用途不同，浏览器回执验收使用采集入口。现有加载目录的扩展源码已更新，稳定扩展 ID 保留，Chrome profile/IndexedDB 未读取或删除。旧扩展源码、本机注册值与合成开关保存于 before-resume；旧包、旧队列及原始回执原位保留。

新合成站点位于 http://127.0.0.1:54394；服务器已切换为 R2 包并验证页面 200、可恢复原图 200/403 开关。当前留在首次失败 403 状态。登记环境时旧桌面 69bcf65a 仍在运行，真实轮次第一步通过正常窗口关闭并用 R2 入口重开。具体简短操作见 REAL_CHROME_SHORT_ROUND.md，机器可读断点见 receipts/handoff-readiness.json 与交付目录 BATCH_HANDOFF_CONTEXT.json。

所有已归档 JSON 原样保存在 receipts，字节哈希与源版本见 EVIDENCE_INDEX.json；.gitattributes 关闭其换行转换。附带本轮构建、测试、只读核对与包冻结记录，既有原生证据没有改写为新版本通过。
