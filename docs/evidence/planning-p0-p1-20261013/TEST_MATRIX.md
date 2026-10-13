# 测试矩阵

|检查|通过|失败|跳过|说明|
|---|---:|---:|---:|---|
|Core首次全量|1681|1|7|正文Package.Body同步回归失败，保留记录|
|Core修复后全量|1682|0|7|后续导航门禁/原字节备份由最终发布回归覆盖|
|WPF首次全量|1555|0|23|中间源码状态；不冒称最终二进制|
|最终Core专项|23|0|0|规划/镜头/文档回归|
|最终WPF专项|13|0|0|规划行事务/失败恢复/原界面合同|
|实际发布Core首次|1682|1|6|旧静态测试限定导航单行语法，适配保存门禁|
|实际发布Core最终|1683|0|6|位置门禁开启；最终发布Core哈希一致|
|实际发布WPF最终|1556|0|23|最终发布Main/Core/模块DLL哈希一致|
|Release构建|PASS|0|—|win-x64自包含，不启动EXE|
|桌面启动/导航/本批点击验收|—|—|NOT_RUN|现有禁用Computer Use及原生API不可用；未虚构前台错误|
|物理DPI/视觉/WPS|—|—|NOT_RUN|不能由数值/状态/对象测试提升为实机批准|

跳过数量按TRX具体NotExecuted结果计，非Counters.notExecuted属性。方法名、失败名、时间、原始TRX哈希在TEST_SUMMARY。原始日志在ignored artifacts/planning-*.log；原始TRX在artifacts/planning-tests。

专项重建曾因本轮全量testhost占用DLL产生MSB3021/3027失败；保留planning-build-file-lock-failed.log，等全量结束后复跑成功。未终止其他任务进程。

最终产品源码28642bc；后续仅修改NavigationSafety204Tests的旧语法断言及交付文档。完整WPF最终测试包含3个新增Foundation测试，比中间全量多1项。发布组件实际位置/哈希验证不等于桌面EXE启动验证。
