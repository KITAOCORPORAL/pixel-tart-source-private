# R7 参考与静态视觉审查

本地Grok资料：D:/AI AGENT/光镜设计交付-3of4/guangjing-master，文件哈希见REFERENCE_IDENTITY。已读取D-PL-11纸张统一、CR-PL-17焦点/正文范围依赖、CR-PL-14原生合并、CR-PL-15/18共用分页器及D-PL-26首页独立内容。

|参考|R7产物|观察/差异|
|---|---|---|
|11_find_replace_1366.png|offscreen/find-narrow.png|有查找/替换两页签、选项与前后/单次/全部；为内嵌面板，不是中央modal；范围明确不含结构字段；未显示假“包含结构”入口|
|11_find_replace_1920.png|offscreen/find-wide.png|长标题和大文档保留；字号间距/面板尺寸并非逐像素复刻|
|09_hf_dlg_1366.png|offscreen/page-settings.png|基础首页内容/页码起始/取消应用可用；没有左右对齐、token胶囊和奇偶页假入口；检查器页面入口真实接线|
|08_adv_pages_hf_1920.png|offscreen/page-1.png、page-2.png、page-3.png|首页独立，后续默认，5页；页码3起，页脚未盖正文；无页缩略侧栏/全Word七页签|
|12_repeat_header_1366.png|PlanningR7PageExtrasTests/XPS|以真实表格行分页验证，不用原型/截图替代；纵向合并续表明确拒绝|

修复审查发现的页签白底浅字和面板叠放；最终静态截图已查看。r5对照拼图在本地artifacts/r7/comparisons，原型PNG未复制提交。不同尺寸/内容/主壳范围不计算虚假相似度。
所有图为合成内容96DPI WPF离屏，不是Windows/WPS实操截图。视觉状态PARTIAL_OFFSCREEN_REVIEW；R6_R5_DIFFERENCES_PRESERVED.csv中的OPEN不因本轮局部实现自动关闭。
