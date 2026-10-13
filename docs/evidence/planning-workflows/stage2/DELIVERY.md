# Stage 2 — 同文档模式与引用

Stage1远端 b7d1de7e15d1ecaef0ce612d5d3ca2098a3789cc 已正常推送后开始本阶段。

实现：Document.Presentation Version1保存模式、初始化标记与稳定BlockId/Kind/SourceIds。Simple/Advanced共用原ProjectId/DocumentId/ShotId。切换先Flush当前表格与RTF；失败留旧模式。首次生成镜头/资源引用块，重复切换不再生成。块读取同一Shot/Resource，镜头名称/动作编辑以明确应用按钮提交；资源通过相同编辑器。移除块不删原对象。一步策划撤销快照包含Document和Shots。

原RTF+RichLayout仍是普通段落/表格/图片的唯一存储，不转换成纯文本。简单版文档内容可读取相同RTF，旧CustomBody保留。进阶版复用真实基础富文本编辑器（标题、格式、表格、图片、分页、已有受限导入导出）并加保存/撤销/刷新结构工具带。

限制：不是完整Word；不是r5七页签完整Ribbon，普通内容和结构块当前分区显示，尚未实现任意穿插分页/块拖拽。模式有每文档记忆，尚无新文档全局默认设置。结构字段当前直接编辑名称/动作，其他镜头字段通过简单版同ID修改。撤销栈限当前进程，不声称持久版本历史。未执行桌面、DPI、视觉或WPS。

测试 stage2-wpf.trx：15通过0失败0跳过，含同身份、多次切换不重复、结构同步/一步撤销、删块不删镜头、保存重开与非法镜头阻止切换；原富文本内容/布局撤销回归继续运行。未修改Color Studio及其他页面布局。
