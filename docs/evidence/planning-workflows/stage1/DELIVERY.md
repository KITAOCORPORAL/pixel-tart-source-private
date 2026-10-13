# Stage 1 — 摄影资源基础

基线fetch确认0766207a7e3bc4affc3c28374182ee7277bb54e9，独立N:/pixart/kitao-planning-workflows-20261013。原工作树与未提交资料不改。读了基线交付/保存合同、真实模型Store/VM/View/测试、Grok decisions r5条目、planning-center-spec分区和模式条目、原型HTML与coverage-planning；本地01_simple_home_1366.png已查看，仅参考，不是桌面批准。

15分区逐项：项目概览/文字/镜头清单/参考图/情绪板/文件/文档内容沿用真实功能；新增灯位图/服化道与道具/设备/人员/场地/当天时间表/预算强类型编辑；档期复用关联命令。每资源Guid、ShotIds、归档、复制、手动编辑、保存重开与进程内一步撤销。灯具保存归一坐标、角度、功率、修饰器、原图路径和可选旧画布RevisionId，点击放置/编辑，非拖动假效果。

资源放在原Document.Resources，Version=1；旧文档缺字段默认空，不迁移旧身份。未知扩展保留。保存继续原持久草稿和失败门禁；资源输入先验证，不合法留编辑对话框。撤销快照按ProjectId隔离，不保存跨重开undo栈。

预算只读FinanceService.QueryAsync按ProjectId+Expense读取，以交易ID去重，保留币种与付款状态；无账本写调用。设备源审计未找到工具箱设备目录/库存模型，因此真实工具箱导入 BLOCKED_MISSING_SOURCE；有强类型导入接口、来源目录+ID去重及合成测试，不显示伪导入按钮。地图/日落/潮汐仅日期时区/地址/手动值/来源/OfflineManual，不编造自动结果。

布局限本模块：纵向15分区、窄窗收起策划列表并保留策划案选择菜单；不是r5像素级复刻。右侧常驻检查器、四组视觉、拖拽宽度持久化仍待完善；VISUAL/DPI/桌面NOT_RUN。灯位引用保留已有快照，不声称新增自由画布生成器。

测试：stage1-wpf.trx 11通过0失败0跳过，包括七类型重开、复制/归档/撤销、来源去重、账本只读筛选、非法坐标和IO草稿恢复。既有P0镜头失败恢复继续执行。没有新第三方资产/照片/生产库；不改Color Studio数学。
