# 预览复用、项目移交与原尺寸内存：验收报告

## 当前基准与交付身份

2026-10-17（Asia/Shanghai）承接前工作树干净，fetch与独立ls-remote均确认为1a2ca91716d0680afa6357a0730a5bcda156a805，没有后续远端提交。原safety-review报告开头的GitHub连接失败已明确改成历史阶段，与后续正常push/独立核验分开。本轮源码按阶段提交，未重做已关闭的数据安全功能。

冻结产品源码：72e916dcef46100c1fb6d96e8ff61745d6fb1d29。最终290文件诊断发布：`N:\pixart\kitao-v3-corrections-colorpage-20261017\artifacts\preview-reuse\22-delivery\release\win-x64`。EXE SHA256：1061CF07B681F4DCB91B314BF22DE070E8A4C16BAF9ED5920A44BE5DBB9EFB9F。工具加载本次发布DLL后逐文件比对，见FINAL_AUDIT_BINDINGS.json；完整回归也替换为相同交付DLL。诊断状态不是正式发行批准。

## 删除的重复计算

- GenerateInstantFilmAsync不再算完预览又ActivateTargetAsync。PreparedProfessionalPreview携master、完整冻结definition、source SHA、target ID、来源version、revision和两张冻结显示图；检查取消/源身份/版本后才统一呈现，再入一次undo历史。
- sRGB曝光不再丢弃先算的preview；同一路径直接呈现。提交busy直到呈现/历史完成；失败恢复图像、source、模式、Look/Stack/Film、版本和编辑器历史，过期结果不回填别的照片。COM/IO/取消使用统一LastProcessingFailure。
- 项目只在暂存工作区完整准备一次，正式工作区移交同一组target/master/bitmap，不重复thumbnail/激活。实例独立resource owner，避免相同持久ID将暂存/正式账本覆盖。准备旧master pin，成功释放旧target，失败暂存释放且旧工作区仍可undo/redo/另存。
- 对照bitmap缓存键保留完整相关definition、源SHA、DecodeKey、方向/尺寸/边长/缩图规则。仅Original raster历史lineage作经证明的输入等价；DecodeKey定义及历史recipe不改。权重/crop/zoom仍失效，未冒险把Amount=0解释为所有参数无关。
- TIFF3第二输入用只读映射原RGB16代码，取消每次395,367,168字节完整float副本；无缩图/无随机种子变化/无配方迁移。映射原像素197,683,584字节仍按潜在驻留计入原资源预算，八方向逐码与旧读取一致。其他输入1/2保留。**专用产品未压缩RGB16/精确sRGB TIFF3，不是通用TIFF。**

## 同授权双照片、同输出的实测

Canon5496×3670，Sony4688×7028，输出4220×4863，与基线完全相同。授权原件/冻结hash核对，无扫描私人目录。

|操作/阶段|基线|最终|调用变化|
|---|---:|---:|---|
|首次应用总耗时|40,884.945 ms|28,151.496 ms|减少12,733.449 ms（31.14%）|
|输入解码|658.490 ms|685.790 ms|master 1→1|
|效果预览实际调用合计|26,600.363 ms|13,456.417 ms|2→1|
|原图对照实际调用|12,693.458 ms|12,644.318 ms|1→1，首张仍必须计算|
|提交阶段|26,273.585 ms|488.795 ms|不再激活重算；与渲染阶段存在包含关系，不能相加|
|界面bitmap呈现/分析观测|198.839 ms|198.407 ms|1→1，非物理显示延迟|
|项目重开|80,686.114 ms|29,140.269 ms|减少51,545.845 ms（63.88%）|
|重开master解码|4次|1次|不重复准备/丢弃active thumbnail master|
|重开效果/对照/呈现|各2次|各1次|正式工作区只移交|

首次操作SecondDecode总调用4→1；其中第一源RenderedSrgbInput的TIFF3读取1→1，真正第二源full-float读取3→0。最终另有2次SecondMappedOpen（效果/对照各一次）。观测计时包含子阶段和后置身份检查，不将嵌套耗时相加。原图对照首张约12.8秒尚未消除；不宣传零计算提交。

连续两次权重改参 .37→.52→.37：28,269.035 / 31,182.916 ms，master命中，实际解码0次；每次效果/对照各1次，均新建1条预期版本。权重影响对照所以未命中对照bitmap，安全优先。改回.37后完整preview hash等于初始。小图同配方重复应用测试对照1→0次，改变输入/域/权重不误用缓存。

