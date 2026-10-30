# 节点开源对照：BT 断点兼容性（公司第四批）

- 节点/版本：隔离 `SpektraBtJoinFloatV1`，只固定 Vulkan decode 的 mode3 分支断点，不更改 film/paper 数据、其它节点或产品代码。仍为 OPEN_RESEARCH_ONLY。
- 输入/输出：单通道 IEEE754 float32 编码RGB→线性RGB；模式来自 BT.709/BT.2020（旧10bit常数约定）；不改原色/白点/alpha/方向。负数沿原signed-power/线性规则；无额外clamp。最终ABI仍RGBA16 clamp。
- 参考1：Colour 0.4.6，commit `0c6f868c4b5c5ae861f868ba8015be3abf0f92f0`，`colour/models/rgb/transfer_functions/itur_bt_709.py:oetf_BT709/oetf_inverse_BT709`委托`itur_bt_601.py:oetf_BT601/oetf_inverse_BT601`；BSD-3-Clause。实际调用安装在隔离目录的此版本。其double rounded常数断点0.08124794403514049，不能把它当原float测试期望。
- 参考2：FFmpeg n7.1，commit `b08d7969c550a804a59511c7b83f2dd8cc0499b8`，`libavutil/csp.c:trc_bt709`；文件LGPL-2.1-or-later。只在本地独立probe抽取原函数用clang20.1.2编译并执行；连续常数 alpha1.099296826809442/beta0.018053968510807，负数clamp。不是rounded曲线的可替换oracle。
- 许可证/分发：参考源码/二进制不进产品、不进本仓库；只提交自编调用/变换工具、数值、hash和出处。Spektra修改后代码仍受GPL研究边界约束；产品接纳须另审代码和每项资源权利，不因进程/DLL/LUT自动解决。
- 决策：不混用参考常数，不以“更规范”名义改现有曲线；以bit-pattern显式固定原C++ float兼容断点`0x3da66555`。Colour/FFmpeg差异如实保留。此版本不宣称规范色准，也不改变产品。
- 固定输入：`bt_probe.cpp`围绕原C++断点±6ULP；case114/index13为`0x3da66554`。另两参考执行中性、0、toe/highlight、负值/>1，见`company-round4/bt-reference-comparison.json`。这是局部数学检查，非自然图/ICC全链准入。
- 编译与数值：glslang16.6.0 `-V --target-env vulkan1.0`与同工具`-Os`；完整工具hash、source/SPIRV hash、分支读回和误差见`bt-shader-compilation.json`与`bt-*.txt`。阈值仍1e-7+7e-7*abs(reference)。原-Os失败5.51020106e-5；补丁同条件index13误差6.20881718e-10；完整pointwise通过130cases。常规原版/补丁公司CTest均11/11。
- 时间/内存：CTest有实测墙钟，矩阵只记录本机每case耗时，不作性能收益结论；本批未做独占GPU/VRAM性能门禁。家庭性能不搬用。
- 预览/保存/重开/导出：公司确定性三输入×三胶片9组合，original/patch均重复、新进程resolved-recipe重开、sRGB ICC TIFF16逐像素读回；9组原补丁bit-exact（这些配方未进入BT输入decode分支）。断点probe才是修复像素差异证据。家庭补丁重验、自然验证保留组、Metal全图oracle仍NOT_RUN。
- PV4/5/6/旧recipe：产品src未改；本轮不重跑全套golden，不暗迁。WB余量仍拒绝产品应用，和此补丁不混算。
- 科学边界：CalibrationEligible=false；未做真实相机/胶片校准、C1对照或肉眼缺陷判断。
