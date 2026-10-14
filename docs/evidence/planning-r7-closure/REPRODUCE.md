# 重现与结果判定

工作目录 D:/AI AGENT/kitao-planning-r7-closure-20261014。

1. 顺序构建tools/VerifiedTestRunner及测试项目（.NET10，Release，x64）。
2. 完整WPF：
```powershell
python tools/planning-r7/verified_runner.py --assembly "$PWD/tests/RAWSelectionAssistant.WpfTests/bin/x64/Release/net10.0-windows10.0.19041.0/RAWSelectionAssistant.WpfTests.dll" --output artifacts/closure/reproduce-wpf
```
3. 完整Core：
```powershell
python tools/planning-r7/verified_runner.py --assembly "$PWD/tests/RAWSelectionAssistant.Tests/bin/x64/Release/net10.0/RAWSelectionAssistant.Tests.dll" --output artifacts/closure/reproduce-core --single-process
```
4. 单类可使用--classes并给完整类名；不支持通配或包含匹配。每次输出新的run-时间-UUID目录。
5. `summary.json`的status和进程退出码都是门禁；NotExecuted即失败，不设allow-skips开关。

页合同原用例仍覆盖原DOCX。PlanningDocxAtomicReplacementTests提供确定性delete-sharing读句柄对照、锁定/只读/ACL拒绝和旧字节/临时清理断言。RuntimeCorrectionWpfTests保留完整画布关闭断言及新项目读回断言。

没有为测试创建生产数据，没有运行Computer Use、改变物理DPI或操作WPS。既有WPF类可能创建合成HWND，只属于自动测试，不记Windows人工实操。
