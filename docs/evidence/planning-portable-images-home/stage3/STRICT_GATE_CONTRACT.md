# 家用机审阅门禁

原verified_runner.py不修改严格定义：adapter discovery后逐FQN核对结果。完整Core1692项：1685Passed/7NotExecuted，strict FAILED；WPF1625项逐类隔离执行。任意timeout/nonzero/真实失败/必需漏跑阻断构建。

原分类既有条目完全一致，仅增加6个required_automatic：3项Core路径/清单，3项WPF真实搬迁/草稿/undo清单。原6项托盘测试保持必需。7项runner/review门禁自测通过。本机SDK10.0.401，dotnet位于C:/Program Files/dotnet；公司D盘发布包未使用。

审阅只能为REVIEW_BUILD_WITH_OPEN_GATES或BLOCKED；RAW/GPU/人工缺口不删除，不全绿。预发布通过后本机冻结源码构建win-x64自包含新目录，组件哈希比对后实际搬迁、表格/DOCX嵌图、启动偏好复验。

预发布结果：Core1685Passed/7NotExecuted，共1692；WPF1602Passed/23NotExecuted，共1625，211类无timeout/非零退出/漏结果/真实失败。strict均FAILED保留；原逐名审阅得REVIEW_BUILD_WITH_OPEN_GATES，errors=0，openGates=29，发布后Core身份1项待实测。
