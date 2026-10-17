# 输入语义、缓存与资源所有权

## sRGB 曝光

入口明确“从原文件创建曝光版本”。读原始已渲染文件并新建RenderedInputVersion2版本，非对当前Look/Stack/Film的可见结果继续曝光。旧版本保留，通过undo/redo还原旧图、参数、模式和历史；不接RAW/V3混合链。渲染前后及提交前校验内容SHA，缓存同时校验source hash/DecodeKey。失败COM/IO/取消统一LastProcessingFailure；busy保持至图像呈现和历史完成。过期revision/目标/version检查后才提交。提交阶段不调用ActivateTargetAsync。

## 缓存边界

准备对象携目标ID、源版本、activation revision、完整definition、source hash、master和冻结显示bitmap。主输入相同DecodeKey且SHA正确时复用。仅RenderedInputVersion2 + Original + 无Look/Stack/Film/LegacyRaw时，历史OriginVersionId不影响像素；严格验证旧contract匹配旧DecodeKey、新旧输入字段等价后重绑contract，不修改历史DecodeKey定义或项目recipe。CurrentEdits绝不使用此等价规则。

对照bitmap键含source SHA、解码域、全definition、方向、尺寸、1600边长及缩图质量；只忽略已证明无像素语义的Original lineage。权重/裁切/缩放/其他可能影响对照的字段仍使缓存失效，宁可重算，不把Amount=0解释为所有参数均可忽略。缓存不保存第二源float。

## 项目加载

完整暂存工作区先读取/渲染；二次校验项目hash和所有源后，移交相同target对象、master与图像，以及编辑器的高精度RAW/交互代理/frozen源状态，不在正式工作区重新LoadThumbnail/Activate。暂存保有owner直到成功，失败由暂存Dispose释放，旧工作区仍可撤销/重做/另存；SessionTransfer故障、取消、关闭测试覆盖。资源owner为target实例独立GUID，不复用持久化target ID，避免暂存/正式账本互相覆盖。旧master在准备期间有pin；成功后旧target释放，暂存从集合移除已移交项才Dispose。停止/关闭取消准备，不重新获取已关闭工作区master。

## 第二照片和账本

TIFF3限产品single-strip little-endian未压缩RGB16、精确sRGB ICC、原512MiB float准入界限不变。只读文件锁+映射原始ushort，逐像素保持ushort/65535f运算和方向，八方向逐码同旧读取；不降尺寸、不量化新副本、不使用缓存proxy。验证hash/profile/layout后开映射，session活跃借用归零才释放view/file。其他输入1/2沿旧解码，不冒充通用TIFF。

映射全像素字节与行scratch照计入未提高的2GiB总预算，文件映射page由OS按需驻留；working set可能包含共享文件页，private不含完整float副本。资源账本不等于进程内存上限；报告独立进程private/working-set时序，不以归零证明没有原生缓存/泄漏。正式构建测试执行完后才封存，不混用旧10/16构建峰值。

旧RAW回退及项目移交失败使用同一PreparedSourceState恢复源bitmap、float RAW代理、frozen master和交互代理，避免表面图像恢复但下一次编辑退到8位路径。新增synthetic.arw的真实编辑命令结果对照及反射输入身份检查；不把合成RAW测试说成相机原生解码证明。
