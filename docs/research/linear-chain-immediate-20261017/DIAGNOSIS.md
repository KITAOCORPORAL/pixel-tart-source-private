# 原失败定位：逐阶段、非裁切掩盖

原始PhotoColorMathV1不变：decode在x<=.04045用x/12.92；encode在L<=.0031308用12.92L。固定colour0.4.7实际decode调用eotf_inverse_sRGB(.0031308)作为切点，值.040449936；故(.040449936,.04045]选择pow分支，.04045解码从本项目.0031308049535603713变为.0031308072830676845。误差不是encode或曝光率2^EV本身，阶段放大在原失败结果保留。

OCIO2.4.2 monCurveFwd按offset/(gamma-1)=.055/1.4≈.03928571428571429推切点，低端斜率由切线连续推导，不是12.92的倒数。换offset不能同时满足sRGB舍入cutoff/斜率/幂段常数；不能继续把ExponentWithLinear当严格等价参考。OCIO float32输入/CPU与double也独立列出，不用最后clamp掩盖负值/高光差异。

新参考对照：colour仅配置其内部scalar decode阈值.04045，向量公式仍原库；独立scikit-image0.25.2原rgb2xyz/xyz2rgb函数保留，矩阵设identity隔离transfer；为记录未裁切encode仅去掉末尾np.clip，未重写任何transfer算式。不是拿项目函数充第二参考。scikit原mask>x cutoff与项目<=分支等价。两库逐阶段decode/exposure/encode/clamp最大误差0，沿用2e-10，不放宽。含原.04045、两cutoff两侧与double/f32相邻、灰阶/原色/负/>1，原colour/OCIO逐阶段差异同表保留。

上述仅数学定义匹配，真实输入/产品/来源完整commit仍须准入合同补证。
