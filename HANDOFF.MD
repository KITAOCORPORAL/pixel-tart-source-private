# 光镜开发交接入口

## 有效基线与本轮
- 当前公司分支：company/whole-app-ui-copy-cleanup-20261026。源起点home/planning-owned-canvas-20261025@a2c3fd00783b2091ecc7dc3a86e9df52bb7a71ac，已包含自有画布修复8886226d；公司dad3dd4c到起点0落后/4前进。
- 本轮目标：完整主程序可达页面文案/布局减负，保留操作、可访问名称、AutomationId、保存失败、外链/项目副本和包排除依赖。纯文案不引依赖，ColorStudio布局/RAW/ICC/PV4/5/6不改。
- 本轮状态：PARTIAL，产品已交付；Core216/216、WPF197PASS/0FAIL/3NOT_RUN，原生工具阻断，产品/验证SHA和最终诊断入口以本轮简短FINAL_REPORT为准，未执行原生不升级PASS。

## 规则和踩坑
- 旧工作树/未提交/未推送成果只读保留；独立分支开发，不reset/clean/stash/rebase旧树，不合main、不强推。不把所有分支当集成。
- 完整诊断必须WholeAppDiagnosticBuild=true、PhotographerTrialBuild=false；统一隔离启动，ZIP非Setup，构建通过不等发行。
- 已有项目自有参考/画布必须保留身份、根路径、SHA验证与禁止原外链/preview fallback；.ptplan v1不含自由画布工作区，不能写画布跨机通过。
- 画布Editor.Changed曾每次布局变更全量重新校验解码；本轮先测再改，布局合并与显式重载校验分离，不扩展大型缓存或修改解码器。
- 策划搜索框曾Margin162避让四常驻包按钮，本轮改真行布局；独立根操作保留更多菜单及AutomationId，取消仅任务期间。
- 原生工具按本轮真实可用清单选；当前Windows使用@oai/sky via node_repl，不照抄家用unsupported mcp__cua_repljs。一次明确阻点后停止空跑，VM重建不等新进程。
- WPF以夹具独立进程；符号链接权限失败保留原结果，不改策略。历史家用Scenario11 source4f57ca56、10000次1292.9921ms是历史PASS，不是本轮性能，不重复测。

## 既有权威资料
- AGENTS.md：像素节点门禁；本轮不触及。
- docs/PIXEL_TART_HOME_DEV_HANDOFF.md：旧跨机环境约定，时间敏感命令须现场复核。
- docs/validation/home-planning-owned-canvas-20261025/FINAL_REPORT.md、NONPASS.json、DESKTOP_STATUS.json：本轮继承的产品/未验边界。
- docs/validation/planning-bounded-thumbnails-20261023/FINAL_REPORT.md：有界预览与缓存安全合同。
- docs/implementation-reports/WHOLE_APP_PRODUCT_LOGIC_AUDIT.md：旧全软件逻辑清单，非当前验收成绩。
- docs/validation/whole-app-ui-copy-cleanup-20261026/FINAL_REPORT.md：本轮最终简报（交付时创建/更新）。不复制历史报告制造第二套最终状态。

## 本轮已复现
- 200个布局/缩放事件、12自有图触发248次解码；80ms合并后36次（含初始和显式完整性复验），通用provider0调用。显式LoadPreviewsAsync仍逐次核对，布局不会按每事件重新解码。
- 窄窗侧栏原设计会折入抽屉，布局测试须验证隐藏列而非要求隐藏搜索框有宽度。
- 普通当前根导入失败需保留“项目ID冲突”可行动原因，技术详情进日志，不能统一抹成未知错误。

## 本轮交付读法（2026-10-26 America/New_York）
- 产品代码SHA b9cb1e25e811903b117aef3afeb203aadf7d0790；测试合同修订SHA0505216f8aacbcc67ea10f7584a8469767afe923（src零差异）。后续证据SHA/最终分支HEAD见最终报告与PUSH_READBACK，不把文档HEAD当产品源码。
- 完整主程序：E:/Kitao-Deliveries/whole-app-ui-copy-cleanup-20261026/Guangjing-UI-Cleanup-Final-Diagnostic-win-x64；隔离Start-Guangjing-Diagnostic.bat。不是Setup。
- 原生一次点击coordinate input geometry is unavailable，JPEG/画布保存退出新进程/A-B整链均NOT_RUN。内验图片是离屏合成，不是桌面实机证据。
- 先前静态教程逐字断言4项已改成真实命令/状态绑定，安全导出/不覆盖原件回归保留；源副本错误短态+展开详情/日志，不放宽禁止回退。
