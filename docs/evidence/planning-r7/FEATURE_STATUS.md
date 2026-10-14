# R7 功能状态与剩余工作

整体：PARTIAL；不是全部完成，不是Windows/WPS验收通过。

|单元|状态|已实现与证据|未关闭|
|---|---|---|---|
|A4页面合同|IMPLEMENTED_TESTED|A4、Letter、A3横向实际尺寸/比例边距/提示；导出OpenXML验证|分节仍转换单节|
|普通正文查找替换|IMPLEMENTED_SCOPED|跨Run、中文、大小写、全词、前后处、单次/全部、一次撤销、零命中|摄影字段复合批量替换未实现；当前范围有明确UI说明|
|焦点路由|IMPLEMENTED_NOT_NATIVE_VERIFIED|正文与列表路径分开；Esc先关编辑器面板|Windows键盘/IME/焦点实操NOT_RUN|
|矩形合并拆分|IMPLEMENTED_SCOPED|网格校验、顺序、RTF重载、gridSpan/vMerge、失败不替换目标|复杂不规则选区拒绝；拆分内容留左上；无Excel|
|重复表头|PARTIAL|Layout持久化、DOCX tblHeader、实际续表行、分页/XPS/PDF共用|纵向合并续表/超高单行拒绝；强制表格起页；未完成通用Word分页|
|表内图片/列表/嵌套表格|REJECTED_SCOPED|明确拒绝安全不支持项，保留原件|未实现全部表内内容往返|
|页眉页脚页码|IMPLEMENTED_SCOPED|模型、设置/取消/撤销、保存失败重试、重开、原生域、首页不同|奇偶页、左右位置、任意token、复杂分节OPEN|
|Grok UI|PARTIAL_OFFSCREEN|沿用现有四组真实工具、页眉入口进入插入及检查器页面组；宽窄图|使用内嵌面板而非Grok中央modal；未做全部图示/胶囊/布局复刻|
|完整Core|见TEST_SUMMARY|冻结源码全量执行，条件项列出|实际RAW/性能/GPU条件项未冒充执行|
|完整WPF|见TEST_SUMMARY|全部发现fixture隔离执行，保留失败和超时|基线已有画布剪贴板/测试DB失败，不能宣称全绿|
|公司Release|见RELEASE_IDENTITY|win-x64自包含，发布组件哈希核对|EXE实机启动NOT_RUN|
|Windows/物理DPI/WPS|NOT_RUN|仅合成WPF/OpenXML/XPS/PDF自动验证|需另行人工实操|

## 底层限制证据
1. 原RTF没有摄影结构字段的稳定内联范围身份。Grok明确CR-PL-17依赖CR-PL-04复合撤销，当前仅外置结构引用；不能把正文字符串匹配机械写回镜头。为避免数据覆盖，本轮保持范围隔离。后续需同一历史栈的正文/镜头前后像事务。
2. 原FlowDocument表头不会自动重复。本轮实现按同一WPF测量器预切表格；跨页RowSpan无法安全重建连续合并状态，明确拒绝而不画假表头。原失败“chunks=2但PageCount=1”已保留，修复显式段落分页后验证一chunk一page。
3. 旧基线两个RuntimeCorrectionWpfTests失败复现，不涉及Color Studio计算。未通过修改PV4/PV5/PV6或历史数学来换测试通过。
