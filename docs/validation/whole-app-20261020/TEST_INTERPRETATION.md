# 测试计数解释

- 原始TRX保留Passed/Failed/NotExecuted。汇总状态仅使用PASS/FAIL/PARTIAL/NOT_RUN/BLOCKED。
- skipped=TRX NotExecuted（包括Assert.Inconclusive opt-in），notRun=适配器发现后未取得结果数量。两者不混并；必须同时报告。
- Core完整与WPF完整分别发现；多次专项复验不重复计入最终完整次数。
- 分片WPF采用适配器发现、按execution-unit取模分配；合并时校验(testId,FQN,displayName)多重集合，planned=discovered、结果missing/extra均显式。
- 模块相关测试可跨模块共享，MODULES的AutoRelated不是互斥计数，不可相加。121业务动作中的Automated=PARTIAL表示有模块级相关回归，未宣称该动作逐步执行通过。1121动作候选仍含动态/隐藏/条件分支，逐项BuildIncluded=PARTIAL，不能说1121全是当前可点按钮。
- 文档原型1项及研究Candidate25项不升级为产品/WPS/物理相纸通过。
- DPI合同17失败因当前截图manifest缺失；不复制历史文件刷绿，也不把74源码合同通过算物理DPI通过。
- 真实桌面只确认完整新EXE启动和模块身份/可访问性树。输入拒绝访问使操作矩阵BLOCKED，不靠自动RaiseEvent或离屏截图填人工列。
- 初版全Core1706/7未执行与WPF1682/7失败/20未执行原样保留。修复版完整结果另列，不能覆盖原失败。

派生CSV中的错误文本仅去除行尾空白以满足Git格式检查；原始TRX/stdout/stderr字节在RAW_TEST_EVIDENCE.zip中保留，不改写。
