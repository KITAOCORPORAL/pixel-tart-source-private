# Pixel Tart Studio UI — 加载

PixelTart.LoadingRing / ProgressBar / LoadingOverlay。未知进度不报百分比。共享 StudioBusyFeedback 只控制呈现，不延迟操作、不更改取消语义：<300 ms 不显示；300–1500 ms 小转圈；之后明确进度条配合业务状态。完成或卸载立即复位。文案：正在载入照片…／正在生成预览…／正在导出…。

参考仿色解码/渲染、联机仿色、发布预览/导出已接入 PixelTart.ProgressBar.Delayed；素材库只延后遮罩显示，其内部仍为现有未知进度条。RAW 匹配保留原状态反馈，尚未接入分阶段组件。没有宣称全应用加载策略已全量闭合。

布局必须预留 20 DIP，不能继续套旧进度条 3 DIP 高度。IsBusy 用于命令禁用；IsVisible 用于延迟视觉反馈，两者不可混淆。测试分别记录 BusyStateStartMs 与 IndicatorStartMs；短操作 IndicatorStartMs=null 是预期结果，不是未响应。

实施资源：`src/RAWSelectionAssistant/Resources/DesignSystem/Studio.Controls.xaml`、`Studio.Symbols.xaml` 及现有兼容字典。
状态：v1 实施中，用户确认待完成。全应用推广为 PARTIAL；本文件不是验收 PASS。
