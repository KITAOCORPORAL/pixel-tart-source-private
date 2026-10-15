# 失败轮次、修复及未关闭项

1. 初始合并8c3986e完整Core/WPF无实际Failed，NotExecuted按strict FAILED保留；实际合并Release组件另外测试。A7/B8均在同一个testhost真实发现/各一次Passed，不相加两线旧结果。
2. 性能：原400ms阈值未改。10,128项ViewModel诊断出现warm483/467/472ms；不是简单归因并行。stack/trace定位到UI线程集合大小完整分页与重复global collection counts；SQL聚合+范围计数复用后warm约29ms。Core SUM与paged oracle、WPF数量/排序/普通刷新验证通过。旧673.8/2499.7ms仓储峰值当时无分段，仍未完全归因。
3. 性能新WPF测试前两轮分别11/21 query，失败揭示遗漏的RefreshCollectionSizeAsync；保留。第三轮因测试oracle120对真实500数量不等失败，修正为同样PageSize500，保留数量/排序断言。第四轮34项Passed。
4. 性能修复后完整WPF真实失败：RuntimeCorrectionWpfTests.CanvasMenusExecuteClipboardAndShareZoomStateAtAllViewportSizes，paste.IsEnabled。审阅门禁BLOCKED，pending publish未执行，不能用前轮合并结果批准。
5. popup根因：测试从全局PresentationSource枚举“最后一个”ContextMenu；关闭的旧popup HWND可能尚未卸载，返回旧禁用菜单。生产明确保留按钮当前ContextMenu、关闭previous；测试用exact owner/menu身份，断言first disabled、Copy后pure CanPaste、second为新实例且启用、paste后2对象、150%缩放共享全部保留，finally关菜单/窗口。10项相关测试通过后再次完整重跑。
6. ICC：初次skcms compile把头文件当C源导致-std错误，保留；正确只编译skcms.cc+TransformBaseline.cc。初始profile时间戳导致hash变化，固定生成日期及profileID后两次hash完全一致。Argyllv4真实拒绝；skcms无M矩阵LUT约半值、感知/饱和与LCMS差异保留，不改输出乘2“修复”。损坏profileLCMS宽容接受，skcms拒绝，因此不批准产品输入。
7. 真实大图：同Canon完整full/tile Structure输出float字节hash相同，但tile更慢/峰值更高，故不改生产节点。Details full此环境可执行，不把历史BUDGET_REJECTED涂成普遍通过或伪造本轮拒绝。自然3秒未证明OS回落，原生allocator归属UNAVAILABLE。
8. 实机：共享Chrome前台且近期输入，按不争抢规则未发送输入或启动产品；产品导航/拖动/DPI/WPS NOT_RUN。不是权限拒绝，不虚构权限错误。
9. 未证明项：A7M4绝对色准、宝丽来相纸精确复刻、任意真实ICC全支持、PV7实际产品接入、真实RAW黑位前/矩阵前、全部五节点大图oracle/内存优化。CalibrationEligible=false。

原始失败TRX、stdout/stderr、runsettings及strict summary均归档；新失败会阻断最终审阅，不无限重跑挑通过。
