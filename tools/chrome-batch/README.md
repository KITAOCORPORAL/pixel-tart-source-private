# 批量采集验收工具

`chrome-capture-batch-tests/test.mjs` 验证独立 DOM、IndexedDB 与队列引擎；它不操作真实 Chrome。运行：`node tools/chrome-capture-batch-tests/test.mjs <新结果文件.json>`。单图共用选择及严格回执校验：`node tools/chrome-capture/source-tests.mjs`。

`native-integration.mjs <新配置.json>` 使用真实 HTTP 原图、已发布 Native Host、全模块桌面及 SQLite；Chrome API 传输和 IndexedDB 为独立测试适配器。重放前必须先用 ChromeCapture.Acceptance 的 seed 模式创建**全新** batch-machine* 合成测试根目录，在独立诊断包运行 Native Host --prepare <包路径> <测试根目录>，再启动对应完整桌面。配置需包含该 seedFile、nativeExe、extensionId 和新的 output；其余输入字段见工具代码。工具会拒绝已被重新准备到 company01 的共享包，避免向旧 Chrome 队列的实际测试库重放机器数据。已有配置及输出只作历史证据，不原样重跑。

`archive_machine_target.py` 只归档 seed 指定的 batch-machine* 根目录中的“待失效测试项目”，并检查目标名和未归档状态；不删除项目。`restart_machine.py` 读取专用包的本机管道与数据根目录启动桌面，使用前应确认当前没有同根目录桌面实例。仅在机器验收中使用这些工具，原生 Chrome 轮次保留用户队列与身份。

将 Allow-403-Retry.cmd 与 Reset-403-Test.cmd 复制到含 test-pages/FIXTURE_IDENTITY.json 的诊断包根目录。它们只改合成测试页的 originalAllowed 开关；前者允许 403 项再次读取，后者恢复首次失败条件。开关不触碰浏览器或桌面队列。真实 Chrome 第一次部分失败后运行 Allow-403-Retry.cmd，再点击扩展“重试失败”。

恢复时修正已知宽度或高度低于筛选阈值、另一维度未知时被“保留尺寸未知”放过的问题；已知不达标的维度优先排除，未知维度只影响其自身。该检查覆盖窄图、矮图及允许/排除未知的边界。