**以上均为单次独立进程实测，不作统计置信或其他机器速度承诺。** 基线与最终环境同机但非CPU独占（另有回归进程），不作多次统计加速保证；最终额外运行了两次连续应用，因此全程309.5→331.2秒不能拿来称整体更快。操作级比较用同操作窗口。

## 最终构建的独立进程内存

原始250ms采样1323点，FINAL_PROCESS_MEMORY_SERIES.json；阶段账本/时钟见FINAL_LIFECYCLE_WORK.json。未混用10/16/18/20构建峰值，未通过GC强制回收或调大预算制造改善。

- 基线private峰4,664,594,432 bytes，最终2,106,720,256 bytes（约下降54.84%）。
- 基线working-set采样峰4,548,444,160 bytes，最终2,138,370,048 bytes（约下降53.00%）；最终Windows生命周期峰2,138,861,568 bytes。
- 冷启动观测private17,666,048 / working-set59,432,960 bytes；源加载后389,545,984 /442,777,600 bytes。
- 首次应用后private955,445,248 / working-set1,005,375,488，账本242,043,840（第一source float）。
- 连续改参完成后private931,012,608 / working-set976,359,424，账本244,806,180。
- 项目重开后private1,310,306,304 / working-set1,359,986,688，账本244,806,180。
- 三出口期间的前后private/working-set与账本事件逐项保留，不猜测编码器内部峰值的具体归属。
- 关闭后账本0，但private仍1,009,672,192 / working-set1,040,248,832 bytes；**这不是无泄漏证明**。运行时堆/native WIC缓存及审计局部引用仍可能保留；未声称进程立刻回到冷启动。该审计也保留用于一致性检查的少量preview/region，明确不把其账本当产品泄漏。

## 输入语义与事务

**从原始已渲染文件创建新曝光版本，不在当前可见Look/Stack/Film上叠加。** UI中英说明及按钮已改，旧编辑保留为前版本、undo/redo可恢复像素及模式。不是场景RAW/DWG/通用ICC。主源缓存验证SHA和DecodeKey，渲染前后与提交前验证真实文件；取消、切图、COM解码故障、呈现故障、关闭、停止按钮、项目移交失败均有真实命令测试。输入所有权细节见INPUT_AND_OWNERSHIP.md。

## 像素及独立参考

完整三格式文件SHA均与基线相同；preview hash同、preview对完整TIFF按线性面积缩图max 2.165012152555157e-6 <=2/65535。PNG16/TIFF16逐码差0；JPEG MAE.518650/RMSE.898402，未放宽5/15。第二照片权重/crop/zoom同坐标区域hash与基线逐项一致。不把重复导出取代preview-full检查。

新曝光数学沿已有准入，最终Core DLL逐阶段colour/scikit独立比较288组最大0、门槛2e-10；3授权真实输入中心64×48三格式独立文件像素比较TIFF/PNG逐码0、JPEG最高MAE2.215603/RMSE3.076834，沿5/15。Fuji冻结输入不证明原生解码重复性。Formula/旧PhotoColorMathV1未改。

## 回归与仍开放边界

最终套件计数已逐项与原始TRX、discovery及测试assembly SHA核对，无缺失、无超时：

|套件|通过|执行失败|条件未运行|
|---|---:|---:|---:|
|源码专项|60|0|0|
|发布DLL专项|60|0|0|
|源码Core|1706|0|7|
|发布Core|1707|0|6|
|完整发布WPF|1651|0|24|
|候选独立测试|25|0|0|
|逻辑DPI|91|0|0|

含NotExecuted的套件整体runner仍FAILED，不能报“全绿”；条件原因见06_regression/CONDITIONAL_NAMES.json。原始2640项文本测试/测量证据ZIP已逐成员hash和解压回读，见06_regression/SEAL.json，无照片/RAW/数据库/构建二进制。旧14次关闭异常失败以及05/06/07的过强缓存假设失败保留，不覆盖原结果。历史V1/V2、PV4/5/6与组件版本由完整回归维持，不改旧golden。新增旧RAW项目移交与失败回滚后继续编辑像素对照，防止只恢复显示图而丢失高精度输入。

Fuji原生重复性、显示ICC分歧、物理DPI/前台验收、完整LUT/贴图、真实断电/满盘/网络多主文件锁仍开放。当前仍需计算首张对照，不声称通用RAW/ICC全部完成。不改系统ICC/DPI，不Browser/WebView/CDP/ComputerUse，不打开浏览器/资源管理器。

推送与最终独立HEAD核验另见PUSH_STATUS.md；不强推、不合并main，不上传照片/RAW/数据库/构建二进制。
