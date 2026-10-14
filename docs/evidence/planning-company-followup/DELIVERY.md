# 公司策划中心续接交付

日期2026-10-14（Europe/Berlin）。实际工作树D:/AI AGENT/kitao-planning-company-20261014；工作分支feature/kitao-planning-company-20261014。
原入口kitao-integration-20261013确认为同一仓库链接工作树，remote指向KITAOCORPORAL/pixel-tart-source-private。fetch后r5远端23473ac3b470b38b04781ee687b830bb831c7d86未前进；本批从该HEAD建立，未用旧集成HEAD覆盖新UI。
发布源码5db9394f10c435039aada709ca60f34303cbe2d8。

|单元|审查提交|结果与边界|
|---|---|---|
|镜头参考/状态/滚动|a2f05b34cb1dc6f08a68f04bbb9f407995792623|真实关联缩略列；镜号+状态左冻结，横向滚动常驻；非原图最右冻结|
|检查器/灯位|4874a84ee3bd47691cd04b6a3db3553e5e7a8f5c|分组拖动/键盘/按钮排序持久化；灯具拖动/吸附/方向线/取消/一次保存+旧撤销；插入线视觉/真实指针未验|
|进阶缩放/分页|3119b5f30a4d89748df007fa59f88318aa3a4389|真实编辑器缩放50–200与预览paginator导航/跳转/页数，按文档保存；不是完整Word页缩略侧栏|
|实际加载/失败态|93d79111792f51508190fe367163e68c99ef2fe0|任务期间遮罩、finally恢复；已实现资源modal取消/校验、文件锁保存失败+重试继续接既有门禁|
|父级Escape|5db9394f10c435039aada709ca60f34303cbe2d8|工作区优先取消灯位手势，避免父级提前吞键|

资料1498文件只读清点，4份REFERENCES及74PNG全哈希一致。缺独立字体/SVG；内联图标/系统字体不等于交付包齐全。引用缺口逐项见SPEC_REFERENCE_CHECKS；DESIGN_SYSTEM_REQUIREMENTS缺失。原图不复制提交，原件保留；HTML不执行。灯位等无独立r5参考PNG，按spec/同壳参照并列REFERENCE_GAP。

Core完整1682/0/7；最终WPF完整1569/0/24（8m37s）；发布组件专项8/0/0。策划专项18/0/0；失败历史包含新增列旧count断言、加载态资源/字体token缺失和同步完成任务测试假设，修复后重跑，原结果不删。发布主DLL/Core/模块与隔离发布测试哈希一致。

EXE D:/AI AGENT/kitao-planning-company-20261014/artifacts/company-followup/release-sealed/win-x64/KitaoPhotoSelector.exe。
完整发布目录 D:/AI AGENT/kitao-planning-company-20261014/artifacts/company-followup/release-sealed/win-x64。
SHA256 CE91698483F2C5E1B979BF599FF9132E15F12FA7B913AADCE54138BC78D3314E。
产品版本2.3.0-planning-company+5db9394f10c435039aada709ca60f34303cbe2d8。前序release/release-final为中间版本，不当最终身份。

离屏：本发布组件合成镜头/灯位1366与900、进阶、drawer/校验/保存失败状态；全部WORKSPACE_ONLY96dpi，不是屏幕物理DPI。原r5矩阵160逻辑尺寸组合回归执行，截图矩阵不能当全视觉通过。Windows启动/焦点/原生拖动/物理DPI/视觉批准NOT_RUN，遵守当前ComputerUse限制，无EXE自动启动，无后台替代实机结论。

完整Word七页签/分页缩略侧栏/自写重复表头与页眉输出、未实现数据能力、所有r5弹窗像素级对齐仍OPEN，不造假按钮。灯位10×10m明确示意、0–1原坐标与对象ID保留，不冒充实测。状态列在左的差异仍OPEN。

本批只改Planning相关源码/模型及测试，ColorStudio历史数学/其他模块零diff；共享仅PlanningUiLayout。其他工作树dirty/ahead和运行进程均保留，main未合并，未reset/clean/强推。测试生成ui-guardian只本树备份后恢复原Git字节。原件/照片/DB/凭据/构建二进制不提交；合成离屏图为获准证据。
