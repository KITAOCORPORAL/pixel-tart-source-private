# 节点对照：SharedIncrementEnvelopeUnsharpV1（独立研究，默认关闭）

## 输入输出与位置
只替代固定PortraA的print_scan_finish中unsharp。输入为已保存print_linear：线性sRGB D65 float32，WB/相机表征/胶片/纸张/输出gamut已完成，允许负值与>1，orientation1，无alpha。候选输出同域、无全局clamp；后续原矩阵/OETF/RGBA16打包不改。

## 两个实际参考
- darktable4.8.1，fb0eb997e62c6dd52966cd35911c74afa150e209，src/iop/sharpen.c::process、init_gaussian_kernel、commit_params。GPL3-or-later。Lab L*名义0..100；sigma=UI radius，内部rad=ceil(2.5sigma*scale/iscale)，MAXR12；threshold按L*软阈值，a/b不变，rad外边缘复制。支持tiling overlap=rad。精确函数body在本地harness编译，框架allocator/type adapter自编；不是完整darktable app。
- RawTherapee5.11，db575c6690d30ce6428c0e1383bec8b8f1fb8d21，rtengine/ipsharpen.cc::ImProcFunctions::sharpening/sharpenHaloCtrl；procparams.h::Threshold::multiply；gauss.cc真实标量Gaussian；rt_algo.cc真实buildBlendMask。GPL3-or-later。L内部=327.68*L*；本次radius.7/scale1/amount70%，contrast=0明确走原all-one mask分支，不自造mask；原threshold[20,80,2000,1200,false]及open控制；halo=100控制；rld/edgesonly/preblur关闭且未实现分支调用会throw。标量CPU非SSE，Gaussian在.7进入原递归边界，并非当前FIR5x5；halo额外依赖局部平滑滚动环境，2px外边复制。两者不能按相同radius就视为相同算子。

文件/依赖/编译器/精确函数body和exe hash见reference-build-v2.json；实际45调用结果reference-numerical.json。9夹具覆盖flat/step/impulse/saturatededge/negative/outofrange/border/lowdetail/detail/noise。原源/生成harness/二进制只本地。负L*/>100为越域压力，不是合法线性RGB对应样本。完整app管线、ICC转换和SIMD一致性NOT_RUN。

## 决策与独立策略
阅读与运行显示：threshold抑制小差分/噪声但不保证大阶跃无下冲；halo需要局部环境约束而不是末端clamp。选用**一项独立写的线性RGB共享增量包络约束**，不移植Lab阈值或RT滚动滤波公式。以原legacy U-I为RGB增量，所有通道共用最小允许gain使结果留在原5x5局部包络；gain1直接返回legacy以保留其精度，gain0返回原像素，受限gain仅保守缩小2^-20安全量。不抬黑，不改变global输出范围，不复刻darktable/RT色彩科学。

## 许可证与准入
GPL参考仅本地行为/数值研究，不复制产品；外部引擎core/GPU代码和派生research helper仍本地。新shader独立创作，但与GPL研究执行器组合的分发未批准；不提交binary、不注册product。profile/厂家数据权利另门禁。数学/技术试验不代表商业兼容审查。

## 冻结与门槛
候选定义candidate-definition.json，测试计划evaluation-plan.json在候选运行前固定。先合成再6既有开发代理A/B；原stage/encoded/RGBA16须与旧结果全通道0差，whole/stream17/37也须float+RGBA16全通道0差，不放宽阈值。只支持ratio1 sigma.7/radius2/halo2；其他preview尺度未批准。对细节、光晕、色相、肤色与噪声分别测量，不以零值少判胜。

## 旧结果与边界
产品src/UI/default/任务桥/PV4/PV5/PV6不改；第十批链/输出/回执/失败不改；不重跑全套golden。候选stage保存重开/输出回读做研究验证，产品预览/项目重开/导出仍NOT_RUN。CalibrationEligible=false，6图不改称独立验证。初始准入VERIFIED_REFERENCE_LOCAL_RESEARCH_ONLY，最终候选判定另见报告。
