# 对照调度合同

效果：输入SHA/DecodeKey + target ID + 源version + activation revision + token验证，prepare只生成效果bitmap。提交仍一次创建版本/历史，结果显现后约200ms分析完成可继续操作。非比较场景不分配第二张完整预览，不主动启动CPU任务。

对照：用户点按钮/选原片/左右/并排模式（或按住原片比较）触发；使用Original()原语义，不改变第二曝光、布局和保留纸面。对照未就绪时SourceImage=null，视图fallback当前MatchedImage并明确状态，没有旧版本/source bitmap占位。效果bitmap可离屏render、实际调度/命令验证；不声称物理鼠标。

任务：ComparisonWork单任务；启动先Task.Yield保证可追踪句柄，CTS + comparison revision + activation revision + target/version/definition/source hash全匹配才允许发布。完成只更新SourceImage/SourceThumbnail/缓存，绝不写RawDefinition/EditVersions/undo，不改MatchedImage。

取消/优先级：应用、版本变更、切图、项目打开和导出都Cancel + await ComparisonWork再进入重活，关闭/隐藏取消，可由生命周期await追踪完成。未释放前状态仍为正在取消，无第二并发对照。全局HeavyWork保持1槽，内存预算和映射全像素计费不改。每tile/行原有token响应保留。无自动后台计算，不会与所有新操作竞争；明确这是按需异步而非自动预取。

缓存：SHA、DecodeKey、完整Original definition、方向/尺寸/quality/边长；仅旧合同中Original lineage等价规则，不忽略第二源weight/crop/zoom。当前版本不新加第一源对照选项，原片下拉在新管线标注为配方基准。

失败：对照失败独立提示，效果/历史保持；可重试。用户取消不会把最新工作区恢复成旧状态。页面unload会取消；不扫描目录或后台自动保存。调用切换/导出前等待旧对照释放的时间单独记为调度取消延迟，不冒充像素加速。
