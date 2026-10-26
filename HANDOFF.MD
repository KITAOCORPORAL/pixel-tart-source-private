# 光镜开发交接入口

## 家用 R5.3 阶段 A 与旧教程退役（2026-10-26 Asia/Shanghai）
- 实际仓库 N:/pixart/pixel-tart-source-private；独立工作树 N:/pixart/guangjing-r53-stage-a-20261026；分支 home/r53-stage-a-20261026。20旧树与211项未提交文件保全，不reset/clean/stash/rebase、不合main、不强推。
- 再fetch/ls-remote公司 company/whole-app-ui-copy-cleanup-20261026 = 48527fa0b7d3d063d5af5f6b62fa1a18cb36ef66，无后续提交；家庭旧产品均已继承。
- 设计包 C:/Users/Admin/Downloads/Compressed/光镜-UI-R5.3-定稿.zip，SHA256 16f75c0f40b506e4db57f140f389967264040cd8c5930ad24946b11c171e2ca9；实读解压目录 N:/pixart/Guangjing-R53-Home-20261026/design-reference。原型只作设计资料，不执行网页/不复制未授权相框。
- 暗室源码节点592ecccb：同一Editor/ColorAdjustmentStack双模式、串行节点/胶片条、稳定original/main变体、用户/系统锁、确认后归档/本会话撤销删除、已有工作文件900ms防抖保存及正常关窗等待、ActualWidth DIP断点、B画笔/\对比、宽窗440检查区/窄窗主动专注视图。阶段A PARTIAL：真实局部蒙版、通用混合/强度、2–4同步变体对比未完成；B–E本轮未开始。
- 追加产品：旧22步新手分片教程永久退役，新装/升级/中断旧进度不激活；删除自动/手动入口、遮罩/步骤提示、升级提示窗口和产品示例生成器。OnboardingService/TutorialDataService仅留不写盘兼容壳，旧字段/证明盐/路径/原数据不删除、不伪造“完成”。正常分片继续实际扫描/匹配/复制/导报。
- 旧品牌根因是教程/帮助/菜单/弹窗和文件元数据硬编码；用户可见文字、翻译值、安装器名称/发布者统一光镜。内部程序集、namespace、资源key、旧翻译key、迁移/数据库/安装兼容路径不改，已有用户水印/项目名称不批量替换。
- 全部功能完成且交互稳定后，集中制作覆盖光镜所有功能的产品引导与说明教程。
- 上述后置引导以届时实际交付功能为准，覆盖完整摄影师工作流、各模块操作及跨模块衔接。当前不开发替代教程。产品待办 docs/PRODUCT_BACKLOG.md。
- 当前用户全局规则禁止Computer Use/Browser/WebView/CDP；不重新启用。真实EXE仅做OS进程/模块身份启动与空工作区关闭核对；自动完整App夹具不等于鼠标保存重开/DPI。离屏WPF图不作实机截图。
- 完整发布/最终产品SHA、家用实测计数、ZIP/远端读回只以 docs/validation/home-r53-stage-a-20261026/FINAL_REPORT.md、TEST_SUMMARY.json、PUSH_READBACK.json 为准（生成后）；公司216/197仅历史。首版592ecccb已跑Core65PASS/2NOT_RUN、WPF62PASS/4NOT_RUN，后续教程退役须重新绑定最终发布DLL，不能沿用这些成绩。
- 统一交付根 N:/pixart/Guangjing-R53-Home-20261026；最新完整诊断包名称见最终报告。WholeAppDiagnosticBuild=true / PhotographerTrialBuild=false / win-x64自包含；不发行、不生成安装包。默认CMD不依赖脚本执行策略，不修改系统策略。
- 下一步先补阶段A批准渲染蒙版/混合与变体对比；像素行为改变遵守docs/color-science/open-source-first/POLICY.md，保留PV4/5/6及默认关闭研究。不得把R3原型175项算WPF完成、把.ptplan v1写成包含自由画布。


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
