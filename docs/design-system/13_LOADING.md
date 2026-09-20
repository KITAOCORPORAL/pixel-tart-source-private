# Pixel Tart Studio UI — 加载

PixelTart.LoadingRing / ProgressBar / LoadingOverlay。未知进度不报百分比。目标：<300 ms 不闪烁，300–1500 ms 轻提示，更久给明确状态。现有操作的实际延时与反馈仍须计时审计；组件存在不等于计时达标。文案：正在载入照片…／正在生成预览…／正在导出…。

实施资源：`src/RAWSelectionAssistant/Resources/DesignSystem/Studio.Controls.xaml`、`Studio.Symbols.xaml` 及现有兼容字典。
状态：v1 实施中，用户确认待完成。全应用推广为 PARTIAL；本文件不是验收 PASS。
