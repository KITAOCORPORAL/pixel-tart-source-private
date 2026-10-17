# 实际风险覆盖与未完成项

本轮真实风险取Sony_DSC08015/Canon/冻结Fuji，明确SHA验证、不遍历原件目录。选择旧风格Stock0 Creative2 +圈窗Paper3 +1.17非整数zoom +窗口双rim +低强度Bands +暗角；每张完整分辨率直接TIFF16/PNG16/JPEG、512产品预览、64原像素区域、会话JSON重开。各格式独立直接render，不研究TIFF转码；逐格式编码ms/校验ms、进程peak、ledger和原件hash见AUTHORIZED_FULL_RISK.json。

三组全部通过；产品preview vs 完整TIFF线性光area最大约2.17e-6 <固定2/65535。PNG/TIFF逐码0；JPEG沿用MAE5/RMSE15，不删除校验。真实WIC过程内存只是实际进程观测，不用OnDemand/CopyPixels推断无内部整图缓存。关闭编码session/条带后才进行校验session，未共持两套第二源。

合成覆盖：组件1兼容、Womb矩阵Base+6stocks八参数、新窗partialcoverage、1.37非整数zoom/highfreq及不同条带精确一致、BW温度/漂移/缺陷、selectedNone/0、二源三格式/变更诊断、真像素/编码/校验握手取消。未穷举组合，不声称全扩展审美或全部分辨率aliasing都已过；缺LUT/第三方贴图照旧不伪造。

当前缺口：此前声称四份Resolve附件，实际明确定位仅一个ZIP（12成员CRC）；未找到的三个独立原附件不编造。colour/OCIO安装固定版本和源码module hashes实际运行，但完整git tagSHA网络查询失败需补；公式库不接旧V3/场景RAW。新页focus颜色非真正计算蒙版颜色。
