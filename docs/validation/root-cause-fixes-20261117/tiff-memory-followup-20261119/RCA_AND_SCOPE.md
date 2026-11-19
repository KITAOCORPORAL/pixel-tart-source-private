# 普通40MP曝光越限与必要存储修复

## 真实失败

原433普通EXE已载入授权7752×5184 RGB48 TIFF。旧工作文件保存了关闭的参数面板；初次AX读取旧/不可见控件，加上不启用的数字字段，曾被过早推断成仍在准备。只读线程栈和本地堆快照证明_sessionLoading=false、_isLoading=false、活动目标/源存在、根_closeTask=null，推翻了加载死锁假设。普通原生新快照随后读到DSCF0347和已保存。这个排查过程没有执行GC或注入业务命令，堆快照只在本地，不进入Git/产品包。

打开现有参数面板后，对可见CompactStudioSlider-BasicTone-exposure做原生RangeValue0.1提交，普通App在后续预览阶段触发原2GiB守卫。首个记录PrivateBytes2424029184，exit1/STOP明确；输入TIFF不改，Jpeg验收根不动，0.1在Guard终止前已经自动保存进独立副本，另存原字节恢复文件。详见ORDINARY_433_EXPOSURE_STOP.json。不将此记成功或exit0。

## 分配链与修改边界

TetherReferenceModeViewModel.PumpInteractiveEditsAsync先用800px代理，然后300ms后请求_source的整帧高质量渲染。ColorStudioBitmapRenderer.RenderHighPrecision同时建立全幅RGBA64 byte[]、ushort[]、RGB16数组、RGBfloat master、pipeline clone/effect、显示RGB24和输出样本。在40MP下这些并存的整帧副本足以越限；不是新的RAW解码或关闭请求重入。

必要修复只改ColorStudioBitmapRenderer.cs的存储适配。对超过4Mi像素、ProcessingVersion2、无reference、无graph、单一BasicTone且只含exposure参数、无宽域/胶片元数据、图层合成为恒等的场景，按约1MiB行块读取RGBA16。复用现有ColorStudioRenderPipeline.ApplyPointEffect→ColorStudioToolProcessor.Apply，不写新曝光/肩部公式。原16位到float归一、float到16位Math.Round及alpha样本保持；原DPI、尺寸、格式保持。输出临时原生缓冲在finally释放，WIC发布前冻结，行float用既有GraphPixelAllocation局部作用域复用，临时输出/复制预留走既有StudioGlobalResources。

其余节点、蒙版、空间滤波、非恒等合成、图路由、宽域、reference、float源和多参数节点不进入新路径，保留原路由。没有用减尺寸预览替代完整导出，没有清他人共享缓存，没有强制GC、提高2GiB阈值或改UI。原高位深整帧函数保留作为不改动参考，PV/recipe数值实现不变。

这不是通用全节点分块或色彩算法接入。它仅消除已复现单曝光路径的冗余全幅中间缓冲；其他大型节点路径仍需按各自空间/全局统计合同验证，不能由本次修复笼统担保。

## 主动反证

- RGB48与RGBA64、alpha0/1/32768/65534/65535、多个EV值、跨行块/最后不满块，与旧整帧函数逐位比较。
- 2051×2049 fixture走实际public dispatch，旧整帧参考与新路径完整RGBA64摘要相同；冻结后跨线程读取。
- 蒙版、非恒等混合、graph、多节点/Details、多参数和旧processingVersion不准入。
- 分配前及行处理中的取消都不发布半成品，局部预留恢复原值。
- 对既有授权40MP全5184行再与旧函数逐行参考比较，内存另计；它只证明文件/数学/资源合同，不替代普通窗口验收。

受控通过之后才冻结源码并重建同一产品线的完整包；旧433原包/失败仍保留，不覆盖旧source标签。新包必须用新源码版本，不能把修复DLL塞入433目录冒充原包。普通TIFF编辑/保存/关闭/独立重开/实际两次导出仍要在修复包复测，当前尚未标PASS。
