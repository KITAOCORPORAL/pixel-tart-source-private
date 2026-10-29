# 公司R5.4本轮节点行为与资源生命周期核查
日期2026-10-29 Europe/Berlin。

## 像素域/数学保持
Graph2仍是encoded sRGB D65 float32 RGB调整结果图；WB/Display沿原输入阶段，source alpha仍属caller；base先layer后，强度*mask调用原StudioLayerComposite，不更改混合公式。内存优化不改变route顺序/颜色范围/位深/最终尺寸。旧PV4/5/6和无新组件的V3 tone仍原数学。节点graph局部精确长度buffer池不量化、不跨图保存；全填写之前不读取已复用旧值。

## 中间结果所有权
- 共享祖先一次计算；按有序边消费者计数，最后消费者结束才Dispose。diagnostic callback仅借用当次输入/输出。
- source借用不释放。普通节点effect、composite的临时异常安全；Mix alias被复制为明确独占输出。最终output转交caller，caller Dispose释放账本。
- 每个实际分配的RGB backing（含池free但仍retained数组）都有Work lease；scope结束回收free/未被HighBitBuffer认领的临时，escaped输出的lease由其Dispose管理。不是“释放结果但仍全部N节点预留”。
- rowband准入按PeakBackingBuffers估计最大live集合；生产预览每worker图scope复用小行带，不创建每行全新图池。范围分析和位图/TIFF适配器消费ProcessingPixels后Dispose。
- 取消/异常/回调失败与重复渲染账本检查；大图实际结果见性能文件，不用小图替代。

## 固定参考与本次对照
既有图合成两参考仍固定Pillow12.3.0 bb1d8e8ab8d29048624d96e3ee53cecf7c13d13d::ImageChops/Chops.c（MIT-CMU）和OpenCV4.10.0 71d3237a093b60a27601c20e9ee6c3e52154e8b1::arithm（Apache2）。本批未改它们已有math，新增golden为公司基线73ac（product06e33）的原程序集：完整20MP旧rowband结果float SHA256，与新full/生产preview逐位比较，另有mask/bypass/group的all-retained oracle。

## 两个可选色彩组件（A/B门禁之后）
仅新ColorComponent不为null时替换旧CandidateTone，采用研究0146c8d3第一方公开参数近似，不复制GPL/第三方LUT/测量图；旧V3无需重新计算。来源JSON固定原字节，数据manifest hash9217033eb8ec98d2f33243f6b377daceb6ec46ece39ad3c18148bf551a655334，原研究个别条款是内部离线候选，不等于最终科学批准。
新组件域encoded sRGB→一次EOTF→线性单调toe/shoulder+neutral preserving chroma→既有空间效果→一次OETF，屏幕ICC不进入模型，具体公式/参数与原Python候选4096×2点max abs<=2.78e-16。旧Womb/tone色偏/温度外观不叠加，未知hash/版本拒绝，recipe嵌完整JSON/hash/数据hash。内部Candidate AlgorithmVersion4让旧读取器显式拒绝，外层仍existing V3 pipeline。不称相机/光谱/实测，SX70/实验相纸不强配，机身不加任意色偏。
色彩更改是EXPLICIT_DIAGNOSTIC_STYLE_APPROXIMATION，无控制相机配对，CalibrationEligible=false。新16bit/float真实样本拟合入口仍独立后续，不声称本轮完成。
