# 人工验收

RELEASE_STARTUP=NOT_RUN；DESKTOP/DPI=NOT_RUN；USER_VISUAL=NOT_RUN；USER_ACCEPTANCE=NOT_APPROVED。遵守现有禁止Computer Use/Browser/WebView/CDP/Explorer，不用替代自动化绕过。WPF命令与文件probe不等于桌面体验。

1. Release用授权副本打开旧PV4会话，确认same definition full TIFF与既有成片像素保持；PV1/2旧照片外观不变。
2. Fit缩小观察细线、棋盘格/织物、暗人像和高饱和边缘：检查area代理对nearest的改善，以及亮度/边缘是否可接受；不期待proxy/full同坐标exact。
3. 100%按钮在100/150/200%DPI应显示物理100%，图像坐标来自真实尺寸；移动时明确代理占位，停下约140ms后更新原像素块，不能认为整个代理自动高精度。大屏超过4Mi像素块预算时查看状态提示/块范围，未覆盖处仍代理。
4. 连续拖动、缩放、切照片、修改EV和WB/切版本；旧patch不得覆盖当前图，停止后HQ1600/原像素detail应更新；真实交互响应是否满足摄影工作流待人工。
5. 快速导出TIFF16/PNG16/JPEG95；检查原尺寸/朝向/sRGB profile、95/444与实际manifest。JPEG允许已声明有损，不与PNG/TIFF容差混用。
6. Publishing原尺寸三格式（TIFF16/PNG8/JPEG配方quality/DPI）核验；PNG16或非sRGB输出应明确拒绝。缩放/水印既有路径需要独立pixel与视觉检查，当前PARTIAL不要当PASS。
7. 导出途中停止或失败应清本次编码临时资源、保原照片/已有文件；清单阶段失败可恢复。不手工删除缺可信记录临时文件冒充恢复完成。
8. 多RAW批次切换/导出观察内存；本机独立Sony流式TIFF累计峰值约0.91GiB，但PNG/JPEG读回、Publishing和整个应用仍可能更高，不能按隔离数字承诺整应用峰值。

缺少真实灰卡/曝光阶梯、未知机/已WB小RAW，需要新增明确授权样本；火焰人像不代替中性灰标定。HDR另批，当前压高光不叫恢复细节。
