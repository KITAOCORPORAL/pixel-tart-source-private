# 公司 TIFF 与 sRGB 事务修复回执

2026-11-16，Europe/Berlin。状态：**PARTIAL，等待普通 App 原生导入**。

产品整合基线为 0a45ee0e7971c18c6bb4b7e287bced86c448263b，已含 edd6d25e→17a95b5a、e28074f1→14eda5c7。Session.cs 相对 e28074f1 的差异仅保留 WorkflowProjectId；源缓存移交、清空和失败回滚保持。原公司工作树未提交修改、主线 PID52544、workflow-runtime 及预约未操作。

## sRGB 最小修复

修复提交 a9932b7333b6e04915f9679805ff028f2bd94eaa。旧基线 bdc913c9 在公司重新构建并复现 SrgbUsesOriginalFileNotCurrentEditsAndUndoRestoresPixels，错误与家中保留记录相同。

准备完成的预览安装快照时，ApplyTargetSnapshot 在新调整栈安装之前发送 SelectedLook / 调整通知，旧编辑栈被同步写回新 sRGB 版本，触发局部层不支持参考匹配的异常。使用既有 _applyingActiveSnapshot 保护这段同步恢复，并在 finally 恢复原保护值。曝光公式、渲染器、recipe 版本、ICC 与编码器未变。

原图输入与当前调整区分、曝光数学、撤销/重做、保存/重开、两次产品命令实际 TIFF 导出，以及准备/提交失败回滚均已回归。整合源码 Release x64：PreparedPreviewTransactionTests 18、LinearSrgbAndImmediateTests 2、RasterPreviewNativeCacheTests 8，合计28/28。旧基线失败与本轮 TRX 见 tests/。这些是 WPF 事务回归，不代表普通 EXE 的曝光屏幕验收已完成。

## 公司原授权样本

DSCF0347_仿色.tif 的文件SHA、全部5184行RGB48像素SHA和3144字节ICC SHA均与原交接一致。尺寸7752×5184、每通道16位、方向1。INPUT_ROW_PROBE.json 为只读WIC探针结果；探针进程在2GiB守卫下exit0，无STOP。它没有相机RAW解码器依赖。

家中 d64948d6 的合成40MP和Sony两轮窗口类/重开证据予以复用；其固定对话框、离屏受控宿主边界保持，不提升为公司普通App验收。

## 当前真实入口断点

普通Release App按独立根启动，运行日志确认 ProductSourceSha=a9932b7333b6e04915f9679805ff028f2bd94eaa。唯一apphost为 KitaoTiffCompany20261116.exe，PID4532。已准备暗室页面，尚未导入任何媒体。仅在上一个实例正常关闭后，将本次隔离settings.json的LastPrimaryPage设为ReferenceColor，用于准备入口；未直接装载图片或注入对话框结果。

截图捕获超时；原生点击返回 coordinate input geometry is unavailable；键盘焦点变换不可靠。需要用户在此独立窗口通过“导入 / 导出 → 导入照片”选择唯一授权TIFF。2GiB/50ms守卫仍运行，无强制GC。当前采样只代表启动和导入准备阶段，不是40MP载入、缩放或导出通过。

普通App导入、适合、100%、缩放、实际导出、保存关闭、独立进程重开、再次导出，以及两份输出完整逐行比较仍为NOT_RUN。最小化恢复和带TIFF自然空闲阶段也待完成。无媒体导入，新增RAW解码0；普通App尚未添加独立解码计数仪器。

## 回传

本机结果根：E:/Kitao-Deliveries/tiff-srgb-company-20261116。RESULT.json、FINAL_BINARY_BINDING.json、ordinary-final-first原始采样与准备中的exports-first / exports-reopen / sessions用于接续。工具见tools/CompanyTiffAcceptance。由产品Codex整合同一全模块包；本会话未发布分叉产品包。高光1bfdfbe9保持研究状态。
