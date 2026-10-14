# 重现与人工验收

## 自动回归
在 D:/AI AGENT/kitao-planning-rich-flow-r6-20261014：
```powershell
./tools/planning-rich-flow/Invoke-Regression.ps1 -Dotnet 'D:/AI AGENT/.dotnet/dotnet.exe' -OutputRoot artifacts/rich-flow/reproduced
```
内容生成器通过生产命令/服务工作；视图动作由WPF进程内绑定/处理路径及控件直接编辑触发。不是录制的连续鼠标操作。

## 公司 Release
```powershell
& 'D:/AI AGENT/.dotnet/dotnet.exe' publish src/RAWSelectionAssistant/RAWSelectionAssistant.csproj -c Release -r win-x64 --self-contained true -p:Platform=x64 -p:PublishSingleFile=false -p:InformationalVersion=2.3.0-planning-rich-flow+526490439dfc4cad26b98627f6b7cbbfd7290c8e -o artifacts/rich-flow/release/win-x64
```
将构建后的WPF testhost目录复制到独立 artifacts/rich-flow/release-tests/wpf，再只替换 RELEASE_IDENTITY 中7个产品DLL为发布目录真实文件；逐项比对SHA。使用 `dotnet vstest` 对此测试DLL运行 Invoke-Regression 同一 WPF filter。Core同理替换发布Core并执行同一Core filter。不要复制自包含runtime覆盖testhost，不启动产品EXE。

## 已知损失/限制
基础 DOCX 不是完整 Word/WPS 实现。复杂表格样式/宽度、继承样式、页眉页脚、多节、混合内联图片的位置等仍有支持范围。合成文档验证的是图片存在、基本正文/表格/镜头差异/恢复/撤销和5页预览；不等于完整往返排版保真。恢复点按导回前状态落盘，自动草稿与恢复点用途不同。

## Windows / WPS 人工清单 — 全部 NOT_RUN
1. 核对 RELEASE_IDENTITY 和整个发布目录哈希后，经允许手动启动。新建合成策划；双击表格编辑，检查器改另一字段；切镜头/切页/关窗前保存。
2. Enter多行动作、Esc取消单元格、焦点Tab；水平滚到最右验证左侧冻结状态；每组参考缩略图与镜头一致。
3. 900/1366/宽窗及物理100/125/150/175/200%DPI：抽屉阻断底层输入，关闭恢复焦点，长名称可查看完整内容，编辑器内/外滚动可到达。
4. 50/100/150/200缩放用真实鼠标点击文字位置编辑，再保存重开。自动测试仅验证Caret逻辑位置，不验证物理坐标命中。
5. 预览前后页、跳转、缩放；核对图/表/正文全部可见，检查原生焦点与页面显示。
6. WPS打开导出DOCX，改正文与镜头表，保存导回，查看损失报告、选字段确认、撤销、重开。自动OpenXML改包不能替代WPS。
7. 文件锁导致导回失败，确认原文件未替换、新草稿仍可见、恢复点能读；解锁重试与一步撤销。

当前未获以上实操证据，不得将此清单改为PASS。没有使用Computer Use/Browser/原型HTML执行或EXE后台启动替代结论。
