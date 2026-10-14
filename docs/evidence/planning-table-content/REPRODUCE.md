# 复现、审阅门禁与发布顺序

工作树 D:/AI AGENT/kitao-planning-table-content-20261014。SDK D:/AI AGENT/.dotnet/dotnet.exe。

1. 顺序构建VerifiedTestRunner、Core测试、WPF测试；不要同时改写正在运行的testhost目录。完整运行用verified_runner.py，原strict summary一直保留FAILED/NotExecuted语义。
2. 每个新增测试的完整FQN人工登记到review-classification.json，未登记/新未执行项不能自动豁免。
3. 汇总完整Core和WPF summary执行review_gate.py --summary ... --policy ... --output ...。漏跑、额外、基础设施错误、实际失败和必需NotExecuted均BLOCKED。只允许逐项已核查条件缺口。
4. 预发布状态REVIEW_BUILD_WITH_OPEN_GATES后，使用冻结产品源码SHA发布win-x64自包含目录。不要覆盖closure-review或旧R7目录。
5. 将测试host复制到独立目录，替换7个产品DLL为真实发布组件；逐个比较SHA256。不要把整套自包含运行时复制覆盖测试host。
6. 设置PIXEL_TART_BATCH12_RELEASE_CORE为实际组件测试目录的RAWSelectionAssistant.Core.dll，执行Batch12ReleasedAssemblyTests；不能继续用“未发布”作为循环跳过理由。
7. 发布组件执行原DOCX覆盖、共享读者/锁/只读/ACL、RuntimeCorrectionWpfTests、R6连续流程、R7页合同/查找/表格/页眉分页、PlanningTableContentTests。
8. 将实际发布身份结果与完整结果共同交给review_gate.py --postpublish；它要求真实身份Passed，不修改原预发布NotExecuted。

示例：
```powershell
python tools/planning-r7/verified_runner.py --assembly <测试DLL绝对路径> --output <新结果根目录>
python tools/planning-r7/review_gate.py --summary <Core-summary> <WPF-summary> --policy tools/planning-r7/review-classification.json --output <prepublish.json>
```

设置KITAO_TABLE_EVIDENCE为独立输出目录可生成24行摄影表格PNG、原生DOCX、XPS、图像PDF；测试每次建立Guid子目录。KITAO_RICH_FLOW_EVIDENCE沿用R6合成流程。所有图片为自绘几何参考，不是客户图。

Windows实机、物理DPI、WPS实操、真实打印机NOT_RUN；XPS不是打印机验收，图像PDF不是可编辑文本PDF。未调用产品EXE启动或Computer Use。
