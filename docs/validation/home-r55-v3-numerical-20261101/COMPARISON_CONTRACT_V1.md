# v3阶段归因合同1（先冻结，后保留样本验证）

本合同仅验证因果归因，不取代旧照片max24/mean1.5/P95=3及ICC max1门槛，也不把原FAIL标绿。依据为探索夹具的AC3 reduced-IDCT响应、WIC默认/PreserveSubsampling平面尺寸和同ICC字节的系统CMM复现。详见COMPARISON_CONTRACT_V1.json。

可等价：同域/同分量采样/同系统CMM参数的字节、方向的纯像素排列、产品冻结身份。
只可量化：FFmpeg低通截断2x2与WIC/IJG面积型reduced-IDCT；同尺寸但色度采样频带不同；Windows CMM8位与LCMS精确矩阵曲线路径。系统CMM内部专有实现未公开，不将观察到的量化特征冒充源码级算法。

后续hold-*夹具不得用于重新拟合容差。若关系验证失败，本合同归因失败，保留阻塞，不改产品迎合参考。
