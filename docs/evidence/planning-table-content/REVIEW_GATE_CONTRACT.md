# 审阅门禁合同

严格模式 tools/planning-r7/verified_runner.py 原逻辑不修改，所有NotExecuted继续导致FAILED。原TRX/summary不改状态。

另设review_gate.py，输入严格结果和review-classification.json。分类表为每个完整FQN一条，绝无通配跳过。默认清单中的必需自动测试不得未执行。29个已核查条件/人工测量缺口逐条写明原因；未知测试或新未执行项不自动继承。

实际Failed、超时、非零子进程退出、缺TRX、发现/结果不符、缺必需结果阻断审阅。批准标签仅REVIEW_BUILD_WITH_OPEN_GATES，不叫全绿或正式发布。

Release身份测试是required_automatic/postpublish：预发布阶段是明确待执行依赖，不是豁免；发布后设置PIXEL_TART_BATCH12_RELEASE_CORE指向实际隔离testhost中的已发布Core，必须通过。闭环审阅版已完成该门禁。最终版本代码变化后另建新发布目录及组件测试，不能复用旧身份。

分类中的manual_acceptance包括单独授权的性能测量/视觉审查会话，不等同将自动截图当成人工通过；所有这些原结果保持NotExecuted，Windows/物理DPI/WPS仍NOT_RUN。

运行器基础设施失败不会因该测试是条件类而被允许；test_review_gate.py验证未知测试、必需未执行、实际失败、超时、非零退出、缺TRX和结果数不符均阻断。
